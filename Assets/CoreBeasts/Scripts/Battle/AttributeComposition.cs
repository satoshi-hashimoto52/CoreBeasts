using System;
using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1体ぶんの「色の組み合わせ」。単色か2色です。3色は作れません。
    ///
    /// 残存戦力の公開では、個体ではなく構成だけを見せます。
    /// 誰がいつ出るかは分からないまま、どの色が何体残っているかは読めます。
    ///
    /// 並びは正規化します（Red &lt; Green &lt; Blue）。
    /// 登録順の違いで別物に見えないようにするためです。
    /// </summary>
    public readonly struct AttributeComposition
        : IEquatable<AttributeComposition>, IComparable<AttributeComposition>
    {
        private AttributeComposition(UnitAttribute first, UnitAttribute second, bool dual)
        {
            First = first;
            Second = second;
            IsDual = dual;
        }

        /// <summary>正規化した1色目。</summary>
        public UnitAttribute First { get; }

        /// <summary>正規化した2色目。単色なら1色目と同じ値です。</summary>
        public UnitAttribute Second { get; }

        /// <summary>2色か。</summary>
        public bool IsDual { get; }

        /// <summary>持っている色の数。1か2です。</summary>
        public int Count => IsDual ? 2 : 1;

        /// <summary>単色を作ります。</summary>
        public static AttributeComposition Single(UnitAttribute attribute)
        {
            return new AttributeComposition(attribute, attribute, false);
        }

        /// <summary>2色を作ります。並びは正規化されます。</summary>
        public static AttributeComposition Dual(UnitAttribute a, UnitAttribute b)
        {
            if (a == b)
            {
                return Single(a);
            }

            return a < b
                ? new AttributeComposition(a, b, true)
                : new AttributeComposition(b, a, true);
        }

        /// <summary>個体から構成だけを取り出します。POWERもCOREも見ません。</summary>
        public static AttributeComposition Of(BattleUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            IReadOnlyList<AttributePower> powers = unit.AttributePowers;

            if (powers.Count >= 2)
            {
                return Dual(powers[0].Attribute, powers[1].Attribute);
            }

            return Single(powers[0].Attribute);
        }

        /// <summary>その色を含むか。</summary>
        public bool Contains(UnitAttribute attribute)
        {
            return First == attribute || (IsDual && Second == attribute);
        }

        public bool Equals(AttributeComposition other)
        {
            return First == other.First
                && Second == other.Second
                && IsDual == other.IsDual;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributeComposition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)First * 397) ^ ((int)Second * 31) ^ (IsDual ? 1 : 0);
        }

        /// <summary>単色を先、同数なら色の順。表示の並べ替えに使います。</summary>
        public int CompareTo(AttributeComposition other)
        {
            if (IsDual != other.IsDual)
            {
                return IsDual ? 1 : -1;
            }

            int byFirst = First.CompareTo(other.First);

            return byFirst != 0 ? byFirst : Second.CompareTo(other.Second);
        }

        public override string ToString()
        {
            return IsDual ? First + "/" + Second : First.ToString();
        }
    }
}
