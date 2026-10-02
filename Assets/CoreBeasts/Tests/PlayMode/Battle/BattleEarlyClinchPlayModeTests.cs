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
    /// 試合の勝敗が確定した瞬間の CORE BREAK（4勝目・早期決着・最終ラウンドの引き分けによる決着）を、
    /// 実際の Battle シーンと実ラウンド経路で確かめます。
    ///
    /// 勝敗は本物の <see cref="BattleRules"/>、決着は本物の <see cref="BattleSession"/> が決めます。
    /// テストは CPU が非公開で選んだ個体を読むだけで（書き換えません）、狙った展開へ届く勝敗を
    /// <see cref="BattleSession.EvaluateMatchState"/>（純粋関数）で探し、その勝敗になる個体を出します。
    /// CPU 編成は乱数のため、狙った展開にならなかった試合は最後まで進めてから再戦して試し直します。
    /// </summary>
    public sealed class BattleEarlyClinchPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        /// <summary>
        /// 狙った展開まで試し直す試合数の上限。3対1 の早期決着には引き分けが2回要りますが、
        /// ATTRIBUTE LINK（Phase 4）の加算で POWER の同値が減り、引き分けが起きにくくなったため多めに取ります。
        /// </summary>
        private const int MaxAttempts = 40;

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleMatchCueView cue;
        private VictoryCoreView victory;
        private BattleResultView resultView;
        private BattleScorePipsView pips;
        private BattleTextCatalog catalog;

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
            resultView = Object.FindAnyObjectByType<BattleResultView>();
            pips = Object.FindAnyObjectByType<BattleScorePipsView>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            cue = controller.MatchCue;
            victory = controller.VictoryCore;

            FieldInfo field = typeof(BattleScreenController).GetField("battleText", BindingFlags.NonPublic | BindingFlags.Instance);
            catalog = (BattleTextCatalog)field.GetValue(controller);

            Assert.That(cue, Is.Not.Null);
            Assert.That(victory, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 早期決着 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator PlayerEarlyClinchAtThreeToOnePlaysCoreBreakBeforeFinalResult()
        {
            MatchTrace m = null;

            yield return Drive(PlayerThreeToOne, x => m = x);

            AssertClinch(m, RoundWinner.Player, 3, 1, 6, expectVictory: true);
            Assert.That(controller.Coordinator.PlayerWins, Is.LessThan(BattleSession.WinsRequired), "4勝に届かない早期決着です。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CpuEarlyClinchAtOneToThreePlaysCoreBreakBeforeFinalResult()
        {
            MatchTrace m = null;

            yield return Drive(CpuOneToThree, x => m = x);

            AssertClinch(m, RoundWinner.Cpu, 1, 3, 6, expectVictory: true);
            Assert.That(controller.Coordinator.CpuWins, Is.LessThan(BattleSession.WinsRequired));
        }

        // ---------------- 4勝・引き分けによる決着・試合の引き分け ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ReachingFourWinsStillPlaysCoreBreakExactlyOnce()
        {
            MatchTrace m = null;

            yield return Drive(PlayerFourToNil, x => m = x);

            AssertClinch(m, RoundWinner.Player, 4, 0, 4, expectVictory: true);
            Assert.That(cue.CoreBreakPlays, Is.EqualTo(1), "この試合で CORE BREAK は1回だけです。");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator AFinalDrawThatConfirmsTheMatchWinnerPlaysCoreBreakForTheMatchWinner()
        {
            MatchTrace m = null;

            yield return Drive(PlayerByFinalDraw, x => m = x);

            Assert.That(m.Last.Winner, Is.EqualTo(RoundWinner.Draw), "最終ラウンドは引き分けです。" + m.Describe());
            AssertClinch(m, RoundWinner.Player, 3, 2, 7, expectVictory: false);
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ADrawnMatchDoesNotPlayCoreBreak()
        {
            MatchTrace m = null;

            yield return Drive(DrawnMatch, x => m = x);

            Assert.That(controller.Coordinator.MatchState, Is.EqualTo(BattleMatchState.Draw), m.Describe());
            Assert.That(m.CoreBreakStarts, Is.EqualTo(0), "試合の引き分けでは CORE BREAK を出しません。" + m.Describe());
            Assert.That(cue.CoreBreakPlays, Is.EqualTo(0));
            Assert.That(resultView.IsFinalVisible, Is.True);
            Assert.That(FinalTitle(), Is.EqualTo(catalog.MatchDraw));
            AssertAtRest("試合の引き分けの後");
        }

        // ---------------- 早期決着の順序・表示・入力 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator ScoreAndPipsAreRevealedBeforeEarlyClinchCoreBreak()
        {
            MatchTrace m = null;

            yield return Drive(PlayerThreeToOne, x => m = x);

            RoundTrace r = m.Last;
            string d = m.Describe();

            Assert.That(r.ScoreRevealFrame, Is.GreaterThan(0), "決着ラウンドでスコアが公開されました。" + d);
            Assert.That(r.CoreBreakStartFrame, Is.GreaterThan(r.ScoreRevealFrame), "スコアとピップは CORE BREAK の前に公開されます。" + d);
            Assert.That(r.PipsAtCoreBreak, Is.EqualTo(new Vector2Int(3, 1)), "CORE BREAK の時点でピップは 3対1 です。" + d);
            Assert.That(r.ScoreTextAtCoreBreak, Is.EqualTo(catalog.FormatScore(3, 1)), "CORE BREAK の時点でスコア文字は 3対1 です。" + d);
            Assert.That(r.ScoreTextChangedWithoutPips, Is.False, "スコア文字とピップは同じフレームで変わります。" + d);
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RoundLabelNeverAdvancesAfterEarlyClinch()
        {
            MatchTrace m = null;

            yield return Drive(PlayerThreeToOne, x => m = x);

            RoundTrace r = m.Last;
            string last = catalog.FormatRound(6, controller.Coordinator.MaxRounds);
            string nonexistent = catalog.FormatRound(7, controller.Coordinator.MaxRounds);

            Assert.That(r.Round, Is.EqualTo(6));
            Assert.That(r.RoundLabels, Is.EquivalentTo(new[] { last }), "決着ラウンドの間は ROUND 6 だけを表示します。" + m.Describe());
            Assert.That(r.RoundLabels, Does.Not.Contain(nonexistent));
            Assert.That(RoundLabel(), Is.EqualTo(last), "決着後も ROUND 6 のままで、存在しない ROUND 7 を出しません。");

            yield return null;
            yield return null;

            Assert.That(RoundLabel(), Is.EqualTo(last));
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator InputStaysClosedUntilEarlyClinchCoreBreakAndFinalResultFinish()
        {
            MatchTrace m = null;

            yield return Drive(PlayerThreeToOne, x => { m = x; x.MashDuringClinch = true; });

            RoundTrace r = m.Last;
            string d = m.Describe();

            Assert.That(r.MashAttempts, Is.GreaterThan(0), "CORE BREAK と結果表示の間に DEPLOY と REMATCH を試しました。" + d);
            Assert.That(r.MashAccepted, Is.EqualTo(0), "演出中の入力が通りました。" + d);
            Assert.That(r.CoreBreakFramesWithGateOpen, Is.EqualTo(0), "CORE BREAK 中に入力が開いていました。" + d);
            Assert.That(r.FinalVisibleWhileCoreBreak, Is.False, d);
            Assert.That(controller.IsRoundInProgress, Is.False, "決着処理の後は開きます。");
            Assert.That(controller.Coordinator.CanRematch, Is.True);
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator FxOffEarlyClinchUsesTheShortCoreBreakPath()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            MatchTrace m = null;

            yield return Drive(PlayerThreeToOne, x => m = x);

            RoundTrace r = m.Last;
            string d = m.Describe();

            Assert.That(controller.FxEnabled, Is.False);
            AssertClinch(m, RoundWinner.Player, 3, 1, 6, expectVictory: false);
            Assert.That(r.CoreBreakGraphicFrames, Is.EqualTo(0), "FX OFF では圧縮・亀裂・フラッシュを出しません。" + d);
            Assert.That(r.SawCoreBreakText, Is.True, "CORE BREAK は短い文字で伝えます。" + d);
            Assert.That(r.CoreBreakSeconds, Is.EqualTo(BattleMatchPresentationPlan.TextOnlyDuration).Within(0.1f), "文字だけの短い時間です。" + d);

            controller.OnFxToggleClicked();
        }

        // ---------------- 中断 ----------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator InterruptingEarlyClinchCoreBreakClearsAllPresentationState()
        {
            bool stopped = false;

            for (int attempt = 0; attempt < MaxAttempts && !stopped; attempt++)
            {
                RestartIfFinished();
                yield return null;

                MatchTrace m = new MatchTrace();

                yield return PlayMatch(PlayerThreeToOne, m, () => cue.Current == MatchCueKind.CoreBreak && cue.Graphic.IsShowing);

                stopped = m.Stopped && controller.Coordinator.PlayerWins == 3 && controller.Coordinator.CpuWins == 1;

                if (m.Stopped && !stopped)
                {
                    yield return FinishCurrentRound();
                }
            }

            Assert.That(stopped, Is.True, "3対1 の早期決着の CORE BREAK まで進められませんでした。");

            BattleSession session = controller.Coordinator.Session;

            controller.gameObject.SetActive(false);

            AssertAtRest("OnDisable の直後");
            Assert.That(controller.IsRoundInProgress, Is.False, "入力ゲートを戻します。");
            Assert.That(controller.IsScoreDisplayHeld, Is.False);
            Assert.That(controller.IsRoundDisplayHeld, Is.False);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished), "進行は矛盾なく決着まで進みます。");
            Assert.That(pips.PlayerWins, Is.EqualTo(session.PlayerWins));
            Assert.That(pips.CpuWins, Is.EqualTo(session.CpuWins));
            Assert.That(RoundLabel(), Is.EqualTo(catalog.FormatRound(session.CompletedRounds, controller.Coordinator.MaxRounds)));

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            AssertAtRest("30フレーム後");

            controller.gameObject.SetActive(true);
            yield return null;

            AssertAtRest("再有効化の後");
        }

        // ---------------- 検査 ----------------

        private void AssertClinch(MatchTrace m, RoundWinner side, int playerWins, int cpuWins, int rounds, bool expectVictory)
        {
            RoundTrace r = m.Last;
            string d = m.Describe();

            Assert.That(controller.Coordinator.PlayerWins, Is.EqualTo(playerWins), d);
            Assert.That(controller.Coordinator.CpuWins, Is.EqualTo(cpuWins), d);
            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(rounds), d);
            Assert.That(controller.Coordinator.MatchState,
                Is.EqualTo(side == RoundWinner.Player ? BattleMatchState.PlayerWin : BattleMatchState.CpuWin), d);

            Assert.That(m.CoreBreakStarts, Is.EqualTo(1), "決着で CORE BREAK が1回だけ出ます。" + d);
            Assert.That(r.CoreBreakStartFrame, Is.GreaterThan(0), "決着のラウンドで出ます。" + d);
            Assert.That(r.CoreBreakSide, Is.EqualTo(side), "対象は試合勝者です。" + d);

            // 衝突・Impact → 結果公開 → 勝利コア → スコア公開 → CORE BREAK → 結果バナー → 片付け → 最終結果
            Assert.That(r.RevealFrame, Is.GreaterThan(0).And.LessThan(r.CoreBreakStartFrame), "結果公開の後です。" + d);

            if (expectVictory)
            {
                Assert.That(r.VictoryEndFrame, Is.GreaterThan(0).And.LessThanOrEqualTo(r.CoreBreakStartFrame), "勝利コアの後です。" + d);
                Assert.That(r.ScoreRevealFrame, Is.GreaterThan(0).And.LessThan(r.CoreBreakStartFrame), "スコア公開の後です。" + d);
            }
            else
            {
                Assert.That(r.VictoryFrames, Is.EqualTo(0), "ラウンド勝者がいなければ勝利コアはありません（FX OFF も省きます）。" + d);
            }

            // CoreBreakEndFrame は CORE BREAK が消えた最初のフレームです。結果バナーはそのフレーム以降に出ます
            // （CORE BREAK の最後の表示フレームより後なので、画面上で重なりません）。
            Assert.That(r.CoreBreakEndFrame, Is.GreaterThan(r.CoreBreakStartFrame), "CORE BREAK が最後まで再生されました。" + d);
            Assert.That(r.BannerFrame, Is.GreaterThanOrEqualTo(r.CoreBreakEndFrame), "結果バナーは CORE BREAK の後です。" + d);
            if (controller.FxEnabled)
            {
                // FX OFF の片付けは待たずに同じフレームで終わるため、観測できるのは FX ON のときだけです。
                Assert.That(r.ArchivingFrame, Is.GreaterThan(r.CoreBreakEndFrame), "片付けは CORE BREAK の後です。" + d);
            }
            Assert.That(r.FinalVisibleFrame, Is.GreaterThan(r.BannerFrame), "最終結果は結果バナーの後です。" + d);
            Assert.That(r.FinalVisibleWhileCoreBreak, Is.False, d);

            Assert.That(resultView.IsFinalVisible, Is.True);
            Assert.That(FinalTitle(), Is.EqualTo(side == RoundWinner.Player ? catalog.PlayerWin : catalog.CpuWin));
            AssertAtRest("決着後");
        }

        private void AssertAtRest(string label)
        {
            Assert.That(cue.IsPlaying, Is.False, label);
            Assert.That(cue.Current, Is.EqualTo(MatchCueKind.None), label);
            Assert.That(cue.Graphic.IsShowing, Is.False, label + ": CORE BREAK の Graphic が残っています。");
            Assert.That(cue.Label.text, Is.Empty, label + ": CORE BREAK の文字が残っています。");
            Assert.That(cue.LabelAlpha, Is.EqualTo(0f), label);
            Assert.That(victory.IsPlaying, Is.False, label);
            Assert.That(victory.Graphic.IsShowing, Is.False, label);
        }

        // ---------------- 狙う展開 ----------------

        /// <summary>狙う最終状態。<see cref="LastRound"/>が Draw 以外なら最終ラウンドの勝敗も固定します。</summary>
        private sealed class Scenario
        {
            internal int PlayerWins;
            internal int CpuWins;
            internal int Rounds;
            internal RoundWinner? LastRound;
            internal BattleMatchState State;
        }

        /// <summary>W/L/W/D/D/W のように、6ラウンドで 3対1（最後は PLAYER の勝ち）。</summary>
        private static readonly Scenario PlayerThreeToOne = new Scenario
        {
            PlayerWins = 3, CpuWins = 1, Rounds = 6, LastRound = RoundWinner.Player, State = BattleMatchState.PlayerWin,
        };

        private static readonly Scenario CpuOneToThree = new Scenario
        {
            PlayerWins = 1, CpuWins = 3, Rounds = 6, LastRound = RoundWinner.Cpu, State = BattleMatchState.CpuWin,
        };

        private static readonly Scenario PlayerFourToNil = new Scenario
        {
            PlayerWins = 4, CpuWins = 0, Rounds = 4, LastRound = RoundWinner.Player, State = BattleMatchState.PlayerWin,
        };

        /// <summary>最終ラウンドの引き分けで 3対2 のまま終わり、PLAYER の勝ちが確定。</summary>
        private static readonly Scenario PlayerByFinalDraw = new Scenario
        {
            PlayerWins = 3, CpuWins = 2, Rounds = 7, LastRound = RoundWinner.Draw, State = BattleMatchState.PlayerWin,
        };

        private static readonly Scenario DrawnMatch = new Scenario
        {
            PlayerWins = 3, CpuWins = 3, Rounds = 7, LastRound = null, State = BattleMatchState.Draw,
        };

        private static readonly RoundWinner[] Outcomes = { RoundWinner.Player, RoundWinner.Cpu, RoundWinner.Draw };

        /// <summary>
        /// (p, c) で <paramref name="done"/> ラウンドを終えた状態から、途中で決着せずに狙いへ届くか。
        /// 決着の判定は既存の <see cref="BattleSession.EvaluateMatchState"/> をそのまま使います。
        /// </summary>
        private static bool Reachable(Scenario s, int p, int c, int done, RoundWinner? last)
        {
            BattleMatchState state = BattleSession.EvaluateMatchState(p, c, done, BattleSession.MaxRounds);

            if (done == s.Rounds)
            {
                return p == s.PlayerWins && c == s.CpuWins && state == s.State &&
                       (!s.LastRound.HasValue || last == s.LastRound.Value);
            }

            if (state != BattleMatchState.InProgress || done > s.Rounds)
            {
                return false;
            }

            foreach (RoundWinner o in Outcomes)
            {
                if (Reachable(s, p + (o == RoundWinner.Player ? 1 : 0), c + (o == RoundWinner.Cpu ? 1 : 0), done + 1, o))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>このラウンドで狙いへ届く勝敗の候補。</summary>
        private static List<RoundWinner> Wanted(Scenario s, BattleSession session)
        {
            List<RoundWinner> wanted = new List<RoundWinner>();

            foreach (RoundWinner o in Outcomes)
            {
                int p = session.PlayerWins + (o == RoundWinner.Player ? 1 : 0);
                int c = session.CpuWins + (o == RoundWinner.Cpu ? 1 : 0);

                if (Reachable(s, p, c, session.CompletedRounds + 1, o))
                {
                    wanted.Add(o);
                }
            }

            return wanted;
        }

        private bool Achieved(Scenario s, MatchTrace m)
        {
            BattleSession session = controller.Coordinator.Session;

            return !m.OffGoal &&
                   session.PlayerWins == s.PlayerWins &&
                   session.CpuWins == s.CpuWins &&
                   session.CompletedRounds == s.Rounds &&
                   session.State == s.State;
        }

        // ---------------- 進行 ----------------

        private sealed class RoundTrace
        {
            internal int Round;
            internal RoundWinner Winner;
            internal int RevealFrame = -1;
            internal int VictoryFrames;
            internal int VictoryEndFrame = -1;
            internal int ScoreRevealFrame = -1;
            internal bool ScoreTextChangedWithoutPips;
            internal int CoreBreakStartFrame = -1;
            internal int CoreBreakEndFrame = -1;
            internal float CoreBreakSeconds;
            internal RoundWinner CoreBreakSide = RoundWinner.Draw;
            internal Vector2Int PipsAtCoreBreak;
            internal string ScoreTextAtCoreBreak;
            internal int CoreBreakGraphicFrames;
            internal int CoreBreakFramesWithGateOpen;
            internal bool SawCoreBreakText;
            internal int BannerFrame = -1;
            internal int ArchivingFrame = -1;
            internal int FinalVisibleFrame = -1;
            internal bool FinalVisibleWhileCoreBreak;
            internal readonly List<string> RoundLabels = new List<string>();
            internal int MashAttempts;
            internal int MashAccepted;
        }

        private sealed class MatchTrace
        {
            internal readonly List<RoundTrace> Rounds = new List<RoundTrace>();
            internal readonly List<string> Scores = new List<string>();
            internal int CoreBreakStarts;
            internal bool OffGoal;
            internal bool MashDuringClinch;
            internal bool Stopped;

            internal RoundTrace Last => Rounds[Rounds.Count - 1];

            internal string Describe()
            {
                RoundTrace r = Rounds.Count > 0 ? Last : null;

                return "\n  scores=" + string.Join(" ", Scores) + " coreBreaks=" + CoreBreakStarts +
                       (r == null
                           ? string.Empty
                           : "\n  last round " + r.Round + " (" + r.Winner + "): reveal=" + r.RevealFrame +
                             " victoryEnd=" + r.VictoryEndFrame + " score=" + r.ScoreRevealFrame +
                             " coreBreak=" + r.CoreBreakStartFrame + ".." + r.CoreBreakEndFrame + " side=" + r.CoreBreakSide +
                             " banner=" + r.BannerFrame + " archiving=" + r.ArchivingFrame + " final=" + r.FinalVisibleFrame);
            }
        }

        private IEnumerator Drive(Scenario s, System.Action<MatchTrace> use)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                RestartIfFinished();
                yield return null;

                MatchTrace m = new MatchTrace();
                use(m);

                yield return PlayMatch(s, m, null);

                if (Achieved(s, m))
                {
                    TestContext.WriteLine("attempt " + (attempt + 1) + ":" + m.Describe());
                    use(m);
                    yield break;
                }

                TestContext.WriteLine("attempt " + (attempt + 1) + " missed:" + m.Describe());
            }

            Assert.Fail(MaxAttempts + " 試合で狙った展開になりませんでした。");
        }

        private void RestartIfFinished()
        {
            if (controller.Coordinator.State == BattleUiState.MatchFinished)
            {
                controller.OnRematchClicked();
            }
        }

        private IEnumerator PlayMatch(Scenario s, MatchTrace m, System.Func<bool> stop)
        {
            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                BattleSession session = controller.Coordinator.Session;
                List<RoundWinner> wanted = Wanted(s, session);
                string pick = Pick(s, session, wanted, out bool onGoal);

                m.OffGoal |= !onGoal;

                RoundTrace r = new RoundTrace { Round = session.CurrentRound };
                m.Rounds.Add(r);

                Vector2Int lastPips = new Vector2Int(pips.PlayerWins, pips.CpuWins);
                string lastScore = ScoreText();
                bool lastVictory = false;
                MatchCueKind lastCue = MatchCueKind.None;
                int frame = 0;

                controller.OnWheelDeployRequested(pick);

                Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

                float startedAt = Time.realtimeSinceStartup;

                while (true)
                {
                    frame++;

                    if (!r.RoundLabels.Contains(RoundLabel()))
                    {
                        r.RoundLabels.Add(RoundLabel());
                    }

                    if (r.RevealFrame < 0 && controller.Coordinator.State == BattleUiState.ShowingResult)
                    {
                        r.RevealFrame = frame;
                    }

                    if (victory.IsPlaying)
                    {
                        r.VictoryFrames++;
                    }
                    else if (lastVictory && r.VictoryEndFrame < 0)
                    {
                        r.VictoryEndFrame = frame;
                    }

                    lastVictory = victory.IsPlaying;

                    Vector2Int shownPips = new Vector2Int(pips.PlayerWins, pips.CpuWins);
                    string score = ScoreText();

                    if (shownPips != lastPips && r.ScoreRevealFrame < 0)
                    {
                        r.ScoreRevealFrame = frame;
                    }

                    if (score != lastScore && shownPips == lastPips)
                    {
                        r.ScoreTextChangedWithoutPips = true;
                    }

                    lastPips = shownPips;
                    lastScore = score;

                    if (cue.Current == MatchCueKind.CoreBreak)
                    {
                        if (lastCue != MatchCueKind.CoreBreak)
                        {
                            m.CoreBreakStarts++;
                            r.CoreBreakStartFrame = frame;
                            r.PipsAtCoreBreak = shownPips;
                            r.ScoreTextAtCoreBreak = score;
                            r.CoreBreakSide = controller.LastMatchPlan != null ? controller.LastMatchPlan.CoreBreakSide : RoundWinner.Draw;
                        }

                        r.CoreBreakSeconds += Time.unscaledDeltaTime;

                        if (cue.Graphic.IsShowing)
                        {
                            r.CoreBreakGraphicFrames++;
                        }

                        if (!controller.IsRoundInProgress)
                        {
                            r.CoreBreakFramesWithGateOpen++;
                        }

                        if (cue.LabelAlpha > 0f && cue.Label.text == BattleMatchCueView.CoreBreakText)
                        {
                            r.SawCoreBreakText = true;
                        }

                        if (resultView.IsFinalVisible)
                        {
                            r.FinalVisibleWhileCoreBreak = true;
                        }
                    }
                    else if (lastCue == MatchCueKind.CoreBreak && r.CoreBreakEndFrame < 0)
                    {
                        r.CoreBreakEndFrame = frame;
                    }

                    lastCue = cue.Current;

                    if (r.BannerFrame < 0 && resultView.IsBannerVisible)
                    {
                        r.BannerFrame = frame;
                    }

                    if (r.ArchivingFrame < 0 && controller.WheelPhase == BattleWheelPhase.Archiving)
                    {
                        r.ArchivingFrame = frame;
                    }

                    if (r.FinalVisibleFrame < 0 && resultView.IsFinalVisible)
                    {
                        r.FinalVisibleFrame = frame;
                    }

                    // 決着ラウンドの CORE BREAK 以降は、DEPLOY と REMATCH を連打しても通りません。
                    if (m.MashDuringClinch && controller.IsRoundInProgress && r.CoreBreakStartFrame > 0)
                    {
                        int completed = session.CompletedRounds;

                        r.MashAttempts++;

                        if (session.PlayerAvailableUnits.Count > 0)
                        {
                            controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);
                        }

                        controller.OnRematchClicked();

                        if (session.CompletedRounds != completed ||
                            controller.Coordinator.Session != session ||
                            !controller.IsRoundInProgress)
                        {
                            r.MashAccepted++;
                        }
                    }

                    if (stop != null && stop())
                    {
                        m.Stopped = true;
                        yield break;
                    }

                    if (!controller.IsRoundInProgress)
                    {
                        break;
                    }

                    yield return null;

                    Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
                }

                r.Winner = controller.Coordinator.LastResult.Winner;
                m.Scores.Add(session.PlayerWins + "-" + session.CpuWins);
            }
        }

        private IEnumerator FinishCurrentRound()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                BattleSession session = controller.Coordinator.Session;

                controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);

                while (controller.IsRoundInProgress)
                {
                    yield return null;

                    Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit * 8));
                }
            }
        }

        /// <summary>
        /// CPU の非公開の選出を読み、狙った勝敗になる個体を選びます（勝敗は BattleRules が決めます）。
        /// 候補の勝敗を出せる個体が無ければ、残りの先頭を返し、狙いから外れたと記録します。
        /// </summary>
        private static string Pick(Scenario s, BattleSession session, List<RoundWinner> wanted, out bool onGoal)
        {
            FieldInfo field = typeof(BattleSession).GetField("pendingCpuUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null);

            BattleUnit cpu = (BattleUnit)field.GetValue(session);
            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

            // 残り2体なら、CPU の最後の1体も決まっています。最終ラウンドまで読んで、
            // 両方のラウンドが狙いどおりになる出し順を選びます（ATTRIBUTE LINK のチェーンも含めて予想します）。
            if (TryPickForLastTwoRounds(s, session, out string twoRound))
            {
                onGoal = true;
                return twoRound;
            }

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
            return available[0].InstanceId;
        }

        /// <summary>
        /// 残り2ラウンドを両方読み、今回と最終ラウンドの勝敗がどちらも狙いへ届く個体を選びます。
        /// </summary>
        private static bool TryPickForLastTwoRounds(Scenario s, BattleSession session, out string pick)
        {
            pick = null;
            int done = session.CompletedRounds;

            foreach (BattleLinkPrediction.TwoRoundPlan plan in BattleLinkPrediction.PredictLastTwoRounds(session))
            {
                int p = session.PlayerWins + (plan.First == RoundWinner.Player ? 1 : 0);
                int c = session.CpuWins + (plan.First == RoundWinner.Cpu ? 1 : 0);

                if (!Reachable(s, p, c, done + 1, plan.First))
                {
                    continue;
                }

                // 今回で狙いどおりに決着するなら、最終ラウンドは行われません。
                bool decided = BattleSession.EvaluateMatchState(p, c, done + 1, BattleSession.MaxRounds) != BattleMatchState.InProgress;
                int p2 = p + (plan.Second == RoundWinner.Player ? 1 : 0);
                int c2 = c + (plan.Second == RoundWinner.Cpu ? 1 : 0);

                if (decided || Reachable(s, p2, c2, done + 2, plan.Second))
                {
                    pick = plan.Now.InstanceId;
                    return true;
                }
            }

            return false;
        }

        // ---------------- 道具 ----------------

        private static string RoundLabel()
        {
            return Find("RoundLabel").GetComponent<TMP_Text>().text;
        }

        private static string ScoreText()
        {
            return Find("ScoreLabel").GetComponent<TMP_Text>().text;
        }

        private static string FinalTitle()
        {
            return Find("FinalTitleLabel").GetComponent<TMP_Text>().text;
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
