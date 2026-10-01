using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 「画面に出ている機械獣は何体か」を固定します。
    ///
    /// 実画面で、1体のはずの機械獣がカード列の下へもう1体、
    /// カードより大きく重複表示されました。
    /// 大きさ(DragGhost 240x290 に対しカード 210x255)と位置から、
    /// 重複していたのは DragGhost です。
    ///
    /// 操作の取り決め:
    /// - ボタンを押していない PointerEnter だけでは、DragGhost を作りも出しもしません。
    /// - ホバー(長押し成立)では HoverLiftRoot 配下の3層だけが動き、機械獣は1体だけです。
    /// - DragGhost は PointerDown 後、移動がしきい値を超えたときにだけ1つ出ます。
    /// - PointerUp / EndDrag / 中断では必ず消えます。次のホバーへ残しません。
    ///
    /// 実シーンを開き、実物の DragGhostPresenter と UnitSetScreen を使います。
    /// </summary>
    public sealed class RosterHoverAndGhostTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";

        private const int CardCount = 8;
        private const int Columns = 4;

        /// <summary>1体の機械獣を構成する層の数（Base / Primary / Secondary）。</summary>
        private const int LayersPerCreature = 3;

        private Scene scene;
        private readonly List<GameObject> cards = new List<GameObject>();

        private UnitSetScreen screen;
        private DragGhostPresenter ghost;
        private RectTransform viewport;
        private RectTransform content;
        private CoreBeastRoster roster;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            BeastCardView.TimeProvider = () => now;

            // Additive だと直前のシーンと同居して Global Light 2D が2つになり、
            // URP 2D が Error を出します。NUnit は想定外の Error を失敗にするため単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");

            screen = FindOne<UnitSetScreen>();
            ghost = FindOne<DragGhostPresenter>();

            viewport = (RectTransform)Find("Viewport");
            content = (RectTransform)Find("Content");

            roster = (CoreBeastRoster)GetField(screen, "roster");

            Assume.That(roster, Is.Not.Null, "UnitSetScreen に roster が設定されていません。");
            Assume.That(
                roster.Owned.Count,
                Is.GreaterThanOrEqualTo(CardCount),
                "所持個体が " + CardCount + " 体未満です。");

            // Start は EditMode で走らないため、通知先として成立する最小限だけ起こします。
            SquadFormation formation = (SquadFormation)GetField(screen, "formation");
            SetField(screen, "editor", new SquadEditor(formation));

            SquadBarView squadBar = (SquadBarView)GetField(screen, "squadBar");
            UiTextCatalog text = (UiTextCatalog)GetField(screen, "text");

            squadBar.Build(text, screen);

            BuildRoster();
        }

        [TearDown]
        public void TearDown()
        {
            BeastCardView.TimeProvider = null;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    Object.DestroyImmediate(cards[i]);
                }
            }

            cards.Clear();

            screen = null;
            ghost = null;
            viewport = null;
            content = null;
            roster = null;

            // 最後の1枚を閉じると Unity が
            // 「Unloading the last loaded scene ... is not supported」を出します。
            // 次の SetUp が Single で開き直すので、1枚だけのときは残しておきます。
            // 保存はしないため、テスト操作はシーンへ残りません。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------------- 道具 ----------------

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

            Assert.Fail(name + " がシーンにありません。");

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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            field.SetValue(target, value);
        }

        private void BuildRoster()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            AttributePalette palette = (AttributePalette)GetField(screen, "palette");
            UiTextCatalog text = (UiTextCatalog)GetField(screen, "text");

            for (int i = 0; i < CardCount; i++)
            {
                GameObject card = Object.Instantiate(asset, content);
                card.name = "Card" + i;

                card.GetComponent<BeastCardView>()
                    .Bind(roster.Owned[i], palette, text, screen);

                cards.Add(card);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private static BeastCardView View(GameObject card)
        {
            return card.GetComponent<BeastCardView>();
        }

        private static Transform Part(GameObject card, string name)
        {
            Transform found = FindDeep(card.transform, name);

            Assert.That(found, Is.Not.Null, card.name + " に " + name + " がありません。");

            return found;
        }

        private static PointerEventData Pointer(Vector2 position)
        {
            return new PointerEventData(EventSystem.current)
            {
                pointerId = 0,
                position = position,
            };
        }

        private static Rect WorldRect(Transform node)
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)node).GetWorldCorners(corners);

            float minX = corners[0].x;
            float maxX = corners[0].x;
            float minY = corners[0].y;
            float maxY = corners[0].y;

            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// 論理Creature = 表示中の <see cref="BeastThumbnailView"/> 1つ。
        /// 1体を Base / Primary / Secondary の3枚の RawImage で描きます。
        ///
        /// BeastCard・SquadSlot・DragGhost は同じ3枚のTextureを共有し、
        /// 色（tint）だけで個体差を出しています。
        /// そのため「同じTextureのRawImageを数える」方法では体数を数えられません。
        /// 数える単位は必ず BeastThumbnailView です。
        /// </summary>
        private List<BeastThumbnailView> VisibleCreatures()
        {
            List<BeastThumbnailView> found = new List<BeastThumbnailView>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BeastThumbnailView thumbnail in
                    root.GetComponentsInChildren<BeastThumbnailView>(true))
                {
                    if (IsCreatureVisible(thumbnail))
                    {
                        found.Add(thumbnail);
                    }
                }
            }

            return found;
        }

        /// <summary>層が1枚でも描かれていれば、その機械獣は見えています。</summary>
        private static bool IsCreatureVisible(BeastThumbnailView thumbnail)
        {
            if (!thumbnail.gameObject.activeInHierarchy)
            {
                return false;
            }

            foreach (RawImage layer in
                thumbnail.GetComponentsInChildren<RawImage>(true))
            {
                if (layer.gameObject.activeInHierarchy && layer.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>この機械獣が、一覧のどれかのカードの中に居るか。</summary>
        private bool IsInsideARosterCard(BeastThumbnailView thumbnail)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                if (thumbnail.transform.IsChildOf(cards[i].transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>一覧のカードの中に居る機械獣の数。正常時はカード枚数と同じです。</summary>
        private int RosterCreatureCount()
        {
            int count = 0;

            List<BeastThumbnailView> visible = VisibleCreatures();

            for (int i = 0; i < visible.Count; i++)
            {
                if (IsInsideARosterCard(visible[i]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>一覧のカードの外に居る機械獣（MY SQUADの枠など）。</summary>
        private List<BeastThumbnailView> CreaturesOutsideTheRoster()
        {
            List<BeastThumbnailView> outside = new List<BeastThumbnailView>();

            List<BeastThumbnailView> visible = VisibleCreatures();

            for (int i = 0; i < visible.Count; i++)
            {
                if (!IsInsideARosterCard(visible[i]))
                {
                    outside.Add(visible[i]);
                }
            }

            return outside;
        }

        /// <summary>指定の根の下に居る機械獣の数。DragGhostLayer の検査に使います。</summary>
        private int CreatureCountUnder(Transform root)
        {
            int count = 0;

            List<BeastThumbnailView> visible = VisibleCreatures();

            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i].transform.IsChildOf(root))
                {
                    count++;
                }
            }

            return count;
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
        /// BeastCardView.Update を直接呼びます。
        /// 長押し成立はここで決まるため、EditMode では自分で回す必要があります。
        /// </summary>
        private static void PumpCard(BeastCardView card)
        {
            EditModeLifecycle.Update(card);
        }

        /// <summary>長押し成立（＝ホバー表示）まで進めます。指は動かしません。</summary>
        private PointerEventData Hover(GameObject card, Vector2 at)
        {
            PointerEventData pointer = Pointer(at);

            View(card).OnPointerDown(pointer);

            // 長押し時間を満たします。移動量は0のままなのでドラッグにはなりません。
            now += 1f;
            PumpCard(View(card));

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.Pending),
                card.name + ": 動かしていないのに Pending から出ています。");

            CardLiftView lift = card.GetComponent<CardLiftView>();

            Assert.That(
                lift.State,
                Is.EqualTo(CardLiftView.LiftState.DragReady),
                card.name + ": 長押しでホバー表示になっていません（検査が空振りします）。");

            // 浮き上がりの補間は CardLiftView.Update が進めます。
            // EditMode では回らないため、ジェスチャーが決めた状態の見た目を確定させます。
            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                lift.LiftProgress,
                Is.EqualTo(1f).Within(0.0001f),
                card.name + ": 浮き上がりが反映されていません。");

            return pointer;
        }

        private void Release(GameObject card, PointerEventData pointer)
        {
            View(card).OnPointerUp(pointer);
            View(card).OnPointerClick(pointer);

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.Idle),
                card.name + ": 離しても Idle へ戻っていません。");

            CardLiftView lift = card.GetComponent<CardLiftView>();

            Assert.That(
                lift.State,
                Is.EqualTo(CardLiftView.LiftState.Normal),
                card.name + ": 離してもホバー表示のままです。");

            lift.SetStateImmediate(CardLiftView.LiftState.Normal);
        }

        // ---------------- 1. ホバーだけでは Ghost を出さない ----------------

        [Test]
        public void HoveringNeverCreatesOrShowsADragGhost()
        {
            Assert.That(ghost.IsShowing, Is.False, "開始時点でGhostが出ています。");
            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(0));

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = cards[i];

                // (a) ボタンを押していない PointerEnter だけ。
                PointerEventData enter = Pointer(WorldRect(card.transform).center);

                ExecuteEvents.Execute(card, enter, ExecuteEvents.pointerEnterHandler);

                Assert.That(
                    ghost.IsShowing,
                    Is.False,
                    card.name + ": PointerEnter だけで DragGhost が出ました。");

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(0),
                    card.name + ": PointerEnter だけで DragGhost が生成されました。");

                Assert.That(
                    View(card).CurrentState,
                    Is.EqualTo(CardGestureState.Idle),
                    card.name + ": PointerEnter だけで状態が変わりました。");

                // (b) PointerExit だけでドラッグを始めない。
                ExecuteEvents.Execute(card, enter, ExecuteEvents.pointerExitHandler);

                Assert.That(
                    View(card).CurrentState,
                    Is.EqualTo(CardGestureState.Idle),
                    card.name + ": PointerExit だけでドラッグが始まりました。");

                Assert.That(ghost.VisibleGhostCount, Is.EqualTo(0));

                // (c) 長押し成立（ホバー表示）まで進めても Ghost は出ない。
                PointerEventData pointer = Hover(card, WorldRect(card.transform).center);

                Assert.That(
                    ghost.IsShowing,
                    Is.False,
                    card.name + ": ホバーだけで DragGhost が出ました。");

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(0),
                    card.name + ": ホバーだけで DragGhost が生成されました。");

                Assert.That(
                    View(card).CurrentState,
                    Is.Not.EqualTo(CardGestureState.SquadDragging),
                    card.name + ": ホバーだけで Drag 状態になりました。");

                Release(card, pointer);
            }
        }

        // ---------------- 2. 出ている機械獣は1体だけ ----------------

        /// <summary>
        /// 画面には一覧の8体が正常に出ています。「画面全体で1体」ではありません。
        ///
        /// 正式な契約:
        ///   - ホバー前後で画面上の論理Creature数は変わらない
        ///   - 一覧のCreatureは常にカード枚数と同じ
        ///   - ホバー対象カードの中のCreatureも1体だけ
        ///   - DragGhostLayer には0体
        ///   - カード外へ追加されたCreatureは0体
        ///   - RawImage総数がホバー前後で増えない
        /// </summary>
        [Test]
        public void HoveringShowsExactlyOneCreature()
        {
            Transform ghostLayer = Find("DragGhostLayer");

            // 1体 = BeastThumbnailView 1つ = RawImage 3枚。
            for (int i = 0; i < cards.Count; i++)
            {
                BeastThumbnailView[] inCard =
                    cards[i].GetComponentsInChildren<BeastThumbnailView>(true);

                Assert.That(
                    inCard.Length,
                    Is.EqualTo(1),
                    cards[i].name + " の中の Creature が1体ではありません。");

                Assert.That(
                    inCard[0].GetComponentsInChildren<RawImage>(true).Length,
                    Is.EqualTo(LayersPerCreature),
                    cards[i].name + " の Creature が3層ではありません。");
            }

            int rosterBefore = RosterCreatureCount();
            int totalBefore = VisibleCreatures().Count;
            int outsideBefore = CreaturesOutsideTheRoster().Count;
            int rawImagesBefore = CountRawImages();

            Assert.That(
                rosterBefore,
                Is.EqualTo(cards.Count),
                "静止時に一覧のCreatureが " + cards.Count + " 体ではありません。");

            Assert.That(
                CreatureCountUnder(ghostLayer),
                Is.EqualTo(0),
                "静止時に DragGhostLayer へ Creature が居ます。");

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = cards[i];

                PointerEventData pointer = Hover(card, WorldRect(card.transform).center);

                // (a) 一覧の体数は変わりません。
                Assert.That(
                    RosterCreatureCount(),
                    Is.EqualTo(rosterBefore),
                    card.name + " のホバーで一覧のCreature数が変わりました。");

                // (b) 画面全体の体数も変わりません。
                Assert.That(
                    VisibleCreatures().Count,
                    Is.EqualTo(totalBefore),
                    card.name + " のホバーで画面上のCreature数が変わりました（複製）。");

                // (c) ホバー対象カードの中も1体だけです。
                Assert.That(
                    card.GetComponentsInChildren<BeastThumbnailView>(true).Length,
                    Is.EqualTo(1),
                    card.name + " の中に Creature が増えました。");

                // (d) DragGhostLayer は空のままです。
                Assert.That(
                    CreatureCountUnder(ghostLayer),
                    Is.EqualTo(0),
                    card.name + " のホバーで DragGhostLayer に Creature が出ました。");

                // (e) カード外へ追加されたCreatureは0体です。
                Assert.That(
                    CreaturesOutsideTheRoster().Count,
                    Is.EqualTo(outsideBefore),
                    card.name + " のホバーでカード外に Creature が追加されました: " +
                    Describe(CreaturesOutsideTheRoster()));

                // (f) RawImage の総数も増えません。
                Assert.That(
                    CountRawImages(),
                    Is.EqualTo(rawImagesBefore),
                    card.name + " のホバーで RawImage の総数が増えました。");

                // (g) 動くのは HoverLiftRoot 配下の3層だけです。
                Assert.That(
                    Part(card, "HoverLiftRoot")
                        .GetComponentsInChildren<RawImage>(true).Length,
                    Is.EqualTo(LayersPerCreature),
                    card.name + ": HoverLiftRoot の層数が3ではありません。");

                Release(card, pointer);

                Assert.That(
                    VisibleCreatures().Count,
                    Is.EqualTo(totalBefore),
                    card.name + ": ホバー解除後にCreature数が戻っていません。");

                Assert.That(
                    CountRawImages(),
                    Is.EqualTo(rawImagesBefore),
                    card.name + ": ホバー解除後に RawImage 総数が戻っていません。");
            }
        }

        // ---------------- 3. 一覧の中に収まっていること ----------------

        [Test]
        public void HoveringKeepsTheOnlyCreatureInsideTheRoster()
        {
            Rect viewportRect = WorldRect(viewport);
            Rect squadRect = WorldRect(Find("SquadRow"));

            // 一覧と MY SQUAD のあいだの帯。ここへ機械獣が出てはいけません。
            Rect between = Rect.MinMaxRect(
                viewportRect.xMin, squadRect.yMax, viewportRect.xMax, viewportRect.yMin);

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = cards[i];

                PointerEventData pointer = Hover(card, WorldRect(card.transform).center);

                Transform liftRoot = Part(card, "HoverLiftRoot");
                Rect portrait = WorldRect(liftRoot);

                Assert.That(
                    portrait.yMax,
                    Is.LessThanOrEqualTo(viewportRect.yMax + 0.01f),
                    card.name + ": 機械獣が Viewport の上端を越えます。");

                Assert.That(
                    portrait.yMin,
                    Is.GreaterThanOrEqualTo(viewportRect.yMin - 0.01f),
                    card.name + ": 機械獣が Viewport の下端を越えます。");

                Assert.That(
                    portrait.xMin,
                    Is.GreaterThanOrEqualTo(viewportRect.xMin - 0.01f),
                    card.name + ": 機械獣が Viewport の左端を越えます。");

                Assert.That(
                    portrait.xMax,
                    Is.LessThanOrEqualTo(viewportRect.xMax + 0.01f),
                    card.name + ": 機械獣が Viewport の右端を越えます。");

                // 一覧と MY SQUAD のあいだに別の機械獣が出ていないこと。
                if (between.height > 0f)
                {
                    List<BeastThumbnailView> visible = VisibleCreatures();

                    for (int j = 0; j < visible.Count; j++)
                    {
                        Rect layerRect = WorldRect(visible[j].transform);

                        bool overlapsGap =
                            layerRect.xMin < between.xMax && between.xMin < layerRect.xMax
                            && layerRect.yMin < between.yMax && between.yMin < layerRect.yMax;

                        Assert.That(
                            overlapsGap,
                            Is.False,
                            card.name + " のホバー中、一覧と MY SQUAD のあいだに機械獣が出ています: " +
                            PathOf(visible[j].transform));
                    }
                }

                Release(card, pointer);
            }
        }

        // ---------------- 4. Ghost はしきい値を超えたときだけ1つ ----------------

        [Test]
        public void DraggingCreatesOnlyOneGhostAfterTheThreshold()
        {
            float threshold = (float)GetField(View(cards[0]), "dragStartDistance");

            Assert.That(threshold, Is.GreaterThan(0f), "しきい値が0です。");

            GameObject card = cards[5];
            Vector2 start = WorldRect(card.transform).center;

            PointerEventData pointer = Pointer(start);

            // (a) PointerDown だけでは出ません。
            View(card).OnPointerDown(pointer);

            Assert.That(
                ghost.VisibleGhostCount, Is.EqualTo(0), "PointerDown だけでGhostが出ました。");

            // (b) 長押しが成立しても、動かなければ出ません。
            now += 1f;
            View(card).OnDrag(pointer);

            Assert.That(
                ghost.VisibleGhostCount, Is.EqualTo(0), "長押しだけでGhostが出ました。");

            // (c) しきい値未満の移動でも出ません。
            pointer.position = start + new Vector2(threshold * 0.5f, 0f);
            View(card).OnDrag(pointer);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "しきい値未満の移動でGhostが出ました。");

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.Pending),
                "しきい値未満でドラッグが始まりました。");

            // (d) しきい値を超えて初めて、1つだけ出ます。
            pointer.position = start + new Vector2(threshold * 2f, 0f);
            View(card).OnDrag(pointer);

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.SquadDragging),
                "しきい値を超えてもドラッグになりません（検査が空振りします）。");

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(1),
                "正式なドラッグでGhostがちょうど1つになりません。");

            // 動かしても増えません。
            pointer.position = start + new Vector2(threshold * 3f, -threshold);
            View(card).OnDrag(pointer);

            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(1), "移動でGhostが増えました。");

            // (e) 離したら必ず消えます。
            View(card).OnEndDrag(pointer);
            View(card).OnPointerUp(pointer);

            Assert.That(
                ghost.VisibleGhostCount, Is.EqualTo(0), "EndDrag / PointerUp で消えません。");

            Assert.That(ghost.IsShowing, Is.False);

            // (f) 次のホバーへ残りません。
            PointerEventData next = Hover(cards[1], WorldRect(cards[1].transform).center);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "次のホバーにGhostが残っています。");

            Release(cards[1], next);
        }

        /// <summary>
        /// ドラッグ中にカードが破棄されると OnEndDrag も OnPointerUp も届きません。
        /// この経路で Ghost が残ると、一覧の下に機械獣がもう1体出たままになります。
        /// </summary>
        [Test]
        public void ACardDestroyedMidDragDoesNotLeaveAGhost()
        {
            float threshold = (float)GetField(View(cards[0]), "dragStartDistance");

            GameObject card = cards[5];
            Vector2 start = WorldRect(card.transform).center;

            PointerEventData pointer = Pointer(start);

            View(card).OnPointerDown(pointer);
            now += 1f;
            pointer.position = start + new Vector2(threshold * 2f, 0f);
            View(card).OnDrag(pointer);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(1),
                "ドラッグが始まっていません（検査が空振りします）。");

            // 一覧の作り直しなどでカードが消える状況です。
            //
            // EditMode では Unity が OnDisable / OnDestroy を配送しません
            // （[ExecuteAlways] を付けたコンポーネントを除く）。
            // 実行時と同じ順序を再現するため、破棄の直前に OnDisable を直接呼びます。
            // 呼ぶのはテスト側だけで、製品コードにテスト用の分岐は入れていません。
            BeastCardView view = View(card);

            EditModeLifecycle.Disable(view);

            // OnDisable が返った時点で、もう表示されていないこと（同一フレーム）。
            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "カードの OnDisable 直後にGhostがまだ表示されています。");

            Assert.That(
                ghost.IsShowing,
                Is.False,
                "カードの OnDisable 直後に Presenter が表示中のままです。");

            Assert.That(
                view.CurrentState,
                Is.EqualTo(CardGestureState.Idle),
                "OnDisable でジェスチャーが Idle へ戻っていません。");

            cards[5] = null;
            Object.DestroyImmediate(card);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "カードが消えたあともGhostが残っています。");

            Assert.That(ghost.IsShowing, Is.False);

            Assert.That(
                CountGhostObjects(),
                Is.EqualTo(0),
                "DragGhost の実体が残っています。");
        }

        // ---------------- 5. 繰り返しても増えない ----------------

        [Test]
        public void RepeatedHoverAndDragNeverAccumulatesPortraits()
        {
            int baselineCreatures = VisibleCreatures().Count;
            int baselineRoster = RosterCreatureCount();
            int baselineRawImages = CountRawImages();
            int baselineGhosts = ghost.VisibleGhostCount;

            Assert.That(baselineGhosts, Is.EqualTo(0), "開始時点でGhostが出ています。");

            float threshold = (float)GetField(View(cards[0]), "dragStartDistance");

            // ホバー → 解除 を5回。
            for (int round = 0; round < 5; round++)
            {
                GameObject card = cards[round % cards.Count];

                PointerEventData pointer = Hover(card, WorldRect(card.transform).center);

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(0),
                    round + "回目のホバーでGhostが出ました。");

                Release(card, pointer);
            }

            // ドラッグ → キャンセル を5回。
            for (int round = 0; round < 5; round++)
            {
                GameObject card = cards[round % cards.Count];
                Vector2 start = WorldRect(card.transform).center;

                PointerEventData pointer = Pointer(start);

                View(card).OnPointerDown(pointer);
                now += 1f;

                pointer.position = start + new Vector2(threshold * 2f, 0f);
                View(card).OnDrag(pointer);

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(1),
                    round + "回目のドラッグでGhostが1つになりません。");

                // どこへも落とさずに離す＝キャンセルです。
                View(card).OnEndDrag(pointer);
                View(card).OnPointerUp(pointer);

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(0),
                    round + "回目のキャンセル後にGhostが残りました。");
            }

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(baselineGhosts),
                "Ghostの数が初期値へ戻りません。");

            Assert.That(
                CountRawImages(),
                Is.EqualTo(baselineRawImages),
                "RawImageの総数が初期値へ戻りません（表示が蓄積しています）。");

            Assert.That(
                VisibleCreatures().Count,
                Is.EqualTo(baselineCreatures),
                "画面上のCreature数が初期値へ戻りません（複製が残っています）。");

            Assert.That(
                RosterCreatureCount(),
                Is.EqualTo(baselineRoster),
                "一覧のCreature数が初期値へ戻りません。");
        }

        private int CountRawImages()
        {
            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<RawImage>(true).Length;
            }

            return count;
        }

        // ---------------- 正式な操作契約 ----------------

        /// <summary>
        /// 長押しだけ（ポインタ未移動）では、カード内の機械獣が浮くだけです。
        /// DragGhost は出しません。
        ///
        /// 呼び出し経路の事実:
        ///   Apply(HoldSatisfied) → SetLift(DragReady) のみ。listener は呼びません。
        ///   Apply(BeginSquadDrag) → listener.OnCardDragBegin → dragGhost.Show
        /// つまり Show は移動しきい値を超えて Dragging へ遷移した時点でしか呼ばれません。
        /// </summary>
        [Test]
        public void LongPressWithoutMovementLiftsThePortraitButShowsNoGhost()
        {
            Transform ghostLayer = Find("DragGhostLayer");

            // 一覧の外にも MY SQUAD の枠があります。ここは「増えないこと」を見ます。
            int outsideBefore = CreaturesOutsideTheRoster().Count;
            int rosterBefore = RosterCreatureCount();
            int rawImagesBefore = CountRawImages();

            Assert.That(
                rosterBefore,
                Is.EqualTo(cards.Count),
                "静止時に一覧のCreatureが " + cards.Count + " 体ではありません。");

            Assert.That(
                CreatureCountUnder(ghostLayer),
                Is.EqualTo(0),
                "静止時に DragGhostLayer へ Creature が居ます。");

            float hold = (float)GetField(View(cards[0]), "holdToDragSeconds");

            Assert.That(hold, Is.GreaterThan(0f), "長押し時間が0です。");

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = cards[i];

                Transform liftRoot = Part(card, "HoverLiftRoot");

                float restY = liftRoot.localPosition.y;
                Vector3 restScale = liftRoot.localScale;
                Rect restPortrait = WorldRect(liftRoot);

                Vector2 at = WorldRect(card.transform).center;

                PointerEventData pointer = Pointer(at);

                View(card).OnPointerDown(pointer);

                // 長押し時間を超えるまで Update を進めます。座標は一切動かしません。
                now += hold + 0.05f;
                PumpCard(View(card));

                Assert.That(
                    pointer.position,
                    Is.EqualTo(at),
                    "検査の途中でポインタ座標が動いています。");

                // (a) 状態は「押下継続中かつ長押し成立」＝ DragReady 表示です。
                Assert.That(
                    View(card).CurrentState,
                    Is.EqualTo(CardGestureState.Pending),
                    card.name + ": 動かしていないのにジェスチャーが確定しました。");

                Assert.That(
                    View(card).CurrentState,
                    Is.Not.EqualTo(CardGestureState.SquadDragging),
                    card.name + ": 移動なしで Dragging へ遷移しました。");

                CardLiftView lift = card.GetComponent<CardLiftView>();

                Assert.That(
                    lift.State,
                    Is.EqualTo(CardLiftView.LiftState.DragReady),
                    card.name + ": 長押しで DragReady になりません。");

                Assert.That(
                    View(card).IsDragReadyVisual,
                    Is.True,
                    card.name + ": IsDragReadyVisual が立ちません。");

                // (b) HoverLiftRoot が実際に上昇・拡大していること。
                lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

                Assert.That(
                    liftRoot.localPosition.y,
                    Is.GreaterThan(restY),
                    card.name + ": 機械獣が上昇していません。");

                Assert.That(
                    liftRoot.localScale.x,
                    Is.GreaterThan(restScale.x),
                    card.name + ": 機械獣が拡大していません。");

                Assert.That(
                    WorldRect(liftRoot).yMax,
                    Is.GreaterThan(restPortrait.yMax),
                    card.name + ": 機械獣の上端が上がっていません。");

                // (c) DragGhost は出ていないこと。
                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(0),
                    card.name + ": 長押しだけで DragGhost が出ました。");

                Assert.That(
                    ghost.IsShowing,
                    Is.False,
                    card.name + ": 長押しだけで DragGhost が生成されました。");

                // (d) カード外へ機械獣が1体も追加されていないこと。
                List<BeastThumbnailView> outside = CreaturesOutsideTheRoster();

                Assert.That(
                    outside.Count,
                    Is.EqualTo(outsideBefore),
                    card.name + ": 長押しでカード外に機械獣が追加されました: " +
                    Describe(outside));

                Assert.That(
                    CreatureCountUnder(ghostLayer),
                    Is.EqualTo(0),
                    card.name + ": 長押しで DragGhostLayer に機械獣が出ました。");

                Assert.That(
                    RosterCreatureCount(),
                    Is.EqualTo(rosterBefore),
                    card.name + ": 長押しで一覧のCreature数が変わりました。");

                Assert.That(
                    CountRawImages(),
                    Is.EqualTo(rawImagesBefore),
                    card.name + ": 長押しで RawImage の総数が増えました。");

                Release(card, pointer);
            }
        }

        /// <summary>
        /// 移動しきい値を超えた時点で初めて、DragGhost がちょうど1体出ます。
        /// </summary>
        [Test]
        public void CrossingTheMovementThresholdCreatesExactlyOneGhost()
        {
            GameObject card = cards[6];

            float threshold = (float)GetField(View(card), "dragStartDistance");

            Vector2 start = WorldRect(card.transform).center;

            PointerEventData pointer = Hover(card, start);

            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(0), "DragReady でGhostが出ました。");

            // (a) しきい値未満の移動では出ません。
            pointer.position = start + new Vector2(threshold * 0.25f, 0f);
            View(card).OnDrag(pointer);

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.Pending),
                "しきい値未満でドラッグが始まりました。");

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "しきい値未満の移動でGhostが出ました。");

            pointer.position = start + new Vector2(0f, -threshold * 0.9f);
            View(card).OnDrag(pointer);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                "しきい値直前の移動でGhostが出ました。");

            // (b) 超えた瞬間に Dragging へ遷移し、Ghost がちょうど1体。
            pointer.position = start + new Vector2(0f, -threshold * 1.5f);
            View(card).OnDrag(pointer);

            Assert.That(
                View(card).CurrentState,
                Is.EqualTo(CardGestureState.SquadDragging),
                "しきい値を超えてもドラッグになりません（検査が空振りします）。");

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(1),
                "しきい値を超えてもGhostがちょうど1体になりません。");

            // (c) さらに動かしても1体のままです。
            for (int step = 1; step <= 4; step++)
            {
                pointer.position =
                    start + new Vector2(threshold * step, -threshold * (1.5f + step));

                View(card).OnDrag(pointer);

                Assert.That(
                    ghost.VisibleGhostCount,
                    Is.EqualTo(1),
                    "移動 " + step + " 回目でGhostが増減しました。");
            }

            // (d) 元カード以外からGhostは生まれていません。
            Assert.That(
                CountGhostObjects(),
                Is.EqualTo(1),
                "DragGhost の実体が1つではありません。");

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == card)
                {
                    continue;
                }

                Assert.That(
                    View(cards[i]).CurrentState,
                    Is.EqualTo(CardGestureState.Idle),
                    cards[i].name + ": 触っていないカードがドラッグ状態です。");
            }

            View(card).OnEndDrag(pointer);
            View(card).OnPointerUp(pointer);

            Assert.That(ghost.VisibleGhostCount, Is.EqualTo(0));
        }

        /// <summary>
        /// 終了・中断のどの経路でも、その場で表示が消えます。
        /// Destroy は次のフレームまで遅れるため、先に非表示にしてから破棄しています。
        /// ここでは「同じ呼び出しの直後に、もう画面に出ていない」ことを確かめます。
        /// </summary>
        [TestCase("EndDragThenPointerUp")]
        [TestCase("PointerUpOnly")]
        [TestCase("EndDragOnly")]
        [TestCase("OnDisable")]
        public void EndingOrCancellingDragHidesTheGhostImmediately(string ending)
        {
            GameObject card = cards[3];

            float threshold = (float)GetField(View(card), "dragStartDistance");

            Vector2 start = WorldRect(card.transform).center;

            PointerEventData pointer = Pointer(start);

            View(card).OnPointerDown(pointer);
            now += 1f;

            pointer.position = start + new Vector2(threshold * 2f, 0f);
            View(card).OnDrag(pointer);

            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(1),
                ending + ": ドラッグが始まっていません（検査が空振りします）。");

            GameObject ghostObject = FirstGhostObject();

            Assert.That(ghostObject, Is.Not.Null, "DragGhost の実体が取れません。");
            Assert.That(ghostObject.activeInHierarchy, Is.True);

            switch (ending)
            {
                case "EndDragThenPointerUp":
                    View(card).OnEndDrag(pointer);
                    View(card).OnPointerUp(pointer);
                    break;

                case "PointerUpOnly":
                    View(card).OnPointerUp(pointer);
                    break;

                case "EndDragOnly":
                    View(card).OnEndDrag(pointer);
                    break;

                case "OnDisable":
                    EditModeLifecycle.Disable(View(card));
                    break;

                default:
                    Assert.Fail("未知の終了経路: " + ending);
                    break;
            }

            // 同じ呼び出しの直後です。フレームは進めていません。
            Assert.That(
                ghost.VisibleGhostCount,
                Is.EqualTo(0),
                ending + ": 終了直後にまだ表示されています。");

            Assert.That(
                ghost.IsShowing,
                Is.False,
                ending + ": Presenter が表示中のままです。");

            // Destroy 待ちで残っていないこと（破棄済みか、すでに非表示）。
            Assert.That(
                ghostObject == null || !ghostObject.activeInHierarchy,
                Is.True,
                ending + ": Destroy 待ちのGhostが表示されたままです。");

            Assert.That(
                CountGhostObjects(),
                Is.EqualTo(0),
                ending + ": DragGhost の実体が残っています。");
        }

        // ---------------- 集計の道具 ----------------

        private static string Describe(List<BeastThumbnailView> creatures)
        {
            if (creatures.Count == 0)
            {
                return "(なし)";
            }

            string text = string.Empty;

            for (int i = 0; i < creatures.Count; i++)
            {
                text += "\n      " + PathOf(creatures[i].transform)
                    + " (InstanceID " + creatures[i].GetInstanceID() + ")";
            }

            return text;
        }

        private int CountGhostObjects()
        {
            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<DragGhostView>(true).Length;
            }

            return count;
        }

        private GameObject FirstGhostObject()
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DragGhostView view = root.GetComponentInChildren<DragGhostView>(true);

                if (view != null)
                {
                    return view.gameObject;
                }
            }

            return null;
        }

    }
}
