using System.Collections.Generic;

using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 連続位相の循環。指を離さずに何周でも回せることを、位相を少しずつ進めて確かめます。
    /// Unityへ依存しないため、そのまま実行できます。
    /// </summary>
    public sealed class BattleRingPhaseTests
    {
        private static BattleUnitRingModel Ring(int count)
        {
            List<string> ids = new List<string>();

            for (int i = 0; i < count; i++)
            {
                ids.Add("p" + i);
            }

            BattleUnitRingModel model = new BattleUnitRingModel();
            model.Build(ids);

            return model;
        }

        private static IReadOnlyList<BattleRingPhaseSlot> At(
            BattleUnitRingModel model, float offset)
        {
            return BattleRingPhase.Resolve(model, offset);
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }

        /// <summary>どの位相でも壊れていないことをまとめて確かめます。</summary>
        private static void AssertHealthy(
            BattleUnitRingModel model, float offset, string because)
        {
            IReadOnlyList<BattleRingPhaseSlot> visible = At(model, offset);

            int expected = model.Count < BattleRingPhase.MaxVisible
                ? model.Count
                : BattleRingPhase.MaxVisible;

            Assert.That(
                visible.Count,
                Is.EqualTo(expected),
                because + ": 表示数が足りません（空きslot）。");

            HashSet<string> seen = new HashSet<string>();
            float nearest = float.MaxValue;

            for (int i = 0; i < visible.Count; i++)
            {
                Assert.That(
                    seen.Add(visible[i].Slot.InstanceId),
                    Is.True,
                    because + ": 同じ個体 " + visible[i].Slot.InstanceId +
                    " が2箇所へ出ています。");

                float distance = Abs(visible[i].Distance);

                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            Assert.That(
                nearest,
                Is.LessThanOrEqualTo(0.5001f),
                because + ": 中央が空いています（wrap地点の空白）。");
        }

        // ---------------- 折り返しそのもの ----------------

        [Test]
        public void TheNearestWrapAlwaysStaysWithinHalfATurn()
        {
            const int count = 7;

            for (float offset = -20f; offset <= 20f; offset += 0.25f)
            {
                for (int index = 0; index < count; index++)
                {
                    float distance = BattleRingPhase.NearestWrap(index, count, offset);

                    Assert.That(
                        Abs(distance),
                        Is.LessThanOrEqualTo(count / 2f + 0.001f),
                        "index " + index + " / offset " + offset +
                        " で折り返しが効いていません。");
                }
            }
        }

        [Test]
        public void AUnitLeavingOnTheLeftComesBackOnTheRight()
        {
            const int count = 7;

            // 左端から出ていく個体（index 4 は offset 0 で距離 -3）。
            float before = BattleRingPhase.NearestWrap(4, count, -0.4f);
            float after = BattleRingPhase.NearestWrap(4, count, -0.6f);

            Assert.That(before, Is.LessThan(0f), "まだ左側に居ます。");
            Assert.That(
                after,
                Is.GreaterThan(0f),
                "左へ出た個体が右側へ回り込んでいません。");
        }

        // ---------------- 静止時は従来の並び ----------------

        [TestCase(7)]
        [TestCase(5)]
        [TestCase(4)]
        [TestCase(3)]
        [TestCase(2)]
        [TestCase(1)]
        public void AtRestTheArrangementMatchesTheAgreedLayout(int count)
        {
            BattleUnitRingModel model = Ring(count);

            IReadOnlyList<BattleRingPhaseSlot> visible = At(model, 0f);

            AssertHealthy(model, 0f, "静止時");

            Assert.That(
                visible[0].Slot.InstanceId,
                Is.EqualTo(model.FocusedInstanceId),
                "中央がモデルの中央と一致しません。");

            Assert.That(
                visible[0].Distance,
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FourRemainingStillPlacesTheLastOneBehind()
        {
            BattleUnitRingModel model = Ring(4);

            IReadOnlyList<BattleRingPhaseSlot> visible = At(model, 0f);

            Assert.That(visible.Count, Is.EqualTo(4));

            // 中央・左右隣接・そして残る1体は距離2（奥側）へ。
            float farthest = 0f;

            for (int i = 0; i < visible.Count; i++)
            {
                float distance = Abs(visible[i].Distance);

                if (distance > farthest)
                {
                    farthest = distance;
                }
            }

            Assert.That(farthest, Is.EqualTo(2f).Within(0.0001f));
        }

        // ---------------- 一巡しても途切れない ----------------

        [TestCase(7)]
        [TestCase(5)]
        [TestCase(4)]
        [TestCase(3)]
        [TestCase(2)]
        [TestCase(1)]
        public void SweepingTwoFullTurnsRightNeverBreaks(int count)
        {
            BattleUnitRingModel model = Ring(count);

            float span = count * 2f;

            for (float offset = 0f; offset <= span; offset += 0.05f)
            {
                AssertHealthy(model, offset, "右へ位相 " + offset);
            }
        }

        [TestCase(7)]
        [TestCase(5)]
        [TestCase(4)]
        [TestCase(3)]
        [TestCase(2)]
        [TestCase(1)]
        public void SweepingTwoFullTurnsLeftNeverBreaks(int count)
        {
            BattleUnitRingModel model = Ring(count);

            float span = count * 2f;

            for (float offset = 0f; offset >= -span; offset -= 0.05f)
            {
                AssertHealthy(model, offset, "左へ位相 " + offset);
            }
        }

        [Test]
        public void EveryUnitTakesTheCentreExactlyOncePerTurn()
        {
            BattleUnitRingModel model = Ring(7);

            HashSet<string> centred = new HashSet<string>();

            // 1周ぶん位相を進め、中央に来た個体を集めます。
            for (float offset = 0f; offset > -7f; offset -= 0.01f)
            {
                IReadOnlyList<BattleRingPhaseSlot> visible = At(model, offset);

                centred.Add(visible[0].Slot.InstanceId);
            }

            Assert.That(
                centred.Count,
                Is.EqualTo(7),
                "1周で全個体が中央を通っていません（順番の飛び）。");
        }

        [Test]
        public void TheOrderNeverReversesWhileSweeping()
        {
            BattleUnitRingModel model = Ring(7);

            string previous = model.FocusedInstanceId;
            List<string> order = new List<string> { previous };

            for (float offset = 0f; offset > -7f; offset -= 0.01f)
            {
                string centre = At(model, offset)[0].Slot.InstanceId;

                if (centre != previous)
                {
                    order.Add(centre);
                    previous = centre;
                }
            }

            // 左へ回すと編成順に進みます（p0 → p1 → ... → p6）。
            // 1周ぶん回すと先頭へ戻るため、最後にもう一度 p0 が現れます。
            Assert.That(
                order.Count,
                Is.EqualTo(8),
                "1周で 7 体を通り、先頭へ戻ります。");

            for (int i = 0; i < 7; i++)
            {
                Assert.That(
                    order[i],
                    Is.EqualTo("p" + i),
                    "中央の入れ替わり順が飛んでいます。");
            }

            Assert.That(
                order[7],
                Is.EqualTo("p0"),
                "1周したら先頭へ循環します（端で止まりません）。");
        }

        [Test]
        public void LeftAndRightAreSymmetric()
        {
            BattleUnitRingModel model = Ring(7);

            for (float offset = 0.05f; offset <= 3f; offset += 0.05f)
            {
                IReadOnlyList<BattleRingPhaseSlot> right = At(model, offset);
                IReadOnlyList<BattleRingPhaseSlot> left = At(model, -offset);

                Assert.That(
                    right.Count,
                    Is.EqualTo(left.Count),
                    "位相 " + offset + " で左右の表示数が違います。");
            }
        }

        // ---------------- 使用済みを外したあと ----------------

        [Test]
        public void TheRingStillCirculatesAfterUnitsAreRemoved()
        {
            BattleUnitRingModel model = Ring(7);

            model.Remove("p0");
            model.Remove("p3");
            model.Remove("p5");

            Assert.That(model.Count, Is.EqualTo(4));

            for (float offset = -8f; offset <= 8f; offset += 0.05f)
            {
                AssertHealthy(model, offset, "除外後の位相 " + offset);
            }

            // 外した個体は二度と出てきません。
            for (float offset = -8f; offset <= 8f; offset += 0.25f)
            {
                IReadOnlyList<BattleRingPhaseSlot> visible = At(model, offset);

                for (int i = 0; i < visible.Count; i++)
                {
                    string id = visible[i].Slot.InstanceId;

                    Assert.That(id, Is.Not.EqualTo("p0"));
                    Assert.That(id, Is.Not.EqualTo("p3"));
                    Assert.That(id, Is.Not.EqualTo("p5"));
                }
            }
        }

        [Test]
        public void ASingleRemainingUnitStaysInTheCentreAtAnyPhase()
        {
            BattleUnitRingModel model = Ring(1);

            for (float offset = -5f; offset <= 5f; offset += 0.1f)
            {
                IReadOnlyList<BattleRingPhaseSlot> visible = At(model, offset);

                Assert.That(visible.Count, Is.EqualTo(1));
                Assert.That(visible[0].Slot.InstanceId, Is.EqualTo("p0"));
            }
        }

        [Test]
        public void TwoRemainingSwapSidesInsteadOfDuplicating()
        {
            BattleUnitRingModel model = Ring(2);

            IReadOnlyList<BattleRingPhaseSlot> right = At(model, 0.6f);
            IReadOnlyList<BattleRingPhaseSlot> left = At(model, -0.6f);

            Assert.That(right.Count, Is.EqualTo(2));
            Assert.That(left.Count, Is.EqualTo(2));

            Assert.That(
                right[0].Slot.InstanceId,
                Is.Not.EqualTo(right[1].Slot.InstanceId),
                "2体を複製してはいけません。");

            // 相方は、指の向きに応じて反対側へ現れます。
            float rightPartner = right[1].Distance;
            float leftPartner = left[1].Distance;

            Assert.That(
                rightPartner * leftPartner,
                Is.LessThan(0f),
                "2体のとき、相方が常に同じ側に居ます。");
        }

        // ---------------- 描画順 ----------------

        [Test]
        public void TheCentreIsAlwaysDrawnLast()
        {
            BattleUnitRingModel model = Ring(7);

            for (float offset = -2f; offset <= 2f; offset += 0.13f)
            {
                IReadOnlyList<BattleRingPhaseSlot> visible = At(model, offset);

                int centreOrder = BattleRingPhase.DrawOrderOf(visible[0].Distance);

                for (int i = 1; i < visible.Count; i++)
                {
                    Assert.That(
                        BattleRingPhase.DrawOrderOf(visible[i].Distance),
                        Is.LessThanOrEqualTo(centreOrder),
                        "位相 " + offset + " で中央が最前面ではありません。");
                }
            }
        }

        [Test]
        public void ThePlacementFollowsTheDistance()
        {
            Assert.That(
                BattleRingPhase.PlacementOf(0f),
                Is.EqualTo(BattleRingPlacement.Center));

            Assert.That(
                BattleRingPhase.PlacementOf(0.9f),
                Is.EqualTo(BattleRingPlacement.AdjacentRight));

            Assert.That(
                BattleRingPhase.PlacementOf(-0.9f),
                Is.EqualTo(BattleRingPlacement.AdjacentLeft));

            Assert.That(
                BattleRingPhase.PlacementOf(2.1f),
                Is.EqualTo(BattleRingPlacement.OuterRight));

            Assert.That(
                BattleRingPhase.PlacementOf(-2.1f),
                Is.EqualTo(BattleRingPlacement.OuterLeft));
        }
    }
}
