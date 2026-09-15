using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
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

            if (scene.IsValid() && scene.isLoaded)
            {
                // 保存せずに閉じるため、テスト操作はシーンへ残りません。
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
                    "attributeChip", "attributeLabel", "nameLabel",
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
                "saveButton");

            AssertAssigned(FindOne<BeastDetailPanel>(),
                "portraitView", "portraitRoot", "nameLabel", "levelLabel",
                "costLabel", "attributeLabel", "powerLabel", "coreLabel",
                "skillNameLabel", "skillDescriptionLabel",
                "attributeChip", "powerGauge", "coreGauge", "palette", "text");

            AssertAssigned(FindOne<DragGhostPresenter>(),
                "ghostRoot", "ghostPrefab", "canvas");

            AssertAssigned(FindOne<PortraitRenderTarget>(),
                "portraitCamera", "targetImage");

            AssertAssigned(FindOne<ToastLabel>(), "canvasGroup", "label");
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
