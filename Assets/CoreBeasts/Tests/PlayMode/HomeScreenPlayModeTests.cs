using System;
using System.Collections;
using System.Collections.Generic;

using CoreBeasts.HomeUI;
using CoreBeasts.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// Phase 7（Home・報酬・ガチャ・獲得画面）を、実際の Home シーンで確かめます。
    /// 保存はテスト用の保存先へ差し替え、端末のプロフィール・編成には触れません。
    /// </summary>
    public sealed class HomeScreenPlayModeTests
    {
        private const float SummonLimitSeconds = 5f;
        private const string ProfileKey = "CoreBeasts.Tests.Profile.Phase7";
        private const string SquadKey = "CoreBeasts.Tests.Squad.Phase7";

        private static readonly string[] Pages =
        {
            "HomePage", "RewardPage", "GachaPage", "AcquisitionPage", "CollectionPage",
        };

        private ISquadRepository originalSquads;
        private readonly List<string> missingCharacterWarnings = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            missingCharacterWarnings.Clear();
            Application.logMessageReceived += CollectMissingCharacters;

            originalSquads = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());
            PlayerProfileProvider.SetRepository(new InMemoryPlayerProfileRepository());
            GameFlowState.Reset();
            PresentationSettings.FxEnabled = true;

            yield return LoadHome();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= CollectMissingCharacters;
            GameFlowState.Reset();
            PresentationSettings.FxEnabled = true;
            PlayerProfileProvider.Reset();
            SquadRepositoryProvider.SetShared(originalSquads);
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.DeleteKey(SquadKey);
            yield return null;
        }

        // ---------------- Home ----------------

        [UnityTest]
        public IEnumerator HomeShowsTheInformationBandAndEveryButtonFitsTheSafeAreaOnIPhone12Mini()
        {
            yield return UsePortraitPhone();

            AssertOnlyPage("HomePage");
            Assert.That(Label("HomePage", "CoinValue"), Is.EqualTo("100"));
            Assert.That(Label("HomePage", "SquadValue"), Is.EqualTo("SET 1"));
            Assert.That(Label("HomePage", "OwnedValue"), Is.EqualTo("7 / 8"));
            Assert.That(Label("HomePage", "OwnedCaption"), Is.EqualTo("OWNED"));
            Assert.That(Label("HomePage", "RecordValue"), Is.EqualTo("W 0  D 0  L 0"));

            RectTransform battle = (RectTransform)FindIn("HomePage", "BATTLEButton").transform;
            RectTransform gacha = (RectTransform)FindIn("HomePage", "GACHAButton").transform;

            Assert.That(WorldRect(battle).height, Is.GreaterThan(WorldRect(gacha).height), "BATTLE を主ボタンとして最も大きく見せます。");
            Assert.That(WorldRect(battle).width, Is.GreaterThan(WorldRect(gacha).width));

            AssertPageLayout("HomePage");

            yield return null;

            Assert.That(missingCharacterWarnings, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EveryPageFitsWithoutTruncationOrOverlapOnIPhone12Mini()
        {
            yield return UsePortraitPhone();

            Click("HomePage", "GACHAButton");
            yield return null;
            AssertPageLayout("GachaPage");

            Click("GachaPage", "ACTIVATE100Button");
            yield return WaitForPage("AcquisitionPage");
            AssertPageLayout("AcquisitionPage");

            Click("AcquisitionPage", "TOCOLLECTIONButton");
            yield return null;
            AssertPageLayout("CollectionPage");

            GameFlowState.AddPendingReward(BattleRewardOutcome.Win, GameEconomy.WinReward);
            PlayerProfileProvider.Get(LoadRoster()).RecordBattle(BattleRewardOutcome.Win, GameEconomy.WinReward);
            yield return LoadHome();
            yield return UsePortraitPhone();
            AssertPageLayout("RewardPage");

            Assert.That(missingCharacterWarnings, Is.Empty);
        }

        // ---------------- 遷移 ----------------

        [UnityTest]
        public IEnumerator HomeGachaAcquisitionCollectionAndBackHome()
        {
            Click("HomePage", "GACHAButton");
            yield return null;
            AssertOnlyPage("GachaPage");
            Assert.That(Label("GachaPage", "PoolStatus"), Is.EqualTo("NEW GUARANTEED"));
            Assert.That(Label("GachaPage", "CostValue"), Is.EqualTo("100"));

            Click("GachaPage", "ACTIVATE100Button");
            yield return null;

            Assert.That(PlayerProfileProvider.Get(LoadRoster()).Coins, Is.Zero, "結果は開始時に確定・保存します。");

            yield return WaitForPage("AcquisitionPage");
            AssertOnlyPage("AcquisitionPage");
            Assert.That(Label("AcquisitionPage", "AcquisitionTitle"), Is.EqualTo("NEW CORE BEAST"));
            Assert.That(FindIn("AcquisitionPage", "TOUNITSETButton").activeSelf, Is.True, "新規獲得なら UNIT SET への導線を出します。");

            Click("AcquisitionPage", "TOCOLLECTIONButton");
            yield return null;
            AssertOnlyPage("CollectionPage");

            Click("CollectionPage", "HOMEButton");
            yield return null;
            AssertOnlyPage("HomePage");
            Assert.That(Label("HomePage", "CoinValue"), Is.EqualTo("0"), "Home へ戻ると残高を読み直します。");
            Assert.That(Label("HomePage", "OwnedValue"), Is.EqualTo("8 / 8"));
        }

        [UnityTest]
        public IEnumerator AcquisitionLeadsToUnitSet()
        {
            Click("HomePage", "GACHAButton");
            yield return null;
            Click("GachaPage", "ACTIVATE100Button");
            yield return WaitForPage("AcquisitionPage");

            Click("AcquisitionPage", "TOUNITSETButton");

            float started = Time.realtimeSinceStartup;

            while (SceneManager.GetActiveScene().name != "UnitSet")
            {
                yield return null;
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(SummonLimitSeconds), "UnitSet へ移りません。");
            }

            Assert.That(PlayerProfileProvider.Get(LoadRoster()).Beasts.Count, Is.EqualTo(8), "編成画面からも獲得した個体を使えます。");
        }

        // ---------------- 報酬 ----------------

        [UnityTest]
        public IEnumerator ReturningFromBattleOpensTheRewardOnceAndLeadsToGacha()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(LoadRoster());
            profile.RecordBattle(BattleRewardOutcome.Win, GameEconomy.WinReward);
            GameFlowState.AddPendingReward(BattleRewardOutcome.Win, GameEconomy.WinReward);

            yield return LoadHome();

            AssertOnlyPage("RewardPage");
            Assert.That(Label("RewardPage", "OutcomeLabel"), Is.EqualTo("WIN"));
            Assert.That(Label("RewardPage", "EarnedLabel"), Is.EqualTo("+30 CORE COIN"));
            Assert.That(Label("RewardPage", "BalanceBefore"), Is.EqualTo("100"));
            Assert.That(Label("RewardPage", "BalanceAfter"), Is.EqualTo("130"));
            Assert.That(profile.Coins, Is.EqualTo(130), "表示しても加算し直しません。");

            Click("RewardPage", "TOGACHAButton");
            yield return null;
            AssertOnlyPage("GachaPage");

            yield return LoadHome();
            AssertOnlyPage("HomePage");
            Assert.That(PlayerProfileProvider.Get(LoadRoster()).Coins, Is.EqualTo(130));
        }

        [UnityTest]
        public IEnumerator ARewardShortOfTheGachaCostLocksTheGachaAndSaysWhy()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(LoadRoster());
            Assert.That(profile.TrySpendCoins(50), Is.True);
            profile.RecordBattle(BattleRewardOutcome.Loss, GameEconomy.LossReward);
            GameFlowState.AddPendingReward(BattleRewardOutcome.Loss, GameEconomy.LossReward);

            yield return LoadHome();

            AssertOnlyPage("RewardPage");
            Assert.That(Label("RewardPage", "OutcomeLabel"), Is.EqualTo("LOSS"));
            Assert.That(FindIn("RewardPage", "TOGACHAButton").GetComponent<Button>().interactable, Is.False);
            Assert.That(Label("RewardPage", "GachaReason"), Is.EqualTo("NEED 40 MORE CORE COIN"));

            Click("RewardPage", "CONTINUEButton");
            yield return null;
            AssertOnlyPage("HomePage");
        }

        // ---------------- ガチャ ----------------

        [UnityTest]
        public IEnumerator ShortOfCoinsTheGachaCannotRun()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(LoadRoster());
            Assert.That(profile.TrySpendCoins(40), Is.True);

            Click("HomePage", "GACHAButton");
            yield return null;

            Button activate = FindIn("GachaPage", "ACTIVATE100Button").GetComponent<Button>();

            Assert.That(activate.interactable, Is.False);
            Assert.That(Label("GachaPage", "ActivateReason"), Is.EqualTo("NEED 40 MORE CORE COIN"));

            // 押せない状態でも、呼ばれたときに何も起きないこと。
            activate.onClick.Invoke();
            yield return null;

            Assert.That(profile.Coins, Is.EqualTo(60));
            Assert.That(profile.Beasts.Count, Is.EqualTo(7));
            AssertOnlyPage("GachaPage");
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GachaSummonView>().IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator MashingActivateSpendsOnlyOneHundred()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(LoadRoster());
            profile.AddCoins(200);

            Click("HomePage", "GACHAButton");
            yield return null;

            Button activate = FindIn("GachaPage", "ACTIVATE100Button").GetComponent<Button>();

            for (int i = 0; i < 6; i++)
            {
                activate.onClick.Invoke();
            }

            Assert.That(profile.Coins, Is.EqualTo(200), "連打しても1回ぶんしか消費しません。");
            Assert.That(FindIn("GachaPage", "SummonInputBlocker").activeSelf, Is.True, "演出中は入力を閉じます。");
            Assert.That(activate.interactable, Is.False);

            yield return null;

            activate.onClick.Invoke();
            Click("GachaPage", "BACKButton", requireInteractable: false);
            AssertOnlyPage("GachaPage");

            yield return WaitForPage("AcquisitionPage");

            Assert.That(profile.Coins, Is.EqualTo(200));
            Assert.That(FindIn("GachaPage", "SummonInputBlocker").activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator NewThenDuplicateAndThePortraitTakesTheAttributeColours()
        {
            CoreBeastRoster roster = LoadRoster();
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.AddCoins(400);
            AttributePalette palette = LoadPalette();
            HashSet<Color> colours = new HashSet<Color>();

            for (int pull = 0; pull < 5; pull++)
            {
                if (pull > 0)
                {
                    Click("AcquisitionPage", "HOMEButton");
                    yield return null;
                }

                Click("HomePage", "GACHAButton");
                yield return null;
                Click("GachaPage", "ACTIVATE100Button");
                yield return WaitForPage("AcquisitionPage");

                string title = Label("AcquisitionPage", "AcquisitionTitle");
                Assert.That(title, Is.EqualTo(pull == 0 ? "NEW CORE BEAST" : "DUPLICATE"), "pull " + pull);

                if (pull > 0)
                {
                    int before = int.Parse(Label("AcquisitionPage", "CopiesBefore"));
                    int after = int.Parse(Label("AcquisitionPage", "CopiesAfter"));

                    Assert.That(after, Is.EqualTo(before + 1), "COPIES は1つ増えます。");
                    Assert.That(FindIn("AcquisitionPage", "TOUNITSETButton").activeSelf, Is.False, "重複では編成への導線を出しません。");
                }

                BeastThumbnailView portrait = FindIn("AcquisitionPage", "Portrait").GetComponent<BeastThumbnailView>();
                CoreBeastDefinition shown = FindDefinition(roster, Label("AcquisitionPage", "AcquisitionName"), Label("AcquisitionPage", "PowerValue"));
                Color expected = palette.GetColors(shown.PrimaryAttribute).PrimaryColor;

                Assert.That(portrait.PrimaryLayerColor.r, Is.EqualTo(expected.r).Within(0.01f), "立ち絵の一次属性の色");
                Assert.That(portrait.PrimaryLayerColor.g, Is.EqualTo(expected.g).Within(0.01f));
                Assert.That(portrait.PrimaryLayerColor.b, Is.EqualTo(expected.b).Within(0.01f));

                colours.Add(new Color(expected.r, expected.g, expected.b));
            }

            Assert.That(profile.Coins, Is.EqualTo(0));
            TestContext.WriteLine("portrait colours seen: " + colours.Count);
            Assert.That(missingCharacterWarnings, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FxOffShowsOnlyAShortTextFade()
        {
            PresentationSettings.FxEnabled = false;

            Click("HomePage", "GACHAButton");
            yield return null;

            GachaSummonView view = UnityEngine.Object.FindAnyObjectByType<GachaSummonView>();
            Click("GachaPage", "ACTIVATE100Button");

            float started = Time.realtimeSinceStartup;
            bool drew = false;

            while (!FindIn("AcquisitionPage").activeSelf)
            {
                drew |= view.Graphic.IsShowing;
                yield return null;
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(SummonLimitSeconds));
            }

            Assert.That(drew, Is.False, "FX OFF では図形を出しません。");
            Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(GachaSummonPlan.FxDuration), "FX OFF は短縮します。");
        }

        // ---------------- 中断 ----------------

        [UnityTest]
        public IEnumerator DisablingDuringTheSummonLeavesNoEffectOrInputBlocker()
        {
            Click("HomePage", "GACHAButton");
            yield return null;
            Click("GachaPage", "ACTIVATE100Button");

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            GachaSummonView view = UnityEngine.Object.FindAnyObjectByType<GachaSummonView>();
            Assert.That(view.IsPlaying, Is.True);

            GameObject controller = GameObject.Find("HomeController");
            controller.SetActive(false);

            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.Graphic.IsShowing, Is.False);
            Assert.That(FindIn("GachaPage", "SummonInputBlocker").activeSelf, Is.False);

            controller.SetActive(true);
            yield return null;

            AssertOnlyPage("HomePage");
            Assert.That(PlayerProfileProvider.Get(LoadRoster()).Beasts.Count, Is.EqualTo(8), "確定した結果はそのまま残ります。");

            Click("HomePage", "COLLECTIONButton");
            yield return null;
            AssertOnlyPage("CollectionPage");
        }

        [UnityTest]
        public IEnumerator ReloadingTheSceneDuringTheSummonLeavesNothingBehind()
        {
            Click("HomePage", "GACHAButton");
            yield return null;
            Click("GachaPage", "ACTIVATE100Button");
            yield return null;

            yield return LoadHome();

            AssertOnlyPage("HomePage");
            Assert.That(FindIn("GachaPage").GetComponent<GachaSummonView>().IsPlaying, Is.False, "非表示のガチャ画面でも演出は動いていません。");
            Assert.That(FindIn("GachaPage", "SummonInputBlocker").activeSelf, Is.False);
            Assert.That(FindIn("AcquisitionPage").activeSelf, Is.False, "再読み込みで獲得画面を勝手に出しません。");
            Assert.That(Label("HomePage", "CoinValue"), Is.EqualTo("0"));

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ARestartEquivalentReloadKeepsTheProfileAndSquad()
        {
            PlayerProfileProvider.SetRepository(new PlayerPrefsPlayerProfileRepository(ProfileKey));
            SquadRepositoryProvider.SetShared(new PlayerPrefsSquadRepository(SquadKey));

            yield return LoadHome();

            Click("HomePage", "GACHAButton");
            yield return null;
            Click("GachaPage", "ACTIVATE100Button");
            yield return WaitForPage("AcquisitionPage");

            // 起動し直したのと同じく、保存済みの JSON から読み直します。
            PlayerProfileProvider.SetRepository(new PlayerPrefsPlayerProfileRepository(ProfileKey));
            SquadRepositoryProvider.SetShared(new PlayerPrefsSquadRepository(SquadKey));

            yield return LoadHome();

            AssertOnlyPage("HomePage");
            Assert.That(Label("HomePage", "CoinValue"), Is.EqualTo("0"));
            Assert.That(Label("HomePage", "OwnedValue"), Is.EqualTo("8 / 8"));
            Assert.That(Label("HomePage", "SquadValue"), Is.EqualTo("SET 1"));
            Assert.That(SquadRepositoryProvider.Shared.TryLoad("1", out SquadSnapshot saved), Is.True, "編成は保存されたままです。");
            Assert.That(saved, Is.Not.Null);
        }

        // ---------------- 繰り返し ----------------

        [UnityTest]
        public IEnumerator TenRoundTripsCreateNoGameObjectsOrComponents()
        {
            PlayerProfileProvider.Get(LoadRoster()).AddCoins(1000);

            int objects = -1;
            int components = -1;

            for (int trip = 0; trip < 11; trip++)
            {
                Click("HomePage", "GACHAButton");
                yield return null;
                Click("GachaPage", "ACTIVATE100Button");
                yield return WaitForPage("AcquisitionPage");
                Click("AcquisitionPage", "TOCOLLECTIONButton");
                yield return null;
                Click("CollectionPage", "HOMEButton");
                yield return null;
                Click("HomePage", "SETTINGSButton");
                yield return null;
                Click("SettingsOverlay", "CLOSEButton");
                yield return null;

                if (trip == 0)
                {
                    objects = Count<Transform>();
                    components = Count<Component>();
                    continue;
                }

                Assert.That(Count<Transform>(), Is.EqualTo(objects), "trip " + trip + " で GameObject が増えました。");
                Assert.That(Count<Component>(), Is.EqualTo(components), "trip " + trip + " で Component が増えました。");
            }

            Assert.That(missingCharacterWarnings, Is.Empty);
        }

        // ---------------- 検査 ----------------

        private static void AssertOnlyPage(string page)
        {
            foreach (string name in Pages)
            {
                Assert.That(FindIn(name).activeSelf, Is.EqualTo(name == page), name + " の表示（主要な画面は1つだけ）");
            }
        }

        /// <summary>画面のボタンと文字が SafeArea の中にあり、切れず、ボタンどうしが重ならず、押せる大きさ（44pt）であること。</summary>
        private static void AssertPageLayout(string page)
        {
            AssertOnlyPage(page);
            Canvas.ForceUpdateCanvases();

            RectTransform safe = (RectTransform)GameObject.Find("SafeArea").transform;
            Rect safeRect = WorldRect(safe);
            CanvasScaler scaler = GameObject.Find("Canvas").GetComponent<CanvasScaler>();
            float unitsPer44pt = 44f * 3f / PixelsPerUnit(scaler);
            float worldPerUnit = WorldRect((RectTransform)GameObject.Find("Canvas").transform).width / ((RectTransform)GameObject.Find("Canvas").transform).rect.width;

            List<Rect> buttons = new List<Rect>();

            foreach (Button button in FindIn(page).GetComponentsInChildren<Button>(false))
            {
                Rect r = WorldRect((RectTransform)button.transform);

                Assert.That(Contains(safeRect, r), Is.True, page + "/" + button.name + " が SafeArea の外にあります。");
                Assert.That(r.width / worldPerUnit, Is.GreaterThanOrEqualTo(unitsPer44pt - 0.5f), button.name + " の幅が44pt未満です。");
                Assert.That(r.height / worldPerUnit, Is.GreaterThanOrEqualTo(unitsPer44pt - 0.5f), button.name + " の高さが44pt未満です。");

                foreach (Rect other in buttons)
                {
                    Assert.That(r.Overlaps(other), Is.False, page + "/" + button.name + " がほかのボタンに重なっています。");
                }

                buttons.Add(r);
            }

            foreach (TMP_Text label in FindIn(page).GetComponentsInChildren<TMP_Text>(false))
            {
                label.ForceMeshUpdate();

                if (string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                Assert.That(label.isTextTruncated, Is.False, page + "/" + label.name + " が切れています: " + label.text);
                Assert.That(label.isTextOverflowing, Is.False, page + "/" + label.name + " が溢れています: " + label.text);
                Assert.That(Contains(safeRect, WorldRect(label.rectTransform)), Is.True, page + "/" + label.name + " が SafeArea の外にあります。");
            }
        }

        // ---------------- 道具 ----------------

        private IEnumerator LoadHome()
        {
            yield return SceneManager.LoadSceneAsync("Home", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator WaitForPage(string page)
        {
            float started = Time.realtimeSinceStartup;

            while (!FindIn(page).activeSelf)
            {
                yield return null;
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(SummonLimitSeconds), page + " へ移りません。");
            }
        }

        /// <summary>Home の Canvas を iPhone 12 mini（1080 x 2340 px、安全余白 上50pt / 下34pt）の縦画面と同じ大きさにします。</summary>
        private static IEnumerator UsePortraitPhone()
        {
            const float width = 1080f;
            const float height = 2340f;

            Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float pixelsPerUnit = PixelsPerUnit(scaler);

            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(width / pixelsPerUnit, height / pixelsPerUnit);

            RectTransform safe = (RectTransform)GameObject.Find("SafeArea").transform;
            safe.anchorMin = new Vector2(0f, 34f * 3f / height);
            safe.anchorMax = new Vector2(1f, 1f - 50f * 3f / height);

            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(WorldRect(safe).height, Is.GreaterThan(WorldRect(safe).width), "縦画面になっていません。");
        }

        private static float PixelsPerUnit(CanvasScaler scaler)
        {
            float logWidth = Mathf.Log(1080f / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(2340f / scaler.referenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        }

        private static void Click(string page, string name, bool requireInteractable = true)
        {
            GameObject target = FindIn(page, name);
            Button button = target.GetComponent<Button>();

            Assert.That(button, Is.Not.Null, name);
            Assert.That(target.activeInHierarchy, Is.True, page + "/" + name + " が表示されていません。");

            if (requireInteractable)
            {
                Assert.That(button.interactable, Is.True, page + "/" + name + " が押せません。");
            }

            button.onClick.Invoke();
        }

        private static string Label(string page, string name)
        {
            return FindIn(page, name).GetComponent<TMP_Text>().text;
        }

        private static GameObject FindIn(string page, string name = null)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name != page || !candidate.scene.IsValid())
                {
                    continue;
                }

                if (name == null)
                {
                    return candidate;
                }

                foreach (Transform t in candidate.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t.gameObject;
                    }
                }
            }

            Assert.Fail(page + "/" + name + " が見つかりません。");
            return null;
        }

        private static CoreBeastDefinition FindDefinition(CoreBeastRoster roster, string displayName, string power)
        {
            foreach (OwnedCoreBeast beast in roster.Owned)
            {
                if (beast.Definition.DisplayName == displayName && AcquisitionSummary.PowersOf(beast.Definition) == power)
                {
                    return beast.Definition;
                }
            }

            Assert.Fail(displayName + " " + power + " の定義が見つかりません。");
            return null;
        }

        private static CoreBeastRoster LoadRoster()
        {
            foreach (CoreBeastRoster roster in Resources.FindObjectsOfTypeAll<CoreBeastRoster>())
            {
                if (roster != null && roster.Owned.Count == 8)
                {
                    return roster;
                }
            }

            Assert.Fail("8体のテスト用Rosterが見つかりません。");
            return null;
        }

        private static AttributePalette LoadPalette()
        {
            foreach (AttributePalette palette in Resources.FindObjectsOfTypeAll<AttributePalette>())
            {
                if (palette != null && palette.name == "AttributePalette_Default")
                {
                    return palette;
                }
            }

            Assert.Fail("AttributePalette_Default が見つかりません。");
            return null;
        }

        private static int Count<T>() where T : Component
        {
            int count = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            const float Epsilon = 0.5f;

            return inner.xMin >= outer.xMin - Epsilon && inner.xMax <= outer.xMax + Epsilon &&
                   inner.yMin >= outer.yMin - Epsilon && inner.yMax <= outer.yMax + Epsilon;
        }

        private void CollectMissingCharacters(string condition, string stackTrace, LogType type)
        {
            if (condition != null && condition.Contains("was not found in the"))
            {
                missingCharacterWarnings.Add(condition);
            }
        }
    }
}
