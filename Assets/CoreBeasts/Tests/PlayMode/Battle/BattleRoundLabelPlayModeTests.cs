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
    /// RoundLabel の表示遅延を、実際の Battle シーンで確かめます。
    ///
    /// 論理上のラウンド番号（<see cref="BattleSession.CurrentRound"/>）は DEPLOY で進みますが、
    /// RoundLabel は、接近・衝突・Impact・勝利コア・CORE BREAK・結果バナー・片付け・次ラウンドの準備が
    /// 終わり、入力を開く直前まで、処理中のラウンド番号を表示し続けます。
    /// 決着後は最後に戦ったラウンドのままで、存在しない次ラウンドの番号を出しません。
    ///
    /// 勝敗は本物の <see cref="BattleRules"/> が決めます。テストは CPU が非公開で選んだ個体を読むだけです。
    /// </summary>
    public sealed class BattleRoundLabelPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        private const int MaxAttempts = 15;

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleFxPlayer fx;
        private BattleResultView resultView;
        private BattleTextCatalog catalog;
        private TMP_Text roundLabel;

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
            resultView = Object.FindAnyObjectByType<BattleResultView>();
            roundLabel = Find("RoundLabel").GetComponent<TMP_Text>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            FieldInfo field = typeof(BattleScreenController).GetField("battleText", BindingFlags.NonPublic | BindingFlags.Instance);
            catalog = (BattleTextCatalog)field.GetValue(controller);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(roundLabel.text, Is.EqualTo(RoundText(1)), "開始時は ROUND 1 です。");
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- DEPLOY と演出中 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RoundLabelDoesNotAdvanceWhenDeployIsPressed()
        {
            RoundTrace t = null;

            yield return PlayRound(RoundWinner.Player, x => t = x);

            Assert.That(t.LogicalAfterDeploy, Is.EqualTo(t.Round + 1), "論理上のラウンド番号は DEPLOY で進みます。" + t.Describe());
            Assert.That(t.LabelAfterDeploy, Is.EqualTo(RoundText(t.Round)), "DEPLOY 直後の RoundLabel は処理中のラウンドです。" + t.Describe());
            Assert.That(t.DisplayedAfterDeploy, Is.EqualTo(t.Round));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RoundLabelStaysOnTheResolvedRoundThroughImpact()
        {
            RoundTrace t = null;

            yield return PlayUntil(RoundWinner.Player, x => t = x);

            AssertHeldThroughPresentation(t, expectVictory: true);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RoundLabelAdvancesOnlyWhenTheNextRoundIsReady()
        {
            RoundTrace t = null;

            yield return PlayUntil(RoundWinner.Cpu, x => t = x);

            AssertHeldThroughPresentation(t, expectVictory: true);
            AssertAdvancedWhenInputOpens(t);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator DrawAlsoKeepsTheCurrentRoundUntilPresentationEnds()
        {
            RoundTrace t = null;

            yield return PlayUntil(RoundWinner.Draw, x => t = x);

            Assert.That(t.ScoreChanged, Is.False, "引き分けではスコア表示が変わりません。" + t.Describe());
            AssertHeldThroughPresentation(t, expectVictory: false);
            AssertAdvancedWhenInputOpens(t);
        }

        // ---------------- 最終ラウンド ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FinalRoundNeverShowsANonexistentNextRound()
        {
            RoundTrace last = null;

            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                yield return PlayRound(RoundWinner.Player, x => last = x);
            }

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));

            int finalRound = controller.Coordinator.Session.CompletedRounds;

            Assert.That(last.Round, Is.EqualTo(finalRound));
            Assert.That(last.MaxLabelRound, Is.EqualTo(finalRound), "最終ラウンドの間、次ラウンドの番号を一瞬も出しません。" + last.Describe());
            Assert.That(last.SawCoreBreakOrFinal, Is.True, "決着の演出まで観測しました。");
            Assert.That(roundLabel.text, Is.EqualTo(RoundText(finalRound)), "決着後も最後に戦ったラウンドのままです。");
            Assert.That(controller.DisplayedRound, Is.EqualTo(finalRound));
            Assert.That(controller.IsRoundDisplayHeld, Is.False);

            // 決着後の画面の作り直しでも変わりません。
            yield return null;

            Assert.That(roundLabel.text, Is.EqualTo(RoundText(finalRound)));
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FxOffKeepsTheRoundUntilResolutionFinishes()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            RoundTrace t = null;

            yield return PlayUntil(RoundWinner.Player, x => t = x);

            Assert.That(t.LogicalAfterDeploy, Is.EqualTo(t.Round + 1));
            Assert.That(t.LabelAfterDeploy, Is.EqualTo(RoundText(t.Round)), "FX OFF でも DEPLOY 直後は処理中のラウンドです。" + t.Describe());
            Assert.That(t.FramesBeforeOpen, Is.GreaterThan(0));
            Assert.That(t.LabelChangedWhileInProgress, Is.False, "FX OFF でも結果処理の途中で進みません。" + t.Describe());
            Assert.That(t.SawBanner, Is.True, "結果表示を通りました。");
            AssertAdvancedWhenInputOpens(t);

            controller.OnFxToggleClicked();
        }

        // ---------------- 中断と再戦 ----------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator InterruptSynchronizesTheDisplayedRound()
        {
            BattleSession session = controller.Coordinator.Session;
            int round = session.CurrentRound;

            controller.OnWheelDeployRequested(Pick(session, RoundWinner.Player, out _));

            float startedAt = Time.realtimeSinceStartup;

            while (fx.Step != BattleFxPlayer.ClashStep.ImpactHold)
            {
                Assert.That(roundLabel.text, Is.EqualTo(RoundText(round)), "衝突の前に進みました。");

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            controller.gameObject.SetActive(false);

            int expected = session.IsFinished ? session.CompletedRounds : session.CurrentRound;

            Assert.That(controller.IsRoundDisplayHeld, Is.False, "中断で留めた表示を解きます。");
            Assert.That(controller.DisplayedRound, Is.EqualTo(expected), "中断直後から論理状態へ同期します。");
            Assert.That(roundLabel.text, Is.EqualTo(RoundText(expected)));
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "中断したラウンドは片付き、次ラウンドの選択待ちです。");

            controller.gameObject.SetActive(true);
            yield return null;

            Assert.That(roundLabel.text, Is.EqualTo(RoundText(expected)), "再有効化しても不整合を残しません。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RematchReturnsTheDisplayedRoundToOne()
        {
            while (controller.Coordinator.State == BattleUiState.Selecting)
            {
                yield return PlayRound(RoundWinner.Player, _ => { });
            }

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(roundLabel.text, Is.Not.EqualTo(RoundText(1)), "決着時点では ROUND 1 ではありません。");

            controller.OnRematchClicked();
            yield return null;

            Assert.That(controller.IsRoundDisplayHeld, Is.False);
            Assert.That(controller.DisplayedRound, Is.EqualTo(1));
            Assert.That(roundLabel.text, Is.EqualTo(RoundText(1)), "再戦で ROUND 1 へ戻ります。");
        }

        // ---------------- 検査 ----------------

        private void AssertHeldThroughPresentation(RoundTrace t, bool expectVictory)
        {
            string d = t.Describe();

            Assert.That(t.LabelAfterDeploy, Is.EqualTo(RoundText(t.Round)), "DEPLOY 直後" + d);
            Assert.That(t.SawClash, Is.True, "接近・衝突を観測しました。" + d);
            Assert.That(t.SawImpact, Is.True, "Impact を観測しました。" + d);
            Assert.That(t.SawBanner, Is.True, "結果バナーを観測しました。" + d);
            Assert.That(t.SawArchiving, Is.True, "片付けを観測しました。" + d);
            Assert.That(t.SawVictory, Is.EqualTo(expectVictory), "勝利コア獲得" + d);

            // 演出・結果表示・片付けのどのフレームでも、処理中のラウンドのままです。
            Assert.That(t.LabelChangedWhileInProgress, Is.False, "演出の途中で RoundLabel が進みました。" + d);
            Assert.That(t.WrongLabelFrames, Is.EqualTo(0), "処理中のラウンド以外を表示したフレームがあります。" + d);
        }

        private void AssertAdvancedWhenInputOpens(RoundTrace t)
        {
            string d = t.Describe();

            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "次ラウンドの選択待ちへ進みました。" + d);
            Assert.That(t.LabelAtOpen, Is.EqualTo(RoundText(t.Round + 1)), "入力が開いた時点で次のラウンドです。" + d);
            Assert.That(t.StateAtOpen, Is.EqualTo(BattleUiState.Selecting), "次ラウンドの準備が終わってから進みます。" + d);
            Assert.That(controller.IsRoundDisplayHeld, Is.False);
            Assert.That(controller.DisplayedRound, Is.EqualTo(controller.Coordinator.CurrentRound));
        }

        // ---------------- 進行 ----------------

        private sealed class RoundTrace
        {
            internal int Round;
            internal RoundWinner Winner;
            internal bool Wanted;
            internal int LogicalAfterDeploy;
            internal int DisplayedAfterDeploy;
            internal string LabelAfterDeploy;
            internal bool LabelChangedWhileInProgress;
            internal int WrongLabelFrames;
            internal int MaxLabelRound;
            internal int FramesBeforeOpen;
            internal bool SawClash;
            internal bool SawImpact;
            internal bool SawVictory;
            internal bool SawBanner;
            internal bool SawArchiving;
            internal bool SawCoreBreakOrFinal;
            internal bool ScoreChanged;
            internal string LabelAtOpen;
            internal BattleUiState StateAtOpen;

            internal string Describe()
            {
                return "\n  round=" + Round + " winner=" + Winner + " logical@deploy=" + LogicalAfterDeploy +
                       " label@deploy='" + LabelAfterDeploy + "' label@open='" + LabelAtOpen + "' state@open=" + StateAtOpen +
                       " maxLabelRound=" + MaxLabelRound + " wrongFrames=" + WrongLabelFrames +
                       " clash=" + SawClash + " impact=" + SawImpact + " victory=" + SawVictory +
                       " banner=" + SawBanner + " archiving=" + SawArchiving;
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

                    yield return PlayRound(wanted, x => round = x);

                    // 次ラウンドへ進んだラウンドだけを使います（決着のラウンドは別のテストで見ます）。
                    if (round.Wanted && round.Winner == wanted &&
                        controller.Coordinator.State == BattleUiState.Selecting)
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

            RoundTrace t = new RoundTrace { Round = session.CurrentRound };

            Assert.That(roundLabel.text, Is.EqualTo(RoundText(t.Round)), "DEPLOY 前の RoundLabel が処理するラウンドではありません。");

            string pick = Pick(session, wanted, out t.Wanted);
            string scoreBefore = Find("ScoreLabel").GetComponent<TMP_Text>().text;

            controller.OnWheelDeployRequested(pick);

            Assert.That(controller.IsRoundInProgress, Is.True, "DEPLOY が通りませんでした。");

            t.LogicalAfterDeploy = session.CurrentRound;
            t.DisplayedAfterDeploy = controller.DisplayedRound;
            t.LabelAfterDeploy = roundLabel.text;
            t.MaxLabelRound = t.Round;

            string held = RoundText(t.Round);
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                t.FramesBeforeOpen++;

                if (roundLabel.text != held)
                {
                    t.LabelChangedWhileInProgress = true;
                    t.WrongLabelFrames++;
                }

                for (int n = t.Round + 1; n <= controller.Coordinator.MaxRounds; n++)
                {
                    if (roundLabel.text == RoundText(n))
                    {
                        t.MaxLabelRound = Mathf.Max(t.MaxLabelRound, n);
                    }
                }

                t.SawClash |= fx.Step == BattleFxPlayer.ClashStep.Approach;
                t.SawImpact |= fx.ImpactBurst.IsShowing || !controller.FxEnabled;
                t.SawVictory |= victory.IsPlaying;
                t.SawBanner |= resultView.IsBannerVisible;
                t.SawArchiving |= controller.WheelPhase == BattleWheelPhase.Archiving;
                t.SawCoreBreakOrFinal |= cue.Current == MatchCueKind.CoreBreak || resultView.IsFinalVisible;

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }

            // 入力が開いたフレーム（ここで初めて進みます）。
            t.LabelAtOpen = roundLabel.text;
            t.StateAtOpen = controller.Coordinator.State;
            t.Winner = controller.Coordinator.LastResult.Winner;
            t.ScoreChanged = Find("ScoreLabel").GetComponent<TMP_Text>().text != scoreBefore;

            for (int n = t.Round + 1; n <= controller.Coordinator.MaxRounds; n++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished && roundLabel.text == RoundText(n))
                {
                    t.MaxLabelRound = Mathf.Max(t.MaxLabelRound, n);
                }
            }

            use(t);
        }

        private string RoundText(int round)
        {
            return catalog.FormatRound(round, controller.Coordinator.MaxRounds);
        }

        private static string Pick(BattleSession session, RoundWinner wanted, out bool onGoal)
        {
            FieldInfo field = typeof(BattleSession).GetField("pendingCpuUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null);

            BattleUnit cpu = (BattleUnit)field.GetValue(session);
            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

            foreach (BattleUnit unit in available)
            {
                if (BattleLinkPrediction.Resolve(session, unit, cpu).Winner == wanted)
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
