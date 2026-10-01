using System;
using System.Collections.Generic;

using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 「どの色に、どれだけのPOWERがあるか」を1組で持つ値。
    ///
    /// 色とPOWERを別々の配列に分けると、件数のずれや並び順の食い違いが
    /// 静かに混入します。ここで1組にしておけば、その種類の不整合は起こりません。
    /// </summary>
    [Serializable]
    public struct AttributePower : IEquatable<AttributePower>
    {
        [SerializeField] private UnitAttribute attribute;

        [SerializeField] [Min(1)] private int power;

        public AttributePower(UnitAttribute attribute, int power)
        {
            this.attribute = attribute;
            this.power = power;
        }

        /// <summary>この行の色。</summary>
        public UnitAttribute Attribute => attribute;

        /// <summary>その色のPOWER。1以上です。</summary>
        public int Power => power;

        /// <summary>POWERだけを差し替えた同じ色の行。補正を掛けるときに使います。</summary>
        public AttributePower WithPower(int value)
        {
            return new AttributePower(attribute, value);
        }

        public bool Equals(AttributePower other)
        {
            return attribute == other.attribute && power == other.power;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributePower other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)attribute * 397) ^ power;
        }

        public override string ToString()
        {
            return attribute + ":" + power;
        }
    }

    /// <summary>属性POWER構成の不備。</summary>
    public enum AttributeLoadoutError
    {
        None = 0,

        /// <summary>1色も無い。</summary>
        Empty = 1,

        /// <summary>3色以上。通常ユニットは1〜2色までです。</summary>
        TooManyAttributes = 2,

        /// <summary>同じ色が2回出ている。</summary>
        DuplicateAttribute = 3,

        /// <summary>POWERが0以下。</summary>
        NonPositivePower = 4,

        /// <summary>定義されていない色。</summary>
        UndefinedAttribute = 5,
    }

    /// <summary>
    /// 属性POWER構成の検査。製品仕様として 1〜2色に限ります。
    ///
    /// 不正なデータを黙って切り捨てたり、先頭2件だけ使ったりはしません。
    /// どこが悪いのかを呼び出し側へ返し、気付ける形にします。
    /// </summary>
    public static class AttributeLoadout
    {
        /// <summary>1ユニットが持てる色の下限。</summary>
        public const int MinAttributes = 1;

        /// <summary>1ユニットが持てる色の上限。3色ユニットは実装しません。</summary>
        public const int MaxAttributes = 2;

        /// <summary>構成が仕様どおりか。問題が無ければ<see cref="AttributeLoadoutError.None"/>。</summary>
        public static AttributeLoadoutError Validate(IReadOnlyList<AttributePower> loadout)
        {
            if (loadout == null || loadout.Count < MinAttributes)
            {
                return AttributeLoadoutError.Empty;
            }

            if (loadout.Count > MaxAttributes)
            {
                return AttributeLoadoutError.TooManyAttributes;
            }

            for (int i = 0; i < loadout.Count; i++)
            {
                if (!IsDefined(loadout[i].Attribute))
                {
                    return AttributeLoadoutError.UndefinedAttribute;
                }

                if (loadout[i].Power <= 0)
                {
                    return AttributeLoadoutError.NonPositivePower;
                }

                for (int j = i + 1; j < loadout.Count; j++)
                {
                    if (loadout[i].Attribute == loadout[j].Attribute)
                    {
                        return AttributeLoadoutError.DuplicateAttribute;
                    }
                }
            }

            return AttributeLoadoutError.None;
        }

        /// <summary>赤・緑・青のいずれかか。</summary>
        public static bool IsDefined(UnitAttribute attribute)
        {
            return attribute == UnitAttribute.Red
                || attribute == UnitAttribute.Green
                || attribute == UnitAttribute.Blue;
        }
    }
}
