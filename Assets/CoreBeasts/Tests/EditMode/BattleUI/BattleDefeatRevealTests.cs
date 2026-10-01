using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 敗北表現のデータ経路。Unityのオブジェクトを作らないため、
    /// 進行役と規則だけを純粋なC#として確かめられます。
    ///
    /// 見た目側（立ち絵の減彩・トレイのグレー）は
    /// <see cref="BattleDefeatPresentationTests"/>が受け持ちます。
    /// ここでは「いつ、どの個体が敗者として公開されるか」だけを見ます。
    /// </summary>
    public sealed class BattleDefeatRevealTests
    {
        private static BattleFlowCoordinator CpuWins()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Green, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector()));
        }

        private static BattleFlowCoordinator PlayerWins()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
                new FirstAvailableSelector()));
        }

        private static BattleFlowCoordinator AlwaysDraw()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector()));
        }

        private static string Deploy(BattleFlowCoordinator c, string id = null)
        {
            string chosen = id ?? c.Session.PlayerAvailableUnits[0].InstanceId;

            c.SelectPlayerUnit(chosen);
            c.Deploy();

            return chosen;
        }

        private static string PlayWholeRound(BattleFlowCoordinator c, string id = null)
        {
            string chosen = Deploy(c, id);

            c.CompleteResolve();
            c.RevealRoundOutcome();
            c.PublishPendingOutcome();
            c.AdvanceToNextRound();

            return chosen;
        }

        // ---------------- 公開のタイミング ----------------

        [Test]
        public void NothingIsRevealedBeforeTheRoundIsDeployed()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            Assert.That(c.RevealRoundOutcome(), Is.False);
            Assert.That(c.LastRevealedOutcome, Is.EqualTo(BattleSlotOutcome.None));
            Assert.That(c.LastRevealedPlayerInstanceId, Is.Null);
        }

        [Test]
        public void DeployAloneDoesNotRevealTheDefeat()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string chosen = Deploy(c);

            Assert.That(c.State, Is.EqualTo(BattleUiState.Resolving));

            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.None),
                "DEPLOY直後に敗者が確定表示になっています。");

            Assert.That(c.LastRevealedOutcome, Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void RevealingRecordsTheLossAgainstThePlayerInstance()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string chosen = Deploy(c);

            c.CompleteResolve();

            Assert.That(c.RevealRoundOutcome(), Is.True);

            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Loss));

            Assert.That(c.LastRevealedOutcome, Is.EqualTo(BattleSlotOutcome.Loss));
            Assert.That(c.LastRevealedPlayerInstanceId, Is.EqualTo(chosen));
        }

        [Test]
        public void RevealingTwiceDoesNothingTheSecondTime()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            Deploy(c);
            c.CompleteResolve();

            Assert.That(c.RevealRoundOutcome(), Is.True);
            Assert.That(c.RevealRoundOutcome(), Is.False);
        }

        [Test]
        public void TheBadgeStillWaitsUntilAfterTheReveal()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string chosen = Deploy(c);

            c.CompleteResolve();
            c.RevealRoundOutcome();

            // 敗者のグレー化はもう確定、W/L/Dバッジはまだ出しません。
            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Loss));

            Assert.That(
                c.Outcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.None),
                "バッジの公開時期が早まっています。");

            c.PublishPendingOutcome();

            Assert.That(c.Outcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Loss));
        }

        // ---------------- 勝者・引き分け ----------------

        [Test]
        public void WinningRoundsAreNotRecordedAsALoss()
        {
            BattleFlowCoordinator c = PlayerWins();
            c.Begin();

            string chosen = PlayWholeRound(c);

            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Win));

            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(
                    c.RevealedOutcomes.GetOutcome(chosen)),
                Is.False,
                "勝者の枠をグレーにしてはいけません。");
        }

        [Test]
        public void DrawsGreyNobody()
        {
            BattleFlowCoordinator c = AlwaysDraw();
            c.Begin();

            string chosen = Deploy(c);

            c.CompleteResolve();
            c.RevealRoundOutcome();

            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Draw));

            Assert.That(
                BattleDefeatPresentation.GreysPlayer(c.State, c.LastRevealedOutcome),
                Is.False);

            Assert.That(
                BattleDefeatPresentation.GreysCpu(c.State, c.LastRevealedOutcome),
                Is.False);
        }

        // ---------------- 持ち越しと解除 ----------------

        [Test]
        public void EveryPastDefeatSurvivesIntoLaterRounds()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string first = PlayWholeRound(c);
            string second = PlayWholeRound(c);

            Assert.That(first, Is.Not.EqualTo(second));

            Assert.That(
                c.RevealedOutcomes.GetOutcome(first),
                Is.EqualTo(BattleSlotOutcome.Loss),
                "1ラウンド目の敗北が消えています。");

            Assert.That(
                c.RevealedOutcomes.GetOutcome(second),
                Is.EqualTo(BattleSlotOutcome.Loss));
        }

        [Test]
        public void TheCurrentRoundHighlightClearsButTheLedgerDoesNot()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string first = PlayWholeRound(c);

            // 出場中の2枠は作り直されるため、こちらは空へ戻ります。
            Assert.That(c.LastRevealedOutcome, Is.EqualTo(BattleSlotOutcome.None));
            Assert.That(c.LastRevealedPlayerInstanceId, Is.Null);

            // トレイ側の控えは個体ごとに残ります。
            Assert.That(
                c.RevealedOutcomes.GetOutcome(first),
                Is.EqualTo(BattleSlotOutcome.Loss));
        }

        [Test]
        public void RematchClearsEveryRevealedDefeat()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string first = PlayWholeRound(c);

            while (c.State != BattleUiState.MatchFinished)
            {
                PlayWholeRound(c);
            }

            Assert.That(
                c.RevealedOutcomes.GetOutcome(first),
                Is.EqualTo(BattleSlotOutcome.Loss),
                "前提が崩れています。");

            Assert.That(c.Rematch(), Is.True);

            Assert.That(
                c.RevealedOutcomes.GetOutcome(first),
                Is.EqualTo(BattleSlotOutcome.None),
                "REMATCHで敗北表現が解除されていません。");

            Assert.That(c.LastRevealedOutcome, Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void LeavingMidPresentationStillRevealsTheDefeat()
        {
            BattleFlowCoordinator c = CpuWins();
            c.Begin();

            string chosen = Deploy(c);

            // HOME / OnDisable 相当。演出は中断されます。
            c.AbortPresentation();

            Assert.That(
                c.RevealedOutcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Loss),
                "途中離脱で「出したのに敗北が分からない個体」が残っています。");

            Assert.That(
                c.Outcomes.GetOutcome(chosen),
                Is.EqualTo(BattleSlotOutcome.Loss));
        }

        // ---------------- 規則 ----------------

        [Test]
        public void TheRuleGreysExactlyOneSidePerDecidedRound()
        {
            Assert.That(
                BattleDefeatPresentation.GreysPlayer(
                    BattleUiState.ShowingResult, BattleSlotOutcome.Loss),
                Is.True);

            Assert.That(
                BattleDefeatPresentation.GreysCpu(
                    BattleUiState.ShowingResult, BattleSlotOutcome.Loss),
                Is.False);

            Assert.That(
                BattleDefeatPresentation.GreysCpu(
                    BattleUiState.ShowingResult, BattleSlotOutcome.Win),
                Is.True);

            Assert.That(
                BattleDefeatPresentation.GreysPlayer(
                    BattleUiState.ShowingResult, BattleSlotOutcome.Win),
                Is.False);
        }

        [Test]
        public void TheRuleKeepsGreyingAfterTheMatchIsDecided()
        {
            Assert.That(
                BattleDefeatPresentation.ShowsResolvedRound(
                    BattleUiState.MatchFinished),
                Is.True,
                "決着後も、最後のラウンドの敗者は出したままにします。");
        }

        [Test]
        public void OnlyLossGreysATraySlot()
        {
            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(BattleSlotOutcome.Loss),
                Is.True);

            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(BattleSlotOutcome.Win),
                Is.False);

            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(BattleSlotOutcome.Draw),
                Is.False);

            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(BattleSlotOutcome.None),
                Is.False);
        }
    }
}
