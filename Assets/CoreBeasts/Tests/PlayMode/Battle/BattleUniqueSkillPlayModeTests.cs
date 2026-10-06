using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 5「ユニークスキル」を、実際の Battle.unity と本物のラウンド経路で確かめます。
    ///
    /// 期待値はテスト内で仕様どおりに組み立て（製品の評価関数を写しません）、本物のラウンドの結果と比べます。
    /// CPU の選出は非公開の値を読むだけで書き換えません。
    /// </summary>
    public sealed class BattleUniqueSkillPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        private const int MaxMatches = 30;
        private const float FrameSlack = 0.1f;
        private const string JapaneseFontName = "NotoSansJP-Bold-CoreBeastsLink SDF";

        private static readonly Regex RichTag = new Regex("<[^>]+>");

        private static readonly string[] HudNames =
        {
            "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton", "HistoryLane", "PlayerWheel",
        };

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleSkillCueView skillView;
        private BattleAttributeLinkView linkView;
        private BattleCombatantView playerCombatant;
        private BattleCombatantView cpuCombatant;
        private BattleTextCatalog catalog;
        private ImpactBurstGraphic impact;
        private readonly List<string> missingCharacterWarnings = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            missingCharacterWarnings.Clear();
            Application.logMessageReceived += CollectMissingCharacters;

            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            SaveTestSquad();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            controller = UnityEngine.Object.FindAnyObjectByType<BattleScreenController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True, "BattleScreenController が参照不足で無効化されました。");

            skillView = controller.SkillCueView;
            linkView = controller.AttributeLinkView;
            playerCombatant = (BattleCombatantView)GetField(controller, "playerCombatant");
            cpuCombatant = (BattleCombatantView)GetField(controller, "cpuCombatant");
            catalog = (BattleTextCatalog)GetField(controller, "battleText");
            impact = UnityEngine.Object.FindAnyObjectByType<BattleFxPlayer>().ImpactBurst;

            Assert.That(skillView, Is.Not.Null, "BattleSkillCueView が配線されていません。");
            Assert.That(playerCombatant.SkillLabel, Is.Not.Null);
            Assert.That(cpuCombatant.SkillLabel, Is.Not.Null);
            Assert.That(impact, Is.Not.Null);

            AssertCueAtRest("開始時");
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CollectMissingCharacters;
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 発動条件と効果 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator EverySkillActivatesExactlyUnderItsConditionInRealRounds()
        {
            HashSet<string> covered = new HashSet<string>();
            string[] needed =
            {
                "CrimsonBite:on", "CrimsonBite:off", "TidalHowl:on", "VerdantFang:on", "VerdantFang:off",
                "StormBite:on", "StormBite:off",
            };

            yield return Play(
                () => Array.TrueForAll(needed, covered.Contains),
                PickForCoverage(covered),
                (round, frames) =>
                {
                    foreach (Side side in new[] { Side.Player, Side.Cpu })
                    {
                        UniqueSkillActivation skill = side == Side.Player ? round.Result.PlayerSkill : round.Result.CpuSkill;

                        if (skill.Kind != UniqueSkillKind.None)
                        {
                            covered.Add(skill.Kind + (skill.Activated ? ":on" : ":off"));
                        }
                    }
                });

            foreach (string n in needed)
            {
                Assert.That(covered, Has.Member(n), n + " を実際のラウンドで確かめられませんでした。");
            }

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator BothSidesCanActivateTogether()
        {
            bool seen = false;

            yield return Play(
                () => seen,
                PickForCoverage(new HashSet<string>()),
                (round, frames) =>
                {
                    if (!(round.Result.PlayerSkill.Activated && round.Result.CpuSkill.Activated))
                    {
                        return;
                    }

                    Assert.That(frames.Exists(f => f.SkillPlayerText.Length > 0 && f.SkillCpuText.Length > 0 && f.SkillPlayerAlpha > 0f && f.SkillCpuAlpha > 0f),
                        Is.True, "双方が発動したら同時に出します。");
                    Assert.That(frames.Exists(f => f.GraphicPlayer && f.GraphicCpu), Is.True);

                    seen = true;
                });

            Assert.That(seen, Is.True, "双方が同時に発動するラウンドを作れませんでした。");
        }

        // ---------------- 時間 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator TheCueRunsDuringTheMoveAndEndsBeforeTheImpactWithoutLengtheningTheRound()
        {
            List<float> withCue = new List<float>();
            List<float> withoutCue = new List<float>();

            yield return Play(
                () => withCue.Count >= 3 && withoutCue.Count >= 3,
                PickForCoverage(new HashSet<string>()),
                (round, frames) =>
                {
                    int impactFrame = frames.FindIndex(f => f.ImpactShowing);

                    Assert.That(impactFrame, Is.GreaterThan(0), "接触の演出が出ませんでした。");

                    float toImpact = frames[impactFrame].Time - frames[0].Time;

                    if (!(round.Result.PlayerSkill.Activated || round.Result.CpuSkill.Activated))
                    {
                        Assert.That(frames.TrueForAll(f => !f.SkillPlaying), Is.True, "発動しないラウンドでは演出を出しません。");
                        withoutCue.Add(toImpact);
                        return;
                    }

                    Assert.That(frames[0].SkillPlaying, Is.True, "DEPLOY と同じフレームで始めます（待ちを挟みません）。");

                    int last = frames.FindLastIndex(f => f.SkillPlaying);

                    Assert.That(frames[last].Time - frames[0].Time, Is.LessThanOrEqualTo(BattleSkillPresentationPlan.CueDuration + FrameSlack));
                    Assert.That(last, Is.LessThan(impactFrame), "接触（Impact）より前に終わります。");

                    withCue.Add(toImpact);
                });

            Assert.That(withCue.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(withoutCue.Count, Is.GreaterThanOrEqualTo(3));

            // スキル演出は待ちを足さないため、DEPLOY から接触までの時間は発動の有無で変わりません。
            float average = 0f;
            float averageWithout = 0f;

            foreach (float t in withCue)
            {
                average += t / withCue.Count;
            }

            foreach (float t in withoutCue)
            {
                averageWithout += t / withoutCue.Count;
            }

            TestContext.WriteLine("deploy→impact with skill " + average.ToString("F3") + "s, without " + averageWithout.ToString("F3") + "s");

            Assert.That(average, Is.EqualTo(averageWithout).Within(0.06f), "スキル演出でラウンドが延びています。");
        }

        // ---------------- 表示 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ThePowerDisplayAndTheResultReasonUseTheSameBreakdown()
        {
            bool checkedReason = false;

            yield return Play(
                () => checkedReason,
                PickForCoverage(new HashSet<string>()),
                (round, frames) =>
                {
                    RoundResult r = round.Result;

                    // 段の表示: LINK と自分のスキルは内訳と同じ数値です。
                    Assert.That(frames.Exists(f => f.PlayerStageSkill == SkillText(r.PlayerSkill)), Is.True, "PLAYER 段のスキル表示");
                    Assert.That(frames.Exists(f => f.CpuStageSkill == SkillText(r.CpuSkill)), Is.True, "CPU 段のスキル表示");

                    if (r.Decision != RoundDecision.PowerComparison || !(r.PlayerPower.HasModifiers || r.CpuPower.HasModifiers))
                    {
                        return;
                    }

                    Assert.That(r.PlayerPower.Link, Is.EqualTo(r.PlayerLink.BonusPower));
                    Assert.That(r.PlayerPower.SelfSkill, Is.EqualTo(r.PlayerSkill.SelfBonus));
                    Assert.That(r.PlayerPower.OpponentPenalty, Is.EqualTo(r.CpuSkill.OpponentPenalty));
                    Assert.That(r.CpuPower.Link, Is.EqualTo(r.CpuLink.BonusPower));
                    Assert.That(r.CpuPower.SelfSkill, Is.EqualTo(r.CpuSkill.SelfBonus));
                    Assert.That(r.CpuPower.OpponentPenalty, Is.EqualTo(r.PlayerSkill.OpponentPenalty));

                    string expected = BattleResultText.BuildDecision(r, catalog);

                    Assert.That(frames.Exists(f => f.Decision == expected), Is.True, "結果理由は記録した内訳から作ります: " + Visible(expected));
                    Assert.That(Visible(expected), Does.Contain(r.PlayerPower.Base + (r.PlayerPower.HasModifiers ? "（" : string.Empty)));

                    checkedReason = true;
                });

            Assert.That(checkedReason, Is.True, "内訳つきの POWER 決着を作れませんでした。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator LinkAndSkillCuesShownTogetherNeverOverlap()
        {
            bool seen = false;

            yield return Play(
                () => seen,
                PickForCoverage(new HashSet<string>()),
                (round, frames) =>
                {
                    foreach (CueFrame f in frames)
                    {
                        if (!f.SkillPlaying || !f.LinkPlaying)
                        {
                            continue;
                        }

                        foreach (Rect skill in new[] { f.SkillPlayerBounds, f.SkillCpuBounds })
                        {
                            foreach (Rect link in new[] { f.LinkPlayerBounds, f.LinkCpuBounds })
                            {
                                if (skill.width > 0f && link.width > 0f)
                                {
                                    Assert.That(skill.Overlaps(link), Is.False, "スキルと LINK の文字が重なっています。");
                                    seen = true;
                                }
                            }
                        }
                    }
                });

            Assert.That(seen, Is.True, "LINK とスキルが同時に出るラウンドを作れませんでした。");
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator FxOffKeepsTheSameRulesAndShowsOnlyText()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            bool sawCue = false;

            yield return Play(
                () => sawCue && controller.Coordinator.State == BattleUiState.MatchFinished,
                PickForCoverage(new HashSet<string>()),
                (round, frames) =>
                {
                    Assert.That(frames.TrueForAll(f => !f.GraphicPlayer && !f.GraphicCpu), Is.True, "FX OFF では図形を出しません。");

                    if (round.Result.PlayerSkill.Activated || round.Result.CpuSkill.Activated)
                    {
                        Assert.That(frames.Exists(f => f.SkillPlayerAlpha >= 1f || f.SkillCpuAlpha >= 1f), Is.True, "FX OFF でも文字で伝えます。");
                        sawCue = true;
                    }
                },
                stopAtMatchEnd: true);

            BattleSession session = controller.Coordinator.Session;
            int playerWins = 0;
            int cpuWins = 0;

            foreach (RoundResult r in session.History)
            {
                playerWins += r.Winner == RoundWinner.Player ? 1 : 0;
                cpuWins += r.Winner == RoundWinner.Cpu ? 1 : 0;
            }

            Assert.That(session.PlayerWins, Is.EqualTo(playerWins), "スコアは各ラウンドの勝敗どおりです。");
            Assert.That(session.CpuWins, Is.EqualTo(cpuWins));
            Assert.That(controller.DisplayedPlayerWins, Is.EqualTo(playerWins));
            Assert.That(controller.DisplayedCpuWins, Is.EqualTo(cpuWins));

            controller.OnFxToggleClicked();
        }

        // ---------------- 片付け ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator DisablingTheScreenDuringTheCueClearsItAndKeepsTheLogicalResult()
        {
            yield return DeployUntilCue();

            BattleSession session = controller.Coordinator.Session;
            int history = session.History.Count;
            RoundResult result = controller.Coordinator.LastResult;

            controller.gameObject.SetActive(false);

            AssertCueAtRest("OnDisable の直後");
            Assert.That(session.History.Count, Is.EqualTo(history), "成立したラウンドは巻き戻しません。");
            Assert.That(session.History[history - 1], Is.SameAs(result));

            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            AssertCueAtRest("20フレーム後");

            controller.gameObject.SetActive(true);
            yield return null;

            AssertCueAtRest("再有効化の後");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ARematchClearsEverySkillDisplay()
        {
            for (int match = 0; match < MaxMatches && controller.Coordinator.State != BattleUiState.MatchFinished; match++)
            {
                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    controller.OnWheelDeployRequested(controller.Coordinator.Session.PlayerAvailableUnits[0].InstanceId);
                    yield return WaitRoundEnd();
                }
            }

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));

            controller.OnRematchClicked();
            yield return null;

            AssertCueAtRest("再戦の直後");
            Assert.That(cpuCombatant.SkillLabel.gameObject.activeSelf, Is.False, "再戦後に CPU のスキル表示を残しません。");

            BattleSession session = controller.Coordinator.Session;

            Assert.That(session.History.Count, Is.EqualTo(0));

            // 再戦後の1ラウンド目は履歴が空なので、CRIMSON BITE / VERDANT FANG は予告せず、
            // 無条件の TIDAL HOWL だけが予告されます（中央の候補で確かめます）。
            foreach (BattleUnit unit in session.PlayerAvailableUnits)
            {
                UniqueSkillPreview preview = session.PreviewPlayerSkill(unit.InstanceId);

                Assert.That(preview.Activates, Is.EqualTo(unit.Skill == UniqueSkillKind.TidalHowl), unit.InstanceId + " " + unit.Skill);
            }

            string focused = controller.Ring.FocusedInstanceId;

            Assert.That(playerCombatant.SkillLabel.gameObject.activeSelf, Is.EqualTo(session.PreviewPlayerSkill(focused).Activates),
                "中央の候補の予告だけを出します。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator LeavingTheSceneDuringTheCueLeavesNothingRunning()
        {
            yield return DeployUntilCue();

            Scene battle = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("BattleUniqueSkillTests_Empty");

            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(battle);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.That(skillView == null, Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<UniqueSkillCueGraphic>(), Is.Null);

            LogAssert.NoUnexpectedReceived();
        }

        // ---------------- 予告と漏えい ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ThePreviewShowsOnlyOwnHistoryAndNeverLeaksTheCpu()
        {
            MethodInfo focusChanged = typeof(BattleScreenController).GetMethod("OnWheelFocusChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            HashSet<UniqueSkillPreviewState> seen = new HashSet<UniqueSkillPreviewState>();

            for (int match = 0; match < MaxMatches && seen.Count < 3; match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting && seen.Count < 3)
                {
                    BattleSession session = controller.Coordinator.Session;
                    string pendingCpu = PendingCpu(session).InstanceId;

                    foreach (BattleUnit unit in session.PlayerAvailableUnits)
                    {
                        controller.Ring.Focus(unit.InstanceId);
                        focusChanged.Invoke(controller, new object[] { unit.InstanceId });

                        UniqueSkillPreview preview = session.PreviewPlayerSkill(unit.InstanceId);
                        UniqueSkillPreview expected = ExpectedPreview(unit, session.History);

                        Assert.That(preview.State, Is.EqualTo(expected.State), unit.InstanceId);
                        Assert.That(preview.SelfBonus, Is.EqualTo(expected.SelfBonus));
                        Assert.That(preview.OpponentPenalty, Is.EqualTo(expected.OpponentPenalty));
                        Assert.That(playerCombatant.SkillLabel.gameObject.activeSelf, Is.EqualTo(preview.Activates), "発動が決まっているときだけ予告します。");

                        if (unit.Skill == UniqueSkillKind.StormBite)
                        {
                            Assert.That(playerCombatant.SkillLabel.gameObject.activeSelf, Is.False, "STORM BITE は発動を予告しません。");
                        }

                        Assert.That(cpuCombatant.SkillLabel.gameObject.activeSelf, Is.False, "DEPLOY 前に CPU のスキルを漏らしません。");
                        Assert.That(cpuCombatant.IsRevealed, Is.False, "DEPLOY 前に CPU の個体を漏らしません。");
                        Assert.That(skillView.IsPlaying, Is.False);
                        Assert.That(PendingCpu(session).InstanceId, Is.EqualTo(pendingCpu), "予告は CPU の選出に触れません。");

                        seen.Add(preview.State);
                    }

                    controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);
                    yield return WaitRoundEnd();
                }
            }

            Assert.That(seen, Is.EquivalentTo(new[] { UniqueSkillPreviewState.None, UniqueSkillPreviewState.Activates, UniqueSkillPreviewState.DependsOnOpponent }));
        }

        // ---------------- iPhone 12 mini ----------------

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator SkillTextsFitWithoutOverlapOnIPhone12Mini([Values(true, false)] bool fx)
        {
            yield return UsePortraitPhone();

            controller.OnWheelDeployRequested(controller.Coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (!(playerCombatant.IsRevealed && cpuCombatant.IsRevealed && controller.IsRoundInProgress))
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "両陣営が公開されません。");
            }

            // 最も長い表記どうしを、LINK と同時に双方へ出します。
            UniqueSkillActivation storm = new UniqueSkillActivation(UniqueSkillKind.StormBite, true, UniqueSkillReason.OpponentHasRedOrBlue, 3, 0);
            UniqueSkillActivation howl = new UniqueSkillActivation(UniqueSkillKind.TidalHowl, true, UniqueSkillReason.Unconditional, 0, 4);
            AttributeLinkResult link = new AttributeLinkResult(4, 6, AttributeLink.BitOf(UnitAttribute.Red) | AttributeLink.BitOf(UnitAttribute.Blue));
            string playerText = catalog.FormatSkillCue("VERDANT FANG", UniqueSkillKind.StormBite, 3);
            string cpuText = catalog.FormatSkillCue("CRIMSON BITE", UniqueSkillKind.CrimsonBite, 4);

            playerCombatant.ShowLink(link);
            cpuCombatant.ShowLink(link);
            playerCombatant.ShowSkill(howl);
            cpuCombatant.ShowSkill(storm);
            linkView.Play(BattleAttributeLinkPresentationPlan.Create(link, link, fx));
            skillView.Play(BattleSkillPresentationPlan.Create(storm, howl, fx), playerText, cpuText);

            yield return null;
            yield return null;

            Assert.That(skillView.IsPlaying, Is.True);
            Assert.That(skillView.Graphic.IsShowing, Is.EqualTo(fx));

            Rect area = WorldRect((RectTransform)skillView.transform);
            Rect playerSkill = AssertRendered(skillView.PlayerLabel, playerText, 2);
            Rect cpuSkill = AssertRendered(skillView.CpuLabel, cpuText, 2);
            Rect playerLink = AssertRendered(linkView.PlayerLabel, linkView.PlayerLabel.text, 2);
            Rect cpuLink = AssertRendered(linkView.CpuLabel, linkView.CpuLabel.text, 2);
            // CoreGate 自体は段の間の帯全体（全幅）なので、見えている中央の輪（いちばん外側の OuterArc）と比べます。
            Rect gate = WorldRect((RectTransform)Find("CoreGate").Find("OuterArc"));

            foreach (Rect skill in new[] { playerSkill, cpuSkill })
            {
                Assert.That(Contains(area, skill), Is.True, "スキルの文字が戦闘表示領域（RectMask2D）で切れます。 " + skill + " / " + area);
                Assert.That(skill.Overlaps(playerLink) || skill.Overlaps(cpuLink), Is.False, "スキルと LINK の文字が重なっています。");
                Assert.That(skill.Overlaps(gate), Is.False, "スキルの文字が中央の CoreGate（VS の輪）に重なっています。 skill=" + skill + " gate=" + gate +
                    " playerSkill=" + playerSkill + " cpuSkill=" + cpuSkill + " area=" + area +
                    " playerStage=" + WorldRect((RectTransform)playerCombatant.transform) + " cpuStage=" + WorldRect((RectTransform)cpuCombatant.transform));

                foreach (string hud in HudNames)
                {
                    Assert.That(skill.Overlaps(WorldRect((RectTransform)Find(hud))), Is.False, "スキルの文字が " + hud + " に入り込んでいます。");
                }

                foreach (BattleCombatantView combatant in new[] { playerCombatant, cpuCombatant })
                {
                    Assert.That(skill.Overlaps(WorldRect((RectTransform)combatant.LinkLabel.transform.parent)), Is.False,
                        "スキルの文字が " + combatant.name + " の情報欄に入り込んでいます。");
                }
            }

            Assert.That(playerSkill.Overlaps(cpuSkill), Is.False, "PLAYER と CPU のスキルの文字が重なっています。");

            if (fx)
            {
                Assert.That(skillView.Graphic.LastVertexCount, Is.LessThanOrEqualTo(BattleSkillPresentationPlan.VertexBudget));
            }
            else
            {
                Assert.That(skillView.PlayerLabelAlpha, Is.EqualTo(1f), "FX OFF でも文字ははっきり読めます。");
            }

            // 段のスキル表示: Info の中で、スキル名・LINK 表示・属性チップ・POWER に重ならず、切れません。
            foreach (BattleCombatantView combatant in new[] { playerCombatant, cpuCombatant })
            {
                TMP_Text label = combatant.SkillLabel;
                Rect bounds = AssertRendered(label, label.text, 1);
                RectTransform info = (RectTransform)label.transform.parent;

                Assert.That(Contains(WorldRect(label.rectTransform), bounds), Is.True, combatant.name + " のスキル表示が枠からはみ出します。");
                Assert.That(Contains(WorldRect(info), bounds), Is.True, combatant.name + " のスキル表示が Info からはみ出します。");

                foreach (string other in new[] { "attributeChip", "nameLabel", "levelLabel", "powerLabel", "coreLabel", "linkLabel" })
                {
                    Component c = (Component)GetField(combatant, other);

                    Assert.That(bounds.Overlaps(WorldRect((RectTransform)c.transform)), Is.False, combatant.name + " のスキル表示が " + other + " に重なっています。");
                }

                TMP_Text skillName = (TMP_Text)GetField(combatant, "skillNameLabel");
                skillName.ForceMeshUpdate();

                Assert.That(bounds.Overlaps(TextWorldRect(skillName)), Is.False, combatant.name + " のスキル表示がスキル名の文字に重なっています。");
            }

            linkView.ResetVisuals();
            skillView.ResetVisuals();

            yield return WaitRoundEnd();

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        // ---------------- 繰り返し ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator TenMatchesCreateNoGameObjectsComponentsOrGraphics()
        {
            int objects = -1;
            int components = -1;
            int graphics = -1;

            for (int match = 0; match < 10; match++)
            {
                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    BattleSession session = controller.Coordinator.Session;

                    controller.OnWheelDeployRequested(PickForCoverage(new HashSet<string>())(session));
                    yield return WaitRoundEnd();
                }

                Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished), "match " + match);

                controller.OnRematchClicked();
                yield return null;
                yield return null;

                if (match == 0)
                {
                    objects = CountScene<Transform>();
                    components = CountScene<Component>();
                    graphics = CountScene<Graphic>();
                    continue;
                }

                Assert.That(CountScene<Transform>(), Is.EqualTo(objects), "match " + match + " の後で GameObject が増えました。");
                Assert.That(CountScene<Component>(), Is.EqualTo(components), "match " + match + " の後で Component が増えました。");
                Assert.That(CountScene<Graphic>(), Is.EqualTo(graphics), "match " + match + " の後で Graphic が増えました。");
            }

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        // ---------------- 進行 ----------------

        private enum Side
        {
            Player,
            Cpu,
        }

        private sealed class RoundRecord
        {
            internal RoundResult Result;
        }

        private sealed class CueFrame
        {
            internal float Time;
            internal bool SkillPlaying;
            internal bool LinkPlaying;
            internal bool GraphicPlayer;
            internal bool GraphicCpu;
            internal string SkillPlayerText;
            internal string SkillCpuText;
            internal float SkillPlayerAlpha;
            internal float SkillCpuAlpha;
            internal Rect SkillPlayerBounds;
            internal Rect SkillCpuBounds;
            internal Rect LinkPlayerBounds;
            internal Rect LinkCpuBounds;
            internal bool ImpactShowing;
            internal string PlayerStageSkill;
            internal string CpuStageSkill;
            internal string Decision;
        }

        /// <summary>
        /// 試合を進め、各ラウンドで「仕様から組み立てた期待値」と実際の結果を突き合わせ、
        /// フレームの記録を <paramref name="check"/> へ渡します。<paramref name="done"/>が成り立てば止めます。
        /// </summary>
        private IEnumerator Play(Func<bool> done, Func<BattleSession, string> pick, Action<RoundRecord, List<CueFrame>> check, bool stopAtMatchEnd = false)
        {
            TMP_Text decision = (TMP_Text)GetField(UnityEngine.Object.FindAnyObjectByType<BattleResultView>(), "decisionLabel");

            for (int match = 0; match < MaxMatches && !done(); match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting && !done())
                {
                    BattleSession session = controller.Coordinator.Session;

                    // DEPLOY 前: CPU の個体・スキルを一切出しません。
                    Assert.That(cpuCombatant.SkillLabel.gameObject.activeSelf, Is.False, "DEPLOY 前に CPU のスキルを漏らしません。");
                    Assert.That(cpuCombatant.IsRevealed, Is.False, "DEPLOY 前に CPU の個体を漏らしません。");
                    Assert.That(skillView.IsPlaying, Is.False);

                    string id = pick(session);
                    BattleUnit player = session.PlayerSquad.Find(id);
                    BattleUnit cpu = PendingCpu(session);
                    Expected expected = ExpectedRound(player, cpu, session.History);

                    controller.OnWheelDeployRequested(id);

                    Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

                    RoundRecord round = new RoundRecord { Result = controller.Coordinator.LastResult };

                    AssertMatchesExpected(round.Result, expected);

                    List<CueFrame> frames = new List<CueFrame>();
                    float startedAt = Time.realtimeSinceStartup;

                    while (true)
                    {
                        frames.Add(Capture(decision));

                        if (!controller.IsRoundInProgress)
                        {
                            break;
                        }

                        yield return null;

                        Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
                    }

                    AssertCueAtRest("ラウンド終了時");
                    AssertShownOnlyWhenActivated(round.Result, frames);

                    check(round, frames);
                }

                if (stopAtMatchEnd && done())
                {
                    yield break;
                }
            }
        }

        private CueFrame Capture(TMP_Text decision)
        {
            return new CueFrame
            {
                Time = Time.realtimeSinceStartup,
                SkillPlaying = skillView.IsPlaying,
                LinkPlaying = linkView.IsPlaying,
                GraphicPlayer = skillView.Graphic.IsShowingPlayer,
                GraphicCpu = skillView.Graphic.IsShowingCpu,
                SkillPlayerText = skillView.PlayerLabel.text,
                SkillCpuText = skillView.CpuLabel.text,
                SkillPlayerAlpha = skillView.PlayerLabelAlpha,
                SkillCpuAlpha = skillView.CpuLabelAlpha,
                SkillPlayerBounds = Bounds(skillView.PlayerLabel),
                SkillCpuBounds = Bounds(skillView.CpuLabel),
                LinkPlayerBounds = Bounds(linkView.PlayerLabel),
                LinkCpuBounds = Bounds(linkView.CpuLabel),
                ImpactShowing = impact.IsShowing,
                // ラウンドの間の段の表示だけを見ます（終わった直後のフレームは、次の候補の予告に切り替わっています）。
                PlayerStageSkill = controller.IsRoundInProgress && playerCombatant.SkillLabel.gameObject.activeInHierarchy ? playerCombatant.SkillLabel.text : string.Empty,
                CpuStageSkill = controller.IsRoundInProgress && cpuCombatant.SkillLabel.gameObject.activeInHierarchy ? cpuCombatant.SkillLabel.text : string.Empty,
                Decision = decision.gameObject.activeInHierarchy ? decision.text : string.Empty,
            };
        }

        private static Rect Bounds(TMP_Text label)
        {
            if (string.IsNullOrEmpty(label.text))
            {
                return Rect.zero;
            }

            label.ForceMeshUpdate();

            return TextWorldRect(label);
        }

        /// <summary>発動した側だけ、演出と段の表示が出ます。発動しない側は何も出ません。</summary>
        private void AssertShownOnlyWhenActivated(RoundResult r, List<CueFrame> frames)
        {
            string playerCue = r.PlayerSkill.Activated ? CueText(r.PlayerSkill, r.PlayerUnit.InstanceId, true) : string.Empty;
            string cpuCue = r.CpuSkill.Activated ? CueText(r.CpuSkill, r.CpuUnit.InstanceId, false) : string.Empty;

            if (r.PlayerSkill.Activated || r.CpuSkill.Activated)
            {
                Assert.That(frames[0].SkillPlayerText, Is.EqualTo(playerCue), "PLAYER のスキル演出の文字");
                Assert.That(frames[0].SkillCpuText, Is.EqualTo(cpuCue), "CPU のスキル演出の文字");
            }

            foreach (CueFrame f in frames)
            {
                if (!r.PlayerSkill.Activated)
                {
                    Assert.That(f.SkillPlayerText, Is.Empty, "発動しない PLAYER のスキル演出を出しました。");
                    Assert.That(f.GraphicPlayer, Is.False);
                    Assert.That(f.PlayerStageSkill, Is.Empty, "発動しない PLAYER のスキル表示を出しました。");
                }

                if (!r.CpuSkill.Activated)
                {
                    Assert.That(f.SkillCpuText, Is.Empty, "発動しない CPU のスキル演出を出しました。");
                    Assert.That(f.GraphicCpu, Is.False);
                    Assert.That(f.CpuStageSkill, Is.Empty, "発動しない CPU のスキル表示を出しました。");
                }
            }
        }

        private string CueText(UniqueSkillActivation skill, string instanceId, bool player)
        {
            BattleSideRoster side = player ? GetMatchSource().PlayerSide : GetMatchSource().CpuSide;
            string name = side.Find(instanceId).Definition.SkillName;

            return catalog.FormatSkillCue(name, skill.Kind, skill.SelfBonus > 0 ? skill.SelfBonus : skill.OpponentPenalty);
        }

        private string SkillText(UniqueSkillActivation skill)
        {
            if (!skill.Activated)
            {
                return string.Empty;
            }

            return skill.SelfBonus > 0 ? catalog.FormatSkillSelfBonus(skill.SelfBonus) : catalog.FormatSkillOpponentPenalty(skill.OpponentPenalty);
        }

        private BattleMatchSource GetMatchSource()
        {
            return (BattleMatchSource)GetField(controller, "matchSource");
        }

        private IEnumerator DeployUntilCue()
        {
            for (int match = 0; match < MaxMatches; match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    controller.OnWheelDeployRequested(PickForCoverage(new HashSet<string>())(controller.Coordinator.Session));

                    if (skillView.IsPlaying)
                    {
                        yield return null;

                        Assert.That(skillView.IsPlaying, Is.True);
                        yield break;
                    }

                    yield return WaitRoundEnd();
                }
            }

            Assert.Fail(MaxMatches + " 試合でスキル演出の途中まで進められませんでした。");
        }

        private IEnumerator RestartIfFinished()
        {
            if (controller.Coordinator.State == BattleUiState.MatchFinished)
            {
                controller.OnRematchClicked();
                yield return null;
            }

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));
        }

        private IEnumerator WaitRoundEnd()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }
        }

        /// <summary>まだ確かめていない「スキル:発動/不発」の組み合わせになる個体を優先して選びます。</summary>
        private static Func<BattleSession, string> PickForCoverage(HashSet<string> covered)
        {
            return session =>
            {
                BattleUnit cpu = PendingCpu(session);
                IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

                foreach (BattleUnit unit in available)
                {
                    if (unit.Skill == UniqueSkillKind.None)
                    {
                        continue;
                    }

                    bool on = ExpectedRound(unit, cpu, session.History).PlayerSkill.Activated;

                    if (!covered.Contains(unit.Skill + (on ? ":on" : ":off")))
                    {
                        return unit.InstanceId;
                    }
                }

                return available[0].InstanceId;
            };
        }

        private static BattleUnit PendingCpu(BattleSession session)
        {
            FieldInfo field = typeof(BattleSession).GetField("pendingCpuUnit", BindingFlags.NonPublic | BindingFlags.Instance);
            BattleUnit cpu = (BattleUnit)field.GetValue(session);

            Assert.That(cpu, Is.Not.Null, "CPU の選出が済んでいません。");

            return cpu;
        }

        // ---------------- 仕様からの期待値 ----------------

        private struct Expected
        {
            internal UniqueSkillActivation PlayerSkill;
            internal UniqueSkillActivation CpuSkill;
            internal int PlayerFinal;
            internal int CpuFinal;
            internal UnitAttribute? Compared;
        }

        /// <summary>
        /// 仕様の文章どおりに期待値を組み立てます（製品の評価関数は使いません）。
        /// 比べる色は、どちらの陣営も同じ単色・2色対単色なら単色側の色、同じ構成の2色どうしは平均です。
        /// </summary>
        private static Expected ExpectedRound(BattleUnit player, BattleUnit cpu, IReadOnlyList<RoundResult> history)
        {
            RoundResult last = history.Count > 0 ? history[history.Count - 1] : null;

            BattleUnit playerPrevious = last?.PlayerUnit;
            BattleUnit cpuPrevious = last?.CpuUnit;
            RoundWinner? lastWinner = last?.Winner;

            UniqueSkillActivation playerSkill = ExpectedSkill(player, playerPrevious, lastWinner == RoundWinner.Cpu, cpu);
            UniqueSkillActivation cpuSkill = ExpectedSkill(cpu, cpuPrevious, lastWinner == RoundWinner.Player, player);

            int playerLink = LinkBonus(playerPrevious, player, last != null ? last.PlayerLink.ChainCount : 0);
            int cpuLink = LinkBonus(cpuPrevious, cpu, last != null ? last.CpuLink.ChainCount : 0);

            int playerDelta = playerLink + playerSkill.SelfBonus - cpuSkill.OpponentPenalty;
            int cpuDelta = cpuLink + cpuSkill.SelfBonus - playerSkill.OpponentPenalty;

            UnitAttribute? compared = null;

            if (!player.IsDual)
            {
                compared = player.PrimaryAttribute;
            }
            else if (!cpu.IsDual)
            {
                compared = cpu.PrimaryAttribute;
            }

            return new Expected
            {
                PlayerSkill = playerSkill,
                CpuSkill = cpuSkill,
                PlayerFinal = Final(player, compared, playerDelta),
                CpuFinal = Final(cpu, compared, cpuDelta),
                Compared = compared,
            };
        }

        private static int Final(BattleUnit unit, UnitAttribute? compared, int delta)
        {
            if (compared.HasValue)
            {
                return Mathf.Max(1, unit.PowerOf(compared.Value) + delta);
            }

            int total = 0;

            foreach (AttributePower p in unit.AttributePowers)
            {
                total += Mathf.Max(1, p.Power + delta);
            }

            return total / unit.AttributeCount;
        }

        private static int LinkBonus(BattleUnit previous, BattleUnit current, int previousChain)
        {
            if (previous == null)
            {
                return 0;
            }

            foreach (AttributePower p in current.AttributePowers)
            {
                if (previous.HasAttribute(p.Attribute))
                {
                    int chain = Mathf.Max(previousChain, 1) + 1;
                    return chain >= 3 ? 6 : 3;
                }
            }

            return 0;
        }

        private static UniqueSkillActivation ExpectedSkill(BattleUnit self, BattleUnit previous, bool lostPrevious, BattleUnit opponent)
        {
            switch (self.Skill)
            {
                case UniqueSkillKind.CrimsonBite:
                    bool red = previous != null && previous.HasAttribute(UnitAttribute.Red);
                    return new UniqueSkillActivation(UniqueSkillKind.CrimsonBite, red,
                        previous == null ? UniqueSkillReason.FirstRound : red ? UniqueSkillReason.PreviousUnitHadRed : UniqueSkillReason.PreviousUnitHadNoRed, 4, 0);
                case UniqueSkillKind.TidalHowl:
                    return new UniqueSkillActivation(UniqueSkillKind.TidalHowl, true, UniqueSkillReason.Unconditional, 0, 4);
                case UniqueSkillKind.VerdantFang:
                    return new UniqueSkillActivation(UniqueSkillKind.VerdantFang, lostPrevious, ExpectedFangReason(previous, lostPrevious), 6, 0);
                case UniqueSkillKind.StormBite:
                    bool hit = opponent.HasAttribute(UnitAttribute.Red) || opponent.HasAttribute(UnitAttribute.Blue);
                    return new UniqueSkillActivation(UniqueSkillKind.StormBite, hit,
                        hit ? UniqueSkillReason.OpponentHasRedOrBlue : UniqueSkillReason.OpponentHasNoRedOrBlue, 3, 0);
                default:
                    return UniqueSkillActivation.None;
            }
        }

        private static UniqueSkillReason ExpectedFangReason(BattleUnit previous, bool lostPrevious)
        {
            if (previous == null)
            {
                return UniqueSkillReason.FirstRound;
            }

            return lostPrevious ? UniqueSkillReason.LostPreviousRound : UniqueSkillReason.WonPreviousRound;
        }

        private static UniqueSkillPreview ExpectedPreview(BattleUnit unit, IReadOnlyList<RoundResult> history)
        {
            RoundResult last = history.Count > 0 ? history[history.Count - 1] : null;

            switch (unit.Skill)
            {
                case UniqueSkillKind.TidalHowl:
                    return new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.Activates, 0, 4);
                case UniqueSkillKind.StormBite:
                    return new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.DependsOnOpponent, 0, 0);
                case UniqueSkillKind.CrimsonBite:
                    return last != null && last.PlayerUnit.HasAttribute(UnitAttribute.Red)
                        ? new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.Activates, 4, 0)
                        : new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.None, 0, 0);
                case UniqueSkillKind.VerdantFang:
                    return last != null && last.Winner == RoundWinner.Cpu
                        ? new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.Activates, 6, 0)
                        : new UniqueSkillPreview(unit.Skill, UniqueSkillPreviewState.None, 0, 0);
                default:
                    return UniqueSkillPreview.None;
            }
        }

        private static void AssertMatchesExpected(RoundResult r, Expected e)
        {
            Assert.That(r.PlayerSkill.Kind, Is.EqualTo(e.PlayerSkill.Kind));
            Assert.That(r.PlayerSkill.Activated, Is.EqualTo(e.PlayerSkill.Activated), "PLAYER " + r.PlayerSkill.Kind + " の発動条件");
            Assert.That(r.PlayerSkill.SelfBonus, Is.EqualTo(e.PlayerSkill.SelfBonus));
            Assert.That(r.PlayerSkill.OpponentPenalty, Is.EqualTo(e.PlayerSkill.OpponentPenalty));
            Assert.That(r.CpuSkill.Kind, Is.EqualTo(e.CpuSkill.Kind));
            Assert.That(r.CpuSkill.Activated, Is.EqualTo(e.CpuSkill.Activated), "CPU " + r.CpuSkill.Kind + " の発動条件");
            Assert.That(r.CpuSkill.SelfBonus, Is.EqualTo(e.CpuSkill.SelfBonus));
            Assert.That(r.CpuSkill.OpponentPenalty, Is.EqualTo(e.CpuSkill.OpponentPenalty));

            if (r.PlayerSkill.Kind == UniqueSkillKind.VerdantFang && r.PlayerSkill.Reason != UniqueSkillReason.DrewPreviousRound)
            {
                Assert.That(r.PlayerSkill.Reason, Is.EqualTo(e.PlayerSkill.Reason));
            }

            if (r.PlayerPower.IsAvailable)
            {
                Assert.That(r.PlayerPower.Final, Is.EqualTo(e.PlayerFinal), "PLAYER の最終 POWER（LINK → 自分のスキル → 相手の妨害 → 下限1）");
                Assert.That(r.CpuPower.Final, Is.EqualTo(e.CpuFinal), "CPU の最終 POWER");
            }
        }

        private void AssertCueAtRest(string label)
        {
            Assert.That(skillView.IsPlaying, Is.False, label + ": スキル演出が残っています。");
            Assert.That(skillView.Graphic.IsShowing, Is.False, label + ": スキルの図形が残っています。");
            Assert.That(skillView.Graphic.Progress, Is.EqualTo(0f), label);
            Assert.That(skillView.PlayerLabel.text, Is.Empty, label);
            Assert.That(skillView.CpuLabel.text, Is.Empty, label);
            Assert.That(skillView.PlayerLabelAlpha, Is.EqualTo(0f), label);
            Assert.That(skillView.CpuLabelAlpha, Is.EqualTo(0f), label);
        }

        /// <summary>メッシュの文字が元の文字列と1字ずつ一致し、日本語・スキル名が日本語フォントで描かれていることを確かめます。</summary>
        private static Rect AssertRendered(TMP_Text label, string source, int lines)
        {
            label.ForceMeshUpdate();

            TMP_TextInfo info = label.textInfo;
            StringBuilder rendered = new StringBuilder();

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];

                rendered.Append(ch.character);

                if (!char.IsWhiteSpace(ch.character) && ch.character > 0x7F)
                {
                    Assert.That(ch.fontAsset.name, Is.EqualTo(JapaneseFontName), label.name + ": '" + ch.character + "' を日本語フォントで描いていません。");
                }
            }

            Assert.That(rendered.ToString(), Is.EqualTo(Visible(source)), label.name + " の文字が置き換わっています。");
            Assert.That(label.textInfo.lineCount, Is.EqualTo(lines), label.name + " の行数");
            Assert.That(label.isTextOverflowing, Is.False, label.name + " が溢れています。");

            return TextWorldRect(label);
        }

        private void CollectMissingCharacters(string condition, string stackTrace, LogType type)
        {
            if (condition != null && condition.Contains("was not found in the"))
            {
                missingCharacterWarnings.Add(condition);
            }
        }

        // ---------------- 画面 ----------------

        /// <summary>ルート Canvas を iPhone 12 mini（1080 x 2340 px、安全余白 上50pt / 下34pt）の縦画面と同じ大きさへ置き換えます。</summary>
        private IEnumerator UsePortraitPhone()
        {
            const float width = 1080f;
            const float height = 2340f;

            Canvas canvas = Find("Canvas").GetComponent<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

            float logWidth = Mathf.Log(width / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(height / scaler.referenceResolution.y, 2f);
            float pixelsPerUnit = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));

            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(width / pixelsPerUnit, height / pixelsPerUnit);

            RectTransform safeArea = (RectTransform)Find("SafeArea");
            safeArea.anchorMin = new Vector2(0f, 34f * 3f / height);
            safeArea.anchorMax = new Vector2(1f, 1f - 50f * 3f / height);

            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(WorldRect(safeArea).height, Is.GreaterThan(WorldRect(safeArea).width), "縦画面になっていません。");
        }

        // ---------------- 道具 ----------------

        private static string Visible(string text)
        {
            return RichTag.Replace(text ?? string.Empty, string.Empty);
        }

        private static Rect TextWorldRect(TMP_Text label)
        {
            Bounds b = label.textBounds;
            Vector3 min = label.transform.TransformPoint(b.min);
            Vector3 max = label.transform.TransformPoint(b.max);

            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            const float Epsilon = 0.5f;

            return inner.xMin >= outer.xMin - Epsilon && inner.xMax <= outer.xMax + Epsilon &&
                   inner.yMin >= outer.yMin - Epsilon && inner.yMax <= outer.yMax + Epsilon;
        }

        private static int CountScene<T>() where T : Component
        {
            int count = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SaveTestSquad()
        {
#if UNITY_EDITOR
            CoreBeastRoster roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);

            Assert.That(roster, Is.Not.Null, RosterPath + " を読めません。");

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            SquadRepositoryProvider.Shared.Save(SetId, new SquadSnapshot(ids));
#else
            Assert.Fail("エディタ上でのみ実行します（テスト用ロスターをAssetDatabaseから読むため）。");
#endif
        }
    }
}
