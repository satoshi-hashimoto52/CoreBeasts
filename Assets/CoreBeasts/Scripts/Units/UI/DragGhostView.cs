using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// ドラッグ中に指へ追従する半透明表示。
    /// 元のカードは動かさず、これを最前面へ生成して動かします。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragGhostView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private BeastThumbnailView thumbnail;
        [SerializeField] private Image attributeChip;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text nameLabel;

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

            CoreBeastDefinition definition = beast.Definition;

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }

            if (attributeLabel != null && text != null)
            {
                attributeLabel.text = text.BuildAttributeSymbol(definition);
            }

            if (attributeChip != null && palette != null)
            {
                attributeChip.color =
                    palette.GetColors(definition.PrimaryAttribute).PrimaryColor;
            }

            if (thumbnail != null)
            {
                thumbnail.Show(definition, palette);
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
