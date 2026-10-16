using System.Collections;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 事前生成サムネイルの合成式（<see cref="CoreBeastThumbnailComposer"/>）が、詳細表示の実際のシェーダー
    /// （M_CoreBeastTint + <see cref="CoreBeastView"/> の MaterialPropertyBlock）と同じ色を出すことを、
    /// GPU の描画結果との画素比較で確かめます（Phase 8）。
    ///
    /// 比較の条件:
    /// - 色空間: プロジェクトは Linear。GPU は詳細表示と同じ sRGB の RenderTexture（2048 x 2048、R8G8B8A8_SRGB）へ描き、
    ///   合成式はリニアで計算してから同じく sRGB の 8bit へ変換します。
    /// - 元画像と同じ 2048 x 2048 で、1テクセルが1画素に重なるよう正射影で描きます（縮小の差を入れないため）。
    /// - 合成式へ渡す画素は、GPU が実際にサンプリングする取り込み後のテクスチャ（エディタの対象プラットフォームの圧縮後）を
    ///   同じ大きさの RenderTexture へ写して読んだものです。圧縮による劣化は式の差ではないため、ここでは比べません
    ///   （圧縮前後の画質は別途比較します）。
    /// - 背景は透明の黒で、通常のアルファ合成（SrcAlpha / OneMinusSrcAlpha）をするため、期待値は「色 × アルファ」です。
    /// - 許容誤差: RGB 各チャンネルで 2/255。8bit 量子化（入力・出力で各 ±0.5）と、GPU とCPUの sRGB 変換の丸めの差のためです。
    ///   アルファは合成で変わるため比べません。
    /// </summary>
    public sealed class ThumbnailComposerGpuTests
    {
        private const string VolxArtPath = "Assets/CoreBeasts/Data/Art/AS_Volx.asset";
        private const string MaterialPath = "Assets/CoreBeasts/Shaders/M_CoreBeastTint.mat";
        private const string PalettePath = "Assets/CoreBeasts/Data/AttributePalette_Default.asset";
        private const int Tolerance = 2;

        private static int sceneCounter;

        private GameObject root;
        private RenderTexture target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Scene scene = SceneManager.CreateScene("ThumbnailComposerGpu_" + sceneCounter++);
            SceneManager.SetActiveScene(scene);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator TheComposerMatchesTheShaderForEachAttribute(
            [Values(UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue)] UnitAttribute primary,
            [Values(false, true)] bool dual)
        {
#if UNITY_EDITOR
            CoreBeastArtSet art = AssetDatabase.LoadAssetAtPath<CoreBeastArtSet>(VolxArtPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            AttributePalette palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);

            Assert.That(art, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(palette, Is.Not.Null);
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear), "比較の前提はリニア色空間です。");

            UnitAttribute secondary = primary == UnitAttribute.Blue ? UnitAttribute.Red : UnitAttribute.Blue;
            int size = CoreBeastArtSet.SourceSize;

            Camera camera = Build(art, material, palette, primary, dual, secondary, size, out CoreBeastView view);

            yield return null;

            camera.Render();
            Color32[] gpu = Read(target);

            AttributeColorResolver.Colors colors = AttributeColorResolver.Resolve(palette, primary, dual, secondary);
            Color[] expected = CoreBeastThumbnailComposer.ComposeLinear(
                ReadTexels(art.BaseTexture, srgb: true),
                ReadTexels(art.PrimaryMask, srgb: false),
                ReadTexels(art.SecondaryMask, srgb: false),
                ReadTexels(art.EmissionMask, srgb: false),
                colors);

            int over = 0;
            int maxDiff = 0;
            long sumDiff = 0;
            int compared = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                Color e = expected[i];

                // 透明の黒の上へ通常のアルファ合成をした結果（リニアで色 × アルファ）を、sRGB の 8bit で比べます。
                byte er = CoreBeastThumbnailComposer.ToSrgb8(e.r * e.a);
                byte eg = CoreBeastThumbnailComposer.ToSrgb8(e.g * e.a);
                byte eb = CoreBeastThumbnailComposer.ToSrgb8(e.b * e.a);
                Color32 g = gpu[i];

                int diff = Mathf.Max(Mathf.Abs(er - g.r), Mathf.Max(Mathf.Abs(eg - g.g), Mathf.Abs(eb - g.b)));

                maxDiff = Mathf.Max(maxDiff, diff);
                sumDiff += diff;
                compared++;

                if (diff > Tolerance)
                {
                    over++;
                }
            }

            string label = primary + (dual ? "/" + secondary : string.Empty);
            string summary = label + ": 許容誤差 " + Tolerance + "/255 を超えた画素 " + over + " / " + compared +
                             "、最大差 " + maxDiff + "/255、平均差 " + (sumDiff / (double)compared).ToString("0.000") + "/255";

            TestContext.WriteLine(summary);
            Assert.That(over, Is.EqualTo(0), summary);
            Assert.That(view.Art, Is.SameAs(art));
