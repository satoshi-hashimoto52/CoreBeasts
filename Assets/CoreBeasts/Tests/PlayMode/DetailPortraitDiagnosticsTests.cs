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
    /// 上部ステータス詳細の大型立ち絵が、選択した個体の属性へ追従しているかを調べます。
    ///
    /// 詳細立ち絵は一覧カードとは別の経路です。
    ///   BeastDetailPanel.Show
    ///     → CoreBeastView.SetAttribute / SetAttributes → Apply
    ///     → SpriteRenderer へ MaterialPropertyBlock（_Primary_Color / _Secondary_Color）
    ///     → M_CoreBeastTint (CoreBeastTint.shadergraph)
    ///     → PortraitCamera が RenderTexture へ描画
    ///     → 詳細欄の Portrait RawImage が表示
    ///
    /// どの段でも止まり得るため、段ごとに分けて確かめます。
    ///   A. PropertyBlock の色が変わらない       → Panel / View / Property ID
    ///   B. PropertyBlock は正しいが HasProperty=false → Reference 名の不一致
    ///   C. ここまで正しいが RenderTexture が変わらない → Camera / RenderTexture
    ///   D. RenderTexture は変わるが画面が変わらない  → RawImage / 参照先 / Canvas
    ///
    /// これは診断です。製品コードは変更していません。
    /// </summary>
    public sealed class DetailPortraitDiagnosticsTests
    {
        private const string SceneName = "UnitSet";
        private const int WarmUpFrames = 4;

        /// <summary>色の比較許容。シェーダへ渡る値は float です。</summary>
        private const float ColorTolerance = 0.004f;

        private UnitSetScreen screen;
        private BeastDetailPanel detailPanel;
        private CoreBeastView portraitView;
        private SpriteRenderer portraitRenderer;
        private PortraitRenderTarget renderTarget;
        private Camera portraitCamera;
        private RawImage portraitImage;
        private AttributePalette palette;

        private readonly List<BeastCardView> cards = new List<BeastCardView>();
        private readonly List<Texture2D> readbacks = new List<Texture2D>();

        private ISquadRepository originalRepository;
        private static int emptySceneCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Scene bootstrap = SceneManager.CreateScene(
                "DetailPortraitTests_Bootstrap_" + emptySceneCounter++);

            SceneManager.SetActiveScene(bootstrap);

            yield return null;

            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            yield return null;

            screen = FindOne<UnitSetScreen>();
            detailPanel = FindOne<BeastDetailPanel>();
            renderTarget = FindOne<PortraitRenderTarget>();

            palette = (AttributePalette)GetField(screen, "palette");

            portraitView = (CoreBeastView)GetField(detailPanel, "portraitView");
            Assert.That(portraitView, Is.Not.Null, "BeastDetailPanel.portraitView が未設定です。");

            portraitRenderer = (SpriteRenderer)GetField(portraitView, "spriteRenderer");
            Assert.That(
                portraitRenderer, Is.Not.Null, "CoreBeastView.spriteRenderer が未設定です。");

            portraitCamera = (Camera)GetField(renderTarget, "portraitCamera");
            portraitImage = (RawImage)GetField(renderTarget, "targetImage");

            Assert.That(portraitCamera, Is.Not.Null, "PortraitRenderTarget.portraitCamera が未設定です。");
            Assert.That(portraitImage, Is.Not.Null, "PortraitRenderTarget.targetImage が未設定です。");

            cards.Clear();
            cards.AddRange(Find("Content").GetComponentsInChildren<BeastCardView>(true));

            Assert.That(cards.Count, Is.GreaterThan(0), "一覧にカードがありません。");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < readbacks.Count; i++)
            {
                if (readbacks[i] != null)
                {
                    Object.Destroy(readbacks[i]);
                }
            }

            readbacks.Clear();
            cards.Clear();

            SquadRepositoryProvider.SetShared(originalRepository);
            originalRepository = null;

            screen = null;
            detailPanel = null;
            portraitView = null;
            portraitRenderer = null;
            renderTarget = null;
            portraitCamera = null;
            portraitImage = null;
            palette = null;

            Scene loaded = SceneManager.GetSceneByName(SceneName);

            Scene empty = SceneManager.CreateScene(
                "DetailPortraitTests_TearDown_" + emptySceneCounter++);

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
                typeof(T).Name + " は1個だけ存在する必要があります（実際 " + found.Count + " 個）。");

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

        private static Vector2 ScreenCentreOf(Transform node)
        {
            RectTransform rect = (RectTransform)node;
            Canvas root = rect.GetComponentInParent<Canvas>().rootCanvas;

            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;

            return RectTransformUtility.WorldToScreenPoint(
                cam, rect.TransformPoint(rect.rect.center));
        }

        /// <summary>実際の選択経路（短いタップ）でカードを選びます。</summary>
        private IEnumerator SelectCard(BeastCardView card)
        {
            PointerEventData pointer = Pointer(ScreenCentreOf(card.transform));

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);

            // 長押し(0.2秒)には満たない短いタップにします。
            yield return null;

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            Assert.That(
                card.CurrentState,
                Is.EqualTo(CardGestureState.Idle),
                card.name + ": タップ後に Idle へ戻っていません。");

            yield return new WaitForEndOfFrame();
            yield return null;
        }

        /// <summary>指定の属性の組み合わせを持つカードを探します。</summary>
        private BeastCardView CardWith(UnitAttribute primary, UnitAttribute? secondary)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                OwnedCoreBeast beast = cards[i].Beast;

                if (beast == null || !beast.IsValid)
                {
                    continue;
                }

                CoreBeastDefinition definition = beast.Definition;

                if (definition.PrimaryAttribute != primary)
                {
                    continue;
                }

                if (secondary.HasValue)
                {
                    if (definition.HasSecondaryAttribute
                        && definition.SecondaryAttribute == secondary.Value)
                    {
                        return cards[i];
                    }
                }
                else if (!definition.HasSecondaryAttribute)
                {
                    return cards[i];
                }
            }

            Assert.Fail(
                "一覧に " + primary +
                (secondary.HasValue ? " / " + secondary.Value : " 単色") +
                " の個体がありません。検査できません。");

            return null;
        }

        /// <summary>
        /// Inspector の色は sRGB です。CoreBeastView は Linear 空間ではリニアへ
        /// 変換してからシェーダへ渡すため、期待値も同じ規則で作ります。
        /// </summary>
        private static Color ToRenderColor(Color color)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            {
                return color;
            }

            Color converted = color.linear;
            converted.a = color.a;

            return converted;
        }

        private static void AssertColor(Color actual, Color expected, string because)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance), because + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance), because + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance), because + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(ColorTolerance), because + " (a)");
        }

        /// <summary>RenderTexture の内容を読み出し、画素のハッシュと有効画素数を返します。</summary>
        private (uint hash, int opaquePixels) CaptureRenderTexture()
        {
            RenderTexture target = portraitCamera.targetTexture;

            Assert.That(target, Is.Not.Null, "PortraitCamera.targetTexture が null です。");
            Assert.That(target.IsCreated(), Is.True, "RenderTexture が未作成です。");

            Texture2D readback = new Texture2D(
                target.width, target.height, TextureFormat.RGBA32, false);

            readbacks.Add(readback);

            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                readback.Apply(false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = readback.GetPixels32();

            // FNV-1a。内容が1画素でも変われば値が変わります。
            uint hash = 2166136261u;
            int opaque = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];

                hash = (hash ^ p.r) * 16777619u;
                hash = (hash ^ p.g) * 16777619u;
                hash = (hash ^ p.b) * 16777619u;
                hash = (hash ^ p.a) * 16777619u;

                if (p.a > 0)
                {
                    opaque++;
                }
            }

            return (hash, opaque);
        }

        private string DescribeBlock()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            portraitRenderer.GetPropertyBlock(block);

            Material material = portraitRenderer.sharedMaterial;

            return
                "\n  renderer.enabled       : " + portraitRenderer.enabled +
                "\n  activeInHierarchy      : " + portraitRenderer.gameObject.activeInHierarchy +
                "\n  material               : " + (material == null ? "null" : material.name) +
                "\n  shader                 : " +
                    (material == null || material.shader == null ? "null" : material.shader.name) +
                "\n  _Primary_Color (block) : " + block.GetColor("_Primary_Color") +
                "\n  _Secondary_Color(block): " + block.GetColor("_Secondary_Color") +
                "\n  view.PrimaryAttribute  : " + portraitView.PrimaryAttribute +
                "\n  view.SecondaryAttribute: " + portraitView.SecondaryAttribute +
                "\n  view.UseSecondary      : " + portraitView.UseSecondaryAttribute;
        }

        // ---------------- テスト1: PropertyBlock ----------------

        [UnityTest]
        public IEnumerator SelectingEachAttributeUpdatesThePortraitRendererPropertyBlock()
        {
            Material material = portraitRenderer.sharedMaterial;

            Assert.That(material, Is.Not.Null, "立ち絵の Material が null です。");

            // (B) Reference 名の一致。MaterialPropertyBlock.GetColor は
            //     シェーダに無いプロパティでも値を返すため、必ず material 側で確かめます。
            string[] required =
            {
                "_Primary_Color", "_Secondary_Color",
                "_Emission_Color", "_Emission_Strength",
                "_Primary_Mask", "_Secondary_Mask", "_Base_Texture",
            };

            for (int i = 0; i < required.Length; i++)
            {
                Assert.That(
                    material.HasProperty(Shader.PropertyToID(required[i])),
                    Is.True,
                    "シェーダに " + required[i] + " がありません" +
                    "（ShaderGraph の Reference 名と C# の Property ID が不一致）。" +
                    " shader=" + material.shader.name);
            }

            // マスクが実際に割り当たっていること。
            Assert.That(
                material.GetTexture("_Primary_Mask"), Is.Not.Null, "_Primary_Mask が未設定です。");

            Assert.That(
                material.GetTexture("_Secondary_Mask"), Is.Not.Null, "_Secondary_Mask が未設定です。");

            Assert.That(
                material.GetTexture("_Base_Texture"), Is.Not.Null, "_Base_Texture が未設定です。");

            (UnitAttribute primary, UnitAttribute? secondary, string label)[] order =
            {
                (UnitAttribute.Red, null, "RED"),
                (UnitAttribute.Green, null, "GREEN"),
                (UnitAttribute.Blue, null, "BLUE"),
                (UnitAttribute.Red, UnitAttribute.Blue, "RED/BLUE"),
                (UnitAttribute.Green, null, "GREEN(再)"),
            };

            for (int i = 0; i < order.Length; i++)
            {
                BeastCardView card = CardWith(order[i].primary, order[i].secondary);

                yield return SelectCard(card);

                // (A) 属性が View へ届いていること。
                Assert.That(
                    portraitView.PrimaryAttribute,
                    Is.EqualTo(order[i].primary),
                    order[i].label + ": CoreBeastView の一次属性が更新されていません。" +
                    DescribeBlock());

                Assert.That(
                    portraitView.UseSecondaryAttribute,
                    Is.EqualTo(order[i].secondary.HasValue),
                    order[i].label + ": 2色フラグが一致しません。" + DescribeBlock());

                if (order[i].secondary.HasValue)
                {
                    Assert.That(
                        portraitView.SecondaryAttribute,
                        Is.EqualTo(order[i].secondary.Value),
                        order[i].label + ": 二次属性が更新されていません。" + DescribeBlock());
                }

                // 表示状態。
                Assert.That(
                    portraitRenderer.enabled, Is.True, order[i].label + ": SpriteRenderer が無効です。");

                Assert.That(
                    portraitRenderer.gameObject.activeInHierarchy,
                    Is.True, order[i].label + ": 立ち絵オブジェクトが非アクティブです。");

                // (A) PropertyBlock の実測値。
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                portraitRenderer.GetPropertyBlock(block);

                Color expectedPrimary = ToRenderColor(
                    palette.GetColors(order[i].primary).PrimaryColor);

                Color expectedSecondary = ToRenderColor(
                    order[i].secondary.HasValue
                        ? palette.GetColors(order[i].secondary.Value).SecondaryColor
                        : palette.GetColors(order[i].primary).SecondaryColor);

                AssertColor(
                    block.GetColor("_Primary_Color"), expectedPrimary,
                    order[i].label + ": _Primary_Color が期待値と違います。" + DescribeBlock());

                AssertColor(
                    block.GetColor("_Secondary_Color"), expectedSecondary,
                    order[i].label + ": _Secondary_Color が期待値と違います。" + DescribeBlock());
            }
        }

        // ---------------- テスト2: RenderTexture ----------------

        [UnityTest]
        public IEnumerator SelectingEachAttributeChangesThePortraitRenderTexture()
        {
            Assert.That(
                portraitCamera.enabled, Is.True, "PortraitCamera が無効です。");

            Assert.That(
                portraitCamera.gameObject.activeInHierarchy,
                Is.True, "PortraitCamera が非アクティブです。");

            int portraitLayer = portraitRenderer.gameObject.layer;

            Assert.That(
                (portraitCamera.cullingMask & (1 << portraitLayer)) != 0,
                Is.True,
                "PortraitCamera の CullingMask に立ち絵の Layer " +
                portraitLayer + " が含まれていません。");

            (UnitAttribute primary, UnitAttribute? secondary, string label)[] order =
            {
                (UnitAttribute.Red, null, "RED"),
                (UnitAttribute.Green, null, "GREEN"),
                (UnitAttribute.Blue, null, "BLUE"),
                (UnitAttribute.Red, UnitAttribute.Blue, "RED/BLUE"),
                (UnitAttribute.Green, null, "GREEN(再)"),
            };

            Dictionary<string, uint> hashes = new Dictionary<string, uint>();

            for (int i = 0; i < order.Length; i++)
            {
                yield return SelectCard(CardWith(order[i].primary, order[i].secondary));

                (uint hash, int opaque) = CaptureRenderTexture();

                Assert.That(
                    opaque, Is.GreaterThan(0),
                    order[i].label + ": RenderTexture の有効画素が0です（何も描かれていません）。");

                hashes[order[i].label] = hash;
            }

            string all = string.Empty;

            foreach (KeyValuePair<string, uint> entry in hashes)
            {
                all += "\n  " + entry.Key + " = " + entry.Value;
            }

            // 4属性が互いに異なること。
            string[] distinct = { "RED", "GREEN", "BLUE", "RED/BLUE" };

            for (int a = 0; a < distinct.Length; a++)
            {
                for (int b = a + 1; b < distinct.Length; b++)
                {
                    if (hashes[distinct[a]] != hashes[distinct[b]])
                    {
                        continue;
                    }

                    // C の切り分け: 手動 Render で変わるなら自動再描画が止まっています。
                    uint before = CaptureRenderTexture().hash;
                    portraitCamera.Render();
                    uint after = CaptureRenderTexture().hash;

                    Assert.Fail(
                        distinct[a] + " と " + distinct[b] +
                        " の RenderTexture が同一です（立ち絵が更新されていません）。" +
                        all +
                        "\n  手動 camera.Render() 前: " + before +
                        "\n  手動 camera.Render() 後: " + after +
                        (before != after
                            ? "\n  → 手動 Render で変化しました。カメラの自動再描画が止まっています（原因C）。"
                            : "\n  → 手動 Render でも変化しません。PropertyBlock かマテリアル側の問題です。") +
                        DescribeBlock());
                }
            }

            // GREEN を選び直したら最初の GREEN と同じ絵へ戻ること。
            Assert.That(
                hashes["GREEN(再)"], Is.EqualTo(hashes["GREEN"]),
                "GREEN を選び直しても最初の GREEN と同じ絵になりません。" + all);

            Assert.That(
                hashes["GREEN"], Is.Not.EqualTo(hashes["RED"]),
                "GREEN 選択後も RED の絵が残っています。" + all);
        }

        // ---------------- テスト3: RawImage ----------------

        [UnityTest]
        public IEnumerator ThePortraitRawImageDisplaysTheCurrentRenderTexture()
        {
            yield return SelectCard(CardWith(UnitAttribute.Red, null));

            RenderTexture first = portraitCamera.targetTexture;

            Assert.That(first, Is.Not.Null, "PortraitCamera.targetTexture が null です。");
            Assert.That(first.IsCreated(), Is.True, "RenderTexture が未作成です。");

            Assert.That(
                portraitImage.texture,
                Is.SameAs(first),
                "Portrait RawImage が別の Texture を参照しています（古い RenderTexture）。");

            Assert.That(portraitImage.enabled, Is.True, "Portrait RawImage が無効です。");

            Assert.That(
                portraitImage.gameObject.activeInHierarchy,
                Is.True, "Portrait RawImage が非アクティブです。");

            Assert.That(
                portraitImage.color.a, Is.GreaterThan(0f), "Portrait RawImage が透明です。");

            foreach (CanvasGroup group in
                portraitImage.GetComponentsInParent<CanvasGroup>(true))
            {
                Assert.That(
                    group.alpha, Is.GreaterThan(0f),
                    "親の CanvasGroup " + group.name + " が alpha 0 です。");
            }

            // 選択を変えても、参照先が差し替わらないこと。
            yield return SelectCard(CardWith(UnitAttribute.Green, null));

            Assert.That(
                portraitCamera.targetTexture,
                Is.SameAs(first),
                "選択後に RenderTexture が別の実体へ差し替わりました。");

            Assert.That(
                portraitImage.texture,
                Is.SameAs(portraitCamera.targetTexture),
                "RawImage の参照とカメラの描画先が食い違っています。");
        }
    }
}
