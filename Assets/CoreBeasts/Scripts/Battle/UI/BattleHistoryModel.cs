using System;
using System.Collections.Generic;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 使い終わった個体を、ラウンド順に並べて持つ表示モデル。
    ///
    /// リングから外れた個体はここへ移ります。戻ることはありません。
    /// Unityへ依存しないため、そのままテストできます。
    ///
    /// 追加できるのは「結果が公開された後」だけです。
    /// 戦闘中の個体はまだここへ入れません（戦闘エリアに居るためです）。
    /// </summary>
    public sealed class BattleHistoryModel
    {
        /// <summary>枠の上限。編成と同じ7です。</summary>
        public const int MaxEntries = BattleSquad.UnitCount;

        private readonly List<BattleHistoryEntry> entries =
            new List<BattleHistoryEntry>(MaxEntries);

        /// <summary>控えている件数。</summary>
        public int Count => entries.Count;

        /// <summary>ラウンド順の一覧。左から1戦目です。</summary>
        public IReadOnlyList<BattleHistoryEntry> Entries => entries;

        /// <summary>指定個体がすでに履歴へ入っているか。</summary>
        public bool Contains(string instanceId)
        {
            return IndexOf(instanceId) >= 0;
        }

        /// <summary>
        /// 1戦ぶんを末尾へ足します。
        /// 同じ個体を二度足すことはできません（1個体は1マッチで1回しか出せないためです）。
        /// </summary>
        public bool Append(string instanceId, int squadNumber, BattleSlotOutcome outcome)
        {
            if (string.IsNullOrEmpty(instanceId) ||
                outcome == BattleSlotOutcome.None ||
                entries.Count >= MaxEntries ||
                Contains(instanceId))
            {
                return false;
            }

            entries.Add(new BattleHistoryEntry(instanceId, squadNumber, outcome));

            return true;
        }

        /// <summary>指定個体の結果。未登録なら None。</summary>
        public BattleSlotOutcome OutcomeOf(string instanceId)
        {
            int index = IndexOf(instanceId);

            return index >= 0 ? entries[index].Outcome : BattleSlotOutcome.None;
        }

        /// <summary>すべて捨てます。REMATCHとマッチの作り直しで呼びます。</summary>
        public void Clear()
        {
            entries.Clear();
        }

        private int IndexOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(
                        entries[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
