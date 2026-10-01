using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 一覧から MY SQUAD へ登録できることを、実操作の経路で固定します。
    ///
    /// PlayMode で動かします。
    /// EditMode では Canvas が実際には描画されないため、
    /// GraphicRaycaster が全 Graphic を足切りしてヒット0になり、
    /// UnitSetScreen.TryDropByRaycast の経路を検証できませんでした。
    /// PlayMode なら EventSystem・GraphicRaycaster・CanvasRenderer が
    /// すべて実物として動くため、Drop 判定をそのまま通せます。
    ///
    /// Awake / OnEnable は手動で呼びません。Scene をロードして Unity に任せ、
    /// 初期化が終わるまでフレームを進めます。
    ///
    /// 指を離したときの配送順は Unity に合わせます。
    ///   OnPointerUp → OnDrop → OnEndDrag
    /// </summary>
    public sealed class SquadDropIntegrationTests
    {
        private const string SceneName = "UnitSet";

        /// <summary>初期化が終わるまで進めるフレーム数。</summary>
        private const int WarmUpFrames = 4;

        private UnitSetScreen screen;
        private DragGhostPresenter ghost;
        private SquadFormation formation;
        private SquadEditor editor;
        private CoreBeastRoster roster;
        private string setId;

        private readonly List<BeastCardView> cards = new List<BeastCardView>();
        private readonly List<SquadSlotView> slots = new List<SquadSlotView>();

        private ISquadRepository originalRepository;
        private float now;
        private static int emptySceneCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            now = 0f;
            BeastCardView.TimeProvider = () => now;

            // 1. 空のシーンから始めます。
            Scene bootstrap = SceneManager.CreateScene(
                "SquadDropTests_Bootstrap_" + emptySceneCounter++);

            SceneManager.SetActiveScene(bootstrap);

            yield return null;

            // 2. 保存状態を退避し、テスト用の空の保存先へ差し替えます。
            //    UnitSetScreen.Awake が保存済み編成を読み込むため、
            //    Scene をロードする前に済ませます。
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            // 3. UnitSet をロードします。Awake / Start は Unity が呼びます。
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            // 4. Canvas の描画・LayoutGroup・EventSystem の初期化が終わるまで進めます。
            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            yield return null;

            screen = FindOne<UnitSetScreen>();

            Assert.That(
                screen.enabled,
                Is.True,
                "UnitSetScreen が参照不足で無効化されました。");

            ghost = FindOne<DragGhostPresenter>();
            formation = (SquadFormation)GetField(screen, "formation");
            editor = (SquadEditor)GetField(screen, "editor");
            roster = (CoreBeastRoster)GetField(screen, "roster");
            setId = (string)GetField(screen, "setId");

            Assert.That(
                formation.OccupiedCount,
                Is.EqualTo(0),
                "画面初期化の時点で編成が空になっていません。");

            cards.Clear();
            cards.AddRange(Find("Content").GetComponentsInChildren<BeastCardView>(true));

            slots.Clear();
            slots.AddRange(Find("SquadRow").GetComponentsInChildren<SquadSlotView>(true));

            Assert.That(
                cards.Count,
                Is.GreaterThanOrEqualTo(SquadFormation.SlotCount + 1),
                "一覧のカードが足りません。");

            Assert.That(
                slots.Count,
                Is.EqualTo(SquadFormation.SlotCount),
                "MY SQUAD の枠が " + SquadFormation.SlotCount + " 個ではありません。");

            Assert.That(
                EventSystem.current,
                Is.Not.Null,
                "EventSystem.current がありません。");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BeastCardView.TimeProvider = null;

            // ドラッグ表示を確実に消します。
            if (ghost != null)
            {
                ghost.Hide();
            }

            // 保存状態を元へ戻します。
            SquadRepositoryProvider.SetShared(originalRepository);
            originalRepository = null;

            cards.Clear();
            slots.Clear();

            screen = null;
            ghost = null;
            formation = null;
            editor = null;
            roster = null;

            // ロードした Scene を確実に降ろします。
            Scene loaded = SceneManager.GetSceneByName(SceneName);

            Scene empty = SceneManager.CreateScene(
                "SquadDropTests_TearDown_" + emptySceneCounter++);

            SceneManager.SetActiveScene(empty);

            if (loaded.IsValid() && loaded.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loaded);
            }

            yield return null;
        }

        // ---------------- 道具 ----------------

        private static T FindOne<T>() where T : Component
        {
            List<T> found = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    found.AddRange(root.GetComponentsInChildren<T>(true));
                }
            }

            Assert.That(
                found.Count, Is.EqualTo(1),
                typeof(T).Name + " は1個だけ存在する必要があります（実際 " +
                found.Count + " 個）。");

            return found[0];
        }

        private static Transform Find(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Transform found = FindDeep(root.transform, name);

                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            Assert.Fail(name + " がありません。");

            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private static PointerEventData Pointer(Vector2 position)
        {
            return new PointerEventData(EventSystem.current)
            {
                pointerId = 0,
                position = position,
            };
        }

        /// <summary>Overlay ならカメラ不要。それ以外は Canvas の worldCamera を使います。</summary>
        private static Camera EventCameraOf(Component node)
        {
            Canvas canvas = node.GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                return null;
            }

            Canvas root = canvas.rootCanvas;

            return root.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;
        }

        /// <summary>この矩形の中心の、画面座標。</summary>
        private static Vector2 ScreenCentreOf(Transform node)
        {
            RectTransform rect = (RectTransform)node;

            Vector3 world = rect.TransformPoint(rect.rect.center);

            return RectTransformUtility.WorldToScreenPoint(EventCameraOf(rect), world);
        }

        private static string PathOf(Transform node)
        {
            string path = node.name;

            while (node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// EventSystem.RaycastAll が枠へ到達することを確かめ、
        /// 実際に拾えた GameObject のパスを返します。
        ///
        /// 前提を段階に分けて確かめ、落ちた段階が分かるようにします。
        ///   (1) 枠の矩形が潰れていないか
        ///   (2) 枠に raycastTarget があるか
        ///   (3) 親の CanvasGroup が入力を止めていないか
        ///   (4) 画面座標が枠の矩形の中に入るか
        ///   (5) EventSystem / GraphicRaycaster が生きているか
        ///   (6) RaycastAll が枠へ到達するか
        /// </summary>
        private static string AssertRaycastFindsSlot(SquadSlotView slot)
        {
            RectTransform slotRect = (RectTransform)slot.transform;

            // (1) レイアウト
            Assert.That(
                slotRect.rect.width * slotRect.rect.height,
                Is.GreaterThan(0f),
                "Slot " + slot.SlotIndex + " の矩形が潰れています（" +
                slotRect.rect.width + " x " + slotRect.rect.height + "）。");

            // (2) raycastTarget
            Graphic target = null;

            foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget && graphic.gameObject.activeInHierarchy
                    && graphic.enabled)
                {
                    target = graphic;
                    break;
                }
            }

            Assert.That(
                target, Is.Not.Null,
                "Slot " + slot.SlotIndex + " に raycastTarget の Graphic がありません。");

            // (3) CanvasGroup
            foreach (CanvasGroup group in slot.GetComponentsInParent<CanvasGroup>(true))
            {
                Assert.That(
                    group.blocksRaycasts, Is.True,
                    "Slot " + slot.SlotIndex + " の親 " + group.name +
                    " が blocksRaycasts=false で入力を止めています。");
            }

            // (4) 座標変換
            Vector2 screenPoint = ScreenCentreOf(slot.transform);

            Assert.That(
                RectTransformUtility.RectangleContainsScreenPoint(
                    target.rectTransform, screenPoint, EventCameraOf(slot)),
                Is.True,
                "Slot " + slot.SlotIndex + " の中心が、その矩形の中に入りません。" +
                Describe(slot, screenPoint));

            // (5) 入力系
            Assert.That(EventSystem.current, Is.Not.Null, "EventSystem.current が null です。");

            Assert.That(
                EventSystem.current.isActiveAndEnabled,
                Is.True, "EventSystem が有効ではありません。");

            GraphicRaycaster raycaster = slot.GetComponentInParent<Canvas>()
                .rootCanvas.GetComponent<GraphicRaycaster>();

            Assert.That(
                raycaster, Is.Not.Null, "ルート Canvas に GraphicRaycaster がありません。");

            Assert.That(
                raycaster.isActiveAndEnabled,
                Is.True, "GraphicRaycaster が有効ではありません。");

            // (6) RaycastAll
            List<RaycastResult> results = new List<RaycastResult>();

            EventSystem.current.RaycastAll(Pointer(screenPoint), results);

            // 子 Graphic が返るため、親を辿って正式な Drop 対象へ解決します。
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject == null)
                {
                    continue;
                }

                if (results[i].gameObject.GetComponentInParent<SquadSlotView>() == slot)
                {
                    return PathOf(results[i].gameObject.transform);
                }
            }

            List<RaycastResult> direct = new List<RaycastResult>();
            raycaster.Raycast(Pointer(screenPoint), direct);

            string hits = "(なし)";

            if (results.Count > 0)
            {
                hits = string.Empty;

                for (int i = 0; i < results.Count; i++)
                {
                    hits += "\n      " + PathOf(results[i].gameObject.transform);
                }
            }

            Assert.Fail(
                "RaycastAll が Slot " + slot.SlotIndex + " へ到達しません。" +
                "\n  EventSystem.RaycastAll ヒット数: " + results.Count +
                "\n  GraphicRaycaster.Raycast 直接呼び出しのヒット数: " + direct.Count +
                Describe(slot, screenPoint) +
                "\n  ヒット一覧:" + hits);

            return null;
        }

        private static string Describe(SquadSlotView slot, Vector2 screenPoint)
        {
            RectTransform rect = (RectTransform)slot.transform;

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Canvas root = slot.GetComponentInParent<Canvas>().rootCanvas;

            return
                "\n  slot rect      : " + rect.rect +
                "\n  slot world     : " + corners[0] + " .. " + corners[2] +
                "\n  screen point   : " + screenPoint +
                "\n  canvas         : renderMode=" + root.renderMode +
                " scaleFactor=" + root.scaleFactor +
                "\n  Screen         : " + Screen.width + " x " + Screen.height;
        }

        /// <summary>実際の配送順で、カードを枠へドラッグします。</summary>
        private IEnumerator DragCardOntoSlot(BeastCardView card, SquadSlotView slot)
        {
            float threshold = (float)GetField(card, "dragStartDistance");

            Vector2 from = ScreenCentreOf(card.transform);
            Vector2 to = ScreenCentreOf(slot.transform);

            Assert.That(
                (to - from).magnitude,
                Is.GreaterThan(threshold),
                "枠とカードが近すぎて、しきい値を超えられません。");

            PointerEventData pointer = Pointer(from);

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);

            // 長押しは BeastCardView.Update が判定します。フレームを進めて待ちます。
            now += 1f;
            yield return null;

            Assert.That(
                card.IsDragReadyVisual,
                Is.True,
                card.name + ": 長押しで DragReady になりません。");

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                card.name + ": 長押しだけで Ghost が出ました。");

            pointer.position = to;
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);

            Assert.That(
                card.CurrentState,
                Is.EqualTo(CardGestureState.SquadDragging),
                card.name + ": しきい値を超えてもドラッグになりません。");

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(1),
                card.name + ": ドラッグ中に Ghost が1つではありません。");

            yield return null;

            // Unity の解放順に合わせます。
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(slot.gameObject, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);

            yield return null;
        }

        private void AssertSettled(BeastCardView card)
        {
            Assert.That(
                ghost.VisibleGhostCount, Is.EqualTo(0), "終了直後に Ghost が残っています。");

            Assert.That(
                ghost.IsShowing, Is.False, "終了直後に Presenter が表示中です。");

            Assert.That(
                card.CurrentState,
                Is.EqualTo(CardGestureState.Idle),
                "終了後に Idle へ戻っていません。");
        }

        private static OwnedCoreBeast BeastOf(BeastCardView card)
        {
            Assert.That(card.Beast, Is.Not.Null, card.name + " に個体が割り当たっていません。");

            return card.Beast;
        }

        /// <summary>この枠に機械獣が表示されているか。</summary>
        private static bool SlotShowsACreature(SquadSlotView slot)
        {
            BeastThumbnailView thumbnail =
                slot.GetComponentInChildren<BeastThumbnailView>(true);

            if (thumbnail == null || !thumbnail.gameObject.activeInHierarchy)
            {
                return false;
            }

            foreach (RawImage layer in thumbnail.GetComponentsInChildren<RawImage>(true))
            {
                if (layer.gameObject.activeInHierarchy && layer.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRaycastTarget(SquadSlotView slot)
        {
            foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------- 1. 空き枠へ入れられること ----------------

        [UnityTest]
        public IEnumerator DraggingARosterUnitIntoAnEmptySlotAddsItToTheSquad()
        {
            SquadSlotView slot = slots[0];

            string hit = AssertRaycastFindsSlot(slot);

            Assert.That(
                hit, Is.Not.Null.And.Not.Empty,
                "RaycastAll が拾った GameObject を特定できません。");

            BeastCardView card = cards[0];
            OwnedCoreBeast beast = BeastOf(card);

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.Null,
                "検査開始時に枠が空ではありません。");

            yield return DragCardOntoSlot(card, slot);

            // ApplyDrop まで到達した証拠。
            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(beast),
                "Slot " + slot.SlotIndex + " へ登録されていません（RaycastAll が拾ったのは " +
                hit + "）。");

            Assert.That(
                editor.Formation.IndexOfInstance(beast.InstanceId),
                Is.EqualTo(slot.SlotIndex),
                "SquadEditor 側の位置が一致しません。");

            // SquadBar の表示も一致していること。
            Assert.That(
                SlotShowsACreature(slot),
                Is.True,
                "Slot " + slot.SlotIndex + " に機械獣が表示されていません。");

            for (int i = 1; i < slots.Count; i++)
            {
                Assert.That(
                    SlotShowsACreature(slots[i]),
                    Is.False,
                    "空のはずの Slot " + slots[i].SlotIndex + " に機械獣が出ています。");
            }

            AssertSettled(card);
        }

        // ---------------- 2. 7枠すべて ----------------

        [UnityTest]
        public IEnumerator DroppingIntoEachOfSevenSlotsWorks()
        {
            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                SquadSlotView slot = slots[i];
                BeastCardView card = cards[i];
                OwnedCoreBeast beast = BeastOf(card);

                AssertRaycastFindsSlot(slot);

                yield return DragCardOntoSlot(card, slot);

                Assert.That(
                    formation.GetAt(slot.SlotIndex),
                    Is.SameAs(beast),
                    "Slot " + slot.SlotIndex + " へ登録されていません。");

                AssertSettled(card);
            }

            Assert.That(
                formation.OccupiedCount,
                Is.EqualTo(SquadFormation.SlotCount),
                "7枠すべてが埋まっていません。");
        }

        // ---------------- 3. 成立した登録が取り消されないこと ----------------

        [UnityTest]
        public IEnumerator SuccessfulDropIsNotCancelledByPointerUpOrOnDisable()
        {
            SquadSlotView slot = slots[2];

            AssertRaycastFindsSlot(slot);

            BeastCardView card = cards[3];
            OwnedCoreBeast beast = BeastOf(card);

            yield return DragCardOntoSlot(card, slot);

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(beast),
                "そもそも登録できていません（検査が空振りします）。");

            // 一覧の作り直しで、成立済みのカードが無効化されます。
            card.gameObject.SetActive(false);

            yield return null;

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(beast),
                "OnDisable の後始末で登録が取り消されました。");

            // さらに終了通知が重ねて届いても変わりません。
            screen.OnCardDragEnd(beast, null);

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(beast),
                "二重の終了通知で登録が取り消されました。");

            Assert.That(
                formation.OccupiedCount,
                Is.EqualTo(1),
                "登録数が増減しました（終了処理の二重実行）。");

            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(0));
        }

        // ---------------- 4. 後始末がドロップ先を壊さないこと ----------------

        [UnityTest]
        public IEnumerator GhostCleanupDoesNotDeleteDropTargets()
        {
            BeastCardView card = cards[0];
            OwnedCoreBeast beast = BeastOf(card);

            int[] indexBefore = new int[slots.Count];
            bool[] raycastBefore = new bool[slots.Count];

            for (int i = 0; i < slots.Count; i++)
            {
                indexBefore[i] = slots[i].SlotIndex;
                raycastBefore[i] = HasRaycastTarget(slots[i]);
            }

            PointerEventData pointer = Pointer(ScreenCentreOf(card.transform));

            ghost.Show(
                beast,
                (AttributePalette)GetField(screen, "palette"),
                (UiTextCatalog)GetField(screen, "text"),
                pointer);

            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(1));

            ghost.Hide();
            ghost.Hide();

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "Hide の直後に Ghost が残っています。");

            yield return null;

            SquadSlotView[] after =
                Find("SquadRow").GetComponentsInChildren<SquadSlotView>(true);

            Assert.That(
                after.Length,
                Is.EqualTo(SquadFormation.SlotCount),
                "Ghost の後始末で SquadSlot が消えました。");

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.That(slots[i], Is.Not.Null, "SquadSlot が破棄されました。");

                Assert.That(
                    slots[i].SlotIndex, Is.EqualTo(indexBefore[i]),
                    "slotIndex が失われました。");

                Assert.That(
                    HasRaycastTarget(slots[i]), Is.EqualTo(raycastBefore[i]),
                    "Raycast 対象が失われました。");

                Assert.That(
                    GetField(slots[i], "listener"), Is.Not.Null,
                    "listener が失われました。");
            }

            // 後始末のあとでも、実際に登録できること。
            AssertRaycastFindsSlot(slots[0]);

            yield return DragCardOntoSlot(card, slots[0]);

            Assert.That(
                formation.GetAt(0),
                Is.SameAs(beast),
                "Ghost の後始末のあと、登録できなくなりました。");
        }

        // ---------------- 5. 枠の外で離したら編成は変わらない ----------------

        [UnityTest]
        public IEnumerator DroppingOutsideTheSquadCancelsWithoutChangingTheSquad()
        {
            SquadSlotView slot = slots[1];

            AssertRaycastFindsSlot(slot);

            // まず正常に1体入れて、比較の基準を作ります。
            yield return DragCardOntoSlot(cards[0], slot);

            OwnedCoreBeast placed = BeastOf(cards[0]);

            Assert.That(formation.GetAt(slot.SlotIndex), Is.SameAs(placed));
            Assert.That(formation.OccupiedCount, Is.EqualTo(1));

            // 次は枠の外（一覧の見出し）で離します。
            BeastCardView card = cards[4];

            float threshold = (float)GetField(card, "dragStartDistance");

            Vector2 from = ScreenCentreOf(card.transform);
            Vector2 outside = ScreenCentreOf(Find("RosterHeading"));

            PointerEventData pointer = Pointer(from);

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);

            now += 1f;
            yield return null;

            pointer.position = from + new Vector2(0f, threshold * 2f);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);

            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.SquadDragging));

            pointer.position = outside;
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);

            yield return null;

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(placed),
                "枠の外で離したのに、既存の編成が変わりました。");

            Assert.That(
                formation.IndexOfInstance(BeastOf(card).InstanceId),
                Is.EqualTo(-1),
                "枠の外で離したのに登録されました。");

            Assert.That(
                formation.OccupiedCount,
                Is.EqualTo(1),
                "枠の外で離したのに編成数が変わりました。");

            AssertSettled(card);
        }

        // ---------------- 6. 保存して開き直しても残ること ----------------

        [UnityTest]
        public IEnumerator SavingAndReopeningPreservesTheDroppedUnit()
        {
            SquadSlotView slot = slots[4];

            AssertRaycastFindsSlot(slot);

            BeastCardView card = cards[2];
            OwnedCoreBeast beast = BeastOf(card);

            yield return DragCardOntoSlot(card, slot);

            Assert.That(
                formation.GetAt(slot.SlotIndex),
                Is.SameAs(beast),
                "そもそも登録できていません（検査が空振りします）。");

            // SAVE SET を実物のボタン経由で押します。
            Button saveButton = (Button)GetField(screen, "saveButton");

            Assert.That(saveButton, Is.Not.Null, "SAVE SET ボタンがありません。");

            saveButton.onClick.Invoke();

            yield return null;

            Assert.That(
                SquadRepositoryProvider.Shared.TryLoad(setId, out SquadSnapshot snapshot),
                Is.True,
                "SAVE SET のあと、保存内容を読み出せません。");

            // 画面を開き直した状態を作ります。
            SquadFormation reopened = new SquadFormation();
            reopened.Restore(snapshot, roster);

            Assert.That(
                reopened.GetAt(slot.SlotIndex),
                Is.Not.Null,
                "開き直すと Slot " + slot.SlotIndex + " が空になります。");

            Assert.That(
                reopened.GetAt(slot.SlotIndex).InstanceId,
                Is.EqualTo(beast.InstanceId),
                "開き直すと別の個体になります。");

            Assert.That(
                reopened.OccupiedCount, Is.EqualTo(1), "開き直すと編成数が変わります。");
        }

        // ---------------- 7. 入れ替えと解除 ----------------

        [UnityTest]
        public IEnumerator ReplacingAndRemovingUnitsStillWorks()
        {
            AssertRaycastFindsSlot(slots[0]);

            BeastCardView first = cards[0];
            BeastCardView second = cards[1];

            yield return DragCardOntoSlot(first, slots[0]);

            Assert.That(formation.GetAt(0), Is.SameAs(BeastOf(first)));

            // 同じ枠へ別の個体を入れると入れ替わります。
            yield return DragCardOntoSlot(second, slots[0]);

            Assert.That(
                formation.GetAt(0),
                Is.SameAs(BeastOf(second)),
                "同じ枠への入れ替えができません。");

            Assert.That(
                formation.OccupiedCount, Is.EqualTo(1), "入れ替えで編成数が増えました。");

            // 枠をタップして解除します（既存の操作）。
            screen.OnSlotTapped(0);

            yield return null;

            Assert.That(
                formation.GetAt(0), Is.Null, "枠をタップしても解除できません。");

            Assert.That(formation.OccupiedCount, Is.EqualTo(0));

            // 7体まで入り、上限を超えません。
            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                yield return DragCardOntoSlot(cards[i], slots[i]);
            }

            Assert.That(
                formation.OccupiedCount,
                Is.EqualTo(SquadFormation.SlotCount),
                "7体まで入りません。");

            Assert.That(
                formation.OccupiedCount,
                Is.LessThanOrEqualTo(SquadFormation.SlotCount),
                "7体の上限を超えました。");
        }
    }
}
