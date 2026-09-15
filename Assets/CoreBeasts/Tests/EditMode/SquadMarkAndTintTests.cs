using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 編成済みマーク（右上のチェック）と、縮小立ち絵の属性着色を固定します。
    /// 実アセット（BeastCard.prefab / Roster_Test.asset）を使うため、
    /// 同じ種類で個体違いのケースも検証できます。
    /// </summary>
    public sealed class SquadMarkAndTintTests
    {
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";
        private const string SlotPrefabPath = "Assets/CoreBeasts/Prefabs/SquadSlot.prefab";
        private const string GhostPrefabPath = "Assets/CoreBeasts/Prefabs/DragGhost.prefab";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string PalettePath =
            "Assets/CoreBeasts/Data/AttributePalette_Default.asset";

        /// <summary>表示だけを検証するための最小のリスナー。</summary>
        private sealed class NullCardListener : IBeastCardListener
        {
            public void OnCardTapped(OwnedCoreBeast beast) { }
            public void OnCardDragBegin(OwnedCoreBeast beast, PointerEventData e) { }
            public void OnCardDragMove(PointerEventData e) { }
            public void OnCardDragEnd(OwnedCoreBeast beast, PointerEventData e) { }
        }

        private readonly NullCardListener cardListener = new NullCardListener();

        private GameObject canvasObject;
        private RosterGridView grid;
        private CoreBeastRoster roster;
        private AttributePalette palette;
        private UiTextCatalog catalog;
        private SquadFormation formation;
        private SquadEditor editor;

        [SetUp]
        public void SetUp()
        {
            roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);
            palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);

            Assume.That(roster, Is.Not.Null, RosterPath + " が読めません。");
            Assume.That(palette, Is.Not.Null, PalettePath + " が読めません。");

            catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            formation = new SquadFormation();
            editor = new SquadEditor(formation);

            canvasObject = new GameObject(
                "Canvas", typeof(RectTransform), typeof(Canvas));

            GameObject gridObject = new GameObject("Grid", typeof(RectTransform));
            gridObject.transform.SetParent(canvasObject.transform, false);
            grid = gridObject.AddComponent<RosterGridView>();

            SetField(grid, "content", (RectTransform)gridObject.transform);
            SetField(grid, "cardPrefab",
                AssetDatabase.LoadAssetAtPath<BeastCardView>(CardPrefabPath));

            grid.Build(roster, palette, catalog, cardListener);
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null)
            {
                Object.DestroyImmediate(canvasObject);
                canvasObject = null;
            }

            if (catalog != null) { Object.DestroyImmediate(catalog); catalog = null; }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, name + " が見つかりません。");
            f.SetValue(target, value);
        }

        private List<BeastCardView> Cards()
        {
            RectTransform content = (RectTransform)typeof(RosterGridView)
                .GetField("content", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(grid);

            List<BeastCardView> list = new List<BeastCardView>();

            for (int i = 0; i < content.childCount; i++)
            {
                BeastCardView card = content.GetChild(i).GetComponent<BeastCardView>();

                if (card != null)
                {
                    list.Add(card);
                }
            }

            return list;
        }

        private BeastCardView CardOf(OwnedCoreBeast beast)
        {
            foreach (BeastCardView card in Cards())
            {
                if (ReferenceEquals(card.Beast, beast))
                {
                    return card;
                }
            }

            Assert.Fail("カードが見つかりません: " + beast.InstanceId);
            return null;
        }

        // ---------------- 編成済みマーク ----------------

        [Test]
        public void NoCardIsMarkedBeforeAnyPlacement()
        {
            grid.RefreshSquadMarks(formation);

            foreach (BeastCardView card in Cards())
            {
                Assert.That(
                    card.IsMarkedInSquad,
                    Is.False,
                    "未編成のカードにマークが付いています: " + card.Beast.InstanceId);
            }
        }

        [Test]
        public void OnlyThePlacedBeastIsMarked()
        {
            OwnedCoreBeast target = roster.Owned[0];

            editor.DropOnSlot(0, target);
            grid.RefreshSquadMarks(formation);

            foreach (BeastCardView card in Cards())
            {
                bool expected = ReferenceEquals(card.Beast, target);

                Assert.That(
                    card.IsMarkedInSquad,
                    Is.EqualTo(expected),
                    card.Beast.InstanceId + " のマークが期待と異なります。");
            }
        }

        [Test]
        public void MarkSurvivesMovingToAnotherSlot()
        {
            OwnedCoreBeast target = roster.Owned[2];

            editor.DropOnSlot(1, target);
            grid.RefreshSquadMarks(formation);
            Assert.That(CardOf(target).IsMarkedInSquad, Is.True);

            editor.DropOnSlot(5, target);
            grid.RefreshSquadMarks(formation);

            Assert.That(
                CardOf(target).IsMarkedInSquad,
                Is.True,
                "別枠へ移しただけでマークが消えてはいけません。");
            Assert.That(formation.IndexOf(target), Is.EqualTo(5));
        }

        [Test]
        public void MarkDisappearsAfterRemoval()
        {
            OwnedCoreBeast target = roster.Owned[3];

            editor.DropOnSlot(2, target);
            grid.RefreshSquadMarks(formation);
            Assert.That(CardOf(target).IsMarkedInSquad, Is.True);

            editor.RemoveAt(2);
            grid.RefreshSquadMarks(formation);

            Assert.That(CardOf(target).IsMarkedInSquad, Is.False);
        }

        [Test]
        public void SameDefinitionDifferentInstanceIsNotMarked()
        {
            // Roster_Test には同じ CB_Volx を使う個体が2件あります。
            OwnedCoreBeast first = null;
            OwnedCoreBeast twin = null;

            for (int i = 0; i < roster.Owned.Count && twin == null; i++)
            {
                for (int j = i + 1; j < roster.Owned.Count; j++)
                {
                    if (roster.Owned[i].Definition == roster.Owned[j].Definition)
                    {
                        first = roster.Owned[i];
                        twin = roster.Owned[j];
                        break;
                    }
                }
            }

            Assume.That(twin, Is.Not.Null, "同じ定義を使う個体が2件必要です。");

            editor.DropOnSlot(0, first);
            grid.RefreshSquadMarks(formation);

            Assert.That(CardOf(first).IsMarkedInSquad, Is.True);
            Assert.That(
                CardOf(twin).IsMarkedInSquad,
                Is.False,
                "同じ種類でも個体が違えばマークは付きません。");
        }

        [Test]
        public void MarksMatchFormationAfterRebuild()
        {
            editor.DropOnSlot(0, roster.Owned[1]);
            editor.DropOnSlot(4, roster.Owned[5]);

            ISquadRepository repository = new InMemorySquadRepository();
            repository.Save("1", formation.CreateSnapshot());

            // 再入場を模して、編成と一覧を作り直す
            SquadFormation restored = new SquadFormation();
            Assert.That(repository.TryLoad("1", out SquadSnapshot snapshot), Is.True);
            restored.Restore(snapshot, roster);

            grid.Build(roster, palette, catalog, cardListener);
            grid.RefreshSquadMarks(restored);

            foreach (BeastCardView card in Cards())
            {
                bool expected = restored.IndexOf(card.Beast) >= 0;

                Assert.That(
                    card.IsMarkedInSquad,
                    Is.EqualTo(expected),
                    card.Beast.InstanceId + " が再構築後に一致しません。");
            }
        }

        // ---------------- 縮小立ち絵の着色 ----------------

        [Test]
        public void ThumbnailsReceiveDifferentColorsPerAttribute()
        {
            Dictionary<UnitAttribute, Color> seen =
                new Dictionary<UnitAttribute, Color>();

            foreach (BeastCardView card in Cards())
            {
                CoreBeastDefinition definition = card.Beast.Definition;

                if (definition.HasSecondaryAttribute)
                {
                    continue;
                }

                BeastThumbnailView thumb =
                    card.GetComponentInChildren<BeastThumbnailView>(true);

                Assert.That(thumb, Is.Not.Null, "縮小立ち絵がありません。");

                seen[definition.PrimaryAttribute] = thumb.LastColors.Primary;
            }

            Assert.That(seen.Count, Is.EqualTo(3), "赤緑青の3属性が必要です。");
            Assert.That(seen[UnitAttribute.Red], Is.Not.EqualTo(seen[UnitAttribute.Green]));
            Assert.That(seen[UnitAttribute.Green], Is.Not.EqualTo(seen[UnitAttribute.Blue]));
            Assert.That(seen[UnitAttribute.Blue], Is.Not.EqualTo(seen[UnitAttribute.Red]));
        }

        [Test]
        public void DualAttributeThumbnailGetsBothColors()
        {
            BeastCardView dual = null;

            foreach (BeastCardView card in Cards())
            {
                if (card.Beast.Definition.HasSecondaryAttribute)
                {
                    dual = card;
                    break;
                }
            }

            Assume.That(dual, Is.Not.Null, "2属性の個体が必要です。");

            CoreBeastDefinition definition = dual.Beast.Definition;
            BeastThumbnailView thumb =
                dual.GetComponentInChildren<BeastThumbnailView>(true);

            AttributeColorResolver.Colors expected =
                AttributeColorResolver.Resolve(palette, definition);

            Assert.That(thumb.LastColors.Primary, Is.EqualTo(expected.Primary));
            Assert.That(thumb.LastColors.Secondary, Is.EqualTo(expected.Secondary));
            Assert.That(
                thumb.LastColors.Primary,
                Is.Not.EqualTo(thumb.LastColors.Secondary),
                "2属性は一次と二次で色が異なります。");
        }

        [Test]
        public void CardSlotAndGhostShareTheSameColorResolution()
        {
            OwnedCoreBeast beast = roster.Owned[6];
            CoreBeastDefinition definition = beast.Definition;

            AttributeColorResolver.Colors expected =
                AttributeColorResolver.Resolve(palette, definition);

            // カード
            BeastThumbnailView cardThumb =
                CardOf(beast).GetComponentInChildren<BeastThumbnailView>(true);
            Assert.That(cardThumb.LastColors.Primary, Is.EqualTo(expected.Primary));
            Assert.That(cardThumb.LastColors.Secondary, Is.EqualTo(expected.Secondary));

            // 編成枠
            GameObject slot = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath),
                canvasObject.transform);
            slot.GetComponent<SquadSlotView>().Show(beast, palette, catalog);
            BeastThumbnailView slotThumb =
                slot.GetComponentInChildren<BeastThumbnailView>(true);
            Assert.That(slotThumb.LastColors.Primary, Is.EqualTo(expected.Primary));
            Assert.That(slotThumb.LastColors.Secondary, Is.EqualTo(expected.Secondary));

            // ドラッグゴースト
            GameObject ghost = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath),
                canvasObject.transform);
            ghost.GetComponent<DragGhostView>().Bind(beast, palette, catalog);
            BeastThumbnailView ghostThumb =
                ghost.GetComponentInChildren<BeastThumbnailView>(true);
            Assert.That(ghostThumb.LastColors.Primary, Is.EqualTo(expected.Primary));
            Assert.That(ghostThumb.LastColors.Secondary, Is.EqualTo(expected.Secondary));

            // ゴーストには編成済みマークを持たせない
            Assert.That(
                ghost.GetComponentInChildren<BeastCardView>(true),
                Is.Null,
                "ゴーストはカードの状態表示を持ちません。");
        }

        [Test]
        public void ResolverMatchesTheDetailPortraitRules()
        {
            // 上部詳細(CoreBeastView)と同じ規則であることを、解決処理そのもので固定する
            AttributeColorResolver.Colors single = AttributeColorResolver.Resolve(
                palette, UnitAttribute.Green, false, UnitAttribute.Blue);

            Assert.That(single.Primary,
                Is.EqualTo(palette.GetColors(UnitAttribute.Green).PrimaryColor));
            Assert.That(single.Secondary,
                Is.EqualTo(palette.GetColors(UnitAttribute.Green).SecondaryColor),
                "単属性では二次領域も同じ属性の色です。");

            AttributeColorResolver.Colors dual = AttributeColorResolver.Resolve(
                palette, UnitAttribute.Red, true, UnitAttribute.Blue);

            Assert.That(dual.Primary,
                Is.EqualTo(palette.GetColors(UnitAttribute.Red).PrimaryColor));
            Assert.That(dual.Secondary,
                Is.EqualTo(palette.GetColors(UnitAttribute.Blue).SecondaryColor));
            Assert.That(dual.Emission,
                Is.EqualTo(palette.GetColors(UnitAttribute.Red).EmissionColor),
                "発光は一次属性側です。");
        }

        // ---------------- 報告された事象の再現 ----------------

        [Test]
        public void SevenPlacedOutOfEight_MarksExactlySeven()
        {
            // 手持ち8体のうち7体を編成した状態（報告された画面と同じ構成）
            List<OwnedCoreBeast> owned = new List<OwnedCoreBeast>(roster.Owned);
            Assume.That(owned.Count, Is.EqualTo(8), "テストロースターは8体想定です。");

            // 青属性の2体のうち、1体だけを編成せずに残す
            OwnedCoreBeast leftOut = null;

            foreach (OwnedCoreBeast beast in owned)
            {
                if (!beast.Definition.HasSecondaryAttribute &&
                    beast.Definition.PrimaryAttribute == UnitAttribute.Blue)
                {
                    leftOut = beast;
                }
            }

            Assume.That(leftOut, Is.Not.Null, "青の単属性個体が必要です。");

            int slot = 0;

            foreach (OwnedCoreBeast beast in owned)
            {
                if (ReferenceEquals(beast, leftOut))
                {
                    continue;
                }

                editor.DropOnSlot(slot, beast);
                slot++;
            }

            Assert.That(formation.OccupiedCount, Is.EqualTo(7));

            grid.RefreshSquadMarks(formation);

            int marked = 0;

            foreach (BeastCardView card in Cards())
            {
                if (card.IsMarkedInSquad)
                {
                    marked++;
                }
            }

            Assert.That(marked, Is.EqualTo(7), "編成した7体だけにマークが付きます。");
            Assert.That(
                CardOf(leftOut).IsMarkedInSquad,
                Is.False,
                "未編成の青1体にマークが付いてはいけません。");
        }

        [Test]
        public void MarkCountNeverExceedsSlotCount()
        {
            foreach (OwnedCoreBeast beast in roster.Owned)
            {
                for (int i = 0; i < SquadFormation.SlotCount; i++)
                {
                    editor.DropOnSlot(i, beast);
                }
            }

            grid.RefreshSquadMarks(formation);

            int marked = 0;

            foreach (BeastCardView card in Cards())
            {
                if (card.IsMarkedInSquad)
                {
                    marked++;
                }
            }

            Assert.That(
                marked,
                Is.LessThanOrEqualTo(SquadFormation.SlotCount),
                "マーク数が枠数を超えてはいけません。");
            Assert.That(marked, Is.EqualTo(formation.OccupiedCount));
        }

        [Test]
        public void MatchingUsesInstanceIdNotObjectReference()
        {
            OwnedCoreBeast target = roster.Owned[4];

            editor.DropOnSlot(3, target);

            // 参照が作り直されても、内部IDが同じなら一致する
            Assert.That(
                formation.IndexOfInstance(target.InstanceId),
                Is.EqualTo(3));

            // 同じ定義を使う別個体のIDでは一致しない
            foreach (OwnedCoreBeast other in roster.Owned)
            {
                if (ReferenceEquals(other, target))
                {
                    continue;
                }

                Assert.That(
                    formation.IndexOfInstance(other.InstanceId),
                    Is.EqualTo(-1),
                    other.InstanceId + " が誤って一致しています。");
            }
        }

        // ---------------- 選択表示 ----------------

        [Test]
        public void SelectedCardHasNoCheckShapeOfItsOwn()
        {
            BeastCardView card = Cards()[0];
            card.SetSelected(true);

            // 黄色チェック用のGameObjectは撤去済みであること
            foreach (Transform child in
                     card.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    child.name,
                    Is.Not.EqualTo("SelectedCheck"),
                    "選択表示は黄色い枠だけにします。");
            }

            Assert.That(
                typeof(BeastCardView).GetField("selectedCheck",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Null,
                "selectedCheck フィールドは撤去されている必要があります。");
        }

        [Test]
        public void SelectedAndInSquadShowsFrameAndCyanCheckTogether()
        {
            OwnedCoreBeast target = roster.Owned[0];

            editor.DropOnSlot(0, target);
            grid.RefreshSquadMarks(formation);

            BeastCardView card = CardOf(target);
            card.SetSelected(true);
            grid.SetSelected(target);

            Assert.That(
                card.IsMarkedInSquad,
                Is.True,
                "選択中でも編成済みのシアンチェックは維持されます。");

            Transform frame = card.transform.Find("VisualRoot/Frame");
            Assert.That(frame, Is.Not.Null, "黄色枠は残っている必要があります。");
        }

        [Test]
        public void CardCanvasStaysEnabledAfterBinding()
        {
            foreach (BeastCardView card in Cards())
            {
                Canvas canvas = card.transform.Find("VisualRoot").GetComponent<Canvas>();

                Assert.That(canvas, Is.Not.Null);
                Assert.That(
                    canvas.enabled,
                    Is.True,
                    "Bind後もCanvasは有効のままである必要があります。");
            }
        }
    }
}
