using System.Collections.Generic;

using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class SquadEditorTests
    {
        private CoreBeastRoster roster;
        private SquadFormation formation;
        private SquadEditor editor;
        private OwnedCoreBeast a;
        private OwnedCoreBeast b;
        private OwnedCoreBeast c;
        private AttributePalette palette;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
            roster = TestRosterFactory.Create(3);
            formation = new SquadFormation();
            editor = new SquadEditor(formation);

            a = roster.Owned[0];
            b = roster.Owned[1];
            c = roster.Owned[2];
        }

        [TearDown]
        public void TearDown()
        {
            TestRosterFactory.Destroy(roster);
            roster = null;

            if (palette != null)
            {
                Object.DestroyImmediate(palette);
                palette = null;
            }
        }

        [Test]
        public void Select_DoesNotChangeFormation()
        {
            editor.Select(a);

            Assert.That(editor.Selected, Is.SameAs(a));

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                Assert.That(
                    formation.GetAt(i),
                    Is.Null,
                    "タップだけで編成が変わってはいけません。"
                );
            }
        }

        [Test]
        public void DropOnEmptySlot_PlacesBeast()
        {
            Assert.That(editor.DropOnSlot(2, a), Is.True);
            Assert.That(formation.GetAt(2), Is.SameAs(a));
        }

        [Test]
        public void DropOnInvalidSlot_DoesNotChangeFormation()
        {
            Assert.That(editor.DropOnSlot(-1, a), Is.False);
            Assert.That(editor.DropOnSlot(SquadFormation.SlotCount, a), Is.False);
            Assert.That(editor.DropOnSlot(0, null), Is.False);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                Assert.That(formation.GetAt(i), Is.Null);
            }
        }

        [Test]
        public void DropOnOccupiedSlot_ReplacesBeast()
        {
            editor.DropOnSlot(0, a);

            Assert.That(editor.DropOnSlot(0, b), Is.True);
            Assert.That(formation.GetAt(0), Is.SameAs(b));
            Assert.That(formation.IndexOf(a), Is.EqualTo(-1));
        }

        [Test]
        public void DropAlreadyPlacedBeast_SwapsWithoutDuplicating()
        {
            editor.DropOnSlot(0, a);
            editor.DropOnSlot(3, b);

            Assert.That(editor.DropOnSlot(3, a), Is.True);

            Assert.That(formation.GetAt(3), Is.SameAs(a));
            Assert.That(formation.GetAt(0), Is.SameAs(b), "2枠が入れ替わるべきです。");

            int count = 0;

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                if (ReferenceEquals(formation.GetAt(i), a))
                {
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(1), "同じ個体が複製されてはいけません。");
        }

        [Test]
        public void DropOnSameSlot_ReportsNoChange()
        {
            editor.DropOnSlot(1, a);

            Assert.That(editor.DropOnSlot(1, a), Is.False);
            Assert.That(formation.GetAt(1), Is.SameAs(a));
        }

        [Test]
        public void SlotOrderIsPreservedAfterSwap()
        {
            editor.DropOnSlot(0, a);
            editor.DropOnSlot(1, b);
            editor.DropOnSlot(2, c);

            editor.DropOnSlot(2, a);

            Assert.That(formation.GetAt(0), Is.SameAs(c));
            Assert.That(formation.GetAt(1), Is.SameAs(b));
            Assert.That(formation.GetAt(2), Is.SameAs(a));
        }

        [Test]
        public void RemoveAt_ClearsOccupiedSlotOnly()
        {
            editor.DropOnSlot(4, a);

            Assert.That(editor.RemoveAt(4), Is.True);
            Assert.That(formation.GetAt(4), Is.Null);

            Assert.That(editor.RemoveAt(4), Is.False, "空き枠のタップは何もしません。");
            Assert.That(editor.RemoveAt(-1), Is.False);
        }

        [Test]
        public void RemoveAt_KeepsBeastInRoster()
        {
            editor.DropOnSlot(0, a);
            editor.RemoveAt(0);

            Assert.That(roster.Find(a.InstanceId), Is.SameAs(a));

            IReadOnlyList<OwnedCoreBeast> owned = roster.Owned;
            Assert.That(owned.Count, Is.EqualTo(3));
        }

        [Test]
        public void RemoveAt_KeepsOtherSlotNumbers()
        {
            editor.DropOnSlot(0, a);
            editor.DropOnSlot(1, b);
            editor.DropOnSlot(2, c);

            editor.RemoveAt(1);

            Assert.That(formation.GetAt(0), Is.SameAs(a));
            Assert.That(formation.GetAt(1), Is.Null);
            Assert.That(formation.GetAt(2), Is.SameAs(c));
        }
    }
}
