using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// カードが「描画され続ける」ことを固定する回帰テスト。
    ///
    /// 背景: 前面表示の解除に Canvas.enabled = false を使ったところ、
    /// ネストしたCanvas配下のGraphicが描画されなくなり、
    /// CORE BEASTS一覧のカードが丸ごと消える不具合が発生しました。
    /// Canvasは常に有効のままとし、overrideSorting だけで前面化を制御します。
    /// </summary>
    public sealed class CardRenderingTests
    {
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";

        private GameObject parentCanvas;
        private GameObject instance;
        private CoreBeastRoster roster;
        private AttributePalette palette;
        private UiTextCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
            roster = TestRosterFactory.Create(1);
            catalog = ScriptableObject.CreateInstance<UiTextCatalog>();

            // overrideSorting は「ネストしたCanvas」でのみ機能するため、
            // 実際の画面と同じように親Canvasの下へ置きます。
            parentCanvas = new GameObject(
                "Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null) { Object.DestroyImmediate(instance); instance = null; }
            if (parentCanvas != null)
            {
                Object.DestroyImmediate(parentCanvas);
                parentCanvas = null;
            }
            if (catalog != null) { Object.DestroyImmediate(catalog); catalog = null; }
            if (palette != null) { Object.DestroyImmediate(palette); palette = null; }
            TestRosterFactory.Destroy(roster);
            roster = null;
        }

        /// <summary>実画面と同じく、親Canvasの下へカードを生成します。</summary>
        private GameObject CreateCardUnderCanvas()
        {
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            return Object.Instantiate(asset, parentCanvas.transform);
        }

        private static Canvas FindVisualRootCanvas(GameObject card)
        {
            Transform visual = card.transform.Find("VisualRoot");
            Assert.That(visual, Is.Not.Null, "VisualRoot が見つかりません。");

            Canvas canvas = visual.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, "VisualRoot に Canvas がありません。");

            return canvas;
        }

        /// <summary>VisualRoot配下のGraphicが実際に描画され得る状態か。</summary>
        private static void AssertCardIsRenderable(GameObject card, string because)
        {
            Canvas canvas = FindVisualRootCanvas(card);

            Assert.That(
                canvas.enabled,
                Is.True,
                $"{because}: VisualRootのCanvasが無効だと配下が一切描画されません。");

            Graphic[] graphics = card.GetComponentsInChildren<Graphic>(true);
            Assert.That(graphics.Length, Is.GreaterThan(0), "Graphicがありません。");

            int visible = 0;

            foreach (Graphic graphic in graphics)
            {
                if (!graphic.gameObject.activeInHierarchy || !graphic.enabled)
                {
                    continue;
                }

                // Canvasが無効だと canvas プロパティは null になります。
                if (graphic.canvas != null)
                {
                    visible++;
                }
            }

            Assert.That(
                visible,
                Is.GreaterThan(0),
                $"{because}: 描画可能なGraphicが1つもありません（カードが消えます）。");
        }

        [Test]
        public void Prefab_VisualRootCanvasIsEnabledAndNotOverridingSorting()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            Canvas canvas = FindVisualRootCanvas(asset);

            Assert.That(
                canvas.enabled,
                Is.True,
                "Prefab上でCanvasが無効だと、生成直後からカードが描画されません。");

            Assert.That(
                canvas.overrideSorting,
                Is.False,
                "通常状態では前面化しません。");
        }

        [Test]
        public void NormalState_CardStaysRenderable()
        {
            instance = CreateCardUnderCanvas();

            AssertCardIsRenderable(instance, "生成直後");

            CardLiftView lift = instance.GetComponent<CardLiftView>();
            lift.ResetImmediate();

            AssertCardIsRenderable(instance, "ResetImmediate後");
        }

        [Test]
        public void LiftCycle_NeverDisablesTheCanvas()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();
            Canvas canvas = FindVisualRootCanvas(instance);

            lift.SetState(CardLiftView.LiftState.DragReady);
            Assert.That(canvas.enabled, Is.True, "長押し時もCanvasは有効のままです。");
            Assert.That(canvas.overrideSorting, Is.True, "長押し時は前面へ出します。");
            AssertCardIsRenderable(instance, "DragReady");

            lift.SetState(CardLiftView.LiftState.Dragging);
            Assert.That(canvas.enabled, Is.True);
            AssertCardIsRenderable(instance, "Dragging");

            lift.SetState(CardLiftView.LiftState.Normal);
            Assert.That(canvas.enabled, Is.True, "終了後もCanvasを無効化しません。");
            Assert.That(
                canvas.overrideSorting,
                Is.False,
                "終了後は前面化を解除します。");
            AssertCardIsRenderable(instance, "Normal復帰");
        }

        [Test]
        public void ResetImmediate_RestoresSortingWithoutHidingTheCard()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();
            Canvas canvas = FindVisualRootCanvas(instance);
            int baseOrder = canvas.sortingOrder;

            lift.SetState(CardLiftView.LiftState.DragReady);
            Assert.That(canvas.sortingOrder, Is.GreaterThan(baseOrder));

            lift.ResetImmediate();

            Assert.That(canvas.enabled, Is.True);
            Assert.That(canvas.overrideSorting, Is.False);
            Assert.That(canvas.sortingOrder, Is.EqualTo(baseOrder), "sortingOrderを元へ戻します。");
            AssertCardIsRenderable(instance, "ResetImmediate後");
        }

        [Test]
        public void DisableThenReEnable_KeepsTheCardRenderable()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();

            lift.SetState(CardLiftView.LiftState.Dragging);

            // EditModeではUnityがOnDisableを配送しないため直接呼びます。
            EditModeLifecycle.Disable(lift);

            AssertCardIsRenderable(instance, "OnDisable後");
            Assert.That(FindVisualRootCanvas(instance).enabled, Is.True);
        }

        [Test]
        public void BoundCard_ShowsItsGraphicsInNormalState()
        {
            instance = CreateCardUnderCanvas();

            BeastCardView card = instance.GetComponent<BeastCardView>();
            card.Bind(roster.Owned[0], palette, catalog, null);

            Assert.That(
                instance.activeSelf,
                Is.True,
                "有効な個体を割り当てたカードは表示されます。");

            AssertCardIsRenderable(instance, "Bind後");

            // 影と発光は通常時は消えている
            Transform visual = instance.transform.Find("VisualRoot");
            Assert.That(visual.Find("DragShadow").gameObject.activeSelf, Is.False);
            Assert.That(visual.Find("DragGlow").gameObject.activeSelf, Is.False);
        }
    }
}
