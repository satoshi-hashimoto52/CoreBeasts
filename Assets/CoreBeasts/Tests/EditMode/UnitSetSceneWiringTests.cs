using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// UnitSet.unity の配線をシーンごと開いて検証します。
    /// PrefabやSceneを再生成してfileIDが動いた場合、ここで参照切れを検出します。
    /// </summary>
    public sealed class UnitSetSceneWiringTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";
        private const string SlotPrefabPath = "Assets/CoreBeasts/Prefabs/SquadSlot.prefab";
        private const string GhostPrefabPath = "Assets/CoreBeasts/Prefabs/DragGhost.prefab";
        private const int ExpectedOwnedCount = 8;

        private Scene scene;
        private readonly List<GameObject> spawned = new List<GameObject>();

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
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();

            // 最後の1枚を閉じると Unity が
            // 「Unloading the last loaded scene ... is not supported」を出します。
            // 次の SetUp が Single で開き直すので、1枚だけのときは残しておきます。
            // 保存はしないため、テスト操作はシーンへ残りません。
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

        private static void AssertAssigned(object target, params string[] names)
        {
            foreach (string name in names)
            {
                object value = GetField(target, name);
                Object unityObject = value as Object;
                bool missing = unityObject != null ? unityObject == null : value == null;

                Assert.That(
                    missing,
                    Is.False,
                    $"{target.GetType().Name}.{name} が未設定です。");
            }
        }

        [Test]
        public void RosterGridView_ExistsOnceAndIsFullyWired()
        {
            RosterGridView grid = FindOne<RosterGridView>();

            AssertAssigned(grid, "content", "cardPrefab");

            RectTransform content = (RectTransform)GetField(grid, "content");
            Assert.That(content.name, Is.EqualTo("Content"));
        }

        [Test]
        public void ScrollRect_PointsAtViewportAndContent()
        {
            RosterGridView grid = FindOne<RosterGridView>();
            ScrollRect scroll = grid.GetComponent<ScrollRect>();

            Assert.That(scroll, Is.Not.Null, "RosterScrollにScrollRectがありません。");
            Assert.That(scroll.content, Is.Not.Null);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(scroll.content.name, Is.EqualTo("Content"));
            Assert.That(scroll.viewport.name, Is.EqualTo("Viewport"));
            Assert.That(scroll.vertical, Is.True, "縦スクロールが有効である必要があります。");
            Assert.That(
                scroll.content,
                Is.SameAs(GetField(grid, "content")),
                "ScrollRect.contentとRosterGridView.contentは同じである必要があります。");
        }

        [Test]
        public void Prefabs_AreValidAndReferencedByTheScene()
        {
            BeastCardView cardAsset =
                AssetDatabase.LoadAssetAtPath<BeastCardView>(CardPrefabPath);
            SquadSlotView slotAsset =
                AssetDatabase.LoadAssetAtPath<SquadSlotView>(SlotPrefabPath);
            DragGhostView ghostAsset =
                AssetDatabase.LoadAssetAtPath<DragGhostView>(GhostPrefabPath);

            Assert.That(cardAsset, Is.Not.Null, CardPrefabPath + " が読めません。");
            Assert.That(slotAsset, Is.Not.Null, SlotPrefabPath + " が読めません。");
            Assert.That(ghostAsset, Is.Not.Null, GhostPrefabPath + " が読めません。");

            // シーンが参照しているのが、その資産そのものであること
            Assert.That(
                GetField(FindOne<RosterGridView>(), "cardPrefab"),
                Is.SameAs(cardAsset),
                "cardPrefabがBeastCard.prefabを指していません（fileIDのずれ）。");

            Assert.That(
                GetField(FindOne<SquadBarView>(), "slotPrefab"),
                Is.SameAs(slotAsset),
                "slotPrefabがSquadSlot.prefabを指していません（fileIDのずれ）。");

            Assert.That(
                GetField(FindOne<DragGhostPresenter>(), "ghostPrefab"),
                Is.SameAs(ghostAsset),
                "ghostPrefabがDragGhost.prefabを指していません（fileIDのずれ）。");
        }

        [Test]
        public void Build_CreatesOneCardPerOwnedBeastWithoutErrors()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();
            RosterGridView grid = FindOne<RosterGridView>();

            CoreBeastRoster roster = (CoreBeastRoster)GetField(screen, "roster");
            AttributePalette palette = (AttributePalette)GetField(screen, "palette");
            UiTextCatalog text = (UiTextCatalog)GetField(screen, "text");

            Assert.That(roster, Is.Not.Null);
            Assert.That(roster.Owned.Count, Is.EqualTo(ExpectedOwnedCount));

            RectTransform content = (RectTransform)GetField(grid, "content");

            grid.Build(roster, palette, text, screen);

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }

            Assert.That(
                content.childCount,
                Is.EqualTo(ExpectedOwnedCount),
                "テストロースター8体分のカードが生成される必要があります。");

            for (int i = 0; i < content.childCount; i++)
            {
                BeastCardView card =
                    content.GetChild(i).GetComponent<BeastCardView>();

                Assert.That(card, Is.Not.Null);
                AssertAssigned(card,
                    "liftView", "background", "selectionFrame", "thumbnail",
                    "attributeSurface", "nameLabel",
                    "levelLabel", "squadBadge");

                CardLiftView lift = (CardLiftView)GetField(card, "liftView");
                AssertAssigned(lift,
                    "visualRoot", "visualGroup", "dragShadow",
                    "dragGlow", "dragGlowImage");
            }
        }

        [Test]
        public void SquadBar_CreatesSevenFullyWiredSlots()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();
            SquadBarView bar = FindOne<SquadBarView>();

            UiTextCatalog text = (UiTextCatalog)GetField(screen, "text");
            RectTransform content = (RectTransform)GetField(bar, "content");

            bar.Build(text, screen);

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }

            Assert.That(
                content.childCount,
                Is.EqualTo(SquadFormation.SlotCount),
                "MY SQUADの7枠が生成される必要があります。");

            for (int i = 0; i < content.childCount; i++)
            {
                SquadSlotView slot =
                    content.GetChild(i).GetComponent<SquadSlotView>();

                Assert.That(slot, Is.Not.Null);
                AssertAssigned(slot,
                    "background", "frame", "thumbnail", "attributeChip",
                    "orderLabel", "attributeLabel", "emptyLabel");
            }
        }

        [Test]
        public void UnitSetScreen_AndRelatedViewsAreFullyWired()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            AssertAssigned(screen,
                "roster", "palette", "text",
                "detailPanel", "rosterGrid", "squadBar", "toast", "dragGhost",
                "homeButtonLabel", "screenTitleLabel", "setNameLabel",
                "rosterHeadingLabel", "squadHeadingLabel", "saveButtonLabel",
                "setChip", "saveButton");

            AssertAssigned(FindOne<BeastDetailPanel>(),
                "portraitView", "portraitRoot", "nameLabel", "levelLabel",
                "costLabel", "attributeLabel", "powerLabel", "coreLabel",
                "skillNameLabel", "skillDescriptionLabel",
                "attributeChip", "coreIcon", "palette", "text");

            AssertAssigned(FindOne<DragGhostPresenter>(),
                "ghostRoot", "ghostPrefab", "canvas");

            AssertAssigned(FindOne<PortraitRenderTarget>(),
                "portraitCamera", "targetImage");

            AssertAssigned(FindOne<ToastLabel>(), "canvasGroup", "label");
        }

        // ---------------- 下部 Action Dock ----------------

        /// <summary>
        /// Safe Area の内側かどうか。SafeAreaController は asmdef の外
        /// （Assembly-CSharp）にあるため、型ではなく名前で探します。
        /// </summary>
        private static bool IsInsideSafeArea(Transform target)
        {
            for (Transform node = target; node != null; node = node.parent)
            {
                Component[] components = node.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null &&
                        components[i].GetType().Name == "SafeAreaController")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Safe Area 直下の下部操作領域。</summary>
        private RectTransform ActionDock()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            Button save = (Button)GetField(screen, "saveButton");
            RectTransform dock = save.transform.parent as RectTransform;

            Assert.That(dock, Is.Not.Null);
            Assert.That(
                dock.name,
                Is.EqualTo("ActionDock"),
                "3つの操作は下部のAction Dockへまとめます。");

            return dock;
        }

        private RectTransform Header()
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform header = FindDeep(root.transform, "Header");

                if (header != null)
                {
                    return (RectTransform)header;
                }
            }

            Assert.Fail("Header が見つかりません。");
            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private CanvasScaler Scaler()
        {
            return FindOne<CanvasScaler>();
        }

        [Test]
        public void TopHeader_KeepsTheTitleAndDropsEveryControl()
        {
            RectTransform header = Header();

            Assert.That(
                header.GetComponentsInChildren<Selectable>(true),
                Is.Empty,
                "上部は情報表示だけにします。操作ボタンは置きません。");

            Assert.That(
                header.GetComponentsInChildren<SceneLoadButton>(true),
                Is.Empty,
                "上部のHOMEは撤去しました。");

            TMP_Text title = header.GetComponentInChildren<TMP_Text>(true);

            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Is.EqualTo("UNIT SET"));

            RectTransform titleRect = title.rectTransform;

            Assert.That(
                titleRect.anchoredPosition.x,
                Is.EqualTo(0f),
                "タイトルはヘッダー中央へ置きます。");
        }

        [Test]
        public void DetailPanel_StaysInTheUpperInformationArea()
        {
            BeastDetailPanel detail = FindOne<BeastDetailPanel>();

            RectTransform rect = detail.GetComponent<RectTransform>();

            Assert.That(
                rect.anchorMax.y,
                Is.EqualTo(1f),
                "ユニット詳細は上部の情報表示として残します。");

            Assert.That(
                rect.GetComponentsInChildren<Selectable>(true),
                Is.Empty,
                "詳細表示に操作ボタンは置きません。");
        }

        [Test]
        public void HomeAndSaveLiveUnderTheBottomActionDock()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();
            RectTransform dock = ActionDock();

            Button save = (Button)GetField(screen, "saveButton");

            Assert.That(save.transform.parent, Is.SameAs(dock));

            SceneLoadButton[] navigation =
                dock.GetComponentsInChildren<SceneLoadButton>(true);

            Assert.That(navigation.Length, Is.EqualTo(1));
            Assert.That((string)GetField(navigation[0], "sceneName"), Is.EqualTo("Home"));
            Assert.That(navigation[0].transform.parent, Is.SameAs(dock));

            Button home = (Button)GetField(navigation[0], "button");

            Assert.That(home, Is.Not.Null);
            Assert.That(home.gameObject, Is.SameAs(navigation[0].gameObject));

            // 親指で押す操作はHOMEとSAVE SETの2つだけです。
            // SET は操作ではなくなったため、Dockにはもう居ません。
            Assert.That(dock.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void TheDockKeepsTheExistingWiringForHomeAndSave()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            Button save = (Button)GetField(screen, "saveButton");

            // 呼び出しはコードから登録します。シーンへ固定の呼び出しは置きません。
            Assert.That(save.onClick.GetPersistentEventCount(), Is.EqualTo(0));

            // ラベルは、下部へ移した実物を指していること。
            TMP_Text saveLabel = (TMP_Text)GetField(screen, "saveButtonLabel");
            TMP_Text setLabel = (TMP_Text)GetField(screen, "setNameLabel");
            TMP_Text homeLabel = (TMP_Text)GetField(screen, "homeButtonLabel");

            RectTransform dock = ActionDock();
            RectTransform chip = (RectTransform)GetField(screen, "setChip");

            Assert.That(saveLabel.GetComponentInParent<Button>(), Is.SameAs(save));
            Assert.That(saveLabel.transform.IsChildOf(dock), Is.True);
            Assert.That(
                homeLabel.transform.IsChildOf(dock),
                Is.True,
                "HOMEのラベルも下部へ移します。");

            // SET の表示はDockを離れ、MY SQUAD見出しの右のチップへ移りました。
            Assert.That(setLabel.transform.IsChildOf(chip), Is.True);
            Assert.That(setLabel.transform.IsChildOf(dock), Is.False);
        }

        [Test]
        public void SaveSetIsThePrimaryActionAndTheOthersAreSecondary()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();
            RectTransform dock = ActionDock();

            Button save = (Button)GetField(screen, "saveButton");
            Button home = dock.GetComponentInChildren<SceneLoadButton>(true)
                .GetComponent<Button>();

            RectTransform saveRect = save.GetComponent<RectTransform>();
            RectTransform homeRect = home.GetComponent<RectTransform>();

            float dockWidth = MobileLayoutMetrics.SafeAreaWidthUnits(Scaler())
                              + dock.sizeDelta.x;

            float saveWidth = (saveRect.anchorMax.x - saveRect.anchorMin.x) * dockWidth
                              + saveRect.sizeDelta.x;

            float homeWidth = (homeRect.anchorMax.x - homeRect.anchorMin.x) * dockWidth
                              + homeRect.sizeDelta.x;

            Assert.That(
                saveWidth,
                Is.GreaterThan(homeWidth * 1.5f),
                "SAVE SET は最も強い操作として、HOMEより広く出します。");

            Color primary = save.GetComponent<Image>().color;
            Color secondary = home.GetComponent<Image>().color;

            Assert.That(
                primary,
                Is.Not.EqualTo(secondary),
                "Primary と Secondary は同じ色で出しません。");

            Assert.That(
                primary.b + primary.g,
                Is.GreaterThan(secondary.b + secondary.g),
                "SAVE SET のほうが目立つ色である必要があります。");
        }

        [Test]
        public void EveryDockButtonMeetsTheMinimumTapTarget()
        {
            RectTransform dock = ActionDock();
            CanvasScaler scaler = Scaler();

            float points = MobileLayoutMetrics.PointsPerUnit(scaler);
            Button[] buttons = dock.GetComponentsInChildren<Button>(true);

            Assert.That(buttons, Is.Not.Empty);

            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();

                Assert.That(
                    rect.sizeDelta.y * points,
                    Is.GreaterThanOrEqualTo(MobileLayoutMetrics.MinimumTapPoints),
                    rect.name + " の高さが 44pt 未満です。");

                Assert.That(
                    rect.sizeDelta.y * points,
                    Is.InRange(48f, 56f),
                    rect.name + " の高さは推奨の 48〜56pt へ収めます。");

                if (rect.anchorMin.x == rect.anchorMax.x)
                {
                    Assert.That(
                        rect.sizeDelta.x * points,
                        Is.GreaterThanOrEqualTo(MobileLayoutMetrics.MinimumTapPoints),
                        rect.name + " の幅が 44pt 未満です。");
                }
            }
        }

        [Test]
        public void TheDockSitsInsideTheBottomSafeAreaAndHidesNothing()
        {
            RectTransform dock = ActionDock();
            CanvasScaler scaler = Scaler();

            Assert.That(
                IsInsideSafeArea(dock),
                Is.True,
                "Action Dock は Safe Area の内側に置きます。");

            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            Vector2 dockSpan = MobileLayoutMetrics.VerticalSpan(dock, safeHeight);

            Assert.That(
                dockSpan.x,
                Is.GreaterThan(0f),
                "ホームインジケーターの上に余白を残します。");

            RectTransform squadRow =
                FindOne<SquadBarView>().GetComponent<RectTransform>();
            RectTransform roster =
                FindOne<RosterGridView>().GetComponent<RectTransform>();

            Vector2 squadSpan = MobileLayoutMetrics.VerticalSpan(squadRow, safeHeight);
            Vector2 rosterSpan = MobileLayoutMetrics.VerticalSpan(roster, safeHeight);

            Assert.That(
                squadSpan.x,
                Is.GreaterThanOrEqualTo(dockSpan.y),
                "Action Dock が MY SQUAD の7枠を隠しています。");

            Assert.That(
                rosterSpan.x,
                Is.GreaterThanOrEqualTo(squadSpan.y),
                "Action Dock 周りが Roster のスクロール領域へ重なっています。");

            Assert.That(
                rosterSpan.y - rosterSpan.x,
                Is.GreaterThan(0f),
                "Roster のスクロール領域が潰れています。");
        }

        [Test]
        public void TheSevenSquadSlotsStayVisibleAboveTheDock()
        {
            CanvasScaler scaler = Scaler();
            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            RectTransform squadRow =
                FindOne<SquadBarView>().GetComponent<RectTransform>();

            Vector2 span = MobileLayoutMetrics.VerticalSpan(squadRow, safeHeight);

            Assert.That(span.x, Is.GreaterThan(0f));
            Assert.That(
                span.y,
                Is.LessThan(safeHeight),
                "7枠がSafe Areaからはみ出しています。");

            // 7枠が横に並びきること（間隔込み）。
            HorizontalLayoutGroup layout =
                squadRow.GetComponent<HorizontalLayoutGroup>();

            Assert.That(layout, Is.Not.Null);

            float rowWidth = MobileLayoutMetrics.SafeAreaWidthUnits(scaler)
                             + squadRow.sizeDelta.x;

            float spacing = layout.spacing * (SquadFormation.SlotCount - 1);
            float padding = layout.padding.left + layout.padding.right;

            Assert.That(
                (rowWidth - spacing - padding) / SquadFormation.SlotCount,
                Is.GreaterThan(0f),
                "7枠を同時に置ける幅がありません。");
        }

        [Test]
        public void TheSavedMessageDoesNotCoverTheDockOrTheSquad()
        {
            CanvasScaler scaler = Scaler();
            float safeHeight = MobileLayoutMetrics.SafeAreaHeightUnits(scaler);

            RectTransform toast = FindOne<ToastLabel>().GetComponent<RectTransform>();
            RectTransform dock = ActionDock();
            RectTransform squadRow =
                FindOne<SquadBarView>().GetComponent<RectTransform>();

            Vector2 toastSpan = MobileLayoutMetrics.VerticalSpan(toast, safeHeight);
            Vector2 dockSpan = MobileLayoutMetrics.VerticalSpan(dock, safeHeight);
            Vector2 squadSpan = MobileLayoutMetrics.VerticalSpan(squadRow, safeHeight);

            Assert.That(
                toastSpan.x,
                Is.GreaterThanOrEqualTo(dockSpan.y),
                "SAVE SET の完了メッセージが Action Dock を隠しています。");

            Assert.That(
                toastSpan.x,
                Is.GreaterThanOrEqualTo(squadSpan.y),
                "SAVE SET の完了メッセージが7枠を隠しています。");
        }

        [Test]
        public void TheDockIsFixedToTheBottomAndNotScrolled()
        {
            RectTransform dock = ActionDock();

            Assert.That(dock.anchorMin.y, Is.EqualTo(0f));
            Assert.That(dock.anchorMax.y, Is.EqualTo(0f));
            Assert.That(dock.pivot.y, Is.EqualTo(0f));

            Assert.That(
                dock.GetComponentInParent<ScrollRect>(),
                Is.Null,
                "Action Dock はスクロール領域の中へ入れません。");

            RosterGridView roster = FindOne<RosterGridView>();

            Assert.That(
                dock.IsChildOf(roster.transform),
                Is.False,
                "Action Dock は Roster の中に入れません。");
        }

        [Test]
        public void GestureLoggingStaysOffByDefault()
        {
            UnitSetScreen screen = FindOne<UnitSetScreen>();

            Assert.That(
                (bool)GetField(screen, "logGestureEvents"),
                Is.False,
                "調査用ログは既定OFFのままにします。");
        }
    }
}
