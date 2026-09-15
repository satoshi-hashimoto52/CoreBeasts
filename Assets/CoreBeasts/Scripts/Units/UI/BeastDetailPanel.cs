using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 選択中Core Beastの詳細表示。
    /// 立ち絵は既存の<see cref="CoreBeastView"/>(SpriteRenderer)を
    /// RenderTexture経由でUIへ出すため、属性カラーの仕組みを重複実装していません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeastDetailPanel : MonoBehaviour
    {
        [Header("Portrait")]
        [Tooltip("RenderTextureへ描く既存のCoreBeastView")]
        [SerializeField] private CoreBeastView portraitView;
        [SerializeField] private GameObject portraitRoot;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text powerLabel;
        [SerializeField] private TMP_Text coreLabel;
        [SerializeField] private TMP_Text skillNameLabel;
        [SerializeField] private TMP_Text skillDescriptionLabel;

        [Header("Gauges and chips")]
        [SerializeField] private Image attributeChip;
        [Tooltip("Image Type を Filled / Horizontal にしてください")]
        [SerializeField] private Image powerGauge;
        [SerializeField] private Image coreGauge;

        [Header("Config")]
        [Tooltip("ゲージを満タンとみなす値。数値表示と併記するため目安で構いません。")]
        [SerializeField] [Min(1)] private int gaugeMaxValue = 120;

        [SerializeField] private AttributePalette palette;
        [SerializeField] private UiTextCatalog text;

        /// <summary>1個体の詳細を表示します。</summary>
        public void Show(OwnedCoreBeast beast)
        {
            if (beast == null || !beast.IsValid || text == null)
            {
                ShowEmpty();
                return;
            }

            CoreBeastDefinition definition = beast.Definition;

            if (portraitRoot != null)
            {
                portraitRoot.SetActive(true);
            }

            if (portraitView != null)
            {
                if (definition.HasSecondaryAttribute)
                {
                    portraitView.SetAttributes(
                        definition.PrimaryAttribute,
                        definition.SecondaryAttribute
                    );
                }
                else
                {
                    portraitView.SetAttribute(definition.PrimaryAttribute);
                }
            }

            SetText(nameLabel, definition.DisplayName);
            SetText(levelLabel, text.FormatLevel(beast.Level));
            SetText(costLabel, text.FormatCost(definition.Cost));
            SetText(powerLabel, text.FormatPower(definition.Power));
            SetText(coreLabel, text.FormatCore(definition.Core));
            SetText(skillNameLabel, definition.SkillName);
            SetText(skillDescriptionLabel, definition.SkillDescription);

            SetText(
                attributeLabel,
                text.BuildAttributeSymbol(definition) + "  " +
                text.BuildAttributeLabel(definition)
            );

            if (attributeChip != null && palette != null)
            {
                attributeChip.enabled = true;
                attributeChip.color =
                    palette.GetColors(definition.PrimaryAttribute).PrimaryColor;
            }

            SetGauge(powerGauge, definition.Power);
            SetGauge(coreGauge, definition.Core);
        }

        /// <summary>未選択状態の表示。</summary>
        public void ShowEmpty()
        {
            string placeholder = text != null ? text.NoSelection : "-";

            if (portraitRoot != null)
            {
                portraitRoot.SetActive(false);
            }

            SetText(nameLabel, placeholder);
            SetText(levelLabel, placeholder);
            SetText(costLabel, placeholder);
            SetText(attributeLabel, placeholder);
            SetText(powerLabel, placeholder);
            SetText(coreLabel, placeholder);
            SetText(skillNameLabel, placeholder);
            SetText(skillDescriptionLabel, string.Empty);

            if (attributeChip != null)
            {
                attributeChip.enabled = false;
            }

            SetGauge(powerGauge, 0);
            SetGauge(coreGauge, 0);
        }

        private void SetGauge(Image gauge, int value)
        {
            if (gauge == null)
            {
                return;
            }

            gauge.fillAmount = Mathf.Clamp01((float)value / gaugeMaxValue);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
