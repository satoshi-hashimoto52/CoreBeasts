using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 所持データ層とバトル中核ロジックの境界。
    /// 変換がここだけで行われ、条件を満たさない入力は編成にならないことを確認します。
    /// </summary>
    public sealed class BattleUnitFactoryTests
    {
        private TestOwnedBeasts beasts;

        [SetUp]
        public void SetUp()
        {
            beasts = new TestOwnedBeasts();
        }

        [TearDown]
        public void TearDown()
        {
            beasts.Cleanup();
            beasts = null;
        }

        [Test]
        public void TryCreateUnit_CopiesInstanceIdAttributeAndPower()
        {
            OwnedCoreBeast owned =
                beasts.Create("inst_1", UnitAttribute.Green, 73);

            Assert.That(
                BattleUnitFactory.TryCreateUnit(owned, out BattleUnit unit),
                Is.True);

            Assert.That(unit.InstanceId, Is.EqualTo("inst_1"));
            Assert.That(unit.PrimaryAttribute, Is.EqualTo(UnitAttribute.Green));
            Assert.That(unit.HasSecondaryAttribute, Is.False);
            Assert.That(unit.Power, Is.EqualTo(73));
        }

        [Test]
        public void TryCreateUnit_KeepsBothAttributesOfADualAttributeBeast()
        {
            OwnedCoreBeast owned = beasts.CreateDual(
                "inst_2",
                UnitAttribute.Red,
                UnitAttribute.Blue,
                55);

            Assert.That(
                BattleUnitFactory.TryCreateUnit(owned, out BattleUnit unit),
                Is.True);

            Assert.That(unit.HasSecondaryAttribute, Is.True);
            Assert.That(unit.HasAttribute(UnitAttribute.Red), Is.True);
            Assert.That(unit.HasAttribute(UnitAttribute.Blue), Is.True);
            Assert.That(unit.HasAttribute(UnitAttribute.Green), Is.False);
        }

        [Test]
        public void TryCreateUnit_WithNullOwned_ReturnsFalse()
        {
            Assert.That(
                BattleUnitFactory.TryCreateUnit(null, out BattleUnit unit),
                Is.False);

            Assert.That(unit, Is.Null);
        }

        [Test]
        public void TryCreateUnit_WithoutDefinition_ReturnsFalse()
        {
            OwnedCoreBeast owned = beasts.CreateWithoutDefinition("inst_3");

            Assert.That(
                BattleUnitFactory.TryCreateUnit(owned, out BattleUnit unit),
                Is.False);

            Assert.That(unit, Is.Null);
        }

        [Test]
        public void TryCreateSquad_FromSevenOwnedBeasts_Succeeds()
        {
            List<OwnedCoreBeast> owned =
                beasts.CreateMany("inst_", BattleSquad.UnitCount);

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    owned,
                    out BattleSquad squad,
                    out BattleError error),
                Is.True);

            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(squad.Count, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(squad.Contains("inst_0"), Is.True);
        }

        [Test]
        public void TryCreateSquad_WithWrongCount_IsRejected()
        {
            List<OwnedCoreBeast> owned = beasts.CreateMany("inst_", 6);

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    owned,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void TryCreateSquad_WithNullList_IsRejected()
        {
            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    (IReadOnlyList<OwnedCoreBeast>)null,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.NullSquad));
        }

        [Test]
        public void TryCreateSquad_WithUnconvertibleBeast_IsRejectedWithoutChangingInput()
        {
            List<OwnedCoreBeast> owned =
                beasts.CreateMany("inst_", BattleSquad.UnitCount);

            owned[2] = beasts.CreateWithoutDefinition("inst_2");

            OwnedCoreBeast[] before = owned.ToArray();

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    owned,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.UnconvertibleUnit));
            Assert.That(owned.Count, Is.EqualTo(before.Length));

            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(owned[i], Is.SameAs(before[i]));
            }
        }

        [Test]
        public void TryCreateSquad_WithDuplicateInstanceId_IsRejected()
        {
            List<OwnedCoreBeast> owned =
                beasts.CreateMany("inst_", BattleSquad.UnitCount);

            owned[4] = beasts.Create("inst_0", UnitAttribute.Red, 50);

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    owned,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.DuplicateInstanceId));
        }

        [Test]
        public void TryCreateSquad_FromFilledFormation_Succeeds()
        {
            SquadFormation formation = new SquadFormation();
            List<OwnedCoreBeast> owned =
                beasts.CreateMany("inst_", SquadFormation.SlotCount);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                formation.Assign(i, owned[i]);
            }

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    formation,
                    out BattleSquad squad,
                    out BattleError error),
                Is.True);

            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(squad.Count, Is.EqualTo(BattleSquad.UnitCount));
        }

        [Test]
        public void TryCreateSquad_FromFormationWithEmptySlot_IsRejected()
        {
            SquadFormation formation = new SquadFormation();
            List<OwnedCoreBeast> owned =
                beasts.CreateMany("inst_", SquadFormation.SlotCount);

            for (int i = 0; i < SquadFormation.SlotCount - 1; i++)
            {
                formation.Assign(i, owned[i]);
            }

            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    formation,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void TryCreateSquad_WithNullFormation_IsRejected()
        {
            Assert.That(
                BattleUnitFactory.TryCreateSquad(
                    (SquadFormation)null,
                    out BattleSquad squad,
                    out BattleError error),
                Is.False);

            Assert.That(squad, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.NullSquad));
        }

        [Test]
        public void ConvertedSquad_CanDriveASession()
        {
            List<OwnedCoreBeast> playerOwned =
                beasts.CreateMany("p_", BattleSquad.UnitCount);

            List<OwnedCoreBeast> cpuOwned =
                beasts.CreateMany("c_", BattleSquad.UnitCount);

            BattleUnitFactory.TryCreateSquad(playerOwned, out BattleSquad player, out _);
            BattleUnitFactory.TryCreateSquad(cpuOwned, out BattleSquad cpu, out _);

            BattleSession session = new BattleSession(
                player,
                cpu,
                new RandomUnitSelector(new SystemRandomSource(5)));

            Assert.That(session.SelectPlayerUnit("p_0").Success, Is.True);
            Assert.That(session.SelectCpuUnit().Success, Is.True);
            Assert.That(
                session.TryResolveRound(out RoundResult round, out BattleError error),
                Is.True);

            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(round.PlayerUnit.InstanceId, Is.EqualTo("p_0"));
            Assert.That(session.CompletedRounds, Is.EqualTo(1));
        }
    }
}
