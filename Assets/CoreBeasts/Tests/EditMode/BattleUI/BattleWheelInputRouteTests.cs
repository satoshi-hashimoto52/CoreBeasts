using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// EventSystem から <see cref="BattleUnitWheelView"/> までの入力経路。
    ///
    /// ジェスチャー判定そのものは<see cref="BattleWheelGestureTests"/>が見ます。
    /// こちらは「そもそも指がリングに当たるか」を見ます。
    /// GraphicRaycaster は raycastTarget が true の Graphic しか拾わないため、
    /// PlayerWheel 配下に当たり判定が1つも無いと、判定ロジックが正しくても無反応になります。
    /// </summary>
    public sealed class BattleWheelInputRouteTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";

        private Scene scene;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // Additive だと、直前のテストが開いたシーンと同居して
            // Global Light 2D が2つになり、URP 2D が Error を出します。
            // NUnit は想定外の Error ログを失敗として扱うため、常に単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();

            // 単独で開いているときは閉じられません（唯一のシーンは閉じられない）。
            // 次の SetUp が Single で開き直すので、ここでは残しておきます。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------------- 素材 ----------------

        private T FindOne<T>() where T : Component
        {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(
                found.Count,
                Is.EqualTo(1),
                typeof(T).Name + " はシーンに1個だけ存在する必要があります。");

            return found[0];
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
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

        private Transform Find(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
        }

        private Graphic InputSurface()
        {
            Transform surface = Find("WheelInputSurface");

            Graphic graphic = surface.GetComponent<Graphic>();

            Assert.That(
                graphic,
                Is.Not.Null,
                "入力面に Graphic がありません。GraphicRaycaster が拾えません。");

            return graphic;
        }

        // ---------------- 当たり判定の存在 ----------------

        [Test]
        public void ThePlayerWheelHasARaycastTargetToBeHit()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            Graphic[] graphics = wheel.GetComponentsInChildren<Graphic>(true);

            List<Graphic> hittable = new List<Graphic>();

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].raycastTarget)
                {
                    hittable.Add(graphics[i]);
                }
            }

            Assert.That(
                hittable,
                Is.Not.Empty,
                "PlayerWheel 配下に raycastTarget=true の Graphic がありません。" +
                " GraphicRaycaster が何も拾えず、リングは無反応になります。");

            Assert.That(
                hittable[0].name,
                Is.EqualTo("WheelInputSurface"),
                "当たり判定はリング操作専用の入力面が担います。");
        }

        [Test]
        public void TheInputSurfaceIsInvisibleButHittable()
        {
            Graphic surface = InputSurface();

            Assert.That(surface.raycastTarget, Is.True);
            Assert.That(
                surface.color.a,
                Is.EqualTo(0f),
                "入力面は見た目に一切影響させません。");

            Assert.That(
                surface.GetComponent<Button>(),
                Is.Null,
                "Button は不要です。");

            Assert.That(
                surface.GetComponent<EventTrigger>(),
                Is.Null,
                "EventTrigger は使いません。");

            Assert.That(
                surface.GetComponent<Mask>(),
                Is.Null,
                "Mask は付けません。");
        }

        [Test]
        public void TheInputSurfaceCoversEveryVisibleCard()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            RectTransform surface = InputSurface().rectTransform;
            RectTransform wheelRect = wheel.GetComponent<RectTransform>();

            Rect surfaceRect = WorldRect(surface);
            Rect wheelBounds = WorldRect(wheelRect);

            // 横はリング全幅、縦はリングより上へ広げて中央カードの頭まで覆います。
            Assert.That(
                surfaceRect.xMin, Is.LessThanOrEqualTo(wheelBounds.xMin + 0.01f));
            Assert.That(
                surfaceRect.xMax, Is.GreaterThanOrEqualTo(wheelBounds.xMax - 0.01f));
            Assert.That(
                surfaceRect.yMin, Is.LessThanOrEqualTo(wheelBounds.yMin + 0.01f));
            Assert.That(
                surfaceRect.yMax,
                Is.GreaterThan(wheelBounds.yMax),
                "中央カードはリング枠より上へ出るため、入力面も上へ広げます。");

            // 実際に出るカードがすべて入力面の中に収まること。
            BuildWheel(wheel, out _);

            RectTransform content = (RectTransform)GetField(wheel, "content");

            Assert.That(content.childCount, Is.GreaterThan(0));

            for (int i = 0; i < content.childCount; i++)
            {
                BattleUnitWheelItemView item =
                    content.GetChild(i).GetComponent<BattleUnitWheelItemView>();

                if (item == null || !item.gameObject.activeSelf)
                {
                    continue;
                }

                Rect card = WorldRect(item.Root);

                Assert.That(
                    surfaceRect.Overlaps(card),
                    Is.True,
                    item.name + " が入力面の外にあります。");
            }
        }

        [Test]
        public void TheInputSurfaceDoesNotReachTheHistoryLane()
        {
            Rect surface = WorldRect(InputSurface().rectTransform);
            Rect lane = WorldRect(
                FindOne<BattleHistoryLaneView>().GetComponent<RectTransform>());

            Assert.That(
                surface.yMax,
                Is.LessThanOrEqualTo(lane.yMin + 0.01f),
                "入力面が履歴レーンまで届いています。必要な範囲だけを覆います。");
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }

        // ---------------- 前面UIが入力を奪っていないか ----------------

        [Test]
        public void NothingDrawnInFrontStealsTheWheelInput()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            Rect probe = WorldRect(InputSurface().rectTransform);
            Vector2 point = probe.center;

            List<string> blockers = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

                for (int i = 0; i < graphics.Length; i++)
                {
                    Graphic graphic = graphics[i];

                    if (!graphic.gameObject.activeInHierarchy || !graphic.raycastTarget)
                    {
                        continue;
                    }

                    // リング自身の入力面は当然当たります。
                    if (graphic.transform.IsChildOf(wheel.transform))
                    {
                        continue;
                    }

                    // blocksRaycasts=false の下は入力を奪いません。
                    if (IsRaycastBlocked(graphic.transform))
                    {
                        continue;
                    }

                    if (WorldRect(graphic.rectTransform).Contains(point))
                    {
                        blockers.Add(graphic.transform.name);
                    }
                }
            }

            Assert.That(
                blockers,
                Is.Empty,
                "リング中央の上に、入力を奪う Graphic があります: " +
                string.Join(", ", blockers));
        }

        /// <summary>祖先に blocksRaycasts=false の CanvasGroup があるか。</summary>
        private static bool IsRaycastBlocked(Transform target)
        {
            for (Transform node = target; node != null; node = node.parent)
            {
                CanvasGroup group = node.GetComponent<CanvasGroup>();

                if (group != null && !group.blocksRaycasts)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TheOverlayLayersNeverBlockRaycasts()
        {
            CanvasGroup ghost =
                FindOne<BattleDeployTransitionView>().GetComponent<CanvasGroup>();

            CanvasGroup lane =
                FindOne<BattleHistoryLaneView>().GetComponent<CanvasGroup>();

            CanvasGroup guide =
                FindOne<BattleSlideUpGuideView>().GetComponent<CanvasGroup>();

            if (ghost != null)
            {
                Assert.That(ghost.blocksRaycasts, Is.False, "出撃ゴースト層が入力を奪います。");
            }

            Assert.That(lane, Is.Not.Null);
            Assert.That(lane.blocksRaycasts, Is.False, "履歴レーンが入力を奪います。");

            Assert.That(guide, Is.Not.Null);
            Assert.That(guide.blocksRaycasts, Is.False, "SLIDE UP案内が入力を奪います。");
        }

        [Test]
        public void TheGuideGraphicsAreDrawingOnly()
        {
            BattleSlideUpGuideView guide = FindOne<BattleSlideUpGuideView>();

            Graphic[] graphics = guide.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics, Is.Not.Empty);

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " は描画専用にします。");
            }
        }

        [Test]
        public void TheWheelItemGraphicsAreDrawingOnly()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            BuildWheel(wheel, out _);

            RectTransform content = (RectTransform)GetField(wheel, "content");

            Assert.That(content.childCount, Is.GreaterThan(0));

            for (int i = 0; i < content.childCount; i++)
            {
                Graphic[] graphics =
                    content.GetChild(i).GetComponentsInChildren<Graphic>(true);

                for (int g = 0; g < graphics.Length; g++)
                {
                    Assert.That(
                        graphics[g].raycastTarget,
                        Is.False,
                        graphics[g].name +
                        " が入力を消費すると、親のリングまでイベントが届きません。");
                }
            }
        }

        // ---------------- 経路そのもの ----------------

        [Test]
        public void TheWheelImplementsEveryPointerInterfaceItNeeds()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            Assert.That(wheel, Is.InstanceOf<IPointerDownHandler>());
            Assert.That(wheel, Is.InstanceOf<IPointerUpHandler>());
            Assert.That(wheel, Is.InstanceOf<IBeginDragHandler>());
            Assert.That(wheel, Is.InstanceOf<IDragHandler>());
            Assert.That(wheel, Is.InstanceOf<IEndDragHandler>());

            Assert.That(wheel.enabled, Is.True);
            Assert.That(wheel.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void TheCanvasCanActuallyRaycast()
        {
            Canvas canvas = FindOne<BattleUnitWheelView>()
                .GetComponentInParent<Canvas>();

            Assert.That(canvas, Is.Not.Null, "Canvas の下にありません。");

            Assert.That(
                canvas.rootCanvas.GetComponent<GraphicRaycaster>(),
                Is.Not.Null,
                "Canvas に GraphicRaycaster がありません。");

            List<EventSystem> systems = new List<EventSystem>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                systems.AddRange(root.GetComponentsInChildren<EventSystem>(true));
            }

            Assert.That(systems.Count, Is.EqualTo(1), "EventSystem が1つ必要です。");
        }

        [Test]
        public void AHitOnTheInputSurfaceReachesTheWheel()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            Graphic surface = InputSurface();

            // GraphicRaycaster が拾うのは Graphic です。そこから
            // ハンドラを持つ祖先へイベントが上がることを確かめます。
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(
                surface.gameObject);

            Assert.That(
                handler,
                Is.SameAs(wheel.gameObject),
                "入力面のヒットが BattleUnitWheelView まで上がりません。");

            Assert.That(
                ExecuteEvents.GetEventHandler<IDragHandler>(surface.gameObject),
                Is.SameAs(wheel.gameObject));
        }

        // ---------------- 実際にイベントを流す ----------------

        private void BuildWheel(BattleUnitWheelView wheel, out BattleUnitRingModel ring)
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            UiTextCatalog text = (UiTextCatalog)GetField(controller, "text");
            AttributePalette palette = (AttributePalette)GetField(controller, "palette");

            List<string> ids = new List<string>();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                ids.Add("p" + i);
            }

            ring = new BattleUnitRingModel();
            ring.Build(ids);

            wheel.Bind(ring, palette, text);
            wheel.SetInteractable(true);
            wheel.Refresh();

            RectTransform content = (RectTransform)GetField(wheel, "content");

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }
        }

        /// <summary>入力面の中心を画面座標で返します。</summary>
        private Vector2 CentreScreenPoint()
        {
            Rect rect = WorldRect(InputSurface().rectTransform);

            return RectTransformUtility.WorldToScreenPoint(
                null, new Vector3(rect.center.x, rect.center.y, 0f));
        }

        private static PointerEventData Pointer(Vector2 position)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = position,
                pointerId = 0,
            };
        }

        /// <summary>
        /// 実操作と同じように、小さな移動を何フレームにも分けて送ります。
        /// 最後の1フレームの移動量は小さいままなので、
        /// 「最終 delta だけを見ている」実装ならここで落ちます。
        /// </summary>
        private void DragInSteps(
            BattleUnitWheelView wheel, Vector2 start, Vector2 total, int frames)
        {
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            for (int i = 1; i <= frames; i++)
            {
                data.position = start + total * (i / (float)frames);
                wheel.OnDrag(data);
            }

            // 指を離す瞬間は動きがありません（delta = 0）。
            wheel.OnEndDrag(data);
            wheel.OnPointerUp(data);
        }

        /// <summary>1ptあたりの画面座標。ローカル座標系へそろえるための換算です。</summary>
        private float ScreenPerPoint(BattleUnitWheelView wheel)
        {
            Canvas canvas = wheel.GetComponentInParent<Canvas>();

            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;

            return wheel.PointsToUnits * scale;
        }

        [Test]
        public void EightSmallSidewaysDragsAccumulateIntoOneTurn()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            string before = ring.FocusedInstanceId;

            List<string> focusEvents = new List<string>();
            wheel.FocusChanged += id => focusEvents.Add(id);

            // 8回 × 10 で累積 80。1回ぶんの delta は 10 しかありません。
            float unit = 10f * ScreenPerPoint(wheel);

            DragInSteps(wheel, CentreScreenPoint(), new Vector2(-unit * 8f, 0f), 8);

            Assert.That(
                ring.FocusedInstanceId,
                Is.Not.EqualTo(before),
                "累積80ptぶん動かしてもリングが回っていません。" +
                " 最終フレームの delta だけで判定していないか確認してください。");

            Assert.That(focusEvents.Count, Is.EqualTo(1), "回転通知は1回だけです。");
        }

        [Test]
        public void ASmallFinalFrameStillCountsAsAFullDrag()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            string before = ring.FocusedInstanceId;

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

            // 30フレームへ細かく割るので、1フレームぶんは間隔の 3% 程度です。
            DragInSteps(wheel, CentreScreenPoint(), new Vector2(-spacing, 0f), 30);

            Assert.That(
                ring.FocusedInstanceId,
                Is.Not.EqualTo(before),
                "細かく分けて動かすと回らなくなっています。");
        }

        [Test]
        public void ADragBelowTheRatioReturnsToTheSameCentre()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            string before = ring.FocusedInstanceId;

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);
            float below =
                spacing * (BattleWheelGestureStateMachine.OneStepRatio - 0.1f);

            DragInSteps(wheel, CentreScreenPoint(), new Vector2(-below, 0f), 8);

            Assert.That(
                ring.FocusedInstanceId,
                Is.EqualTo(before),
                "閾値未満は元の中央へ戻します。");
        }

        [Test]
        public void AMultiFrameUpwardDragRequestsDeployOnce()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            List<string> deploys = new List<string>();
            wheel.DeployRequested += id => deploys.Add(id);

            string focused = ring.FocusedInstanceId;

            float commit = BattleWheelGestureStateMachine.CommitDistance
                           * ScreenPerPoint(wheel);

            // 10フレームへ割るので、1フレームぶんは 7pt 程度です。
            DragInSteps(wheel, CentreScreenPoint(), new Vector2(0f, commit * 1.1f), 10);

            Assert.That(
                deploys.Count,
                Is.EqualTo(1),
                "累積72ptを超えても出撃していません。");

            Assert.That(deploys[0], Is.EqualTo(focused));
        }

        [Test]
        public void EndDragAndPointerUpTogetherStillDeployOnlyOnce()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out _);

            List<string> deploys = new List<string>();
            wheel.DeployRequested += id => deploys.Add(id);

            float commit = BattleWheelGestureStateMachine.CommitDistance
                           * ScreenPerPoint(wheel);

            Vector2 start = CentreScreenPoint();
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            for (int i = 1; i <= 8; i++)
            {
                data.position = start + new Vector2(0f, commit * 1.2f * (i / 8f));
                wheel.OnDrag(data);
            }

            wheel.OnEndDrag(data);
            wheel.OnPointerUp(data);
            wheel.OnPointerUp(data);

            Assert.That(
                deploys.Count,
                Is.EqualTo(1),
                "OnEndDrag と OnPointerUp の両方で確定してはいけません。");
        }

        [Test]
        public void AShortUpwardDragDoesNotRequestDeploy()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out _);

            List<string> deploys = new List<string>();
            wheel.DeployRequested += id => deploys.Add(id);

            float commit = BattleWheelGestureStateMachine.CommitDistance
                           * ScreenPerPoint(wheel);

            DragInSteps(wheel, CentreScreenPoint(), new Vector2(0f, commit * 0.4f), 8);

            Assert.That(deploys, Is.Empty, "閾値未満で出撃してはいけません。");
        }

        [Test]
        public void LockingSidewaysThenGoingUpNeverDeploys()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out _);

            List<string> deploys = new List<string>();
            wheel.DeployRequested += id => deploys.Add(id);

            float screen = ScreenPerPoint(wheel);
            Vector2 start = CentreScreenPoint();
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            // まず横で方向を確定させます。
            for (int i = 1; i <= 4; i++)
            {
                data.position = start + new Vector2(-40f * screen * (i / 4f), 0f);
                wheel.OnDrag(data);
            }

            // そのあと大きく上へ動かしても、出撃にはなりません。
            for (int i = 1; i <= 8; i++)
            {
                data.position = start + new Vector2(-40f * screen, 300f * screen * (i / 8f));
                wheel.OnDrag(data);
            }

            wheel.OnEndDrag(data);
            wheel.OnPointerUp(data);

            Assert.That(deploys, Is.Empty, "横ロック後に出撃してはいけません。");
        }

        [Test]
        public void LockingUpwardThenGoingSidewaysNeverRotates()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            string before = ring.FocusedInstanceId;

            float screen = ScreenPerPoint(wheel);
            Vector2 start = CentreScreenPoint();
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            for (int i = 1; i <= 4; i++)
            {
                data.position = start + new Vector2(0f, 40f * screen * (i / 4f));
                wheel.OnDrag(data);
            }

            for (int i = 1; i <= 8; i++)
            {
                data.position =
                    start + new Vector2(-400f * screen * (i / 8f), 40f * screen);
                wheel.OnDrag(data);
            }

            wheel.OnEndDrag(data);
            wheel.OnPointerUp(data);

            Assert.That(
                ring.FocusedInstanceId,
                Is.EqualTo(before),
                "上ロック後にリングが回ってはいけません。");
        }

        [Test]
        public void TheSameGestureWorksAtAnyCanvasScale()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            Canvas canvas = wheel.GetComponentInParent<Canvas>().rootCanvas;

            float original = canvas.scaleFactor;

            try
            {
                List<string> results = new List<string>();

                float[] scales = { 1f, 2f, 3f };

                for (int i = 0; i < scales.Length; i++)
                {
                    canvas.scaleFactor = scales[i];
                    Canvas.ForceUpdateCanvases();

                    BuildWheel(wheel, out BattleUnitRingModel ring);

                    string before = ring.FocusedInstanceId;

                    float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

                    DragInSteps(
                        wheel, CentreScreenPoint(), new Vector2(-spacing, 0f), 8);

                    results.Add(ring.FocusedInstanceId == before ? "stay" : "turn");
                }

                Assert.That(
                    results,
                    Is.All.EqualTo("turn"),
                    "Canvas の拡大率で操作量が変わってはいけません: " +
                    string.Join(", ", results));
            }
            finally
            {
                canvas.scaleFactor = original;
                Canvas.ForceUpdateCanvases();
            }
        }

        [Test]
        public void BlockedInputProducesNothingAtAll()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            // 設定パネル表示中・演出中に相当します。
            wheel.SetInteractable(false);

            List<string> deploys = new List<string>();
            List<string> focusEvents = new List<string>();

            wheel.DeployRequested += id => deploys.Add(id);
            wheel.FocusChanged += id => focusEvents.Add(id);

            string before = ring.FocusedInstanceId;
            float screen = ScreenPerPoint(wheel);

            DragInSteps(wheel, CentreScreenPoint(),
                new Vector2(0f, BattleWheelGestureStateMachine.CommitDistance * 1.5f * screen), 8);

            DragInSteps(wheel, CentreScreenPoint(),
                new Vector2(-wheel.ItemSpacingPoints * screen, 0f), 8);

            Assert.That(deploys, Is.Empty);
            Assert.That(focusEvents, Is.Empty);
            Assert.That(ring.FocusedInstanceId, Is.EqualTo(before));
        }

        // ---------------- 指を離さない連続循環 ----------------

        /// <summary>
        /// PointerUp を挟まずに、指を動かし続けたときの中央と表示を追います。
        /// </summary>
        private void SweepWithoutRelease(
            BattleUnitWheelView wheel,
            BattleUnitRingModel ring,
            float totalScreen,
            int frames,
            List<string> centres,
            List<string> problems)
        {
            Vector2 start = CentreScreenPoint();
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            RectTransform content = (RectTransform)GetField(wheel, "content");

            for (int i = 1; i <= frames; i++)
            {
                data.position = start + new Vector2(totalScreen * (i / (float)frames), 0f);
                wheel.OnDrag(data);

                centres.Add(ring.FocusedInstanceId);

                // この瞬間の表示に、空きも重複も無いこと。
                HashSet<string> seen = new HashSet<string>();
                int shown = 0;

                for (int c = 0; c < content.childCount; c++)
                {
                    BattleUnitWheelItemView item =
                        content.GetChild(c).GetComponent<BattleUnitWheelItemView>();

                    if (item == null || !item.gameObject.activeSelf)
                    {
                        continue;
                    }

                    shown++;

                    if (!seen.Add(item.InstanceId))
                    {
                        problems.Add("frame " + i + ": " + item.InstanceId + " が重複");
                    }
                }

                int expected = ring.Count < BattleRingPhase.MaxVisible
                    ? ring.Count
                    : BattleRingPhase.MaxVisible;

                if (shown != expected)
                {
                    problems.Add("frame " + i + ": 表示数 " + shown + " (期待 " + expected + ")");
                }
            }

            // 指はまだ離していません。
        }

        [Test]
        public void HoldingAndDraggingRightCirclesPastOneFullTurn()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

            List<string> centres = new List<string>();
            List<string> problems = new List<string>();

            // 2周ぶん、指を離さずに右へ。
            SweepWithoutRelease(wheel, ring, spacing * 14f, 140, centres, problems);

            Assert.That(problems, Is.Empty, string.Join(" / ", problems));

            HashSet<string> distinct = new HashSet<string>(centres);

            Assert.That(
                distinct.Count,
                Is.EqualTo(BattleSquad.UnitCount),
                "指を離さずに一巡できていません（端で止まっています）。");
        }

        [Test]
        public void HoldingAndDraggingLeftCirclesPastOneFullTurn()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

            List<string> centres = new List<string>();
            List<string> problems = new List<string>();

            SweepWithoutRelease(wheel, ring, -spacing * 14f, 140, centres, problems);

            Assert.That(problems, Is.Empty, string.Join(" / ", problems));

            Assert.That(
                new HashSet<string>(centres).Count,
                Is.EqualTo(BattleSquad.UnitCount),
                "左方向へ一巡できていません。");
        }

        [Test]
        public void TheFocusMovesDuringTheDragWithoutWaitingForPointerUp()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            string before = ring.FocusedInstanceId;

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

            Vector2 start = CentreScreenPoint();
            PointerEventData data = Pointer(start);

            wheel.OnPointerDown(data);
            wheel.OnBeginDrag(data);

            for (int i = 1; i <= 20; i++)
            {
                data.position = start + new Vector2(-spacing * 1.5f * (i / 20f), 0f);
                wheel.OnDrag(data);
            }

            Assert.That(
                ring.FocusedInstanceId,
                Is.Not.EqualTo(before),
                "PointerUp を待たずに中央が更新されていません。");

            Assert.That(
                wheel.RebasedSteps,
                Is.Not.EqualTo(0f),
                "Drag 中に位相の付け替えが起きていません。");

            // 位相は常に1枚ぶんの中へ収まります。
            Assert.That(Mathf.Abs(wheel.Phase), Is.LessThan(1.0001f));
        }

        [Test]
        public void ARemovedUnitNeverReappearsWhileCirculating()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BuildWheel(wheel, out BattleUnitRingModel ring);

            ring.Remove("p1");
            ring.Remove("p4");
            wheel.Refresh();

            float spacing = wheel.ItemSpacingPoints * ScreenPerPoint(wheel);

            List<string> centres = new List<string>();
            List<string> problems = new List<string>();

            SweepWithoutRelease(wheel, ring, spacing * 10f, 100, centres, problems);

            Assert.That(problems, Is.Empty, string.Join(" / ", problems));
            Assert.That(centres, Has.No.Member("p1"));
            Assert.That(centres, Has.No.Member("p4"));
        }

        // ---------------- 入力ゲートの初期値 ----------------

        [Test]
        public void TheGateIsOpenAsSoonAsSelectingStarts()
        {
            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.Selecting, false, false),
                Is.EqualTo(BattleWheelPhase.Selecting));

            Assert.That(
                BattleWheelPhases.AllowsInput(BattleWheelPhase.Selecting, false),
                Is.True,
                "Play直後は設定パネルも演出も無いため、入力を受け付けます。");
        }
    }
}
