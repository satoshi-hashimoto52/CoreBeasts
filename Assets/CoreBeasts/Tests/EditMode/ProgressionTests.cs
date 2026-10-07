using CoreBeasts.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class ProgressionTests
    {
        private const string ProfileKey = "CoreBeasts.Tests.Profile.Phase6";
        private const string SquadKey = "CoreBeasts.Tests.Squad.Phase6";

        private sealed class FixedRandom : IGachaRandomSource
        {
            private readonly int value;

            internal FixedRandom(int selected)
            {
                value = selected;
            }

            public int Next(int maxExclusive)
            {
                return value < maxExclusive ? value : maxExclusive - 1;
            }
        }

        private CoreBeastRoster roster;

        [SetUp]
        public void SetUp()
        {
            roster = TestRosterFactory.Create(8);
            PlayerProfileProvider.SetRepository(new InMemoryPlayerProfileRepository());
            GameFlowState.Reset();
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.DeleteKey(SquadKey);
        }

        [TearDown]
        public void TearDown()
        {
            TestRosterFactory.Destroy(roster);
            PlayerProfileProvider.Reset();
            GameFlowState.Reset();
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.DeleteKey(SquadKey);
        }

        [Test]
        public void NewProfileStartsWithOneGachaPullAndSevenBeasts()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);

            Assert.That(profile.Coins, Is.EqualTo(100));
            Assert.That(profile.Beasts.Count, Is.EqualTo(7));
            Assert.That(profile.Owns("inst_0"), Is.True);
            Assert.That(profile.Owns("inst_6"), Is.True);
            Assert.That(profile.Owns("inst_7"), Is.False);
        }

        [TestCase(BattleRewardOutcome.Win, 30)]
        [TestCase(BattleRewardOutcome.Draw, 20)]
        [TestCase(BattleRewardOutcome.Loss, 10)]
        public void BattleRewardUsesAgreedEconomy(
            BattleRewardOutcome outcome,
            int expected)
        {
            Assert.That(GameEconomy.RewardFor(outcome), Is.EqualTo(expected));
        }

        [Test]
        public void RecordingBattleAddsCoinsAndResultExactlyOnce()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);

            profile.RecordBattle(BattleRewardOutcome.Win, GameEconomy.WinReward);

            Assert.That(profile.Coins, Is.EqualTo(130));
            Assert.That(profile.Battles, Is.EqualTo(1));
            Assert.That(profile.Wins, Is.EqualTo(1));
            Assert.That(profile.Draws, Is.Zero);
            Assert.That(profile.Losses, Is.Zero);
        }

        [Test]
        public void GachaCostsOneHundredAndUnlocksTheEighthBeast()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            GachaService gacha = new GachaService(new FixedRandom(7));

            Assert.That(gacha.TryPull(profile, roster, out GachaResult result), Is.True);
            Assert.That(result.Beast.InstanceId, Is.EqualTo("inst_7"));
            Assert.That(result.IsNew, Is.True);
            Assert.That(result.Copies, Is.EqualTo(1));
            Assert.That(result.RemainingCoins, Is.Zero);
            Assert.That(profile.Owns("inst_7"), Is.True);
        }

        [Test]
        public void DuplicateGachaIncreasesCopiesWithoutAddingAnotherEntry()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.Acquire("inst_7");
            profile.AddCoins(GameEconomy.GachaCost);
            GachaService gacha = new GachaService(new FixedRandom(0));

            Assert.That(gacha.TryPull(profile, roster, out GachaResult result), Is.True);
            Assert.That(result.IsNew, Is.False);
            Assert.That(result.Copies, Is.EqualTo(2));
            Assert.That(profile.Beasts.Count, Is.EqualTo(8));
        }

        [Test]
        public void GachaDoesNotChangeAnythingWhenCoinsAreInsufficient()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            Assert.That(profile.TrySpendCoins(1), Is.True);
            int entries = profile.Beasts.Count;

            GachaService gacha = new GachaService(new FixedRandom(7));

            Assert.That(gacha.TryPull(profile, roster, out GachaResult result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(profile.Coins, Is.EqualTo(99));
            Assert.That(profile.Beasts.Count, Is.EqualTo(entries));
        }

        [Test]
        public void StarterSquadMakesBattlePlayableImmediately()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            InMemorySquadRepository squads = new InMemorySquadRepository();

            PlayerProfileProvider.EnsureStarterSquad(profile, roster, squads, "1");

            Assert.That(squads.TryLoad("1", out SquadSnapshot snapshot), Is.True);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                Assert.That(snapshot.GetInstanceId(i), Is.EqualTo("inst_" + i));
            }
        }

        [Test]
        public void ValidSavedSquadIsNotOverwritten()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            InMemorySquadRepository squads = new InMemorySquadRepository();
            string[] reversed =
            {
                "inst_6", "inst_5", "inst_4", "inst_3", "inst_2", "inst_1", "inst_0",
            };
            squads.Save("1", new SquadSnapshot(reversed));

            PlayerProfileProvider.EnsureStarterSquad(profile, roster, squads, "1");

            Assert.That(squads.TryLoad("1", out SquadSnapshot snapshot), Is.True);
            Assert.That(snapshot.GetInstanceId(0), Is.EqualTo("inst_6"));
            Assert.That(snapshot.GetInstanceId(6), Is.EqualTo("inst_0"));
        }

        [Test]
        public void PendingRewardsAggregateUntilHomeShowsThem()
        {
            GameFlowState.AddPendingReward(30);
            GameFlowState.AddPendingReward(10);

            Assert.That(GameFlowState.PendingReward, Is.EqualTo(40));
            Assert.That(GameFlowState.PendingBattles, Is.EqualTo(2));
            Assert.That(GameFlowState.ConsumePendingReward(), Is.EqualTo(40));
            Assert.That(GameFlowState.HasPendingReward, Is.False);
        }

        [Test]
        public void OwnedListContainsOnlyUnlockedInstances()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);

            Assert.That(PlayerProfileProvider.OwnedFrom(profile, roster).Count, Is.EqualTo(7));
        }

        [Test]
        public void ProfileRoundTripsThroughPlayerPrefsJson()
        {
            PlayerPrefsPlayerProfileRepository repository =
                new PlayerPrefsPlayerProfileRepository(ProfileKey);
            PlayerProfile profile = PlayerProfile.CreateNew();
            profile.Acquire("inst_7");
            profile.RecordBattle(BattleRewardOutcome.Draw, GameEconomy.DrawReward);

            repository.Save(profile);

            PlayerPrefsPlayerProfileRepository reopened =
                new PlayerPrefsPlayerProfileRepository(ProfileKey);
            Assert.That(reopened.TryLoad(out PlayerProfile loaded), Is.True);
            Assert.That(loaded.Coins, Is.EqualTo(120));
            Assert.That(loaded.Battles, Is.EqualTo(1));
            Assert.That(loaded.Draws, Is.EqualTo(1));
            Assert.That(loaded.CopiesOf("inst_7"), Is.EqualTo(1));
        }

        [Test]
        public void SquadRoundTripsThroughPlayerPrefsJson()
        {
            string[] ids =
            {
                "a", "b", "c", "d", "e", "f", "g",
            };
            PlayerPrefsSquadRepository repository =
                new PlayerPrefsSquadRepository(SquadKey);

            repository.Save("1", new SquadSnapshot(ids));

            PlayerPrefsSquadRepository reopened =
                new PlayerPrefsSquadRepository(SquadKey);
            Assert.That(reopened.TryLoad("1", out SquadSnapshot loaded), Is.True);
            Assert.That(loaded.GetInstanceId(0), Is.EqualTo("a"));
            Assert.That(loaded.GetInstanceId(6), Is.EqualTo("g"));
        }
    }
}
