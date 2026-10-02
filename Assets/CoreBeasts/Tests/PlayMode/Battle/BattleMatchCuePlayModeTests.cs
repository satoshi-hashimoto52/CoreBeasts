using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
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
    /// Phase 3「勝利コア・FINAL CORE・CORE BREAK」を、実際の Battle シーンで確かめます。
    ///
    /// 勝敗は本物の<see cref="BattleRules"/>が決めます。テストは CPU が非公開で選んだ個体を
    /// 読むだけで（書き換えません）、望む勝敗になるプレイヤー個体を循環リングの入口から出します。
    /// CPU 編成は乱数のため、狙った展開にならなかった試合は最後まで進めてから再戦して試し直します。
    /// </summary>
    public sealed class BattleMatchCuePlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        /// <summary>
        /// 狙った展開まで試し直す試合数の上限。「3対3 の後に最終ラウンドを引き分け」のように、
        /// 最後に残った個体と CPU の最後の選出の組み合わせに左右される展開があるため、多めに取ります。
        /// </summary>
        private const int MaxAttempts = 40;

        /// <summary>試合演出の間も動いてはいけないもの。</summary>
        private static readonly string[] StaticNames =
        {
            "SafeArea", "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton",
            "BattleRoot", "PlayerWheel", "HistoryLane", "ResultView", "RematchButton", "FinalHomeButton",
            "PipP1", "PipP2", "PipP3", "PipP4", "PipC1", "PipC2", "PipC3", "PipC4",
            "FxImpact", "FxMatchCue", "VictoryCoreFx",
        };

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleMatchCueView cue;
        private VictoryCoreView victory;
        private BattleResultView resultView;

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

            cue = controller.MatchCue;
            victory = controller.VictoryCore;
            resultView = UnityEngine.Object.FindAnyObjectByType<BattleResultView>();

            Assert.That(cue, Is.Not.Null, "BattleMatchCueView が配線されていません。");
            Assert.That(victory, Is.Not.Null, "VictoryCoreView が配線されていません。");
            Assert.That(resultView, Is.Not.Null);

            AssertCuesAtRest("開始時");
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 勝利コア獲得 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator BothSidesGainAVictoryCoreOnTheirNewPipOnly()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Player), r => rec = r, ReachedThreeAllThen(RoundWinner.Player));

            Assert.That(rec.VictoryCores.Exists(v => v.Side == RoundWinner.Player), Is.True, "PLAYER の勝利コア獲得がありません。");
            Assert.That(rec.VictoryCores.Exists(v => v.Side == RoundWinner.Cpu), Is.True, "CPU の勝利コア獲得がありません。");

            foreach (VictoryRecord v in rec.VictoryCores)
            {
                Assert.That(v.PipIndex, Is.EqualTo(v.WinsAfter - 1), "新しく点灯したピップだけを光らせます: " + v);
                Assert.That(v.Pip.name, Is.EqualTo((v.Side == RoundWinner.Player ? "PipP" : "PipC") + v.WinsAfter), v.ToString());
            }

            Assert.That(rec.VictoryCores.Count, Is.EqualTo(rec.Wins), "勝敗が付いたラウンドの数だけ出ます（引き分けでは出ません）。");
            Assert.That(rec.MovedStatic, Is.Empty, "ピップ・HUD・SafeArea が動きました: " + string.Join(", ", rec.MovedStatic));
        }

        // ---------------- FINAL CORE ----------------

        [UnityTest]
        [Timeout(1500000)]
        public IEnumerator FinalCoreAppearsOnceAtThreeAllAndNotAgainAfterADraw()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Draw), r => rec = r, ReachedThreeAllThen(RoundWinner.Draw));

            Assert.That(rec.FinalCoreStarts, Is.EqualTo(1), "FINAL CORE は1試合に1回です。");
            Assert.That(cue.FinalCorePlays, Is.EqualTo(1));
            Assert.That(controller.FinalCoreShown, Is.True);
            Assert.That(rec.FinalCoreAfterRound, Is.EqualTo(rec.ThreeAllRound), "3対3 になったラウンドの直後に出ます。");
            Assert.That(rec.FinalCoreWhileSelecting, Is.True, "次ラウンドの開始前（選択待ち）に出ます。");
            Assert.That(rec.FinalCoreWhileGateOpen, Is.False, "FINAL CORE の間は入力が閉じています。");
            Assert.That(rec.CoreBreakStarts, Is.EqualTo(0), "引き分けで試合が終われば CORE BREAK はありません。");
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(rec.MovedStatic, Is.Empty, string.Join(", ", rec.MovedStatic));
        }

        // ---------------- CORE BREAK ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator APlayerFourthWinBreaksTheCoreBeforePlayerWin()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Player), r => rec = r, ReachedThreeAllThen(RoundWinner.Player));

            AssertCoreBreakThenFinal(rec, RoundWinner.Player, "PLAYER WIN");
            Assert.That(rec.FinalCoreStarts, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ACpuFourthWinBreaksTheCoreBeforeCpuWin()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Cpu), r => rec = r, ReachedThreeAllThen(RoundWinner.Cpu));

            AssertCoreBreakThenFinal(rec, RoundWinner.Cpu, "CPU WIN");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AFourthWinFromThreeOneAlsoBreaksTheCore()
        {
            Recorder rec = null;

            yield return Drive(ThreeOneThenPlayer(), r => rec = r, ReachedFourOneFromThreeOne());

            AssertCoreBreakThenFinal(rec, RoundWinner.Player, "PLAYER WIN");
            Assert.That(rec.FinalCoreStarts, Is.EqualTo(0), "3対3 を通らなければ FINAL CORE はありません。");
        }

        // ---------------- 入力ゲート ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MashingDeployDuringTheCuesIsIgnored()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Player), r => { rec = r; r.MashDeploy = true; }, ReachedThreeAllThen(RoundWinner.Player));

            Assert.That(rec.MashAttempts, Is.GreaterThan(0), "演出中に DEPLOY を試しました。");
            Assert.That(rec.MashAccepted, Is.EqualTo(0), "演出中の DEPLOY が通りました。");
            Assert.That(rec.CueFramesWithGateOpen, Is.EqualTo(0), "演出中に入力が開いていました。");
        }

        // ---------------- 中断 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator DisablingTheScreenDuringTheCoreBreakLeavesNothingBehind()
        {
            yield return DriveUntil(ThreeOneThenPlayer(), () => cue.Current == MatchCueKind.CoreBreak && cue.Graphic.IsShowing);

            Assert.That(cue.Current, Is.EqualTo(MatchCueKind.CoreBreak), "CORE BREAK の途中まで進められませんでした。");

            controller.gameObject.SetActive(false);

            AssertCuesAtRest("OnDisable の直後");
            Assert.That(controller.IsRoundInProgress, Is.False, "入力ゲートも戻ります。");

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            AssertCuesAtRest("30フレーム後");
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished), "進行は矛盾なく決着まで進みます。");

            controller.gameObject.SetActive(true);
            yield return null;

            AssertCuesAtRest("再有効化の後");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator DisablingTheCueComponentDuringFinalCoreClearsItAndTheRoundStillEnds()
        {
            yield return DriveUntil(ThreeAllThen(RoundWinner.Player), () => cue.Current == MatchCueKind.FinalCore && cue.LabelAlpha > 0f);

            Assert.That(cue.Current, Is.EqualTo(MatchCueKind.FinalCore), "FINAL CORE の途中まで進められませんでした。");

            cue.gameObject.SetActive(false);

            AssertCuesAtRest("FX コンポーネント無効化の直後");

            yield return WaitRoundEnd();

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "次ラウンドへ進みます。");

            cue.gameObject.SetActive(true);
            yield return null;

            AssertCuesAtRest("再有効化の後");
            Assert.That(controller.FinalCoreShown, Is.True, "中断しても同じ試合で FINAL CORE を出し直しません。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator LeavingTheSceneDuringTheCoreBreakLeavesNothingRunning()
        {
            yield return DriveUntil(ThreeOneThenPlayer(), () => cue.Current == MatchCueKind.CoreBreak);

            Assert.That(cue.Current, Is.EqualTo(MatchCueKind.CoreBreak));

            Scene battle = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("BattleMatchCueTests_Empty");

            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(battle);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.That(cue == null, Is.True);
            Assert.That(victory == null, Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<CoreBreakGraphic>(), Is.Null);

            LogAssert.NoUnexpectedReceived();
        }

        // ---------------- 再戦と繰り返し ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ARematchResetsTheFinalCoreRecord()
        {
            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Player), r => rec = r, ReachedThreeAllThen(RoundWinner.Player));

            Assert.That(controller.FinalCoreShown, Is.True);

            controller.OnRematchClicked();
            yield return null;

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(controller.FinalCoreShown, Is.False, "再戦で FINAL CORE の再生済み状態が戻ります。");
            Assert.That(controller.LastMatchPlan, Is.Null);
            Assert.That(cue.FinalCorePlays, Is.EqualTo(0));
            AssertCuesAtRest("再戦直後");

            // 新しい試合でも 3対3 になれば、もう一度出ます。
            yield return Drive(ThreeAllThen(RoundWinner.Cpu), r => rec = r, ReachedThreeAllThen(RoundWinner.Cpu));

            Assert.That(rec.FinalCoreStarts, Is.EqualTo(1), "再戦後の試合でも FINAL CORE は1回出ます。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator TenMatchesCreateNoGraphicsOrGameObjects()
        {
            int baselineObjects = -1;
            int baselineGraphics = -1;
            int baselineComponents = -1;

            for (int match = 0; match < 10; match++)
            {
                Recorder rec = new Recorder(this);

                // 同じ試合の中でも節目が出るよう、3対3 を狙います（届かなくても試合は最後まで進めます）。
                yield return PlayMatch(ThreeAllThen(match % 2 == 0 ? RoundWinner.Player : RoundWinner.Cpu), rec);

                Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished), "match " + match);
                AssertCuesAtRest("match " + match + " の後");

                controller.OnRematchClicked();
                yield return null;
                yield return null;

                // 1試合目の再戦直後を基準にします（初回だけ遅れて作られる表示物を数えないためです）。
                if (match == 0)
                {
                    baselineObjects = CountScene<Transform>();
                    baselineGraphics = CountScene<Graphic>();
                    baselineComponents = CountScene<Component>();
                    continue;
                }

                Assert.That(CountScene<Transform>(), Is.EqualTo(baselineObjects), "match " + match + " の後で GameObject が増えました。");
                Assert.That(CountScene<Graphic>(), Is.EqualTo(baselineGraphics), "match " + match + " の後で Graphic が増えました。");
                Assert.That(CountScene<Component>(), Is.EqualTo(baselineComponents), "match " + match + " の後で Component が増えました。");
            }
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FxOffStillFinishesTheMatchWithShortTextCues()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            Recorder rec = null;

            yield return Drive(ThreeAllThen(RoundWinner.Player), r => rec = r, ReachedThreeAllThen(RoundWinner.Player));

            Assert.That(controller.FxEnabled, Is.False, "再戦を挟んでも FX 設定は保たれます。");
            Assert.That(rec.CueGraphicFrames, Is.EqualTo(0), "FX OFF ではリング・亀裂・フラッシュを出しません。");
            Assert.That(rec.VictoryGraphicFrames, Is.EqualTo(0), "FX OFF では勝利コア獲得の演出を省きます。");
            Assert.That(rec.FinalCoreStarts, Is.EqualTo(1), "FINAL CORE は短い文字で伝えます。");
            Assert.That(rec.SawFinalCoreText, Is.True);
            Assert.That(rec.CoreBreakStarts, Is.EqualTo(1), "CORE BREAK は短い文字で伝えます。");
            Assert.That(rec.SawCoreBreakText, Is.True);
            Assert.That(rec.FinalVisibleDuringCoreBreak, Is.False);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(resultView.IsFinalVisible, Is.True);
            Assert.That(controller.Coordinator.PlayerWins, Is.EqualTo(BattleSession.WinsRequired), "スコア更新と結果進行は同じです。");

            controller.OnFxToggleClicked();
        }

        // ---------------- 検査 ----------------

        private void AssertCoreBreakThenFinal(Recorder rec, RoundWinner side, string title)
        {
            Assert.That(rec.CoreBreakStarts, Is.EqualTo(1), "4勝目で CORE BREAK が1回出ます。");
            Assert.That(rec.CoreBreakSide, Is.EqualTo(side));
            Assert.That(rec.CoreBreakGraphicSeen, Is.True, "圧縮・亀裂・フラッシュが描かれます。");
            Assert.That(rec.SawCoreBreakText, Is.True, "CORE BREAK の文字が出ます。");
            Assert.That(rec.VictoryBeforeCoreBreak, Is.True, "勝利コア獲得の後に CORE BREAK です。");
            Assert.That(rec.FinalVisibleDuringCoreBreak, Is.False, "CORE BREAK の途中で最終結果が出ました。");
            Assert.That(rec.FinalVisibleFrame, Is.GreaterThan(rec.CoreBreakEndFrame), "最終結果は CORE BREAK の後です。");
            Assert.That(resultView.IsFinalVisible, Is.True);
            Assert.That(Find("FinalTitleLabel").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(title));
            Assert.That(rec.MovedStatic, Is.Empty, "HUD・SafeArea が動きました: " + string.Join(", ", rec.MovedStatic));
            AssertCuesAtRest("決着後");
        }

        private void AssertCuesAtRest(string label)
        {
            Assert.That(cue.IsPlaying, Is.False, label + ": 節目の表示が再生中です。");
            Assert.That(cue.Current, Is.EqualTo(MatchCueKind.None), label);
            Assert.That(cue.Graphic.IsShowing, Is.False, label + ": 節目の Graphic が残っています。");
            Assert.That(cue.Label.text, Is.Empty, label + ": FINAL CORE / CORE BREAK の文字が残っています。");
            Assert.That(cue.LabelAlpha, Is.EqualTo(0f), label);
            Assert.That(victory.IsPlaying, Is.False, label + ": 勝利コア獲得が再生中です。");
            Assert.That(victory.Graphic.IsShowing, Is.False, label + ": 勝利コアの Graphic が残っています。");
        }

        // ---------------- 展開の狙い ----------------

        /// <summary>このラウンドで狙う勝敗（優先順）。空なら何でもよい（試合を終わらせるだけ）。</summary>
        private delegate RoundWinner[] Goal(int playerWins, int cpuWins);

        /// <summary>狙った展開になったか。</summary>
        private delegate bool Achieved(Recorder rec);

        /// <summary>引き分けを挟まずに 3対3 へ並べ、最後に <paramref name="last"/>。</summary>
        private static Goal ThreeAllThen(RoundWinner last)
        {
            return (p, c) =>
            {
                if (p == 3 && c == 3)
                {
                    return new[] { last };
                }

                if (p >= 3)
                {
                    return new[] { RoundWinner.Cpu };
                }

                if (c >= 3)
                {
                    return new[] { RoundWinner.Player };
                }

                return p <= c
                    ? new[] { RoundWinner.Player, RoundWinner.Cpu }
                    : new[] { RoundWinner.Cpu, RoundWinner.Player };
            };
        }

        /// <summary>3対1 まで進め、PLAYER が4勝目。</summary>
        private static Goal ThreeOneThenPlayer()
        {
            return (p, c) =>
            {
                if (p < 3 && c < 1)
                {
                    return new[] { RoundWinner.Player, RoundWinner.Cpu };
                }

                if (p < 3)
                {
                    return new[] { RoundWinner.Player };
                }

                if (c < 1)
                {
                    return new[] { RoundWinner.Cpu };
                }

                return new[] { RoundWinner.Player };
            };
        }

        private static Achieved ReachedThreeAllThen(RoundWinner last)
        {
            return rec => rec.ThreeAllRound > 0 && rec.ThreeAllRound < BattleSession.MaxRounds &&
                          rec.Results.Count == rec.ThreeAllRound + 1 &&
                          rec.Results[rec.Results.Count - 1] == last;
        }

        private static Achieved ReachedFourOneFromThreeOne()
        {
            return rec => rec.SawScore(3, 1) && rec.FinalPlayerWins == 4 && rec.FinalCpuWins == 1;
        }

        /// <summary>狙った展開になるまで、試合を最後まで進めては再戦します。</summary>
        private IEnumerator Drive(Goal goal, Action<Recorder> use, Achieved achieved)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                Recorder rec = new Recorder(this);
                use(rec);

                yield return PlayMatch(goal, rec);

                if (achieved(rec))
                {
                    TestContext.WriteLine("attempt " + (attempt + 1) + ": " + rec.Describe());
                    yield break;
                }

                TestContext.WriteLine("attempt " + (attempt + 1) + " missed: " + rec.Describe());
            }

            Assert.Fail(MaxAttempts + " 試合で狙った展開になりませんでした。");
        }

        /// <summary>狙った展開のまま、<paramref name="stop"/>が成り立つフレームまで進めます。</summary>
        private IEnumerator DriveUntil(Goal goal, Func<bool> stop)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                Recorder rec = new Recorder(this) { Stop = stop };

                yield return PlayMatch(goal, rec);

                if (rec.Stopped)
                {
                    yield break;
                }
            }

            Assert.Fail(MaxAttempts + " 試合で狙った場面へ進められませんでした。");
        }

        /// <summary>1試合を最後まで（または <see cref="Recorder.Stop"/> まで）進めます。</summary>
        private IEnumerator PlayMatch(Goal goal, Recorder rec)
        {
            rec.CaptureStatic();

            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                BattleSession session = controller.Coordinator.Session;
                bool onGoal = true;
                string pick = PickForLastTwoRounds(session, goal) ?? Pick(session, goal(session.PlayerWins, session.CpuWins), out onGoal);

                rec.OffGoal |= !onGoal;

                int winsBefore = session.PlayerWins + session.CpuWins;
                int round = session.CurrentRound;

                controller.OnWheelDeployRequested(pick);

                Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

                float startedAt = Time.realtimeSinceStartup;

                while (controller.IsRoundInProgress)
                {
                    rec.Frame(round);

                    if (rec.Stopped)
                    {
                        yield break;
                    }

                    yield return null;

                    Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
                }

                rec.Frame(round);
                rec.EndRound(round, winsBefore);

                if (rec.Stopped)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// 残り2体なら最終ラウンドまで読み、今回と最終ラウンドの勝敗がどちらも狙いどおりになる出し順を選びます。
        /// ATTRIBUTE LINK（Phase 4）の加算で POWER の同値が減り、最後の引き分けのような展開は
        /// 1ラウンド先だけを見ていると届きにくくなったためです。届く出し順が無ければ null を返します。
        /// </summary>
        private static string PickForLastTwoRounds(BattleSession session, Goal goal)
        {
            foreach (BattleLinkPrediction.TwoRoundPlan plan in BattleLinkPrediction.PredictLastTwoRounds(session))
            {
                if (System.Array.IndexOf(goal(session.PlayerWins, session.CpuWins), plan.First) < 0)
                {
                    continue;
                }

                int p = session.PlayerWins + (plan.First == RoundWinner.Player ? 1 : 0);
                int c = session.CpuWins + (plan.First == RoundWinner.Cpu ? 1 : 0);
                bool decided = BattleSession.EvaluateMatchState(p, c, session.CompletedRounds + 1, BattleSession.MaxRounds) != BattleMatchState.InProgress;

                if (decided || System.Array.IndexOf(goal(p, c), plan.Second) >= 0)
                {
                    return plan.Now.InstanceId;
                }
            }

            return null;
        }

        /// <summary>
        /// CPU の非公開の選出を読み、狙った勝敗になる個体を選びます（勝敗は BattleRules が決めます）。
        /// 狙いどおりの個体が無ければ、引き分けにならない個体、それも無ければ残りの先頭を返します。
        /// </summary>
        private static string Pick(BattleSession session, RoundWinner[] wanted, out bool onGoal)
        {
            FieldInfo field = typeof(BattleSession).GetField("pendingCpuUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, "BattleSession.pendingCpuUnit が見つかりません。");

            BattleUnit cpu = (BattleUnit)field.GetValue(session);

            Assert.That(cpu, Is.Not.Null, "CPU の選出が済んでいません。");

            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

            foreach (RoundWinner want in wanted)
            {
                foreach (BattleUnit unit in available)
                {
                    if (BattleLinkPrediction.Resolve(session, unit, cpu).Winner == want)
                    {
                        onGoal = true;
                        return unit.InstanceId;
                    }
                }
            }

            onGoal = false;

            foreach (BattleUnit unit in available)
            {
                if (BattleLinkPrediction.Resolve(session, unit, cpu).Winner != RoundWinner.Draw)
                {
                    return unit.InstanceId;
                }
            }

            return available[0].InstanceId;
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

        // ---------------- 観測 ----------------

        private sealed class VictoryRecord
        {
            internal RoundWinner Side;
            internal int PipIndex;
            internal int WinsAfter;
            internal RectTransform Pip;

            public override string ToString()
            {
                return Side + " pip " + PipIndex + " wins " + WinsAfter;
            }
        }

        private sealed class Recorder
        {
            private readonly BattleMatchCuePlayModeTests owner;
            private readonly Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
            private readonly Dictionary<string, Vector3> scales = new Dictionary<string, Vector3>();

            private int frame;
            private MatchCueKind lastCue = MatchCueKind.None;
            private bool lastVictoryPlaying;
            private bool victoryThisRound;

            internal readonly List<VictoryRecord> VictoryCores = new List<VictoryRecord>();
            internal readonly List<RoundWinner> Results = new List<RoundWinner>();
            internal readonly List<string> MovedStatic = new List<string>();
            internal readonly List<string> Scores = new List<string>();

            internal bool MashDeploy;
            internal int MashAttempts;
            internal int MashAccepted;
            internal int CueFramesWithGateOpen;

            internal int FinalCoreStarts;
            internal int FinalCoreAfterRound;
            internal bool FinalCoreWhileSelecting;
            internal bool FinalCoreWhileGateOpen;

            internal int CoreBreakStarts;
            internal RoundWinner CoreBreakSide = RoundWinner.Draw;
            internal int CoreBreakEndFrame = -1;
            internal bool CoreBreakGraphicSeen;
            internal bool VictoryBeforeCoreBreak;
            internal bool FinalVisibleDuringCoreBreak;
            internal int FinalVisibleFrame = -1;

            internal bool SawFinalCoreText;
            internal bool SawCoreBreakText;
            internal int CueGraphicFrames;
            internal int VictoryGraphicFrames;

            internal int ThreeAllRound;
            internal int Wins;
            internal int FinalPlayerWins;
            internal int FinalCpuWins;
            internal bool OffGoal;

            internal Func<bool> Stop;
            internal bool Stopped;

            internal Recorder(BattleMatchCuePlayModeTests owner)
            {
                this.owner = owner;
            }

            internal void CaptureStatic()
            {
                foreach (string name in StaticNames)
                {
                    Transform t = Find(name);

                    positions[name] = t.position;
                    scales[name] = t.localScale;
                }
            }

            internal void Frame(int round)
            {
                frame++;

                BattleMatchCueView cue = owner.cue;
                VictoryCoreView victory = owner.victory;
                BattleScreenController controller = owner.controller;

                foreach (KeyValuePair<string, Vector3> entry in positions)
                {
                    Transform t = Find(entry.Key);

                    if ((t.position != entry.Value || t.localScale != scales[entry.Key]) && !MovedStatic.Contains(entry.Key))
                    {
                        MovedStatic.Add(entry.Key);
                    }
                }

                // 勝利コア獲得
                if (victory.IsPlaying && !lastVictoryPlaying)
                {
                    BattleMatchPresentationPlan plan = controller.LastMatchPlan;

                    VictoryCores.Add(new VictoryRecord
                    {
                        Side = victory.LastSide,
                        PipIndex = victory.LastPipIndex,
                        WinsAfter = victory.LastSide == RoundWinner.Player
                            ? controller.Coordinator.PlayerWins
                            : controller.Coordinator.CpuWins,
                        Pip = victory.LastPip,
                    });

                    victoryThisRound = true;

                    Assert.That(plan, Is.Not.Null);
                    Assert.That(plan.VictorySide, Is.EqualTo(victory.LastSide));
                }

                lastVictoryPlaying = victory.IsPlaying;

                if (victory.Graphic.IsShowing)
                {
                    VictoryGraphicFrames++;
                }

                // 節目
                if (cue.Current != lastCue)
                {
                    if (cue.Current == MatchCueKind.FinalCore)
                    {
                        FinalCoreStarts++;
                        FinalCoreAfterRound = round;
                        FinalCoreWhileSelecting = controller.Coordinator.State == BattleUiState.Selecting;
                    }
                    else if (cue.Current == MatchCueKind.CoreBreak)
                    {
                        CoreBreakStarts++;
                        CoreBreakSide = controller.Coordinator.PlayerWins >= BattleSession.WinsRequired
                            ? RoundWinner.Player
                            : RoundWinner.Cpu;
                        VictoryBeforeCoreBreak = victoryThisRound || !controller.FxEnabled;
                    }

                    if (lastCue == MatchCueKind.CoreBreak)
                    {
                        CoreBreakEndFrame = frame;
                    }

                    lastCue = cue.Current;
                }

                if (cue.Current != MatchCueKind.None)
                {
                    if (!controller.IsRoundInProgress)
                    {
                        CueFramesWithGateOpen++;
                    }

                    if (cue.Current == MatchCueKind.FinalCore && !controller.IsRoundInProgress)
                    {
                        FinalCoreWhileGateOpen = true;
                    }

                    if (cue.Graphic.IsShowing)
                    {
                        CueGraphicFrames++;

                        if (cue.Current == MatchCueKind.CoreBreak)
                        {
                            CoreBreakGraphicSeen = true;
                        }
                    }

                    if (cue.LabelAlpha > 0f)
                    {
                        SawFinalCoreText |= cue.Label.text == BattleMatchCueView.FinalCoreText;
                        SawCoreBreakText |= cue.Label.text == BattleMatchCueView.CoreBreakText;
                    }

                    if (cue.Current == MatchCueKind.CoreBreak && owner.resultView.IsFinalVisible)
                    {
                        FinalVisibleDuringCoreBreak = true;
                    }
                }

                if (FinalVisibleFrame < 0 && owner.resultView.IsFinalVisible)
                {
                    FinalVisibleFrame = frame;
                }

                // 演出中の連打
                if (MashDeploy && (cue.IsPlaying || victory.IsPlaying))
                {
                    BattleSession session = controller.Coordinator.Session;
                    int completed = session.CompletedRounds;

                    if (session.PlayerAvailableUnits.Count > 0)
                    {
                        MashAttempts++;
                        controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);

                        if (session.CompletedRounds != completed)
                        {
                            MashAccepted++;
                        }
                    }
                }

                if (Stop != null && !Stopped && Stop())
                {
                    Stopped = true;
                }
            }

            internal void EndRound(int round, int winsBefore)
            {
                BattleSession session = owner.controller.Coordinator.Session;
                RoundResult result = owner.controller.Coordinator.LastResult;

                Results.Add(result.Winner);

                if (result.Winner != RoundWinner.Draw)
                {
                    Wins++;
                }

                Scores.Add(session.PlayerWins + "-" + session.CpuWins);

                if (ThreeAllRound == 0 && session.PlayerWins == 3 && session.CpuWins == 3)
                {
                    ThreeAllRound = round;
                }

                FinalPlayerWins = session.PlayerWins;
                FinalCpuWins = session.CpuWins;
                victoryThisRound = false;
            }

            internal bool SawScore(int player, int cpu)
            {
                return Scores.Contains(player + "-" + cpu);
            }

            internal string Describe()
            {
                return string.Join(" ", Scores) + " finalCore=" + FinalCoreStarts + " coreBreak=" + CoreBreakStarts +
                       " victory=" + VictoryCores.Count + (OffGoal ? " (off goal)" : string.Empty);
            }
        }

        // ---------------- 道具 ----------------

        private static int CountScene<T>() where T : Component
        {
            int count = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
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
