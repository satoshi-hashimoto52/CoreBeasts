using System;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 初期CPUの選出仕様。
    /// 戦略を持たず、注入された乱数源だけで結果が決まることを確認します。
    /// </summary>
    public sealed class RandomUnitSelectorTests
    {
        private static List<BattleUnit> Candidates(int count)
        {
            return TestBattleUnits.CreateUnits("c", UnitAttribute.Red, count);
        }

        [Test]
        public void Constructor_WithNullRandomSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RandomUnitSelector(null));
        }

        [Test]
        public void Select_UsesTheIndexReturnedByTheRandomSource()
        {
            FakeRandomSource random = new FakeRandomSource(3);
            RandomUnitSelector selector = new RandomUnitSelector(random);

            BattleUnit chosen = selector.Select(Candidates(5));

            Assert.That(chosen.InstanceId, Is.EqualTo("c3"));
            Assert.That(random.LastExclusiveMax, Is.EqualTo(5));
        }

        [Test]
        public void Select_WithEmptyCandidates_ReturnsNullWithoutDrawingRandom()
        {
            FakeRandomSource random = new FakeRandomSource(0);
            RandomUnitSelector selector = new RandomUnitSelector(random);

            Assert.That(selector.Select(new List<BattleUnit>()), Is.Null);
            Assert.That(random.CallCount, Is.Zero);
        }

        [Test]
        public void Select_WithNullCandidates_ReturnsNull()
        {
            RandomUnitSelector selector =
                new RandomUnitSelector(new FakeRandomSource(0));

            Assert.That(selector.Select(null), Is.Null);
        }

        [Test]
        public void Select_WithSingleCandidate_ReturnsThatCandidate()
        {
            RandomUnitSelector selector =
                new RandomUnitSelector(new FakeRandomSource(0));

            BattleUnit chosen = selector.Select(Candidates(1));

            Assert.That(chosen.InstanceId, Is.EqualTo("c0"));
        }

        [Test]
        public void Select_WithIndexAtOrAboveCandidateCount_StaysInRange()
        {
            List<BattleUnit> candidates = Candidates(4);

            Assert.That(
                new RandomUnitSelector(new FakeRandomSource(4))
                    .Select(candidates)
                    .InstanceId,
                Is.EqualTo("c3"));

            Assert.That(
                new RandomUnitSelector(new FakeRandomSource(int.MaxValue))
                    .Select(candidates)
                    .InstanceId,
                Is.EqualTo("c3"));
        }

        [Test]
        public void Select_WithNegativeIndex_StaysInRange()
        {
            List<BattleUnit> candidates = Candidates(4);

            Assert.That(
                new RandomUnitSelector(new FakeRandomSource(-1))
                    .Select(candidates)
                    .InstanceId,
                Is.EqualTo("c0"));

            Assert.That(
                new RandomUnitSelector(new FakeRandomSource(int.MinValue))
                    .Select(candidates)
                    .InstanceId,
                Is.EqualTo("c0"));
        }

        [Test]
        public void Select_WithSameSeedAndSameCandidates_IsReproducible()
        {
            List<string> first = DrawSequence(new SystemRandomSource(777));
            List<string> second = DrawSequence(new SystemRandomSource(777));

            Assert.That(second, Is.EqualTo(first));
        }

        private static List<string> DrawSequence(IRandomSource randomSource)
        {
            RandomUnitSelector selector = new RandomUnitSelector(randomSource);
            List<BattleUnit> candidates = Candidates(BattleSquad.UnitCount);
            List<string> picks = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                picks.Add(selector.Select(candidates).InstanceId);
            }

            return picks;
        }

        [Test]
        public void Select_OnlyReturnsUnitsFromTheGivenCandidates()
        {
            List<BattleUnit> candidates = Candidates(BattleSquad.UnitCount);
            RandomUnitSelector selector =
                new RandomUnitSelector(new SystemRandomSource(31));

            for (int i = 0; i < 50; i++)
            {
                Assert.That(candidates, Does.Contain(selector.Select(candidates)));
            }
        }

        [Test]
        public void SystemRandomSource_WithSameSeed_ProducesTheSameSequence()
        {
            SystemRandomSource first = new SystemRandomSource(12345);
            SystemRandomSource second = new SystemRandomSource(12345);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(second.NextInt(7), Is.EqualTo(first.NextInt(7)));
            }
        }

        [Test]
        public void SystemRandomSource_StaysWithinTheRequestedRange()
        {
            SystemRandomSource random = new SystemRandomSource(99);

            for (int i = 0; i < 200; i++)
            {
                int value = random.NextInt(BattleSquad.UnitCount);

                Assert.That(value, Is.InRange(0, BattleSquad.UnitCount - 1));
            }
        }

        [Test]
        public void SystemRandomSource_WithNonPositiveMax_Throws()
        {
            SystemRandomSource random = new SystemRandomSource(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-1));
        }
    }
}
