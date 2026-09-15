using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 画面に出す文字列をまとめたアセット。
    /// コード側は内部enumと内部IDだけを扱い、表示文字列はここからのみ取得します。
    /// 言語を増やすときはこのアセットを複製して差し替えます
    /// （Localizationパッケージの導入は今回のスコープ外）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "UiTextCatalog_EN",
        menuName = "CoreBeasts/UI Text Catalog"
    )]
    public sealed class UiTextCatalog : ScriptableObject
    {
        [Header("Navigation")]
        [SerializeField] private string home = "HOME";
        [SerializeField] private string unitSet = "UNIT SET";
        [SerializeField] private string setNameFormat = "SET {0}";

        [Header("Sections")]
        [SerializeField] private string coreBeasts = "CORE BEASTS";
        [SerializeField] private string mySquad = "MY SQUAD";

        [Header("Save")]
        [SerializeField] private string saveSet = "SAVE SET";
        [SerializeField] private string savedFormat = "{0} SAVED";
        [SerializeField] private string removedFromSquad = "REMOVED FROM SQUAD";

        [Header("Stats")]
        [SerializeField] private string levelFormat = "Lv.{0}";
        [SerializeField] private string costFormat = "COST {0}";
        [SerializeField] private string powerFormat = "POWER {0}";
        [SerializeField] private string coreFormat = "CORE {0}";

        [Header("Attributes")]
        [SerializeField] private string attributeRed = "RED";
        [SerializeField] private string attributeGreen = "GREEN";
        [SerializeField] private string attributeBlue = "BLUE";
        [SerializeField] private string dualAttributeFormat = "{0} / {1}";

        [Header("Attribute initials (color-blind aid)")]
        [Tooltip("色に頼らず属性を見分けるための頭文字。TMPのフォントに含まれる文字にしてください。")]
        [SerializeField] private string symbolRed = "R";
        [SerializeField] private string symbolGreen = "G";
        [SerializeField] private string symbolBlue = "B";
        [SerializeField] private string dualSymbolFormat = "{0}/{1}";

        [Header("Slots")]
        [SerializeField] private string emptySlot = "+";
        [SerializeField] private string slotOrderFormat = "{0}";
        [SerializeField] private string noSelection = "-";

        public string Home => home;
        public string UnitSet => unitSet;
        public string CoreBeasts => coreBeasts;
        public string MySquad => mySquad;
        public string SaveSet => saveSet;
        public string RemovedFromSquad => removedFromSquad;
        public string EmptySlot => emptySlot;
        public string NoSelection => noSelection;

        public string FormatSetName(string setId)
        {
            return SafeFormat(setNameFormat, setId);
        }

        public string FormatSaved(string setName)
        {
            return SafeFormat(savedFormat, setName);
        }

        public string FormatLevel(int level)
        {
            return SafeFormat(levelFormat, level);
        }

        public string FormatCost(int cost)
        {
            return SafeFormat(costFormat, cost);
        }

        public string FormatPower(int power)
        {
            return SafeFormat(powerFormat, power);
        }

        public string FormatCore(int core)
        {
            return SafeFormat(coreFormat, core);
        }

        public string FormatSlotOrder(int oneBasedOrder)
        {
            return SafeFormat(slotOrderFormat, oneBasedOrder);
        }

        /// <summary>属性の表示名。色に依存せず読み取れるようにするためのものです。</summary>
        public string GetAttributeLabel(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return attributeRed;

                case UnitAttribute.Green:
                    return attributeGreen;

                case UnitAttribute.Blue:
                    return attributeBlue;

                default:
                    return attributeRed;
            }
        }

        /// <summary>属性の頭文字。色覚差があっても判別できるようにする補助表示です。</summary>
        public string GetAttributeSymbol(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return symbolRed;

                case UnitAttribute.Green:
                    return symbolGreen;

                case UnitAttribute.Blue:
                    return symbolBlue;

                default:
                    return symbolRed;
            }
        }

        /// <summary>
        /// 単属性/2属性をまとめた頭文字表示（例: R / R/B）。
        /// </summary>
        public string BuildAttributeSymbol(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return noSelection;
            }

            string primary = GetAttributeSymbol(definition.PrimaryAttribute);

            if (!definition.HasSecondaryAttribute)
            {
                return primary;
            }

            return SafeFormat(
                dualSymbolFormat,
                primary,
                GetAttributeSymbol(definition.SecondaryAttribute)
            );
        }

        /// <summary>単属性/2属性をまとめた表示ラベル（例: RED / RED / BLUE）。</summary>
        public string BuildAttributeLabel(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return noSelection;
            }

            string primary = GetAttributeLabel(definition.PrimaryAttribute);

            if (!definition.HasSecondaryAttribute)
            {
                return primary;
            }

            return SafeFormat(
                dualAttributeFormat,
                primary,
                GetAttributeLabel(definition.SecondaryAttribute)
            );
        }

        /// <summary>書式が壊れていても例外を出さずに素の値を返します。</summary>
        private static string SafeFormat(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return args != null && args.Length > 0 && args[0] != null
                    ? args[0].ToString()
                    : string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (System.FormatException)
            {
                return format;
            }
        }
    }
}
