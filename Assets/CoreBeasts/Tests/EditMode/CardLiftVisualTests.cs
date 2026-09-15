using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>長押し成立時の浮き上がり表示に関する検証。</summary>
    public sealed class CardLiftVisualTests
    {
        private sealed class FakeListener : IBeastCardListener
        {
            public readonly List<string> Calls = new List<string>();
            public void OnCardTapped(OwnedCoreBeast b) => Calls.Add("tap");
            public void OnCardDragBegin(OwnedCoreBeast b, PointerEventData e) => Calls.Add("dragBegin");
            public void OnCardDragMove(PointerEventData e) => Calls.Add("dragMove");
            public void OnCardDragEnd(OwnedCoreBeast b, PointerEventData e) => Calls.Add("dragEnd");
        }

        private const float Lift = 24f;
        private const float Scale = 1.06f;

        private GameObject canvasObject;
        private ScrollRect scrollRect;
        private readonly List<BeastCardView> cards = new List<BeastCardView>();
        private readonly List<CardLiftView> lifts = new List<CardLiftView>();
        private readonly List<RectTransform> visuals = new List<RectTransform>();
        private FakeListener listener;
        private CoreBeastRoster roster;
        private AttributePalette palette;
        private UiTextCatalog catalog;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            BeastCardView.TimeProvider = () => now;

            palette = TestPaletteFactory.Create();
            roster = TestRosterFactory.Create(2);
            catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            listener = new FakeListener();

            canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));

            GameObject scrollObject =
                new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(canvasObject.transform, false);
            scrollRect = scrollObject.GetComponent<ScrollRect>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollObject.transform, false);
            scrollRect.content = (RectTransform)content.transform;

            cards.Clear(); lifts.Clear(); visuals.Clear();

            for (int i = 0; i < 2; i++)
            {
                GameObject cardObject = new GameObject("Card" + i, typeof(RectTransform));
                cardObject.SetActive(false);
                cardObject.transform.SetParent(content.transform, false);

                GameObject visualObject =
                    new GameObject("VisualRoot", typeof(RectTransform));
                visualObject.transform.SetParent(cardObject.transform, false);
                RectTransform visual = (RectTransform)visualObject.transform;

                CardLiftView lift = cardObject.AddComponent<CardLiftView>();
                SetField(lift, "visualRoot", visual);
                SetField(lift, "liftDistance", Lift);
                SetField(lift, "liftScale", Scale);
                SetField(lift, "transitionSeconds", 0.1f);

                BeastCardView card = cardObject.AddComponent<BeastCardView>();
                SetField(card, "liftView", lift);

                cardObject.SetActive(true);
                card.Bind(roster.Owned[i], palette, catalog, listener);

                cards.Add(card); lifts.Add(lift); visuals.Add(visual);
            }
        }

        [TearDown]
        public void TearDown()
        {
            BeastCardView.TimeProvider = null;

            if (canvasObject != null) { Object.DestroyImmediate(canvasObject); canvasObject = null; }
            if (catalog != null) { Object.DestroyImmediate(catalog); catalog = null; }
            if (palette != null) { Object.DestroyImmediate(palette); palette = null; }

            TestRosterFactory.Destroy(roster);
            roster = null;
        }

        private static PointerEventData Pointer(int id, Vector2 position)
        {
            return new PointerEventData(EventSystem.current)
            {
                pointerId = id,
                position = position,
            };
        }

        /// <summary>Updateを直接呼び、押下継続時間の監視を再現します。</summary>
        private static void PumpUpdate(BeastCardView card)
        {
            card.GetType()
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(card, null);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name + " が見つかりません。");
            field.SetValue(target, value);
        }

        [Test]
        public void BeforeHold_IsNotDragReady()
        {
            cards[0].OnPointerDown(Pointer(0, Vector2.zero));

            now = 0.1f;
            PumpUpdate(cards[0]);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
            Assert.That(cards[0].IsDragReadyVisual, Is.False);
        }

        [Test]
        public void WhenHoldSatisfied_BecomesDragReadyBeforeAnyMovement()
        {
            cards[0].OnPointerDown(Pointer(0, Vector2.zero));

            now = 0.25f;
            PumpUpdate(cards[0]);

            Assert.That(
                lifts[0].State,
                Is.EqualTo(CardLiftView.LiftState.DragReady),
                "移動前に浮き上がり表示になる必要があります。");
            Assert.That(cards[0].CurrentState, Is.EqualTo(CardGestureState.Pending));
        }

        [Test]
        public void Scrolling_NeverBecomesDragReady()
        {
            PointerEventData e = Pointer(0, Vector2.zero);
            cards[0].OnPointerDown(e);

            now = 0.04f;
            e.position = new Vector2(0f, 40f);
            cards[0].OnBeginDrag(e);

            now = 1f;
            PumpUpdate(cards[0]);

            Assert.That(cards[0].CurrentState, Is.EqualTo(CardGestureState.Scrolling));
            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
        }

        [Test]
        public void ReleaseAfterHoldWithoutMoving_ReturnsToNormal()
        {
            PointerEventData e = Pointer(0, Vector2.zero);
            cards[0].OnPointerDown(e);

            now = 0.25f;
            PumpUpdate(cards[0]);
            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.DragReady));

            cards[0].OnPointerUp(e);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
        }

        [Test]
        public void DragKeepsTheLiftedLook()
        {
            PointerEventData e = Pointer(0, Vector2.zero);
            cards[0].OnPointerDown(e);

            now = 0.25f;
            PumpUpdate(cards[0]);

            e.position = new Vector2(0f, 40f);
            cards[0].OnBeginDrag(e);

            Assert.That(cards[0].CurrentState, Is.EqualTo(CardGestureState.SquadDragging));
            Assert.That(
                lifts[0].State,
                Is.EqualTo(CardLiftView.LiftState.Dragging),
                "ドラッグ中も浮いたままにします。");
            Assert.That(cards[0].IsDragReadyVisual, Is.True);
        }

        [Test]
        public void AfterDrop_ReturnsToNormal()
        {
            PointerEventData e = Pointer(0, Vector2.zero);
            cards[0].OnPointerDown(e);
            now = 0.25f;
            PumpUpdate(cards[0]);
            e.position = new Vector2(0f, 40f);
            cards[0].OnBeginDrag(e);

            cards[0].OnEndDrag(e);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
        }

        [Test]
        public void AfterDropOutsideSquad_ReturnsToNormal()
        {
            PointerEventData e = Pointer(0, new Vector2(500f, 500f));
            cards[0].OnPointerDown(e);
            now = 0.25f;
            PumpUpdate(cards[0]);
            e.position = new Vector2(500f, 560f);
            cards[0].OnBeginDrag(e);
            cards[0].OnEndDrag(e);
            cards[0].OnPointerUp(e);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
            Assert.That(cards[0].CurrentState, Is.EqualTo(CardGestureState.Idle));
        }

        [Test]
        public void OnDisable_ReturnsToNormal()
        {
            cards[0].OnPointerDown(Pointer(0, Vector2.zero));
            now = 0.25f;
            PumpUpdate(cards[0]);
            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.DragReady));

            cards[0].gameObject.SetActive(false);

            // EditModeではUnityがOnDisableを配送しないため、実機と同じ順序で呼びます。
            EditModeLifecycle.Disable(cards[0]);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.Normal));
            Assert.That(visuals[0].localScale, Is.EqualTo(Vector3.one));
            Assert.That(visuals[0].localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RepeatedGestures_DoNotAccumulateScaleOrPosition()
        {
            Vector3 baseScale = visuals[0].localScale;
            Vector3 basePosition = visuals[0].localPosition;

            for (int i = 0; i < 5; i++)
            {
                PointerEventData e = Pointer(0, Vector2.zero);
                cards[0].OnPointerDown(e);
                now += 0.25f;
                PumpUpdate(cards[0]);

                e.position = new Vector2(0f, 40f);
                cards[0].OnBeginDrag(e);
                cards[0].OnEndDrag(e);
                cards[0].OnPointerUp(e);

                // 補間を完了させる
                for (int f = 0; f < 20; f++)
                {
                    PumpLiftUpdate(lifts[0]);
                }
            }

            Assert.That(visuals[0].localScale, Is.EqualTo(baseScale));
            Assert.That(visuals[0].localPosition, Is.EqualTo(basePosition));
        }

        [Test]
        public void LiftDoesNotLeakToAnotherCard()
        {
            cards[0].OnPointerDown(Pointer(0, Vector2.zero));
            now = 0.25f;
            PumpUpdate(cards[0]);
            PumpUpdate(cards[1]);

            Assert.That(lifts[0].State, Is.EqualTo(CardLiftView.LiftState.DragReady));
            Assert.That(
                lifts[1].State,
                Is.EqualTo(CardLiftView.LiftState.Normal),
                "別カードへ表示状態が残ってはいけません。");
            Assert.That(visuals[1].localScale, Is.EqualTo(Vector3.one));
        }

        private static void PumpLiftUpdate(CardLiftView lift)
        {
            lift.GetType()
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(lift, null);
        }
    }
}
