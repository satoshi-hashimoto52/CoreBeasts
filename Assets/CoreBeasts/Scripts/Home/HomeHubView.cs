using System.Collections.Generic;
using System.Text;

using CoreBeasts.HomeUI;
using CoreBeasts.Progression;
using CoreBeasts.Shared.UI;
using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CoreBeasts.Home
{
    /// <summary>
    /// Home シーン上の拠点・報酬・ガチャ・獲得・コレクション画面（Phase 6 で導入、Phase 7 でゲーム画面として再構成）。
    ///
    /// - 画面はシーンの読み込み時に一度だけ組み立て、以後は表示の切り替えと文字の更新だけを行います
    ///   （画面を開くたびに GameObject・Material・List を増やしません）
    /// - 画面遷移は <see cref="HomeFlow"/> が決め、主要な画面は常に1つだけ表示します
    /// - ガチャの結果は <see cref="GachaSummonSession"/> が開始時に一度だけ確定・保存し、演出は見た目だけです
    /// - 背景（ShipBackdrop）を生かすため、各画面は半透明のパネルで構成します
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeHubView : MonoBehaviour
    {
        public const string SquadSetId = "1";

        // 構造色（青系）・白金色（重要情報と報酬）。赤・青・緑は属性の表示だけに使います。
        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.06f, 0.35f);
        private static readonly Color PanelFill = new Color(0.04f, 0.07f, 0.12f, 0.88f);
        private static readonly Color PanelEdge = new Color(0.24f, 0.40f, 0.58f, 0.95f);
        private static readonly Color Cyan = ShipPalette.ColdCyanBright;
        private static readonly Color Platinum = new Color(1f, 0.88f, 0.60f, 1f);
        private static readonly Color PlatinumDeep = new Color(0.86f, 0.70f, 0.38f, 0.96f);
        private static readonly Color TextMain = new Color(0.94f, 0.96f, 1f, 1f);
        private static readonly Color TextSub = new Color(0.62f, 0.74f, 0.90f, 1f);
        private static readonly Color TextOnPrimary = new Color(0.06f, 0.07f, 0.10f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.68f, 0.78f, 1f);

        private TMP_FontAsset font;
        private CoreBeastRoster catalog;
        private AttributePalette palette;
        private GachaSummonSession session;
        private UnityAction battleAction;
        private UnityAction unitSetAction;

        private readonly HomeFlow flow = new HomeFlow();
        private readonly Dictionary<HomePage, GameObject> pages = new Dictionary<HomePage, GameObject>();

        private GameObject homePage;
        private GameObject rewardPage;
        private GameObject gachaPage;
        private GameObject acquisitionPage;
        private GameObject collectionPage;
        private GameObject settingsOverlay;

        // Home
        private TMP_Text coinValue;
        private TMP_Text squadValue;
        private TMP_Text ownedValue;
        private TMP_Text recordValue;
        private TMP_Text fxLabel;

        // Reward
        private TMP_Text rewardOutcome;
        private TMP_Text rewardBreakdown;
        private TMP_Text rewardEarned;
        private TMP_Text rewardBefore;
        private TMP_Text rewardAfter;
        private TMP_Text rewardReason;
        private Button rewardGachaButton;

        // Gacha
        private TMP_Text gachaCoinValue;
        private TMP_Text gachaCostValue;
        private TMP_Text gachaPoolValue;
        private TMP_Text gachaStatus;
        private TMP_Text gachaReason;
        private Button pullButton;
        private Button gachaBackButton;
        private GameObject inputBlocker;
        private GachaSummonView summonView;
        private GachaSummonGraphic summonGraphic;

        // Acquisition
        private TMP_Text acquisitionTitle;
        private TMP_Text acquisitionName;
        private TMP_Text acquisitionLevel;
        private TMP_Text acquisitionPower;
        private TMP_Text acquisitionCore;
        private TMP_Text acquisitionSkillName;
        private TMP_Text acquisitionSkillText;
        private TMP_Text acquisitionCopiesBefore;
        private TMP_Text acquisitionCopiesAfter;
        private TMP_Text acquisitionOwned;
        private BeastThumbnailView acquisitionPortrait;
        private GameObject acquisitionUnitSetButton;

        // Collection
        private TMP_Text collectionLabel;

        /// <summary>画面遷移の状態（確認・テスト用）。</summary>
        public HomeFlow Flow => flow;

        /// <summary>直近に表示した報酬の要約。</summary>
        public RewardSummary LastReward { get; private set; }

        /// <summary>直近に表示した獲得の要約。</summary>
        public AcquisitionSummary LastAcquisition { get; private set; }

        /// <summary>
        /// 画面を一度だけ組み立てます。<paramref name="portraitTextures"/>は立ち絵の3層
        /// （下地・一次属性マスク・二次属性マスク）で、UnitSet・Battle の縮小立ち絵と同じ素材と配色規則を使います。
        /// </summary>
        public void Build(
            RectTransform host,
            TMP_FontAsset textFont,
            CoreBeastRoster beastCatalog,
            AttributePalette attributePalette,
            Texture[] portraitTextures,
            GachaSummonSession summonSession,
            UnityAction onBattle,
            UnityAction onUnitSet)
        {
            font = textFont;
            catalog = beastCatalog;
            palette = attributePalette;
            session = summonSession;
            battleAction = onBattle;
            unitSetAction = onUnitSet;

            HideLegacyContent(host);

            RectTransform root = CreateRect("PlayableFlow", host);
            Stretch(root);
            Image rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = Dim;
            rootImage.raycastTarget = true;
            root.SetAsLastSibling();

            BuildHome(root);
            BuildReward(root);
            BuildGacha(root);
            BuildAcquisition(root, portraitTextures);
            BuildCollection(root);
            BuildSettings(root);

            pages[HomePage.Home] = homePage;
            pages[HomePage.Reward] = rewardPage;
            pages[HomePage.Gacha] = gachaPage;
            pages[HomePage.Summoning] = gachaPage;
            pages[HomePage.Acquisition] = acquisitionPage;
            pages[HomePage.Collection] = collectionPage;

            PrepareFallbackGlyphs();
        }

        private void OnEnable()
        {
            // 演出の途中で無効になった後は Home から再開します（中断時に遷移は Home へ戻しています）。
            if (homePage != null)
            {
                ShowOnly(flow.Current);
            }
        }

        private void OnDisable()
        {
            // 演出の途中で画面が無効になっても、入力を閉じたまま・演出を残したままにしません。
            // 結果は開始時に保存済みなので、コインと所持はそのまま正しく残ります。
            if (summonView != null)
            {
                summonView.ResetVisuals();
            }

            session?.Finish();
            flow.Abort();
            SetInputBlocked(false);
        }

        /// <summary>シーンを読み込んだときの最初の画面。未確認の報酬があるときだけ報酬画面を一度だけ出します。</summary>
        public void ShowInitial()
        {
            PlayerProfile profile = Profile();

            if (GameFlowState.HasPendingReward)
            {
                ShowReward(GameFlowState.ConsumePendingSummary(profile != null ? profile.Coins : 0));
                return;
            }

            ShowHome();
        }

        // ---------------- Home ----------------

        public void ShowHome()
        {
            if (flow.Current != HomePage.Home && !flow.TryGo(HomePage.Home))
            {
                return;
            }

            ShowOnly(HomePage.Home);

            // 戻るたびにプロフィールと編成から読み直します。
            HomeSummary summary = HomeSummary.Build(Profile(), catalog, SquadSetId);

            coinValue.text = summary.CoinText;
            squadValue.text = summary.SquadText;
            ownedValue.text = HomeText.Number(summary.OwnedCount) + " / " + HomeText.Number(summary.CatalogCount);
            recordValue.text = summary.RecordText;
        }

        private void BuildHome(RectTransform root)
        {
            homePage = CreatePage("HomePage", root);

            Text(homePage.transform, "TitleLabel", HomeText.Title, 76, Platinum, TopCenter(0f, -150f, 900f, 110f), true);
            Text(homePage.transform, "SubtitleLabel", HomeText.Subtitle, 24, Cyan, TopCenter(0f, -238f, 900f, 40f));

            // 情報帯: CORE COIN / SQUAD / OWNED
            RectTransform band = Panel(homePage.transform, "InfoBand", TopStretch(32f, -300f, 210f), Platinum);
            coinValue = InfoCell(band, "Coin", HomeText.CoinCaption, 0f, 1f / 3f, Platinum);
            squadValue = InfoCell(band, "Squad", HomeText.SquadCaption, 1f / 3f, 2f / 3f, TextMain);
            ownedValue = InfoCell(band, "Owned", HomeText.OwnedCaption, 2f / 3f, 1f, TextMain);

            RectTransform record = Panel(homePage.transform, "RecordBand", TopStretch(32f, -530f, 96f), Cyan);
            Text(record, "RecordCaption", HomeText.RecordCaption, 24, TextSub, Box(new Vector2(0f, 0f), new Vector2(0.35f, 1f), 28f, 0f), false, TextAlignmentOptions.MidlineLeft);
            recordValue = Text(record, "RecordValue", string.Empty, 36, TextMain, Box(new Vector2(0.35f, 0f), new Vector2(1f, 1f), 0f, -28f), true, TextAlignmentOptions.MidlineRight);

            // 主ボタン（最も強く）と副ボタン
            Button(homePage.transform, "BATTLEButton", HomeText.Battle, true, BottomStretch(48f, 230f, 210f), 74, () => battleAction?.Invoke());
            Button(homePage.transform, "UNITSETButton", HomeText.UnitSet, false, BottomThird(0, 40f, 150f), 32, () => unitSetAction?.Invoke());
            Button(homePage.transform, "GACHAButton", HomeText.Gacha, false, BottomThird(1, 40f, 150f), 32, () => Navigate(HomePage.Gacha));
            Button(homePage.transform, "COLLECTIONButton", HomeText.Collection, false, BottomThird(2, 40f, 150f), 30, () => Navigate(HomePage.Collection));

            Button(homePage.transform, "SETTINGSButton", HomeText.Settings, false,
                new Layout(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(250f, 120f)),
                26, OpenSettings);
        }

        // ---------------- Settings ----------------

        private void BuildSettings(RectTransform root)
        {
            settingsOverlay = CreatePage("SettingsOverlay", root);
            Image dim = settingsOverlay.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;

            RectTransform panel = Panel(settingsOverlay.transform, "SettingsPanel",
                new Layout(Center, Center, Center, Vector2.zero, new Vector2(720f, 560f)), Cyan);

            Text(panel, "SettingsTitle", HomeText.Settings, 44, Platinum, TopCenter(0f, -40f, 600f, 70f), true);
            fxLabel = Button(panel, "FXButton", HomeText.FxOn, false,
                new Layout(Center, Center, Center, new Vector2(0f, 10f), new Vector2(560f, 140f)), 38, ToggleFx).GetComponentInChildren<TMP_Text>(true);
            Button(panel, "CLOSEButton", HomeText.Close, false,
                new Layout(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 130f)), 34, CloseSettings);

            settingsOverlay.SetActive(false);
        }

        private void OpenSettings()
        {
            if (flow.Current != HomePage.Home)
            {
                return;
            }

            fxLabel.text = PresentationSettings.FxEnabled ? HomeText.FxOn : HomeText.FxOff;
            settingsOverlay.SetActive(true);
        }

        private void ToggleFx()
        {
            PresentationSettings.FxEnabled = !PresentationSettings.FxEnabled;
            fxLabel.text = PresentationSettings.FxEnabled ? HomeText.FxOn : HomeText.FxOff;
        }

        private void CloseSettings()
        {
            settingsOverlay.SetActive(false);
        }

        // ---------------- Reward ----------------

        public void ShowReward(RewardSummary summary)
        {
            LastReward = summary;
            ForcePage(HomePage.Reward);

            rewardOutcome.text = summary.OutcomeText;
            rewardOutcome.color = summary.Battles == 1 && summary.Wins == 1 ? Platinum : summary.Losses == summary.Battles ? Muted : TextMain;
            rewardBreakdown.text = summary.BreakdownText;
            rewardEarned.text = summary.EarnedText;
            rewardBefore.text = HomeText.Number(summary.BalanceBefore);
            rewardAfter.text = HomeText.Number(summary.BalanceAfter);
            SetButtonEnabled(rewardGachaButton, summary.CanAffordGacha);
            rewardReason.text = summary.GachaReasonText;
        }

        private void BuildReward(RectTransform root)
        {
            rewardPage = CreatePage("RewardPage", root);

            // 最初の文字は MISSION COMPLETE です（画面の見出し）。
            Text(rewardPage.transform, "MissionLabel", HomeText.MissionComplete, 60, Platinum, TopCenter(0f, -190f, 960f, 90f), true);
            rewardOutcome = Text(rewardPage.transform, "OutcomeLabel", string.Empty, 120, Platinum, TopCenter(0f, -290f, 960f, 150f), true);
            rewardBreakdown = Text(rewardPage.transform, "BreakdownLabel", string.Empty, 30, TextSub, TopCenter(0f, -440f, 900f, 50f));

            RectTransform panel = Panel(rewardPage.transform, "RewardPanel", TopStretch(48f, -510f, 360f), Platinum);
            Text(panel, "RewardCaption", "REWARD", 26, TextSub, TopCenter(0f, -28f, 800f, 40f));
            rewardEarned = Text(panel, "EarnedLabel", string.Empty, 66, Platinum, TopCenter(0f, -76f, 900f, 90f), true);
            Text(panel, "BalanceCaption", "BALANCE", 24, TextSub, TopCenter(0f, -190f, 800f, 36f));
            rewardBefore = Text(panel, "BalanceBefore", string.Empty, 50, TextMain,
                new Layout(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -232f), new Vector2(260f, 80f)), true, TextAlignmentOptions.MidlineRight);
            Arrow(panel, "BalanceArrow", new Layout(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(84f, 48f)), Platinum);
            rewardAfter = Text(panel, "BalanceAfter", string.Empty, 50, Platinum,
                new Layout(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(70f, -232f), new Vector2(260f, 80f)), true, TextAlignmentOptions.MidlineLeft);

            Button(rewardPage.transform, "HOMEButton", HomeText.Home, false,
                new Layout(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(220f, 120f)), 28, () => Navigate(HomePage.Home));
            Button(rewardPage.transform, "CONTINUEButton", HomeText.Continue, true, BottomStretch(48f, 270f, 190f), 60, () => Navigate(HomePage.Home));
            rewardReason = Text(rewardPage.transform, "GachaReason", string.Empty, 26, Platinum, BottomStretch(48f, 200f, 56f));
            rewardGachaButton = Button(rewardPage.transform, "TOGACHAButton", HomeText.ToGacha, false, BottomStretch(48f, 40f, 150f), 34, () => Navigate(HomePage.Gacha));
        }

        // ---------------- Gacha ----------------

        public void ShowGacha()
        {
            if (flow.Current != HomePage.Gacha && !flow.TryGo(HomePage.Gacha))
            {
                return;
            }

            ShowOnly(HomePage.Gacha);
            RefreshGacha();
        }

        private void RefreshGacha()
        {
            GachaSummary summary = GachaSummary.Build(Profile(), catalog);

            gachaCoinValue.text = HomeText.Number(summary.Balance);
            gachaCostValue.text = HomeText.Number(summary.Cost);
            gachaPoolValue.text = HomeText.Number(summary.UnownedCount) + " / " + HomeText.Number(summary.CatalogCount);
            gachaStatus.text = summary.PoolStatusText;
            gachaStatus.color = summary.IsNewGuaranteed ? Platinum : Cyan;
            gachaReason.text = summary.ReasonText;
            SetButtonEnabled(pullButton, summary.CanActivate && !session.IsBusy);
            SetButtonEnabled(gachaBackButton, true);

            summonGraphic.Show(GachaSummonPhase.Idle, 0.5f, 0f);
        }

        private void BuildGacha(RectTransform root)
        {
            gachaPage = CreatePage("GachaPage", root);

            Text(gachaPage.transform, "SummonTitle", HomeText.Summon, 64, Platinum, TopCenter(0f, -190f, 960f, 90f), true);
            Text(gachaPage.transform, "SummonSubtitle", HomeText.SummonSubtitle, 22, Cyan, TopCenter(0f, -268f, 960f, 40f));

            RectTransform chamber = Panel(gachaPage.transform, "SummonChamber", TopStretch(48f, -320f, 620f), Cyan);

            RectTransform graphicRect = CreateRect("SummonGraphic", chamber);
            Stretch(graphicRect, 24f);
            graphicRect.offsetMax = new Vector2(-24f, -100f);
            graphicRect.offsetMin = new Vector2(24f, 84f);
            summonGraphic = graphicRect.gameObject.AddComponent<GachaSummonGraphic>();

            gachaStatus = Text(chamber, "PoolStatus", string.Empty, 36, Platinum, BottomStretch(24f, 18f, 60f), true);

            TMP_Text summonLabel = Text(chamber, "SummonLabel", string.Empty, 40, Platinum, TopCenter(0f, -30f, 700f, 60f), true);
            CanvasGroup summonGroup = summonLabel.gameObject.AddComponent<CanvasGroup>();
            summonGroup.alpha = 0f;

            summonView = gachaPage.AddComponent<GachaSummonView>();
            summonView.Bind(summonGraphic, summonLabel, summonGroup);

            RectTransform info = Panel(gachaPage.transform, "SummonInfo", TopStretch(48f, -960f, 220f), Platinum);
            gachaCoinValue = InfoCell(info, "Coin", HomeText.CoinCaption, 0f, 1f / 3f, Platinum);
            gachaCostValue = InfoCell(info, "Cost", "COST", 1f / 3f, 2f / 3f, TextMain);
            gachaPoolValue = InfoCell(info, "Pool", "UNOWNED / POOL", 2f / 3f, 1f, TextMain);

            gachaBackButton = Button(gachaPage.transform, "BACKButton", HomeText.Back, false,
                new Layout(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(220f, 120f)), 28, () => Navigate(HomePage.Home));
            gachaReason = Text(gachaPage.transform, "ActivateReason", string.Empty, 28, Platinum, BottomStretch(48f, 270f, 56f));
            pullButton = Button(gachaPage.transform, "ACTIVATE100Button", HomeText.Activate(GameEconomy.GachaCost), true, BottomStretch(48f, 60f, 200f), 60, Activate);

            // 演出中は入力を閉じます（画面の最前面の透明な受け皿）。
            RectTransform blocker = CreateRect("SummonInputBlocker", gachaPage.transform);
            Stretch(blocker);
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0f);
            blockerImage.raycastTarget = true;
            inputBlocker = blocker.gameObject;
            inputBlocker.SetActive(false);
        }

        /// <summary>
        /// ACTIVATE。結果はここで一度だけ確定・保存し、演出の間は入力を閉じます。
        /// 連打しても、演出中・残高不足では何もしません。
        /// </summary>
        private void Activate()
        {
            if (flow.IsInputLocked || session.IsBusy || flow.Current != HomePage.Gacha)
            {
                return;
            }

            PlayerProfile profile = Profile();

            if (!session.TryBegin(profile, catalog, out GachaResult _))
            {
                RefreshGacha();
                return;
            }

            if (!flow.TryGo(HomePage.Summoning))
            {
                session.Finish();
                return;
            }

            SetInputBlocked(true);
            RefreshGachaWhileSummoning();
            summonView.Play(GachaSummonPlan.Create(PresentationSettings.FxEnabled), OnSummonCompleted);
        }

        private void RefreshGachaWhileSummoning()
        {
            gachaCoinValue.text = HomeText.Number(Profile().Coins);
            SetButtonEnabled(pullButton, false);
            SetButtonEnabled(gachaBackButton, false);
            gachaReason.text = string.Empty;
        }

        private void OnSummonCompleted()
        {
            GachaResult result = session.Result;

            session.Finish();
            SetInputBlocked(false);

            if (!flow.CompleteSummon())
            {
                return;
            }

            ShowAcquisition(AcquisitionSummary.Build(result, Profile(), catalog));
        }

        /// <summary>押せない状態は、面だけでなく文字も暗くして一目で分かるようにします。</summary>
        private static void SetButtonEnabled(Button button, bool enabled)
        {
            button.interactable = enabled;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.alpha = enabled ? 1f : 0.4f;
            }
        }

        private void SetInputBlocked(bool blocked)
        {
            if (inputBlocker != null)
            {
                inputBlocker.SetActive(blocked);
            }
        }

        // ---------------- Acquisition ----------------

        private void ShowAcquisition(AcquisitionSummary summary)
        {
            LastAcquisition = summary;
            ShowOnly(HomePage.Acquisition);

            if (!summary.IsValid)
            {
                acquisitionTitle.text = "ACQUISITION ERROR";
                acquisitionName.text = string.Empty;
                acquisitionLevel.text = string.Empty;
                acquisitionPower.text = string.Empty;
                acquisitionCore.text = string.Empty;
                acquisitionSkillName.text = string.Empty;
                acquisitionSkillText.text = string.Empty;
                acquisitionCopiesBefore.text = string.Empty;
                acquisitionCopiesAfter.text = string.Empty;
                acquisitionOwned.text = string.Empty;
                acquisitionPortrait.Clear();
                acquisitionUnitSetButton.SetActive(false);
                return;
            }

            acquisitionTitle.text = summary.TitleText;
            acquisitionTitle.color = summary.IsNew ? Platinum : Cyan;
            acquisitionName.text = summary.Name;
            acquisitionLevel.text = summary.LevelText + "   " + ColouredAttributes(summary.Definition);
            acquisitionPower.text = summary.PowerText;
            acquisitionCore.text = summary.CoreText;
            acquisitionSkillName.text = summary.SkillName;
            acquisitionSkillText.text = summary.SkillDescription;
            acquisitionCopiesBefore.text = HomeText.Number(summary.CopiesBefore);
            acquisitionCopiesAfter.text = HomeText.Number(summary.CopiesAfter);
            acquisitionOwned.text = summary.OwnedText;
            acquisitionPortrait.Show(summary.Definition, palette);
            acquisitionUnitSetButton.SetActive(summary.ShowsUnitSetLink);
        }

        private void BuildAcquisition(RectTransform root, Texture[] textures)
        {
            acquisitionPage = CreatePage("AcquisitionPage", root);

            acquisitionTitle = Text(acquisitionPage.transform, "AcquisitionTitle", string.Empty, 64, Platinum, TopCenter(0f, -150f, 960f, 90f), true);

            RectTransform frame = Panel(acquisitionPage.transform, "PortraitFrame",
                new Layout(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(560f, 560f)), Cyan);
            acquisitionPortrait = BuildPortrait(frame, textures);

            acquisitionName = Text(acquisitionPage.transform, "AcquisitionName", string.Empty, 66, TextMain, TopCenter(0f, -826f, 960f, 84f), true);
            acquisitionLevel = Text(acquisitionPage.transform, "AcquisitionLevel", string.Empty, 32, TextSub, TopCenter(0f, -906f, 960f, 48f));
            acquisitionLevel.richText = true;

            RectTransform stats = Panel(acquisitionPage.transform, "StatsPanel", TopStretch(48f, -966f, 140f), Platinum);
            acquisitionPower = InfoCell(stats, "Power", HomeText.Power, 0f, 0.6f, TextMain);
            acquisitionCore = InfoCell(stats, "Core", HomeText.Core, 0.6f, 1f, TextMain);

            RectTransform skill = Panel(acquisitionPage.transform, "SkillPanel", TopStretch(48f, -1122f, 196f), Cyan);
            acquisitionSkillName = Text(skill, "SkillName", string.Empty, 34, Platinum, TopStretch(28f, -18f, 52f), true, TextAlignmentOptions.MidlineLeft);
            acquisitionSkillText = Text(skill, "SkillDescription", string.Empty, 28, TextMain, Box(new Vector2(0f, 0f), new Vector2(1f, 1f), 28f, -28f, 16f, -76f), false, TextAlignmentOptions.TopLeft);

            RectTransform copies = CreateRect("CopiesRow", acquisitionPage.transform);
            Apply(copies, TopStretch(48f, -1334f, 72f));
            Text(copies, "CopiesCaption", HomeText.Copies, 26, TextSub, Box(new Vector2(0f, 0f), new Vector2(0.25f, 1f), 0f, 0f), false, TextAlignmentOptions.MidlineLeft);
            acquisitionCopiesBefore = Text(copies, "CopiesBefore", string.Empty, 40, TextMain, Box(new Vector2(0.22f, 0f), new Vector2(0.32f, 1f), 0f, 0f), true, TextAlignmentOptions.MidlineRight);
            Arrow(copies, "CopiesArrow", Box(new Vector2(0.34f, 0.25f), new Vector2(0.44f, 0.75f), 0f, 0f), Platinum);
            acquisitionCopiesAfter = Text(copies, "CopiesAfter", string.Empty, 40, Platinum, Box(new Vector2(0.46f, 0f), new Vector2(0.56f, 1f), 0f, 0f), true, TextAlignmentOptions.MidlineLeft);
            acquisitionOwned = Text(copies, "OwnedLabel", string.Empty, 28, TextSub, Box(new Vector2(0.56f, 0f), new Vector2(1f, 1f), 0f, 0f), false, TextAlignmentOptions.MidlineRight);

            Button(acquisitionPage.transform, "TOCOLLECTIONButton", HomeText.ToCollection, true, BottomHalf(0, 210f, 150f), 32, () => Navigate(HomePage.Collection));
            Button(acquisitionPage.transform, "HOMEButton", HomeText.Home, false, BottomHalf(1, 210f, 150f), 32, () => Navigate(HomePage.Home));
            acquisitionUnitSetButton = Button(acquisitionPage.transform, "TOUNITSETButton", HomeText.ToUnitSet, false, BottomStretch(48f, 40f, 150f), 32, () => unitSetAction?.Invoke()).gameObject;
        }

        /// <summary>UnitSet・Battle の縮小立ち絵と同じ3層（下地・一次属性・二次属性）で立ち絵を組み立てます。</summary>
        private static BeastThumbnailView BuildPortrait(RectTransform frame, Texture[] textures)
        {
            RectTransform holder = CreateRect("Portrait", frame);
            Stretch(holder, 28f);

            RawImage[] layers = new RawImage[3];
            string[] names = { "BaseLayer", "PrimaryLayer", "SecondaryLayer" };

            for (int i = 0; i < layers.Length; i++)
            {
                RectTransform layer = CreateRect(names[i], holder);
                Stretch(layer);
                layers[i] = layer.gameObject.AddComponent<RawImage>();
                layers[i].texture = textures != null && i < textures.Length ? textures[i] : null;
                layers[i].raycastTarget = false;
            }

            BeastThumbnailView view = holder.gameObject.AddComponent<BeastThumbnailView>();
            view.Bind(layers[0], layers[1], layers[2]);
            return view;
        }

        private string ColouredAttributes(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string primary = Coloured(definition.PrimaryAttribute);

            return definition.HasSecondaryAttribute ? primary + " / " + Coloured(definition.SecondaryAttribute) : primary;
        }

        private string Coloured(UnitAttribute attribute)
        {
            string name = attribute.ToString().ToUpperInvariant();

            if (palette == null)
            {
                return name;
            }

            return "<color=#" + ColorUtility.ToHtmlStringRGB(palette.GetColors(attribute).PrimaryColor) + ">" + name + "</color>";
        }

        // ---------------- Collection ----------------

        public void ShowCollection()
        {
            if (flow.Current != HomePage.Collection && !flow.TryGo(HomePage.Collection))
            {
                return;
            }

            ShowOnly(HomePage.Collection);

            PlayerProfile profile = Profile();
            StringBuilder builder = new StringBuilder();
            int unique = 0;
            int total = 0;

            if (catalog != null && profile != null)
            {
                for (int i = 0; i < catalog.Owned.Count; i++)
                {
                    OwnedCoreBeast beast = catalog.Owned[i];

                    if (beast == null || !beast.IsValid)
                    {
                        continue;
                    }

                    total++;
                    int copies = profile.CopiesOf(beast.InstanceId);

                    if (copies > 0)
                    {
                        unique++;
                        builder.Append("[OWNED] ")
                            .Append(beast.Definition.DisplayName)
                            .Append("  Lv.").Append(beast.Level)
                            .Append("  ").Append(AcquisitionSummary.AttributesOf(beast.Definition).Replace(" ", string.Empty))
                            .Append("  x").Append(copies);
                    }
                    else
                    {
                        builder.Append("[LOCKED] CORE  ?????");
                    }

                    builder.AppendLine();
                }
            }

            collectionLabel.text = "COLLECTION  " + unique + "/" + total + "\n\n" + builder;
        }

        private void BuildCollection(RectTransform root)
        {
            collectionPage = CreatePage("CollectionPage", root);

            // 最初の文字は一覧です（既存の確認と同じ並び）。
            RectTransform panel = Panel(collectionPage.transform, "CollectionPanel", Box(new Vector2(0f, 0f), new Vector2(1f, 1f), 32f, -32f, 420f, -170f), Cyan);
            collectionLabel = Text(panel, "CollectionList", string.Empty, 28, TextMain, Box(new Vector2(0f, 0f), new Vector2(1f, 1f), 32f, -32f, 24f, -24f), false, TextAlignmentOptions.TopLeft);
            panel.SetAsFirstSibling();

            Button(collectionPage.transform, "BACKButton", HomeText.Back, false,
                new Layout(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(220f, 120f)), 28, () => Navigate(HomePage.Home));
            Button(collectionPage.transform, "TOUNITSETButton", HomeText.ToUnitSet, true, BottomStretch(48f, 220f, 160f), 34, () => unitSetAction?.Invoke());
            Button(collectionPage.transform, "HOMEButton", HomeText.Home, false, BottomStretch(48f, 40f, 150f), 32, () => Navigate(HomePage.Home));
        }

        // ---------------- 遷移 ----------------

        private void Navigate(HomePage page)
        {
            if (flow.IsInputLocked)
            {
                return;
            }

            switch (page)
            {
                case HomePage.Home:
                    ShowHome();
                    break;
                case HomePage.Gacha:
                    ShowGacha();
                    break;
                case HomePage.Collection:
                    ShowCollection();
                    break;
            }
        }

        /// <summary>報酬画面はシーン読み込み時にだけ、最初の画面として出します。</summary>
        private void ForcePage(HomePage page)
        {
            flow.Abort();
            flow.Begin(page);
            ShowOnly(page);
        }

        private void ShowOnly(HomePage page)
        {
            GameObject target = pages.TryGetValue(page, out GameObject value) ? value : homePage;

            homePage.SetActive(target == homePage);
            rewardPage.SetActive(target == rewardPage);
            gachaPage.SetActive(target == gachaPage);
            acquisitionPage.SetActive(target == acquisitionPage);
            collectionPage.SetActive(target == collectionPage);

            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }
        }

        private PlayerProfile Profile()
        {
            return PlayerProfileProvider.Get(catalog);
        }

        /// <summary>
        /// 獲得画面のスキル説明は日本語で、LiberationSans のフォールバック（Noto Sans JP サブセット）から描きます。
        /// TMP はフォールバックの文字を初めて描くときに子の SubMesh を作るため、組み立て時に一度だけ作っておきます。
        /// </summary>
        private void PrepareFallbackGlyphs()
        {
            bool wasActive = acquisitionPage.activeSelf;

            acquisitionPage.SetActive(true);
            acquisitionSkillText.text = "直前に出した個体";
            acquisitionSkillText.ForceMeshUpdate(true, true);
            acquisitionSkillText.text = string.Empty;
            acquisitionSkillText.ForceMeshUpdate(true, true);
            acquisitionPage.SetActive(wasActive);
        }

        /// <summary>Phase 6 以前のシーン上の文字・ボタン（HOME・BATTLE・UNIT SET）は使わないため隠し、背景だけを残します。</summary>
        private static void HideLegacyContent(RectTransform host)
        {
            for (int i = 0; i < host.childCount; i++)
            {
                Transform child = host.GetChild(i);

                if (child.GetComponent<ShipBackdropView>() == null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        // ---------------- 部品 ----------------

        private readonly struct Layout
        {
            internal Layout(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                Pivot = pivot;
                Position = position;
                Size = size;
                UsesOffsets = false;
                OffsetMin = Vector2.zero;
                OffsetMax = Vector2.zero;
            }

            internal Layout(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                Pivot = new Vector2(0.5f, 0.5f);
                Position = Vector2.zero;
                Size = Vector2.zero;
                UsesOffsets = true;
                OffsetMin = offsetMin;
                OffsetMax = offsetMax;
            }

            internal Vector2 AnchorMin { get; }
            internal Vector2 AnchorMax { get; }
            internal Vector2 Pivot { get; }
            internal Vector2 Position { get; }
            internal Vector2 Size { get; }
            internal bool UsesOffsets { get; }
            internal Vector2 OffsetMin { get; }
            internal Vector2 OffsetMax { get; }
        }

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Layout TopCenter(float x, float y, float width, float height)
        {
            return new Layout(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(width, height));
        }

        private static Layout TopStretch(float inset, float y, float height)
        {
            return new Layout(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(inset, y - height), new Vector2(-inset, y));
        }

        private static Layout BottomStretch(float inset, float y, float height)
        {
            return new Layout(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(inset, y), new Vector2(-inset, y + height));
        }

        /// <summary>下段を3等分した i 番目（左右48・間24）。</summary>
        private static Layout BottomThird(int index, float y, float height)
        {
            float a = index / 3f;
            float b = (index + 1) / 3f;
            float left = index == 0 ? 48f : 12f;
            float right = index == 2 ? 48f : 12f;

            return new Layout(new Vector2(a, 0f), new Vector2(b, 0f), new Vector2(left, y), new Vector2(-right, y + height));
        }

        /// <summary>下段を2等分した i 番目。</summary>
        private static Layout BottomHalf(int index, float y, float height)
        {
            float a = index * 0.5f;
            float b = a + 0.5f;
            float left = index == 0 ? 48f : 12f;
            float right = index == 1 ? 48f : 12f;

            return new Layout(new Vector2(a, 0f), new Vector2(b, 0f), new Vector2(left, y), new Vector2(-right, y + height));
        }

        private static Layout Box(Vector2 anchorMin, Vector2 anchorMax, float left, float right, float bottom = 0f, float top = 0f)
        {
            return new Layout(anchorMin, anchorMax, new Vector2(left, bottom), new Vector2(right, top));
        }

        private static void Apply(RectTransform rect, Layout layout)
        {
            rect.anchorMin = layout.AnchorMin;
            rect.anchorMax = layout.AnchorMax;

            if (layout.UsesOffsets)
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = layout.OffsetMin;
                rect.offsetMax = layout.OffsetMax;
                return;
            }

            rect.pivot = layout.Pivot;
            rect.sizeDelta = layout.Size;
            rect.anchoredPosition = layout.Position;
        }

        private GameObject CreatePage(string name, RectTransform root)
        {
            RectTransform page = CreateRect(name, root);
            Stretch(page);
            return page.gameObject;
        }

        private RectTransform Panel(Transform parent, string name, Layout layout, Color accent)
        {
            RectTransform rect = CreateRect(name, parent);
            Apply(rect, layout);

            HomePanelGraphic graphic = rect.gameObject.AddComponent<HomePanelGraphic>();
            graphic.Configure(PanelFill, PanelEdge, accent);

            return rect;
        }

        private TMP_Text InfoCell(RectTransform band, string name, string caption, float from, float to, Color valueColor)
        {
            RectTransform cell = CreateRect(name + "Cell", band);
            Apply(cell, Box(new Vector2(from, 0f), new Vector2(to, 1f), 8f, -8f));

            Text(cell, name + "Caption", caption, 22, TextSub, TopStretch(0f, -22f, 34f));

            return Text(cell, name + "Value", string.Empty, 50, valueColor, Box(new Vector2(0f, 0f), new Vector2(1f, 1f), 0f, 0f, 14f, -60f), true);
        }

        private void Arrow(Transform parent, string name, Layout layout, Color arrowColor)
        {
            RectTransform rect = CreateRect(name, parent);
            Apply(rect, layout);

            HomeArrowGraphic arrow = rect.gameObject.AddComponent<HomeArrowGraphic>();
            arrow.color = arrowColor;
        }

        private Button Button(Transform parent, string name, string label, bool primary, Layout layout, float fontSize, UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent);
            Apply(rect, layout);

            // 押せる範囲（透明）と見た目（パネル）を分けます。
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            RectTransform face = CreateRect("Face", rect);
            Stretch(face);
            HomePanelGraphic panel = face.gameObject.AddComponent<HomePanelGraphic>();
            panel.Configure(
                primary ? PlatinumDeep : PanelFill,
                primary ? Platinum : PanelEdge,
                primary ? new Color(1f, 0.97f, 0.88f, 1f) : Cyan,
                primary ? 26f : 18f,
                primary ? 3f : 2f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.84f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.42f, 0.44f, 0.5f, 0.55f);
            button.colors = colors;

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TMP_Text text = Text(rect, "Label", label, fontSize, primary ? TextOnPrimary : TextMain, Box(Vector2.zero, Vector2.one, 12f, -12f), true);
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(20f, fontSize);
            text.fontSizeMax = fontSize;

            return button;
        }

        private TMP_Text Text(
            Transform parent,
            string name,
            string value,
            float size,
            Color color,
            Layout layout,
            bool bold = false,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = CreateRect(name, parent);
            Apply(rect, layout);

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = alignment == TextAlignmentOptions.TopLeft ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.text = value ?? string.Empty;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.layer = 5;
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
