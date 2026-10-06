using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 出場中の1体を出す表示。立ち絵と情報パネルを持ちます。
    ///
    /// CPU側とプレイヤー側で同じコンポーネントを使い、
    /// 左右の配置と立ち絵の向きだけをシーン側で反転させます（ミラー構成）。
    /// 反転は立ち絵のRectTransformにだけ掛け、テキストへは掛けません。
    ///
    /// 着色は既存の<see cref="BeastThumbnailView"/>へ委ねます。
    /// PNG・マスク・シェーダ・マテリアルは触りません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleCombatantView : MonoBehaviour
    {
        [Header("Portrait")]
        [SerializeField] private RectTransform portraitRoot;
        [SerializeField] private CanvasGroup portraitGroup;
        [SerializeField] private BeastThumbnailView thumbnail;

        [Header("Hidden state")]
        [Tooltip("解決前のCPU側に出す非公開表示。個体は分かりません。")]
        [SerializeField] private GameObject hiddenRoot;
        [SerializeField] private TMP_Text hiddenLabel;

        [Header("Info panel")]
        [SerializeField] private GameObject infoRoot;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text powerLabel;
        [SerializeField] private TMP_Text coreLabel;
        [SerializeField] private TMP_Text skillNameLabel;
        [SerializeField] private TMP_Text skillDescriptionLabel;

        [Tooltip("ATTRIBUTE LINK のボーナス表示（LINK +3 / LINK +6）。基礎POWERの表示とは別に出します。")]
        [SerializeField] private TMP_Text linkLabel;

        [Tooltip("ユニークスキルの発動・予告の表示（Phase 5）。任意。")]
        [SerializeField] private TMP_Text skillLabel;
        [SerializeField] private Image attributeChip;

        [Tooltip("COREの小さなアイコン。文字ではなくGraphicで出します。")]
        [SerializeField] private Graphic coreIcon;

        [Header("Stat row")]
        [Tooltip("属性POWERの群と CORE の群のあいだ。")]
        [SerializeField] [Range(24f, 40f)] private float powerToCoreGap = 30f;

        [Tooltip("COREアイコンとCORE数値のあいだ。")]
        [SerializeField] [Range(6f, 14f)] private float iconToValueGap = 8f;

        [Tooltip("COREアイコンの大きさ。")]
        [SerializeField] [Range(24f, 44f)] private float coreIconSize = 34f;

        [Tooltip("Info の内側に残す左右の余白。ここより外へは出しません。")]
        [SerializeField] [Range(4f, 40f)] private float statRowPadding = 8f;

        private AttributePalette palette;
        private UiTextCatalog text;
        private IBattleTextSource battleText;

        /// <summary>演出が動かす立ち絵のRectTransform。</summary>
        public RectTransform PortraitTransform => portraitRoot;

        /// <summary>演出が明暗を付けるCanvasGroup。</summary>
        public CanvasGroup PortraitGroup => portraitGroup;

        /// <summary>今表示している個体。非公開・未選択なら null。</summary>
        public BattleUnitCard Current { get; private set; }

        /// <summary>情報パネルを出しているか。非公開表示のときは false。</summary>
        public bool IsRevealed { get; private set; }

        /// <summary>直近に適用した一次属性色。演出のフラッシュ色に使います。</summary>
        public Color PrimaryColor { get; private set; } = Color.white;

        /// <summary>このラウンドで敗れた側として、グレー化しているか。</summary>
        public bool IsDefeated { get; private set; }

        /// <summary>
        /// 片付けの薄まり具合（0=通常, 1=完全に消えた）。
        ///
        /// 戦闘が終わった個体は、履歴の位置へ動かしません。
        /// いま居る場所のままここで薄くして消します。
        /// </summary>
        public float FadeOut { get; private set; }

        /// <summary>
        /// 現在位置のまま薄くします。Transformは一切動かしません。
        /// 履歴レーンへ飛ばさないのは、この方式のためです。
        /// </summary>
        public void SetFadeOut(float amount)
        {
            FadeOut = Mathf.Clamp01(amount);

            if (portraitGroup != null)
            {
                portraitGroup.alpha = 1f - FadeOut;
            }

            if (infoRoot != null)
            {
                SetActive(infoRoot, FadeOut < 0.999f && IsRevealed);
            }
        }

        /// <summary>
        /// 敗北表示を切り替えます。勝敗の判定はここでは行わず、
        /// <see cref="BattleFlowCoordinator"/>が公開した結果を写すだけです。
        ///
        /// 立ち絵の属性レイヤーをニュートラルグレーへ置き換えます。
        /// 情報パネルの文字は読める明るさのまま残すため、
        /// CanvasGroupの一括減光は使いません。
        /// </summary>
        public void SetDefeated(bool defeated)
        {
            IsDefeated = defeated;

            if (thumbnail != null)
            {
                thumbnail.SetDefeated(defeated);
            }
        }

        /// <summary>表示に使うデータを渡します。</summary>
        public void Bind(
            AttributePalette attributePalette,
            UiTextCatalog uiText,
            IBattleTextSource battleTextSource)
        {
            palette = attributePalette;
            text = uiText;
            battleText = battleTextSource;

            PrepareFallbackGlyphs();
        }

        /// <summary>
        /// スキルの説明文は日本語で、LiberationSans のフォールバック（Noto Sans JP サブセット）から描きます。
        /// TMP はフォールバックの文字を初めて描くときに子の SubMesh を作るため、試合中に GameObject を作らないよう、
        /// 画面へ出す前（Bind 時）に一度だけ日本語でメッシュを作っておきます。文字と表示状態はすぐ元へ戻します。
        /// </summary>
        private void PrepareFallbackGlyphs()
        {
            if (skillDescriptionLabel == null || battleText == null)
            {
                return;
            }

            bool infoWasActive = infoRoot != null && infoRoot.activeSelf;
            string previous = skillDescriptionLabel.text;

            SetActive(infoRoot, true);

            skillDescriptionLabel.text = battleText.FormatSkillOpponentPenalty(UniqueSkill.TidalHowlPenalty);
            skillDescriptionLabel.ForceMeshUpdate(true, true);
            skillDescriptionLabel.text = previous;
            skillDescriptionLabel.ForceMeshUpdate(true, true);

            SetActive(infoRoot, infoWasActive);
        }

        /// <summary>いま表示している ATTRIBUTE LINK。表示していなければ <see cref="AttributeLinkResult.None"/>。</summary>
        public AttributeLinkResult ShownLink { get; private set; } = AttributeLinkResult.None;

        /// <summary>LINK 表示のラベル（確認・テスト用）。</summary>
        public TMP_Text LinkLabel => linkLabel;

        /// <summary>
        /// ATTRIBUTE LINK のボーナスを、基礎POWERとは別の表示で出します。
        /// チェーン1（LINK 不成立）では何も出しません（LOST なども出しません）。
        /// 2属性で2色とも共有していても、ボーナス表記は1回だけです。
        /// </summary>
        public void ShowLink(AttributeLinkResult link)
        {
            ShownLink = link.IsActive ? link : AttributeLinkResult.None;

            if (linkLabel == null)
            {
                return;
            }

            bool visible = link.IsActive && link.BonusPower > 0 && battleText != null;

            linkLabel.text = visible ? battleText.FormatLinkBonus(link.BonusPower) : string.Empty;
            SetActive(linkLabel.gameObject, visible);
        }

        /// <summary>LINK 表示を消します。</summary>
        public void ClearLink()
        {
            ShowLink(AttributeLinkResult.None);
        }

        /// <summary>いま表示しているユニークスキル（戦闘中の発動）。予告中・非表示なら <see cref="UniqueSkillActivation.None"/>。</summary>
        public UniqueSkillActivation ShownSkill { get; private set; } = UniqueSkillActivation.None;

        /// <summary>いま表示している選択前予告。</summary>
        public UniqueSkillPreview ShownSkillPreview { get; private set; } = UniqueSkillPreview.None;

        /// <summary>スキル表示のラベル（確認・テスト用）。</summary>
        public TMP_Text SkillLabel => skillLabel;

        /// <summary>
        /// 戦闘中のユニークスキルの発動を、基礎POWERとは別の表示で出します。発動しなければ何も出しません。
        /// </summary>
        public void ShowSkill(UniqueSkillActivation skill)
        {
            ShownSkillPreview = UniqueSkillPreview.None;
            ShownSkill = skill.Activated ? skill : UniqueSkillActivation.None;

            SetSkillText(skill.Activated ? skill.SelfBonus : 0, skill.Activated ? skill.OpponentPenalty : 0);
        }

        /// <summary>
        /// 選択前予告。自分の履歴だけで発動が決まるときだけ出します（相手の選出で決まるものは出しません）。
        /// </summary>
        public void ShowSkillPreview(UniqueSkillPreview preview)
        {
            ShownSkill = UniqueSkillActivation.None;
            ShownSkillPreview = preview.Activates ? preview : UniqueSkillPreview.None;

            SetSkillText(preview.Activates ? preview.SelfBonus : 0, preview.Activates ? preview.OpponentPenalty : 0);
        }

        /// <summary>スキル表示を消します。</summary>
        public void ClearSkill()
        {
            ShownSkill = UniqueSkillActivation.None;
            ShownSkillPreview = UniqueSkillPreview.None;

            SetSkillText(0, 0);
        }

        private void SetSkillText(int selfBonus, int opponentPenalty)
        {
            if (skillLabel == null)
            {
                return;
            }

            string value = string.Empty;

            if (battleText != null)
            {
                if (selfBonus > 0)
                {
                    value = battleText.FormatSkillSelfBonus(selfBonus);
                }
                else if (opponentPenalty > 0)
                {
                    value = battleText.FormatSkillOpponentPenalty(opponentPenalty);
                }
            }

            skillLabel.text = value;
            SetActive(skillLabel.gameObject, value.Length > 0);
        }

        /// <summary>
        /// 個体を伏せたまま「選出済み」だけを示します。
        /// 名前・属性・POWERのいずれも出しません。
        /// </summary>
        public void ShowHidden()
        {
            ClearLink();
            ClearSkill();
            Current = null;
            IsRevealed = false;
            PrimaryColor = Color.white;
            IsDefeated = false;
            FadeOut = 0f;

            SetActive(hiddenRoot, true);
            SetActive(infoRoot, false);

            ClearDecisionHighlight();
            ShowCoreIcon(false);

            if (thumbnail != null)
            {
                thumbnail.Clear();
            }

            if (hiddenLabel != null && battleText != null)
            {
                hiddenLabel.text = battleText.Ready;
            }

            if (portraitGroup != null)
            {
                portraitGroup.alpha = 1f;
            }
        }

        /// <summary>まだ何も出していない状態にします。</summary>
        public void ShowEmpty()
        {
            ClearLink();
            ClearSkill();
            Current = null;
            IsRevealed = false;
            PrimaryColor = Color.white;
            IsDefeated = false;
            FadeOut = 0f;

            SetActive(hiddenRoot, true);
            SetActive(infoRoot, false);

            ClearDecisionHighlight();
            ShowCoreIcon(false);

            if (thumbnail != null)
            {
                thumbnail.Clear();
            }

            if (hiddenLabel != null && battleText != null)
            {
                hiddenLabel.text = battleText.Hidden;
            }

            if (portraitGroup != null)
            {
                portraitGroup.alpha = 1f;
            }
        }

        /// <summary>個体を公開します。立ち絵・属性・POWER・CORE・スキルを出します。</summary>
        public void Show(BattleUnitCard card)
        {
            if (card == null || card.Definition == null)
            {
                ShowEmpty();
                return;
            }

            Current = card;
            IsRevealed = true;

            // 前の個体の LINK 表示は持ち越しません。画面が今回の LINK を渡し直します。
            ClearLink();
            ClearSkill();

            // 新しい個体を出す時点では、前ラウンドの敗北表示と薄まりを持ち越しません。
            IsDefeated = false;
            FadeOut = 0f;

            CoreBeastDefinition definition = card.Definition;

            SetActive(hiddenRoot, false);
            SetActive(infoRoot, true);

            if (thumbnail != null)
            {
                thumbnail.Show(definition, palette);
            }

            if (palette != null)
            {
                PrimaryColor = palette
                    .GetColors(definition.PrimaryAttribute)
                    .PrimaryColor;
            }

            SetText(nameLabel, definition.DisplayName);
            SetText(skillNameLabel, definition.SkillName);
            SetText(skillDescriptionLabel, definition.SkillDescription);

            if (text != null)
            {
                SetText(levelLabel, text.FormatLevel(card.Level));

                // POWER / CORE の常設ラベルは使いません。
                // 文字列づくりと色はステータス画面と同じ処理を通します。
                SetText(
                    powerLabel,
                    StatLinePresenter.BuildPowerLine(definition.AttributePowers, palette));

                SetText(coreLabel, StatLinePresenter.BuildCoreValue(definition.Core));
                ShowCoreIcon(true);

                // 固定座標で並べるのではなく、TMPの実測幅で詰めます。
                // 単色でも2色でも、同じ見た目の間隔になります。
                LayoutStatRow();
                // 記号と名前の重複（R/B RED / BLUE）はやめ、名前だけを出します。
                // 色だけに頼らず読めるよう、文字は残します。
                SetText(attributeLabel, text.BuildAttributeLabel(definition));
            }

            if (attributeChip != null)
            {
                attributeChip.enabled = true;
                attributeChip.color = PrimaryColor;
            }

            if (portraitGroup != null)
            {
                portraitGroup.alpha = 1f;
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleCombatantView),
                ReferenceCheck.Of(nameof(portraitRoot), portraitRoot),
                ReferenceCheck.Of(nameof(portraitGroup), portraitGroup),
                ReferenceCheck.Of(nameof(thumbnail), thumbnail),
                ReferenceCheck.Of(nameof(hiddenRoot), hiddenRoot),
                ReferenceCheck.Of(nameof(hiddenLabel), hiddenLabel),
                ReferenceCheck.Of(nameof(infoRoot), infoRoot),
                ReferenceCheck.Of(nameof(nameLabel), nameLabel),
                ReferenceCheck.Of(nameof(levelLabel), levelLabel),
                ReferenceCheck.Of(nameof(attributeLabel), attributeLabel),
                ReferenceCheck.Of(nameof(powerLabel), powerLabel),
                ReferenceCheck.Of(nameof(coreLabel), coreLabel),
                ReferenceCheck.Of(nameof(skillNameLabel), skillNameLabel),
                ReferenceCheck.Of(nameof(skillDescriptionLabel), skillDescriptionLabel),
                ReferenceCheck.Of(nameof(attributeChip), attributeChip));
        }

        /// <summary>
        /// 属性POWERとCOREを1つのステータス行としてまとめ、HUDの中へ収めます。
        ///
        /// 位置は必ず Info の矩形を基準にします。行が長い2色表示でも、
        /// 左端が Info の外へ出て先頭の「40 /」が切れることがありません。
        /// 並べ方はステータス画面と同じ共通処理を使います。
        /// </summary>
        /// <summary>
        /// ステータス行を今の文字から組み直します。
        /// 文字や書体が外から変わったときに、呼び出し側から掛け直せるようにしています。
        /// </summary>
        public void RefreshStatRow()
        {
            LayoutStatRow();
        }

        private void LayoutStatRow()
        {
            if (powerLabel == null || coreLabel == null)
            {
                return;
            }

            RectTransform info = infoRoot != null
                ? infoRoot.transform as RectTransform
                : powerLabel.rectTransform.parent as RectTransform;

            if (info == null)
            {
                return;
            }

            StatLinePresenter.LayoutRow(
                powerLabel,
                coreIcon,
                coreLabel,
                info,
                float.NaN,          // Info の中央へ寄せます。
                statRowPadding,
                powerToCoreGap,
                iconToValueGap,
                coreIconSize);
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

        /// <summary>
        /// 判定に使われた部分を一時的に強調します。
        ///
        /// 強調は「見た目だけ」です。勝敗はここでは決めません。
        /// <see cref="ClearDecisionHighlight"/>で必ず通常へ戻します。
        /// </summary>
        public void HighlightDecision(RoundDecision decision, Color accent)
        {
            ClearDecisionHighlight();

            switch (decision)
            {
                case RoundDecision.AttributeAdvantage:
                    if (attributeChip != null)
                    {
                        attributeChip.color = accent;
                    }

                    break;

                case RoundDecision.PowerComparison:
                    if (powerLabel != null)
                    {
                        powerLabel.fontStyle |= FontStyles.Bold;
                    }

                    break;

                case RoundDecision.CoreComparison:
                    if (coreLabel != null)
                    {
                        coreLabel.fontStyle |= FontStyles.Bold;
                    }

                    if (coreIcon != null)
                    {
                        coreIcon.color = accent;
                    }

                    break;
            }
        }

        /// <summary>強調を解きます。FX OFF でも REMATCH でも必ず通常へ戻します。</summary>
        public void ClearDecisionHighlight()
        {
            if (powerLabel != null)
            {
                powerLabel.fontStyle &= ~FontStyles.Bold;
            }

            if (coreLabel != null)
            {
                coreLabel.fontStyle &= ~FontStyles.Bold;
            }

            if (coreIcon != null)
            {
                coreIcon.color = StatLinePresenter.CoreIconColor;
            }

            if (attributeChip != null)
            {
                attributeChip.color = PrimaryColor;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
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
