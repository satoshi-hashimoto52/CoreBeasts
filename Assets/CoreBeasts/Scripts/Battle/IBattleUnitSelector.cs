using System.Collections.Generic;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1ラウンドに出す個体を選ぶ役。CPU側の選出はこの境界を通します。
    ///
    /// 受け取るのは自分の未使用候補だけです。
    /// 相手の今回の選択は引数に現れないため、
    /// 実装が相手の手を見て選ぶことは構造的にできません（同時・非公開選出）。
    /// </summary>
    public interface IBattleUnitSelector
    {
        /// <summary>
        /// 候補から1体を選びます。候補が無い場合は null を返してください。
        /// </summary>
        BattleUnit Select(IReadOnlyList<BattleUnit> availableUnits);
    }
}
