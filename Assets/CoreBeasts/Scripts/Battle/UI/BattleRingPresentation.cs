using System.Collections.Generic;

namespace CoreBeasts.Battle.UI
{
    /// <summary>リング上の見え方の区分。</summary>
    public enum BattleRingPlacement
    {
        /// <summary>中央。最前面で最大。</summary>
        Center = 0,

        /// <summary>中央のすぐ左。</summary>
        AdjacentLeft = 1,

        /// <summary>中央のすぐ右。</summary>
        AdjacentRight = 2,

        /// <summary>左の外側。</summary>
        OuterLeft = 3,

        /// <summary>右の外側。</summary>
        OuterRight = 4,

        /// <summary>奥側。中央より背面へ小さく置きます。</summary>
        Back = 5,
    }

    /// <summary>画面へ出す1枠。中央からの距離と区分を持ちます。</summary>
    public readonly struct BattleRingVisibleSlot
    {
        public readonly BattleRingSlot Slot;

        /// <summary>中央からの相対位置。左が負、右が正。</summary>
        public readonly int Offset;

        public readonly BattleRingPlacement Placement;

        public BattleRingVisibleSlot(
            BattleRingSlot slot, int offset, BattleRingPlacement placement)
        {
            Slot = slot;
            Offset = offset;
            Placement = placement;
        }
    }

    /// <summary>
    /// 残り人数ごとに「どの枠をどこへ出すか」を決めます。
    ///
    /// 同じ個体を左右へ複製して空間を埋めることは決してしません。
    /// 返す一覧の個体IDは必ず重複しません（残り人数ぶんが上限です）。
    ///
    /// Unityへ依存しないため、そのままテストできます。
    /// </summary>
    public static class BattleRingPresentation
    {
        /// <summary>同時に出せる最大数。</summary>
        public const int MaxVisible = 5;

        /// <summary>
        /// 中央からのオフセット一覧を、残り人数に応じて決めます。
        ///
        ///   7〜5体 : -2 -1 0 +1 +2（最大5体。残りはリング裏側として出しません）
        ///   4体    : -1 0 +1 と、残る1体を奥側(+2)へ
        ///   3体    : -1 0 +1
        ///   2体    : 0 と +1 のみ（-1 は +1 と同じ個体になるため出しません）
        ///   1体    : 0 のみ
        ///   0体    : 空
        /// </summary>
        public static IReadOnlyList<BattleRingVisibleSlot> Resolve(
            BattleUnitRingModel model)
        {
            List<BattleRingVisibleSlot> visible = new List<BattleRingVisibleSlot>();

            if (model == null || model.IsEmpty)
            {
                return visible;
            }

            int count = model.Count;

            Add(visible, model, 0, BattleRingPlacement.Center);

            if (count == 1)
            {
                return visible;
            }

            if (count == 2)
            {
                // -1 と +1 は同じ個体です。片側だけに出し、複製は作りません。
                Add(visible, model, 1, BattleRingPlacement.AdjacentRight);
                return visible;
            }

            Add(visible, model, -1, BattleRingPlacement.AdjacentLeft);
            Add(visible, model, 1, BattleRingPlacement.AdjacentRight);

            if (count == 3)
            {
                return visible;
            }

            if (count == 4)
            {
                // 残る1体は左右どちらの外側でもないため、奥側中央へ小さく置きます。
                Add(visible, model, 2, BattleRingPlacement.Back);
                return visible;
            }

            Add(visible, model, -2, BattleRingPlacement.OuterLeft);
            Add(visible, model, 2, BattleRingPlacement.OuterRight);

            return visible;
        }

        private static void Add(
            List<BattleRingVisibleSlot> visible,
            BattleUnitRingModel model,
            int offset,
            BattleRingPlacement placement)
        {
            BattleRingSlot slot = model.SlotAtOffset(offset);

            if (!slot.IsValid)
            {
                return;
            }

            visible.Add(new BattleRingVisibleSlot(slot, offset, placement));
        }

        /// <summary>
        /// 描画順（先に描くものから）。奥側 → 外側 → 隣接 → 中央。
        /// 中央が必ず最前面になります。
        /// </summary>
        public static int DrawOrderOf(BattleRingPlacement placement)
        {
            switch (placement)
            {
                case BattleRingPlacement.Back:
                    return 0;

                case BattleRingPlacement.OuterLeft:
                case BattleRingPlacement.OuterRight:
                    return 1;

                case BattleRingPlacement.AdjacentLeft:
                case BattleRingPlacement.AdjacentRight:
                    return 2;

                default:
                    return 3;
            }
        }
    }
}