#else
            Assert.Fail("エディタ上でのみ実行します（素材を AssetDatabase から読むため）。");
            yield break;
#endif
        }

        /// <summary>
        /// パレットの数値どおりの色が、詳細表示の画面に1回だけ変換されて出ることを確かめます。
        /// 一次マスクだけが乗る画素（一次マスク.a ≥ 250/255、二次・発光 ≤ 5/255、元の絵のアルファ ≥ 250/255）では、
        /// 画面の色 = 元の絵 × 一次色 なので、両者の比（リニア）が一次色のリニア値になります。二次色も同様です。
        /// 許容誤差はリニアで 0.01（8bit の量子化と、マスクの端の混ざりの分）。2回変換していれば大きく外れます
        /// （赤の一次色の R で 0.54 に対して 0.25）。
        /// </summary>
        [UnityTest]
        public IEnumerator TheDetailShaderShowsThePaletteColourConvertedOnce(
            [Values(UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue)] UnitAttribute primary)
        {
#if UNITY_EDITOR
            CoreBeastArtSet art = AssetDatabase.LoadAssetAtPath<CoreBeastArtSet>(VolxArtPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            AttributePalette palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);
            UnitAttribute secondary = primary == UnitAttribute.Blue ? UnitAttribute.Red : UnitAttribute.Blue;
            int size = CoreBeastArtSet.SourceSize;

            Camera camera = Build(art, material, palette, primary, true, secondary, size, out _);

            yield return null;

            camera.Render();
            Color32[] gpu = Read(target);
            Color32[] basePixels = ReadTexels(art.BaseTexture, srgb: true);
            Color32[] primaryMask = ReadTexels(art.PrimaryMask, srgb: false);
            Color32[] secondaryMask = ReadTexels(art.SecondaryMask, srgb: false);
            Color32[] emissionMask = ReadTexels(art.EmissionMask, srgb: false);

            Color expectedPrimary = CoreBeastTint.ToLinear(palette.GetColors(primary).PrimaryColor);
            Color expectedSecondary = CoreBeastTint.ToLinear(palette.GetColors(secondary).SecondaryColor);

            Color shownPrimary = EffectiveTint(gpu, basePixels, primaryMask, new[] { secondaryMask, emissionMask });
            Color shownSecondary = EffectiveTint(gpu, basePixels, secondaryMask, new[] { primaryMask, emissionMask });

            TestContext.WriteLine(primary + ": 一次色 パレット(リニア)=" + expectedPrimary + " 画面=" + shownPrimary +
                                  " / 二次色(" + secondary + ") パレット(リニア)=" + expectedSecondary + " 画面=" + shownSecondary);

            AssertTint(shownPrimary, expectedPrimary, primary + " の一次色");
            AssertTint(shownSecondary, expectedSecondary, secondary + " の二次色");
#else
            Assert.Fail("エディタ上でのみ実行します（素材を AssetDatabase から読むため）。");
            yield break;
#endif
        }

        /// <summary>
        /// <paramref name="mask"/> だけが全面に乗る画素で、画面の色 ÷ 元の絵の色（どちらもリニア）の平均。
        /// 暗すぎて比が不安定な画素（元の絵のリニア値が 0.05 未満）は使いません。
        /// </summary>
        private static Color EffectiveTint(Color32[] gpu, Color32[] basePixels, Color32[] mask, Color32[][] others)
        {
            double r = 0;
            double g = 0;
            double b = 0;
            int count = 0;

            for (int i = 0; i < gpu.Length; i++)
            {
                if (basePixels[i].a < 250 || mask[i].a < 250)
                {
                    continue;
                }

                bool clean = true;

                foreach (Color32[] other in others)
                {
                    clean &= other[i].a <= 5;
                }

                Color baseLinear = ((Color)basePixels[i]).linear;

                if (!clean || baseLinear.r < 0.05f || baseLinear.g < 0.05f || baseLinear.b < 0.05f)
                {
                    continue;
                }

                // 透明の黒の上へのアルファ合成なので、画面の色は「色 × アルファ」です。アルファで割り戻します。
                Color shown = ((Color)gpu[i]).linear;
                float alpha = basePixels[i].a / 255f;

                r += shown.r / (baseLinear.r * alpha);
                g += shown.g / (baseLinear.g * alpha);
                b += shown.b / (baseLinear.b * alpha);
                count++;
            }

            Assert.That(count, Is.GreaterThan(500), "比べられる画素が少なすぎます。");
            return new Color((float)(r / count), (float)(g / count), (float)(b / count), 1f);
        }

        private static void AssertTint(Color shown, Color expected, string label)
        {
            Assert.That(shown.r, Is.EqualTo(expected.r).Within(0.01f), label + " (R)");
            Assert.That(shown.g, Is.EqualTo(expected.g).Within(0.01f), label + " (G)");
            Assert.That(shown.b, Is.EqualTo(expected.b).Within(0.01f), label + " (B)");
        }

        private Camera Build(
            CoreBeastArtSet art,
            Material material,
            AttributePalette palette,
            UnitAttribute primary,
            bool dual,
            UnitAttribute secondary,
            int size,
            out CoreBeastView view)
        {
            root = new GameObject("ThumbnailComposerGpu");

            GameObject beast = new GameObject("Beast");
            beast.transform.SetParent(root.transform, false);

            SpriteRenderer renderer = beast.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sprite = art.Preview;

            beast.SetActive(false);
            view = beast.AddComponent<CoreBeastView>();
            SetField(view, "spriteRenderer", renderer);
            SetField(view, "palette", palette);
            beast.SetActive(true);

            view.SetArt(art);

            if (dual)
            {
                view.SetAttributes(primary, secondary);
            }
            else
            {
                view.SetAttribute(primary);
            }

            // Sprite の矩形（テクスチャ全体）の中心へカメラを向け、1テクセル = 1画素にします。
            Sprite sprite = art.Preview;
            float ppu = sprite.pixelsPerUnit;
            Vector2 centre = new Vector2(
                (sprite.rect.width * 0.5f - sprite.pivot.x) / ppu,
                (sprite.rect.height * 0.5f - sprite.pivot.y) / ppu);

            // 詳細表示と同じ作り方（R8G8B8A8_SRGB・デプス付き）。URP 2D はデプス無しの描画先を受け付けません。
            target = PortraitRenderTarget.CreatePortraitTexture(size, size);
            Assert.That(target, Is.Not.Null);

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(centre.x, centre.y, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = sprite.rect.height * 0.5f / ppu;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = target;
            camera.enabled = false;

            Assert.That(sprite.rect.width, Is.EqualTo(size));
            Assert.That(sprite.rect.height, Is.EqualTo(size));

            return camera;
        }

        private static Color32[] Read(RenderTexture texture)
        {
            Texture2D readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readback.Apply(false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        /// <summary>
        /// GPU がサンプリングするテクセル（取り込み後）を 8bit で読みます。sRGB のテクスチャは sRGB の RenderTexture へ、
        /// リニアのテクスチャはリニアの RenderTexture へ写すので、保存されている値がそのまま戻ります。
        /// </summary>
        private static Color32[] ReadTexels(Texture2D texture, bool srgb)
        {
            RenderTexture copy = new RenderTexture(new RenderTextureDescriptor(texture.width, texture.height, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = srgb,
                msaaSamples = 1,
                useMipMap = false,
            });

            try
            {
                copy.Create();
                Graphics.Blit(texture, copy);
                return Read(copy);
            }
            finally
            {
                copy.Release();
                Object.Destroy(copy);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");
            field.SetValue(target, value);
        }
    }
}
