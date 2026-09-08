using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Home
{
    [DisallowMultipleComponent]
    public sealed class HomeController : MonoBehaviour
    {
        private const string BattleSceneName = "Battle";

        [SerializeField]
        private Button battleButton;

        private void Awake()
        {
            if (battleButton == null)
            {
                Debug.LogError(
                    "BattleButtonがHomeControllerに設定されていません。",
                    this
                );

                enabled = false;
                return;
            }

            battleButton.onClick.AddListener(LoadBattleScene);
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