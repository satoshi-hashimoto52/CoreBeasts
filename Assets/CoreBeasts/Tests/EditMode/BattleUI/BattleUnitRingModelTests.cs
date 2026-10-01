using System.Collections.Generic;

using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 循環リングの選択モデル。Unityへ依存しないため、そのまま実行できます。
    ///
    /// 「回しただけでは出撃しない」「同じ個体を重複して持たない」
    /// 「使用済みを外したあと誰が中央になるか」を確かめます。
    /// </summary>
    public sealed class BattleUnitRingModelTests
    {
        private static List<string> Ids(int count, string prefix = "p")
        {
            List<string> ids = new List<string>();

            for (int i = 0; i < count; i++)
            {
                ids.Add(prefix + i);
            }

            return ids;
        }

        private static BattleUnitRingModel Ring(int count = 7)
        {
            BattleUnitRingModel model = new BattleUnitRingModel();
            model.Build(Ids(count));

            return model;
        }

        // ---------------- 組み立て ----------------

        [Test]
        public void BuildingKeepsTheSquadOrderAndNumbersOneToSeven()
        {
            BattleUnitRingModel model = Ring();

            Assert.That(model.Count, Is.EqualTo(7));
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));

            for (int i = 0; i < 7; i++)
            {
                Assert.That(model.Slots[i].InstanceId, Is.EqualTo("p" + i));
                Assert.That(
                    model.Slots[i].SquadNumber,
                    Is.EqualTo(i + 1),
                    "編成番号は1始まりで並び順どおりです。");
            }
        }

        [Test]
        public void TheSameInstanceIsNeverHeldTwice()
        {
            List<string> ids = Ids(3);
            ids.Add(ids[0]);
            ids.Add(ids[1]);

            BattleUnitRingModel model = new BattleUnitRingModel();
            model.Build(ids);

            Assert.That(model.Count, Is.EqualTo(3), "重複した個体は取り込みません。");

            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < model.Count; i++)
            {
                Assert.That(
                    seen.Add(model.Slots[i].InstanceId),
                    Is.True,
                    "リングに同じ個体IDが二度あります。");
            }
        }

        // ---------------- 循環 ----------------

        [Test]
        public void RotatingRightWrapsFromTheLastBackToTheFirst()
        {
            BattleUnitRingModel model = Ring();

            for (int i = 0; i < 6; i++)
            {
                model.RotateRight();
            }

            Assert.That(model.FocusedInstanceId, Is.EqualTo("p6"), "末尾まで来ていません。");

            model.RotateRight();

            Assert.That(
                model.FocusedInstanceId,
                Is.EqualTo("p0"),
                "末尾から右へ回すと先頭へ戻ります。");
        }

        [Test]
        public void RotatingLeftFromTheFirstWrapsToTheLast()
        {
            BattleUnitRingModel model = Ring();

            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));

            model.RotateLeft();

            Assert.That(
                model.FocusedInstanceId,
                Is.EqualTo("p6"),
                "先頭から左へ回すと末尾へ回り込みます。");
        }

        [Test]
        public void ThereIsNoLeftOrRightEdge()
        {
            BattleUnitRingModel model = Ring();

            for (int i = 0; i < 7 * 3; i++)
            {
                Assert.That(
                    model.RotateRight(),
                    Is.True,
                    "右端に当たって止まってはいけません。");
            }

            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"), "3周して戻ります。");

            for (int i = 0; i < 7 * 3; i++)
            {
                Assert.That(model.RotateLeft(), Is.True, "左端に当たって止まってはいけません。");
            }

            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));
        }

        [Test]
        public void LargeRotationsStillLandInsideTheRing()
        {
            BattleUnitRingModel model = Ring();

            model.Rotate(1000);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p" + (1000 % 7)));

            model.Rotate(-1000);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));
        }

        [Test]
        public void OffsetsReadAroundTheRingInBothDirections()
        {
            BattleUnitRingModel model = Ring();

            Assert.That(model.SlotAtOffset(0).InstanceId, Is.EqualTo("p0"));
            Assert.That(model.SlotAtOffset(1).InstanceId, Is.EqualTo("p1"));
            Assert.That(model.SlotAtOffset(-1).InstanceId, Is.EqualTo("p6"));
            Assert.That(model.SlotAtOffset(-2).InstanceId, Is.EqualTo("p5"));
            Assert.That(model.SlotAtOffset(7).InstanceId, Is.EqualTo("p0"));
        }

        // ---------------- 使用済みの除去 ----------------

        [Test]
        public void RemovingTheCentreMovesToTheNextUnusedUnit()
        {
            BattleUnitRingModel model = Ring();

            model.RotateRight(2);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p2"));

            Assert.That(model.Remove("p2"), Is.True);

            Assert.That(
                model.FocusedInstanceId,
                Is.EqualTo("p3"),
                "編成順で次の未使用個体が中央になります。");

            Assert.That(model.Count, Is.EqualTo(6));
        }

        [Test]
        public void RemovingTheLastUnitWrapsTheCentreToTheFirst()
        {
            BattleUnitRingModel model = Ring();

            model.RotateLeft();
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p6"));

            model.Remove("p6");

            Assert.That(
                model.FocusedInstanceId,
                Is.EqualTo("p0"),
                "後続が無ければ先頭へ循環します。");
        }

        [Test]
        public void RemovingSomeoneBeforeTheCentreKeepsTheSameCentre()
        {
            BattleUnitRingModel model = Ring();

            model.RotateRight(3);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p3"));

            model.Remove("p1");

            Assert.That(
                model.FocusedInstanceId,
                Is.EqualTo("p3"),
                "中央の個体そのものは変わりません。");
        }

        [Test]
        public void RemovingAnUnknownInstanceChangesNothing()
        {
            BattleUnitRingModel model = Ring();

            Assert.That(model.Remove("missing"), Is.False);
            Assert.That(model.Remove(null), Is.False);
            Assert.That(model.Count, Is.EqualTo(7));
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));
        }

        [Test]
        public void TheRingShrinksSevenToZeroAndKeepsTheOriginalNumbers()
        {
            BattleUnitRingModel model = Ring();

            int[] expected = { 7, 6, 5, 4, 3, 2, 1, 0 };

            Assert.That(model.Count, Is.EqualTo(expected[0]));

            for (int i = 0; i < 7; i++)
            {
                string focused = model.FocusedInstanceId;
                int number = model.SquadNumberOf(focused);

                Assert.That(
                    number,
                    Is.EqualTo(int.Parse(focused.Substring(1)) + 1),
                    "使用済みが抜けても編成番号は変わりません。");

                model.Remove(focused);

                Assert.That(model.Count, Is.EqualTo(expected[i + 1]));
            }

            Assert.That(model.IsEmpty, Is.True);
            Assert.That(model.FocusedInstanceId, Is.Null);
            Assert.That(model.CanRotate, Is.False);
        }

        [Test]
        public void ASingleRemainingUnitCannotRotate()
        {
            BattleUnitRingModel model = Ring();

            for (int i = 0; i < 6; i++)
            {
                model.Remove("p" + i);
            }

            Assert.That(model.Count, Is.EqualTo(1));
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p6"));
            Assert.That(model.CanRotate, Is.False);
            Assert.That(model.RotateRight(), Is.False);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p6"));
        }

        // ---------------- REMATCH ----------------

        [Test]
        public void RestoreBringsBackAllSevenInTheOriginalOrder()
        {
            BattleUnitRingModel model = Ring();

            model.Remove("p0");
            model.Remove("p3");
            model.Remove("p5");

            Assert.That(model.Count, Is.EqualTo(4));

            model.Restore();

            Assert.That(model.Count, Is.EqualTo(7));
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p0"));

            for (int i = 0; i < 7; i++)
            {
                Assert.That(model.Slots[i].InstanceId, Is.EqualTo("p" + i));
                Assert.That(model.Slots[i].SquadNumber, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void FocusOnlyMovesTheCentreAndNeverSelectsForTheSession()
        {
            BattleUnitRingModel model = Ring();

            Assert.That(model.Focus("p4"), Is.True);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p4"));

            Assert.That(model.Focus("p4"), Is.False, "同じ個体なら変化なしです。");
            Assert.That(model.Focus("missing"), Is.False);
            Assert.That(model.FocusedInstanceId, Is.EqualTo("p4"));

            // 中央が動いても、リングの中身は減りません。
            Assert.That(model.Count, Is.EqualTo(7));
        }
    }
}
