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

        [Header("Acquisition portrait (Phase 7)")]
        [Tooltip("属性の配色。UnitSet・Battle と同じアセットを使います。")]
        [SerializeField]
        private AttributePalette attributePalette;

        [Tooltip("立ち絵の下地（UnitSet・Battle の縮小立ち絵と同じ素材）。")]
        [SerializeField]
        private Texture portraitBase;

        [Tooltip("一次属性の着色マスク。")]
        [SerializeField]
        private Texture portraitPrimaryMask;

        [Tooltip("二次属性の着色マスク。")]
        [SerializeField]
        private Texture portraitSecondaryMask;

        private HomeHubView hub;
        private PlayerProfile profile;
        private GachaSummonSession summon;

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
            summon = new GachaSummonSession(
                new GachaService(new SystemGachaRandomSource()),
                PlayerProfileProvider.Save);

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
                roster,
                attributePalette,
                new[] { portraitBase, portraitPrimaryMask, portraitSecondaryMask },
                summon,
                LoadBattleScene,
                () => SceneManager.LoadScene("UnitSet"));

            // 未確認の報酬があるときだけ、Home を見せずに報酬画面から始めます。
            hub.ShowInitial();
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
    }
}
