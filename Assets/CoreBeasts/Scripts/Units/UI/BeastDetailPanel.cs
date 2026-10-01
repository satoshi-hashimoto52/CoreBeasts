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

        [Header("Chips and icons")]
        [SerializeField] private Image attributeChip;

        [Tooltip("COREの小さなアイコン。文字ではなくGraphicで出します。")]
        [SerializeField] private Graphic coreIcon;

        [Header("Stat row")]
        [Tooltip("ステータス行の左端（詳細カードのローカル座標）。")]
        [SerializeField] private float statRowLeft = 348f;

        [Tooltip("属性POWERの群と CORE の群のあいだ。")]
        [SerializeField] [Range(20f, 48f)] private float powerToCoreGap = 28f;

        [Tooltip("COREアイコンとCORE数値のあいだ。")]
        [SerializeField] [Range(6f, 20f)] private float iconToValueGap = 10f;

        [Tooltip("COREアイコンの大きさ。")]
        [SerializeField] [Range(18f, 48f)] private float coreIconSize = 42f;

        [Tooltip("カードの内側に残す左右の余白。ここより外へは出しません。")]
        [SerializeField] [Range(4f, 48f)] private float statRowPadding = 24f;

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
            // POWER / CORE の常設ラベルは使いません。数値だけを色付きで出します。
            SetText(
                powerLabel,
                StatLinePresenter.BuildPowerLine(definition.AttributePowers, palette));

            SetText(coreLabel, StatLinePresenter.BuildCoreValue(definition.Core));
            ShowCoreIcon(true);

            // 固定座標ではなく、TMPの実測幅で詰めます。
            // バトル画面と同じ共通処理を使い、画面ごとに複製しません。
            // 位置はカードの矩形を基準にし、はみ出しはクランプで防ぎます。
            RectTransform card = transform as RectTransform;

            StatLinePresenter.LayoutRow(
                powerLabel,
                coreIcon,
                coreLabel,
                card,
                statRowLeft,
                statRowPadding,
                powerToCoreGap,
                iconToValueGap,
                coreIconSize);
            SetText(skillNameLabel, definition.SkillName);
            SetText(skillDescriptionLabel, definition.SkillDescription);

            // 記号と名前の重複（R/B RED / BLUE）はやめ、名前だけを出します。
            SetText(attributeLabel, text.BuildAttributeLabel(definition));

            if (attributeChip != null && palette != null)
            {
                attributeChip.enabled = true;
                attributeChip.color =
                    palette.GetColors(definition.PrimaryAttribute).PrimaryColor;
            }

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
            ShowCoreIcon(false);
            SetText(skillNameLabel, placeholder);
            SetText(skillDescriptionLabel, string.Empty);

            if (attributeChip != null)
            {
                attributeChip.enabled = false;
            }

        }

        /// <summary>COREアイコンの出し入れ。装飾なので入力は取りません。</summary>
        private void ShowCoreIcon(bool visible)
        {
            if (coreIcon == null)
            {
                return;
            }

            coreIcon.enabled = visible;
            coreIcon.raycastTarget = false;
            coreIcon.color = StatLinePresenter.CoreIconColor;
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
