using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Boot
{
    /// <summary>
    /// Boot画面の操作とシーン遷移を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootController : MonoBehaviour
    {
        private const string HomeSceneName = "Home";

        [SerializeField]
        private Button startButton;

        private void Awake()
        {
            if (startButton == null)
            {
                Debug.LogError(
                    "StartButtonがBootControllerに設定されていません。",
                    this
                );

                enabled = false;
                return;
            }

            startButton.onClick.AddListener(LoadHomeScene);
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(LoadHomeScene);
            }
        }

        private void LoadHomeScene()
        {
            SceneManager.LoadScene(HomeSceneName);
        }
    }
}