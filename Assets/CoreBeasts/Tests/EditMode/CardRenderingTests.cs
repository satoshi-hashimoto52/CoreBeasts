using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// カードが「描画され続ける」ことを固定する回帰テスト。
    ///
    /// 経緯:
    /// 1. 前面表示の解除に Canvas.enabled = false を使ったところ、
    ///    ネストしたCanvas配下のGraphicが描画されなくなり、
    ///    CORE BEASTS一覧のカードが丸ごと消えました。
    /// 2. そこで Canvas は常に有効のままにし、overrideSorting だけで
    ///    前面化する方式へ変えました。
    /// 3. しかしこの方式はホバー時にカード全体を最前面へ持ち上げてしまい、
    ///    さらに overrideSorting がスクロール領域の RectMask2D の切り抜きを
    ///    無効化するため、カードがスクロール範囲の外まではみ出しました。
    ///
    /// 現在の取り決めは「カードは前面化しない」です。
    /// 機械獣が色面より前に出ることは、prefabの恒久的な兄弟順だけで満たします。
    /// そのため VisualRoot の Canvas そのものを外し、
    /// 1. も 3. も構造的に起こり得なくしています。
    ///
    /// この変更は期待値の緩和ではなく、
    /// 「ホバーしたカード全体を最前面へ移動して解決しない」という
    /// ユーザー指定に合わせた契約変更です。
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

        /// <summary>カード配下のGraphicが実際に描画され得る状態か。</summary>
        private static void AssertCardIsRenderable(GameObject card, string because)
        {
            Graphic[] graphics = card.GetComponentsInChildren<Graphic>(true);
            Assert.That(graphics.Length, Is.GreaterThan(0), "Graphicがありません。");

            int visible = 0;

            foreach (Graphic graphic in graphics)
            {
                if (!graphic.gameObject.activeInHierarchy || !graphic.enabled)
                {
                    continue;
                }

                // 所属Canvasが辿れないGraphicは描画されません。
                if (graphic.canvas != null)
                {
                    visible++;
                }
            }

            Assert.That(
                visible,
                Is.GreaterThan(0),
                because + ": 描画可能なGraphicが1つもありません（カードが消えます）。");
        }

        // ---------------- 前面化の仕組みを持たないこと ----------------

        [Test]
        public void Prefab_TheCardHasNoNestedCanvas()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            Canvas[] canvases = asset.GetComponentsInChildren<Canvas>(true);

            Assert.That(
                canvases,
                Is.Empty,
                "カードにネストしたCanvasがあります。" +
                "前面化はしない取り決めなので、Canvasは不要です。" +
                "見つかった場所: " + Describe(canvases));
        }

        private static string Describe(Canvas[] canvases)
        {
            if (canvases.Length == 0)
            {
                return "(なし)";
            }

            string text = string.Empty;

            for (int i = 0; i < canvases.Length; i++)
            {
                text += (i == 0 ? string.Empty : ", ") + canvases[i].name;
            }

            return text;
        }

        [Test]
        public void LiftCycle_NeverCreatesACanvasOnTheCard()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                instance.GetComponentsInChildren<Canvas>(true),
                Is.Empty,
                "ホバーでカードにCanvasを足してはいけません。");

            lift.SetStateImmediate(CardLiftView.LiftState.Dragging);

            Assert.That(
                instance.GetComponentsInChildren<Canvas>(true),
                Is.Empty,
                "ドラッグでカードにCanvasを足してはいけません。");

            lift.SetStateImmediate(CardLiftView.LiftState.Normal);

            Assert.That(
                instance.GetComponentsInChildren<Canvas>(true),
                Is.Empty,
                "通常復帰後もCanvasは持ちません。");
        }

        // ---------------- 消えないこと ----------------

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
        public void LiftCycle_KeepsTheCardRenderableThroughout()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);
            AssertCardIsRenderable(instance, "DragReady");

            lift.SetStateImmediate(CardLiftView.LiftState.Dragging);
            AssertCardIsRenderable(instance, "Dragging");

            lift.SetStateImmediate(CardLiftView.LiftState.Normal);
            AssertCardIsRenderable(instance, "Normal復帰");
        }

        [Test]
        public void ResetImmediate_RestoresTheRestPoseWithoutHidingTheCard()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();

            RectTransform liftRoot =
                (RectTransform)instance.transform.Find("VisualRoot/Thumb/HoverLiftRoot");

            Assert.That(liftRoot, Is.Not.Null, "HoverLiftRoot が見つかりません。");

            Vector3 rest = liftRoot.localPosition;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                liftRoot.localPosition.y,
                Is.GreaterThan(rest.y),
                "ホバーで機械獣が持ち上がっていません。");

            lift.ResetImmediate();

            Assert.That(
                liftRoot.localPosition,
                Is.EqualTo(rest),
                "ResetImmediateで元の位置へ戻りません。");

            AssertCardIsRenderable(instance, "ResetImmediate後");
        }

        [Test]
        public void DisableThenReEnable_KeepsTheCardRenderable()
        {
            instance = CreateCardUnderCanvas();

            CardLiftView lift = instance.GetComponent<CardLiftView>();

            lift.SetStateImmediate(CardLiftView.LiftState.Dragging);

            // EditModeではUnityがOnDisableを配送しないため直接呼びます。
            EditModeLifecycle.Disable(lift);

            AssertCardIsRenderable(instance, "OnDisable後");
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
            // （キャラクターだけを浮かせるため、どちらも Thumb の配下にあります）
            Transform visual = instance.transform.Find("VisualRoot");
            Assert.That(visual.Find("Thumb/DragShadow").gameObject.activeSelf, Is.False);
            Assert.That(visual.Find("Thumb/DragGlow").gameObject.activeSelf, Is.False);
        }
    }
}
