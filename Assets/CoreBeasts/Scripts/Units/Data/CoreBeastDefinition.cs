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

        [Header("Attributes")]
        [SerializeField]
        private UnitAttribute primaryAttribute = UnitAttribute.Red;

        [SerializeField]
        private bool hasSecondaryAttribute;

        [SerializeField]
        private UnitAttribute secondaryAttribute = UnitAttribute.Blue;

        [Header("Stats")]
        [SerializeField]
        [Min(0)]
        private int cost = 20;

        [SerializeField]
        [Min(0)]
        private int power = 50;

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

        public UnitAttribute PrimaryAttribute => primaryAttribute;

        public bool HasSecondaryAttribute => hasSecondaryAttribute;

        public UnitAttribute SecondaryAttribute => secondaryAttribute;

        public int Cost => cost;

        public int Power => power;

        public int Core => core;

        public string SkillName => skillName;

        public string SkillDescription => skillDescription;

        public Sprite Thumbnail => thumbnail;
    }
}
