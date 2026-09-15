using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// ボタン1つでシーンを読み込む汎用コンポーネント。
    /// 画面ごとのControllerを増やさずに導線を足すために使います。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoadButton : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        [Tooltip("遷移先のシーン名。Build Settingsへ登録済みである必要があります。")]
        private string sceneName = string.Empty;

        private void Awake()
        {
            if (button == null || string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError(
                    $"[SceneLoadButton] GameObject「{name}」の" +
                    "ButtonまたはScene Nameが未設定のため、遷移できません。",
                    this
                );

                enabled = false;
                return;
            }

            button.onClick.AddListener(LoadScene);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(LoadScene);
            }
        }

        private void LoadScene()
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
