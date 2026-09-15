using System;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 1マッチの進行仕様。
    /// 同時・非公開選出、使用済み管理、スコア、終了判定、
    /// 不正操作で状態が変わらないことを確認します。
    ///
    /// 属性で結果が揺れないよう、ここでは両軍とも Red の単属性にして
    /// POWERだけで勝敗が決まる編成を使います。
    /// </summary>
    public sealed class BattleSessionTests
    {
        private const int Weak = 10;
        private const int Strong = 100;
        private const int Even = 50;

        private static BattleSquad SquadWithPowers(string prefix, params int[] powers)
        {
            List<BattleUnit> units = new List<BattleUnit>(powers.Length);

            for (int i = 0; i < powers.Length; i++)
            {
                units.Add(TestBattleUnits.Single(prefix + i, UnitAttribute.Red, powers[i]));
            }

            return TestBattleUnits.CreateSquad(units);
        }

        private static BattleSquad UniformSquad(string prefix, int power)
        {
            return TestBattleUnits.CreateSquad(prefix, UnitAttribute.Red, power);
        }

        private static BattleSession CreateSession(
            int playerPower,
            int cpuPower,
            IBattleUnitSelector selector = null)
        {
            return new BattleSession(
                UniformSquad("p", playerPower),
                UniformSquad("c", cpuPower),
                selector ?? RecordingUnitSelector.First());
        }

        /// <summary>プレイヤーの選出・CPUの選出・解決をまとめて進めます。</summary>
        private static RoundResult PlayRound(BattleSession session, string playerUnitId)
        {
            Assert.That(
                session.SelectPlayerUnit(playerUnitId).Success,
                Is.True,
                "プレイヤーの選出が拒否されました: " + playerUnitId);

            Assert.That(session.SelectCpuUnit().Success, Is.True);

            Assert.That(
                session.TryResolveRound(out RoundResult result, out BattleError error),
                Is.True,
                "ラウンドを解決できませんでした: " + error);

            return result;
        }

        [Test]
        public void NewSession_IsInProgressWithNoRoundsAndNoSelections()
        {
            BattleSession session = CreateSession(Even, Even);

            Assert.That(session.State, Is.EqualTo(BattleMatchState.InProgress));
            Assert.That(session.IsFinished, Is.False);
            Assert.That(session.PlayerWins, Is.Zero);
            Assert.That(session.CpuWins, Is.Zero);
            Assert.That(session.CompletedRounds, Is.Zero);
            Assert.That(session.CurrentRound, Is.EqualTo(1));
            Assert.That(session.HasPlayerSelected, Is.False);
            Assert.That(session.HasCpuSelected, Is.False);
            Assert.That(session.History, Is.Empty);
            Assert.That(
                session.PlayerAvailableUnits.Count,
                Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void Constructor_WithNullArguments_Throws()
        {
            BattleSquad squad = UniformSquad("p", Even);
            IBattleUnitSelector selector = RecordingUnitSelector.First();

            Assert.Throws<ArgumentNullException>(
                () => new BattleSession(null, squad, selector));

            Assert.Throws<ArgumentNullException>(
                () => new BattleSession(squad, null, selector));

            Assert.Throws<ArgumentNullException>(
                () => new BattleSession(squad, squad, null));
        }

        [Test]
        public void MatchConstants_AreSevenRoundsAndFourWins()
        {
            Assert.That(BattleSession.MaxRounds, Is.EqualTo(7));
            Assert.That(BattleSession.WinsRequired, Is.EqualTo(4));
        }

        [Test]
        public void RoundStaysUnresolvedUntilBothSidesHaveSelected()
        {
            BattleSession session = CreateSession(Strong, Weak);

            Assert.That(
                session.TryResolveRound(out RoundResult none, out BattleError empty),
                Is.False);

            Assert.That(none, Is.Null);
            Assert.That(empty, Is.EqualTo(BattleError.SelectionIncomplete));

            session.SelectPlayerUnit("p0");

            Assert.That(
                session.TryResolveRound(out RoundResult still, out BattleError onlyPlayer),
                Is.False);

            Assert.That(still, Is.Null);
            Assert.That(onlyPlayer, Is.EqualTo(BattleError.SelectionIncomplete));
            Assert.That(session.CompletedRounds, Is.Zero);

            session.SelectCpuUnit();

            Assert.That(
                session.TryResolveRound(out RoundResult resolved, out BattleError ok),
                Is.True);

            Assert.That(ok, Is.EqualTo(BattleError.None));
            Assert.That(resolved, Is.Not.Null);
            Assert.That(session.CompletedRounds, Is.EqualTo(1));
        }

        [Test]
        public void SelectionOrder_DoesNotMatter_CpuMaySelectFirst()
        {
            BattleSession session = CreateSession(Strong, Weak);

            Assert.That(session.SelectCpuUnit().Success, Is.True);
            Assert.That(session.HasCpuSelected, Is.True);
            Assert.That(session.HasPlayerSelected, Is.False);

            Assert.That(session.SelectPlayerUnit("p0").Success, Is.True);

            Assert.That(
                session.TryResolveRound(out RoundResult result, out _),
                Is.True);

            Assert.That(result.PlayerUnit.InstanceId, Is.EqualTo("p0"));
            Assert.That(result.CpuUnit.InstanceId, Is.EqualTo("c0"));
        }

        [Test]
        public void SelectionState_AdvancesAndClearsAcrossRounds()
        {
            BattleSession session = CreateSession(Strong, Weak);

            session.SelectPlayerUnit("p0");

            Assert.That(session.HasPlayerSelected, Is.True);
            Assert.That(session.SelectedPlayerUnit.InstanceId, Is.EqualTo("p0"));

            session.SelectCpuUnit();
            session.TryResolveRound(out _, out _);

            Assert.That(session.HasPlayerSelected, Is.False);
            Assert.That(session.HasCpuSelected, Is.False);
            Assert.That(session.SelectedPlayerUnit, Is.Null);
            Assert.That(session.CurrentRound, Is.EqualTo(2));
        }

        [Test]
        public void CpuSelectionIsNotExposedBeforeTheRoundResolves()
        {
            BattleSession session = CreateSession(Strong, Weak);

            session.SelectCpuUnit();

            Assert.That(session.HasCpuSelected, Is.True);
            Assert.That(
                session.History,
                Is.Empty,
                "解決前にCPUの選出が履歴へ出てはいけません。");

            Assert.That(
                typeof(BattleSession).GetProperty("SelectedCpuUnit"),
                Is.Null,
                "CPUの選出内容を公開するAPIがあってはいけません。");
        }

        [Test]
        public void CpuSelector_ReceivesOnlyItsOwnUnusedUnits()
        {
            RecordingUnitSelector selector = RecordingUnitSelector.First();

            BattleSession session = new BattleSession(
                UniformSquad("p", Strong),
                UniformSquad("c", Weak),
                selector);

            PlayRound(session, "p0");
            PlayRound(session, "p1");

            Assert.That(selector.ReceivedCandidateIds.Count, Is.EqualTo(2));

            Assert.That(
                selector.ReceivedCandidateIds[0],
                Is.EqualTo(new[] { "c0", "c1", "c2", "c3", "c4", "c5", "c6" }));

            Assert.That(
                selector.ReceivedCandidateIds[1],
                Is.EqualTo(new[] { "c1", "c2", "c3", "c4", "c5", "c6" }),
                "使用済みのCPUユニットは候補から外れます。");

            foreach (string[] candidates in selector.ReceivedCandidateIds)
            {
                foreach (string id in candidates)
                {
                    Assert.That(
                        id.StartsWith("c", StringComparison.Ordinal),
                        Is.True,
                        "CPUへプレイヤー側の情報を渡してはいけません。");
                }
            }
        }

        [Test]
        public void SelectPlayerUnit_WithUnitFromOpponentSquad_IsRejected()
        {
            BattleSession session = CreateSession(Even, Even);

            SelectionResult result = session.SelectPlayerUnit("c0");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.UnitNotInSquad));
            Assert.That(session.HasPlayerSelected, Is.False);
        }

        [Test]
        public void SelectPlayerUnit_WithUnknownInstanceId_IsRejected()
        {
            BattleSession session = CreateSession(Even, Even);

            SelectionResult result = session.SelectPlayerUnit("does-not-exist");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.UnitNotInSquad));
        }

        [Test]
        public void SelectPlayerUnit_WithEmptyInstanceId_IsRejected()
        {
            BattleSession session = CreateSession(Even, Even);

            Assert.That(
                session.SelectPlayerUnit(string.Empty).Error,
                Is.EqualTo(BattleError.EmptyInstanceId));

            Assert.That(
                session.SelectPlayerUnit(null).Error,
                Is.EqualTo(BattleError.EmptyInstanceId));

            Assert.That(session.HasPlayerSelected, Is.False);
        }

        [Test]
        public void SelectPlayerUnit_Twice_IsRejectedAsAlreadySelected()
        {
            BattleSession session = CreateSession(Even, Even);

            Assert.That(session.SelectPlayerUnit("p0").Success, Is.True);

            SelectionResult second = session.SelectPlayerUnit("p1");

            Assert.That(second.Success, Is.False);
            Assert.That(second.Error, Is.EqualTo(BattleError.AlreadySelected));
            Assert.That(
                session.SelectedPlayerUnit.InstanceId,
                Is.EqualTo("p0"),
                "二重選択で選出内容が上書きされてはいけません。");
        }

        [Test]
        public void SelectCpuUnit_Twice_IsRejectedAsAlreadySelected()
        {
            BattleSession session = CreateSession(Even, Even);

            Assert.That(session.SelectCpuUnit().Success, Is.True);

            SelectionResult second = session.SelectCpuUnit();

            Assert.That(second.Success, Is.False);
            Assert.That(second.Error, Is.EqualTo(BattleError.AlreadySelected));
        }

        [Test]
        public void SelectPlayerUnit_WithAlreadyUsedUnit_IsRejected()
        {
            BattleSession session = CreateSession(Strong, Weak);

            PlayRound(session, "p0");

            SelectionResult result = session.SelectPlayerUnit("p0");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.UnitAlreadyUsed));
            Assert.That(session.HasPlayerSelected, Is.False);
        }

        [Test]
        public void RejectedSelection_LeavesSessionStateUntouched()
        {
            BattleSession session = CreateSession(Strong, Weak);

            PlayRound(session, "p0");

            int rounds = session.CompletedRounds;
            int wins = session.PlayerWins;
            int cpuWins = session.CpuWins;
            int available = session.PlayerAvailableUnits.Count;

            Assert.That(session.SelectPlayerUnit("p0").Success, Is.False);
            Assert.That(session.SelectPlayerUnit("c3").Success, Is.False);
            Assert.That(session.SelectPlayerUnit("nope").Success, Is.False);
            Assert.That(session.SelectPlayerUnit(string.Empty).Success, Is.False);

            Assert.That(session.CompletedRounds, Is.EqualTo(rounds));
            Assert.That(session.PlayerWins, Is.EqualTo(wins));
            Assert.That(session.CpuWins, Is.EqualTo(cpuWins));
            Assert.That(session.PlayerAvailableUnits.Count, Is.EqualTo(available));
            Assert.That(session.HasPlayerSelected, Is.False);
            Assert.That(session.State, Is.EqualTo(BattleMatchState.InProgress));
        }

        [Test]
        public void ResolvedRound_MarksBothSidesUnitsAsUsed()
        {
            BattleSession session = CreateSession(Strong, Weak);

            PlayRound(session, "p2");

            Assert.That(session.IsPlayerUnitUsed("p2"), Is.True);
            Assert.That(session.IsPlayerUnitUsed("p3"), Is.False);

            List<string> availableIds = new List<string>();

            foreach (BattleUnit unit in session.PlayerAvailableUnits)
            {
                availableIds.Add(unit.InstanceId);
            }

            Assert.That(availableIds, Does.Not.Contain("p2"));
            Assert.That(availableIds.Count, Is.EqualTo(BattleSquad.UnitCount - 1));
        }

        [Test]
        public void SameCpuUnitIsNeverUsedTwiceInOneMatch()
        {
            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                UniformSquad("c", Even),
                new RandomUnitSelector(new SystemRandomSource(20260915)));

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < BattleSession.MaxRounds; i++)
            {
                RoundResult round = PlayRound(session, "p" + i);

                Assert.That(
                    seen.Add(round.CpuUnit.InstanceId),
                    Is.True,
                    "CPUが同じ個体を再使用しました: " + round.CpuUnit.InstanceId);
            }

            Assert.That(seen.Count, Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void OnlyTheWinningSideGainsOneWin()
        {
            BattleSession session = CreateSession(Strong, Weak);

            RoundResult round = PlayRound(session, "p0");

            Assert.That(round.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(session.PlayerWins, Is.EqualTo(1));
            Assert.That(session.CpuWins, Is.Zero);
        }

        [Test]
        public void CpuRoundWin_AddsOnlyToCpuScore()
        {
            BattleSession session = CreateSession(Weak, Strong);

            RoundResult round = PlayRound(session, "p0");

            Assert.That(round.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(session.CpuWins, Is.EqualTo(1));
            Assert.That(session.PlayerWins, Is.Zero);
        }

        [Test]
        public void RoundDraw_AddsNoWinToEitherSide()
        {
            BattleSession session = CreateSession(Even, Even);

            RoundResult round = PlayRound(session, "p0");

            Assert.That(round.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(round.IsDraw, Is.True);
            Assert.That(session.PlayerWins, Is.Zero);
            Assert.That(session.CpuWins, Is.Zero);
            Assert.That(session.CompletedRounds, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(BattleMatchState.InProgress));
        }

        [Test]
        public void PlayerReachingFourWins_FinishesMatchImmediately()
        {
            BattleSession session = CreateSession(Strong, Weak);

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(session, "p" + i);
            }

            Assert.That(session.State, Is.EqualTo(BattleMatchState.PlayerWin));
            Assert.That(session.IsFinished, Is.True);
            Assert.That(session.PlayerWins, Is.EqualTo(BattleSession.WinsRequired));
            Assert.That(
                session.CompletedRounds,
                Is.EqualTo(BattleSession.WinsRequired),
                "4勝到達時点で打ち切るべきです。");
        }

        [Test]
        public void CpuReachingFourWins_FinishesMatchImmediately()
        {
            BattleSession session = CreateSession(Weak, Strong);

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(session, "p" + i);
            }

            Assert.That(session.State, Is.EqualTo(BattleMatchState.CpuWin));
            Assert.That(session.CpuWins, Is.EqualTo(BattleSession.WinsRequired));
            Assert.That(session.CompletedRounds, Is.EqualTo(BattleSession.WinsRequired));
        }

        [Test]
        public void SevenRoundsOfDraws_EndTheMatchAsDraw()
        {
            BattleSession session = CreateSession(Even, Even);

            for (int i = 0; i < BattleSession.MaxRounds; i++)
            {
                PlayRound(session, "p" + i);
            }

            Assert.That(session.State, Is.EqualTo(BattleMatchState.Draw));
            Assert.That(session.CompletedRounds, Is.EqualTo(BattleSession.MaxRounds));
            Assert.That(session.PlayerWins, Is.Zero);
            Assert.That(session.CpuWins, Is.Zero);
        }

        [Test]
        public void SevenRoundsWithoutFourWins_EndTheMatchAsDraw()
        {
            BattleSession session = new BattleSession(
                SquadWithPowers("p", 60, 60, 60, 10, 10, 10, 50),
                SquadWithPowers("c", 50, 50, 50, 60, 60, 60, 50),
                RecordingUnitSelector.First());

            for (int i = 0; i < BattleSession.MaxRounds; i++)
            {
                PlayRound(session, "p" + i);
            }

            Assert.That(session.PlayerWins, Is.EqualTo(3));
            Assert.That(session.CpuWins, Is.EqualTo(3));
            Assert.That(session.State, Is.EqualTo(BattleMatchState.Draw));
        }

        [Test]
        public void FinishedMatch_RejectsFurtherSelectionAndResolve()
        {
            BattleSession session = CreateSession(Strong, Weak);

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                PlayRound(session, "p" + i);
            }

            Assert.That(
                session.SelectPlayerUnit("p4").Error,
                Is.EqualTo(BattleError.MatchAlreadyFinished));

            Assert.That(
                session.SelectCpuUnit().Error,
                Is.EqualTo(BattleError.MatchAlreadyFinished));

            Assert.That(
                session.TryResolveRound(out RoundResult extra, out BattleError error),
                Is.False);

            Assert.That(extra, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.MatchAlreadyFinished));
            Assert.That(
                session.CompletedRounds,
                Is.EqualTo(BattleSession.WinsRequired));
        }

        [Test]
        public void History_RecordsEveryRoundInOrderWithBothSelections()
        {
            BattleSession session = CreateSession(Strong, Weak);

            PlayRound(session, "p5");
            PlayRound(session, "p1");

            Assert.That(session.History.Count, Is.EqualTo(2));

            Assert.That(session.History[0].RoundNumber, Is.EqualTo(1));
            Assert.That(session.History[0].PlayerUnit.InstanceId, Is.EqualTo("p5"));
            Assert.That(session.History[0].CpuUnit.InstanceId, Is.EqualTo("c0"));
            Assert.That(session.History[0].Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(
                session.History[0].Decision,
                Is.EqualTo(RoundDecision.PowerComparison));

            Assert.That(session.History[1].RoundNumber, Is.EqualTo(2));
            Assert.That(session.History[1].PlayerUnit.InstanceId, Is.EqualTo("p1"));
            Assert.That(session.History[1].CpuUnit.InstanceId, Is.EqualTo("c1"));
        }

        [Test]
        public void History_IsReadOnlyAndCannotBeMutated()
        {
            BattleSession session = CreateSession(Strong, Weak);

            PlayRound(session, "p0");

            IList<RoundResult> asList = (IList<RoundResult>)session.History;

            Assert.Throws<NotSupportedException>(() => asList.Clear());
            Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));

            Assert.That(session.History.Count, Is.EqualTo(1));
            Assert.That(session.CompletedRounds, Is.EqualTo(1));
        }

        [Test]
        public void PlayerAvailableUnits_IsReadOnlyAndDoesNotExposeInternalState()
        {
            BattleSession session = CreateSession(Strong, Weak);

            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;
            IList<BattleUnit> asList = (IList<BattleUnit>)available;

            Assert.Throws<NotSupportedException>(
                () => asList.Add(TestBattleUnits.Single("x", UnitAttribute.Red)));

            PlayRound(session, "p0");

            Assert.That(
                session.PlayerAvailableUnits.Count,
                Is.EqualTo(BattleSquad.UnitCount - 1));
        }

        [Test]
        public void SameSeed_ReproducesTheSameMatchTranscript()
        {
            string[] first = RunSeededMatch(4242);
            string[] second = RunSeededMatch(4242);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SeededMatch_UsesEveryCpuUnitExactlyOnce()
        {
            string[] transcript = RunSeededMatch(4242);

            Assert.That(transcript.Length, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(transcript, Is.Unique);
        }

        [Test]
        public void InjectedRandomIndices_DetermineTheCpuSelectionOfEveryRound()
        {
            // 候補は毎ラウンド1件ずつ減るため、先頭固定の指数でも別の個体が出ます。
            FakeRandomSource random = new FakeRandomSource(0, 0, 0);

            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                UniformSquad("c", Even),
                new RandomUnitSelector(random));

            Assert.That(PlayRound(session, "p0").CpuUnit.InstanceId, Is.EqualTo("c0"));
            Assert.That(PlayRound(session, "p1").CpuUnit.InstanceId, Is.EqualTo("c1"));
            Assert.That(PlayRound(session, "p2").CpuUnit.InstanceId, Is.EqualTo("c2"));

            Assert.That(random.CallCount, Is.EqualTo(3));
            Assert.That(
                random.LastExclusiveMax,
                Is.EqualTo(BattleSquad.UnitCount - 2),
                "未使用候補の件数がそのまま乱数の上限になります。");
        }

        private static string[] RunSeededMatch(int seed)
        {
            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                UniformSquad("c", Even),
                new RandomUnitSelector(new SystemRandomSource(seed)));

            List<string> transcript = new List<string>();

            for (int i = 0; i < BattleSession.MaxRounds; i++)
            {
                RoundResult round = PlayRound(session, "p" + i);
                transcript.Add(round.CpuUnit.InstanceId);
            }

            return transcript.ToArray();
        }

        [Test]
        public void SelectCpuUnit_WhenSelectorReturnsNull_IsRejectedWithoutChangingState()
        {
            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                UniformSquad("c", Even),
                new RecordingUnitSelector(_ => null));

            SelectionResult result = session.SelectCpuUnit();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.InvalidSelectorResult));
            Assert.That(session.HasCpuSelected, Is.False);
        }

        [Test]
        public void SelectCpuUnit_WhenSelectorReturnsForeignUnit_IsRejected()
        {
            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                UniformSquad("c", Even),
                new RecordingUnitSelector(
                    _ => TestBattleUnits.Single("outsider", UnitAttribute.Red)));

            SelectionResult result = session.SelectCpuUnit();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.InvalidSelectorResult));
            Assert.That(session.HasCpuSelected, Is.False);
        }

        [Test]
        public void SelectCpuUnit_WhenSelectorReturnsUsedUnit_IsRejected()
        {
            BattleSquad cpuSquad = UniformSquad("c", Even);
            BattleUnit firstCpuUnit = cpuSquad.Units[0];

            BattleSession session = new BattleSession(
                UniformSquad("p", Even),
                cpuSquad,
                new RecordingUnitSelector(_ => firstCpuUnit));

            PlayRound(session, "p0");

            SelectionResult result = session.SelectCpuUnit();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(BattleError.InvalidSelectorResult));
            Assert.That(session.HasCpuSelected, Is.False);
            Assert.That(session.CompletedRounds, Is.EqualTo(1));
        }

        [Test]
        public void AttributeWinIsRecordedWithItsDecisionReason()
        {
            BattleSquad player = TestBattleUnits.CreateSquad(
                TestBattleUnits.CreateUnits("p", UnitAttribute.Red, BattleSquad.UnitCount, 1));

            BattleSquad cpu = TestBattleUnits.CreateSquad(
                TestBattleUnits.CreateUnits("c", UnitAttribute.Green, BattleSquad.UnitCount, 999));

            BattleSession session = new BattleSession(
                player,
                cpu,
                RecordingUnitSelector.First());

            RoundResult round = PlayRound(session, "p0");

            Assert.That(round.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(round.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(session.PlayerWins, Is.EqualTo(1));
        }
    }
}
