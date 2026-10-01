using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 所持カードの属性表示。チップと文字をやめ、外周フレームと薄い背景で示します。
    ///
    /// 生成メッシュの頂点位置と色を直接読んで検証するため、
    /// 見た目の比較画像には依存しません。
    /// </summary>
    public sealed class AttributeCardSurfaceTests
    {
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";
        private const float Width = 210f;
        private const float Height = 255f;

        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<Object> spawned = new List<Object>();

        private AttributePalette palette;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
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

            if (palette != null)
            {
                Object.DestroyImmediate(palette);
                palette = null;
            }
        }

        // ---------------- 素材 ----------------

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, FieldFlags);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            field.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, FieldFlags)?.GetValue(target);
        }

        private CoreBeastDefinition Definition(
            UnitAttribute primary,
            bool dual = false,
            UnitAttribute secondary = UnitAttribute.Blue)
        {
            CoreBeastDefinition definition =
                ScriptableObject.CreateInstance<CoreBeastDefinition>();

            definition.name = "CB_" + primary + (dual ? "_" + secondary : string.Empty);

            SetField(definition, "beastId", definition.name);
            SetField(definition, "displayName", definition.name);
            SetField(definition, "primaryAttribute", primary);
            SetField(definition, "hasSecondaryAttribute", dual);
            SetField(definition, "secondaryAttribute", secondary);

            spawned.Add(definition);

            return definition;
        }

        private DiagonalAttributeCardGraphic Surface()
        {
            GameObject go = new GameObject("AttributeSurface");
            spawned.Add(go);

            DiagonalAttributeCardGraphic graphic =
                go.AddComponent<DiagonalAttributeCardGraphic>();

            graphic.rectTransform.sizeDelta = new Vector2(Width, Height);

            return graphic;
        }

        /// <summary>定義から色を解決して流し込みます（カード本体と同じ経路）。</summary>
        private DiagonalAttributeCardGraphic SurfaceFor(CoreBeastDefinition definition)
        {
            DiagonalAttributeCardGraphic graphic = Surface();

            AttributeColorResolver.ResolveCardColors(
                palette, definition,
                out Color primary, out Color secondary, out bool dual);

            graphic.Apply(primary, secondary, dual);

            return graphic;
        }

        /// <summary>生成メッシュの頂点をそのまま取り出します。</summary>
        private static List<UIVertex> Vertices(DiagonalAttributeCardGraphic graphic)
        {
            VertexHelper vh = new VertexHelper();

            MethodInfo populate = typeof(DiagonalAttributeCardGraphic).GetMethod(
                "OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(VertexHelper) },
                null);

            Assert.That(populate, Is.Not.Null, "OnPopulateMesh が見つかりません。");

            populate.Invoke(graphic, new object[] { vh });

            List<UIVertex> list = new List<UIVertex>();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                UIVertex vertex = default;
                vh.PopulateUIVertex(ref vertex, i);
                list.Add(vertex);
            }

            vh.Dispose();

            return list;
        }

        private static void AssertRgb(UIVertex vertex, Color expected, string because)
        {
            Color actual = vertex.color;

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1f / 255f), because + " (R)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1f / 255f), because + " (G)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1f / 255f), because + " (B)");
        }

        /// <summary>第一属性色で塗られる頂点の番号。</summary>
        private static readonly int[] FirstSide =
            { 0, 1, 2, 6, 7, 8, 9, 10, 11, 12, 13 };

        /// <summary>第二属性色で塗られる頂点の番号。</summary>
        private static readonly int[] SecondSide =
            { 3, 4, 5, 14, 15, 16, 17, 18, 19, 20, 21 };

        private static Vector2 Centroid(List<UIVertex> verts, int[] indices)
        {
            Vector2 sum = Vector2.zero;

            for (int i = 0; i < indices.Length; i++)
            {
                sum += (Vector2)verts[indices[i]].position;
            }

            return sum / indices.Length;
        }

        // ---------------- メッシュの形 ----------------

        [Test]
        public void TheMeshHasTheExpectedVertexBudget()
        {
            List<UIVertex> verts = Vertices(Surface());

            Assert.That(
                verts.Count,
                Is.EqualTo(DiagonalAttributeCardGraphic.TotalVertexCount),
                "頂点数が想定と違います。");

            Assert.That(verts.Count, Is.EqualTo(22));
        }

        [Test]
        public void TheOuterVerticesSitExactlyOnTheCardEdges()
        {
            DiagonalAttributeCardGraphic graphic = Surface();
            List<UIVertex> verts = Vertices(graphic);

            Rect r = graphic.rectTransform.rect;

            // フレーム上辺の外側 2 点 = 左上・右上
            AssertPoint(verts[6], r.xMin, r.yMax, "上辺の左");
            AssertPoint(verts[7], r.xMax, r.yMax, "上辺の右");

            // フレーム下辺の外側 2 点 = 右下・左下
            AssertPoint(verts[18], r.xMax, r.yMin, "下辺の右");
            AssertPoint(verts[19], r.xMin, r.yMin, "下辺の左");
        }

        private static void AssertPoint(UIVertex v, float x, float y, string because)
        {
            Assert.That(v.position.x, Is.EqualTo(x).Within(0.001f), because + " のX");
            Assert.That(v.position.y, Is.EqualTo(y).Within(0.001f), because + " のY");
        }

        [Test]
        public void TheBorderIsInsetByTheConfiguredWidth()
        {
            DiagonalAttributeCardGraphic graphic = Surface();

            Assert.That(graphic.BorderWidth, Is.InRange(4f, 6f));

            List<UIVertex> verts = Vertices(graphic);
            Rect r = graphic.rectTransform.rect;

            float b = graphic.BorderWidth;

            // 上辺の内側 2 点
            AssertPoint(verts[8], r.xMax - b, r.yMax - b, "上辺の内側右");
            AssertPoint(verts[9], r.xMin + b, r.yMax - b, "上辺の内側左");
        }

        // ---------------- 単属性 ----------------

        [Test]
        public void ASingleAttributeCardIsOneColourAllOver()
        {
            CoreBeastDefinition definition = Definition(UnitAttribute.Red);

            DiagonalAttributeCardGraphic graphic = SurfaceFor(definition);

            Assert.That(graphic.IsDualAttribute, Is.False);

            Color expected = palette.GetColors(UnitAttribute.Red).PrimaryColor;

            List<UIVertex> verts = Vertices(graphic);

            for (int i = 0; i < verts.Count; i++)
            {
                AssertRgb(verts[i], expected, "単属性なので頂点" + i + "も同じ色です");
            }
        }

        [Test]
        public void ASingleAttributeStillUsesTheSharedPalette()
        {
            foreach (UnitAttribute attribute in
                     new[] { UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue })
            {
                DiagonalAttributeCardGraphic graphic = SurfaceFor(Definition(attribute));

                AssertRgb(
                    Vertices(graphic)[0],
                    palette.GetColors(attribute).PrimaryColor,
                    attribute + " の色がパレットと一致しません");
            }
        }

        // ---------------- 2属性 ----------------

        [TestCase(UnitAttribute.Red, UnitAttribute.Blue)]
        [TestCase(UnitAttribute.Red, UnitAttribute.Green)]
        [TestCase(UnitAttribute.Green, UnitAttribute.Blue)]
        public void ADualAttributeCardPutsTheFirstAttributeTopLeft(
            UnitAttribute first, UnitAttribute second)
        {
            DiagonalAttributeCardGraphic graphic =
                SurfaceFor(Definition(first, true, second));

            Assert.That(graphic.IsDualAttribute, Is.True);

            Color expectedFirst = palette.GetColors(first).PrimaryColor;
            Color expectedSecond = palette.GetColors(second).PrimaryColor;

            Assert.That(
                expectedFirst,
                Is.Not.EqualTo(expectedSecond),
                "この組み合わせは色が違う前提です。");

            List<UIVertex> verts = Vertices(graphic);

            for (int i = 0; i < FirstSide.Length; i++)
            {
                AssertRgb(
                    verts[FirstSide[i]], expectedFirst,
                    "左上側の頂点" + FirstSide[i] + "は第一属性色です");
            }

            for (int i = 0; i < SecondSide.Length; i++)
            {
                AssertRgb(
                    verts[SecondSide[i]], expectedSecond,
                    "右下側の頂点" + SecondSide[i] + "は第二属性色です");
            }

            // 左上・右下という位置関係そのものを確かめます。
            Vector2 firstCentre = Centroid(verts, FirstSide);
            Vector2 secondCentre = Centroid(verts, SecondSide);

            Assert.That(
                firstCentre.x,
                Is.LessThan(secondCentre.x),
                "第一属性が左側にありません。");

            Assert.That(
                firstCentre.y,
                Is.GreaterThan(secondCentre.y),
                "第一属性が上側にありません。");
        }

        [Test]
        public void TheOuterCornersLandOnTheExpectedSideOfTheDiagonal()
        {
            DiagonalAttributeCardGraphic graphic =
                SurfaceFor(Definition(UnitAttribute.Red, true, UnitAttribute.Blue));

            List<UIVertex> verts = Vertices(graphic);

            Color red = palette.GetColors(UnitAttribute.Red).PrimaryColor;
            Color blue = palette.GetColors(UnitAttribute.Blue).PrimaryColor;

            // 頂点6 = 外周の左上、頂点18 = 外周の右下。対角線のどちら側かが明確な2点です。
            AssertRgb(verts[6], red, "左上隅は第一属性です");
            AssertRgb(verts[18], blue, "右下隅は第二属性です");
        }

        [Test]
        public void SwappingTheAttributesSwapsTheSides()
        {
            List<UIVertex> redBlue =
                Vertices(SurfaceFor(Definition(UnitAttribute.Red, true, UnitAttribute.Blue)));

            List<UIVertex> blueRed =
                Vertices(SurfaceFor(Definition(UnitAttribute.Blue, true, UnitAttribute.Red)));

            Color red = palette.GetColors(UnitAttribute.Red).PrimaryColor;
            Color blue = palette.GetColors(UnitAttribute.Blue).PrimaryColor;

            AssertRgb(redBlue[6], red, "Red/Blue の左上は Red");
            AssertRgb(redBlue[18], blue, "Red/Blue の右下は Blue");

            AssertRgb(blueRed[6], blue, "Blue/Red の左上は Blue");
            AssertRgb(blueRed[18], red, "Blue/Red の右下は Red");
        }

        [Test]
        public void TheDiagonalSplitIsNotAHorizontalOrVerticalCut()
        {
            List<UIVertex> verts =
                Vertices(SurfaceFor(Definition(UnitAttribute.Red, true, UnitAttribute.Blue)));

            Vector2 firstCentre = Centroid(verts, FirstSide);
            Vector2 secondCentre = Centroid(verts, SecondSide);

            // 上下分割でも左右分割でもなく、両方向にずれていること。
            Assert.That(
                Mathf.Abs(firstCentre.x - secondCentre.x),
                Is.GreaterThan(1f),
                "横方向に差がありません（上下分割になっています）。");

            Assert.That(
                Mathf.Abs(firstCentre.y - secondCentre.y),
                Is.GreaterThan(1f),
                "縦方向に差がありません（左右分割になっています）。");
        }

        // ---------------- 濃さと入力 ----------------

        [Test]
        public void TheBackgroundStaysFaintAndTheBorderStaysSolid()
        {
            DiagonalAttributeCardGraphic graphic =
                SurfaceFor(Definition(UnitAttribute.Red));

            Assert.That(graphic.BackgroundAlpha, Is.InRange(0.12f, 0.18f));
            Assert.That(graphic.BorderAlpha, Is.InRange(0.9f, 1f));

            List<UIVertex> verts = Vertices(graphic);

            for (int i = 0; i < DiagonalAttributeCardGraphic.FillVertexCount; i++)
            {
                Assert.That(
                    verts[i].color.a / 255f,
                    Is.InRange(0.12f, 0.18f),
                    "背景の頂点" + i + "が薄さの範囲外です。");
            }

            for (int i = DiagonalAttributeCardGraphic.FillVertexCount; i < verts.Count; i++)
            {
                Assert.That(
                    verts[i].color.a / 255f,
                    Is.InRange(0.89f, 1f),
                    "フレームの頂点" + i + "が濃さの範囲外です。");
            }
        }

        [Test]
        public void TheSurfaceNeverTakesInput()
        {
            DiagonalAttributeCardGraphic graphic = Surface();

            Assert.That(
                graphic.raycastTarget,
                Is.False,
                "属性表示がタップを奪ってはいけません。");
        }

        [Test]
        public void ResizingTheCardRedrawsTheMesh()
        {
            DiagonalAttributeCardGraphic graphic =
                SurfaceFor(Definition(UnitAttribute.Red));

            graphic.rectTransform.sizeDelta = new Vector2(Width * 2f, Height * 2f);

            List<UIVertex> verts = Vertices(graphic);
            Rect r = graphic.rectTransform.rect;

            AssertPoint(verts[6], r.xMin, r.yMax, "拡大後の左上");
            AssertPoint(verts[18], r.xMax, r.yMin, "拡大後の右下");
        }

        // ---------------- Prefab の中身 ----------------

        private GameObject SpawnCard()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            GameObject card = Object.Instantiate(asset);
            spawned.Add(card);

            return card;
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

        [Test]
        public void TheCardNoLongerCarriesAnAttributeChipOrLabel()
        {
            GameObject card = SpawnCard();

            Assert.That(
                FindDeep(card.transform, "AttributeChip"),
                Is.Null,
                "AttributeChip が Prefab に残っています。");

            Assert.That(
                FindDeep(card.transform, "AttributeLabel"),
                Is.Null,
                "AttributeLabel が Prefab に残っています。");

            BeastCardView view = card.GetComponent<BeastCardView>();

            Assert.That(
                view.GetType().GetField("attributeChip", FieldFlags),
                Is.Null,
                "BeastCardView に attributeChip の参照が残っています。");

            Assert.That(
                view.GetType().GetField("attributeLabel", FieldFlags),
                Is.Null,
                "BeastCardView に attributeLabel の参照が残っています。");
        }

        [Test]
        public void TheCardKeepsItsNameLevelBadgeAndSelectionFrame()
        {
            GameObject card = SpawnCard();
            BeastCardView view = card.GetComponent<BeastCardView>();

            Assert.That(GetField(view, "nameLabel"), Is.Not.Null, "NameLabel を残します。");
            Assert.That(GetField(view, "levelLabel"), Is.Not.Null, "LevelLabel を残します。");
            Assert.That(GetField(view, "squadBadge"), Is.Not.Null, "SquadBadge を残します。");
            Assert.That(
                GetField(view, "selectionFrame"), Is.Not.Null, "選択枠を残します。");
            Assert.That(
                GetField(view, "attributeSurface"), Is.Not.Null, "属性表示が未配線です。");

            Assert.That(FindDeep(card.transform, "NameLabel"), Is.Not.Null);
            Assert.That(FindDeep(card.transform, "LevelLabel"), Is.Not.Null);
            Assert.That(FindDeep(card.transform, "SquadBadge"), Is.Not.Null);

            Assert.That(
                card.GetComponentsInChildren<TMP_Text>(true).Length,
                Is.EqualTo(2),
                "カードに残す文字は名前とレベルの2つだけです。");
        }

        [Test]
        public void TheAttributeFrameNeverCoversTheSelectionFrame()
        {
            GameObject card = SpawnCard();
            BeastCardView view = card.GetComponent<BeastCardView>();

            RectTransform selection =
                ((Image)GetField(view, "selectionFrame")).rectTransform;

            RectTransform surface =
                ((DiagonalAttributeCardGraphic)GetField(view, "attributeSurface"))
                .rectTransform;

            Rect selectionRect = selection.rect;
            Rect surfaceRect = surface.rect;

            // 選択枠はカードより外側の帯、属性フレームはカードの内側。
            // 帯が重ならないため、属性フレームが選択枠を上書きできません。
            Assert.That(
                surfaceRect.width,
                Is.LessThan(selectionRect.width),
                "属性フレームが選択枠の帯まではみ出しています。");

            Assert.That(
                surfaceRect.height,
                Is.LessThan(selectionRect.height),
                "属性フレームが選択枠の帯まではみ出しています。");

            // 編成済みチェックは属性フレームより後（前面）に描きます。
            Transform badge = FindDeep(card.transform, "SquadBadge");

            Assert.That(
                badge.GetSiblingIndex(),
                Is.GreaterThan(surface.GetSiblingIndex()),
                "編成済みチェックが属性フレームに隠れます。");
        }

        [Test]
        public void TheAttributeSurfaceCoversTheWholeCardFace()
        {
            GameObject card = SpawnCard();
            BeastCardView view = card.GetComponent<BeastCardView>();

            RectTransform surface =
                ((DiagonalAttributeCardGraphic)GetField(view, "attributeSurface"))
                .rectTransform;

            Assert.That(surface.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(surface.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(surface.sizeDelta, Is.EqualTo(Vector2.zero));
        }

        // ---------------- 個体ごとの反映 ----------------

        [Test]
        public void EachCardShowsItsOwnAttributesEvenWhenBoundInSequence()
        {
            GameObject firstCard = SpawnCard();
            GameObject secondCard = SpawnCard();

            UiTextCatalog catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            spawned.Add(catalog);

            OwnedCoreBeast single = Owned("a", Definition(UnitAttribute.Red));
            OwnedCoreBeast dual =
                Owned("b", Definition(UnitAttribute.Green, true, UnitAttribute.Blue));

            firstCard.GetComponent<BeastCardView>().Bind(single, palette, catalog, null);
            secondCard.GetComponent<BeastCardView>().Bind(dual, palette, catalog, null);

            DiagonalAttributeCardGraphic firstSurface =
                firstCard.GetComponentInChildren<DiagonalAttributeCardGraphic>(true);

            DiagonalAttributeCardGraphic secondSurface =
                secondCard.GetComponentInChildren<DiagonalAttributeCardGraphic>(true);

            Assert.That(firstSurface.IsDualAttribute, Is.False);
            Assert.That(secondSurface.IsDualAttribute, Is.True);

            AssertRgb(
                Vertices(firstSurface)[6],
                palette.GetColors(UnitAttribute.Red).PrimaryColor,
                "単属性カードが自分の属性色を出していません");

            AssertRgb(
                Vertices(secondSurface)[6],
                palette.GetColors(UnitAttribute.Green).PrimaryColor,
                "2属性カードの左上が第一属性ではありません");

            AssertRgb(
                Vertices(secondSurface)[18],
                palette.GetColors(UnitAttribute.Blue).PrimaryColor,
                "2属性カードの右下が第二属性ではありません");
        }

        [Test]
        public void TwoInstancesOfTheSameDefinitionShowTheSameAttributes()
        {
            GameObject firstCard = SpawnCard();
            GameObject secondCard = SpawnCard();

            UiTextCatalog catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            spawned.Add(catalog);

            CoreBeastDefinition shared =
                Definition(UnitAttribute.Red, true, UnitAttribute.Blue);

            firstCard.GetComponent<BeastCardView>()
                .Bind(Owned("twin_a", shared), palette, catalog, null);

            secondCard.GetComponent<BeastCardView>()
                .Bind(Owned("twin_b", shared), palette, catalog, null);

            List<UIVertex> a =
                Vertices(firstCard.GetComponentInChildren<DiagonalAttributeCardGraphic>(true));

            List<UIVertex> b =
                Vertices(secondCard.GetComponentInChildren<DiagonalAttributeCardGraphic>(true));

            Color red = palette.GetColors(UnitAttribute.Red).PrimaryColor;
            Color blue = palette.GetColors(UnitAttribute.Blue).PrimaryColor;

            AssertRgb(a[6], red, "1体目の左上");
            AssertRgb(b[6], red, "2体目の左上");
            AssertRgb(a[18], blue, "1体目の右下");
            AssertRgb(b[18], blue, "2体目の右下");
        }

        private OwnedCoreBeast Owned(string instanceId, CoreBeastDefinition definition)
        {
            OwnedCoreBeast beast = new OwnedCoreBeast();

            SetField(beast, "instanceId", instanceId);
            SetField(beast, "definition", definition);
            SetField(beast, "level", 1);

            return beast;
        }

        // ---------------- 既存の操作 ----------------

        [Test]
        public void BindingStillLeavesTheCardTappableAndUnselected()
        {
            GameObject card = SpawnCard();

            UiTextCatalog catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            spawned.Add(catalog);

            BeastCardView view = card.GetComponent<BeastCardView>();

            view.Bind(Owned("x", Definition(UnitAttribute.Red)), palette, catalog, null);

            Assert.That(card.activeSelf, Is.True);
            Assert.That(view.IsMarkedInSquad, Is.False, "初期状態で編成済みにはしません。");
            Assert.That(
                card.GetComponent<Image>().raycastTarget,
                Is.True,
                "タップ領域が失われています。");

            view.SetInSquad(true);
            Assert.That(view.IsMarkedInSquad, Is.True, "編成済みチェックが働きません。");

            view.SetSelected(true);

            Image selection = (Image)GetField(view, "selectionFrame");
            Color selected = selection.color;

            view.SetSelected(false);

            Assert.That(
                selection.color,
                Is.Not.EqualTo(selected),
                "選択枠の切り替えが働きません。");
        }
    }
}
