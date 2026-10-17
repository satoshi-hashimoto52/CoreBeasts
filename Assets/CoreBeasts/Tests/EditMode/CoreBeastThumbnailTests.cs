using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 事前生成サムネイル（Phase 8）を確かめます。
    ///
    /// - 合成式は詳細表示のシェーダーと同じ（リニア色空間で lerp、アルファは元の絵）
    /// - 事前生成済みの PNG は、今の素材と配色から作り直した結果と画素単位で一致する（許容誤差 0、sRGB 8bit）
    /// - 入力（配色・素材・式の版）のハッシュが一致し、古いまま残っていない
    /// - 描かれた色は、その定義の属性色にいちばん近い
    /// - 取り込み設定（Sprite、512px、mipmap なし、iOS は ASTC 6x6）
    ///
    /// GPU（実際のシェーダー）との画素比較は PlayMode の <c>ThumbnailComposerGpuTests</c> で行います。
    /// </summary>
    public sealed class CoreBeastThumbnailTests
    {
        private const string PalettePath = "Assets/CoreBeasts/Data/AttributePalette_Default.asset";

        private static IEnumerable<string> DefinitionsWithArt()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CoreBeastDefinition", new[] { "Assets/CoreBeasts" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path).Art != null)
                {
                    yield return path;
                }
            }
        }

        // ---------------- 合成式 ----------------

        [Test]
        public void ComposerFollowsTheShaderFormulaInLinearSpace()
        {
            Color32 grey = new Color32(128, 128, 128, 200);
            Color32 none = new Color32(255, 255, 255, 0);
            Color32 full = new Color32(255, 255, 255, 255);
            Color32 half = new Color32(255, 255, 255, 128);

            Color32[] basePixels = { grey, grey, grey, grey, grey };
            Color32[] primary = { none, full, none, none, half };
            Color32[] secondary = { none, none, full, none, none };
            Color32[] emission = { none, none, none, full, none };

            AttributeColorResolver.Colors colors = new AttributeColorResolver.Colors(
                new Color(1f, 0f, 0f), new Color(0f, 1f, 0f), new Color(0f, 0f, 1f), 0.5f);

            Color[] result = CoreBeastThumbnailComposer.ComposeLinear(basePixels, primary, secondary, emission, colors);
            float g = Mathf.GammaToLinearSpace(128f / 255f);

            AssertLinear(result[0], new Color(g, g, g), "マスクの外は元の絵のまま");
            AssertLinear(result[1], new Color(g, 0f, 0f), "一次マスク: 元の絵 × 一次色");
            AssertLinear(result[2], new Color(0f, g, 0f), "二次マスク: 元の絵 × 二次色");
            AssertLinear(result[3], new Color(0f, 0f, 0.5f), "発光マスク: 発光色 × 強さ（元の絵を掛けません）");
            AssertLinear(result[4], new Color(g, g * (1f - 128f / 255f), g * (1f - 128f / 255f)), "マスクの割合はリニアで混ぜます");

            foreach (Color c in result)
            {
                Assert.That(c.a, Is.EqualTo(200f / 255f).Within(1e-6f), "アルファは元の絵のものです。");
            }
        }

        [Test]
        public void DownsamplingWeightsByAlphaSoEdgesDoNotDarken()
        {
            // 2x2 のうち1画素だけが不透明な赤。縮小しても赤のまま（黒と混ざらない）で、アルファは 1/4。
            Color[] source =
            {
                new Color(1f, 0f, 0f, 1f), new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f),
            };

            Color32[] result = CoreBeastThumbnailComposer.Downsample(source, 2, 1);

            Assert.That(result[0].r, Is.EqualTo(255));
            Assert.That(result[0].g, Is.EqualTo(0));
            Assert.That(result[0].a, Is.EqualTo(64));
        }

        [Test]
        public void TheHashChangesWithEveryInput()
        {
            AttributeColorResolver.Colors a = new AttributeColorResolver.Colors(Color.red, Color.red, Color.red, 1.5f);
            AttributeColorResolver.Colors b = new AttributeColorResolver.Colors(Color.red, Color.blue, Color.red, 1.5f);

            string baseline = CoreBeastThumbnailComposer.Hash(a, "layers", 512);

            Assert.That(CoreBeastThumbnailComposer.Hash(a, "layers", 512), Is.EqualTo(baseline), "同じ入力なら同じ値");
            Assert.That(CoreBeastThumbnailComposer.Hash(b, "layers", 512), Is.Not.EqualTo(baseline), "配色");
            Assert.That(CoreBeastThumbnailComposer.Hash(a, "other", 512), Is.Not.EqualTo(baseline), "素材");
            Assert.That(CoreBeastThumbnailComposer.Hash(a, "layers", 256), Is.Not.EqualTo(baseline), "大きさ");
        }

        // ---------------- 事前生成済みの実アセット ----------------

        [Test]
        public void EveryVolxDefinitionHasABakedThumbnail()
        {
            List<string> paths = new List<string>(DefinitionsWithArt());

            Assert.That(paths.Count, Is.GreaterThanOrEqualTo(4), "VOLX の4定義が素材を持ちます。");

            foreach (string path in paths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path).BakedThumbnail, Is.Not.Null,
                    path + " のサムネイルがありません（CoreBeasts/サムネイルを事前生成 を実行してください）。");
            }
        }

        [TestCaseSource(nameof(DefinitionsWithArt))]
        public void TheBakedThumbnailIsUpToDate(string path)
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path);
            AttributeColorResolver.Colors colors = AttributeColorResolver.Resolve(LoadPalette(), definition);
            string expected = CoreBeastThumbnailComposer.Hash(
                colors, definition.Art.EditorLayerIdentity(), CoreBeastArtSet.ThumbnailSize);

            Assert.That(definition.BakedThumbnailHash, Is.EqualTo(expected),
                path + " のサムネイルが古いままです（素材・配色・式が変わりました）。作り直してください。");
        }

        [TestCaseSource(nameof(DefinitionsWithArt))]
        public void TheBakedPixelsEqualAFreshCompositionExactly(string path)
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path);
            Color32[] fresh = Compose(definition);
            Color32[] baked = ReadPng(AssetDatabase.GetAssetPath(definition.BakedThumbnail), out int width, out int height);

            Assert.That(width, Is.EqualTo(CoreBeastArtSet.ThumbnailSize));
            Assert.That(height, Is.EqualTo(CoreBeastArtSet.ThumbnailSize));

            int different = 0;

            for (int i = 0; i < fresh.Length; i++)
            {
                if (!fresh[i].Equals(baked[i]))
                {
                    different++;
                }
            }

            Assert.That(different, Is.EqualTo(0), path + ": 事前生成済みの画素が、今の素材と配色から作った結果と違います。");
        }

        [TestCaseSource(nameof(DefinitionsWithArt))]
        public void TheBakedColoursAreClosestToTheDefinitionsAttributes(string path)
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path);
            AttributePalette palette = LoadPalette();
            Color32[] baked = ReadPng(AssetDatabase.GetAssetPath(definition.BakedThumbnail), out _, out _);

            Color primaryMean = MeanInMask(baked, definition.Art.PrimaryMask);
            Assert.That(ClosestAttribute(palette, primaryMean, secondary: false), Is.EqualTo(definition.PrimaryAttribute),
                path + ": 一次マスクの範囲の色 " + primaryMean);

            Color secondaryMean = MeanInMask(baked, definition.Art.SecondaryMask);
            UnitAttribute secondaryAttribute = definition.HasSecondaryAttribute ? definition.SecondaryAttribute : definition.PrimaryAttribute;
            Assert.That(ClosestAttribute(palette, secondaryMean, secondary: true), Is.EqualTo(secondaryAttribute),
                path + ": 二次マスクの範囲の色 " + secondaryMean);
        }

        [TestCaseSource(nameof(DefinitionsWithArt))]
        public void TheBakedThumbnailUsesTheMobileImportSettings(string path)
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(definition.BakedThumbnail));
            TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iOS");

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(CoreBeastArtSet.ThumbnailSize));
            Assert.That(ios.overridden, Is.True);
            Assert.That(ios.maxTextureSize, Is.EqualTo(CoreBeastArtSet.ThumbnailSize));
            Assert.That(ios.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                Assert.That(definition.BakedThumbnail.texture.format, Is.EqualTo(TextureFormat.ASTC_6x6), "iOS で取り込んだ形式");
                Assert.That(definition.BakedThumbnail.texture.width, Is.EqualTo(CoreBeastArtSet.ThumbnailSize), "iOS で取り込んだ大きさ");
            }
        }

        // ---------------- 道具 ----------------

        private static AttributePalette LoadPalette()
        {
            AttributePalette palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);

            Assert.That(palette, Is.Not.Null, PalettePath);
            return palette;
        }

        private static Color32[] Compose(CoreBeastDefinition definition)
        {
            CoreBeastArtSet art = definition.Art;
            Color[] linear = CoreBeastThumbnailComposer.ComposeLinear(
                ReadPng(AssetDatabase.GetAssetPath(art.BaseTexture), out _, out _),
                ReadPng(AssetDatabase.GetAssetPath(art.PrimaryMask), out _, out _),
                ReadPng(AssetDatabase.GetAssetPath(art.SecondaryMask), out _, out _),
                ReadPng(AssetDatabase.GetAssetPath(art.EmissionMask), out _, out _),
                AttributeColorResolver.Resolve(LoadPalette(), definition));

            return CoreBeastThumbnailComposer.Downsample(linear, CoreBeastArtSet.SourceSize, CoreBeastArtSet.ThumbnailSize);
        }

        /// <summary>
        /// マスクのアルファが 0.9 以上の範囲（縮小後の画素で、元の 4x4 の平均）にある、不透明な画素の平均色（sRGB）。
        /// </summary>
        private static Color MeanInMask(Color32[] baked, Texture2D mask)
        {
            Color32[] source = ReadPng(AssetDatabase.GetAssetPath(mask), out _, out _);
            int size = CoreBeastArtSet.SourceSize;
            int target = CoreBeastArtSet.ThumbnailSize;
            int step = size / target;
            double r = 0;
            double g = 0;
            double b = 0;
            int count = 0;

            for (int ty = 0; ty < target; ty++)
            {
                for (int tx = 0; tx < target; tx++)
                {
                    int sum = 0;

                    for (int y = 0; y < step; y++)
                    {
                        for (int x = 0; x < step; x++)
                        {
                            sum += source[(ty * step + y) * size + tx * step + x].a;
                        }
                    }

                    Color32 p = baked[ty * target + tx];

                    if (sum >= 0.9f * 255f * step * step && p.a > 200)
                    {
                        r += p.r;
                        g += p.g;
                        b += p.b;
                        count++;
                    }
                }
            }

            Assert.That(count, Is.GreaterThan(100), "マスクの範囲が小さすぎます。");
            return new Color((float)(r / count / 255.0), (float)(g / count / 255.0), (float)(b / count / 255.0));
        }

        /// <summary>色相の向き（明るさを除いた RGB の比）がいちばん近い属性。</summary>
        private static UnitAttribute ClosestAttribute(AttributePalette palette, Color mean, bool secondary)
        {
            UnitAttribute best = UnitAttribute.Red;
            float bestDistance = float.MaxValue;

            foreach (UnitAttribute attribute in new[] { UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue })
            {
                AttributePalette.AttributeColors colors = palette.GetColors(attribute);
                float distance = Distance(Chroma(mean), Chroma(secondary ? colors.SecondaryColor : colors.PrimaryColor));

                if (distance < bestDistance)
                {
                    best = attribute;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Vector3 Chroma(Color c)
        {
            float sum = Mathf.Max(1e-4f, c.r + c.g + c.b);
            return new Vector3(c.r / sum, c.g / sum, c.b / sum);
        }

        private static float Distance(Vector3 a, Vector3 b)
        {
            return (a - b).magnitude;
        }

        private static Color32[] ReadPng(string assetPath, out int width, out int height)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(assetPath)), Is.True, assetPath);
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void AssertLinear(Color actual, Color expected, string because)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-5f), because + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-5f), because + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-5f), because + " (b)");
        }
    }
}
