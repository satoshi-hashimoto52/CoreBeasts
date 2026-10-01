using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 編成データの読み込みと組み立て。
    /// 保存済み編成の復元、CPU編成の生成、対戦への受け渡しを確かめます。
    /// </summary>
    public sealed class BattleSquadDataTests
    {
        private TestBattleCards cards;

        [SetUp]
        public void SetUp()
        {
            cards = new TestBattleCards();

            SquadRepositoryProvider.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            cards.Cleanup();

            SquadRepositoryProvider.Reset();
        }

        /// <summary>所持一覧の先頭7体を保存済み編成として書き込みます。</summary>
        private static void SaveSquad(
            ISquadRepository repository,
            string setId,
            CoreBeastRoster roster,
            int count)
        {
            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < count && i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            repository.Save(setId, new SquadSnapshot(ids));
        }

        // ---------------- 1体ぶんの変換 ----------------

        [Test]
        public void Card_CarriesBothCoreValuesAndDisplayValues()
        {
            CoreBeastDefinition definition =
                cards.CreateDefinition("volx", UnitAttribute.Red, 76);

            Assert.That(
                BattleUnitCard.TryCreate("inst_1", 18, definition, out BattleUnitCard card),
                Is.True);

            Assert.That(card.InstanceId, Is.EqualTo("inst_1"));
            Assert.That(card.Level, Is.EqualTo(18));
            Assert.That(card.Definition, Is.SameAs(definition));
            Assert.That(card.Unit.PowerOf(UnitAttribute.Red), Is.EqualTo(76));
            Assert.That(card.Unit.PrimaryAttribute, Is.EqualTo(UnitAttribute.Red));
            Assert.That(card.Unit.HasSecondaryAttribute, Is.False);
        }

        [Test]
        public void Card_KeepsBothAttributesForDualUnits()
        {
            CoreBeastDefinition definition = cards.CreateDefinition(
                "dual", UnitAttribute.Red, 50, true, UnitAttribute.Blue);

            BattleUnitCard.TryCreate("inst_dual", 1, definition, out BattleUnitCard card);

            Assert.That(card.Unit.HasSecondaryAttribute, Is.True);
            Assert.That(card.Unit.HasAttribute(UnitAttribute.Red), Is.True);
            Assert.That(card.Unit.HasAttribute(UnitAttribute.Blue), Is.True);
        }

        [Test]
        public void Card_IsNotCreatedWithoutDefinitionOrId()
        {
            Assert.That(
                BattleUnitCard.TryCreate("inst", 1, null, out BattleUnitCard _),
                Is.False);

            Assert.That(
                BattleUnitCard.TryCreate(
                    string.Empty,
                    1,
                    cards.CreateDefinition("x", UnitAttribute.Red, 1),
                    out BattleUnitCard _),
                Is.False);
        }

        [Test]
        public void Card_CanTakeADifferentInstanceIdForTheCpuSide()
        {
            OwnedCoreBeast owned = cards.CreateOwned("volx_r_01", UnitAttribute.Red, 60);

            BattleUnitCard.TryCreate(owned, "cpu:volx_r_01", out BattleUnitCard card);

            Assert.That(card.InstanceId, Is.EqualTo("cpu:volx_r_01"));
            Assert.That(card.Unit.InstanceId, Is.EqualTo("cpu:volx_r_01"));
            Assert.That(card.Definition, Is.SameAs(owned.Definition));
        }

        // ---------------- 7体ぶんの編成 ----------------

        [Test]
        public void Side_BuildsASquadFromSevenCards()
        {
            BattleSideRoster side = cards.CreateSide();

            Assert.That(side, Is.Not.Null);
            Assert.That(side.Count, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(side.Squad.Count, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(side.Find("card_3"), Is.Not.Null);
            Assert.That(side.Find("missing"), Is.Null);
        }

        [Test]
        public void Side_KeepsCardOrderAlignedWithTheSquad()
        {
            BattleSideRoster side = cards.CreateSide();

            for (int i = 0; i < side.Count; i++)
            {
                Assert.That(
                    side.Cards[i].InstanceId,
                    Is.EqualTo(side.Squad.Units[i].InstanceId));
            }
        }

        [Test]
        public void Side_IsRejectedWhenFewerThanSevenCards()
        {
            Assert.That(
                BattleSideRoster.TryCreate(
                    cards.CreateCards(6),
                    out BattleSideRoster side,
                    out BattleError error),
                Is.False);

            Assert.That(side, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void Side_IsRejectedWhenIdsRepeat()
        {
            List<BattleUnitCard> list = cards.CreateCards(BattleSquad.UnitCount);

            BattleUnitCard.TryCreate(
                list[0].InstanceId,
                1,
                cards.CreateDefinition("dup", UnitAttribute.Blue, 10),
                out BattleUnitCard duplicate);

            list[6] = duplicate;

            Assert.That(
                BattleSideRoster.TryCreate(list, out BattleSideRoster _, out BattleError error),
                Is.False);

            Assert.That(error, Is.EqualTo(BattleError.DuplicateInstanceId));
        }

        // ---------------- プレイヤー編成の読み込み ----------------

        [Test]
        public void PlayerSide_LoadsTheSavedSquadKeepingInstanceIds()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);
            InMemorySquadRepository repository = new InMemorySquadRepository();

            SaveSquad(repository, "1", roster, SquadFormation.SlotCount);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    repository, "1", roster, out BattleSideRoster side, out BattleError error),
                Is.True,
                "保存済みの7体を読み込めます。");

            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(side.Count, Is.EqualTo(SquadFormation.SlotCount));

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                Assert.That(
                    side.Cards[i].InstanceId,
                    Is.EqualTo(roster.Owned[i].InstanceId),
                    "InstanceIdを保ったまま復元します。");

                Assert.That(side.Cards[i].Level, Is.EqualTo(roster.Owned[i].Level));
            }
        }

        [Test]
        public void PlayerSide_IsNotLoadedWhenNothingWasSaved()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    new InMemorySquadRepository(),
                    "1",
                    roster,
                    out BattleSideRoster side,
                    out BattleError error),
                Is.False);

            Assert.That(side, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void PlayerSide_IsNotLoadedWithFewerThanSevenSlotsFilled()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);
            InMemorySquadRepository repository = new InMemorySquadRepository();

            SaveSquad(repository, "1", roster, 6);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    repository, "1", roster, out BattleSideRoster side, out BattleError error),
                Is.False,
                "空き枠が残る編成では対戦を開始しません。");

            Assert.That(side, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void PlayerSide_IsNotLoadedWhenAnIdIsMissingFromTheRoster()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);
            InMemorySquadRepository repository = new InMemorySquadRepository();

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            ids[3] = "not-owned";
            repository.Save("1", new SquadSnapshot(ids));

            Assert.That(
                PlayerSideLoader.TryLoad(
                    repository, "1", roster, out BattleSideRoster _, out BattleError error),
                Is.False);

            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void PlayerSide_IsNotLoadedWithoutRepositoryOrRoster()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    null, "1", roster, out BattleSideRoster _, out BattleError _),
                Is.False);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    new InMemorySquadRepository(),
                    "1",
                    null,
                    out BattleSideRoster _,
                    out BattleError _),
                Is.False);
        }

        [Test]
        public void SharedRepository_LetsBattleReadWhatUnitSetSaved()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            // UnitSet画面と同じ保存先へ書き、Battle画面と同じ読み方で取り出します。
            SaveSquad(
                SquadRepositoryProvider.Shared, "1", roster, SquadFormation.SlotCount);

            Assert.That(
                PlayerSideLoader.TryLoad(
                    SquadRepositoryProvider.Shared,
                    "1",
                    roster,
                    out BattleSideRoster side,
                    out BattleError _),
                Is.True);

            Assert.That(side.Count, Is.EqualTo(SquadFormation.SlotCount));
        }

        [Test]
        public void SharedRepository_CanBeReplacedForTesting()
        {
            InMemorySquadRepository replacement = new InMemorySquadRepository();

            SquadRepositoryProvider.SetShared(replacement);

            Assert.That(SquadRepositoryProvider.Shared, Is.SameAs(replacement));

            SquadRepositoryProvider.Reset();

            Assert.That(SquadRepositoryProvider.Shared, Is.Not.SameAs(replacement));
        }

        // ---------------- CPU編成 ----------------

        [Test]
        public void CpuSide_BuildsSevenDistinctUnits()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            RosterCpuSideBuilder builder =
                new RosterCpuSideBuilder(roster, new StepRandomSource());

            Assert.That(builder.TryBuild(out BattleSideRoster side, out BattleError error), Is.True);
            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(side.Count, Is.EqualTo(BattleSquad.UnitCount));

            HashSet<string> ids = new HashSet<string>();

            for (int i = 0; i < side.Count; i++)
            {
                Assert.That(ids.Add(side.Cards[i].InstanceId), Is.True, "IDは重複しません。");
            }
        }

        [Test]
        public void CpuSide_PrefixesIdsSoTheyNeverCollideWithThePlayer()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            RosterCpuSideBuilder builder =
                new RosterCpuSideBuilder(roster, new StepRandomSource());

            builder.TryBuild(out BattleSideRoster side, out BattleError _);

            for (int i = 0; i < side.Count; i++)
            {
                Assert.That(
                    side.Cards[i].InstanceId,
                    Does.StartWith(RosterCpuSideBuilder.DefaultInstanceIdPrefix));

                Assert.That(
                    roster.Find(side.Cards[i].InstanceId),
                    Is.Null,
                    "CPU側のIDは、そのままでは所持個体と一致しません。");
            }
        }

        [Test]
        public void CpuSide_IsDeterministicForTheSameSeed()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            new RosterCpuSideBuilder(roster, new SystemRandomSource(1234))
                .TryBuild(out BattleSideRoster first, out BattleError _);

            new RosterCpuSideBuilder(roster, new SystemRandomSource(1234))
                .TryBuild(out BattleSideRoster second, out BattleError _);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(
                    second.Cards[i].InstanceId,
                    Is.EqualTo(first.Cards[i].InstanceId),
                    "同じseedなら同じCPU編成になります。");
            }
        }

        [Test]
        public void CpuSide_IsNotBuiltFromTooSmallARoster()
        {
            CoreBeastRoster roster = cards.CreateRoster(6);

            Assert.That(
                new RosterCpuSideBuilder(roster, new StepRandomSource())
                    .TryBuild(out BattleSideRoster side, out BattleError error),
                Is.False);

            Assert.That(side, Is.Null);
            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void CpuSide_IsNotBuiltWithoutARoster()
        {
            Assert.That(
                new RosterCpuSideBuilder(null, new StepRandomSource())
                    .TryBuild(out BattleSideRoster _, out BattleError error),
                Is.False);

            Assert.That(error, Is.EqualTo(BattleError.NullSquad));
        }

        // ---------------- マッチの用意 ----------------

        [Test]
        public void MatchSource_ProvidesBothSquadsAndASelector()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);
            BattleSideRoster playerSide = cards.CreateSide("p_");

            BattleMatchSource source = new BattleMatchSource(
                playerSide,
                new RosterCpuSideBuilder(roster, new StepRandomSource()),
                new StepRandomSource());

            Assert.That(
                source.TryCreateMatch(
                    out BattleSquad player,
                    out BattleSquad cpu,
                    out IBattleUnitSelector selector,
                    out BattleError error),
                Is.True);

            Assert.That(error, Is.EqualTo(BattleError.None));
            Assert.That(player, Is.SameAs(playerSide.Squad));
            Assert.That(cpu, Is.Not.Null);
            Assert.That(selector, Is.Not.Null);
            Assert.That(source.CpuSide, Is.Not.Null);
        }

        [Test]
        public void MatchSource_FailsWithoutAPlayerSquad()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            BattleMatchSource source = new BattleMatchSource(
                null,
                new RosterCpuSideBuilder(roster, new StepRandomSource()),
                new StepRandomSource());

            Assert.That(
                source.TryCreateMatch(
                    out BattleSquad _, out BattleSquad _,
                    out IBattleUnitSelector _, out BattleError error),
                Is.False);

            Assert.That(error, Is.EqualTo(BattleError.InvalidSquadSize));
        }

        [Test]
        public void MatchSource_BuildsAFreshCpuSquadForEveryMatch()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            BattleMatchSource source = new BattleMatchSource(
                cards.CreateSide("p_"),
                new RosterCpuSideBuilder(roster, new SystemRandomSource(7)),
                new SystemRandomSource(7));

            source.TryCreateMatch(
                out BattleSquad _, out BattleSquad _,
                out IBattleUnitSelector _, out BattleError _);

            BattleSideRoster first = source.CpuSide;

            source.TryCreateMatch(
                out BattleSquad _, out BattleSquad _,
                out IBattleUnitSelector _, out BattleError _);

            Assert.That(
                source.CpuSide,
                Is.Not.SameAs(first),
                "REMATCHではCPU編成を作り直します。");
        }

        [Test]
        public void MatchSource_DrivesAFullMatchThroughTheCoordinator()
        {
            CoreBeastRoster roster = cards.CreateRoster(8);

            BattleMatchSource source = new BattleMatchSource(
                cards.CreateSide("p_"),
                new RosterCpuSideBuilder(roster, new SystemRandomSource(99)),
                new SystemRandomSource(99));

            BattleFlowCoordinator coordinator = new BattleFlowCoordinator(source);

            Assert.That(coordinator.Begin(), Is.True);

            int rounds = 0;

            while (coordinator.State == BattleUiState.Selecting && rounds < 10)
            {
                coordinator.SelectPlayerUnit(
                    coordinator.Session.PlayerAvailableUnits[0].InstanceId);

                coordinator.Deploy();
                coordinator.CompleteResolve();
                coordinator.AdvanceToNextRound();

                rounds++;
            }

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(rounds, Is.LessThanOrEqualTo(BattleSession.MaxRounds));
            Assert.That(
                coordinator.MatchState,
                Is.Not.EqualTo(BattleMatchState.InProgress));
        }

        /// <summary>常に先頭を選ぶ乱数源。並びを固定したいテストで使います。</summary>
        private sealed class StepRandomSource : IRandomSource
        {
            public int NextInt(int exclusiveMax)
            {
                return 0;
            }
        }
    }
}
