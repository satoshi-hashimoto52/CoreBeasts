using UnityEngine;

namespace CoreBeasts.UI
{
    /// <summary>
    /// iPhoneやiPadのノッチ、Dynamic Island、
    /// ホームインジケーターを避けるようにRectTransformを調整します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaController : MonoBehaviour
    {
        private RectTransform targetRectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private ScreenOrientation lastOrientation;

        private void Awake()
        {
            targetRectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = GetComponent<RectTransform>();
            }

            ApplySafeArea();
        }

        private void Update()
        {
            Vector2Int currentScreenSize = new(Screen.width, Screen.height);

            if (Screen.safeArea != lastSafeArea ||
                currentScreenSize != lastScreenSize ||
                Screen.orientation != lastOrientation)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            if (targetRectTransform == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            targetRectTransform.anchorMin = anchorMin;
            targetRectTransform.anchorMax = anchorMax;
            targetRectTransform.offsetMin = Vector2.zero;
            targetRectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;
        }
    }
}