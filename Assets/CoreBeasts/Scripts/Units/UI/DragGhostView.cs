using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// ドラッグ中に指へ追従する表示。
    /// 元のカードは動かさず、これを最前面へ生成して動かします。
    ///
    /// 出すのはキャラクター画像だけです。
    /// 背景カード・名前・レベル・属性チップ・編成済みチェックは持ちません。
    /// 属性の見分けは<see cref="BeastThumbnailView"/>のPrimary / Secondary着色が担います。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragGhostView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private BeastThumbnailView thumbnail;

        /// <summary>着色済みの立ち絵（確認・テスト用）。</summary>
        public BeastThumbnailView Thumbnail => thumbnail;

        private void Awake()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (canvasGroup != null)
            {
                // ドロップ先の判定を妨げないよう、レイキャストを通します。
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        /// <summary>表示内容を設定します。</summary>
        public void Bind(
            OwnedCoreBeast beast,
            AttributePalette palette,
            UiTextCatalog text)
        {
            if (beast == null || !beast.IsValid)
            {
                return;
            }

            if (thumbnail != null)
            {
                thumbnail.Show(beast.Definition, palette);
            }
        }

        /// <summary>親RectTransform内のローカル座標へ移動します。</summary>
        public void MoveTo(Vector2 localPoint)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }
    }
}
