using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class CardGestureStateMachineTests
    {
        private const float Hold = 0.2f;
        private const float Move = 24f;

        private CardGestureStateMachine machine;

        [SetUp]
        public void SetUp()
        {
            machine = new CardGestureStateMachine(Hold, Move);
        }

        [Test]
        public void PointerDown_EntersPending()
        {
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Idle));

            Assert.That(machine.PointerDown(0, Vector2.zero, 0f), Is.True);

            Assert.That(machine.State, Is.EqualTo(CardGestureState.Pending));
            Assert.That(machine.HoldSatisfied, Is.False);
        }

        [Test]
        public void MoveBeforeHold_BecomesScrolling()
        {
            machine.PointerDown(0, Vector2.zero, 0f);

            // 10px(pixelDragThreshold)では判定しない
            Assert.That(
                machine.Move(0, new Vector2(0f, 10f), 0.02f),
                Is.EqualTo(CardGestureAction.None));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Pending));

            // しきい値超え、かつ長押し未成立 → スクロール
            Assert.That(
                machine.Move(0, new Vector2(0f, 40f), 0.05f),
                Is.EqualTo(CardGestureAction.BeginScroll));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Scrolling));
        }

        [Test]
        public void MoveAfterHold_BecomesSquadDragging()
        {
            machine.PointerDown(0, Vector2.zero, 0f);

            machine.Tick(0.25f);
            Assert.That(machine.HoldSatisfied, Is.True, "押下継続時間で成立すべきです。");

            Assert.That(
                machine.Move(0, new Vector2(0f, 40f), 0.30f),
                Is.EqualTo(CardGestureAction.BeginSquadDrag));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.SquadDragging));
        }

        [Test]
        public void HoldIsEvaluatedWithoutRelyingOnBeginDragTiming()
        {
            // Tickを呼ばず、最初の移動が長押し成立後でも正しく判定されること
            machine.PointerDown(0, Vector2.zero, 0f);

            Assert.That(
                machine.Move(0, new Vector2(0f, 40f), 0.50f),
                Is.EqualTo(CardGestureAction.BeginSquadDrag));
        }

        [Test]
        public void Scrolling_NeverUpgradesToSquadDragging()
        {
            machine.PointerDown(0, Vector2.zero, 0f);
            machine.Move(0, new Vector2(0f, 40f), 0.05f);

            Assert.That(machine.State, Is.EqualTo(CardGestureState.Scrolling));

            machine.Tick(5f);

            Assert.That(
                machine.Move(0, new Vector2(0f, 200f), 5f),
                Is.EqualTo(CardGestureAction.ContinueScroll));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Scrolling));
        }

        [Test]
        public void SquadDragging_ContinuesUntilEnd()
        {
            machine.PointerDown(0, Vector2.zero, 0f);
            machine.Tick(0.25f);
            machine.Move(0, new Vector2(0f, 40f), 0.3f);

            Assert.That(
                machine.Move(0, new Vector2(0f, 120f), 0.4f),
                Is.EqualTo(CardGestureAction.ContinueSquadDrag));

            Assert.That(machine.End(0), Is.EqualTo(CardGestureAction.EndSquadDrag));
        }

        [Test]
        public void End_ReturnsToIdle()
        {
            machine.PointerDown(0, Vector2.zero, 0f);
            machine.Tick(0.25f);
            machine.Move(0, new Vector2(0f, 40f), 0.3f);
            machine.End(0);

            Assert.That(machine.State, Is.EqualTo(CardGestureState.Idle));
            Assert.That(
                machine.ActivePointerId,
                Is.EqualTo(CardGestureStateMachine.NoPointer));
            Assert.That(machine.HoldSatisfied, Is.False);
        }

        [Test]
        public void EndWithoutDrag_IsTreatedAsTap()
        {
            machine.PointerDown(0, Vector2.zero, 0f);

            Assert.That(machine.End(0), Is.EqualTo(CardGestureAction.None));
            Assert.That(
                machine.GestureConsumed,
                Is.False,
                "ドラッグしていなければタップとして扱えること。");
        }

        [Test]
        public void DoesNotMixDifferentPointerIds()
        {
            machine.PointerDown(0, Vector2.zero, 0f);

            // 別の指の押下は受け付けない
            Assert.That(machine.PointerDown(1, new Vector2(300f, 0f), 0.01f), Is.False);
            Assert.That(machine.ActivePointerId, Is.EqualTo(0));

            // 別の指の移動・終了も無視する
            machine.Tick(0.25f);
            Assert.That(
                machine.Move(1, new Vector2(0f, 200f), 0.3f),
                Is.EqualTo(CardGestureAction.None));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Pending));

            Assert.That(machine.End(1), Is.EqualTo(CardGestureAction.None));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Pending));

            // 本来の指なら進む
            Assert.That(
                machine.Move(0, new Vector2(0f, 40f), 0.31f),
                Is.EqualTo(CardGestureAction.BeginSquadDrag));
            Assert.That(machine.End(0), Is.EqualTo(CardGestureAction.EndSquadDrag));
        }

        [Test]
        public void TinyJitterAfterHold_DoesNotStartDrag()
        {
            machine.PointerDown(0, Vector2.zero, 0f);
            machine.Tick(0.5f);

            Assert.That(
                machine.Move(0, new Vector2(3f, 4f), 0.5f),
                Is.EqualTo(CardGestureAction.None));
            Assert.That(machine.State, Is.EqualTo(CardGestureState.Pending));
        }
    }
}
