using System;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// <see cref="System.Random"/>による乱数源。
    /// 同じseedからは同じ並びを返すため、対戦の再現に使えます。
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;

        /// <summary>seedを指定して作ります。同じseedなら同じ並びになります。</summary>
        public SystemRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMax),
                    "1以上を指定してください。");
            }

            return random.Next(exclusiveMax);
        }
    }
}
