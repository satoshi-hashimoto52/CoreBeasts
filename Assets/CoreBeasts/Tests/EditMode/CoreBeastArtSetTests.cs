using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 6レイヤー方式の素材規格（Phase 8）を、実アセットで確かめます。
    ///
    /// - 6レイヤーは同じフォルダに規約の名前で揃っている
    /// - 元画像はすべて 2048 x 2048
    /// - 全レイヤーのピクセル位置が一致し、上下左右に 8% 以上の余白がある
    /// - マスクはリニア（sRGB OFF）で、アルファをそのまま使う
    /// - 定義は ID を変えずに素材を参照する
    /// </summary>
    public sealed class CoreBeastArtSetTests
    {
        private const string VolxArtPath = "Assets/CoreBeasts/Data/Art/AS_Volx.asset";

        /// <summary>アルファがこの値を超える画素を「描かれている」とみなします（8bit）。</summary>
        private const byte CoveredAlpha = 8;

        private static readonly string[] VolxDefinitionPaths =
        {
            "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset",
            "Assets/CoreBeasts/Data/Testing/CB_Volx_Green_TEST.asset",
            "Assets/CoreBeasts/Data/Testing/CB_Volx_Blue_TEST.asset",
            "Assets/CoreBeasts/Data/Testing/CB_Volx_RedBlue_TEST.asset",
        };

        private static readonly string[] LayerNames =
        {
            CoreBeastArtSet.LineartName,
            CoreBeastArtSet.ShadingName,
            CoreBeastArtSet.PrimaryMaskName,
            CoreBeastArtSet.SecondaryMaskName,
            CoreBeastArtSet.EmissionMaskName,
            CoreBeastArtSet.PreviewName,
        };

        private static IEnumerable<string> ArtSetPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CoreBeastArtSet", new[] { "Assets/CoreBeasts" }))
            {
                yield return AssetDatabase.GUIDToAssetPath(guid);
            }
        }

        private static CoreBeastArtSet Load(string path)
        {
            CoreBeastArtSet art = AssetDatabase.LoadAssetAtPath<CoreBeastArtSet>(path);

            Assert.That(art, Is.Not.Null, path + " を読めません。");
            return art;
        }

        [Test]
        public void TheProjectHasAtLeastTheVolxArtSet()
        {
            CollectionAssert.Contains(new List<string>(ArtSetPaths()), VolxArtPath);
        }

        [TestCaseSource(nameof(ArtSetPaths))]
        public void EveryArtSetHasAllRuntimeLayersUnderTheirConventionalNames(string path)
        {
            CoreBeastArtSet art = Load(path);

            Assert.That(art.IsComplete, Is.True, path + " に未設定のレイヤーがあります。");
            Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(art.BaseTexture)), Is.EqualTo(CoreBeastArtSet.PreviewName));
            Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(art.PrimaryMask)), Is.EqualTo(CoreBeastArtSet.PrimaryMaskName));
            Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(art.SecondaryMask)), Is.EqualTo(CoreBeastArtSet.SecondaryMaskName));
            Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(art.EmissionMask)), Is.EqualTo(CoreBeastArtSet.EmissionMaskName));
        }

        [TestCaseSource(nameof(ArtSetPaths))]
        public void AllSixLayersSitTogetherAsSquareSourceImages(string path)
        {
            string folder = LayerFolder(Load(path));

            foreach (string name in LayerNames)
            {
                string layer = folder + "/" + name + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(layer) as TextureImporter;

                Assert.That(importer, Is.Not.Null, layer + " がありません（6レイヤーは同じフォルダに置きます）。");

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);

                Assert.That(width, Is.EqualTo(CoreBeastArtSet.SourceSize), layer);
                Assert.That(height, Is.EqualTo(CoreBeastArtSet.SourceSize), layer);
            }
        }

        [TestCaseSource(nameof(ArtSetPaths))]
        public void MasksAreLinearAndKeepTheirAlpha(string path)
        {
            CoreBeastArtSet art = Load(path);

            foreach (Texture2D mask in new[] { art.PrimaryMask, art.SecondaryMask, art.EmissionMask })
            {
                string maskPath = AssetDatabase.GetAssetPath(mask);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);

                Assert.That(importer.sRGBTexture, Is.False, maskPath + " はリニアで読みます（色ではなく割合のため）。");
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), maskPath);
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True, maskPath + " の範囲はアルファです。");
            }
        }

        /// <summary>
        /// 実行時に使う4レイヤーは、iOS では最大 1024・ASTC 6x6 で取り込みます（元 PNG は 2048 のまま）。
        /// エディタの対象プラットフォームが iOS のときは、実際に取り込まれたテクスチャの形式と大きさも確かめます。
        /// </summary>
        [TestCaseSource(nameof(ArtSetPaths))]
        public void RuntimeLayersUseTheIosAstcSettings(string path)
        {
            CoreBeastArtSet art = Load(path);

            foreach (Texture2D layer in new[] { art.BaseTexture, art.PrimaryMask, art.SecondaryMask, art.EmissionMask })
            {
                string layerPath = AssetDatabase.GetAssetPath(layer);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(layerPath);
                TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iOS");

                Assert.That(ios.overridden, Is.True, layerPath);
                Assert.That(ios.maxTextureSize, Is.EqualTo(1024), layerPath);
                Assert.That(ios.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6), layerPath);
                Assert.That(importer.mipmapEnabled, Is.False, layerPath + " は等倍付近でしか描かないため mipmap を持ちません。");

                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
                {
                    Assert.That(layer.format, Is.EqualTo(TextureFormat.ASTC_6x6), layerPath + "（iOS で取り込んだ形式）");
                    Assert.That(layer.width, Is.EqualTo(1024), layerPath + "（iOS で取り込んだ大きさ）");
                }
            }
        }

        [TestCaseSource(nameof(ArtSetPaths))]
        public void LayersArePixelAlignedAndKeepTheMargin(string path)
        {
            string folder = LayerFolder(Load(path));
            byte[] silhouette = ReadAlpha(folder + "/" + CoreBeastArtSet.PreviewName + ".png");
            RectInt bounds = Bounds(silhouette);
            int size = CoreBeastArtSet.SourceSize;
            int margin = Mathf.CeilToInt(size * CoreBeastArtSet.MinMargin);

            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(margin), "左の余白");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(margin), "上の余白");
            Assert.That(size - bounds.xMax, Is.GreaterThanOrEqualTo(margin), "右の余白");
            Assert.That(size - bounds.yMax, Is.GreaterThanOrEqualTo(margin), "下の余白");

            foreach (string name in LayerNames)
            {
                if (name == CoreBeastArtSet.PreviewName)
                {
                    continue;
                }

                byte[] layer = ReadAlpha(folder + "/" + name + ".png");
                int covered = 0;
                int outside = 0;

                for (int i = 0; i < layer.Length; i++)
                {
                    if (layer[i] > CoveredAlpha)
                    {
                        covered++;

                        if (silhouette[i] <= CoveredAlpha)
                        {
                            outside++;
                        }
                    }
                }

                Assert.That(covered, Is.GreaterThan(0), name + " が空です。");
                Assert.That(outside, Is.LessThanOrEqualTo(covered / 1000),
                    name + " が元の絵の外にはみ出しています（位置がずれていませんか）。outside=" + outside + " / " + covered);
            }
        }

        [Test]
        public void TheVolxDefinitionsShareTheVolxArtAndKeepTheirIds()
        {
            CoreBeastArtSet volx = Load(VolxArtPath);
            string[] expectedIds = { "volx", "volx_g_test", "volx_b_test", "volx_rb_test" };

            for (int i = 0; i < VolxDefinitionPaths.Length; i++)
            {
                CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(VolxDefinitionPaths[i]);

                Assert.That(definition, Is.Not.Null, VolxDefinitionPaths[i]);
                Assert.That(definition.Art, Is.SameAs(volx), VolxDefinitionPaths[i]);
                Assert.That(definition.BeastId, Is.EqualTo(expectedIds[i]), "定義の ID は変えません。");
            }
        }

        // ---------------- 道具 ----------------

        private static string LayerFolder(CoreBeastArtSet art)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(art.BaseTexture)).Replace('\\', '/');
        }

        /// <summary>PNG の元画像のアルファ（Unity の圧縮前）。左上が先頭です。</summary>
        private static byte[] ReadAlpha(string assetPath)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(assetPath)), Is.True, assetPath);

                Color32[] pixels = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;
                byte[] alpha = new byte[pixels.Length];

                for (int y = 0; y < height; y++)
                {
                    int source = (height - 1 - y) * width;
                    int target = y * width;

                    for (int x = 0; x < width; x++)
                    {
                        alpha[target + x] = pixels[source + x].a;
                    }
                }

                return alpha;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static RectInt Bounds(byte[] alpha)
        {
            int size = CoreBeastArtSet.SourceSize;
            int minX = size;
            int minY = size;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (alpha[y * size + x] > CoveredAlpha)
                    {
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            Assert.That(maxX, Is.GreaterThanOrEqualTo(0), "元の絵が空です。");
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
