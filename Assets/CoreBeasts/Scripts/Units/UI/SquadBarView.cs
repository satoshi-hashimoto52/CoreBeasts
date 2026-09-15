using System.Collections.Generic;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 自部隊7枠の表示。横スクロールせず、7枠を同時に並べます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private SquadSlotView slotPrefab;

        private readonly List<SquadSlotView> slots = new List<SquadSlotView>();

        /// <summary>7枠を生成します。</summary>
        public void Build(UiTextCatalog text, ISquadSlotListener listener)
        {
            Clear();

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(SquadBarView),
                    ReferenceCheck.Of(nameof(content), content),
                    ReferenceCheck.Of(nameof(slotPrefab), slotPrefab),
                    ReferenceCheck.Of(nameof(text), text),
                    ReferenceCheck.Of(nameof(listener), listener)))
            {
                return;
            }

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                SquadSlotView slot = Instantiate(slotPrefab, content);
                slot.name = $"Slot_{i + 1}";
                slot.Bind(i, text, listener);

                slots.Add(slot);
            }
        }

        /// <summary>編成データの内容を枠へ反映します。</summary>
        public void Refresh(
            SquadFormation formation,
            AttributePalette palette,
            UiTextCatalog text)
        {
            if (formation == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Show(formation.GetAt(slots[i].SlotIndex), palette, text);
            }
        }

        /// <summary>
        /// ドラッグ中、7枠を配置先候補として控えめに強調します。
        /// ドラッグ中の個体が既に入っている枠は、入れ替え可能と分かる表示にします。
        /// </summary>
        public void SetDragTarget(bool dragging, int occupiedSlotIndex)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SlotHighlight highlight;

                if (!dragging)
                {
                    highlight = SlotHighlight.None;
                }
                else if (slots[i].SlotIndex == occupiedSlotIndex)
                {
                    highlight = SlotHighlight.Swap;
                }
                else
                {
                    highlight = SlotHighlight.Available;
                }

                slots[i].SetBaseHighlight(highlight);
            }
        }

        /// <summary>配置が成立した枠を短時間だけ明るくします。</summary>
        public void FlashSlot(int slotIndex)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].SlotIndex == slotIndex)
                {
                    slots[i].FlashPlaced();
                    return;
                }
            }
        }

        /// <summary>
        /// すべての枠の強調を解除します。
        /// 強調はドラッグ中に指が重なった枠だけが自分で点灯させるため、
        /// ここでは消灯のみを行います。
        /// </summary>
        public void ClearHighlights()
        {
            SetDragTarget(false, -1);
        }

        private void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    DestroyView(slots[i].gameObject);
                }
            }

            slots.Clear();
        }

        /// <summary>
        /// 再生成時に古い表示が残らないよう破棄します。
        /// Destroy はEditModeで遅延するため、実行中かどうかで使い分けます
        /// （DragGhostPresenter / PortraitRenderTarget と同じ方式）。
        /// </summary>
        private static void DestroyView(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
