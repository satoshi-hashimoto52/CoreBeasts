using System.Collections.Generic;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 渡された候補を記録しつつ、指定した規則で1体を返すテスト用の選択器。
    /// セッションが候補として何を渡したかを検証できます。
    /// </summary>
    internal sealed class RecordingUnitSelector : IBattleUnitSelector
    {
        private readonly System.Func<IReadOnlyList<BattleUnit>, BattleUnit> pick;

        internal RecordingUnitSelector(
            System.Func<IReadOnlyList<BattleUnit>, BattleUnit> pick)
        {
            this.pick = pick;
        }

        /// <summary>各呼び出しで渡された候補のID一覧。</summary>
        internal List<string[]> ReceivedCandidateIds { get; } = new List<string[]>();

        /// <summary>先頭の候補を選ぶ選択器。</summary>
        internal static RecordingUnitSelector First()
        {
            return new RecordingUnitSelector(candidates => candidates[0]);
        }

        public BattleUnit Select(IReadOnlyList<BattleUnit> availableUnits)
        {
            string[] ids = new string[availableUnits.Count];

            for (int i = 0; i < availableUnits.Count; i++)
            {
                ids[i] = availableUnits[i].InstanceId;
            }

            ReceivedCandidateIds.Add(ids);

            return pick(availableUnits);
        }
    }
}
