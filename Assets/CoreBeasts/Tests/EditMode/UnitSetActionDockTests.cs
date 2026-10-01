using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 親指操作を基準にした下部Action Dockの寸法と配置。
    ///
    /// 画面の基準は iPhone 16 Pro（<see cref="MobileLayoutMetrics"/>）です。
    /// 数値を直接書かず参照解像度から換算するため、
    /// CanvasScalerを変えてもテストの意味が変わりません。
    /// </summary>
    public sealed class UnitSetActionDockTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";

        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            // Additive だと、直前のテストが開いたシーンと同居して
            // Global Light 2D が2つになり、URP 2D が Error を出します。
            // NUnit は想定外の Error ログを失敗として扱うため、常に単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
        }

        [TearDown]
        public void TearDown()
        {
            // 単独で開いているときは閉じられません（唯一のシーンは閉じられない）。
            // 次の SetUp が Single で開き直すので、ここでは残しておきます。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private T FindOne<T>() where T : Component
        {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(
                found.Count,
                Is.EqualTo(1),
                typeof(T).Name + " はシーンに1個だけ存在する必要があります。");

            return found[0];
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private CanvasScaler Scaler()
        {
            return FindOne<CanvasScaler>();
        }

        private RectTransform Dock()
        {
            Button save = (Button)GetField(FindOne<UnitSetScreen>(), "saveButton");
            RectTransform dock = save.transform.parent as RectTransform;

            Assert.That(dock, Is.Not.Null);
            Assert.That(dock.name, Is.EqualTo("ActionDock"));

            return dock;
        }

        private Button Home()
        {
            return Dock().GetComponentInChildren<SceneLoadButton>(true)
                .GetComponent<Button>();
        }

        private Button Save()
        {
            return (Button)GetField(FindOne<UnitSetScreen>(), "saveButton");
        }

        /// <summary>Dockの内側で、この矩形が占める横幅（Canvas単位）。</summary>
        private float WidthInDock(RectTransform rect)
        {
            RectTransform dock = Dock();

            float dockWidth = MobileLayoutMetrics.SafeAreaWidthUnits(Scaler())
                              + dock.sizeDelta.x;

            return (rect.anchorMax.x - rect.anchorMin.x) * dockWidth + rect.sizeDelta.x;
        }

        private float DockWidth()
        {
            return MobileLayoutMetrics.SafeAreaWidthUnits(Scaler()) + Dock().sizeDelta.x;
        }

        // ---------------- SET は操作ではなく状態表示 ----------------

        [Test]
        public void TheSetLabelIsAStatusChipAndNotAButton()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            RectTransform chip = (RectTransform)GetField(screen, "setChip");

            Assert.That(chip, Is.Not.Null, "SET のチップが未設定です。");

            Assert.That(
                chip.GetComponent<Button>(),
                Is.Null,
                "セット切替は未実装のため、SET は押せる見た目にしません。");

            Assert.That(
                chip.GetComponentsInChildren<Selectable>(true),
                Is.Empty,
                "SET のチップ配下に操作を置きません。");

            // 将来ボタン化できるよう、根と表示は残しておきます。
            TMP_Text label = (TMP_Text)GetField(screen, "setNameLabel");

            Assert.That(
                label.transform.IsChildOf(chip),
                Is.True,
                "SET の表示はチップの中に置きます。");
        }

        [Test]
        public void TheSetChipSitsBesideTheMySquadHeadingNotInTheDock()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            RectTransform chip = (RectTransform)GetField(screen, "setChip");
            RectTransform dock = Dock();

            Assert.That(
                chip.IsChildOf(dock),
                Is.False,
                "SET は下部Dockから外します。Dockは頻繁な操作だけにします。");

            CanvasScaler scaler = Scaler();
            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            TMP_Text heading = (TMP_Text)GetField(screen, "squadHeadingLabel");

            Vector2 headingSpan = MobileLayoutMetrics.VerticalSpan(
                heading.rectTransform, safeHeight);

            Vector2 chipSpan = MobileLayoutMetrics.VerticalSpan(chip, safeHeight);

            // 見出しと同じ帯に並ぶこと（縦に重なりがあること）。
            Assert.That(
                chipSpan.x,
                Is.LessThan(headingSpan.y),
                "SET のチップが MY SQUAD 見出しの帯から外れています。");

            Assert.That(chipSpan.y, Is.GreaterThan(headingSpan.x));

            // 見出しの右側に置くこと。
            Assert.That(
                chip.anchorMin.x,
                Is.EqualTo(1f),
                "SET のチップは見出しの右端へ寄せます。");
        }

        [Test]
        public void TheSetChipDoesNotLookPressable()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            RectTransform chip = (RectTransform)GetField(screen, "setChip");

            Image fill = chip.GetComponent<Image>();
            Image save = Save().GetComponent<Image>();
            Image home = Home().GetComponent<Image>();

            Assert.That(fill, Is.Not.Null);

            float chipWeight = fill.color.r + fill.color.g + fill.color.b;

            Assert.That(
                chipWeight,
                Is.LessThan(save.color.r + save.color.g + save.color.b),
                "SET の背景が Primary 操作より目立っています。");

            Assert.That(
                chipWeight,
                Is.LessThanOrEqualTo(home.color.r + home.color.g + home.color.b),
                "SET の背景が Secondary 操作より目立っています。");
        }

        // ---------------- Dock の寸法 ----------------

        [Test]
        public void TheDockHoldsExactlyHomeAndSaveSet()
        {
            RectTransform dock = Dock();

            Button[] buttons = dock.GetComponentsInChildren<Button>(true);

            Assert.That(
                buttons.Length,
                Is.EqualTo(2),
                "下部へ集約するのは HOME と SAVE SET の2つだけです。");

            Assert.That(Home().transform.parent, Is.SameAs(dock));
            Assert.That(Save().transform.parent, Is.SameAs(dock));
        }

        [Test]
        public void SaveSetTakesTwoThirdsAndHomeTakesAboutThirty()
        {
            float dockWidth = DockWidth();

            float home = WidthInDock(Home().GetComponent<RectTransform>());
            float save = WidthInDock(Save().GetComponent<RectTransform>());

            Assert.That(
                home / dockWidth,
                Is.InRange(0.28f, 0.32f),
                "HOME の横幅が 28〜32% の範囲外です。");

            Assert.That(
                save / dockWidth,
                Is.InRange(0.64f, 0.68f),
                "SAVE SET の横幅が 64〜68% の範囲外です。");

            Assert.That(
                save,
                Is.GreaterThan(home),
                "SAVE SET は HOME より広く出します。");
        }

        [Test]
        public void TheTwoButtonsKeepAComfortableGap()
        {
            float dockWidth = DockWidth();
            float points = MobileLayoutMetrics.PointsPerUnit(Scaler());

            RectTransform home = Home().GetComponent<RectTransform>();
            RectTransform save = Save().GetComponent<RectTransform>();

            float homeRight = home.anchorMin.x * dockWidth
                              + home.anchoredPosition.x
                              + WidthInDock(home);

            float saveLeft = save.anchorMin.x * dockWidth + save.anchoredPosition.x;

            Assert.That(
                (saveLeft - homeRight) * points,
                Is.InRange(12f, 16f),
                "ボタン間隔が 12〜16pt の範囲外です。");
        }

        [Test]
        public void BothButtonsAreTallEnoughForAThumb()
        {
            float points = MobileLayoutMetrics.PointsPerUnit(Scaler());

            Button[] buttons = Dock().GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();

                Assert.That(
                    rect.sizeDelta.y * points,
                    Is.GreaterThanOrEqualTo(48f),
                    rect.name + " のタップ高さが 48pt 未満です。");

                Assert.That(
                    rect.sizeDelta.y * points,
                    Is.InRange(52f, 56f),
                    rect.name + " の高さは 52〜56pt へ収めます。");
            }
        }

        [Test]
        public void TheDockClearsTheHomeIndicator()
        {
            CanvasScaler scaler = Scaler();

            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);
            float points = MobileLayoutMetrics.PointsPerUnit(scaler);

            Vector2 span = MobileLayoutMetrics.VerticalSpan(Dock(), safeHeight);

            Assert.That(
                span.x * points,
                Is.GreaterThanOrEqualTo(12f),
                "Dock が Safe Area 下端から 12pt 以上離れていません。");
        }

        // ---------------- 重なりと Roster ----------------

        [Test]
        public void TheDockSquadRowAndRosterNeverOverlap()
        {
            CanvasScaler scaler = Scaler();
            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            UnitSetScreen screen = FindOne<UnitSetScreen>();

            Vector2 dock = MobileLayoutMetrics.VerticalSpan(Dock(), safeHeight);

            Vector2 squad = MobileLayoutMetrics.VerticalSpan(
                FindOne<SquadBarView>().GetComponent<RectTransform>(), safeHeight);

            Vector2 heading = MobileLayoutMetrics.VerticalSpan(
                ((TMP_Text)GetField(screen, "squadHeadingLabel")).rectTransform,
                safeHeight);

            Vector2 chip = MobileLayoutMetrics.VerticalSpan(
                (RectTransform)GetField(screen, "setChip"), safeHeight);

            Vector2 roster = MobileLayoutMetrics.VerticalSpan(
                FindOne<RosterGridView>().GetComponent<RectTransform>(), safeHeight);

            Assert.That(
                squad.x, Is.GreaterThanOrEqualTo(dock.y),
                "Action Dock が MY SQUAD の7枠へ重なっています。");

            Assert.That(
                heading.x, Is.GreaterThanOrEqualTo(squad.y),
                "MY SQUAD 見出しが7枠へ重なっています。");

            Assert.That(
                roster.x, Is.GreaterThanOrEqualTo(heading.y),
                "Roster が MY SQUAD 見出しへ重なっています。");

            Assert.That(
                roster.x, Is.GreaterThanOrEqualTo(chip.y),
                "Roster が SET のチップへ重なっています。");
        }

        [Test]
        public void TheRosterGotTallerThanTheOldTwoRowDock()
        {
            CanvasScaler scaler = Scaler();
            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            Vector2 roster = MobileLayoutMetrics.VerticalSpan(
                FindOne<RosterGridView>().GetComponent<RectTransform>(), safeHeight);

            // 2段Dock（280u）だったころの Roster は 578u でした。
            Assert.That(
                roster.y - roster.x,
                Is.GreaterThan(578f),
                "1段Dockで空いた高さが Roster へ回っていません。");
        }

        // ---------------- 既存配線 ----------------

        [Test]
        public void SavingAndNavigationKeepTheirExistingWiring()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            Button save = Save();

            Assert.That(
                save.onClick.GetPersistentEventCount(),
                Is.EqualTo(0),
                "保存の呼び出しはコード側から登録します。");

            TMP_Text saveLabel = (TMP_Text)GetField(screen, "saveButtonLabel");

            Assert.That(saveLabel.GetComponentInParent<Button>(), Is.SameAs(save));

            SceneLoadButton navigation =
                Dock().GetComponentInChildren<SceneLoadButton>(true);

            Assert.That((string)GetField(navigation, "sceneName"), Is.EqualTo("Home"));
            Assert.That(
                (Button)GetField(navigation, "button"),
                Is.SameAs(Home()),
                "HOME遷移の配線が外れています。");

            Assert.That(
                FindOne<ToastLabel>(),
                Is.Not.Null,
                "保存完了メッセージの表示先が必要です。");
        }
    }
}
