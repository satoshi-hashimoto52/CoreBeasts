using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 右上の歯車から開く設定パネル。
    ///
    /// ヘッダーへ常時出していた HOME と FX ON/OFF をここへ移しました。
    /// 開閉と「押された」の通知だけを持ち、進行にも勝敗にも触れません。
    /// </summary>
    public sealed class BattleSettingsViewTests
    {
        private TestBattleViews views;
        private readonly FakeBattleText text = new FakeBattleText();

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
        }

        [TearDown]
        public void TearDown()
        {
            views.Cleanup();
        }

        private BattleSettingsView Create(out TestBattleViews.SettingsParts parts)
        {
            BattleSettingsView view = views.CreateSettingsView(out parts);

            view.Bind(text);

            return view;
        }

        // ---------------- 初期状態 ----------------

        [Test]
        public void StartsClosed()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            Assert.That(view.IsOpen, Is.False, "初期状態でパネルを出しません。");
            Assert.That(parts.PanelRoot.activeSelf, Is.False);
        }

        [Test]
        public void Bind_FillsTheStaticLabels()
        {
            Create(out TestBattleViews.SettingsParts parts);

            Assert.That(parts.TitleLabel.text, Is.EqualTo("SETTINGS"));
            Assert.That(parts.FxCaptionLabel.text, Is.EqualTo("FX"));
            Assert.That(parts.HomeLabel.text, Is.EqualTo("HOME"));
            Assert.That(parts.CloseLabel.text, Is.EqualTo("CLOSE"));
        }

        [Test]
        public void AllReferencesAreRequired()
        {
            BattleSettingsView view = Create(out _);

            Assert.That(view.HasRequiredReferences(), Is.True);
        }

        // ---------------- 開閉 ----------------

        [Test]
        public void GearOpensThePanelAndOpensItOnlyOnce()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            int changes = 0;
            view.OpenStateChanged += () => changes++;

            parts.SettingsButton.onClick.Invoke();

            Assert.That(view.IsOpen, Is.True);
            Assert.That(parts.PanelRoot.activeSelf, Is.True);
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(view.Open(), Is.False, "開いているときのOpenは何もしません。");
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void GearClosesThePanelWhenItIsAlreadyOpen()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            parts.SettingsButton.onClick.Invoke();
            parts.SettingsButton.onClick.Invoke();

            Assert.That(view.IsOpen, Is.False);
            Assert.That(parts.PanelRoot.activeSelf, Is.False);
        }

        [Test]
        public void CloseButtonClosesThePanel()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            view.Open();
            parts.CloseButton.onClick.Invoke();

            Assert.That(view.IsOpen, Is.False);
        }

        [Test]
        public void TappingOutsideThePanelClosesIt()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            view.Open();
            parts.BackdropButton.onClick.Invoke();

            Assert.That(view.IsOpen, Is.False, "パネル外のタップで閉じます。");
        }

        [Test]
        public void TheBackdropIsBehindThePanelBody()
        {
            Create(out TestBattleViews.SettingsParts parts);

            Transform backdrop = parts.BackdropButton.transform;

            Assert.That(
                backdrop.parent,
                Is.SameAs(parts.PanelRoot.transform),
                "パネル外の板はパネルの内側に置きます。");

            Assert.That(
                parts.Body.transform.parent,
                Is.SameAs(parts.PanelRoot.transform));

            Assert.That(
                backdrop.GetSiblingIndex(),
                Is.LessThan(parts.Body.transform.GetSiblingIndex()),
                "板は本体より後ろに描きます。");
        }

        [Test]
        public void ADisabledGearDoesNotOpenThePanel()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            view.SetSettingsInteractable(false);

            Assert.That(parts.SettingsButton.interactable, Is.False);

            parts.SettingsButton.onClick.Invoke();

            Assert.That(
                view.IsOpen,
                Is.False,
                "演出中は歯車を押せません。押されても開きません。");

            view.SetSettingsInteractable(true);
            parts.SettingsButton.onClick.Invoke();

            Assert.That(view.IsOpen, Is.True);
        }

        // ---------------- 中身の操作 ----------------

        [Test]
        public void FxTapRaisesTheRequestAndTheValueFollowsTheState()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            int requests = 0;
            view.FxToggleRequested += () => requests++;

            view.SetFxState(true);
            Assert.That(parts.FxValueLabel.text, Is.EqualTo("ON"));

            view.Open();
            parts.FxButton.onClick.Invoke();

            Assert.That(requests, Is.EqualTo(1), "切り替えるかどうかは受け手が決めます。");

            // 受け手が切り替えた結果を、あらためて反映します。
            view.SetFxState(false);

            Assert.That(parts.FxValueLabel.text, Is.EqualTo("OFF"));
        }

        [Test]
        public void HomeTapRaisesTheRequest()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            int requests = 0;
            view.HomeRequested += () => requests++;

            view.Open();
            parts.HomeButton.onClick.Invoke();

            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void AClosedPanelIgnoresItsOwnButtons()
        {
            BattleSettingsView view = Create(out TestBattleViews.SettingsParts parts);

            int fx = 0;
            int home = 0;

            view.FxToggleRequested += () => fx++;
            view.HomeRequested += () => home++;

            parts.FxButton.onClick.Invoke();
            parts.HomeButton.onClick.Invoke();

            Assert.That(fx, Is.EqualTo(0));
            Assert.That(home, Is.EqualTo(0));
        }

        [Test]
        public void TheGearIsTheOnlyControlOutsideThePanel()
        {
            Create(out TestBattleViews.SettingsParts parts);

            Assert.That(
                parts.SettingsButton.transform.parent,
                Is.Not.SameAs(parts.PanelRoot.transform),
                "歯車だけはパネルの外に出します。");

            Assert.That(parts.FxButton.transform.parent, Is.SameAs(parts.Body.transform));
            Assert.That(parts.HomeButton.transform.parent, Is.SameAs(parts.Body.transform));
            Assert.That(parts.CloseButton.transform.parent, Is.SameAs(parts.Body.transform));
        }

        // ---------------- 入力ゲート ----------------

        [Test]
        public void AnOpenPanelBlocksUnitSelectionAndDeploy()
        {
            Assert.That(
                BattleInputGate.AllowsTraySelection(true, false),
                Is.True,
                "閉じていれば従来どおりです。");

            Assert.That(
                BattleInputGate.AllowsTraySelection(true, true),
                Is.False,
                "パネル表示中はユニット選択を通しません。");

            Assert.That(
                BattleInputGate.AllowsDeploy(true, false, false), Is.True);

            Assert.That(
                BattleInputGate.AllowsDeploy(true, true, false),
                Is.False,
                "パネル表示中はDEPLOYを通しません。");

            Assert.That(
                BattleInputGate.AllowsRematch(true, true, false),
                Is.False);
        }

        [Test]
        public void ThePresentationBlocksDeployAndRematchToo()
        {
            Assert.That(BattleInputGate.AllowsDeploy(true, false, true), Is.False);
            Assert.That(BattleInputGate.AllowsRematch(true, false, true), Is.False);
        }

        [Test]
        public void TheGateNeverOverridesTheCoordinator()
        {
            // 進行役が「不可」と言っているものを、画面側が通すことはありません。
            Assert.That(BattleInputGate.AllowsTraySelection(false, false), Is.False);
            Assert.That(BattleInputGate.AllowsDeploy(false, false, false), Is.False);
            Assert.That(BattleInputGate.AllowsRematch(false, false, false), Is.False);
        }

        [TestCase(BattleUiState.Selecting, true)]
        [TestCase(BattleUiState.MatchFinished, true)]
        [TestCase(BattleUiState.SquadRequired, true)]
        [TestCase(BattleUiState.Resolving, false)]
        [TestCase(BattleUiState.ShowingResult, false)]
        public void SettingsAreLockedWhileTheRoundIsBeingPresented(
            BattleUiState state,
            bool expected)
        {
            Assert.That(
                BattleInputGate.AllowsSettings(state, false),
                Is.EqualTo(expected));
        }

        [Test]
        public void SettingsAreLockedWhileACoroutineIsStillRunning()
        {
            Assert.That(
                BattleInputGate.AllowsSettings(BattleUiState.Selecting, true),
                Is.False,
                "演出のCoroutineが走っているあいだは設定を開けません。");
        }
    }
}
