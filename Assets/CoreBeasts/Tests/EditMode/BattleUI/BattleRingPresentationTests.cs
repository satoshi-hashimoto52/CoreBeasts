using System.Collections.Generic;

using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 残り人数ごとの配置と、Dock風の連続変化。
    /// どちらもUnityへ依存しないため、そのまま実行できます。
    /// </summary>
    public sealed class BattleRingPresentationTests
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

        private static void AssertNoDuplicates(
            IReadOnlyList<BattleRingVisibleSlot> visible)
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < visible.Count; i++)
            {
                Assert.That(
                    seen.Add(visible[i].Slot.InstanceId),
                    Is.True,
                    "同じ個体 " + visible[i].Slot.InstanceId + " が2箇所へ出ています。");
            }
        }

        private static BattleRingVisibleSlot Find(
            IReadOnlyList<BattleRingVisibleSlot> visible, BattleRingPlacement placement)
        {
            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i].Placement == placement)
                {
                    return visible[i];
                }
            }

            Assert.Fail(placement + " が見つかりません。");
            return default;
        }

        private static bool Has(
            IReadOnlyList<BattleRingVisibleSlot> visible, BattleRingPlacement placement)
        {
            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i].Placement == placement)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------- 残り人数ごと ----------------

        [TestCase(7)]
        [TestCase(6)]
        [TestCase(5)]
        public void FiveToSevenRemainingShowAtMostFive(int count)
        {
            IReadOnlyList<BattleRingVisibleSlot> visible =
                BattleRingPresentation.Resolve(Ring(count));

            Assert.That(
                visible.Count,
                Is.EqualTo(BattleRingPresentation.MaxVisible),
                "中央・左右隣接・左右外側の5体を出します。");

            AssertNoDuplicates(visible);

            Assert.That(Has(visible, BattleRingPlacement.Center), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentLeft), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentRight), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.OuterLeft), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.OuterRight), Is.True);
            Assert.That(
                Has(visible, BattleRingPlacement.Back),
                Is.False,
                "5体以上では奥側を使いません。");
        }

        [Test]
        public void FourRemainingPutsTheLastOneAtTheBack()
        {
            IReadOnlyList<BattleRingVisibleSlot> visible =
                BattleRingPresentation.Resolve(Ring(4));

            Assert.That(visible.Count, Is.EqualTo(4), "4体すべてを出します。");
            AssertNoDuplicates(visible);

            Assert.That(Has(visible, BattleRingPlacement.Center), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentLeft), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentRight), Is.True);
            Assert.That(
                Has(visible, BattleRingPlacement.Back),
                Is.True,
                "残る1体は奥側中央へ出します。");

            BattleRingVisibleSlot back = Find(visible, BattleRingPlacement.Back);
            BattleRingVisibleSlot center = Find(visible, BattleRingPlacement.Center);

            Assert.That(
                back.Slot.InstanceId,
                Is.Not.EqualTo(center.Slot.InstanceId),
                "奥側が中央と同じ個体になってはいけません。");
        }

        [Test]
        public void ThreeRemainingUseLeftCentreRight()
        {
            IReadOnlyList<BattleRingVisibleSlot> visible =
                BattleRingPresentation.Resolve(Ring(3));

            Assert.That(visible.Count, Is.EqualTo(3));
            AssertNoDuplicates(visible);

            Assert.That(Has(visible, BattleRingPlacement.Center), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentLeft), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.AdjacentRight), Is.True);
            Assert.That(Has(visible, BattleRingPlacement.OuterLeft), Is.False);
            Assert.That(Has(visible, BattleRingPlacement.Back), Is.False);
        }

        [Test]
        public void TwoRemainingShowOneSideOnlyAndNeverDuplicate()
        {
            BattleUnitRingModel model = Ring(2);

            IReadOnlyList<BattleRingVisibleSlot> visible =
                BattleRingPresentation.Resolve(model);

            Assert.That(visible.Count, Is.EqualTo(2), "2体を2箇所へ出します。");
            AssertNoDuplicates(visible);

            Assert.That(Has(visible, BattleRingPlacement.Center), Is.True);
            Assert.That(
                Has(visible, BattleRingPlacement.AdjacentLeft),
                Is.False,
                "同じ個体を反対側へ複製してはいけません。");

            // 回すと2体の位置が入れ替わります。
            string before = model.FocusedInstanceId;
            model.RotateRight();

            Assert.That(model.FocusedInstanceId, Is.Not.EqualTo(before));

            IReadOnlyList<BattleRingVisibleSlot> swapped =
                BattleRingPresentation.Resolve(model);

            Assert.That(swapped.Count, Is.EqualTo(2));
            AssertNoDuplicates(swapped);
        }

        [Test]
        public void OneRemainingIsCentreOnly()
        {
            BattleUnitRingModel model = Ring(1);

            IReadOnlyList<BattleRingVisibleSlot> visible =
                BattleRingPresentation.Resolve(model);

            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(visible[0].Placement, Is.EqualTo(BattleRingPlacement.Center));
            Assert.That(model.CanRotate, Is.False, "1体では横回転しません。");
        }

        [Test]
        public void AnEmptyRingShowsNothing()
        {
            Assert.That(BattleRingPresentation.Resolve(Ring(0)), Is.Empty);
            Assert.That(BattleRingPresentation.Resolve(null), Is.Empty);
        }

        [Test]
        public void NoArrangementEverRepeatsAnInstance()
        {
            for (int count = 1; count <= 7; count++)
            {
                BattleUnitRingModel model = Ring(count);

                for (int rotation = 0; rotation < count; rotation++)
                {
                    AssertNoDuplicates(BattleRingPresentation.Resolve(model));
                    model.RotateRight();
                }
            }
        }

        // ---------------- 描画順 ----------------

        [Test]
        public void TheCentreIsAlwaysDrawnInFront()
        {
            int back = BattleRingPresentation.DrawOrderOf(BattleRingPlacement.Back);
            int outer = BattleRingPresentation.DrawOrderOf(BattleRingPlacement.OuterLeft);
            int adjacent =
                BattleRingPresentation.DrawOrderOf(BattleRingPlacement.AdjacentRight);
            int center = BattleRingPresentation.DrawOrderOf(BattleRingPlacement.Center);

            Assert.That(back, Is.LessThan(outer));
            Assert.That(outer, Is.LessThan(adjacent));
            Assert.That(adjacent, Is.LessThan(center), "中央が最前面です。");

            Assert.That(
                BattleRingPresentation.DrawOrderOf(BattleRingPlacement.OuterRight),
                Is.EqualTo(outer));

            Assert.That(
                BattleRingPresentation.DrawOrderOf(BattleRingPlacement.AdjacentLeft),
                Is.EqualTo(adjacent));
        }

        // ---------------- Dock風の連続変化 ----------------

        [Test]
        public void TheCentreIsTheLargestAndFullyOpaque()
        {
            BattleRingSample center = BattleRingLayout.Evaluate(0f);

            Assert.That(center.Scale, Is.EqualTo(1.20f).Within(0.0001f));
            Assert.That(center.OffsetY, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(center.Alpha, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(center.Tilt, Is.EqualTo(0f).Within(0.0001f), "中央は正面です。");
        }

        [Test]
        public void EachTierMatchesTheAgreedNumbers()
        {
            BattleRingSample adjacent = BattleRingLayout.Evaluate(1f);
            BattleRingSample outer = BattleRingLayout.Evaluate(2f);
            BattleRingSample back = BattleRingLayout.BackSample();

            // 隣接は仕様で 0.85〜0.95。合意値はその中の 0.92 です。
            Assert.That(adjacent.Scale, Is.EqualTo(0.92f).Within(0.0001f));
            Assert.That(
                adjacent.Scale,
                Is.InRange(0.85f, 0.95f),
                "隣接の倍率が仕様の範囲外です。");
            Assert.That(adjacent.OffsetY, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(adjacent.Alpha, Is.EqualTo(0.90f).Within(0.0001f));

            Assert.That(outer.Scale, Is.EqualTo(0.78f).Within(0.0001f));
            Assert.That(outer.OffsetY, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(outer.Alpha, Is.EqualTo(0.68f).Within(0.0001f));

            Assert.That(back.Scale, Is.EqualTo(0.66f).Within(0.0001f));
            Assert.That(back.Alpha, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void ScaleChangesContinuouslyWithDistance()
        {
            float previous = BattleRingLayout.Evaluate(0f).Scale;

            // 中央だけが突然拡大しないこと。0→2 まで段階的に小さくなります。
            for (float d = 0.05f; d <= 2f; d += 0.05f)
            {
                float scale = BattleRingLayout.Evaluate(d).Scale;

                Assert.That(
                    scale,
                    Is.LessThanOrEqualTo(previous + 0.0001f),
                    "距離 " + d + " で拡大率が増えています。");

                Assert.That(
                    previous - scale,
                    Is.LessThan(0.05f),
                    "距離 " + d + " で拡大率が跳ねています（連続ではありません）。");

                previous = scale;
            }
        }

        [Test]
        public void HalfwayBetweenTiersIsHalfwayBetweenTheValues()
        {
            BattleRingSample half = BattleRingLayout.Evaluate(0.5f);

            Assert.That(
                half.Scale,
                Is.EqualTo((1.20f + 0.92f) * 0.5f).Within(0.0001f));

            Assert.That(
                half.OffsetY,
                Is.EqualTo((24f + 10f) * 0.5f).Within(0.0001f));

            Assert.That(
                half.Alpha,
                Is.EqualTo((1f + 0.90f) * 0.5f).Within(0.0001f));
        }

        [Test]
        public void BothSidesLeanTowardsTheCentre()
        {
            BattleRingSample left = BattleRingLayout.Evaluate(-1f);
            BattleRingSample right = BattleRingLayout.Evaluate(1f);

            Assert.That(left.Tilt, Is.GreaterThan(0f), "左側は中央へ向けて傾けます。");
            Assert.That(right.Tilt, Is.LessThan(0f), "右側は中央へ向けて傾けます。");
            Assert.That(left.Tilt, Is.EqualTo(-right.Tilt).Within(0.0001f));

            Assert.That(
                BattleRingLayout.Evaluate(2f).Tilt,
                Is.LessThan(right.Tilt),
                "外側ほど強く傾けます。");
        }

        [Test]
        public void LeftAndRightAreMirroredApartFromTheTilt()
        {
            BattleRingSample left = BattleRingLayout.Evaluate(-1.4f);
            BattleRingSample right = BattleRingLayout.Evaluate(1.4f);

            Assert.That(left.Scale, Is.EqualTo(right.Scale).Within(0.0001f));
            Assert.That(left.OffsetY, Is.EqualTo(right.OffsetY).Within(0.0001f));
            Assert.That(left.Alpha, Is.EqualTo(right.Alpha).Within(0.0001f));
        }

        [Test]
        public void HorizontalOffsetGrowsOutwardAndIsMirrored()
        {
            const float spacing = 100f;

            Assert.That(
                BattleRingLayout.HorizontalOffset(0f, spacing),
                Is.EqualTo(0f).Within(0.0001f));

            float one = BattleRingLayout.HorizontalOffset(1f, spacing);
            float two = BattleRingLayout.HorizontalOffset(2f, spacing);

            Assert.That(one, Is.EqualTo(spacing).Within(0.0001f));
            Assert.That(two, Is.GreaterThan(one), "外側ほど遠くへ置きます。");

            Assert.That(
                two - one,
                Is.LessThan(one),
                "外側ほど間隔を詰め、弧に見えるようにします。");

            Assert.That(
                BattleRingLayout.HorizontalOffset(-2f, spacing),
                Is.EqualTo(-two).Within(0.0001f));
        }
    }
}
