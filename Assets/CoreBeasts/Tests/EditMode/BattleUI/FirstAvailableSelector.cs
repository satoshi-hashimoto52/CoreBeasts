using System.Collections.Generic;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 候補の先頭を選ぶだけの決定論的な選択器。
    /// 受け取った候補も記録するため、プレイヤーの選択が渡っていないことを確かめられます。
    /// </summary>
    internal sealed class FirstAvailableSelector : IBattleUnitSelector
    {
        internal List<int> ObservedCandidateCounts { get; } = new List<int>();

        internal List<BattleUnit> Chosen { get; } = new List<BattleUnit>();

        public BattleUnit Select(IReadOnlyList<BattleUnit> availableUnits)
        {
            if (availableUnits == null || availableUnits.Count == 0)
            {
                return null;
            }

            ObservedCandidateCounts.Add(availableUnits.Count);
            Chosen.Add(availableUnits[0]);

            return availableUnits[0];
        }
    }
}
