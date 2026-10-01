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

            // POWERもCOREも差が付かなかったので Draw です。
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.Draw));
            Assert.That(outcome.IsDraw, Is.True);
        }

        [Test]
        public void DualVersusSingle_WinsWithAnyFavourableColour()
        {
            // Red/Blue vs Green : Red が Green に勝ちます。
            // Blue が Green に負けていても相殺しません。
            // 2色は相手へ有利な色を選んで戦えるものとして扱います。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 1, 1),
                Cpu(UnitAttribute.Green, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(UnitAttribute.Red));
        }

        [Test]
        public void SingleVersusDual_WinsForTheDualSideFromEitherSide()
        {
            // 前のテストの表裏。左右を入れ替えても 2色側が勝ちます。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Player(UnitAttribute.Green, 999),
                TestBattleUnits.Dual("cpu", UnitAttribute.Red, UnitAttribute.Blue, 1, 1));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(UnitAttribute.Red));
        }

        [Test]
        public void DualVersusDual_DifferentPairs_AreDecidedBySurplusColours()
        {
            // Red/Blue vs Green/Blue : 共通色は Blue。
            // 残った Red VS Green を三すくみで比べ、Red が勝ちます。
            // POWERは使いません（相手のほうが高くても結果は変わりません）。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Blue, 1, 1),
                TestBattleUnits.Dual("cpu", UnitAttribute.Green, UnitAttribute.Blue, 99, 99));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.SharedAttribute, Is.EqualTo(UnitAttribute.Blue));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(UnitAttribute.Red));
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
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.Draw));
        }

        [Test]
        public void NoAdvantageOnEitherSide_SamePower_IsRoundDraw()
        {
            // Red/Green vs Red : 共通色は Red。余剰色 Green は Red に勝てません。
            // ただし単色側の属性勝ちにはせず、共通色 Red のPOWER勝負へ移ります。
            RoundOutcome outcome = BattleRules.ResolveRound(
                TestBattleUnits.Dual("player", UnitAttribute.Red, UnitAttribute.Green, 50, 50),
                Cpu(UnitAttribute.Red, 50));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.Draw));
            Assert.That(outcome.SharedAttribute, Is.EqualTo(UnitAttribute.Red));
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
        public void DualWithRepeatedAttribute_IsRejectedAsInvalidData()
        {
            // 同じ色を2回持つ個体は仕様で禁止です。
            // 黙って単色として扱わず、検証で弾きます。
            BattleUnit repeated = TestBattleUnits.Dual(
                "player", UnitAttribute.Red, UnitAttribute.Red, 50, 50);

            Assert.That(
                repeated.Validate(),
                Is.EqualTo(BattleError.InvalidAttributeLoadout),
                "同じ色の重複を受け入れてはいけません。");
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
