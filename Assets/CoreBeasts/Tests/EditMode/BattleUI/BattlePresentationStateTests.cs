using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 表示状態と文言の組み立て。Unityへ依存しないため通常のC#として試せます。
    /// 勝敗の再判定をしていないこと（確定済みの値を写すだけであること）を確かめます。
    /// </summary>
    public sealed class BattlePresentationStateTests
    {
        private readonly FakeBattleText text = new FakeBattleText();

        private static RoundResult Round(
            RoundWinner winner,
            RoundDecision decision,
            int playerPower = 50,
            int cpuPower = 40)
        {
            return new RoundResult(
                1,
                new BattleUnit("p0", UnitAttribute.Red, playerPower),
                new BattleUnit("c0", UnitAttribute.Green, cpuPower),
                winner,
                decision);
        }

        // ---------------- ラウンド結果の文言 ----------------

        [Test]
        public void AttributeDecision_ReadsAsAttributeWin()
        {
            string built = BattleResultText.BuildDecision(
                Round(RoundWinner.Player, RoundDecision.AttributeAdvantage), text);

            Assert.That(built, Is.EqualTo("ATTRIBUTE WIN"));
        }

        [Test]
        public void PowerDecision_ReadsAsPowerWin()
        {
            string built = BattleResultText.BuildDecision(
                Round(RoundWinner.Cpu, RoundDecision.PowerComparison), text);

            Assert.That(built, Is.EqualTo("POWER WIN"));
        }

        [Test]
        public void DrawnRound_ReadsAsDrawEvenThoughDecisionIsPower()
        {
            RoundResult drawn = Round(
                RoundWinner.Draw, RoundDecision.PowerComparison, 50, 50);

            Assert.That(
                BattleResultText.BuildDecision(drawn, text),
                Is.EqualTo("DRAW"),
                "引き分けは決着理由ではなく引き分けとして出します。");
        }

        [Test]
        public void RoundWinner_ReadsPerSide()
        {
            Assert.That(
                BattleResultText.BuildRoundWinner(RoundWinner.Player, text),
                Is.EqualTo("PLAYER WIN"));

            Assert.That(
                BattleResultText.BuildRoundWinner(RoundWinner.Cpu, text),
                Is.EqualTo("CPU WIN"));

            Assert.That(
                BattleResultText.BuildRoundWinner(RoundWinner.Draw, text),
                Is.EqualTo("DRAW"));
        }

        [Test]
        public void MatchResult_ReadsPerOutcome()
        {
            Assert.That(
                BattleResultText.BuildMatchResult(BattleMatchState.PlayerWin, text),
                Is.EqualTo("PLAYER WIN"));

            Assert.That(
                BattleResultText.BuildMatchResult(BattleMatchState.CpuWin, text),
                Is.EqualTo("CPU WIN"));

            Assert.That(
                BattleResultText.BuildMatchResult(BattleMatchState.Draw, text),
                Is.EqualTo("MATCH DRAW"));
        }

        [Test]
        public void MissingInputs_ProduceEmptyTextInsteadOfThrowing()
        {
            Assert.That(BattleResultText.BuildDecision(null, text), Is.Empty);
            Assert.That(
                BattleResultText.BuildDecision(
                    Round(RoundWinner.Player, RoundDecision.PowerComparison), null),
                Is.Empty);
        }

        [Test]
        public void ScoreAndRoundFormatsUseTheSuppliedText()
        {
            Assert.That(text.FormatScore(2, 1), Is.EqualTo("PLAYER 2  -  1 CPU"));
            Assert.That(text.FormatRound(3, 7), Is.EqualTo("ROUND 3 / 7"));
        }

        // ---------------- プレイヤー枠の状態 ----------------

        [Test]
        public void UnusedAndUnselectedSlot_IsAvailable()
        {
            Assert.That(
                BattleSlotStates.Resolve("p1", null, false),
                Is.EqualTo(BattleSlotState.Available));
        }

        [Test]
        public void SelectedSlot_IsSelected()
        {
            Assert.That(
                BattleSlotStates.Resolve("p1", "p1", false),
                Is.EqualTo(BattleSlotState.Selected));
        }

        [Test]
        public void OtherSelectedSlot_StaysAvailable()
        {
            Assert.That(
                BattleSlotStates.Resolve("p1", "p2", false),
                Is.EqualTo(BattleSlotState.Available));
        }

        [Test]
        public void UsedSlot_StaysUsedEvenIfItWasSelected()
        {
            Assert.That(
                BattleSlotStates.Resolve("p1", "p1", true),
                Is.EqualTo(BattleSlotState.Used),
                "使用済みが最優先です。再選択できません。");
        }

        [Test]
        public void EmptySlot_IsNeverSelected()
        {
            Assert.That(
                BattleSlotStates.Resolve(null, null, false),
                Is.EqualTo(BattleSlotState.Available));

            Assert.That(
                BattleSlotStates.Resolve(string.Empty, string.Empty, false),
                Is.EqualTo(BattleSlotState.Available));
        }

        // ---------------- CPU側の非公開枠 ----------------

        [Test]
        public void EnemyMarkers_StartAllUnused()
        {
            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    EnemyMarkerStates.Resolve(i, 0, false),
                    Is.EqualTo(EnemyMarkerState.Unused));
            }
        }

        [Test]
        public void EnemyMarkers_ShowOnePendingWhileSelecting()
        {
            Assert.That(
                EnemyMarkerStates.Resolve(0, 0, true),
                Is.EqualTo(EnemyMarkerState.Pending));

            Assert.That(
                EnemyMarkerStates.Resolve(1, 0, true),
                Is.EqualTo(EnemyMarkerState.Unused));
        }

        [Test]
        public void EnemyMarkers_MarkUsedOnesFromTheStart()
        {
            Assert.That(
                EnemyMarkerStates.Resolve(0, 2, true),
                Is.EqualTo(EnemyMarkerState.Used));

            Assert.That(
                EnemyMarkerStates.Resolve(1, 2, true),
                Is.EqualTo(EnemyMarkerState.Used));

            Assert.That(
                EnemyMarkerStates.Resolve(2, 2, true),
                Is.EqualTo(EnemyMarkerState.Pending));

            Assert.That(
                EnemyMarkerStates.Resolve(3, 2, true),
                Is.EqualTo(EnemyMarkerState.Unused));
        }

        [Test]
        public void EnemyMarkers_AllUsedWhenEverySlotIsSpent()
        {
            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    EnemyMarkerStates.Resolve(i, BattleSquad.UnitCount, false),
                    Is.EqualTo(EnemyMarkerState.Used));
            }
        }

        [Test]
        public void EnemyMarkerInputs_CarryNoUnitIdentity()
        {
            // 引数は「位置」「使用済み数」「選出済みか」だけです。
            // 個体を渡す口が無いため、この計算から相手の編成は漏れません。
            System.Reflection.MethodInfo method =
                typeof(EnemyMarkerStates).GetMethod(nameof(EnemyMarkerStates.Resolve));

            System.Reflection.ParameterInfo[] parameters = method.GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(3));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(bool)));
        }
    }
}
