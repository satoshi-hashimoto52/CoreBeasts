using System.Collections.Generic;

using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// Core Beast 1種の不変データ。
    /// 内部ID(<see cref="BeastId"/>)と表示名(<see cref="DisplayName"/>)を分離しており、
    /// ゲームロジックは表示文字列に依存しません。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CB_NewBeast",
        menuName = "CoreBeasts/Core Beast Definition"
    )]
    public sealed class CoreBeastDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("内部ID。表示には使いません。")]
        private string beastId = "beast";

        [SerializeField]
        [Tooltip("画面表示名。ローカライズ対象。")]
        private string displayName = "BEAST";

        [Header("Attributes and power")]
        [Tooltip("色とPOWERの組。1〜2件。並び順が主属性・副属性です。")]
        [SerializeField]
        private List<AttributePower> attributePowers = new List<AttributePower>();

        [Header("Legacy (migration only)")]
        [Tooltip("旧データ。attributePowers が空のときだけ読み替えに使います。")]
        [SerializeField]
        private UnitAttribute primaryAttribute = UnitAttribute.Red;

        [SerializeField]
        private bool hasSecondaryAttribute;

        [SerializeField]
        private UnitAttribute secondaryAttribute = UnitAttribute.Blue;

        [Tooltip("旧データの共通POWER。戦闘judgeは参照しません。")]
        [SerializeField]
        [Min(0)]
        private int power = 50;

        [Header("Stats")]
        [SerializeField]
        [Min(0)]
        private int cost = 20;

        [SerializeField]
        [Min(0)]
        private int core = 50;

        [Header("Skill")]
        [SerializeField]
        private string skillName = "SKILL";

        [SerializeField]
        [TextArea(2, 3)]
        [Tooltip("1〜2行の短い説明。長文にしないでください。")]
        private string skillDescription = string.Empty;

        [Header("Art")]
        [SerializeField]
        [Tooltip("一覧・枠に出す小さな立ち絵")]
        private Sprite thumbnail;

        public string BeastId => beastId;

        public string DisplayName => displayName;

        /// <summary>
        /// 色別POWER。登録順（主属性が先）です。これが正です。
        ///
        /// 旧データ（<c>attributePowers</c>が空）のアセットでも動くよう、
        /// そのときだけ旧フィールドから読み替えた1〜2件を返します。
        /// 読み替えは表示と変換のためのもので、
        /// 戦闘judgeは常にこの一覧だけを見ます。
        /// </summary>
        public IReadOnlyList<AttributePower> AttributePowers =>
            attributePowers != null && attributePowers.Count > 0
                ? attributePowers
                : BuildFromLegacy();

        /// <summary>主属性（登録順の1つ目）。</summary>
        public UnitAttribute PrimaryAttribute => AttributePowers[0].Attribute;

        /// <summary>副属性を持つか。</summary>
        public bool HasSecondaryAttribute => AttributePowers.Count > 1;

        /// <summary>副属性。単色なら主属性と同じ値を返します。</summary>
        public UnitAttribute SecondaryAttribute =>
            AttributePowers.Count > 1 ? AttributePowers[1].Attribute : PrimaryAttribute;

        /// <summary>指定色のPOWER。持っていなければ0。</summary>
        public int PowerOf(UnitAttribute attribute)
        {
            IReadOnlyList<AttributePower> powers = AttributePowers;

            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i].Attribute == attribute)
                {
                    return powers[i].Power;
                }
            }

            return 0;
        }

        /// <summary>構成が仕様（1〜2色・重複なし・POWER正）を満たすか。</summary>
        public AttributeLoadoutError ValidateAttributePowers()
        {
            return AttributeLoadout.Validate(AttributePowers);
        }

        public int Cost => cost;

        public int Core => core;

        /// <summary>旧データからの読み替え。移行前のアセットを開けるようにするためだけのものです。</summary>
        private List<AttributePower> BuildFromLegacy()
        {
            List<AttributePower> fallback = new List<AttributePower>(2)
            {
                new AttributePower(primaryAttribute, power < 1 ? 1 : power),
            };

            if (hasSecondaryAttribute)
            {
                fallback.Add(new AttributePower(secondaryAttribute, power < 1 ? 1 : power));
            }

            return fallback;
        }

        public string SkillName => skillName;

        public string SkillDescription => skillDescription;

        public Sprite Thumbnail => thumbnail;
    }
}
