using System.Collections.Generic;
using System.Globalization;
using System.Text;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// バトル中核ロジックが1個体について必要とする値だけを持つ不変データ。
    /// 表示名、立ち絵、レベルなどの提示用情報は持ちません。
    ///
    /// POWERは色ごとに持ちます。「このユニットのPOWER」という
    /// 1つの数字はもう存在しません。どの色で比べるのかが決まらないまま
    /// 数字だけを比べてしまうことを、型の側で防ぐためです。
    ///
    /// 色は1〜2色です。3色ユニットは実装しません。
    /// 妥当性の検査は<see cref="Validate"/>と編成側（<see cref="BattleSquad"/>）が行います。
    /// </summary>
    public sealed class BattleUnit
    {
        private readonly AttributePower[] attributePowers;

        /// <summary>単色の個体を作ります。</summary>
        public BattleUnit(string instanceId, UnitAttribute attribute, int power, int core = 0)
            : this(instanceId, new[] { new AttributePower(attribute, power) }, core)
        {
        }

        /// <summary>2色の個体を作ります。登録順が主属性・副属性です。</summary>
        public BattleUnit(
            string instanceId,
            UnitAttribute primaryAttribute,
            int primaryPower,
            UnitAttribute secondaryAttribute,
            int secondaryPower,
            int core = 0)
            : this(
                instanceId,
                new[]
                {
                    new AttributePower(primaryAttribute, primaryPower),
                    new AttributePower(secondaryAttribute, secondaryPower),
                },
                core)
        {
        }

        /// <summary>色別POWERをそのまま受け取ります。並び順は登録順として保ちます。</summary>
        public BattleUnit(
            string instanceId,
            IReadOnlyList<AttributePower> attributePowers,
            int core = 0)
        {
            InstanceId = instanceId ?? string.Empty;
            Core = core;

            int count = attributePowers != null ? attributePowers.Count : 0;

            this.attributePowers = new AttributePower[count];

            for (int i = 0; i < count; i++)
            {
                this.attributePowers[i] = attributePowers[i];
            }
        }

        /// <summary>所持個体の内部ID。対戦中の同一性判定に使います。</summary>
        public string InstanceId { get; }

        /// <summary>CORE。POWERでも決着しなかったときの比較値です。</summary>
        public int Core { get; }

        /// <summary>色別POWER。登録順（主属性が先）です。</summary>
        public IReadOnlyList<AttributePower> AttributePowers => attributePowers;

        /// <summary>持っている色の数。1か2です。</summary>
        public int AttributeCount => attributePowers.Length;

        /// <summary>2色か。</summary>
        public bool IsDual => attributePowers.Length == 2;

        /// <summary>主属性（登録順の1つ目）。</summary>
        public UnitAttribute PrimaryAttribute =>
            attributePowers.Length > 0
                ? attributePowers[0].Attribute
                : UnitAttribute.Red;

        /// <summary>副属性を持つか。</summary>
        public bool HasSecondaryAttribute => attributePowers.Length > 1;

        /// <summary>副属性。単色なら主属性と同じ値を返します。</summary>
        public UnitAttribute SecondaryAttribute =>
            attributePowers.Length > 1
                ? attributePowers[1].Attribute
                : PrimaryAttribute;

        /// <summary>
        /// 色別POWERの単純な合計。
        ///
        /// これは「このユニットのPOWER」ではありません。
        /// 平均の比較を割り算せずに行うための途中式です
        /// （2色の平均どうしなら合計のまま、2色と単色なら単色側を2倍して比べます）。
        ///
        /// 単一の戦闘POWERとして誤用されないよう、
        /// <see cref="BattleRules"/>と同じアセンブリからだけ見えるようにしています。
        /// 画面へ出す値が要るときは<see cref="PowerOf"/>か
        /// <c>StatLinePresenter</c>を使ってください。
        /// </summary>
        internal int SumOfAttributePower
        {
            get
            {
                int total = 0;

                for (int i = 0; i < attributePowers.Length; i++)
                {
                    total += attributePowers[i].Power;
                }

                return total;
            }
        }

        /// <summary>
        /// 指定色のPOWER。その色を持たない場合は0を返します。
        /// 呼ぶ前に<see cref="HasAttribute"/>で持っていることを確かめてください。
        /// </summary>
        public int PowerOf(UnitAttribute attribute)
        {
            for (int i = 0; i < attributePowers.Length; i++)
            {
                if (attributePowers[i].Attribute == attribute)
                {
                    return attributePowers[i].Power;
                }
            }

            return 0;
        }

        /// <summary>
        /// 指定属性を持つか。
        /// 勝敗判定は属性を集合として扱うため、登録順で結果は変わりません。
        /// </summary>
        public bool HasAttribute(UnitAttribute attribute)
        {
            for (int i = 0; i < attributePowers.Length; i++)
            {
                if (attributePowers[i].Attribute == attribute)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 全色POWERへ同じ規則を掛けた個体を返します。
        /// 色を指定しない「POWERを上げる」効果は、持っている色すべてへ同じように効きます。
        /// </summary>
        public BattleUnit WithScaledPower(float multiplier)
        {
            AttributePower[] scaled = new AttributePower[attributePowers.Length];

            for (int i = 0; i < attributePowers.Length; i++)
            {
                int value = (int)(attributePowers[i].Power * multiplier + 0.5f);

                scaled[i] = attributePowers[i].WithPower(value < 1 ? 1 : value);
            }

            return new BattleUnit(InstanceId, scaled, Core);
        }

        /// <summary>指定色のPOWERだけを差し替えた個体を返します。</summary>
        public BattleUnit WithPowerOf(UnitAttribute attribute, int power)
        {
            AttributePower[] changed = new AttributePower[attributePowers.Length];

            for (int i = 0; i < attributePowers.Length; i++)
            {
                changed[i] = attributePowers[i].Attribute == attribute
                    ? attributePowers[i].WithPower(power)
                    : attributePowers[i];
            }

            return new BattleUnit(InstanceId, changed, Core);
        }

        /// <summary>個体単体としての妥当性。問題がなければ<see cref="BattleError.None"/>。</summary>
        public BattleError Validate()
        {
            if (string.IsNullOrEmpty(InstanceId))
            {
                return BattleError.EmptyInstanceId;
            }

            switch (AttributeLoadout.Validate(attributePowers))
            {
                case AttributeLoadoutError.None:
                    break;

                case AttributeLoadoutError.NonPositivePower:
                    return BattleError.NegativePower;

                case AttributeLoadoutError.UndefinedAttribute:
                    return BattleError.InvalidAttribute;

                default:
                    return BattleError.InvalidAttributeLoadout;
            }

            if (Core < 0)
            {
                return BattleError.NegativeCore;
            }

            return BattleError.None;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(InstanceId).Append('(');

            for (int i = 0; i < attributePowers.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('/');
                }

                builder
                    .Append(attributePowers[i].Attribute)
                    .Append(' ')
                    .Append(attributePowers[i].Power.ToString(CultureInfo.InvariantCulture));
            }

            return builder
                .Append(" CORE ")
                .Append(Core.ToString(CultureInfo.InvariantCulture))
                .Append(')')
                .ToString();
        }
    }
}
