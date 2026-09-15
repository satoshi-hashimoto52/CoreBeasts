using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>編成枠の強調段階。</summary>
    public enum SlotHighlight
    {
        /// <summary>通常。</summary>
        None,

        /// <summary>ドラッグ中の配置先候補（控えめ）。</summary>
        Available,

        /// <summary>ドラッグ中の個体が既に入っている枠（入れ替え可能）。</summary>
        Swap,

        /// <summary>指が重なっている枠（強い強調）。</summary>
        Hovered,
    }

    /// <summary>
    /// 自部隊7枠のうちの1枠。選出順・立ち絵・空き表示を出します。
    /// 強調はドラッグ中に指が重なった枠だけに出し、常時は点灯させません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadSlotView : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IDropHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private BeastThumbnailView thumbnail;
        [SerializeField] private Image attributeChip;
        [SerializeField] private TMP_Text orderLabel;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text emptyLabel;

        [Header("Highlight appearance")]
        [SerializeField] private Color normalBackground = new Color(0.14f, 0.16f, 0.2f, 1f);
        [SerializeField] private Color normalFrame = new Color(0.32f, 0.36f, 0.42f, 1f);
        [SerializeField] private Color normalEmptyMark = new Color(0.45f, 0.5f, 0.58f, 1f);

        [Tooltip("ドラッグ中の配置先候補。控えめにします。")]
        [SerializeField] private Color availableBackground = new Color(0.17f, 0.21f, 0.19f, 1f);
        [SerializeField] private Color availableFrame = new Color(0.42f, 0.56f, 0.46f, 1f);

        [Tooltip("ドラッグ中の個体が既に入っている枠（入れ替え）。")]
        [SerializeField] private Color swapBackground = new Color(0.24f, 0.22f, 0.16f, 1f);
        [SerializeField] private Color swapFrame = new Color(0.9f, 0.76f, 0.38f, 1f);

        [Tooltip("指が重なっている枠。")]
        [SerializeField] private Color targetBackground = new Color(0.24f, 0.3f, 0.24f, 1f);
        [SerializeField] private Color targetFrame = new Color(0.55f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color targetEmptyMark = new Color(0.75f, 1f, 0.75f, 1f);

        [Tooltip("配置が成立したとき、枠を明るくする時間(秒)。")]
        [SerializeField] [Range(0.05f, 0.4f)] private float placedFlashSeconds = 0.12f;

        private int slotIndex = -1;
        private ISquadSlotListener listener;
        private SlotHighlight baseHighlight = SlotHighlight.None;
        private SlotHighlight applied = SlotHighlight.None;
        private float flashRemaining;

        /// <summary>0始まりの枠番号。</summary>
        public int SlotIndex => slotIndex;

        /// <summary>枠番号と通知先を設定します。</summary>
        public void Bind(int index, UiTextCatalog text, ISquadSlotListener slotListener)
        {
            slotIndex = index;
            listener = slotListener;

            if (orderLabel != null && text != null)
            {
                orderLabel.text = text.FormatSlotOrder(index + 1);
            }

            SetBaseHighlight(SlotHighlight.None);
        }

        /// <summary>枠の中身を更新します。null なら空き枠表示。</summary>
        public void Show(
            OwnedCoreBeast beast,
            AttributePalette palette,
            UiTextCatalog text)
        {
            bool hasBeast = beast != null && beast.IsValid;

            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(!hasBeast);

                if (!hasBeast && text != null)
                {
                    emptyLabel.text = text.EmptySlot;
                }
            }

            if (thumbnail != null)
            {
                if (hasBeast)
                {
                    thumbnail.Show(beast.Definition, palette);
                }
                else
                {
                    thumbnail.Clear();
                }
            }

            if (attributeChip != null)
            {
                attributeChip.enabled = hasBeast;

                if (hasBeast && palette != null)
                {
                    attributeChip.color = palette
                        .GetColors(beast.Definition.PrimaryAttribute)
                        .PrimaryColor;
                }
            }

            if (attributeLabel != null)
            {
                attributeLabel.gameObject.SetActive(hasBeast);

                if (hasBeast && text != null)
                {
                    attributeLabel.text =
                        text.BuildAttributeSymbol(beast.Definition);
                }
            }
        }

        /// <summary>現在の強調段階（テスト・確認用）。</summary>
        public SlotHighlight Highlight => applied;

        /// <summary>
        /// ドラッグ状況に応じた基準の強調段階を設定します。
        /// 指が重なったときの強い強調は、この基準へ戻せるよう別に保持します。
        /// </summary>
        public void SetBaseHighlight(SlotHighlight highlight)
        {
            baseHighlight = highlight;

            if (flashRemaining <= 0f)
            {
                ApplyColors(highlight);
            }
        }

        /// <summary>配置が成立したことを、短時間の明滅で知らせます。</summary>
        public void FlashPlaced()
        {
            flashRemaining = placedFlashSeconds;

            ApplyColors(SlotHighlight.Hovered);
        }

        private void Update()
        {
            if (flashRemaining <= 0f)
            {
                return;
            }

            flashRemaining -= Time.unscaledDeltaTime;

            if (flashRemaining <= 0f)
            {
                flashRemaining = 0f;

                ApplyColors(baseHighlight);
            }
        }

        private void ApplyColors(SlotHighlight highlight)
        {
            applied = highlight;

            Color back = normalBackground;
            Color edge = normalFrame;
            Color mark = normalEmptyMark;

            switch (highlight)
            {
                case SlotHighlight.Available:
                    back = availableBackground;
                    edge = availableFrame;
                    break;

                case SlotHighlight.Swap:
                    back = swapBackground;
                    edge = swapFrame;
                    break;

                case SlotHighlight.Hovered:
                    back = targetBackground;
                    edge = targetFrame;
                    mark = targetEmptyMark;
                    break;
            }

            if (background != null)
            {
                background.color = back;
            }

            if (frame != null)
            {
                frame.color = edge;
            }

            if (emptyLabel != null)
            {
                emptyLabel.color = mark;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.dragging)
            {
                return;
            }

            if (slotIndex >= 0)
            {
                listener?.OnSlotTapped(slotIndex);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (slotIndex >= 0)
            {
                listener?.OnSlotDropped(slotIndex);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            GestureLog.Write(
                "SquadSlotView", "OnPointerEnter", eventData, 0f, 0f,
                CardGestureState.SquadDragging, null, slotIndex);

            if (listener != null && listener.IsDraggingBeast && flashRemaining <= 0f)
            {
                ApplyColors(SlotHighlight.Hovered);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (flashRemaining <= 0f)
            {
                ApplyColors(baseHighlight);
            }
        }
    }
}
