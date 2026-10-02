using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ラウンド結果のバナーと、マッチ終了時の最終結果を出します。
    ///
    /// 勝敗の再判定はしません。<see cref="RoundResult"/>が持つ確定済みの
    /// 勝者・決着理由だけを文字へ写します。
    /// バナーは短時間だけ中央へ出し、画面全体を覆い続けません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleResultView : MonoBehaviour
    {
        [Header("Round banner")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text decisionLabel;
        [SerializeField] private TMP_Text winnerLabel;
        [SerializeField] private TMP_Text matchupLabel;
        [SerializeField] private TMP_Text bannerScoreLabel;

        [Header("Match result")]
        [SerializeField] private GameObject finalRoot;
        [SerializeField] private TMP_Text finalTitleLabel;
        [SerializeField] private TMP_Text finalScoreLabel;

        private IBattleTextSource battleText;
        private UiTextCatalog text;

        /// <summary>演出側がフェードに使うバナーのCanvasGroup。</summary>
        public CanvasGroup BannerGroup => bannerGroup;

        /// <summary>ラウンドバナーを出しているか。</summary>
        public bool IsBannerVisible =>
            bannerRoot != null && bannerRoot.activeSelf;

        /// <summary>最終結果を出しているか。</summary>
        public bool IsFinalVisible =>
            finalRoot != null && finalRoot.activeSelf;

        /// <summary>表示に使うデータを渡します。</summary>
        public void Bind(IBattleTextSource battleTextSource, UiTextCatalog uiText)
        {
            battleText = battleTextSource;
            text = uiText;

            HideAll();
            PrepareFallbackGlyphs();
        }

        /// <summary>
        /// 決着理由は英語（LiberationSans）と、ATTRIBUTE LINK の日本語（フォールバックの Noto Sans JP）が混ざります。
        /// TMP はフォールバックの文字を初めて描くときに子の SubMesh を作るため、試合中に GameObject を作らないよう、
        /// 画面へ出す前（Bind 時）に一度だけ日本語の理由でメッシュを作っておきます。文字と表示状態はすぐ元へ戻します。
        /// </summary>
        private void PrepareFallbackGlyphs()
        {
            if (decisionLabel == null || battleText == null)
            {
                return;
            }

            string sample = battleText.FormatLinkPowerDecision(
                battleText.FormatLinkedPower(0, AttributeLink.SecondLinkBonus),
                battleText.FormatUnlinkedPower(0));

            bool bannerWasActive = bannerRoot != null && bannerRoot.activeSelf;
            string previous = decisionLabel.text;

            SetActive(bannerRoot, true);

            decisionLabel.text = sample;
            decisionLabel.ForceMeshUpdate(true, true);
            decisionLabel.text = previous;
            decisionLabel.ForceMeshUpdate(true, true);

            SetActive(bannerRoot, bannerWasActive);
        }

        /// <summary>
        /// ラウンド結果を出します。
        /// 双方の選出・属性・POWER・決着理由・勝者・更新後スコアを含みます。
        /// </summary>
        public void ShowRound(
            RoundResult result,
            BattleUnitCard playerCard,
            BattleUnitCard cpuCard,
            int playerWins,
            int cpuWins)
        {
            if (result == null || battleText == null)
            {
                HideRound();
                return;
            }

            SetActive(bannerRoot, true);

            if (bannerGroup != null)
            {
                bannerGroup.alpha = 0f;
            }

            SetText(decisionLabel, BattleResultText.BuildDecision(result, battleText));
            SetText(
                winnerLabel,
                BattleResultText.BuildRoundWinner(result.Winner, battleText));

            SetText(
                matchupLabel,
                BuildSide(playerCard, result.PlayerUnit) +
                "   " + battleText.Versus + "   " +
                BuildSide(cpuCard, result.CpuUnit));

            SetText(bannerScoreLabel, battleText.FormatScore(playerWins, cpuWins));
        }

        /// <summary>ラウンドバナーを閉じます。</summary>
        public void HideRound()
        {
            if (bannerGroup != null)
            {
                bannerGroup.alpha = 0f;
            }

            SetActive(bannerRoot, false);
        }

        /// <summary>最終結果を出します。</summary>
        public void ShowFinal(BattleMatchState state, int playerWins, int cpuWins)
        {
            if (battleText == null)
            {
                return;
            }

            SetActive(finalRoot, true);

            SetText(
                finalTitleLabel,
                BattleResultText.BuildMatchResult(state, battleText));

            SetText(finalScoreLabel, battleText.FormatScore(playerWins, cpuWins));
        }

        /// <summary>最終結果を閉じます。</summary>
        public void HideFinal()
        {
            SetActive(finalRoot, false);
        }

        /// <summary>すべて閉じます。</summary>
        public void HideAll()
        {
            HideRound();
            HideFinal();
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleResultView),
                ReferenceCheck.Of(nameof(bannerRoot), bannerRoot),
                ReferenceCheck.Of(nameof(bannerGroup), bannerGroup),
                ReferenceCheck.Of(nameof(decisionLabel), decisionLabel),
                ReferenceCheck.Of(nameof(winnerLabel), winnerLabel),
                ReferenceCheck.Of(nameof(matchupLabel), matchupLabel),
                ReferenceCheck.Of(nameof(bannerScoreLabel), bannerScoreLabel),
                ReferenceCheck.Of(nameof(finalRoot), finalRoot),
                ReferenceCheck.Of(nameof(finalTitleLabel), finalTitleLabel),
                ReferenceCheck.Of(nameof(finalScoreLabel), finalScoreLabel));
        }

        /// <summary>
        /// 片側の要約。属性は頭文字で、POWERは中核が使った値をそのまま出します。
        /// 表示用データが引けない場合でも、中核の値だけで成立させます。
        /// </summary>
        private string BuildSide(BattleUnitCard card, BattleUnit unit)
        {
            if (unit == null)
            {
                return string.Empty;
            }

            string symbol = card != null && card.Definition != null && text != null
                ? text.BuildAttributeSymbol(card.Definition)
                : BuildSymbolFromUnit(unit);

            string name = card != null && card.Definition != null
                ? card.Definition.DisplayName
                : string.Empty;

            // 共通POWERは廃止したため、色別POWERを合計ではなく一行で出します。
            string summary = battleText.FormatUnitSummary(
                symbol,
                StatLinePresenter.BuildPlainPowerLine(unit.AttributePowers));

            return string.IsNullOrEmpty(name) ? summary : name + " " + summary;
        }

        /// <summary>表示用データが無いときの控えの属性表示。</summary>
        private string BuildSymbolFromUnit(BattleUnit unit)
        {
            if (text == null)
            {
                return string.Empty;
            }

            string primary = text.GetAttributeSymbol(unit.PrimaryAttribute);

            return unit.HasSecondaryAttribute
                ? primary + "/" + text.GetAttributeSymbol(unit.SecondaryAttribute)
                : primary;
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
