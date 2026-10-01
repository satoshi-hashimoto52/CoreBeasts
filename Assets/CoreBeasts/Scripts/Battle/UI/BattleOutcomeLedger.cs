using System;
using System.Collections.Generic;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 解決済みラウンドを、PLAYER側の個体IDごとの結果として控える表示モデル。
    ///
    /// 勝敗は<see cref="RoundResult.Winner"/>をそのまま言い換えるだけで、
    /// 属性・POWER・スコアからの再計算は行いません。
    /// Unityへ依存しないため、通常のC#オブジェクトとしてそのままテストできます。
    ///
    /// 1個体は1マッチで1回しか出せないため、同じIDが二度記録されることはありません。
    /// それでも上書きを許すのは、同じラウンドを二重に渡されても
    /// 結果が食い違わないようにするためです。
    /// </summary>
    public sealed class BattleOutcomeLedger : IBattleSlotOutcomeSource
    {
        private readonly Dictionary<string, BattleSlotOutcome> outcomes =
            new Dictionary<string, BattleSlotOutcome>(StringComparer.Ordinal);

        /// <summary>控えている件数。</summary>
        public int Count => outcomes.Count;

        /// <summary>
        /// 1ラウンドぶんの結果を控えます。
        /// キーはPLAYER側が出した個体の<see cref="BattleUnit.InstanceId"/>です。
        /// CPU側は控えません。
        /// </summary>
        public bool Record(RoundResult result)
        {
            if (result == null || result.PlayerUnit == null)
            {
                return false;
            }

            string instanceId = result.PlayerUnit.InstanceId;

            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            outcomes[instanceId] = BattleSlotOutcomes.FromWinner(result.Winner);

            return true;
        }

        /// <summary>指定個体の結果。未戦闘・未知のIDなら None。</summary>
        public BattleSlotOutcome GetOutcome(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return BattleSlotOutcome.None;
            }

            return outcomes.TryGetValue(instanceId, out BattleSlotOutcome outcome)
                ? outcome
                : BattleSlotOutcome.None;
        }

        /// <summary>控えをすべて捨てます。REMATCHとマッチの作り直しで呼びます。</summary>
        public void Clear()
        {
            outcomes.Clear();
        }
    }
}
