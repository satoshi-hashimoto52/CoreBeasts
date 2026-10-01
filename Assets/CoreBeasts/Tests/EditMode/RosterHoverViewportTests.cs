using System.Collections.Generic;

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// ホバーで持ち上がった機械獣が、CORE BEASTS一覧の表示範囲から切れないことを固定します。
    ///
    /// 背景:
    /// 一覧は RosterScroll/Viewport の RectMask2D で切り抜いています。
    /// 最上段のカードをホバーすると、機械獣は上へ移動・拡大してカード上端を越えるため、
    /// Content 上部に余白が無いと Viewport の外へ出て頭や耳が消えます。
    /// 実画面では「背面へ潜った」ように見えます。
    ///
    /// 解決は GridLayoutGroup.padding.top で上余白を確保することだけです。
    /// RectMask2D を外す、Canvas の overrideSorting で切り抜きを無効化する、
    /// ホバー移動量を0にする、といった方法は取りません。
    ///
    /// 実シーンを開いて実物の寸法で確かめます。
    /// </summary>
    public sealed class RosterHoverViewportTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";

        /// <summary>一覧に並べる枚数。4列なので2行になります。</summary>
        private const int CardCount = 8;
        private const int Columns = 4;

        /// <summary>座標比較の許容。</summary>
        private const float Epsilon = 0.01f;

        private Scene scene;
        private readonly List<GameObject> spawned = new List<GameObject>();

        private RectTransform safeArea;
        private RectTransform viewport;
        private RectTransform content;
        private GridLayoutGroup grid;

        [SetUp]
        public void SetUp()
        {
            // Additive だと直前のシーンと同居して Global Light 2D が2つになり、
            // URP 2D が Error を出します。NUnit は想定外の Error を失敗にするため単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");

            safeArea = (RectTransform)Find("SafeArea");
            viewport = (RectTransform)Find("Viewport");
            content = (RectTransform)Find("Content");

            grid = content.GetComponent<GridLayoutGroup>();

            Assert.That(grid, Is.Not.Null, "Content に GridLayoutGroup がありません。");
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

            safeArea = null;
            viewport = null;
            content = null;
            grid = null;

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

        private static Transform Part(GameObject card, string name)
        {
            Transform found = FindDeep(card.transform, name);

            Assert.That(found, Is.Not.Null, card.name + " に " + name + " がありません。");

            return found;
        }

        private static CardLiftView Lift(GameObject card)
        {
            CardLiftView lift = card.GetComponent<CardLiftView>();

            Assert.That(lift, Is.Not.Null, card.name + " に CardLiftView がありません。");

            return lift;
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
        /// CanvasScaler(Scale With Screen Size) で、実ピクセル1つが何Canvas単位になるか。
        /// 参照解像度と match をアセットから読むため、設定を変えても意味が変わりません。
        /// </summary>
        private float PixelsPerUnit(float screenWidth, float screenHeight)
        {
            CanvasScaler scaler = safeArea.GetComponentInParent<CanvasScaler>();

            Assert.That(scaler, Is.Not.Null, "CanvasScaler が見つかりません。");

            Assert.That(
                scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize),
                "Scale With Screen Size を前提にしています。");

            float logWidth = Mathf.Log(screenWidth / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(screenHeight / scaler.referenceResolution.y, 2f);

            return Mathf.Pow(
                2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        }

        /// <summary>
        /// 端末の Safe Area を Canvas 単位へ直して SafeArea の矩形に入れます。
        ///
        /// SafeAreaController は Screen.safeArea を見ますが、
        /// EditMode では Game View の値になり、端末を選べません。
        /// そこで SafeAreaController と同じ結果になる矩形を直接置きます。
        /// </summary>
        private void ApplyDevice(
            float screenWidth, float screenHeight,
            float topInsetPoints, float bottomInsetPoints, float deviceScale)
        {
            float pixelsPerUnit = PixelsPerUnit(screenWidth, screenHeight);

            float insetPixels = (topInsetPoints + bottomInsetPoints) * deviceScale;

            float width = screenWidth / pixelsPerUnit;
            float height = (screenHeight - insetPixels) / pixelsPerUnit;

            safeArea.anchorMin = new Vector2(0.5f, 0.5f);
            safeArea.anchorMax = new Vector2(0.5f, 0.5f);
            safeArea.pivot = new Vector2(0.5f, 0.5f);
            safeArea.anchoredPosition = Vector2.zero;
            safeArea.sizeDelta = new Vector2(width, height);
        }

        /// <summary>一覧へカードを並べ、レイアウトを確定させます。</summary>
        private List<GameObject> FillRoster()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            List<GameObject> cards = new List<GameObject>();

            for (int i = 0; i < CardCount; i++)
            {
                GameObject card = Object.Instantiate(asset, content);
                card.name = "BeastCard" + i;

                spawned.Add(card);
                cards.Add(card);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            return cards;
        }

        /// <summary>ホバーし、機械獣が実際に上昇・拡大したことを先に確かめます。</summary>
        private Transform HoverAndVerifyItMoved(GameObject card)
        {
            Transform liftRoot = Part(card, "HoverLiftRoot");

            float restY = liftRoot.localPosition.y;
            Vector3 restScale = liftRoot.localScale;
            Rect before = WorldRect(liftRoot);

            Lift(card).SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                liftRoot.localPosition.y,
                Is.GreaterThan(restY),
                card.name + ": ホバーで機械獣が上昇していません（検査が空振りします）。");

            Assert.That(
                liftRoot.localScale.x,
                Is.GreaterThan(restScale.x),
                card.name + ": ホバーで機械獣が拡大していません（検査が空振りします）。");

            Rect after = WorldRect(liftRoot);

            Assert.That(
                after.yMax,
                Is.GreaterThan(before.yMax + Epsilon),
                card.name + ": 機械獣の上端が上がっていません。");

            Assert.That(
                after.height,
                Is.GreaterThan(before.height + Epsilon),
                card.name + ": 機械獣が縦に広がっていません。");

            return liftRoot;
        }

        private static Rect Intersection(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMax <= xMin || yMax <= yMin)
            {
                return Rect.zero;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // ---------------- 最上段が切れないこと ----------------

        // iPhone 16 Pro（本プロジェクトの基準）と iPhone 12 mini（いちばん細い）。
        [TestCase(1206f, 2622f, 62f, 34f, 3f, TestName =
            "TheTopRowPortraitStaysInsideTheRosterViewportWhenHovered(iPhone 16 Pro)")]
        [TestCase(1080f, 2340f, 50f, 34f, 3f, TestName =
            "TheTopRowPortraitStaysInsideTheRosterViewportWhenHovered(iPhone 12 mini)")]
        public void TheTopRowPortraitStaysInsideTheRosterViewportWhenHovered(
            float screenWidth, float screenHeight,
            float topInsetPoints, float bottomInsetPoints, float deviceScale)
        {
            ApplyDevice(
                screenWidth, screenHeight, topInsetPoints, bottomInsetPoints, deviceScale);

            List<GameObject> cards = FillRoster();

            Rect viewportRect = WorldRect(viewport);

            Assert.That(
                viewportRect.height,
                Is.GreaterThan(0f),
                "Viewport の高さが0です。端末指定が効いていません。");

            // 1行目の4枚すべてを個別に確かめます。
            for (int i = 0; i < Columns; i++)
            {
                GameObject card = cards[i];

                Rect cardRect = WorldRect(card.transform);

                Assert.That(
                    cardRect.yMax,
                    Is.LessThanOrEqualTo(viewportRect.yMax + Epsilon),
                    card.name + ": カード自体が Viewport の上へはみ出しています。");

                Transform liftRoot = HoverAndVerifyItMoved(card);

                Rect portrait = WorldRect(liftRoot);

                Assert.That(
                    portrait.yMax,
                    Is.LessThanOrEqualTo(viewportRect.yMax + Epsilon),
                    card.name + ": 機械獣の頭が Viewport の上端で切られます。" +
                    "はみ出し " + (portrait.yMax - viewportRect.yMax).ToString("F1") +
                    " 単位。GridLayoutGroup.padding.top の上余白が足りません。");

                Assert.That(
                    portrait.yMin,
                    Is.GreaterThanOrEqualTo(viewportRect.yMin - Epsilon),
                    card.name + ": 機械獣の足元が Viewport の下端で切られます。");

                Assert.That(
                    portrait.xMin,
                    Is.GreaterThanOrEqualTo(viewportRect.xMin - Epsilon),
                    card.name + ": 機械獣が Viewport の左端で切られます。");

                Assert.That(
                    portrait.xMax,
                    Is.LessThanOrEqualTo(viewportRect.xMax + Epsilon),
                    card.name + ": 機械獣が Viewport の右端で切られます。");

                // カード背景そのものは一覧の外へ出しません。
                Assert.That(
                    WorldRect(Part(card, "Background")).yMax,
                    Is.LessThanOrEqualTo(viewportRect.yMax + Epsilon),
                    card.name + ": カード背景が一覧の外へはみ出しています。");

                Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);
            }
        }

        /// <summary>
        /// 上余白は「ホバー時の最大突出量」から決めます。
        /// liftDistance や liftScale を変えたら、この検査が先に落ちます。
        /// </summary>
        [Test]
        public void TheGridKeepsEnoughHeadroomForTheHoverLift()
        {
            ApplyDevice(1206f, 2622f, 62f, 34f, 3f);

            List<GameObject> cards = FillRoster();

            GameObject card = cards[0];

            Rect cardRect = WorldRect(card.transform);

            Transform liftRoot = HoverAndVerifyItMoved(card);

            float overshoot = WorldRect(liftRoot).yMax - cardRect.yMax;

            Assert.That(
                overshoot,
                Is.GreaterThan(0f),
                "機械獣がカード上端を越えていません（検査が空振りします）。");

            Assert.That(
                grid.padding.top,
                Is.GreaterThanOrEqualTo(overshoot),
                "上余白 " + grid.padding.top + " が、ホバー時の突出量 " +
                overshoot.ToString("F1") + " を下回っています。");

            Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);
        }

        // ---------------- 8枚すべてで機械獣が見えていること ----------------

        [Test]
        public void EveryRosterCardKeepsItsPortraitVisibleWhileHovered()
        {
            ApplyDevice(1080f, 2340f, 50f, 34f, 3f);

            List<GameObject> cards = FillRoster();

            Rect viewportRect = WorldRect(viewport);

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = cards[i];

                Transform liftRoot = Part(card, "HoverLiftRoot");

                Vector3 restPosition = liftRoot.localPosition;
                Vector3 restScale = liftRoot.localScale;
                Rect restPortrait = WorldRect(liftRoot);

                int backgroundOrder = DrawIndex(Part(card, "Background"));
                int surfaceOrder = DrawIndex(Part(card, "AttributeSurface"));

                HoverAndVerifyItMoved(card);

                // (a) 色面・背景より前面であること。
                Assert.That(
                    DrawIndex(liftRoot),
                    Is.GreaterThan(backgroundOrder),
                    card.name + ": 機械獣が Background より背面です。");

                Assert.That(
                    DrawIndex(liftRoot),
                    Is.GreaterThan(surfaceOrder),
                    card.name + ": 機械獣が AttributeSurface より背面です。");

                // (b) 見えている面積が0にならないこと。
                Rect visible = Intersection(WorldRect(liftRoot), viewportRect);

                Assert.That(
                    visible.width * visible.height,
                    Is.GreaterThan(0f),
                    card.name + ": 機械獣の表示面積が0です（完全に切り抜かれています）。");

                Assert.That(
                    visible.height,
                    Is.EqualTo(WorldRect(liftRoot).height).Within(Epsilon),
                    card.name + ": 機械獣が縦に切り取られています。");

                Assert.That(
                    visible.width,
                    Is.EqualTo(WorldRect(liftRoot).width).Within(Epsilon),
                    card.name + ": 機械獣が横に切り取られています。");

                // (c) 表示そのものが消えていないこと。
                foreach (RawImage layer in liftRoot.GetComponentsInChildren<RawImage>(true))
                {
                    Assert.That(
                        layer.gameObject.activeInHierarchy && layer.enabled,
                        Is.True,
                        card.name + " の " + layer.name + " が無効です。");
                }

                CanvasGroup group = Part(card, "Thumb").GetComponent<CanvasGroup>();

                if (group != null)
                {
                    Assert.That(
                        group.alpha,
                        Is.GreaterThan(0f),
                        card.name + ": ホバー中に機械獣が透明になっています。");
                }

                // (d) ホバーを抜けたら完全に元へ戻ること。
                Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);

                Assert.That(
                    liftRoot.localPosition,
                    Is.EqualTo(restPosition),
                    card.name + ": ホバー解除後に位置が戻っていません。");

                Assert.That(
                    liftRoot.localScale,
                    Is.EqualTo(restScale),
                    card.name + ": ホバー解除後に拡大が戻っていません。");

                Assert.That(
                    WorldRect(liftRoot),
                    Is.EqualTo(restPortrait),
                    card.name + ": ホバー解除後に矩形が戻っていません。");

                Assert.That(
                    card.GetComponentsInChildren<Canvas>(true),
                    Is.Empty,
                    card.name + ": ホバーで Canvas が足されています（カード全体の前面化）。");
            }
        }

        /// <summary>
        /// 深さ優先の通し番号。大きいほど前面に描かれます。
        /// 1つのCanvasの中では、描画順は階層の深さ優先順で決まります。
        /// </summary>
        private int DrawIndex(Transform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();

            Assert.That(canvas, Is.Not.Null, target.name + " が Canvas の下にありません。");

            int counter = 0;
            int found = -1;

            Visit(canvas.transform, target, ref counter, ref found);

            Assert.That(found, Is.GreaterThanOrEqualTo(0), target.name + " を辿れません。");

            return found;
        }

        private static void Visit(
            Transform node, Transform target, ref int counter, ref int found)
        {
            if (node == target)
            {
                found = counter;
            }

            counter++;

            for (int i = 0; i < node.childCount; i++)
            {
                Visit(node.GetChild(i), target, ref counter, ref found);
            }
        }
    }
}
