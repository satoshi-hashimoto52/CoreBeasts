using UnityEngine;
using UnityEngine.EventSystems;

namespace CoreBeasts.Units
{
    /// <summary>
    /// ドラッグ表示の生成・追従・破棄を一箇所で管理します。
    /// 操作終了時に必ず破棄するため、表示が残りません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragGhostPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Canvas最前面のコンテナ。ここへドラッグ表示を作ります。")]
        private RectTransform ghostRoot;

        [SerializeField] private DragGhostView ghostPrefab;

        [SerializeField]
        [Tooltip("座標変換に使うCanvas。Overlayならカメラ不要です。")]
        private Canvas canvas;

        [SerializeField]
        [Tooltip("指で隠れないよう、指の位置からずらす量（Canvas units）。")]
        private Vector2 ghostOffset = new Vector2(0f, 96f);

        private DragGhostView current;

        /// <summary>ドラッグ表示を出しているか。</summary>
        public bool IsShowing => current != null;

        /// <summary>ドラッグ表示を作り、指の位置へ置きます。</summary>
        public void Show(
            OwnedCoreBeast beast,
            AttributePalette palette,
            UiTextCatalog text,
            PointerEventData eventData)
        {
            Hide();

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(DragGhostPresenter),
                    ReferenceCheck.Of(nameof(ghostRoot), ghostRoot),
                    ReferenceCheck.Of(nameof(ghostPrefab), ghostPrefab)))
            {
                return;
            }

            current = Instantiate(ghostPrefab, ghostRoot);
            current.name = "DragGhost";
            current.Bind(beast, palette, text);

            Move(eventData);
        }

        /// <summary>指の位置へ追従させます。</summary>
        public void Move(PointerEventData eventData)
        {
            if (current == null || ghostRoot == null || eventData == null)
            {
                return;
            }

            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    ghostRoot, eventData.position, eventCamera, out Vector2 local))
            {
                current.MoveTo(local + ghostOffset);
            }
        }

        /// <summary>ドラッグ表示を破棄します。何度呼んでも安全です。</summary>
        public void Hide()
        {
            if (current == null)
            {
                current = null;
                return;
            }

            GameObject target = current.gameObject;
            current = null;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}
