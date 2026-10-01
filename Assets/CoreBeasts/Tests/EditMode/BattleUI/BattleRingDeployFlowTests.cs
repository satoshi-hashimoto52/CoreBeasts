using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// リング・進行役・履歴の受け渡し。Unityへ依存しないため、そのまま実行できます。
    ///
    /// <see cref="BattleScreenController"/>が踏む順序をそのまま並べて、
    /// 「受理されたときだけリングから外れる」「結果公開の後で履歴へ入る」
    /// を確かめます。
    /// </summary>
    public sealed class BattleRingDeployFlowTests
    {
        private BattleFlowCoordinator coordinator;
        private BattleUnitRingModel ring;
        private BattleHistoryModel history;

        private static List<string> Ids(string prefix = "p")
        {
            List<string> ids = new List<string>();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                ids.Add(prefix + i);
            }

            return ids;
        }

        private void Start(UnitAttribute player, UnitAttribute cpu)
        {
            coordinator = new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", player, 50),
                TestBattleSquads.Uniform("c", cpu, 50),
                new FirstAvailableSelector()));

            ring = new BattleUnitRingModel();
            history = new BattleHistoryModel();

            coordinator.Begin();
            ring.Build(Ids());
        }

        [SetUp]
        public void SetUp()
        {
            // PLAYERが必ず負ける組み合わせを既定にします（Green は Red に負けます）。
            Start(UnitAttribute.Green, UnitAttribute.Red);
        }

        /// <summary>
        /// コントローラの<c>OnWheelDeployRequested</c>と同じ順序です。
        /// 選出とDeployの両方が受理されたときだけ、リングから外します。
        /// </summary>
        private bool SlideUp(string instanceId)
        {
            if (!coordinator.SelectPlayerUnit(instanceId))
            {
                return false;
            }

            if (!coordinator.Deploy())
            {
                return false;
            }

            ring.Remove(instanceId);

            return true;
        }

        /// <summary>演出と結果公開を終え、履歴へ片付けるところまで進めます。</summary>
        private void FinishRound()
        {
            coordinator.CompleteResolve();
            coordinator.RevealRoundOutcome();
            coordinator.PublishPendingOutcome();

            RoundResult result = coordinator.LastResult;

            history.Append(
                result.PlayerUnit.InstanceId,
                SquadNumberOf(result.PlayerUnit.InstanceId),
                coordinator.Outcomes.GetOutcome(result.PlayerUnit.InstanceId));

            coordinator.AdvanceToNextRound();
        }

        private static int SquadNumberOf(string instanceId)
        {
            return int.Parse(instanceId.Substring(1)) + 1;
        }

        // ---------------- 受理されたときだけ外れる ----------------

        [Test]
        public void ASuccessfulSlideUpRemovesTheUnitFromTheRing()
        {
            Assert.That(ring.Count, Is.EqualTo(7));

            Assert.That(SlideUp("p0"), Is.True);

            Assert.That(ring.Count, Is.EqualTo(6));
            Assert.That(ring.Contains("p0"), Is.False);
            Assert.That(
                ring.FocusedInstanceId,
                Is.EqualTo("p1"),
                "外したあとは編成順で次の個体が中央です。");
        }

        [Test]
        public void ARejectedSelectionLeavesTheRingUntouched()
        {
            // 編成外のIDは選出が通りません。
            Assert.That(SlideUp("not_in_squad"), Is.False);

            Assert.That(ring.Count, Is.EqualTo(7), "Deploy失敗時に外してはいけません。");
            Assert.That(ring.Contains("p0"), Is.True);
            Assert.That(history.Count, Is.EqualTo(0));
        }

        [Test]
        public void AlreadyUsedUnitsAreRejectedAndNothingChanges()
        {
            SlideUp("p0");
            FinishRound();

            Assert.That(ring.Count, Is.EqualTo(6));

            // 同じ個体をもう一度出そうとしても通りません。
            Assert.That(SlideUp("p0"), Is.False);

            Assert.That(ring.Count, Is.EqualTo(6));
            Assert.That(history.Count, Is.EqualTo(1));
        }

        [Test]
        public void AUsedUnitNeverComesBackToTheRing()
        {
            SlideUp("p0");
            FinishRound();

            SlideUp("p1");
            FinishRound();

            Assert.That(ring.Contains("p0"), Is.False);
            Assert.That(ring.Contains("p1"), Is.False);
            Assert.That(ring.Count, Is.EqualTo(5));

            for (int i = 0; i < ring.Count; i++)
            {
                Assert.That(ring.Slots[i].InstanceId, Is.Not.EqualTo("p0"));
                Assert.That(ring.Slots[i].InstanceId, Is.Not.EqualTo("p1"));
            }
        }

        // ---------------- 履歴へ入るタイミング ----------------

        [Test]
        public void TheUnitIsNotInTheHistoryWhileItIsStillFighting()
        {
            SlideUp("p0");

            // 出撃済みでリングには居ません。戦闘エリアに居るので履歴にもまだ居ません。
            Assert.That(ring.Contains("p0"), Is.False);
            Assert.That(
                history.Contains("p0"),
                Is.False,
                "戦闘中の個体を履歴へ出してはいけません。");

            coordinator.CompleteResolve();
            coordinator.RevealRoundOutcome();

            Assert.That(
                history.Contains("p0"),
                Is.False,
                "結果公開だけではまだ履歴へ入りません。");
        }

        [Test]
        public void TheUnitJoinsTheHistoryOnceTheResultIsPublished()
        {
            SlideUp("p0");
            FinishRound();

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history.Entries[0].InstanceId, Is.EqualTo("p0"));
            Assert.That(history.Entries[0].SquadNumber, Is.EqualTo(1));
        }

        [Test]
        public void TheHistoryKeepsRoundOrderFromTheLeft()
        {
            string[] order = { "p3", "p0", "p5" };

            for (int i = 0; i < order.Length; i++)
            {
                ring.Focus(order[i]);
                SlideUp(order[i]);
                FinishRound();
            }

            Assert.That(history.Count, Is.EqualTo(3));

            for (int i = 0; i < order.Length; i++)
            {
                Assert.That(
                    history.Entries[i].InstanceId,
                    Is.EqualTo(order[i]),
                    "履歴はラウンド順に左から並びます。");
            }
        }

        [Test]
        public void TheHistoryOutcomeMatchesTheRoundResult()
        {
            SlideUp("p0");

            RoundResult result = coordinator.LastResult;

            FinishRound();

            Assert.That(
                history.Entries[0].Outcome,
                Is.EqualTo(BattleSlotOutcomes.FromWinner(result.Winner)),
                "UI側で勝敗を決め直してはいけません。");

            Assert.That(history.Entries[0].Outcome, Is.EqualTo(BattleSlotOutcome.Loss));
        }

        [Test]
        public void OnlyLossesAreMarkedForTheGreyTint()
        {
            // PLAYERが必ず勝つ組み合わせに差し替えます。
            Start(UnitAttribute.Red, UnitAttribute.Green);

            SlideUp("p0");
            FinishRound();

            Assert.That(history.Entries[0].Outcome, Is.EqualTo(BattleSlotOutcome.Win));

            Assert.That(
                BattleDefeatPresentation.GreysTraySlot(history.Entries[0].Outcome),
                Is.False,
                "勝った個体をグレーにしてはいけません。");
        }

        [Test]
        public void TheSquadNumberSurvivesIntoTheHistory()
        {
            ring.Focus("p4");
            SlideUp("p4");
            FinishRound();

            Assert.That(
                history.Entries[0].SquadNumber,
                Is.EqualTo(5),
                "元の編成番号をそのまま出します。");
        }

        [Test]
        public void TheSameInstanceIsNeverArchivedTwice()
        {
            SlideUp("p0");
            FinishRound();

            Assert.That(
                history.Append("p0", 1, BattleSlotOutcome.Win),
                Is.False,
                "1個体は1マッチで1回しか出せません。");

            Assert.That(history.Count, Is.EqualTo(1));
        }

        // ---------------- 最後まで ----------------

        [Test]
        public void TheRingEmptiesAndTheHistoryFillsOverTheWholeMatch()
        {
            int rounds = 0;

            while (coordinator.State == BattleUiState.Selecting && !ring.IsEmpty)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
                rounds++;
            }

            Assert.That(rounds, Is.GreaterThan(0));
            Assert.That(history.Count, Is.EqualTo(rounds));
            Assert.That(ring.Count, Is.EqualTo(BattleSquad.UnitCount - rounds));
            Assert.That(
                history.Count,
                Is.LessThanOrEqualTo(BattleHistoryModel.MaxEntries));
        }

        [Test]
        public void TheSeventhRoundStillHasExactlyOneUnitLeftInTheRing()
        {
            // 同属性どうしは引き分けなので、勝敗が付かず 7 回戦まで進みます。
            Start(UnitAttribute.Red, UnitAttribute.Red);

            for (int round = 1; round <= BattleSquad.UnitCount; round++)
            {
                Assert.That(
                    ring.Count,
                    Is.EqualTo(BattleSquad.UnitCount - (round - 1)),
                    round + "回戦の開始時点でリングの残数が合いません。");

                Assert.That(
                    coordinator.State,
                    Is.EqualTo(BattleUiState.Selecting),
                    round + "回戦を選出できません。");

                if (round == BattleSquad.UnitCount)
                {
                    // 最後の1体は、どの位相でも中央に居つづけます。
                    Assert.That(ring.Count, Is.EqualTo(1));

                    for (float offset = -4f; offset <= 4f; offset += 0.25f)
                    {
                        IReadOnlyList<BattleRingPhaseSlot> visible =
                            BattleRingPhase.Resolve(ring, offset);

                        Assert.That(
                            visible.Count,
                            Is.EqualTo(1),
                            "残り1体を複製してはいけません。");

                        Assert.That(
                            visible[0].Slot.InstanceId,
                            Is.EqualTo(ring.FocusedInstanceId));
                    }
                }

                Assert.That(SlideUp(ring.FocusedInstanceId), Is.True);
                FinishRound();
            }

            Assert.That(ring.IsEmpty, Is.True, "7回戦を終えてもリングが空になりません。");
            Assert.That(history.Count, Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void TheSixUsedUnitsAndTheLastRemainingOneNeverOverlap()
        {
            Start(UnitAttribute.Red, UnitAttribute.Red);

            for (int round = 0; round < BattleSquad.UnitCount - 1; round++)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            Assert.That(history.Count, Is.EqualTo(6));
            Assert.That(ring.Count, Is.EqualTo(1));

            string remaining = ring.FocusedInstanceId;

            HashSet<string> used = new HashSet<string>();

            for (int i = 0; i < history.Count; i++)
            {
                Assert.That(
                    used.Add(history.Entries[i].InstanceId),
                    Is.True,
                    "履歴へ同じ個体が二重に並んでいます。");
            }

            Assert.That(
                used.Contains(remaining),
                Is.False,
                "使用済みレーンと残り1体に同じ個体が出ています。");

            // 使用済みの6体は、どの位相でもリングへ戻ってきません。
            for (float offset = -8f; offset <= 8f; offset += 0.25f)
            {
                IReadOnlyList<BattleRingPhaseSlot> visible =
                    BattleRingPhase.Resolve(ring, offset);

                for (int i = 0; i < visible.Count; i++)
                {
                    Assert.That(
                        used.Contains(visible[i].Slot.InstanceId),
                        Is.False,
                        "使用済みの " + visible[i].Slot.InstanceId +
                        " がリングへ戻っています。");
                }
            }
        }

        // ---------------- 新しい終了条件と画面状態 ----------------

        /// <summary>属性だけで勝敗が決まる編成を差し替えます。</summary>
        private void StartWithPowers(int[] playerPowers, int[] cpuPowers)
        {
            coordinator = new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.WithPowers("p", UnitAttribute.Red, playerPowers),
                TestBattleSquads.WithPowers("c", UnitAttribute.Red, cpuPowers),
                new FirstAvailableSelector()));

            ring = new BattleUnitRingModel();
            history = new BattleHistoryModel();

            coordinator.Begin();
            ring.Build(Ids());
        }

        [Test]
        public void ThreeTwoAndTwoDrawsEndsAsAPlayerWinNotADraw()
        {
            StartWithPowers(
                new[] { 100, 100, 100, 10, 10, 50, 50 },
                new[] { 10, 10, 10, 100, 100, 50, 50 });

            int rounds = 0;

            while (coordinator.State == BattleUiState.Selecting && !ring.IsEmpty)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
                rounds++;
            }

            Assert.That(rounds, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(coordinator.PlayerWins, Is.EqualTo(3));
            Assert.That(coordinator.CpuWins, Is.EqualTo(2));

            Assert.That(
                coordinator.MatchState,
                Is.EqualTo(BattleMatchState.PlayerWin),
                "PLAYER 3 - 2 CPU で DRAW を出してはいけません。");

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
        }

        [Test]
        public void TwoThreeAndTwoDrawsEndsAsACpuWin()
        {
            StartWithPowers(
                new[] { 100, 100, 10, 10, 10, 50, 50 },
                new[] { 10, 10, 100, 100, 100, 50, 50 });

            while (coordinator.State == BattleUiState.Selecting && !ring.IsEmpty)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            Assert.That(coordinator.PlayerWins, Is.EqualTo(2));
            Assert.That(coordinator.CpuWins, Is.EqualTo(3));
            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.CpuWin));
        }

        [Test]
        public void OnlyAnEqualScoreShowsTheDraw()
        {
            // 同属性・同POWERなので7戦すべて引き分けです。
            Start(UnitAttribute.Red, UnitAttribute.Red);

            while (coordinator.State == BattleUiState.Selecting && !ring.IsEmpty)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            Assert.That(coordinator.PlayerWins, Is.EqualTo(coordinator.CpuWins));
            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.Draw));
        }

        [Test]
        public void TheMatchStopsAsSoonAsTheResultCannotBeOverturned()
        {
            // 3勝したあと引き分けが続き、5戦目で残り2 → 逆転不能になります。
            StartWithPowers(
                new[] { 100, 100, 100, 50, 50, 50, 50 },
                new[] { 10, 10, 10, 50, 50, 50, 50 });

            for (int i = 0; i < 4; i++)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            // 3対0・残り3。まだ同点へ追いつけるので続きます。
            Assert.That(coordinator.PlayerWins, Is.EqualTo(3));
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            SlideUp(ring.FocusedInstanceId);
            FinishRound();

            Assert.That(
                coordinator.State,
                Is.EqualTo(BattleUiState.MatchFinished),
                "逆転不能になったら次ラウンドへ進みません。");

            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.PlayerWin));
            Assert.That(coordinator.CurrentRound, Is.EqualTo(6), "6戦目は始まりません。");
        }

        [Test]
        public void AnEarlyFinishLeavesTheUnusedNodesEmptyAndTheUnitsUnused()
        {
            // プレイヤーが4連勝して即決着します。
            Start(UnitAttribute.Red, UnitAttribute.Green);

            for (int i = 0; i < BattleSession.WinsRequired; i++)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.PlayerWin));

            // 履歴に入るのは実際に戦ったぶんだけ。残りの枠は空のままです。
            Assert.That(history.Count, Is.EqualTo(BattleSession.WinsRequired));

            Assert.That(
                BattleHistoryModel.MaxEntries - history.Count,
                Is.EqualTo(3),
                "未使用の履歴枠が3つ残ります。");

            // 出さなかった個体はリングに残り、使用済みにもなりません。
            Assert.That(ring.Count, Is.EqualTo(3));

            for (int i = 0; i < ring.Count; i++)
            {
                string id = ring.Slots[i].InstanceId;

                Assert.That(
                    coordinator.IsPlayerUnitUsed(id),
                    Is.False,
                    id + " は出していないのに使用済みです。");

                Assert.That(
                    history.Contains(id),
                    Is.False,
                    id + " が履歴へ入っています。");

                Assert.That(
                    coordinator.GetPlayerOutcome(id),
                    Is.EqualTo(BattleSlotOutcome.None),
                    id + " に結果が付いています。");
            }
        }

        [Test]
        public void TheFinalResultComesAfterTheLastPendingOutcomeIsPublished()
        {
            Start(UnitAttribute.Red, UnitAttribute.Green);

            for (int i = 0; i < BattleSession.WinsRequired - 1; i++)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            // 決め手になるラウンド。
            string decider = ring.FocusedInstanceId;

            SlideUp(decider);
            coordinator.CompleteResolve();

            // ここではまだ公開前。バッジも履歴も動きません。
            Assert.That(coordinator.HasPendingOutcome, Is.True);
            Assert.That(
                coordinator.GetPlayerOutcome(decider),
                Is.EqualTo(BattleSlotOutcome.None),
                "公開前にバッジを出してはいけません。");

            coordinator.RevealRoundOutcome();

            Assert.That(
                coordinator.GetPlayerOutcome(decider),
                Is.EqualTo(BattleSlotOutcome.None),
                "グレー化の時点ではまだバッジを出しません。");

            Assert.That(
                coordinator.State,
                Is.EqualTo(BattleUiState.ShowingResult),
                "公開前に最終結果へ移ってはいけません。");

            coordinator.PublishPendingOutcome();

            Assert.That(
                coordinator.GetPlayerOutcome(decider),
                Is.EqualTo(BattleSlotOutcome.Win),
                "公開後にバッジが出ます。");

            // 最終結果はここから先です。
            history.Append(
                decider,
                SquadNumberOf(decider),
                coordinator.Outcomes.GetOutcome(decider));

            coordinator.AdvanceToNextRound();

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(coordinator.MatchState, Is.EqualTo(BattleMatchState.PlayerWin));
            Assert.That(history.Count, Is.EqualTo(BattleSession.WinsRequired));
        }

        [Test]
        public void TheOrderIsTheSameWhateverTheEffectsSettingIs()
        {
            // FX の有無は演出の待ち時間だけの違いです。
            // 判定の順序（解決 → 公開 → 次へ）は同じ手順で進みます。
            BattleMatchState[] states = new BattleMatchState[2];
            int[] playerWins = new int[2];
            int[] cpuWins = new int[2];

            for (int pass = 0; pass < 2; pass++)
            {
                StartWithPowers(
                    new[] { 100, 100, 100, 10, 10, 50, 50 },
                    new[] { 10, 10, 10, 100, 100, 50, 50 });

                while (coordinator.State == BattleUiState.Selecting && !ring.IsEmpty)
                {
                    SlideUp(ring.FocusedInstanceId);

                    coordinator.CompleteResolve();
                    coordinator.RevealRoundOutcome();

                    // FX ON 相当では、ここに演出の待ちが挟まるだけです。
                    coordinator.PublishPendingOutcome();

                    RoundResult result = coordinator.LastResult;

                    history.Append(
                        result.PlayerUnit.InstanceId,
                        SquadNumberOf(result.PlayerUnit.InstanceId),
                        coordinator.Outcomes.GetOutcome(result.PlayerUnit.InstanceId));

                    coordinator.AdvanceToNextRound();
                }

                states[pass] = coordinator.MatchState;
                playerWins[pass] = coordinator.PlayerWins;
                cpuWins[pass] = coordinator.CpuWins;
            }

            Assert.That(states[0], Is.EqualTo(states[1]));
            Assert.That(playerWins[0], Is.EqualTo(playerWins[1]));
            Assert.That(cpuWins[0], Is.EqualTo(cpuWins[1]));
            Assert.That(states[0], Is.EqualTo(BattleMatchState.PlayerWin));
        }

        // ---------------- REMATCH ----------------

        [Test]
        public void RematchRestoresTheRingAndWipesTheHistory()
        {
            SlideUp("p0");
            FinishRound();
            SlideUp("p1");
            FinishRound();

            Assert.That(ring.Count, Is.EqualTo(5));
            Assert.That(history.Count, Is.EqualTo(2));

            while (coordinator.State != BattleUiState.MatchFinished)
            {
                SlideUp(ring.FocusedInstanceId);
                FinishRound();
            }

            Assert.That(coordinator.Rematch(), Is.True);

            ring.Restore();
            history.Clear();

            // 勝数とマッチ状態も初期化されます。
            Assert.That(coordinator.PlayerWins, Is.Zero, "REMATCHで勝数が残っています。");
            Assert.That(coordinator.CpuWins, Is.Zero, "REMATCHで勝数が残っています。");
            Assert.That(
                coordinator.MatchState,
                Is.EqualTo(BattleMatchState.InProgress),
                "REMATCHでマッチ状態が戻っていません。");
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    coordinator.IsPlayerUnitUsed("p" + i),
                    Is.False,
                    "REMATCHで p" + i + " が使用済みのままです。");

                Assert.That(
                    coordinator.GetPlayerOutcome("p" + i),
                    Is.EqualTo(BattleSlotOutcome.None),
                    "REMATCHで p" + i + " に結果が残っています。");
            }

            Assert.That(ring.Count, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(ring.FocusedInstanceId, Is.EqualTo("p0"));
            Assert.That(history.Count, Is.EqualTo(0));

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(ring.Slots[i].InstanceId, Is.EqualTo("p" + i));
                Assert.That(ring.Slots[i].SquadNumber, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void RemovingOneInstanceNeverRemovesAnother()
        {
            // 同じDefinitionでもIDが違えば別個体です。IDで引くので取り違えません。
            ring.Focus("p2");
            SlideUp("p2");

            Assert.That(ring.Contains("p2"), Is.False);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                if (i == 2)
                {
                    continue;
                }

                Assert.That(
                    ring.Contains("p" + i),
                    Is.True,
                    "p" + i + " まで巻き添えで外れています。");
            }
        }

        // ---------------- 段階 ----------------

        [Test]
        public void TheWheelIsOnlyInteractiveWhileSelecting()
        {
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            Assert.That(
                BattleWheelPhases.AllowsInput(
                    BattleWheelPhases.Resolve(coordinator.State, false, false), false),
                Is.True);

            SlideUp("p0");

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));

            Assert.That(
                BattleWheelPhases.AllowsInput(
                    BattleWheelPhases.Resolve(coordinator.State, false, false), false),
                Is.False,
                "解決中はリングを触らせません。");

            coordinator.CompleteResolve();

            Assert.That(
                BattleWheelPhases.AllowsInput(
                    BattleWheelPhases.Resolve(coordinator.State, false, false), false),
                Is.False,
                "結果表示中もリングを触らせません。");
        }
    }
}
