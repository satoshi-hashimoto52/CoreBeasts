using System;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1陣営・1ラウンドぶんの ATTRIBUTE LINK の結果（Phase 4）。不変データです。
    ///
    /// <see cref="ChainCount"/>は連続して属性がつながった個体数で、最初の個体は1です。
    /// <see cref="BonusPower"/>は、そのラウンドだけ各属性POWERへ足す値です（元のPOWERは書き換えません）。
    /// </summary>
    public readonly struct AttributeLinkResult : IEquatable<AttributeLinkResult>
    {
        public AttributeLinkResult(int chainCount, int bonusPower, int sharedAttributeMask)
        {
            ChainCount = chainCount < 1 ? 1 : chainCount;
            BonusPower = bonusPower < 0 ? 0 : bonusPower;
            SharedAttributeMask = sharedAttributeMask;
        }

        /// <summary>つながっていない状態（チェーン1・ボーナス0）。最初の個体もこれです。</summary>
        public static AttributeLinkResult None => new AttributeLinkResult(1, 0, 0);

        /// <summary>連続してつながった個体数。最初の個体・途切れた直後は1です。4以上もそのまま保持します。</summary>
        public int ChainCount { get; }

        /// <summary>各属性POWERへ足す値。チェーン2で+3、3以上で+6（上限）。</summary>
        public int BonusPower { get; }

        /// <summary>直前の自分の個体と共有した属性のビット集合（1 &lt;&lt; (int)UnitAttribute）。</summary>
        public int SharedAttributeMask { get; }

        /// <summary>LINKが成立しているか（チェーン2以上）。</summary>
        public bool IsActive => ChainCount >= 2;

        /// <summary>共有した属性の数。2色が両方一致しても、チェーンの加算は1回です。</summary>
        public int SharedAttributeCount => AttributeLink.CountBits(SharedAttributeMask);

        /// <summary>その属性を直前の個体と共有したか。</summary>
        public bool Shares(UnitAttribute attribute)
        {
            int bit = AttributeLink.BitOf(attribute);

            return bit != 0 && (SharedAttributeMask & bit) != 0;
        }

        public bool Equals(AttributeLinkResult other)
        {
            return ChainCount == other.ChainCount &&
                   BonusPower == other.BonusPower &&
                   SharedAttributeMask == other.SharedAttributeMask;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributeLinkResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (ChainCount * 397 ^ BonusPower) * 397 ^ SharedAttributeMask;
        }

        public override string ToString()
        {
            return "LINK x" + ChainCount + " +" + BonusPower + " mask=" + SharedAttributeMask;
        }
    }

    /// <summary>
    /// ATTRIBUTE LINK の判定（Phase 4）。状態を持たない純粋関数で、Unityに依存しません。
    ///
    /// 規則:
    ///   - PLAYER と CPU で別々に、同じ規則を使います（自分の履歴だけを見ます）
    ///   - 最初に出した個体はチェーン1・ボーナス0
    ///   - 直前に自分が出した個体と属性を1つ以上共有すれば、チェーン+1
    ///   - 共有しなければチェーン1へ戻します
    ///   - 勝ち・負け・引き分けはチェーンに影響しません
    ///   - 2属性は集合として扱います（R/B と B/R は同じ）。2色とも一致しても加算は1回です
    ///   - チェーン2で各属性POWERへ+3、3以上で+6（上限）
    ///
    /// POWERは<see cref="Apply"/>で作る一時的な個体にだけ反映し、元の個体は書き換えません。
    /// 属性相性で決まる勝敗はPOWERを見ないため、LINKで変わりません。
    /// </summary>
    public static class AttributeLink
    {
        /// <summary>チェーン2のボーナス。</summary>
        public const int SecondLinkBonus = 3;

        /// <summary>チェーン3以上のボーナス（上限）。</summary>
        public const int MaxLinkBonus = 6;

        /// <summary>チェーン数に対するボーナス。1以下は0です。</summary>
        public static int BonusFor(int chainCount)
        {
            if (chainCount >= 3)
            {
                return MaxLinkBonus;
            }

            return chainCount == 2 ? SecondLinkBonus : 0;
        }

        /// <summary>
        /// 直前の自分の個体（無ければ null）と今回の個体から、今回のLINKを求めます。
        /// <paramref name="previousChainCount"/>は直前の個体のチェーン数です。
        /// </summary>
        public static AttributeLinkResult Evaluate(BattleUnit previous, BattleUnit current, int previousChainCount)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (previous == null)
            {
                return AttributeLinkResult.None;
            }

            int shared = MaskOf(previous) & MaskOf(current);

            if (shared == 0)
            {
                return AttributeLinkResult.None;
            }

            int chain = (previousChainCount < 1 ? 1 : previousChainCount) + 1;

            return new AttributeLinkResult(chain, BonusFor(chain), shared);
        }

        /// <summary>
        /// LINKを反映した一時的な個体を返します。各属性POWERへ<see cref="AttributeLinkResult.BonusPower"/>を足します。
        /// ボーナスが0なら、元の個体をそのまま返します。元の個体は書き換えません。
        /// </summary>
        public static BattleUnit Apply(BattleUnit unit, AttributeLinkResult link)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (link.BonusPower <= 0)
            {
                return unit;
            }

            AttributePower[] boosted = new AttributePower[unit.AttributePowers.Count];

            for (int i = 0; i < boosted.Length; i++)
            {
                AttributePower original = unit.AttributePowers[i];

                boosted[i] = original.WithPower(original.Power + link.BonusPower);
            }

            return new BattleUnit(unit.InstanceId, boosted, unit.Core, unit.Skill);
        }

        /// <summary>個体が持つ属性のビット集合。範囲外の値は含めません（一致扱いにしません）。</summary>
        public static int MaskOf(BattleUnit unit)
        {
            if (unit == null)
            {
                return 0;
            }

            int mask = 0;

            for (int i = 0; i < unit.AttributePowers.Count; i++)
            {
                mask |= BitOf(unit.AttributePowers[i].Attribute);
            }

            return mask;
        }

        /// <summary>属性1つのビット。Red・Green・Blue 以外は0です。</summary>
        public static int BitOf(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                case UnitAttribute.Green:
                case UnitAttribute.Blue:
                    return 1 << (int)attribute;

                default:
                    return 0;
            }
        }

        internal static int CountBits(int mask)
        {
            int count = 0;

            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
        }
    }
}
