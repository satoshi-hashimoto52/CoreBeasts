using System.Collections.Generic;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 連続位相での1枠。中央からの距離が小数なので、Drag中も途切れません。
    /// </summary>
    public readonly struct BattleRingPhaseSlot
    {
        public readonly BattleRingSlot Slot;

        /// <summary>リング内の位置（0始まり）。同じ個体は必ず同じ値です。</summary>
        public readonly int Index;

        /// <summary>
        /// 中央からの符号つき距離（カード間隔単位、小数）。
        /// 左が負、右が正。0 がちょうど中央です。
        /// </summary>
        public readonly float Distance;

        public BattleRingPhaseSlot(BattleRingSlot slot, int index, float distance)
        {
            Slot = slot;
            Index = index;
            Distance = distance;
        }
    }

    /// <summary>
    /// 連続位相から「今どの個体をどこへ出すか」を決めます。
    ///
    /// 端という概念を持ちません。各個体の距離は「一番近い巻き位置」へ折り返すので、
    /// 左へ出た個体はその瞬間に右側の距離を持ち、逆も同じです。
    /// PointerUp も Refresh も待たずに補充されるのは、この折り返しのためです。
    ///
    /// 同じ個体は 0..Count-1 の Index を1つずつしか持たないため、
    /// 返す一覧に同一 InstanceId が二度現れることはありません。
    ///
    /// Unityへ依存しないため、そのままテストできます。
    /// </summary>
    public static class BattleRingPhase
    {
        /// <summary>同時に出す最大数。</summary>
        public const int MaxVisible = BattleRingPresentation.MaxVisible;

        /// <summary>
        /// 位相 <paramref name="offset"/> における表示一覧を、中央に近い順で返します。
        ///
        /// <paramref name="offset"/> は「指がカード何枚ぶん動いたか」です。
        /// 右へ動かすと正、左へ動かすと負になります。
        /// </summary>
        public static IReadOnlyList<BattleRingPhaseSlot> Resolve(
            BattleUnitRingModel model, float offset, int maxVisible = MaxVisible)
        {
            List<BattleRingPhaseSlot> visible = new List<BattleRingPhaseSlot>();

            if (model == null || model.IsEmpty)
            {
                return visible;
            }

            int count = model.Count;

            for (int i = 0; i < count; i++)
            {
                // 中央から数えて i 番目の個体。
                BattleRingSlot slot = model.SlotAtOffset(i);

                // i と同じ剰余を持つ位置のうち、いま一番近いものを選びます。
                // これがリングの「折り返し」そのものです。
                float distance = NearestWrap(i, count, offset);

                visible.Add(new BattleRingPhaseSlot(slot, i, distance));
            }

            // 中央に近い順に並べ、必要数だけ残します。
            visible.Sort(CompareByDistance);

            int limit = maxVisible < count ? maxVisible : count;

            if (visible.Count > limit)
            {
                visible.RemoveRange(limit, visible.Count - limit);
            }

            return visible;
        }

        /// <summary>
        /// 位置 <paramref name="index"/> の個体が、位相 <paramref name="offset"/> のときに
        /// 描かれるべき距離。剰余が同じ候補のうち、中央に一番近いものを返します。
        /// </summary>
        public static float NearestWrap(int index, int count, float offset)
        {
            if (count <= 0)
            {
                return 0f;
            }

            // index + offset - count * round((index + offset) / count)
            float raw = index + offset;
            float turns = raw / count;

            int rounded = (int)(turns >= 0f ? turns + 0.5f : turns - 0.5f);

            return raw - count * rounded;
        }

        /// <summary>
        /// 距離から見た目の区分を決めます。描画順にも使います。
        /// </summary>
        public static BattleRingPlacement PlacementOf(float distance)
        {
            float magnitude = distance < 0f ? -distance : distance;

            if (magnitude < 0.5f)
            {
                return BattleRingPlacement.Center;
            }

            if (magnitude < 1.5f)
            {
                return distance < 0f
                    ? BattleRingPlacement.AdjacentLeft
                    : BattleRingPlacement.AdjacentRight;
            }

            return distance < 0f
                ? BattleRingPlacement.OuterLeft
                : BattleRingPlacement.OuterRight;
        }

        /// <summary>
        /// 中央に近いものほど手前（大きい値）。中央が必ず最前面になります。
        /// </summary>
        public static int DrawOrderOf(float distance)
        {
            float magnitude = distance < 0f ? -distance : distance;

            // 距離 0 で 1000、遠いほど小さくなります。
            return 1000 - (int)(magnitude * 100f);
        }

        private static int CompareByDistance(
            BattleRingPhaseSlot a, BattleRingPhaseSlot b)
        {
            float da = a.Distance < 0f ? -a.Distance : a.Distance;
            float db = b.Distance < 0f ? -b.Distance : b.Distance;

            int byDistance = da.CompareTo(db);

            // 同距離（左右対称）のときは、並びを安定させるため Index で決めます。
            return byDistance != 0 ? byDistance : a.Index.CompareTo(b.Index);
        }
    }
}
