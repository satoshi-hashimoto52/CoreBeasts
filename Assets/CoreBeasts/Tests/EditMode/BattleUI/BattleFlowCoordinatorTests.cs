using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// バトル画面の進行役。Unityへ依存しないため、そのまま通常のC#として試せます。
    ///
    /// 勝敗そのものは中核（BattleRules / BattleSession）のテストが担当します。
    /// ここで確かめるのは「どの入力をいつ受け付けるか」と
    /// 「CPUの選出が解決前に出てこないか」です。
    /// </summary>
    public sealed class BattleFlowCoordinatorTests
    {
        /// <summary>プレイヤーが必ず属性勝ちする組み合わせ。</summary>
        private static BattleFlowCoordinator CreatePlayerWinsAlways(
            out FakeBattleMatchSource source)
        {
            source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
                new FirstAvailableSelector());

            return new BattleFlowCoordinator(source);
        }

        /// <summary>毎ラウンド必ず引き分けになる組み合わせ。</summary>
        private static BattleFlowCoordinator CreateAlwaysDraw(
            out FakeBattleMatchSource source)
        {
            source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector());

            return new BattleFlowCoordinator(source);
        }

        private static string FirstAvailablePlayerId(BattleFlowCoordinator coordinator)
        {
            IReadOnlyList<BattleUnit> available =
                coordinator.Session.PlayerAvailableUnits;

            return available[0].InstanceId;
        }

        /// <summary>1ラウンドを最後まで進めます。</summary>
        private static void PlayRound(BattleFlowCoordinator coordinator)
        {
            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();
            coordinator.CompleteResolve();
            coordinator.AdvanceToNextRound();
        }

        // ---------------- 開始 ----------------

        [Test]
        public void Begin_WithCompleteSquads_StartsSelecting()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Loading));
            Assert.That(coordinator.Begin(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(coordinator.CurrentRound, Is.EqualTo(1));
            Assert.That(coordinator.PlayerWins, Is.EqualTo(0));
            Assert.That(coordinator.CpuWins, Is.EqualTo(0));
        }

        [Test]
        public void Begin_WhenSquadCannotBeBuilt_EntersSquadRequired()
        {
            BattleFlowCoordinator coordinator =
                CreatePlayerWinsAlways(out FakeBattleMatchSource source);

            source.CanCreate = false;

            Assert.That(coordinator.Begin(), Is.False);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.SquadRequired));
            Assert.That(coordinator.Session, Is.Null);
            Assert.That(coordinator.LastError, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void SquadRequired_RejectsSelectionAndDeploy()
        {
            BattleFlowCoordinator coordinator =
                CreatePlayerWinsAlways(out FakeBattleMatchSource source);

            source.CanCreate = false;
            coordinator.Begin();

            Assert.That(coordinator.SelectPlayerUnit("p0"), Is.False);
            Assert.That(coordinator.CanDeploy, Is.False);
            Assert.That(coordinator.Deploy(), Is.False);
        }

        // ---------------- CPUの非公開選出 ----------------

        [Test]
        public void Begin_MakesCpuSelectBeforePlayerChooses()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(
                coordinator.Session.HasCpuSelected,
                Is.True,
                "CPUはラウンド開始時に選出を済ませます。");

            Assert.That(
                coordinator.Session.HasPlayerSelected,
                Is.False,
                "プレイヤーの選出はまだセッションへ渡っていません。");
        }

        [Test]
        public void CpuSelection_IsNotExposedBeforeResolve()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(
                coordinator.LastResult,
                Is.Null,
                "解決前に公開できる結果はありません。");

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));

            Assert.That(
                coordinator.LastResult,
                Is.Null,
                "DEPLOY前も結果は出てきません。");

            Assert.That(
                coordinator.Session.History,
                Is.Empty,
                "解決前の履歴は空です。");
        }

        [Test]
        public void PublicApi_HasNoWayToReadPendingCpuUnit()
        {
            // CPUの選出中の個体を返すpublicメンバーが増えていないことを、型として確かめます。
            AssertNoPendingCpuAccessor(typeof(BattleSession));
            AssertNoPendingCpuAccessor(typeof(BattleFlowCoordinator));
        }

        private static void AssertNoPendingCpuAccessor(System.Type type)
        {
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];

                bool leaks = property.PropertyType == typeof(BattleUnit)
                    && property.Name.IndexOf(
                        "Cpu", System.StringComparison.OrdinalIgnoreCase) >= 0;

                Assert.That(
                    leaks,
                    Is.False,
                    type.Name + "." + property.Name +
                    " はCPUの選出を解決前に公開してしまいます。");
            }
        }

        [Test]
        public void CpuSelector_NeverSeesPlayerSelection()
        {
            FirstAvailableSelector selector = new FirstAvailableSelector();

            FakeBattleMatchSource source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
                selector);

            BattleFlowCoordinator coordinator = new BattleFlowCoordinator(source);
            coordinator.Begin();

            // 選択器が呼ばれるのはプレイヤーが選ぶ前です。
            Assert.That(selector.Chosen.Count, Is.EqualTo(1));

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));

            Assert.That(
                selector.Chosen.Count,
                Is.EqualTo(1),
                "プレイヤーの選択でCPUが選び直すことはありません。");

            Assert.That(
                selector.ObservedCandidateCounts[0],
                Is.EqualTo(BattleSquad.UnitCount),
                "選択器へ渡るのは自分の未使用候補だけです。");
        }

        // ---------------- プレイヤーの選択 ----------------

        [Test]
        public void SelectPlayerUnit_StoresSelection()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            string id = FirstAvailablePlayerId(coordinator);

            Assert.That(coordinator.SelectPlayerUnit(id), Is.True);
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.EqualTo(id));
            Assert.That(coordinator.CanDeploy, Is.True);
        }

        [Test]
        public void SelectPlayerUnit_CanBeChangedBeforeDeploy()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            string first = coordinator.Session.PlayerSquad.Units[0].InstanceId;
            string second = coordinator.Session.PlayerSquad.Units[3].InstanceId;

            coordinator.SelectPlayerUnit(first);
            Assert.That(coordinator.SelectPlayerUnit(second), Is.True);
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.EqualTo(second));
        }

        [Test]
        public void SelectPlayerUnit_SameUnitTwice_ReportsNoChange()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            string id = FirstAvailablePlayerId(coordinator);

            Assert.That(coordinator.SelectPlayerUnit(id), Is.True);
            Assert.That(coordinator.SelectPlayerUnit(id), Is.False);
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.EqualTo(id));
        }

        [Test]
        public void SelectPlayerUnit_RejectsUnknownId()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(coordinator.SelectPlayerUnit("not-in-squad"), Is.False);
            Assert.That(coordinator.LastError, Is.EqualTo(BattleError.UnitNotInSquad));
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.Null);
        }

        [Test]
        public void SelectPlayerUnit_RejectsUsedUnit()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            string used = FirstAvailablePlayerId(coordinator);
            PlayRound(coordinator);

            Assert.That(coordinator.IsPlayerUnitUsed(used), Is.True);
            Assert.That(coordinator.SelectPlayerUnit(used), Is.False);
            Assert.That(coordinator.LastError, Is.EqualTo(BattleError.UnitAlreadyUsed));
        }

        [Test]
        public void ClearSelection_RemovesSelection()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));

            Assert.That(coordinator.ClearSelection(), Is.True);
            Assert.That(coordinator.HasSelection, Is.False);
            Assert.That(coordinator.CanDeploy, Is.False);
        }

        // ---------------- DEPLOY ----------------

        [Test]
        public void Deploy_WithoutSelection_IsRejected()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(coordinator.CanDeploy, Is.False);
            Assert.That(coordinator.Deploy(), Is.False);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(coordinator.Session.CompletedRounds, Is.EqualTo(0));
        }

        [Test]
        public void Deploy_ResolvesRoundAndRevealsBothSides()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            string id = FirstAvailablePlayerId(coordinator);
            coordinator.SelectPlayerUnit(id);

            Assert.That(coordinator.Deploy(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));
            Assert.That(coordinator.LastResult, Is.Not.Null);
            Assert.That(coordinator.LastResult.PlayerUnit.InstanceId, Is.EqualTo(id));
            Assert.That(coordinator.LastResult.CpuUnit, Is.Not.Null);
        }

        [Test]
        public void Deploy_RepeatedTaps_ResolveOnlyOneRound()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));

            Assert.That(coordinator.Deploy(), Is.True);
            Assert.That(coordinator.Deploy(), Is.False);
            Assert.That(coordinator.Deploy(), Is.False);

            Assert.That(coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));
        }

        [Test]
        public void Deploy_RaisesRoundResolvedOnce()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            List<RoundResult> raised = new List<RoundResult>();
            coordinator.RoundResolved += raised.Add;

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();
            coordinator.Deploy();

            Assert.That(raised.Count, Is.EqualTo(1));
            Assert.That(raised[0], Is.SameAs(coordinator.LastResult));
        }

        // ---------------- 演出中の入力 ----------------

        [Test]
        public void WhileResolving_AllInputIsRejected()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(coordinator.CanSelect, Is.False);
            Assert.That(coordinator.CanDeploy, Is.False);
            Assert.That(coordinator.CanRematch, Is.False);
            Assert.That(
                coordinator.SelectPlayerUnit(
                    coordinator.Session.PlayerSquad.Units[5].InstanceId),
                Is.False);
        }

        [Test]
        public void WhileShowingResult_AllInputIsRejected()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();
            coordinator.CompleteResolve();

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.ShowingResult));
            Assert.That(coordinator.CanSelect, Is.False);
            Assert.That(coordinator.CanDeploy, Is.False);
        }

        [Test]
        public void CompleteResolve_OnlyWorksWhileResolving()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(coordinator.CompleteResolve(), Is.False);

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(coordinator.CompleteResolve(), Is.True);
            Assert.That(coordinator.CompleteResolve(), Is.False);
        }

        [Test]
        public void AbortPresentation_SettlesStateWithoutLosingTheRound()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(coordinator.AbortPresentation(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(coordinator.CurrentRound, Is.EqualTo(2));
        }

        [Test]
        public void AbortPresentation_DoesNothingWhileSelecting()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(coordinator.AbortPresentation(), Is.False);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
        }

        // ---------------- ラウンド進行 ----------------

        [Test]
        public void AdvanceToNextRound_StartsTheNextRoundAndClearsSelection()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            PlayRound(coordinator);

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.Null);
            Assert.That(coordinator.CurrentRound, Is.EqualTo(2));
            Assert.That(coordinator.Session.HasCpuSelected, Is.True);
        }

        [Test]
        public void AttributeAdvantage_IsReportedAsTheDecision()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(
                coordinator.LastResult.Decision,
                Is.EqualTo(RoundDecision.AttributeAdvantage));

            Assert.That(coordinator.LastResult.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(coordinator.PlayerWins, Is.EqualTo(1));
        }

        [Test]
        public void PowerComparison_IsReportedAsTheDecision()
        {
            FakeBattleMatchSource source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 90),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 10),
                new FirstAvailableSelector());

            BattleFlowCoordinator coordinator = new BattleFlowCoordinator(source);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(
                coordinator.LastResult.Decision,
                Is.EqualTo(RoundDecision.PowerComparison));

            Assert.That(coordinator.LastResult.Winner, Is.EqualTo(RoundWinner.Player));
        }

        [Test]
        public void Draw_AddsNoWinToEitherSide()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
            coordinator.Deploy();

            Assert.That(coordinator.LastResult.IsDraw, Is.True);
            Assert.That(coordinator.PlayerWins, Is.EqualTo(0));
            Assert.That(coordinator.CpuWins, Is.EqualTo(0));
        }

        [Test]
        public void ScoreFollowsResolvedRounds()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            PlayRound(coordinator);
            Assert.That(coordinator.PlayerWins, Is.EqualTo(1));

            PlayRound(coordinator);
            Assert.That(coordinator.PlayerWins, Is.EqualTo(2));
            Assert.That(coordinator.CpuWins, Is.EqualTo(0));
            Assert.That(coordinator.CurrentRound, Is.EqualTo(3));
        }

        // ---------------- 終了 ----------------

        [Test]
        public void FourWins_FinishesTheMatch()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.PlayerWin));
            Assert.That(coordinator.PlayerWins, Is.EqualTo(BattleSession.WinsRequired));
        }

        [Test]
        public void SevenDrawnRounds_FinishTheMatchAsDraw()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            for (int i = 0; i < BattleSession.MaxRounds; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.Draw));
            Assert.That(
                coordinator.Session.CompletedRounds,
                Is.EqualTo(BattleSession.MaxRounds));
        }

        [Test]
        public void AfterTheMatch_NoMoreSelectionIsAccepted()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(
                coordinator.SelectPlayerUnit(
                    coordinator.Session.PlayerSquad.Units[6].InstanceId),
                Is.False);

            Assert.That(coordinator.CanSelect, Is.False);
            Assert.That(coordinator.CanDeploy, Is.False);
            Assert.That(coordinator.Deploy(), Is.False);
            Assert.That(coordinator.AdvanceToNextRound(), Is.False);
        }

        // ---------------- REMATCH ----------------

        [Test]
        public void Rematch_ResetsScoreRoundAndUsedUnits()
        {
            BattleFlowCoordinator coordinator =
                CreatePlayerWinsAlways(out FakeBattleMatchSource source);

            coordinator.Begin();

            string firstUsed = FirstAvailablePlayerId(coordinator);

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(coordinator.Rematch(), Is.True);

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            Assert.That(coordinator.PlayerWins, Is.EqualTo(0));
            Assert.That(coordinator.CpuWins, Is.EqualTo(0));
            Assert.That(coordinator.CurrentRound, Is.EqualTo(1));
            Assert.That(coordinator.Session.CompletedRounds, Is.EqualTo(0));
            Assert.That(coordinator.IsPlayerUnitUsed(firstUsed), Is.False);
            Assert.That(coordinator.SelectedPlayerInstanceId, Is.Null);
            Assert.That(source.CreateCount, Is.EqualTo(2), "編成を作り直します。");
        }

        [Test]
        public void Rematch_IsRejectedWhileTheMatchIsRunning()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWinsAlways(out _);
            coordinator.Begin();

            Assert.That(coordinator.CanRematch, Is.False);
            Assert.That(coordinator.Rematch(), Is.False);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
        }

        [Test]
        public void Rematch_FromSquadRequired_RetriesLoading()
        {
            BattleFlowCoordinator coordinator =
                CreatePlayerWinsAlways(out FakeBattleMatchSource source);

            source.CanCreate = false;
            coordinator.Begin();

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.SquadRequired));

            source.CanCreate = true;

            Assert.That(coordinator.Rematch(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
        }

        // ---------------- 演出設定から独立していること ----------------

        [Test]
        public void MatchOutcomeDoesNotDependOnPresentationPacing()
        {
            // 「演出あり」を、解決と表示の間に追加の呼び出しが入る進行として再現します。
            List<string> withFx = RunScriptedMatch(true);
            List<string> withoutFx = RunScriptedMatch(false);

            Assert.That(
                withFx,
                Is.EqualTo(withoutFx),
                "FXの有無でラウンド結果は変わりません。");
        }

        /// <summary>
        /// 決まった順番で選び切る1マッチを走らせ、各ラウンドの結果を文字列で返します。
        /// </summary>
        private static List<string> RunScriptedMatch(bool simulateFxDelay)
        {
            FakeBattleMatchSource source = new FakeBattleMatchSource(
                TestBattleSquads.Create(
                    "p",
                    new[]
                    {
                        UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue,
                        UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue,
                        UnitAttribute.Red,
                    },
                    new[] { 10, 20, 30, 40, 50, 60, 70 }),
                TestBattleSquads.Create(
                    "c",
                    new[]
                    {
                        UnitAttribute.Green, UnitAttribute.Green, UnitAttribute.Blue,
                        UnitAttribute.Red, UnitAttribute.Red, UnitAttribute.Blue,
                        UnitAttribute.Green,
                    },
                    new[] { 70, 60, 50, 40, 30, 20, 10 }),
                new FirstAvailableSelector());

            BattleFlowCoordinator coordinator = new BattleFlowCoordinator(source);
            coordinator.Begin();

            List<string> log = new List<string>();

            while (coordinator.State == BattleUiState.Selecting)
            {
                coordinator.SelectPlayerUnit(FirstAvailablePlayerId(coordinator));
                coordinator.Deploy();

                if (simulateFxDelay)
                {
                    // 演出中に相当する期間。この間の入力は受け付けられません。
                    coordinator.SelectPlayerUnit("p6");
                    coordinator.Deploy();
                    coordinator.SelectPlayerUnit("p5");
                }

                log.Add(
                    coordinator.LastResult.RoundNumber + ":" +
                    coordinator.LastResult.Winner + ":" +
                    coordinator.LastResult.Decision + ":" +
                    coordinator.PlayerWins + "-" + coordinator.CpuWins);

                coordinator.CompleteResolve();
                coordinator.AdvanceToNextRound();
            }

            log.Add("final:" + coordinator.MatchState);

            return log;
        }
    }
}
