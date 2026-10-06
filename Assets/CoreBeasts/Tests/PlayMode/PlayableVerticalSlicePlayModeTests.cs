using System.Collections;

using CoreBeasts.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    public sealed class PlayableVerticalSlicePlayModeTests
    {
        private ISquadRepository originalSquads;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalSquads = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());
            PlayerProfileProvider.SetRepository(new InMemoryPlayerProfileRepository());
            GameFlowState.Reset();

            yield return SceneManager.LoadSceneAsync("Home", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlowState.Reset();
            PlayerProfileProvider.Reset();
            SquadRepositoryProvider.SetShared(originalSquads);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeGachaAcquisitionAndCollectionFormAPlayableLoop()
        {
            Assert.That(Find("PlayableFlow"), Is.Not.Null);
            Assert.That(Find("HomePage").activeSelf, Is.True);
            Assert.That(Find("RewardPage").activeSelf, Is.False);

            Click("GACHAButton");
            yield return null;

            Assert.That(Find("GachaPage").activeSelf, Is.True);

            Click("ACTIVATE100Button");
            yield return null;

            Assert.That(Find("AcquisitionPage").activeSelf, Is.True);

            PlayerProfile profile = PlayerProfileProvider.Get(LoadRoster());
            Assert.That(profile.Coins, Is.Zero);
            Assert.That(profile.Beasts.Count, Is.EqualTo(8));

            Click("TOCOLLECTIONButton");
            yield return null;

            Assert.That(Find("CollectionPage").activeSelf, Is.True);
            string collectionText = Find("CollectionPage")
                .GetComponentInChildren<TMPro.TMP_Text>(true).text;
            Assert.That(collectionText, Does.Contain("COLLECTION"));
            Assert.That(collectionText, Does.Contain("[OWNED]"));
            Assert.That(collectionText, Does.Not.Contain("◆"));
            Assert.That(collectionText, Does.Not.Contain("◇"));
        }

        [UnityTest]
        public IEnumerator ReturningFromBattleOpensTheRewardPageExactlyOnce()
        {
            GameFlowState.AddPendingReward(GameEconomy.WinReward);

            yield return SceneManager.LoadSceneAsync("Home", LoadSceneMode.Single);
            yield return null;

            GameObject reward = Find("RewardPage");
            Assert.That(reward.activeSelf, Is.True);
            Assert.That(reward.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Does.Contain("MISSION COMPLETE"));
            Assert.That(GameFlowState.HasPendingReward, Is.False);

            yield return SceneManager.LoadSceneAsync("Home", LoadSceneMode.Single);
            yield return null;

            Assert.That(Find("HomePage").activeSelf, Is.True);
            Assert.That(Find("RewardPage").activeSelf, Is.False);
        }

        private static void Click(string name)
        {
            GameObject target = Find(name);
            Assert.That(target, Is.Not.Null, name + " が見つかりません。");
            Button button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
        }

        private static GameObject Find(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name && all[i].scene.IsValid())
                {
                    return all[i];
                }
            }

            return null;
        }

        private static CoreBeastRoster LoadRoster()
        {
            CoreBeastRoster[] rosters = Resources.FindObjectsOfTypeAll<CoreBeastRoster>();

            for (int i = 0; i < rosters.Length; i++)
            {
                if (rosters[i] != null && rosters[i].Owned.Count == 8)
                {
                    return rosters[i];
                }
            }

            Assert.Fail("8体のテスト用Rosterが見つかりません。");
            return null;
        }
    }
}
