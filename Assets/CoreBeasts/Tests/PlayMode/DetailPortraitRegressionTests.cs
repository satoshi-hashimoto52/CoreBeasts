using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// UnitSet 上部の詳細立ち絵が、選んだ個体の属性色へ追従することの回帰テスト。
    ///
    /// 実際の UnitSet シーンを Single で読み、カードは本物の pointerClickHandler 経由で選びます。
    /// 詳細立ち絵の経路は一覧カード（BeastThumbnailView）とは別です。
    ///   BeastDetailPanel.Show → CoreBeastView.SetAttribute(s) → Apply
    ///   → SpriteRenderer の MaterialPropertyBlock → PortraitCamera → RenderTexture → RawImage
    ///
    /// <see cref="TracesEveryStageOfThePortraitPipeline"/>は各段の実測値を出力し、
    /// どの段で止まっているかを記録します。残りのテストが仕様を固定します。
    /// </summary>
    public sealed class DetailPortraitRegressionTests
    {
        private const string SceneName = "UnitSet";
        private const int WarmUpFrames = 4;
        private const float ColorTolerance = 0.004f;

        private static readonly string[] MaterialProperties =
        {
            "_Primary_Color", "_Secondary_Color", "_Emission_Color", "_Emission_Strength",
            "_Base_Texture", "_Primary_Mask", "_Secondary_Mask",
        };

        private static int sceneCounter;

        private ISquadRepository originalRepository;

        private UnitSetScreen screen;
        private BeastDetailPanel detailPanel;

        /// <summary>BeastDetailPanel.portraitView が実行時に指している CoreBeastView（null もあり得ます）。</summary>
        private CoreBeastView boundView;

        /// <summary>シーンに実在する詳細立ち絵の CoreBeastView。観測はこちらで行います。</summary>
        private CoreBeastView portraitView;
        private SpriteRenderer portraitRenderer;
        private PortraitRenderTarget renderTarget;
        private Camera portraitCamera;
        private RawImage portraitImage;
        private AttributePalette palette;
        private UiTextCatalog text;

        private readonly List<BeastCardView> cards = new List<BeastCardView>();

        /// <summary>1回の選択で観測したこと。</summary>
        private sealed class Observation
        {
            internal string Label;
            internal string InstanceId;
            internal CoreBeastDefinition Definition;
            internal string SelectedId;
            internal string FramedCardId;
            internal string LevelText;
            internal string AttributeText;

            internal UnitAttribute ViewPrimary;
            internal UnitAttribute ViewSecondary;
            internal bool ViewUsesSecondary;

            internal Color BlockPrimary;
            internal Color BlockSecondary;
            internal Color BlockEmission;
            internal float BlockEmissionStrength;

            internal Color ExpectedPrimary;
            internal Color ExpectedSecondary;
            internal Color ExpectedEmission;
            internal float ExpectedEmissionStrength;

            internal uint Hash;
            internal int OpaquePixels;
            internal Color MeanColour;

            internal bool ManualRenderRan;
            internal uint HashAfterManualRender;

            internal string References;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            yield return LoadUnitSet();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
            originalRepository = null;

            cards.Clear();

            Scene loaded = SceneManager.GetSceneByName(SceneName);
            Scene empty = SceneManager.CreateScene("DetailPortraitRegression_TearDown_" + sceneCounter++);

            SceneManager.SetActiveScene(empty);

            if (loaded.IsValid() && loaded.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loaded);
            }

            yield return null;
        }

        // ---------------- 診断: 各段の実測 ----------------

        [UnityTest]
        public IEnumerator TracesEveryStageOfThePortraitPipeline()
        {
            StringBuilder log = new StringBuilder();

            log.AppendLine("ColorSpace: " + QualitySettings.activeColorSpace);
            log.AppendLine(DescribeStaticReferences());

            (UnitAttribute, UnitAttribute?)[] order =
            {
                (UnitAttribute.Red, null),
                (UnitAttribute.Green, null),
                (UnitAttribute.Blue, null),
                (UnitAttribute.Red, UnitAttribute.Blue),
                (UnitAttribute.Green, null),
            };

            List<Observation> seen = new List<Observation>();

            foreach ((UnitAttribute primary, UnitAttribute? secondary) in order)
            {
                Observation o = null;

                yield return Select(primary, secondary, x => o = x, manualRenderIfUnchanged: seen);

                seen.Add(o);
                log.AppendLine(Describe(o));
            }

            TestContext.WriteLine(log.ToString());

            Assert.That(seen.Count, Is.EqualTo(order.Length));
        }

        // ---------------- 仕様 ----------------

        [UnityTest]
        public IEnumerator SelectingRedGreenAndBlueChangesTheVisiblePortrait()
        {
            Observation red = null;
            Observation green = null;
            Observation blue = null;

            yield return Select(UnitAttribute.Red, null, x => red = x);
            yield return Select(UnitAttribute.Green, null, x => green = x);
            yield return Select(UnitAttribute.Blue, null, x => blue = x);

            string detail = Describe(red) + Describe(green) + Describe(blue);

            Assert.That(red.OpaquePixels, Is.GreaterThan(0), "立ち絵が描かれていません。" + detail);
            Assert.That(green.Hash, Is.Not.EqualTo(red.Hash), "GREEN を選んでも RED のままです。" + detail);
            Assert.That(blue.Hash, Is.Not.EqualTo(red.Hash), "BLUE を選んでも RED のままです。" + detail);
            Assert.That(blue.Hash, Is.Not.EqualTo(green.Hash), "BLUE と GREEN が同じ絵です。" + detail);
        }

        [UnityTest]
        public IEnumerator SelectingTheSameAttributeAgainRestoresTheSamePortrait()
        {
            Observation first = null;
            Observation red = null;
            Observation second = null;

            yield return Select(UnitAttribute.Green, null, x => first = x);
            yield return Select(UnitAttribute.Red, null, x => red = x);
            yield return Select(UnitAttribute.Green, null, x => second = x);

            string detail = Describe(first) + Describe(red) + Describe(second);

            AssertColor(second.BlockPrimary, first.BlockPrimary, "2回の GREEN の _Primary_Color");
            AssertColor(second.BlockSecondary, first.BlockSecondary, "2回の GREEN の _Secondary_Color");
            Assert.That(red.Hash, Is.Not.EqualTo(first.Hash), "RED で絵が変わっていません。" + detail);
            Assert.That(second.Hash, Is.EqualTo(first.Hash), "GREEN へ戻しても同じ絵になりません。" + detail);
        }

        [UnityTest]
        public IEnumerator SelectingADualAttributeUpdatesBothPortraitColours()
        {
            Observation red = null;
            Observation redBlue = null;

            yield return Select(UnitAttribute.Red, null, x => red = x);
            yield return Select(UnitAttribute.Red, UnitAttribute.Blue, x => redBlue = x);

            string detail = Describe(red) + Describe(redBlue);

            Assert.That(redBlue.ViewPrimary, Is.EqualTo(red.ViewPrimary));
            Assert.That(redBlue.ViewUsesSecondary, Is.True, "2属性として扱われていません。" + detail);
            Assert.That(red.ViewUsesSecondary, Is.False);
            Assert.That(redBlue.ViewSecondary, Is.EqualTo(UnitAttribute.Blue));

            AssertColor(redBlue.BlockPrimary, red.BlockPrimary, "一次色は同じ RED");
            Assert.That(
                Distance(redBlue.BlockSecondary, red.BlockSecondary),
                Is.GreaterThan(0.05f),
                "二次色が変わっていません。" + detail);
            Assert.That(redBlue.Hash, Is.Not.EqualTo(red.Hash), "RED/BLUE が RED 単色と同じ絵です。" + detail);
        }

        [UnityTest]
        public IEnumerator TheDetailLabelsAndPortraitAlwaysRepresentTheSameBeast()
        {
            (UnitAttribute, UnitAttribute?)[] order =
            {
                (UnitAttribute.Red, null),
                (UnitAttribute.Green, null),
                (UnitAttribute.Blue, null),
                (UnitAttribute.Red, UnitAttribute.Blue),
                (UnitAttribute.Green, null),
            };

            foreach ((UnitAttribute primary, UnitAttribute? secondary) in order)
            {
                Observation o = null;

                yield return Select(primary, secondary, x => o = x);

                string detail = Describe(o);

                Assert.That(o.SelectedId, Is.EqualTo(o.InstanceId), "選択中の個体" + detail);
                Assert.That(o.FramedCardId, Is.EqualTo(o.InstanceId), "選択枠の個体" + detail);
                Assert.That(o.AttributeText, Is.EqualTo(text.BuildAttributeLabel(o.Definition)), "属性ラベル" + detail);
                Assert.That(o.ViewPrimary, Is.EqualTo(o.Definition.PrimaryAttribute), "立ち絵の一次属性" + detail);
                Assert.That(o.ViewUsesSecondary, Is.EqualTo(o.Definition.HasSecondaryAttribute), "立ち絵の2属性" + detail);

                if (o.Definition.HasSecondaryAttribute)
                {
                    Assert.That(o.ViewSecondary, Is.EqualTo(o.Definition.SecondaryAttribute), "立ち絵の二次属性" + detail);
                }

                AssertColor(o.BlockPrimary, o.ExpectedPrimary, o.Label + " _Primary_Color");
                AssertColor(o.BlockSecondary, o.ExpectedSecondary, o.Label + " _Secondary_Color");
                AssertColor(o.BlockEmission, o.ExpectedEmission, o.Label + " _Emission_Color");
                Assert.That(o.BlockEmissionStrength, Is.EqualTo(o.ExpectedEmissionStrength).Within(ColorTolerance));
            }
        }

        [UnityTest]
        public IEnumerator RepeatedSelectionNeverLeavesTheFirstRedPortrait()
        {
            Dictionary<UnitAttribute, uint> firstHash = new Dictionary<UnitAttribute, uint>();
            UnitAttribute[] cycle = { UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue };

            for (int lap = 0; lap < 10; lap++)
            {
                foreach (UnitAttribute attribute in cycle)
                {
                    Observation o = null;

                    yield return Select(attribute, null, x => o = x);

                    string label = "lap " + (lap + 1) + " " + attribute + Describe(o);

                    if (!firstHash.ContainsKey(attribute))
                    {
                        firstHash[attribute] = o.Hash;
                    }

                    Assert.That(o.Hash, Is.EqualTo(firstHash[attribute]), label + " 前回と同じ属性で絵が違います。");

                    if (attribute != UnitAttribute.Red)
                    {
                        Assert.That(o.Hash, Is.Not.EqualTo(firstHash[UnitAttribute.Red]), label + " が RED のままです。");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator TheVisibleRendererIsTheOneBoundToBeastDetailPanel()
        {
            yield return Select(UnitAttribute.Blue, null, _ => { });

            List<Renderer> visible = RenderersSeenBy(portraitCamera);

            string detail = DescribeStaticReferences();

            Assert.That(boundView, Is.Not.Null, "BeastDetailPanel.portraitView が実行時に null です。" + detail);
            Assert.That(boundView, Is.SameAs(portraitView), "BeastDetailPanel が画面に映っていない CoreBeastView を更新しています。" + detail);

            Assert.That(visible, Does.Contain(portraitRenderer), "PortraitCamera に詳細欄の SpriteRenderer が映っていません。" + detail);

            foreach (Renderer renderer in visible)
            {
                if (renderer == portraitRenderer)
                {
                    continue;
                }

                Assert.That(
                    renderer.GetComponent<CoreBeastView>(),
                    Is.Null,
                    "別の CoreBeastView が PortraitCamera に映っています: " + PathOf(renderer.transform) + detail);

                Assert.That(
                    renderer is SpriteRenderer,
                    Is.False,
                    "別の SpriteRenderer が PortraitCamera に映っています: " + PathOf(renderer.transform) + detail);
            }

            Assert.That(GetField(portraitView, "spriteRenderer"), Is.SameAs(portraitRenderer));
            Assert.That(portraitCamera.targetTexture, Is.Not.Null);
            Assert.That(portraitImage.texture, Is.SameAs(portraitCamera.targetTexture), "RawImage が別の Texture を表示しています。" + detail);
            Assert.That(renderTarget.Texture, Is.SameAs(portraitCamera.targetTexture));
        }

        [UnityTest]
        public IEnumerator ReopeningUnitSetStillUpdatesThePortrait()
        {
            yield return Select(UnitAttribute.Green, null, _ => { });

            // 閉じて開き直します（Single で別シーンへ移ってから、もう一度 UnitSet を読む）。
            Scene other = SceneManager.CreateScene("DetailPortraitRegression_Away_" + sceneCounter++);
            Scene unitSet = SceneManager.GetSceneByName(SceneName);

            SceneManager.SetActiveScene(other);

            yield return SceneManager.UnloadSceneAsync(unitSet);
            yield return null;

            yield return LoadUnitSet();

            Observation red = null;
            Observation green = null;
            Observation blue = null;

            yield return Select(UnitAttribute.Red, null, x => red = x);
            yield return Select(UnitAttribute.Green, null, x => green = x);
            yield return Select(UnitAttribute.Blue, null, x => blue = x);

            string detail = Describe(red) + Describe(green) + Describe(blue);

            Assert.That(green.Hash, Is.Not.EqualTo(red.Hash), "開き直した後、GREEN が RED のままです。" + detail);
            Assert.That(blue.Hash, Is.Not.EqualTo(red.Hash), "開き直した後、BLUE が RED のままです。" + detail);
            Assert.That(blue.Hash, Is.Not.EqualTo(green.Hash), detail);
            Assert.That(portraitImage.texture, Is.SameAs(portraitCamera.targetTexture));
        }

        // ---------------- 準備 ----------------

        private IEnumerator LoadUnitSet()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            yield return EndOfFrame();
            yield return null;

            screen = FindOne<UnitSetScreen>();
            detailPanel = FindOne<BeastDetailPanel>();
            renderTarget = FindOne<PortraitRenderTarget>();

            palette = (AttributePalette)GetField(screen, "palette");
            text = (UiTextCatalog)GetField(screen, "text");

            boundView = (CoreBeastView)GetField(detailPanel, "portraitView");

            // 参照が切れていても各段を観測できるよう、シーンに実在する CoreBeastView を別に控えます。
            portraitView = FindOne<CoreBeastView>();

            portraitRenderer = (SpriteRenderer)GetField(portraitView, "spriteRenderer");
            Assert.That(portraitRenderer, Is.Not.Null, "CoreBeastView.spriteRenderer が未設定です。");

            portraitCamera = (Camera)GetField(renderTarget, "portraitCamera");
            portraitImage = (RawImage)GetField(renderTarget, "targetImage");

            Assert.That(portraitCamera, Is.Not.Null);
            Assert.That(portraitImage, Is.Not.Null);

            cards.Clear();
            cards.AddRange(Find("Content").GetComponentsInChildren<BeastCardView>(true));

            Assert.That(cards.Count, Is.GreaterThan(0), "一覧にカードがありません。");
        }

        /// <summary>
        /// WaitForEndOfFrame まで待ちます。batchmode では WaitForEndOfFrame が使えないため、
        /// 代わりに2フレーム進めてカメラの描画を1回以上通します。
        /// </summary>
        private static IEnumerator EndOfFrame()
        {
            if (Application.isBatchMode)
            {
                yield return null;
                yield return null;
                yield break;
            }

            yield return new WaitForEndOfFrame();
        }

        // ---------------- 選択と観測 ----------------

        private IEnumerator Select(
            UnitAttribute primary,
            UnitAttribute? secondary,
            System.Action<Observation> result,
            List<Observation> manualRenderIfUnchanged = null)
        {
            BeastCardView card = CardWith(primary, secondary);
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = 0,
                position = ScreenCentreOf(card.transform),
            };

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);

            yield return null;

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            Assert.That(card.CurrentState, Is.EqualTo(CardGestureState.Idle), card.name + " がタップとして処理されていません。");

            yield return EndOfFrame();

            Observation o = Observe(card, primary, secondary);

            // 自動描画で絵が変わっていないときだけ、手動で1回描いて比べます（診断用）。
            if (manualRenderIfUnchanged != null)
            {
                Observation previous = manualRenderIfUnchanged.Count > 0
                    ? manualRenderIfUnchanged[manualRenderIfUnchanged.Count - 1]
                    : null;

                bool differentBeastLook =
                    previous != null &&
                    (previous.Definition.PrimaryAttribute != o.Definition.PrimaryAttribute ||
                     previous.Definition.HasSecondaryAttribute != o.Definition.HasSecondaryAttribute);

                if (differentBeastLook && previous.Hash == o.Hash)
                {
                    portraitCamera.Render();

                    o.ManualRenderRan = true;
                    o.HashAfterManualRender = Capture(out _, out _);
                }
            }

            yield return null;

            result(o);
        }

        private Observation Observe(BeastCardView card, UnitAttribute primary, UnitAttribute? secondary)
        {
            CoreBeastDefinition definition = card.Beast.Definition;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            portraitRenderer.GetPropertyBlock(block);

            AttributeColorResolver.Colors expected = AttributeColorResolver.Resolve(palette, definition);

            SquadEditor editor = (SquadEditor)GetField(screen, "editor");

            Observation o = new Observation
            {
                Label = primary + (secondary.HasValue ? "/" + secondary.Value : string.Empty),
                InstanceId = card.Beast.InstanceId,
                Definition = definition,
                SelectedId = editor.Selected != null ? editor.Selected.InstanceId : "(none)",
                FramedCardId = FramedCardId(),
                LevelText = LabelText("levelLabel"),
                AttributeText = LabelText("attributeLabel"),

                ViewPrimary = portraitView.PrimaryAttribute,
                ViewSecondary = portraitView.SecondaryAttribute,
                ViewUsesSecondary = portraitView.UseSecondaryAttribute,

                BlockPrimary = block.GetColor("_Primary_Color"),
                BlockSecondary = block.GetColor("_Secondary_Color"),
                BlockEmission = block.GetColor("_Emission_Color"),
                BlockEmissionStrength = block.GetFloat("_Emission_Strength"),

                ExpectedPrimary = ToRenderColor(expected.Primary),
                ExpectedSecondary = ToRenderColor(expected.Secondary),
                ExpectedEmission = ToRenderColor(expected.Emission),
                ExpectedEmissionStrength = expected.EmissionStrength,

                References = DescribeStaticReferences(),
            };

            o.Hash = Capture(out o.OpaquePixels, out o.MeanColour);

            return o;
        }

        private string FramedCardId()
        {
            string found = "(none)";

            foreach (BeastCardView card in cards)
            {
                Image frame = (Image)GetField(card, "selectionFrame");
                Color selected = (Color)GetField(card, "selectedFrame");
                Color normal = (Color)GetField(card, "normalFrame");

                if (frame != null && frame.color == selected && selected != normal && card.Beast != null)
                {
                    found = card.Beast.InstanceId;
                }
            }

            return found;
        }

        private string LabelText(string field)
        {
            TMP_Text label = (TMP_Text)GetField(detailPanel, field);

            return label != null ? label.text : "(null)";
        }

        /// <summary>RenderTexture を読み、FNV-1a のハッシュ・不透明画素数・不透明画素の平均色を返します。</summary>
        private uint Capture(out int opaque, out Color mean)
        {
            RenderTexture target = portraitCamera.targetTexture;

            Assert.That(target, Is.Not.Null, "PortraitCamera.targetTexture が null です。");

            Texture2D readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                readback.Apply(false);

                Color32[] pixels = readback.GetPixels32();
                Color32 background = pixels[0];

                uint hash = 2166136261u;
                double r = 0;
                double g = 0;
                double b = 0;

                opaque = 0;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];

                    hash = (hash ^ p.r) * 16777619u;
                    hash = (hash ^ p.g) * 16777619u;
                    hash = (hash ^ p.b) * 16777619u;
                    hash = (hash ^ p.a) * 16777619u;

                    // 背景（左下隅と同じ色）以外を立ち絵の画素として数えます。
                    if (p.r != background.r || p.g != background.g || p.b != background.b)
                    {
                        opaque++;
                        r += p.r;
                        g += p.g;
                        b += p.b;
                    }
                }

                mean = opaque > 0
                    ? new Color((float)(r / opaque / 255.0), (float)(g / opaque / 255.0), (float)(b / opaque / 255.0), 1f)
                    : Color.clear;

                return hash;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        // ---------------- 記述 ----------------

        private string DescribeStaticReferences()
        {
            StringBuilder s = new StringBuilder();
            Material material = portraitRenderer.sharedMaterial;

            s.Append("\n  panel.portraitView     : ").Append(boundView == null ? "null" : Id(boundView) + " " + PathOf(boundView.transform));
            s.Append("\n  scene CoreBeastView    : ").Append(Id(portraitView)).Append(' ').Append(PathOf(portraitView.transform));
            s.Append("\n  same view              : ").Append(ReferenceEquals(boundView, portraitView));
#if UNITY_EDITOR
            s.Append("\n  scene view GlobalId    : ").Append(UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(portraitView));
#endif
            s.Append("\n  view.spriteRenderer    : ").Append(Id(portraitRenderer)).Append(' ').Append(PathOf(portraitRenderer.transform));
            s.Append("\n  view.palette           : ").Append(GetField(portraitView, "palette") is Object p && p != null ? p.name : "null");
            s.Append("\n  renderer.enabled       : ").Append(portraitRenderer.enabled);
            s.Append("\n  activeInHierarchy      : ").Append(portraitRenderer.gameObject.activeInHierarchy);
            s.Append("\n  renderer.isVisible     : ").Append(portraitRenderer.isVisible);
            s.Append("\n  sharedMaterial / shader: ").Append(material == null ? "null" : material.name)
                .Append(" / ").Append(material == null || material.shader == null ? "null" : material.shader.name);

            if (material != null)
            {
                s.Append("\n  material.HasProperty   :");

                foreach (string name in MaterialProperties)
                {
                    s.Append(' ').Append(name).Append('=').Append(material.HasProperty(name));
                }
            }

            s.Append("\n  camera                 : ").Append(Id(portraitCamera)).Append(' ').Append(PathOf(portraitCamera.transform))
                .Append(" enabled=").Append(portraitCamera.enabled)
                .Append(" mask=").Append(portraitCamera.cullingMask)
                .Append(" ortho=").Append(portraitCamera.orthographic).Append('/').Append(portraitCamera.orthographicSize);
            s.Append("\n  camera.targetTexture   : ").Append(Id(portraitCamera.targetTexture));
            s.Append("\n  rawImage.texture       : ").Append(Id(portraitImage.texture)).Append(' ').Append(PathOf(portraitImage.transform));
            s.Append("\n  same texture           : ").Append(ReferenceEquals(portraitImage.texture, portraitCamera.targetTexture));

            List<CoreBeastView> views = FindAll<CoreBeastView>();
            s.Append("\n  CoreBeastView in scene : ").Append(views.Count);

            foreach (CoreBeastView view in views)
            {
                s.Append("\n    - ").Append(Id(view)).Append(' ').Append(PathOf(view.transform))
                    .Append(" primary=").Append(view.PrimaryAttribute)
                    .Append(" useSecondary=").Append(view.UseSecondaryAttribute);
            }

            List<Renderer> visible = RenderersSeenBy(portraitCamera);
            s.Append("\n  renderers seen by cam  : ").Append(visible.Count);

            foreach (Renderer renderer in visible)
            {
                CoreBeastView owner = renderer.GetComponent<CoreBeastView>();

                s.Append("\n    - ").Append(renderer.GetType().Name).Append(' ').Append(Id(renderer)).Append(' ').Append(PathOf(renderer.transform))
                    .Append(" coreBeastView=").Append(owner == null ? "none" : Id(owner).ToString())
                    .Append(" sorting=").Append(renderer.sortingLayerName).Append('/').Append(renderer.sortingOrder)
                    .Append(" z=").Append(renderer.transform.position.z.ToString("0.###"));
            }

            return s.ToString();
        }

        private static string Describe(Observation o)
        {
            if (o == null)
            {
                return "\n(no observation)";
            }

            return
                "\n[" + o.Label + "] id=" + o.InstanceId +
                " selected=" + o.SelectedId + " frame=" + o.FramedCardId +
                " level='" + o.LevelText + "' attribute='" + o.AttributeText + "'" +
                "\n  definition             : primary=" + o.Definition.PrimaryAttribute +
                    " secondary=" + o.Definition.SecondaryAttribute +
                    " hasSecondary=" + o.Definition.HasSecondaryAttribute +
                "\n  view                   : primary=" + o.ViewPrimary +
                    " secondary=" + o.ViewSecondary + " useSecondary=" + o.ViewUsesSecondary +
                "\n  block _Primary_Color   : " + o.BlockPrimary + " expected " + o.ExpectedPrimary +
                "\n  block _Secondary_Color : " + o.BlockSecondary + " expected " + o.ExpectedSecondary +
                "\n  block _Emission_Color  : " + o.BlockEmission + " expected " + o.ExpectedEmission +
                "\n  block _Emission_Str.   : " + o.BlockEmissionStrength + " expected " + o.ExpectedEmissionStrength +
                "\n  RenderTexture          : hash=" + o.Hash.ToString("X8") + " pixels=" + o.OpaquePixels + " mean=" + o.MeanColour +
                (o.ManualRenderRan
                    ? "\n  manual camera.Render() : before=" + o.Hash.ToString("X8") + " after=" + o.HashAfterManualRender.ToString("X8")
                    : string.Empty);
        }

        /// <summary>カメラの視野（正射影の箱）とカリングマスクに入っている、有効な Renderer。</summary>
        private static List<Renderer> RenderersSeenBy(Camera camera)
        {
            List<Renderer> seen = new List<Renderer>();

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 origin = camera.transform.position;

            Bounds view = new Bounds(
                new Vector3(origin.x, origin.y, origin.z + (camera.nearClipPlane + camera.farClipPlane) * 0.5f),
                new Vector3(halfWidth * 2f, halfHeight * 2f, camera.farClipPlane - camera.nearClipPlane));

            foreach (Renderer renderer in FindAll<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (renderer.bounds.Intersects(view))
                {
                    seen.Add(renderer);
                }
            }

            return seen;
        }

        // ---------------- 道具 ----------------

        private BeastCardView CardWith(UnitAttribute primary, UnitAttribute? secondary)
        {
            foreach (BeastCardView card in cards)
            {
                OwnedCoreBeast beast = card.Beast;

                if (beast == null || !beast.IsValid)
                {
                    continue;
                }

                CoreBeastDefinition definition = beast.Definition;

                if (definition.PrimaryAttribute != primary)
                {
                    continue;
                }

                if (secondary.HasValue
                        ? definition.HasSecondaryAttribute && definition.SecondaryAttribute == secondary.Value
                        : !definition.HasSecondaryAttribute)
                {
                    return card;
                }
            }

            Assert.Fail("一覧に " + primary + (secondary.HasValue ? "/" + secondary.Value : " 単色") + " の個体がありません。");
            return null;
        }

        private static Vector2 ScreenCentreOf(Transform node)
        {
            RectTransform rect = (RectTransform)node;
            Canvas root = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            return RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        }

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
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance), because + " (r) " + actual + " vs " + expected);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance), because + " (g) " + actual + " vs " + expected);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance), because + " (b) " + actual + " vs " + expected);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(ColorTolerance), because + " (a) " + actual + " vs " + expected);
        }

        private static float Distance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        private static int Id(Object target)
        {
            return target == null ? 0 : target.GetInstanceID();
        }

        private static string PathOf(Transform node)
        {
            if (node == null)
            {
                return "(null)";
            }

            string path = node.name;

            for (Transform parent = node.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return node.gameObject.scene.name + ":" + path;
        }

        private static List<T> FindAll<T>() where T : Component
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

            return found;
        }

        private static T FindOne<T>() where T : Component
        {
            List<T> found = FindAll<T>();

            Assert.That(found.Count, Is.EqualTo(1), typeof(T).Name + " は1個だけ存在する必要があります（実際 " + found.Count + " 個）。");

            return found[0];
        }

        private static Transform Find(string name)
        {
            foreach (Transform t in FindAll<Transform>())
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Assert.Fail(name + " がありません。");
            return null;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }
    }
}
