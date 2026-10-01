using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 表示スコアの遅延（Phase 3 の追加修正）を、実際の Battle シーンで確かめます。
    ///
    /// 必須の順序:
    ///   DEPLOY → 表示スコアはラウンド前のまま → 接近・衝突・Impact → 勝敗公開
    ///   → 勝利コアが対象ピップへ収束 → 収束した瞬間に新しいピップとスコア文字を同一フレームで更新
    ///   → 4勝目なら CORE BREAK → 結果バナー・最終結果
    ///
    /// 論理スコア（<see cref="BattleSession"/>）は DEPLOY の時点で更新されたままであることも確かめます。
    /// 勝敗は本物の <see cref="BattleRules"/> が決めます。テストは CPU が非公開で選んだ個体を読むだけです。
    /// </summary>
    public sealed class BattleScoreRevealPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        private const int MaxAttempts = 15;

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleFxPlayer fx;
        private BattleScorePipsView pips;
        private BattleResultView resultView;
        private TMP_Text scoreLabel;

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

            controller = Object.FindAnyObjectByType<BattleScreenController>();
            fx = Object.FindAnyObjectByType<BattleFxPlayer>();
            pips = Object.FindAnyObjectByType<BattleScorePipsView>();
            resultView = Object.FindAnyObjectByType<BattleResultView>();
            scoreLabel = Find("ScoreLabel").GetComponent<TMP_Text>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(controller.VictoryCore, Is.Not.Null);
            Assert.That(controller.MatchCue, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 勝ちラウンド ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator APlayerWinShowsTheNewPipAndScoreOnlyWhenTheVictoryCoreConverges()
        {
            RoundTrace trace = null;

            yield return PlayUntil(RoundWinner.Player, t => trace = t);

            AssertDelayedReveal(trace, RoundWinner.Player);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ACpuWinShowsTheNewPipAndScoreOnlyWhenTheVictoryCoreConverges()
        {
            RoundTrace trace = null;

            yield return PlayUntil(RoundWinner.Cpu, t => trace = t);

            AssertDelayedReveal(trace, RoundWinner.Cpu);
        }

        // ---------------- 引き分け ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ADrawNeverChangesTheDisplayedScore()
        {
            RoundTrace trace = null;

            yield return PlayUntil(RoundWinner.Draw, t => trace = t);

            Assert.That(trace.LogicalAfterDeploy, Is.EqualTo(trace.Before), "引き分けは論理スコアも増えません。");
            Assert.That(trace.DisplayChangeFrame, Is.EqualTo(-1), "引き分けでは表示スコアが変わりません。");
            Assert.That(trace.VictoryFrames, Is.EqualTo(0), "引き分けでは勝利コア獲得がありません。");
            Assert.That(trace.LabelChangedWithoutPips, Is.False);
            Assert.That(controller.IsScoreDisplayHeld, Is.False, "ラウンド後は留めていません。");
        }

        // ---------------- 4勝目 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TheFourthPipAppearsBeforeTheCoreBreakAndTheFinalResult()
        {
            RoundTrace trace = null;

            for (int attempt = 0; attempt < MaxAttempts && trace == null; attempt++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    RoundTrace round = null;

                    yield return PlayRound(RoundWinner.Player, t => round = t);

                    if (round.Wanted && round.Winner == RoundWinner.Player &&
                        round.LogicalAfterDeploy.x == BattleSession.WinsRequired)
                    {
                        trace = round;
                    }
                }
            }

            Assert.That(trace, Is.Not.Null, "PLAYER の4勝目まで進められませんでした。");

            AssertDelayedReveal(trace, RoundWinner.Player);

            Assert.That(trace.CoreBreakStartFrame, Is.GreaterThan(trace.DisplayChangeFrame), "4個目のピップは CORE BREAK の前に出ます。");
            Assert.That(trace.DisplayAtCoreBreak, Is.EqualTo(new Vector2Int(BattleSession.WinsRequired, trace.Before.y)), "CORE BREAK の時点で表示は4勝です。");
            Assert.That(trace.FinalVisibleFrame, Is.GreaterThan(trace.CoreBreakStartFrame), "最終結果は CORE BREAK の後です。");
            Assert.That(resultView.IsFinalVisible, Is.True);
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FxOffUpdatesTheDisplayRightAfterTheReveal()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            RoundTrace trace = null;

            yield return PlayUntil(RoundWinner.Player, t => trace = t);

            Assert.That(trace.LogicalAfterDeploy, Is.EqualTo(trace.Before + new Vector2Int(1, 0)), "論理スコアは DEPLOY で更新されます。");
            Assert.That(trace.DisplayAfterDeploy, Is.EqualTo(trace.Before), "FX OFF でも DEPLOY 直後の表示はラウンド前のままです。");
            Assert.That(trace.DisplayChangeFrame, Is.GreaterThan(0));
            Assert.That(trace.RevealFrame, Is.GreaterThan(0));
            Assert.That(trace.DisplayChangeFrame, Is.GreaterThanOrEqualTo(trace.RevealFrame), "勝敗公開より前には変わりません。");
            Assert.That(trace.BannerFrame, Is.GreaterThanOrEqualTo(trace.DisplayChangeFrame), "結果バナーより前に追いつきます。");
            Assert.That(trace.VictoryFrames, Is.EqualTo(0), "FX OFF では勝利コア獲得を省きます。");
            Assert.That(trace.LabelChangedWithoutPips, Is.False, "ピップとスコア文字は同じフレームで変わります。");

            controller.OnFxToggleClicked();
        }

        // ---------------- 中断と再戦 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator InterruptingMidClashShowsTheLogicalScoreImmediately()
        {
            BattleSession session = controller.Coordinator.Session;
            Vector2Int before = new Vector2Int(session.PlayerWins, session.CpuWins);

            controller.OnWheelDeployRequested(Pick(session, RoundWinner.Player, out _));

            float startedAt = Time.realtimeSinceStartup;

            while (fx.Step != BattleFxPlayer.ClashStep.ImpactHold)
            {
                Assert.That(Displayed(), Is.EqualTo(before), "衝突の前に表示が変わりました。");

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            controller.gameObject.SetActive(false);

            Vector2Int logical = new Vector2Int(session.PlayerWins, session.CpuWins);

            Assert.That(controller.IsScoreDisplayHeld, Is.False, "中断で留めた表示を解きます。");
            Assert.That(Displayed(), Is.EqualTo(logical), "中断直後から論理スコアを表示します。");
            Assert.That(ScoreText(), Is.EqualTo(ExpectedScoreText(logical)));

            controller.gameObject.SetActive(true);
            yield return null;

            Assert.That(Displayed(), Is.EqualTo(logical));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ARematchStartsTheDisplayAtZero()
        {
            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                yield return PlayRound(RoundWinner.Player, _ => { });
            }

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(Displayed(), Is.EqualTo(new Vector2Int(controller.Coordinator.PlayerWins, controller.Coordinator.CpuWins)),
                "決着後の表示は論理スコアと同じです。");

            controller.OnRematchClicked();
            yield return null;

            Assert.That(controller.IsScoreDisplayHeld, Is.False);
            Assert.That(Displayed(), Is.EqualTo(Vector2Int.zero));
            Assert.That(ScoreText(), Is.EqualTo(ExpectedScoreText(Vector2Int.zero)));
        }

        // ---------------- 検査 ----------------

        private void AssertDelayedReveal(RoundTrace t, RoundWinner side)
        {
            Vector2Int gain = side == RoundWinner.Player ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
            Vector2Int after = t.Before + gain;
            string d = t.Describe();

            // 1. 論理スコアは DEPLOY で即時更新（BattleSession の更新時点は変えていません）。
            Assert.That(t.LogicalAfterDeploy, Is.EqualTo(after), "論理スコアは DEPLOY で更新されます。" + d);

            // 2. 表示はラウンド前のまま。
            Assert.That(t.DisplayAfterDeploy, Is.EqualTo(t.Before), "DEPLOY 直後の表示はラウンド前のままです。" + d);
            Assert.That(t.LabelAfterDeploy, Is.EqualTo(t.LabelBefore), "DEPLOY 直後のスコア文字はラウンド前のままです。" + d);

            // 3. 接近・衝突・Impact → 勝敗公開 → 勝利コアの後に、表示が変わる。
            Assert.That(t.DisplayChangeFrame, Is.GreaterThan(0), "表示スコアが更新されませんでした。" + d);
            Assert.That(t.ClashFrame, Is.GreaterThan(0).And.LessThan(t.DisplayChangeFrame), "衝突より前に表示が変わりました。" + d);
            Assert.That(t.ImpactFrame, Is.GreaterThan(0).And.LessThan(t.DisplayChangeFrame), "Impact より前に表示が変わりました。" + d);
            Assert.That(t.RevealFrame, Is.GreaterThan(0).And.LessThan(t.DisplayChangeFrame), "勝敗公開より前に表示が変わりました。" + d);
            Assert.That(t.VictoryStartFrame, Is.GreaterThan(0).And.LessThan(t.DisplayChangeFrame), "勝利コアより前に表示が変わりました。" + d);

            // 4. 収束した瞬間（勝利コアが消えたフレーム）に、ピップとスコア文字が同時に変わる。
            Assert.That(t.VictoryEndFrame, Is.EqualTo(t.DisplayChangeFrame), "勝利コアの収束と表示更新が同じフレームではありません。" + d);
            Assert.That(t.LabelChangeFrame, Is.EqualTo(t.DisplayChangeFrame), "ピップとスコア文字が同じフレームで変わりません。" + d);
            Assert.That(t.LabelChangedWithoutPips, Is.False, d);
            Assert.That(t.DisplayAtChange, Is.EqualTo(after), d);
            Assert.That(t.LabelAtChange, Is.EqualTo(ExpectedScoreText(after)), d);
            Assert.That(t.VictoryPip, Is.EqualTo(side == RoundWinner.Player ? after.x - 1 : after.y - 1), "収束先は新しいピップです。" + d);

            // 5. 結果バナーは表示更新の後。
            Assert.That(t.BannerFrame, Is.GreaterThan(t.DisplayChangeFrame), "結果バナーが表示更新より先に出ました。" + d);

            Assert.That(controller.IsScoreDisplayHeld, Is.False, "ラウンド後は留めていません。");
            Assert.That(Displayed(), Is.EqualTo(new Vector2Int(controller.Coordinator.PlayerWins, controller.Coordinator.CpuWins)));
        }

        // ---------------- 進行 ----------------

        private sealed class RoundTrace
        {
            internal RoundWinner Winner;
            internal bool Wanted;
            internal Vector2Int Before;
            internal string LabelBefore;
            internal Vector2Int LogicalAfterDeploy;
            internal Vector2Int DisplayAfterDeploy;
            internal string LabelAfterDeploy;

            internal int ClashFrame = -1;
            internal int ImpactFrame = -1;
            internal int RevealFrame = -1;
            internal int VictoryStartFrame = -1;
            internal int VictoryEndFrame = -1;
            internal int VictoryFrames;
            internal int VictoryPip = -1;
            internal int DisplayChangeFrame = -1;
            internal int LabelChangeFrame = -1;
            internal bool LabelChangedWithoutPips;
            internal Vector2Int DisplayAtChange;
            internal string LabelAtChange;
            internal int CoreBreakStartFrame = -1;
            internal Vector2Int DisplayAtCoreBreak;
            internal int BannerFrame = -1;
            internal int FinalVisibleFrame = -1;

            internal string Describe()
            {
                return "\n  before=" + Before + " logical=" + LogicalAfterDeploy + " display@deploy=" + DisplayAfterDeploy +
                       "\n  frames: clash=" + ClashFrame + " impact=" + ImpactFrame + " reveal=" + RevealFrame +
                       " victory=" + VictoryStartFrame + ".." + VictoryEndFrame + " display=" + DisplayChangeFrame +
                       " label=" + LabelChangeFrame + " coreBreak=" + CoreBreakStartFrame + " banner=" + BannerFrame +
                       " final=" + FinalVisibleFrame;
            }
        }

        /// <summary>狙った勝敗のラウンドが1回出るまで進めます（出なければ再戦して試し直します）。</summary>
        private IEnumerator PlayUntil(RoundWinner wanted, System.Action<RoundTrace> use)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                while (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    RoundTrace round = null;

                    yield return PlayRound(wanted, t => round = t);

                    if (round.Wanted && round.Winner == wanted)
                    {
                        use(round);
                        yield break;
                    }
                }
            }

            Assert.Fail(MaxAttempts + " 試合で " + wanted + " のラウンドを作れませんでした。");
        }

        private IEnumerator PlayRound(RoundWinner wanted, System.Action<RoundTrace> use)
        {
            BattleSession session = controller.Coordinator.Session;
            BattleMatchCueView cue = controller.MatchCue;
            VictoryCoreView victory = controller.VictoryCore;

            RoundTrace t = new RoundTrace
            {
                Before = new Vector2Int(session.PlayerWins, session.CpuWins),
                LabelBefore = ScoreText(),
            };

            string pick = Pick(session, wanted, out t.Wanted);

            controller.OnWheelDeployRequested(pick);

            Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

            t.LogicalAfterDeploy = new Vector2Int(session.PlayerWins, session.CpuWins);
            t.DisplayAfterDeploy = Displayed();
            t.LabelAfterDeploy = ScoreText();

            Vector2Int lastDisplay = t.DisplayAfterDeploy;
            string lastLabel = t.LabelAfterDeploy;
            bool lastVictory = false;
            int frame = 0;
            float startedAt = Time.realtimeSinceStartup;

            while (true)
            {
                frame++;

                if (t.ClashFrame < 0 && fx.Step == BattleFxPlayer.ClashStep.Approach)
                {
                    t.ClashFrame = frame;
                }

                if (t.ImpactFrame < 0 && fx.ImpactBurst.IsShowing)
                {
                    t.ImpactFrame = frame;
                }

                if (t.RevealFrame < 0 && controller.Coordinator.State == BattleUiState.ShowingResult)
                {
                    t.RevealFrame = frame;
                }

                if (victory.IsPlaying)
                {
                    t.VictoryFrames++;

                    if (t.VictoryStartFrame < 0)
                    {
                        t.VictoryStartFrame = frame;
                        t.VictoryPip = victory.LastPipIndex;
                    }
                }
                else if (lastVictory && t.VictoryEndFrame < 0)
                {
                    t.VictoryEndFrame = frame;
                }

                lastVictory = victory.IsPlaying;

                Vector2Int display = Displayed();
                string label = ScoreText();

                if (display != lastDisplay && t.DisplayChangeFrame < 0)
                {
                    t.DisplayChangeFrame = frame;
                    t.DisplayAtChange = display;
                }

                if (label != lastLabel)
                {
                    if (t.LabelChangeFrame < 0)
                    {
                        t.LabelChangeFrame = frame;
                        t.LabelAtChange = label;
                    }

                    if (display == lastDisplay)
                    {
                        t.LabelChangedWithoutPips = true;
                    }
                }

                lastDisplay = display;
                lastLabel = label;

                if (t.CoreBreakStartFrame < 0 && cue.Current == MatchCueKind.CoreBreak)
                {
                    t.CoreBreakStartFrame = frame;
                    t.DisplayAtCoreBreak = display;
                }

                if (t.BannerFrame < 0 && resultView.IsBannerVisible)
                {
                    t.BannerFrame = frame;
                }

                if (t.FinalVisibleFrame < 0 && resultView.IsFinalVisible)
                {
                    t.FinalVisibleFrame = frame;
                }

                if (!controller.IsRoundInProgress)
                {
                    break;
                }

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }

            t.Winner = controller.Coordinator.LastResult.Winner;

            use(t);
        }

        /// <summary>画面のピップが示している勝利数（ピップ側の実値）。スコア文字と別に数えます。</summary>
        private Vector2Int Displayed()
        {
            return new Vector2Int(pips.PlayerWins, pips.CpuWins);
        }

        private string ScoreText()
        {
            return scoreLabel.text;
        }

        /// <summary>画面と同じ文言カタログで作った、そのスコアの文字。</summary>
        private string ExpectedScoreText(Vector2Int wins)
        {
            FieldInfo field = typeof(BattleScreenController).GetField("battleText", BindingFlags.NonPublic | BindingFlags.Instance);
            BattleTextCatalog catalog = (BattleTextCatalog)field.GetValue(controller);

            return catalog.FormatScore(wins.x, wins.y);
        }

        private static string Pick(BattleSession session, RoundWinner wanted, out bool onGoal)
        {
            FieldInfo field = typeof(BattleSession).GetField("pendingCpuUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null);

            BattleUnit cpu = (BattleUnit)field.GetValue(session);
            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

            foreach (BattleUnit unit in available)
            {
                if (BattleRules.ResolveRound(unit, cpu).Winner == wanted)
                {
                    onGoal = true;
                    return unit.InstanceId;
                }
            }

            onGoal = false;
            return available[0].InstanceId;
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }
            }

            Assert.Fail(name + " が見つかりません。");
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
