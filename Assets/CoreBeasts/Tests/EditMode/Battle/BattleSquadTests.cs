using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 7体編成の受理条件と、公開一覧が内部状態を守ることの確認。
    /// </summary>
    public sealed class BattleSquadTests
    {
        [Test]
        public void UnitCount_MatchesSquadFormationSlotCount()
        {
            Assert.That(BattleSquad.UnitCount, Is.EqualTo(SquadFormation.SlotCount));
            Assert.That(BattleSquad.UnitCount, Is.EqualTo(7));
        }

        [Test]
        public void TryCreate_WithSevenValidUnits_Succeeds()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.True);
            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(squad, Is.Not.Null);
            Assert.That(squad.Count, Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void TryCreate_WithSixUnits_IsRejectedAsInvalidSquadSize()
        {
            List<BattleUnit> units =
                TestBattleUnits.CreateUnits("p", UnitAttribute.Red, 6);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void TryCreate_WithEightUnits_IsRejectedAsInvalidSquadSize()
        {
            List<BattleUnit> units =
                TestBattleUnits.CreateUnits("p", UnitAttribute.Red, 8);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void TryCreate_WithNullList_IsRejectedAsNullSquad()
        {
            bool created = BattleSquad.TryCreate(
                null,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.NullSquad));
        }

        [Test]
        public void TryCreate_WithNullUnit_IsRejectedAsNullUnit()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[3] = null;

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.NullUnit));
        }

        [Test]
        public void TryCreate_WithDuplicateInstanceId_IsRejected()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[5] = TestBattleUnits.Single("p0", UnitAttribute.Blue);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.DuplicateInstanceId));
        }

        [Test]
        public void TryCreate_WithEmptyInstanceId_IsRejected()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[0] = TestBattleUnits.Single(string.Empty, UnitAttribute.Red);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.EmptyInstanceId));
        }

        [Test]
        public void TryCreate_WithNullInstanceId_IsRejectedAsEmptyInstanceId()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[2] = TestBattleUnits.Single(null, UnitAttribute.Red);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.EmptyInstanceId));
        }

        [Test]
        public void TryCreate_WithUndefinedAttribute_IsRejected()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[1] = TestBattleUnits.Single("p1", (UnitAttribute)99);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidAttribute));
        }

        [Test]
        public void TryCreate_WithUndefinedSecondaryAttribute_IsRejected()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[4] = TestBattleUnits.Dual("p4", UnitAttribute.Red, (UnitAttribute)(-1));

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidAttribute));
        }

        [Test]
        public void TryCreate_WithNegativePower_IsRejected()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[6] = TestBattleUnits.Single("p6", UnitAttribute.Red, -1);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.False);
            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.NegativePower));
        }

        [Test]
        public void TryCreate_Rejection_DoesNotModifyInputCollection()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[5] = TestBattleUnits.Single("p0", UnitAttribute.Red);

            BattleUnit[] before = units.ToArray();

            BattleSquad.TryCreate(units, out _, out BattleError error);

            Assert.That(error, Is.EqualTo(BattleError.DuplicateInstanceId));
            Assert.That(units.Count, Is.EqualTo(before.Length));

            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(
                    units[i],
                    Is.SameAs(before[i]),
                    "拒否しても入力側のコレクションは変えてはいけません。");
            }
        }

        [Test]
        public void TryCreate_CopiesInput_SoLaterInputChangesDoNotLeakIn()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            BattleUnit original = units[0];

            BattleSquad squad = TestBattleUnits.CreateSquad(units);

            units[0] = TestBattleUnits.Single("intruder", UnitAttribute.Blue);

            Assert.That(squad.Units[0], Is.SameAs(original));
            Assert.That(squad.Contains("intruder"), Is.False);
        }

        [Test]
        public void Units_IsReadOnlyAndCannotBeMutated()
        {
            BattleSquad squad = TestBattleUnits.CreateSquad("p");

            IList<BattleUnit> asList = (IList<BattleUnit>)squad.Units;

            Assert.Throws<System.NotSupportedException>(
                () => asList.Add(TestBattleUnits.Single("x", UnitAttribute.Red)));

            Assert.Throws<System.NotSupportedException>(
                () => asList[0] = TestBattleUnits.Single("x", UnitAttribute.Red));

            Assert.That(squad.Count, Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void Find_ReturnsUnitForKnownInstanceId()
        {
            BattleSquad squad = TestBattleUnits.CreateSquad("p");

            BattleUnit found = squad.Find("p3");

            Assert.That(found, Is.Not.Null);
            Assert.That(found.InstanceId, Is.EqualTo("p3"));
        }

        [Test]
        public void Find_ReturnsNullForUnknownOrEmptyInstanceId()
        {
            BattleSquad squad = TestBattleUnits.CreateSquad("p");

            Assert.That(squad.Find("missing"), Is.Null);
            Assert.That(squad.Find(string.Empty), Is.Null);
            Assert.That(squad.Find(null), Is.Null);
        }

        [Test]
        public void Contains_ReportsMembershipByInstanceId()
        {
            BattleSquad squad = TestBattleUnits.CreateSquad("p");

            Assert.That(squad.Contains("p0"), Is.True);
            Assert.That(squad.Contains("c0"), Is.False);
        }

        [Test]
        public void TryCreate_AcceptsDualAttributeUnits()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[0] = TestBattleUnits.Dual("p0", UnitAttribute.Red, UnitAttribute.Blue);

            bool created = BattleSquad.TryCreate(
                units,
                out BattleSquad squad,
                out BattleError error);

            Assert.That(created, Is.True);
            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(squad.Units[0].HasSecondaryAttribute, Is.True);
            Assert.That(
                squad.Units[0].SecondaryAttribute,
                Is.EqualTo(UnitAttribute.Blue));
        }

        [Test]
        public void TryCreate_AcceptsZeroPower()
        {
            List<BattleUnit> units = TestBattleUnits.CreateUnits("p", UnitAttribute.Red);
            units[1] = TestBattleUnits.Single("p1", UnitAttribute.Red, 0);

            bool created = BattleSquad.TryCreate(units, out _, out BattleError error);

            Assert.That(created, Is.True);
            Assert.That(error, Is.EqualTo(BattleError.None));
        }
    }
}
