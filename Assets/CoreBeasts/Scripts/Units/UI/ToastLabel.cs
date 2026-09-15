using System.Collections;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 短時間だけ出るメッセージ表示。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastLabel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;

        [SerializeField] [Min(0.2f)] private float holdSeconds = 1.4f;
        [SerializeField] [Min(0.05f)] private float fadeSeconds = 0.3f;

        private Coroutine routine;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            Hide();
        }

        /// <summary>メッセージを出して、しばらく後に自動で消します。</summary>
        public void Show(string message)
        {
            if (label == null || canvasGroup == null)
            {
                Debug.LogError(
                    $"[ToastLabel] GameObject「{name}」の参照が不足しているため" +
                    "メッセージを表示できません。",
                    this
                );

                return;
            }

            label.text = message ?? string.Empty;

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            if (!isActiveAndEnabled)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            canvasGroup.alpha = 1f;

            yield return new WaitForSeconds(holdSeconds);

            float elapsed = 0f;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);

                yield return null;
            }

            Hide();
            routine = null;
        }

        private void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
