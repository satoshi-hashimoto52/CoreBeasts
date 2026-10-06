using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using CoreBeasts.Progression;
using CoreBeasts.Units;
using TMPro;

namespace CoreBeasts.Home
{
    [DisallowMultipleComponent]
    public sealed class HomeController : MonoBehaviour
    {
        private const string BattleSceneName = "Battle";

        [SerializeField]
        private Button battleButton;

        [SerializeField]
        private CoreBeastRoster roster;

        private HomeHubView hub;
        private PlayerProfile profile;
        private GachaService gacha;

        public CoreBeastRoster Roster => roster;

        private void Awake()
        {
            if (battleButton == null || roster == null)
            {
                Debug.LogError(
                    "BattleButtonまたはRosterがHomeControllerに設定されていません。",
                    this
                );

                enabled = false;
                return;
            }

            battleButton.onClick.AddListener(LoadBattleScene);

            // 通常起動では編成も端末へ保存します。テストはProviderをInMemoryへ差し替えられます。
            SquadRepositoryProvider.UsePersistentDefault();

            profile = PlayerProfileProvider.Get(roster);
            PlayerProfileProvider.EnsureStarterSquad(
                profile, roster, SquadRepositoryProvider.Shared, "1");
            gacha = new GachaService(new SystemGachaRandomSource());

            TMP_Text template = battleButton.GetComponentInChildren<TMP_Text>(true);
            RectTransform host = battleButton.transform.parent as RectTransform;

            if (template == null || host == null)
            {
                Debug.LogError("Homeの表示テンプレートまたはSafeAreaを取得できません。", this);
                enabled = false;
                return;
            }

            hub = gameObject.AddComponent<HomeHubView>();
            hub.Build(
                host,
                template.font,
                LoadBattleScene,
                () => SceneManager.LoadScene("UnitSet"),
                PullGacha);

            if (GameFlowState.HasPendingReward)
            {
                int battles = GameFlowState.PendingBattles;
                int reward = GameFlowState.ConsumePendingReward();
                hub.ShowReward(reward, battles, profile);
            }
            else
            {
                hub.ShowHome(profile);
            }
        }

        private void OnDestroy()
        {
            if (battleButton != null)
            {
                battleButton.onClick.RemoveListener(LoadBattleScene);
            }
        }

        private void LoadBattleScene()
        {
            SceneManager.LoadScene(BattleSceneName);
        }

        private void PullGacha()
        {
            if (!gacha.TryPull(profile, roster, out GachaResult result))
            {
                hub.ShowGacha(profile, "NOT ENOUGH CORE COINS");
                return;
            }

            PlayerProfileProvider.Save();
            hub.ShowAcquisition(result);
        }
    }
}
