using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    public sealed class BeastCardViewGestureTests
    {
        private sealed class FakeListener : IBeastCardListener
        {
            public readonly List<string> Calls = new List<string>();

            public void OnCardTapped(OwnedCoreBeast beast) => Calls.Add("tap");

            public void OnCardDragBegin(OwnedCoreBeast beast, PointerEventData e) =>
                Calls.Add("dragBegin");

            public void OnCardDragMove(PointerEventData e) => Calls.Add("dragMove");

            public void OnCardDragEnd(OwnedCoreBeast beast, PointerEventData e) =>
                Calls.Add("dragEnd");
        }

        private GameObject canvasObject;
        private ScrollRect scrollRect;
        private BeastCardView card;
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
            roster = TestRosterFactory.Create(1);
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

            GameObject cardObject = new GameObject("Card", typeof(RectTransform));
            cardObject.SetActive(false);
            cardObject.transform.SetParent(content.transform, false);
            card = cardObject.AddComponent<BeastCardView>();
            cardObject.SetActive(true);

            card.Bind(roster.Owned[0], palette, catalog, listener);
        }

        [TearDown]
        public void TearDown()
        {
            BeastCardView.TimeProvider = null;

            if (canvasObject != null)
            {
                Object.DestroyImmediate(canvasObject);
                canvasObject = null;
            }

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

        [Test]
        public void Tap_DoesNotStartDragAndReportsTap()
        {
            PointerEventData e = Pointer(0, Vector2.zero);

            card.OnPointerDown(e);
            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Pending));

            now = 0.05f;
            card.OnPointerUp(e);
            card.OnPointerClick(e);

            Assert.That(listener.Calls, Is.EqualTo(new[] { "tap" }));
            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Idle));
        }

        [Test]
        public void QuickSwipe_ScrollsAndLeavesScrollRectEnabled()
        {
            PointerEventData e = Pointer(0, Vector2.zero);

            card.OnPointerDown(e);

            now = 0.04f;
            e.position = new Vector2(0f, 40f);
            card.OnBeginDrag(e);

            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Scrolling));
            Assert.That(
                scrollRect.enabled,
                Is.True,
                "スクロール中はScrollRectを止めません。");
            Assert.That(listener.Calls, Is.Empty, "編成ドラッグは始まりません。");

            card.OnEndDrag(e);
            card.OnPointerUp(e);

            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Idle));
            Assert.That(scrollRect.enabled, Is.True);
        }

        [Test]
        public void HoldThenMove_StartsSquadDragAndSuspendsScrollRect()
        {
            PointerEventData e = Pointer(0, Vector2.zero);

            card.OnPointerDown(e);

            now = 0.30f;
            e.position = new Vector2(0f, 40f);
            card.OnBeginDrag(e);

            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.SquadDragging));
            Assert.That(
                scrollRect.enabled,
                Is.False,
                "編成ドラッグ中はScrollRectを停止します。");
            Assert.That(listener.Calls, Contains.Item("dragBegin"));

            card.OnEndDrag(e);

            Assert.That(listener.Calls, Contains.Item("dragEnd"));
            Assert.That(
                scrollRect.enabled,
                Is.True,
                "操作終了時にScrollRectを必ず復元します。");
            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Idle));
        }

        [Test]
        public void DragThenRelease_DoesNotAlsoFireTap()
        {
            PointerEventData e = Pointer(0, Vector2.zero);

            card.OnPointerDown(e);
            now = 0.30f;
            e.position = new Vector2(0f, 40f);
            card.OnBeginDrag(e);
            card.OnEndDrag(e);
            card.OnPointerUp(e);
            card.OnPointerClick(e);

            Assert.That(listener.Calls, Does.Not.Contain("tap"));
        }

        [Test]
        public void ScrollRectIsRestoredEvenIfCardIsDisabledMidDrag()
        {
            PointerEventData e = Pointer(0, Vector2.zero);

            card.OnPointerDown(e);
            now = 0.30f;
            e.position = new Vector2(0f, 40f);
            card.OnBeginDrag(e);

            Assert.That(scrollRect.enabled, Is.False);

            card.gameObject.SetActive(false);

            // EditModeではUnityがOnDisableを配送しないため、実機と同じ順序で呼びます。
            EditModeLifecycle.Disable(card);

            Assert.That(
                scrollRect.enabled,
                Is.True,
                "途中で無効化されてもScrollRectを戻します。");
        }
    }
}
