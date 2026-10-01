using System.Collections.Generic;

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// ホバー時の描画順を固定します。
    ///
    /// 背面から前面へ、必ずこの順です。
    ///   1. カード背景 (Background)
    ///   2. 属性の色面 (AttributeSurface)
    ///   3. 影・発光 (DragShadow / DragGlow)
    ///   4. 機械獣 (HoverLiftRoot 配下の Base / Primary / Secondary)
    ///   5. 枠・選択枠・ラベル・バッジ (Frame / NameLabel / LevelLabel / SquadBadge)
    ///
    /// 1つのCanvasの中では、描画順は「階層の深さ優先順」で決まります。
    /// そのためここでは Canvas ルートから深さ優先で数えた通し番号を
    /// 描画順とみなして比べます。番号が大きいほど前面です。
    ///
    /// 解決方法の制約:
    /// - ホバーしたカード全体を最前面へ移動して解決しません。
    /// - AttributeSurface を透明化・削除して隠しません。
    /// - 切り抜き(RectMask2D)を外して誤魔化しません。
    /// - 実行時に毎フレーム SetAsLastSibling する実装にしません。
    /// 満たす手段は prefab の恒久的な兄弟順だけです。
    /// </summary>
    public sealed class CardDrawOrderTests
    {
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";

        /// <summary>実画面(UnitSet)の Content と同じ並べ方。</summary>
        private const int Columns = 4;
        private const int CardCount = 8;
        private static readonly Vector2 CellSize = new Vector2(210f, 255f);
        private static readonly Vector2 CellSpacing = new Vector2(12f, 12f);

        /// <summary>機械獣より背面でなければならない部品。</summary>
        private static readonly string[] BehindThePortrait =
        {
            "Background",
            "AttributeSurface",
            "DragShadow",
            "DragGlow",
        };

        /// <summary>機械獣より前面でなければならない部品。</summary>
        private static readonly string[] InFrontOfThePortrait =
        {
            "Frame",
            "NameLabel",
            "LevelLabel",
            "SquadBadge",
        };

        /// <summary>機械獣そのものを構成する層。</summary>
        private static readonly string[] PortraitLayers =
        {
            "Base",
            "Primary",
            "Secondary",
        };

        private GameObject canvasRoot;
        private RectTransform content;
        private readonly List<GameObject> cards = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            canvasRoot = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));

            RectTransform canvasRect = (RectTransform)canvasRoot.transform;
            canvasRect.sizeDelta = new Vector2(1080f, 1920f);

            GameObject contentObject = new GameObject(
                "Content", typeof(RectTransform), typeof(GridLayoutGroup));

            content = (RectTransform)contentObject.transform;
            content.SetParent(canvasRoot.transform, false);
            content.sizeDelta = new Vector2(900f, 1200f);

            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = CellSize;
            grid.spacing = CellSpacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            for (int i = 0; i < CardCount; i++)
            {
                GameObject card = Object.Instantiate(asset, content);
                card.name = "BeastCard" + i;
                cards.Add(card);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        [TearDown]
        public void TearDown()
        {
            cards.Clear();

            if (canvasRoot != null)
            {
                Object.DestroyImmediate(canvasRoot);
                canvasRoot = null;
            }

            content = null;
        }

        // ---------------- 道具 ----------------

        /// <summary>深さ優先の通し番号。大きいほど前面に描かれます。</summary>
        private int DrawIndex(Transform target)
        {
            int counter = 0;
            int found = -1;

            Visit(canvasRoot.transform, target, ref counter, ref found);

            Assert.That(
                found,
                Is.GreaterThanOrEqualTo(0),
                target.name + " が Canvas の下にありません。");

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

        /// <summary>Canvas配下すべての描画順を記録します。</summary>
        private Dictionary<Transform, int> Snapshot()
        {
            Dictionary<Transform, int> map = new Dictionary<Transform, int>();
            int counter = 0;

            Collect(canvasRoot.transform, map, ref counter);

            return map;
        }

        private static void Collect(
            Transform node, Dictionary<Transform, int> map, ref int counter)
        {
            map[node] = counter;
            counter++;

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), map, ref counter);
            }
        }

        private static Transform Part(GameObject card, string name)
        {
            Transform found = FindDeep(card.transform, name);

            Assert.That(found, Is.Not.Null, card.name + " に " + name + " がありません。");

            return found;
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

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax && b.xMin < a.xMax
                && a.yMin < b.yMax && b.yMin < a.yMax;
        }

        /// <summary>行が2つ以上できる位置のカード。1つ上の行と重なります。</summary>
        private GameObject SecondRowCard => cards[Columns];

        /// <summary>機械獣が実際に持ち上がったことを確かめてから調べます。</summary>
        private Transform HoverAndGetLiftRoot(GameObject card)
        {
            Transform liftRoot = Part(card, "HoverLiftRoot");

            float before = liftRoot.localPosition.y;
            Rect cardRectBefore = WorldRect(card.transform);
            Rect portraitBefore = WorldRect(liftRoot);

            Lift(card).SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                liftRoot.localPosition.y,
                Is.GreaterThan(before),
                "ホバーで機械獣が持ち上がっていません（検査が空振りします）。");

            Rect portraitAfter = WorldRect(liftRoot);

            Assert.That(
                portraitAfter.yMax,
                Is.GreaterThan(portraitBefore.yMax),
                "機械獣が上へ出ていません（検査が空振りします）。");

            Assert.That(
                portraitAfter.yMax,
                Is.GreaterThan(cardRectBefore.yMax),
                "機械獣がカードの上端を越えていません。" +
                "隣のカードとの前後関係を試せません。");

            return liftRoot;
        }

        // ---------------- 1. 機械獣はあらゆるカード背景より前面 ----------------

        /// <summary>
        /// 上段・下段・左右端を含む8枚すべてで確かめます。
        /// 1枚だけでは、行や列の位置で変わる前後関係を見逃します。
        /// </summary>
        [Test]
        public void ThePortraitRendersAboveEveryCardBackground()
        {
            int neighboursChecked = 0;

            for (int index = 0; index < cards.Count; index++)
            {
                GameObject card = cards[index];
                string where = Position(index);

                Transform liftRoot = HoverAndGetLiftRoot(card);

                // (a) 自分のカードの色面・背景・影・発光より前面であること。
                for (int layer = 0; layer < PortraitLayers.Length; layer++)
                {
                    int portrait = DrawIndex(Part(card, PortraitLayers[layer]));

                    for (int i = 0; i < BehindThePortrait.Length; i++)
                    {
                        Assert.That(
                            portrait,
                            Is.GreaterThan(DrawIndex(Part(card, BehindThePortrait[i]))),
                            where + ": " + PortraitLayers[layer] + " が " +
                            BehindThePortrait[i] + " より背面です（機械獣が色面へ潜ります）。");
                    }

                    // カードのルートにある透明な入力面より前面であること。
                    Assert.That(
                        portrait,
                        Is.GreaterThan(DrawIndex(card.transform)),
                        where + ": " + PortraitLayers[layer] +
                        " がカードのルートより背面です。");
                }

                // (b) 実際に重なっている「別のカード」の背景より前面であること。
                Rect portraitRect = WorldRect(liftRoot);
                int portraitIndex = DrawIndex(liftRoot);

                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] == card)
                    {
                        continue;
                    }

                    if (!Overlaps(portraitRect, WorldRect(cards[i].transform)))
                    {
                        continue;
                    }

                    neighboursChecked++;

                    for (int j = 0; j < BehindThePortrait.Length; j++)
                    {
                        Assert.That(
                            portraitIndex,
                            Is.GreaterThan(DrawIndex(Part(cards[i], BehindThePortrait[j]))),
                            where + ": 持ち上がった機械獣が " + cards[i].name + " の " +
                            BehindThePortrait[j] + " へ潜っています。");
                    }
                }

                Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);
            }

            // 2行目の4枚は必ず上の行と重なります。0件なら検査が空振りしています。
            Assert.That(
                neighboursChecked,
                Is.GreaterThanOrEqualTo(Columns),
                "隣接カードとの重なりが足りません（隣接カードの検査が空振りします）。");
        }

        /// <summary>失敗メッセージ用に、盤面のどこかを示します。</summary>
        private static string Position(int index)
        {
            int row = index / Columns;
            int column = index % Columns;

            string rowName = row == 0 ? "1行目" : (row + 1) + "行目";

            string columnName =
                column == 0 ? "左端"
                : column == Columns - 1 ? "右端"
                : (column + 1) + "列目";

            return "BeastCard" + index + "(" + rowName + columnName + ")";
        }

        // ---------------- 2. 機械獣はラベル・バッジ・選択枠より背面 ----------------

        [Test]
        public void ThePortraitRendersBelowLabelsBadgesAndSelection()
        {
            // 上段・下段・左右端を含む8枚すべてで確かめます。
            for (int index = 0; index < cards.Count; index++)
            {
                GameObject card = cards[index];
                string where = Position(index);

                // 静止時とホバー中の両方で成り立ちます。
                AssertPortraitIsBehindTheOverlay(card, where + " 静止時");

                HoverAndGetLiftRoot(card);

                AssertPortraitIsBehindTheOverlay(card, where + " ホバー中");

                Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);
            }
        }

        private void AssertPortraitIsBehindTheOverlay(GameObject card, string because)
        {
            for (int layer = 0; layer < PortraitLayers.Length; layer++)
            {
                int portrait = DrawIndex(Part(card, PortraitLayers[layer]));

                for (int i = 0; i < InFrontOfThePortrait.Length; i++)
                {
                    Assert.That(
                        portrait,
                        Is.LessThan(DrawIndex(Part(card, InFrontOfThePortrait[i]))),
                        because + ": " + PortraitLayers[layer] + " が " +
                        InFrontOfThePortrait[i] + " より前面です。");
                }
            }
        }

        /// <summary>
        /// 枠を前面へ出したので、中を塗ってしまうとカードが全部隠れます。
        /// 輪郭としてだけ描かれることを確かめます。
        /// </summary>
        [Test]
        public void TheSelectionFrameIsAnOutlineSoItDoesNotHideTheCard()
        {
            Image frame = Part(SecondRowCard, "Frame").GetComponent<Image>();

            Assert.That(frame, Is.Not.Null, "Frame に Image がありません。");

            Assert.That(
                frame.type,
                Is.EqualTo(Image.Type.Sliced),
                "輪郭にするには9スライスが要ります。");

            Assert.That(
                frame.fillCenter,
                Is.False,
                "Frame は機械獣より前面です。中を塗るとカードが隠れます。");

            Assert.That(frame.sprite, Is.Not.Null, "Frame に Sprite がありません。");

            Vector4 border = frame.sprite.border;

            Assert.That(
                border.x + border.y + border.z + border.w,
                Is.GreaterThan(0f),
                "Sprite に9スライスの余白がないと、" +
                "fillCenter=false では何も描かれません（枠が消えます）。");
        }

        // ---------------- 3. カード全体を前面化しない ----------------

        [Test]
        public void HoveringDoesNotPromoteTheWholeCard()
        {
            GameObject card = SecondRowCard;

            Dictionary<Transform, int> before = Snapshot();

            int siblingBefore = card.transform.GetSiblingIndex();
            Vector3 visualBefore = Part(card, "VisualRoot").localPosition;
            Rect cardBefore = WorldRect(card.transform);

            HoverAndGetLiftRoot(card);

            // (a) Canvas も sorting も足しません。
            Assert.That(
                card.GetComponentsInChildren<Canvas>(true),
                Is.Empty,
                "ホバーでカードにCanvasが足されています（カード全体の前面化）。");

            // (b) 兄弟順を組み替えません（毎フレーム SetAsLastSibling も含みます）。
            Dictionary<Transform, int> after = Snapshot();

            Assert.That(
                after.Count,
                Is.EqualTo(before.Count),
                "ホバーで Canvas 配下の構成が変わっています。");

            foreach (KeyValuePair<Transform, int> entry in before)
            {
                Assert.That(
                    after.ContainsKey(entry.Key),
                    Is.True,
                    entry.Key.name + " がホバーで消えました。");

                Assert.That(
                    after[entry.Key],
                    Is.EqualTo(entry.Value),
                    entry.Key.name + " の描画順がホバーで変わりました。" +
                    "兄弟順の組み替えで前面化してはいけません。");
            }

            Assert.That(
                card.transform.GetSiblingIndex(),
                Is.EqualTo(siblingBefore),
                "カード自身の兄弟順が変わりました。");

            // (c) 動くのは機械獣だけです。カードも見た目の根も動きません。
            Assert.That(
                Part(card, "VisualRoot").localPosition,
                Is.EqualTo(visualBefore),
                "VisualRoot が動いています。動かすのは機械獣だけです。");

            Assert.That(
                WorldRect(card.transform),
                Is.EqualTo(cardBefore),
                "カードの矩形が動いています。");

            // (d) 色面は消しも透明化もしません。
            Graphic surface = Part(card, "AttributeSurface").GetComponent<Graphic>();

            Assert.That(surface, Is.Not.Null, "AttributeSurface に Graphic がありません。");
            Assert.That(
                surface.gameObject.activeInHierarchy,
                Is.True,
                "AttributeSurface を消して解決してはいけません。");
            Assert.That(
                surface.enabled,
                Is.True,
                "AttributeSurface を無効化して解決してはいけません。");
        }

        // ---------------- 4. ホバーを抜けたら元へ戻る ----------------

        [Test]
        public void LeavingHoverRestoresPortraitSorting()
        {
            GameObject card = SecondRowCard;

            Dictionary<Transform, int> before = Snapshot();

            Transform liftRoot = Part(card, "HoverLiftRoot");
            Vector3 restPosition = liftRoot.localPosition;
            Vector3 restScale = liftRoot.localScale;

            HoverAndGetLiftRoot(card);

            Lift(card).SetStateImmediate(CardLiftView.LiftState.Normal);

            Dictionary<Transform, int> after = Snapshot();

            foreach (KeyValuePair<Transform, int> entry in before)
            {
                Assert.That(
                    after.ContainsKey(entry.Key),
                    Is.True,
                    entry.Key.name + " がホバー解除で消えました。");

                Assert.That(
                    after[entry.Key],
                    Is.EqualTo(entry.Value),
                    entry.Key.name + " の描画順がホバー解除後に戻っていません。");
            }

            Assert.That(
                liftRoot.localPosition,
                Is.EqualTo(restPosition),
                "機械獣が元の位置へ戻っていません。");

            Assert.That(
                liftRoot.localScale,
                Is.EqualTo(restScale),
                "機械獣の拡大が元へ戻っていません。");

            Assert.That(
                card.GetComponentsInChildren<Canvas>(true),
                Is.Empty,
                "ホバー解除後もCanvasが残っています。");

            // 解除後も、静止時の前後関係が保たれています。
            AssertPortraitIsBehindTheOverlay(card, "ホバー解除後");

            for (int layer = 0; layer < PortraitLayers.Length; layer++)
            {
                int portrait = DrawIndex(Part(card, PortraitLayers[layer]));

                for (int i = 0; i < BehindThePortrait.Length; i++)
                {
                    Assert.That(
                        portrait,
                        Is.GreaterThan(DrawIndex(Part(card, BehindThePortrait[i]))),
                        "ホバー解除後: " + PortraitLayers[layer] + " が " +
                        BehindThePortrait[i] + " より背面です。");
                }
            }
        }

        // ---------------- prefab そのものの順序 ----------------

        [Test]
        public void ThePrefabItselfKeepsTheAgreedOrder()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            Transform visual = asset.transform.Find("VisualRoot");
            Assert.That(visual, Is.Not.Null, "VisualRoot がありません。");

            string[] expected =
            {
                "Background",
                "AttributeSurface",
                "Thumb",
                "Frame",
                "NameLabel",
                "LevelLabel",
                "SquadBadge",
            };

            Assert.That(
                visual.childCount,
                Is.EqualTo(expected.Length),
                "VisualRoot の子の数が想定と違います。");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    visual.GetChild(i).name,
                    Is.EqualTo(expected[i]),
                    "VisualRoot の " + i + " 番目が違います（描画順が崩れます）。");
            }

            Transform thumb = visual.Find("Thumb");

            string[] insideThumb = { "DragShadow", "DragGlow", "HoverLiftRoot" };

            Assert.That(
                thumb.childCount,
                Is.EqualTo(insideThumb.Length),
                "Thumb の子の数が想定と違います。");

            for (int i = 0; i < insideThumb.Length; i++)
            {
                Assert.That(
                    thumb.GetChild(i).name,
                    Is.EqualTo(insideThumb[i]),
                    "Thumb の " + i + " 番目が違います（影・発光が機械獣を覆います）。");
            }
        }
    }
}
