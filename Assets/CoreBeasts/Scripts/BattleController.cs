using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// Battle画面の操作とシーン遷移を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleController : MonoBehaviour
    {
        private const string HomeSceneName = "Home";

        [SerializeField]
        private Button homeButton;

        private void Awake()
        {
            if (homeButton == null)
            {
                Debug.LogError(
                    "HomeButtonがBattleControllerに設定されていません。",
                    this
                );

                enabled = false;
                return;
            }

            homeButton.onClick.AddListener(LoadHomeScene);
        }

        private void OnDestroy()
        {
            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(LoadHomeScene);
            }
        }

        private void LoadHomeScene()
        {
            SceneManager.LoadScene(HomeSceneName);
        }
    }
}
