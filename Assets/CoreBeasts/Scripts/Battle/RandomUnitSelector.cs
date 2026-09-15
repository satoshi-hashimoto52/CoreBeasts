using System;
using System.Collections.Generic;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 初期CPUの選出。戦略を持たず、未使用候補から一様に1体を選びます。
    /// 乱数源を注入するため、テストでは結果を固定できます。
    /// </summary>
    public sealed class RandomUnitSelector : IBattleUnitSelector
    {
        private readonly IRandomSource randomSource;

        public RandomUnitSelector(IRandomSource randomSource)
        {
            this.randomSource = randomSource
                ?? throw new ArgumentNullException(nameof(randomSource));
        }

        /// <summary>
        /// 候補から1体を選びます。候補が空、または null の場合は null を返します。
        /// 乱数源が範囲外の値を返しても、候補の範囲へ収めてから参照します。
        /// </summary>
        public BattleUnit Select(IReadOnlyList<BattleUnit> availableUnits)
        {
            if (availableUnits == null || availableUnits.Count == 0)
            {
                return null;
            }

            int index = randomSource.NextInt(availableUnits.Count);

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= availableUnits.Count)
            {
                index = availableUnits.Count - 1;
            }

            return availableUnits[index];
        }
    }
}
