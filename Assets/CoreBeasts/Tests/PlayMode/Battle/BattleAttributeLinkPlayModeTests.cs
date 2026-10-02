using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 4「ATTRIBUTE LINK」を、実際の Battle シーンで確かめます。
    ///
    /// 勝敗と LINK は本物の <see cref="BattleSession"/> が決めます。テストは個体の選び方だけを工夫し
    /// （直前の自分の個体と属性を共有する候補を優先）、CPU 側の LINK は起きるまでラウンドを進めます。
    /// </summary>
    public sealed class BattleAttributeLinkPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        private const int MaxMatches = 30;

        /// <summary>LINK 演出の終わりの許容（フレームの粒度ぶん）。</summary>
        private const float FrameSlack = 0.1f;

        /// <summary>LINK 演出の間も動いてはいけないもの。</summary>
        private static readonly string[] StaticNames =
        {
            "SafeArea", "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton",
            "BattleRoot", "PlayerWheel", "HistoryLane", "ResultView", "RematchButton", "FinalHomeButton",
            "FxImpact", "FxMatchCue", "FxLink",
        };

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleAttributeLinkView linkView;
        private BattleCombatantView playerCombatant;
        private BattleCombatantView cpuCombatant;
        private BattleHistoryLaneView lane;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            linkView = controller.AttributeLinkView;
            playerCombatant = (BattleCombatantView)GetField(controller, "playerCombatant");
            cpuCombatant = (BattleCombatantView)GetField(controller, "cpuCombatant");
            lane = UnityEngine.Object.FindAnyObjectByType<BattleHistoryLaneView>();

            Assert.That(linkView, Is.Not.Null, "BattleAttributeLinkView が配線されていません。");
            Assert.That(playerCombatant.LinkLabel, Is.Not.Null);
            Assert.That(cpuCombatant.LinkLabel, Is.Not.Null);
            Assert.That(lane.LinkMarkers, Is.Not.Null);

            AssertLinkAtRest("開始時");
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 演出（4C） ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator APlayerLinkCueStartsWithDeployAndEndsInsideTheMove()
        {
            bool checkedCue = false;

            yield return PlayUntil(
                () => checkedCue,
                result => result.PlayerLink.IsActive,
                (result, frames) =>
                {
                    checkedCue = true;
                    AssertCueRan(result, frames, true);
                });

            Assert.That(checkedCue, Is.True, "プレイヤーの LINK が起きませんでした。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ACpuLinkIsShownOnlyAfterDeploy()
        {
            bool checkedCue = false;

            yield return PlayUntil(
                () => checkedCue,
                result => result.CpuLink.IsActive,
                (result, frames) =>
                {
                    checkedCue = true;
                    AssertCueRan(result, frames, true);

                    Assert.That(frames.Exists(f => f.CpuStageLink == ExpectedBonus(result.CpuLink)), Is.True,
                        "CPU 側の戦闘表示にも LINK ボーナスを出します。");
                },
                pickByLink: false);

            Assert.That(checkedCue, Is.True, "CPU の LINK が起きませんでした。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FxOffShowsOnlyTheShortTextDuringTheMove()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            bool checkedCue = false;

            yield return PlayUntil(
                () => checkedCue,
                result => result.PlayerLink.IsActive || result.CpuLink.IsActive,
                (result, frames) =>
                {
                    checkedCue = true;
                    AssertCueRan(result, frames, false);
                });

            controller.OnFxToggleClicked();

            Assert.That(checkedCue, Is.True, "LINK が起きませんでした。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator DisablingTheScreenDuringTheCueClearsItAndKeepsTheLogicalChain()
        {
            for (int match = 0; match < MaxMatches; match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    BattleSession session = controller.Coordinator.Session;
                    int historyBefore = session.History.Count;

                    controller.OnWheelDeployRequested(PickLinking(session));

                    RoundResult result = controller.Coordinator.LastResult;

                    if (result.PlayerLink.IsActive && linkView.IsPlaying)
                    {
                        yield return null;

                        Assert.That(linkView.IsPlaying, Is.True, "LINK 演出の途中まで進められませんでした。");

                        controller.gameObject.SetActive(false);

                        AssertLinkAtRest("OnDisable の直後");
                        Assert.That(controller.IsRoundInProgress, Is.False, "入力ゲートも戻ります。");
                        Assert.That(session.History.Count, Is.EqualTo(historyBefore + 1), "論理上の LINK は巻き戻しません。");
                        Assert.That(session.History[session.History.Count - 1].PlayerLink, Is.EqualTo(result.PlayerLink));

                        for (int i = 0; i < 20; i++)
                        {
                            yield return null;
                        }

                        AssertLinkAtRest("20フレーム後");

                        controller.gameObject.SetActive(true);
                        yield return null;

                        AssertLinkAtRest("再有効化の後");
                        yield break;
                    }

                    yield return WaitRoundEnd();
                }
            }

            Assert.Fail(MaxMatches + " 試合で LINK 演出の途中まで進められませんでした。");
        }

        // ---------------- 予告（4B） ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ThePreviewFollowsTheFocusedCandidateWithoutChangingTheSession()
        {
            MethodInfo focusChanged = typeof(BattleScreenController).GetMethod(
                "OnWheelFocusChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(focusChanged, Is.Not.Null);

            // 1ラウンド目は直前の個体が無いので、どの候補も予告しません。
            BattleSession first = controller.Coordinator.Session;

            foreach (BattleUnit unit in first.PlayerAvailableUnits)
            {
                FocusCandidate(focusChanged, unit.InstanceId);

                Assert.That(playerCombatant.LinkLabel.gameObject.activeSelf, Is.False, "1ラウンド目は予告しません。");
            }

            bool sawLinked = false;
            bool sawUnlinked = false;

            for (int match = 0; match < MaxMatches && !(sawLinked && sawUnlinked); match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting && !(sawLinked && sawUnlinked))
                {
                    BattleSession session = controller.Coordinator.Session;

                    if (session.History.Count > 0)
                    {
                        int round = session.CurrentRound;
                        int history = session.History.Count;
                        int available = session.PlayerAvailableUnits.Count;
                        int playerWins = session.PlayerWins;
                        int cpuWins = session.CpuWins;

                        foreach (BattleUnit unit in session.PlayerAvailableUnits)
                        {
                            FocusCandidate(focusChanged, unit.InstanceId);

                            AttributeLinkResult preview = session.PreviewPlayerLink(unit.InstanceId);

                            Assert.That(playerCombatant.ShownLink, Is.EqualTo(preview), "中央の候補の LINK を予告します。");
                            Assert.That(playerCombatant.LinkLabel.gameObject.activeSelf, Is.EqualTo(preview.IsActive));
                            Assert.That(cpuCombatant.LinkLabel.gameObject.activeSelf, Is.False, "DEPLOY 前に CPU の LINK を漏らしません。");
                            Assert.That(linkView.IsPlaying, Is.False, "予告では演出を出しません。");

                            if (preview.IsActive)
                            {
                                sawLinked = true;
                                Assert.That(playerCombatant.LinkLabel.text, Is.EqualTo("同じ属性で POWER +" + preview.BonusPower));
                            }
                            else
                            {
                                sawUnlinked = true;
                            }
                        }

                        Assert.That(session.CurrentRound, Is.EqualTo(round), "予告はセッションを進めません。");
                        Assert.That(session.History.Count, Is.EqualTo(history));
                        Assert.That(session.PlayerAvailableUnits.Count, Is.EqualTo(available));
                        Assert.That(session.PlayerWins, Is.EqualTo(playerWins));
                        Assert.That(session.CpuWins, Is.EqualTo(cpuWins));
                        Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));
                    }

                    controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);
                    yield return WaitRoundEnd();
                }
            }

            Assert.That(sawLinked, Is.True, "LINK になる候補の予告を確かめられませんでした。");
            Assert.That(sawUnlinked, Is.True, "LINK にならない候補の予告を確かめられませんでした。");
        }

        // ---------------- 履歴・再戦（4A / 4B） ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TheHistoryMarksExactlyThePlayerLinkRounds()
        {
            for (int match = 0; match < MaxMatches; match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    controller.OnWheelDeployRequested(PickLinking(controller.Coordinator.Session));
                    yield return WaitRoundEnd();
                }

                for (int i = 0; i < 3; i++)
                {
                    yield return null;
                }

                BattleSession session = controller.Coordinator.Session;
                BattleHistoryModel history = controller.History;
                int linked = 0;

                Assert.That(history.Count, Is.GreaterThan(0));

                for (int i = 0; i < history.Count; i++)
                {
                    bool expected = session.History[i].PlayerLink.IsActive;

                    Assert.That(history.Entries[i].Linked, Is.EqualTo(expected), "履歴 " + i + " の LINK 記録");
                    Assert.That(lane.LinkMarkers.IsMarked(i), Is.EqualTo(expected), "履歴 " + i + " の LINK マーカー");

                    if (expected)
                    {
                        linked++;
                    }
                }

                Assert.That(lane.LinkMarkers.MarkerCount, Is.EqualTo(linked));

                if (linked > 0)
                {
                    yield break;
                }
            }

            Assert.Fail(MaxMatches + " 試合で LINK のラウンドが履歴に残りませんでした。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ARematchResetsTheChain()
        {
            bool linkedBeforeRematch = false;

            for (int match = 0; match < MaxMatches && !linkedBeforeRematch; match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    controller.OnWheelDeployRequested(PickLinking(controller.Coordinator.Session));
                    yield return WaitRoundEnd();
                }

                RoundResult last = controller.Coordinator.LastResult;
                linkedBeforeRematch = last.PlayerLink.IsActive;
            }

            Assert.That(linkedBeforeRematch, Is.True, "最終ラウンドが LINK の試合を作れませんでした。");

            controller.OnRematchClicked();
            yield return null;

            BattleSession session = controller.Coordinator.Session;

            Assert.That(session.History.Count, Is.EqualTo(0));
            AssertLinkAtRest("再戦の直後");
            Assert.That(cpuCombatant.LinkLabel.gameObject.activeSelf, Is.False, "再戦後に CPU の LINK 表示を残しません。");
            Assert.That(playerCombatant.LinkLabel.gameObject.activeSelf, Is.False, "再戦後の1ラウンド目は予告しません。");
            Assert.That(lane.LinkMarkers.MarkerCount, Is.EqualTo(0), "再戦で履歴マーカーも消えます。");

            foreach (BattleUnit unit in session.PlayerAvailableUnits)
            {
                Assert.That(session.PreviewPlayerLink(unit.InstanceId).IsActive, Is.False, "再戦後の1ラウンド目は LINK しません。");
            }

            controller.OnWheelDeployRequested(PickLinking(session));

            RoundResult firstRound = controller.Coordinator.LastResult;

            Assert.That(firstRound.PlayerLink.ChainCount, Is.EqualTo(1), "前の試合のチェーンを持ち越しません。");
            Assert.That(firstRound.CpuLink.ChainCount, Is.EqualTo(1));
            Assert.That(linkView.IsPlaying, Is.False);

            yield return WaitRoundEnd();
        }

        // ---------------- 検査 ----------------

        private sealed class CueFrame
        {
            internal float Time;
            internal bool Playing;
            internal bool GraphicPlayer;
            internal bool GraphicCpu;
            internal string PlayerText;
            internal string CpuText;
            internal float PlayerAlpha;
            internal float CpuAlpha;
            internal int CpuStageLink;
            internal bool RoundActive;
        }

        private void AssertCueRan(RoundResult result, List<CueFrame> frames, bool fx)
        {
            Assert.That(frames.Count, Is.GreaterThan(0));
            Assert.That(frames[0].Playing, Is.True, "DEPLOY と同じフレームで始めます（待ちを挟みません）。");

            int lastPlaying = frames.FindLastIndex(f => f.Playing);
            float duration = frames[lastPlaying].Time - frames[0].Time;
            float limit = fx ? BattleAttributeLinkPresentationPlan.CueDuration : BattleAttributeLinkPresentationPlan.TextOnlyDuration;

            Assert.That(duration, Is.LessThanOrEqualTo(limit + FrameSlack), "約0.30秒で終わります。");
            Assert.That(lastPlaying, Is.LessThan(frames.Count - 1), "ラウンドの途中で終わります。");
            Assert.That(frames.GetRange(0, lastPlaying + 1).TrueForAll(f => f.RoundActive), Is.True, "入力ゲートは既存の roundActive のままです。");

            string expectedPlayer = result.PlayerLink.IsActive ? CueText(result.PlayerLink) : string.Empty;
            string expectedCpu = result.CpuLink.IsActive ? CueText(result.CpuLink) : string.Empty;

            for (int i = 0; i <= lastPlaying; i++)
            {
                CueFrame f = frames[i];

                Assert.That(f.PlayerText, Is.EqualTo(expectedPlayer));
                Assert.That(f.CpuText, Is.EqualTo(expectedCpu));

                if (fx)
                {
                    Assert.That(f.GraphicCpu && !result.CpuLink.IsActive, Is.False, "LINK していない側は描きません。");
                    Assert.That(f.GraphicPlayer && !result.PlayerLink.IsActive, Is.False, "LINK していない側は描きません。");
                }
                else
                {
                    Assert.That(f.GraphicPlayer || f.GraphicCpu, Is.False, "FX OFF では図形を出しません。");

                    if (result.PlayerLink.IsActive)
                    {
                        Assert.That(f.PlayerAlpha, Is.EqualTo(1f), "FX OFF でも短い文字で伝えます。");
                    }

                    if (result.CpuLink.IsActive)
                    {
                        Assert.That(f.CpuAlpha, Is.EqualTo(1f));
                    }
                }
            }

            if (fx)
            {
                Assert.That(frames.Exists(f => f.GraphicPlayer), Is.EqualTo(result.PlayerLink.IsActive));
                Assert.That(frames.Exists(f => f.GraphicCpu), Is.EqualTo(result.CpuLink.IsActive));

                if (result.PlayerLink.IsActive && result.CpuLink.IsActive)
                {
                    Assert.That(frames.Exists(f => f.GraphicPlayer && f.GraphicCpu), Is.True, "双方が LINK すれば同時に出します。");
                }
            }

            for (int i = lastPlaying + 1; i < frames.Count; i++)
            {
                Assert.That(frames[i].GraphicPlayer || frames[i].GraphicCpu, Is.False, "演出の後に図形を残しません。");
                Assert.That(frames[i].PlayerText, Is.Empty);
                Assert.That(frames[i].CpuText, Is.Empty);
            }

            // 論理: 基礎の個体は書き換えず、LINK 反映後の個体で判定しています。
            Assert.That(result.PlayerEffectiveUnit, Is.Not.Null);
            Assert.That(result.CpuEffectiveUnit, Is.Not.Null);

            if (result.PlayerLink.IsActive)
            {
                Assert.That(result.PlayerEffectiveUnit, Is.Not.SameAs(result.PlayerUnit));
                Assert.That(result.PlayerEffectiveUnit.PowerOf(result.PlayerUnit.PrimaryAttribute),
                    Is.EqualTo(result.PlayerUnit.PowerOf(result.PlayerUnit.PrimaryAttribute) + result.PlayerLink.BonusPower));
            }
        }

        private void AssertLinkAtRest(string label)
        {
            Assert.That(linkView.IsPlaying, Is.False, label + ": LINK 演出が残っています。");
            Assert.That(linkView.Graphic.IsShowing, Is.False, label + ": LINK の図形が残っています。");
            Assert.That(linkView.Graphic.Progress, Is.EqualTo(0f), label);
            Assert.That(linkView.PlayerLabel.text, Is.Empty, label);
            Assert.That(linkView.CpuLabel.text, Is.Empty, label);
            Assert.That(linkView.PlayerLabelAlpha, Is.EqualTo(0f), label);
            Assert.That(linkView.CpuLabelAlpha, Is.EqualTo(0f), label);
        }

        // ---------------- 進行 ----------------

        /// <summary>
        /// <paramref name="wanted"/>のラウンドが来るまで試合を進め、そのラウンドの各フレームを記録して
        /// <paramref name="check"/>に渡します。<paramref name="done"/>が成り立てば止めます。
        /// </summary>
        private IEnumerator PlayUntil(Func<bool> done, Predicate<RoundResult> wanted, Action<RoundResult, List<CueFrame>> check, bool pickByLink = true)
        {
            for (int match = 0; match < MaxMatches && !done(); match++)
            {
                yield return RestartIfFinished();

                while (controller.Coordinator.State == BattleUiState.Selecting && !done())
                {
                    BattleSession session = controller.Coordinator.Session;

                    // DEPLOY 前: CPU の LINK を漏らしません。
                    Assert.That(cpuCombatant.LinkLabel.gameObject.activeSelf, Is.False, "DEPLOY 前に CPU の LINK を漏らしません。");
                    Assert.That(linkView.IsPlaying, Is.False);

                    string pick = pickByLink ? PickLinking(session) : session.PlayerAvailableUnits[0].InstanceId;
                    Dictionary<string, Pose> statics = CaptureStatics();

                    controller.OnWheelDeployRequested(pick);

                    Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

                    RoundResult result = controller.Coordinator.LastResult;
                    bool record = wanted(result);
                    List<CueFrame> frames = new List<CueFrame>();
                    float startedAt = Time.realtimeSinceStartup;

                    while (true)
                    {
                        if (record)
                        {
                            frames.Add(Capture());

                            if (linkView.IsPlaying)
                            {
                                AssertStatics(statics);
                            }
                        }

                        if (!controller.IsRoundInProgress)
                        {
                            break;
                        }

                        yield return null;

                        Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
                    }

                    AssertLinkAtRest("ラウンド終了時");

                    if (record)
                    {
                        check(result, frames);
                    }
                }
            }
        }

        private CueFrame Capture()
        {
            return new CueFrame
            {
                Time = Time.realtimeSinceStartup,
                Playing = linkView.IsPlaying,
                GraphicPlayer = linkView.Graphic.IsShowingPlayer,
                GraphicCpu = linkView.Graphic.IsShowingCpu,
                PlayerText = linkView.PlayerLabel.text,
                CpuText = linkView.CpuLabel.text,
                PlayerAlpha = linkView.PlayerLabelAlpha,
                CpuAlpha = linkView.CpuLabelAlpha,
                CpuStageLink = cpuCombatant.LinkLabel.gameObject.activeSelf ? cpuCombatant.ShownLink.BonusPower : 0,
                RoundActive = controller.IsRoundInProgress,
            };
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

        /// <summary>直前の自分の個体と属性を共有する候補を優先します（無ければ先頭）。</summary>
        private static string PickLinking(BattleSession session)
        {
            foreach (BattleUnit unit in session.PlayerAvailableUnits)
            {
                if (session.PreviewPlayerLink(unit.InstanceId).IsActive)
                {
                    return unit.InstanceId;
                }
            }

            return session.PlayerAvailableUnits[0].InstanceId;
        }

        private void FocusCandidate(MethodInfo focusChanged, string instanceId)
        {
            controller.Ring.Focus(instanceId);

            Assert.That(controller.Ring.FocusedInstanceId, Is.EqualTo(instanceId), "候補を中央へ持ってこられません。");
            focusChanged.Invoke(controller, new object[] { instanceId });
        }

        private static string CueText(AttributeLinkResult link)
        {
            return "属性リンク " + link.ChainCount + "連鎖！\nPOWER +" + link.BonusPower;
        }

        private static int ExpectedBonus(AttributeLinkResult link)
        {
            return link.BonusPower;
        }

        private static Dictionary<string, Pose> CaptureStatics()
        {
            Dictionary<string, Pose> poses = new Dictionary<string, Pose>();

            foreach (string name in StaticNames)
            {
                Transform t = Find(name);
                poses[name] = new Pose(t.position, t.rotation);
            }

            return poses;
        }

        private static void AssertStatics(Dictionary<string, Pose> statics)
        {
            foreach (KeyValuePair<string, Pose> entry in statics)
            {
                Transform t = Find(entry.Key);

                Assert.That(t.position, Is.EqualTo(entry.Value.position), entry.Key + " を LINK 演出で動かしません。");
                Assert.That(t.rotation, Is.EqualTo(entry.Value.rotation), entry.Key + " を LINK 演出で回しません。");
            }
        }

        // ---------------- 道具 ----------------

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
