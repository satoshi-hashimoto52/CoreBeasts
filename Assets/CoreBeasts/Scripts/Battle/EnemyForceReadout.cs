using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 敵の残存戦力として画面へ渡してよい情報。
    ///
    /// ここに入っていないものは、画面からは知りようがありません。
    /// 個体ID、名前、POWER、CORE、レベル、スキル、CPUの選択順、
    /// そして「次に出る個体」は含めません。
    ///
    /// 構成の並びは正規化した順に並べ替えてあり、編成順ではありません。
    /// そのため、並びの位置から次の個体を逆算することはできません。
    /// </summary>
    public sealed class EnemyForceReadout
    {
        private static readonly ReadOnlyCollection<AttributeComposition> None =
            new ReadOnlyCollection<AttributeComposition>(new AttributeComposition[0]);

        internal EnemyForceReadout(
            EnemyForceDisclosure policy,
            int totalSlots,
            int usedCount,
            IReadOnlyList<AttributeComposition> compositions,
            int red,
            int green,
            int blue,
            int dual)
        {
            Policy = policy;
            TotalSlots = totalSlots;
            UsedCount = usedCount;
            Compositions = compositions ?? None;
            RedHolders = red;
            GreenHolders = green;
            BlueHolders = blue;
            DualCount = dual;
        }

        /// <summary>どこまで見せる設定で作られたか。</summary>
        public EnemyForceDisclosure Policy { get; }

        /// <summary>枠の総数。7です。</summary>
        public int TotalSlots { get; }

        /// <summary>使い終わった数。</summary>
        public int UsedCount { get; }

        /// <summary>残っている数。どの方針でも分かります。</summary>
        public int RemainingCount => TotalSlots - UsedCount;

        /// <summary>
        /// 残っている構成。<see cref="EnemyForceDisclosure.FullComposition"/>のときだけ入ります。
        /// 順序を持たないプールとして扱ってください。
        /// </summary>
        public IReadOnlyList<AttributeComposition> Compositions { get; }

        /// <summary>REDを持つ残存数。Masked では0です。</summary>
        public int RedHolders { get; }

        /// <summary>GREENを持つ残存数。Masked では0です。</summary>
        public int GreenHolders { get; }

        /// <summary>BLUEを持つ残存数。Masked では0です。</summary>
        public int BlueHolders { get; }

        /// <summary>2色の残存数。Masked では0です。</summary>
        public int DualCount { get; }

        /// <summary>色ごとの内訳を見せているか。</summary>
        public bool ShowsAttributeCounts => Policy != EnemyForceDisclosure.Masked;

        /// <summary>1体ずつの構成を見せているか。</summary>
        public bool ShowsEachComposition => Policy == EnemyForceDisclosure.FullComposition;

        /// <summary>その色を持つ残存数。Masked では常に0です。</summary>
        public int HoldersOf(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return RedHolders;

                case UnitAttribute.Green:
                    return GreenHolders;

                default:
                    return BlueHolders;
            }
        }
    }

    /// <summary>
    /// 残存戦力の読み取りを組み立てます。状態を持たない純粋な変換です。
    ///
    /// 将来の解析スキル用に、次の上書きを受け取れる形にしてあります。
    ///   ・指定した構成を1件だけ公開する
    ///   ・指定した色の残存数だけ公開する
    /// スキルそのものと操作UIはここでは実装しません。
    /// </summary>
    public static class EnemyForceModel
    {
        /// <summary>
        /// 残っている個体から、方針に応じた読み取りを作ります。
        /// </summary>
        /// <param name="remaining">まだ使っていない個体。</param>
        /// <param name="totalSlots">枠の総数。</param>
        /// <param name="policy">どこまで見せるか。</param>
        /// <param name="revealCompositions">
        /// Masked や Aggregate でも、この件数だけは構成を公開します（解析スキル用）。
        /// </param>
        /// <param name="revealAttributeCounts">
        /// Masked でも、この色の残存数だけは公開します（解析スキル用）。
        /// </param>
        public static EnemyForceReadout Build(
            IReadOnlyList<BattleUnit> remaining,
            int totalSlots,
            EnemyForceDisclosure policy,
            int revealCompositions = 0,
            IReadOnlyList<UnitAttribute> revealAttributeCounts = null)
        {
            if (remaining == null)
            {
                throw new ArgumentNullException(nameof(remaining));
            }

            if (totalSlots < remaining.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalSlots), totalSlots, "残存数より枠が少なくなっています。");
            }

            List<AttributeComposition> pool = new List<AttributeComposition>(remaining.Count);

            int red = 0, green = 0, blue = 0, dual = 0;

            for (int i = 0; i < remaining.Count; i++)
            {
                AttributeComposition composition = AttributeComposition.Of(remaining[i]);

                pool.Add(composition);

                if (composition.Contains(UnitAttribute.Red)) red++;
                if (composition.Contains(UnitAttribute.Green)) green++;
                if (composition.Contains(UnitAttribute.Blue)) blue++;
                if (composition.IsDual) dual++;
            }

            // 編成順を消します。ここを省くと、並びの位置から次の個体を推測できてしまいます。
            pool.Sort();

            int used = totalSlots - remaining.Count;

            switch (policy)
            {
                case EnemyForceDisclosure.FullComposition:
                    return new EnemyForceReadout(
                        policy, totalSlots, used, pool.AsReadOnly(), red, green, blue, dual);

                case EnemyForceDisclosure.AggregateCounts:
                    return new EnemyForceReadout(
                        policy, totalSlots, used,
                        Peek(pool, revealCompositions),
                        red, green, blue, dual);

                default:
                    return new EnemyForceReadout(
                        policy, totalSlots, used,
                        Peek(pool, revealCompositions),
                        CountIfRevealed(revealAttributeCounts, UnitAttribute.Red, red),
                        CountIfRevealed(revealAttributeCounts, UnitAttribute.Green, green),
                        CountIfRevealed(revealAttributeCounts, UnitAttribute.Blue, blue),
                        0);
            }
        }

        /// <summary>解析スキルで公開する分だけを、正規化順の先頭から取り出します。</summary>
        private static IReadOnlyList<AttributeComposition> Peek(
            List<AttributeComposition> pool, int count)
        {
            if (count <= 0)
            {
                return null;
            }

            int take = count < pool.Count ? count : pool.Count;

            return pool.GetRange(0, take).AsReadOnly();
        }

        private static int CountIfRevealed(
            IReadOnlyList<UnitAttribute> revealed, UnitAttribute attribute, int value)
        {
            if (revealed == null)
            {
                return 0;
            }

            for (int i = 0; i < revealed.Count; i++)
            {
                if (revealed[i] == attribute)
                {
                    return value;
                }
            }

            return 0;
        }
    }
}
