using System.Globalization;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// バトル中核ロジックが1個体について必要とする値だけを持つ不変データ。
    /// 表示名、立ち絵、レベルなどの提示用情報は持ちません。
    ///
    /// 妥当性の検査は編成側（<see cref="BattleSquad"/>）へ集約しています。
    /// ここは値の入れ物であり、どの個体が対戦へ持ち込めるかは編成が決めます。
    /// </summary>
    public sealed class BattleUnit
    {
        /// <summary>単属性の個体を作ります。</summary>
        public BattleUnit(string instanceId, UnitAttribute primaryAttribute, int power)
        {
            InstanceId = instanceId ?? string.Empty;
            PrimaryAttribute = primaryAttribute;
            HasSecondaryAttribute = false;
            SecondaryAttribute = primaryAttribute;
            Power = power;
        }

        /// <summary>2属性の個体を作ります。</summary>
        public BattleUnit(
            string instanceId,
            UnitAttribute primaryAttribute,
            UnitAttribute secondaryAttribute,
            int power)
        {
            InstanceId = instanceId ?? string.Empty;
            PrimaryAttribute = primaryAttribute;
            HasSecondaryAttribute = true;
            SecondaryAttribute = secondaryAttribute;
            Power = power;
        }

        /// <summary>所持個体の内部ID。対戦中の同一性判定に使います。</summary>
        public string InstanceId { get; }

        /// <summary>一次属性。</summary>
        public UnitAttribute PrimaryAttribute { get; }

        /// <summary>二次属性を持つか。</summary>
        public bool HasSecondaryAttribute { get; }

        /// <summary>二次属性。単属性の個体では一次属性と同じ値になります。</summary>
        public UnitAttribute SecondaryAttribute { get; }

        /// <summary>POWER。属性相性で決着しないときの比較値です。</summary>
        public int Power { get; }

        /// <summary>
        /// 指定属性を持つか。
        /// 勝敗判定は属性を集合として扱うため、一次・二次の並び順で結果は変わりません。
        /// </summary>
        public bool HasAttribute(UnitAttribute attribute)
        {
            if (PrimaryAttribute == attribute)
            {
                return true;
            }

            return HasSecondaryAttribute && SecondaryAttribute == attribute;
        }

        /// <summary>個体単体としての妥当性。問題がなければ<see cref="BattleError.None"/>。</summary>
        public BattleError Validate()
        {
            if (string.IsNullOrEmpty(InstanceId))
            {
                return BattleError.EmptyInstanceId;
            }

            if (!IsDefinedAttribute(PrimaryAttribute))
            {
                return BattleError.InvalidAttribute;
            }

            if (HasSecondaryAttribute && !IsDefinedAttribute(SecondaryAttribute))
            {
                return BattleError.InvalidAttribute;
            }

            if (Power < 0)
            {
                return BattleError.NegativePower;
            }

            return BattleError.None;
        }

        private static bool IsDefinedAttribute(UnitAttribute attribute)
        {
            return attribute == UnitAttribute.Red
                || attribute == UnitAttribute.Green
                || attribute == UnitAttribute.Blue;
        }

        public override string ToString()
        {
            string attributes = HasSecondaryAttribute
                ? PrimaryAttribute + "/" + SecondaryAttribute
                : PrimaryAttribute.ToString();

            return string.Concat(
                InstanceId,
                "(",
                attributes,
                " POWER ",
                Power.ToString(CultureInfo.InvariantCulture),
                ")");
        }
    }
}
