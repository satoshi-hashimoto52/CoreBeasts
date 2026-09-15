using System;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 属性相性とPOWER比較による1ラウンド判定の仕様。
    /// 属性は集合として扱い、一次・二次の並び順で結果が変わらないことも確認します。
    /// </summary>
    public sealed class BattleRulesTests
    {
        private static BattleUnit Player(UnitAttribute attribute, int power = 50)
        {
            return TestBattleUnits.Single("player", attribute, power);
        }

        private static BattleUnit Cpu(UnitAttribute attribute, int power = 50)
        {
            return TestBattleUnits.Single("cpu", attribute, power);
        }

        [Test]
        public void Beats_FollowsRedGreenBlueTriangle()
        {
            Assert.That(BattleRules.Beats(UnitAttribute.Red, UnitAttribute.Green), Is.True);
            Assert.That(BattleRules.Beats(UnitAttribute.Green, UnitAttribute.Blue), Is.True);
            Assert.That(BattleRules.Beats(UnitAttribute.Blue, UnitAttribute.Red), Is.True);
        }

        [Test]
        public void Beats_IsFalseInTheReverseDirection()
        {
            Assert.That(BattleRules.Beats(UnitAttribute.Green, UnitAttribute.Red), Is.False);
            Assert.That(BattleRules.Beats(UnitAttribute.Blue, UnitAttribute.Green), Is.False);
            Assert.That(BattleRules.Beats(UnitAttribute.Red, UnitAttribute.Blue), Is.False);
        }

        [Test]
        public void Beats_IsFalseForSameAttribute()
        {
            Assert.That(BattleRules.Beats(UnitAttribute.Red, UnitAttribute.Red), Is.False);
            Assert.That(BattleRules.Beats(UnitAttribute.Green, UnitAttribute.Green), Is.False);
            Assert.That(BattleRules.Beats(UnitAttribute.Blue, UnitAttribute.Blue), Is.False);
        }

        [Test]
        public void SingleAttribute_RedBeatsGreen_WinsByAttributeRegardlessOfPower()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Red, 1),
                Cpu(UnitAttribute.Green, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [Test]
        public void SingleAttribute_GreenBeatsBlue_WinsByAttribute()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Green, 1),
                Cpu(UnitAttribute.Blue, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [Test]
        public void SingleAttribute_BlueBeatsRed_WinsByAttribute()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Blue, 1),
                Cpu(UnitAttribute.Red, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [Test]
        public void SingleAttribute_DisadvantagedSide_LosesByAttribute()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Green, 999),
                Cpu(UnitAttribute.Red, 1));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [TestCase(UnitAttribute.Red)]
        [TestCase(UnitAttribute.Green)]
        [TestCase(UnitAttribute.Blue)]
        public void SameAttribute_HigherPowerWins(UnitAttribute attribute)
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(attribute, 60),
                Cpu(attribute, 59));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [TestCase(UnitAttribute.Red)]
        [TestCase(UnitAttribute.Green)]
        [TestCase(UnitAttribute.Blue)]
        public void SameAttributeAndSamePower_IsRoundDraw(UnitAttribute attribute)
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(attribute, 50),
                Cpu(attribute, 50));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.IsDraw, Is.True);
        }

        [Test]
        public void DualVersusSingle_AttributeThatBeatsWholeOpponent_WinsByAttribute()
        {
            // Red/Blue vs Green : Red は相手の全属性(Green)に勝つ。
            // Green は Blue に勝つが Red には勝てないため、相手側は属性有利にならない。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 1),
                Cpu(UnitAttribute.Green, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [Test]
        public void SingleVersusDual_DualSideWinsByAttribute()
        {
            // 前のテストの表裏。左右を入れ替えても同じ結論になります。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Green, 999),
                TestBattleUnits.Dual("cpu", UnitAttribute.Red, UnitAttribute.Blue, 1));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        [Test]
        public void DualVersusDual_MutualAdvantageRelations_ComparePower()
        {
            // Red/Blue vs Green/Blue : Red は Green に、Green は Blue に有利。
            // どちらも相手の全属性は制圧できないため、相性は付かずPOWER比較になります。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 60),
                TestBattleUnits.Dual("cpu", UnitAttribute.Green, UnitAttribute.Blue, 40));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void DualVersusDual_SameAttributePair_ComparePower()
        {
            // Red/Blue vs Red/Blue : 双方に有利関係があり相殺されるためPOWER比較。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 40),
                TestBattleUnits.Dual("cpu", UnitAttribute.Red, UnitAttribute.Blue, 60));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void DualVersusDual_SameAttributePairAndSamePower_IsRoundDraw()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 50),
                TestBattleUnits.Dual("cpu", UnitAttribute.Red, UnitAttribute.Blue, 50));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void NoAdvantageOnEitherSide_SamePower_IsRoundDraw()
        {
            // Red/Green vs Red : Red も Green も相手(Red)に勝てず、
            // Red も Red/Green の全属性には勝てないため、有利は双方に無い。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Green, 50),
                Cpu(UnitAttribute.Red, 50));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void NoAdvantageOnEitherSide_HigherPowerWins()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Green, 51),
                Cpu(UnitAttribute.Red, 50));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void AttributeOrder_DoesNotChangeResult()
        {
            RoundOutcome redBlue = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 50),
                Cpu(UnitAttribute.Green, 50));

            RoundOutcome blueRed = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Blue, UnitAttribute.Red, 50),
                Cpu(UnitAttribute.Green, 50));

            Assert.That(blueRed.Winner, Is.EqualTo(redBlue.Winner));
            Assert.That(blueRed.Decision, Is.EqualTo(redBlue.Decision));
        }

        [Test]
        public void AttributeOrder_DoesNotChangeResult_OnBothSides()
        {
            RoundOutcome baseline = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 60),
                TestBattleUnits.Dual("cpu", UnitAttribute.Green, UnitAttribute.Blue, 40));

            RoundOutcome swapped = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Blue, UnitAttribute.Red, 60),
                TestBattleUnits.Dual("cpu", UnitAttribute.Blue, UnitAttribute.Green, 40));

            Assert.That(swapped.Winner, Is.EqualTo(baseline.Winner));
            Assert.That(swapped.Decision, Is.EqualTo(baseline.Decision));
        }

        [Test]
        public void DualWithRepeatedAttribute_BehavesLikeSingleAttribute()
        {
            RoundOutcome dual = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Red, 50),
                Cpu(UnitAttribute.Green, 50));

            RoundOutcome single = BattleRules.ResolveRound(
                Player(UnitAttribute.Red, 50),
                Cpu(UnitAttribute.Green, 50));

            Assert.That(dual.Winner, Is.EqualTo(single.Winner));
            Assert.That(dual.Decision, Is.EqualTo(single.Decision));
        }

        [Test]
        public void HasAttributeAdvantage_IsOneSidedForSingleVersusDual()
        {
            BattleUnit redBlue =
                TestBattleUnits.Dual("a", UnitAttribute.Red, UnitAttribute.Blue);

            BattleUnit green = TestBattleUnits.Single("b", UnitAttribute.Green);

            Assert.That(BattleRules.HasAttributeAdvantage(redBlue, green), Is.True);
            Assert.That(BattleRules.HasAttributeAdvantage(green, redBlue), Is.False);
        }

        [Test]
        public void ResolveRound_WithNullUnit_Throws()
        {
            BattleUnit unit = Player(UnitAttribute.Red);

            Assert.Throws<ArgumentNullException>(
                () => BattleRules.ResolveRound(null, unit));

            Assert.Throws<ArgumentNullException>(
                () => BattleRules.ResolveRound(unit, null));
        }

        [Test]
        public void HasAttributeAdvantage_WithNullUnit_Throws()
        {
            BattleUnit unit = Player(UnitAttribute.Red);

            Assert.Throws<ArgumentNullException>(
                () => BattleRules.HasAttributeAdvantage(null, unit));

            Assert.Throws<ArgumentNullException>(
                () => BattleRules.HasAttributeAdvantage(unit, null));
        }
    }
}
