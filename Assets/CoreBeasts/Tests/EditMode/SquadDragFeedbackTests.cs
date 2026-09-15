using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>ドラッグ中の枠の強調と、配置・除外の結果を検証します。</summary>
    public sealed class SquadDragFeedbackTests
    {
        private GameObject root;
        private SquadBarView bar;
        private readonly List<SquadSlotView> slots = new List<SquadSlotView>();
        private CoreBeastRoster roster;
        private AttributePalette palette;
        private UiTextCatalog catalog;
        private SquadFormation formation;
        private SquadEditor editor;

        private sealed class FakeSlotListener : ISquadSlotListener
        {
            public bool IsDraggingBeast { get; set; }
            public readonly List<int> Tapped = new List<int>();
            public readonly List<int> Dropped = new List<int>();
            public void OnSlotTapped(int slotIndex) => Tapped.Add(slotIndex);
            public void OnSlotDropped(int slotIndex) => Dropped.Add(slotIndex);
        }

        private FakeSlotListener listener;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
            roster = TestRosterFactory.Create(3);
            catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            formation = new SquadFormation();
            editor = new SquadEditor(formation);
            listener = new FakeSlotListener();

            root = new GameObject("SquadRow", typeof(RectTransform));
            bar = root.AddComponent<SquadBarView>();

            GameObject slotPrefab = new GameObject("SlotPrefab", typeof(RectTransform));
            slotPrefab.SetActive(false);
            SquadSlotView prefabView = slotPrefab.AddComponent<SquadSlotView>();

            SetField(bar, "content", (RectTransform)root.transform);
            SetField(bar, "slotPrefab", prefabView);

            bar.Build(catalog, listener);

            slots.Clear();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                slots.Add(root.transform.GetChild(i).GetComponent<SquadSlotView>());
            }

            Object.DestroyImmediate(slotPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) { Object.DestroyImmediate(root); root = null; }
            if (catalog != null) { Object.DestroyImmediate(catalog); catalog = null; }
            if (palette != null) { Object.DestroyImmediate(palette); palette = null; }
            TestRosterFactory.Destroy(roster);
            roster = null;
            slots.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, name + " が見つかりません。");
            f.SetValue(target, value);
        }

        [Test]
        public void Build_CreatesSevenSlotsInOrder()
        {
            Assert.That(slots.Count, Is.EqualTo(SquadFormation.SlotCount));

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.That(slots[i].SlotIndex, Is.EqualTo(i));
                Assert.That(slots[i].Highlight, Is.EqualTo(SlotHighlight.None));
            }
        }

        [Test]
        public void DuringDrag_AllSlotsBecomeAvailable()
        {
            bar.SetDragTarget(true, -1);

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.That(
                    slots[i].Highlight,
                    Is.EqualTo(SlotHighlight.Available),
                    "配置先候補として控えめに強調されるべきです。");
            }
        }

        [Test]
        public void DuringDrag_SlotHoldingTheSameBeastShowsSwap()
        {
            editor.DropOnSlot(2, roster.Owned[0]);

            bar.SetDragTarget(true, formation.IndexOf(roster.Owned[0]));

            Assert.That(slots[2].Highlight, Is.EqualTo(SlotHighlight.Swap));

            for (int i = 0; i < slots.Count; i++)
            {
                if (i == 2) continue;
                Assert.That(slots[i].Highlight, Is.EqualTo(SlotHighlight.Available));
            }
        }

        [Test]
        public void AfterDrag_AllHighlightsAreCleared()
        {
            bar.SetDragTarget(true, 1);
            bar.ClearHighlights();

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.That(slots[i].Highlight, Is.EqualTo(SlotHighlight.None));
            }
        }

        [Test]
        public void DropOnEmptySlot_PlacesAndKeepsOtherSlots()
        {
            Assert.That(editor.DropOnSlot(0, roster.Owned[0]), Is.True);

            bar.Refresh(formation, palette, catalog);

            Assert.That(formation.GetAt(0), Is.SameAs(roster.Owned[0]));
            Assert.That(formation.GetAt(1), Is.Null);
        }

        [Test]
        public void DropOnOccupiedSlot_ReplacesOccupant()
        {
            editor.DropOnSlot(0, roster.Owned[0]);

            Assert.That(editor.DropOnSlot(0, roster.Owned[1]), Is.True);
            Assert.That(formation.GetAt(0), Is.SameAs(roster.Owned[1]));
            Assert.That(formation.IndexOf(roster.Owned[0]), Is.EqualTo(-1));
        }

        [Test]
        public void MovingAPlacedBeast_SwapsInsteadOfDuplicating()
        {
            editor.DropOnSlot(0, roster.Owned[0]);
            editor.DropOnSlot(4, roster.Owned[1]);

            editor.DropOnSlot(4, roster.Owned[0]);

            Assert.That(formation.GetAt(4), Is.SameAs(roster.Owned[0]));
            Assert.That(formation.GetAt(0), Is.SameAs(roster.Owned[1]));
        }

        [Test]
        public void DropOutsideSquad_LeavesFormationUnchanged()
        {
            editor.DropOnSlot(3, roster.Owned[0]);

            // 枠以外で離した状況（枠番号が特定できない）
            Assert.That(editor.DropOnSlot(-1, roster.Owned[1]), Is.False);
            Assert.That(formation.GetAt(3), Is.SameAs(roster.Owned[0]));

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                if (i == 3) continue;
                Assert.That(formation.GetAt(i), Is.Null);
            }
        }

        [Test]
        public void TapOnOccupiedSlot_RemovesButKeepsRoster()
        {
            editor.DropOnSlot(5, roster.Owned[2]);

            Assert.That(editor.RemoveAt(5), Is.True);

            bar.Refresh(formation, palette, catalog);

            Assert.That(formation.GetAt(5), Is.Null);
            Assert.That(roster.Find(roster.Owned[2].InstanceId), Is.Not.Null);
            Assert.That(roster.Owned.Count, Is.EqualTo(3));
        }

        [Test]
        public void TapOnEmptySlot_ChangesNothing()
        {
            Assert.That(editor.RemoveAt(6), Is.False);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                Assert.That(formation.GetAt(i), Is.Null);
            }
        }

        [Test]
        public void FlashPlaced_ReturnsToBaseHighlightAfterTheFlash()
        {
            bar.SetDragTarget(true, -1);
            bar.FlashSlot(2);

            Assert.That(slots[2].Highlight, Is.EqualTo(SlotHighlight.Hovered));

            // 明滅時間を経過させる
            MethodInfo update = typeof(SquadSlotView).GetMethod(
                "Update", BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < 200; i++)
            {
                update.Invoke(slots[2], null);
            }

            Assert.That(
                slots[2].Highlight,
                Is.EqualTo(SlotHighlight.Available),
                "明滅後は基準の強調段階へ戻る必要があります。");
        }

        [Test]
        public void SnapshotSurvivesSaveAndRestore()
        {
            editor.DropOnSlot(1, roster.Owned[0]);
            editor.DropOnSlot(3, roster.Owned[2]);

            ISquadRepository repository = new InMemorySquadRepository();
            repository.Save("1", formation.CreateSnapshot());

            SquadFormation restored = new SquadFormation();
            Assert.That(repository.TryLoad("1", out SquadSnapshot snapshot), Is.True);
            restored.Restore(snapshot, roster);

            Assert.That(restored.GetAt(1), Is.SameAs(roster.Owned[0]));
            Assert.That(restored.GetAt(3), Is.SameAs(roster.Owned[2]));
            Assert.That(restored.GetAt(0), Is.Null);
        }
    }
}
