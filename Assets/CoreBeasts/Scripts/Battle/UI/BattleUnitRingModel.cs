using System;
using System.Collections.Generic;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 自軍の未使用個体を、左右端のない循環リストとして持つ選択モデル。
    ///
    /// Unityへ依存しないため、通常のC#オブジェクトとしてそのままテストできます。
    /// <see cref="BattleSession"/>も<see cref="BattleFlowCoordinator"/>も触りません。
    ///
    /// 重要: 回しただけでは出撃が決まりません。
    /// ここが持つ<see cref="FocusedInstanceId"/>はあくまで「今リング中央にある個体」で、
    /// 実際の選出確定は上スライドが成立したときに、呼び出し側が
    /// <see cref="BattleFlowCoordinator.SelectPlayerUnit"/>へ渡して行います。
    /// スクロール中に選出を確定しないので、何度でも選び直せます。
    ///
    /// 同じ個体IDは二度持ちません。したがって画面上に同一個体が重複することもありません。
    /// </summary>
    public sealed class BattleUnitRingModel
    {
        private readonly List<BattleRingSlot> slots = new List<BattleRingSlot>();

        /// <summary>作り直し用に、最初に渡された編成をそのまま控えます。</summary>
        private readonly List<BattleRingSlot> original = new List<BattleRingSlot>();

        private int centerIndex;

        /// <summary>リングに残っている個体数。</summary>
        public int Count => slots.Count;

        /// <summary>リングが空か。空なら表示そのものを隠します。</summary>
        public bool IsEmpty => slots.Count == 0;

        /// <summary>回転できるか。1体以下では横回転しません。</summary>
        public bool CanRotate => slots.Count > 1;

        /// <summary>今リング中央にある個体。空なら null。</summary>
        public string FocusedInstanceId =>
            slots.Count == 0 ? null : slots[centerIndex].InstanceId;

        /// <summary>今リング中央にある枠。空なら既定値。</summary>
        public BattleRingSlot Focused =>
            slots.Count == 0 ? default : slots[centerIndex];

        /// <summary>中央の位置（0始まり）。空なら -1。</summary>
        public int CenterIndex => slots.Count == 0 ? -1 : centerIndex;

        /// <summary>残っている枠を、編成順のまま返します。</summary>
        public IReadOnlyList<BattleRingSlot> Slots => slots;

        /// <summary>
        /// 編成からリングを作り直します。REMATCHもここを通ります。
        /// 中央は先頭の個体になります。
        /// </summary>
        public void Build(IReadOnlyList<BattleUnitCard> cards)
        {
            List<string> instanceIds = new List<string>();

            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    instanceIds.Add(cards[i] != null ? cards[i].InstanceId : null);
                }
            }

            Build(instanceIds);
        }

        /// <summary>
        /// 個体IDの並びからリングを作り直します。
        /// 表示用の型を通さないため、Unityを起動せずに組み立てられます。
        /// </summary>
        public void Build(IReadOnlyList<string> instanceIds)
        {
            slots.Clear();
            original.Clear();
            centerIndex = 0;

            if (instanceIds == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < instanceIds.Count; i++)
            {
                string instanceId = instanceIds[i];

                if (string.IsNullOrEmpty(instanceId))
                {
                    continue;
                }

                // 同じ個体IDを二度持たないのは、画面上の重複を根本から防ぐためです。
                if (!seen.Add(instanceId))
                {
                    continue;
                }

                // 編成番号は「今の並び」ではなく「元の並び」です。
                // 使用済みが抜けても 1〜7 の番号は変わりません。
                BattleRingSlot slot = new BattleRingSlot(instanceId, slots.Count + 1);

                slots.Add(slot);
                original.Add(slot);
            }
        }

        /// <summary>元の7体・元の順序へ戻します。REMATCH用です。</summary>
        public void Restore()
        {
            slots.Clear();
            slots.AddRange(original);
            centerIndex = 0;
        }

        /// <summary>指定個体を中央へ持ってきます。居なければ何もしません。</summary>
        public bool Focus(string instanceId)
        {
            int index = IndexOf(instanceId);

            if (index < 0 || index == centerIndex)
            {
                return false;
            }

            centerIndex = index;

            return true;
        }

        /// <summary>左へ1つ回します（中央が1つ前の個体になります）。</summary>
        public bool RotateLeft(int steps = 1)
        {
            return Rotate(-steps);
        }

        /// <summary>右へ1つ回します（中央が1つ後の個体になります）。</summary>
        public bool RotateRight(int steps = 1)
        {
            return Rotate(steps);
        }

        /// <summary>
        /// 中央を相対移動します。左右端はありません。
        /// 何周ぶん渡されても modulo で必ずリング内へ収まります。
        /// </summary>
        public bool Rotate(int steps)
        {
            if (!CanRotate || steps == 0)
            {
                return false;
            }

            centerIndex = Wrap(centerIndex + steps, slots.Count);

            return true;
        }

        /// <summary>
        /// 中央から <paramref name="offset"/> だけ離れた枠。
        /// 端は無いので、どんな値でも必ず何かが返ります（空のときだけ既定値）。
        /// </summary>
        public BattleRingSlot SlotAtOffset(int offset)
        {
            if (slots.Count == 0)
            {
                return default;
            }

            return slots[Wrap(centerIndex + offset, slots.Count)];
        }

        /// <summary>指定個体がまだリングに残っているか。</summary>
        public bool Contains(string instanceId)
        {
            return IndexOf(instanceId) >= 0;
        }

        /// <summary>指定個体の編成番号。居なければ 0。</summary>
        public int SquadNumberOf(string instanceId)
        {
            int index = IndexOf(instanceId);

            return index >= 0 ? slots[index].SquadNumber : 0;
        }

        /// <summary>
        /// 使用済みになった個体をリングから外します。
        ///
        /// 外したのが中央だった場合、元の編成順で次にある未使用個体が中央になります。
        /// 後続が無ければ先頭へ循環します。残り0体なら中央はなくなります。
        ///
        /// 呼ぶのは「出撃が受理された後」だけです。
        /// 入力が不正だったときやDeployが失敗したときは呼びません。
        /// </summary>
        public bool Remove(string instanceId)
        {
            int index = IndexOf(instanceId);

            if (index < 0)
            {
                return false;
            }

            slots.RemoveAt(index);

            if (slots.Count == 0)
            {
                centerIndex = 0;
                return true;
            }

            if (index < centerIndex)
            {
                // 中央より前が消えたぶん、中央の位置が1つ手前へずれます。
                centerIndex--;
            }
            else if (index == centerIndex)
            {
                // 消えた位置に居る個体が「次の個体」です。
                // 末尾を消したときだけ先頭へ回り込みます。
                centerIndex = Wrap(index, slots.Count);
            }

            return true;
        }

        private int IndexOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return -1;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>負の値でも必ず 0..count-1 へ収める剰余。</summary>
        internal static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = value % count;

            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
