using System.Collections.Generic;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 決まった値を順に返す乱数源。CPUの選出をテストで固定するために使います。
    /// 値を使い切ったあとは最後の値を返し続けます。
    /// </summary>
    internal sealed class FakeRandomSource : IRandomSource
    {
        private readonly List<int> values;

        private int cursor;

        internal FakeRandomSource(params int[] values)
        {
            this.values = new List<int>(values);
        }

        /// <summary>NextIntが呼ばれた回数。</summary>
        internal int CallCount { get; private set; }

        /// <summary>最後に渡された排他上限。候補数が正しく渡っているかの確認に使います。</summary>
        internal int LastExclusiveMax { get; private set; } = -1;

        public int NextInt(int exclusiveMax)
        {
            CallCount++;
            LastExclusiveMax = exclusiveMax;

            if (values.Count == 0)
            {
                return 0;
            }

            int index = cursor < values.Count ? cursor : values.Count - 1;
            cursor++;

            return values[index];
        }
    }
}
