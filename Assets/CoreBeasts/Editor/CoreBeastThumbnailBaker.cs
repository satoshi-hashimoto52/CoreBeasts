using System.Collections.Generic;
using System.IO;
using System.Text;

using CoreBeasts.Units;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.EditorTools
{
    /// <summary>
    /// 一覧・編成枠・バトル・獲得画面のサムネイル（512px）を、6レイヤー素材と属性色から事前生成します（Phase 8）。
    ///
    /// 計算は <see cref="CoreBeastThumbnailComposer"/>（詳細表示のシェーダーと同じ式）に任せ、
    /// ここでは PNG の書き出し・取り込み設定・定義への割り当てだけを行います。
    /// 出力先は素材のフォルダの <c>Thumbnails/</c>、名前は <c>&lt;定義の名前&gt;_thumb.png</c> です。
    ///
    /// コマンドラインからは <c>-executeMethod CoreBeasts.EditorTools.CoreBeastThumbnailBaker.BakeFromCommandLine</c> で実行できます。
    /// </summary>
    public static class CoreBeastThumbnailBaker
    {
        public const string PalettePath = "Assets/CoreBeasts/Data/AttributePalette_Default.asset";
        private const string Menu = "CoreBeasts/サムネイルを事前生成";

        [MenuItem(Menu)]
        public static void BakeFromMenu()
        {
            Debug.Log(Bake(force: false));
        }

        public static void BakeFromCommandLine()
        {
            string report = Bake(force: false);

            Debug.Log(report);
            EditorApplication.Exit(report.Contains("ERROR") ? 1 : 0);
        }

        /// <summary>素材を持つすべての定義のサムネイルを作ります。入力が変わっていなければ作り直しません。</summary>
        public static string Bake(bool force)
        {
            AttributePalette palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);
            StringBuilder report = new StringBuilder("[CoreBeastThumbnailBaker]\n");

            if (palette == null)
            {
                return report.Append("ERROR: ").Append(PalettePath).Append(" を読めません。").ToString();
            }

            Dictionary<CoreBeastArtSet, Color32[][]> layerCache = new Dictionary<CoreBeastArtSet, Color32[][]>();

            foreach (string guid in AssetDatabase.FindAssets("t:CoreBeastDefinition", new[] { "Assets/CoreBeasts" }))
            {
                string definitionPath = AssetDatabase.GUIDToAssetPath(guid);
                CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(definitionPath);

                if (definition == null || definition.Art == null)
                {
                    continue;
                }

                if (!definition.Art.IsComplete)
                {
                    report.Append("ERROR: ").Append(definitionPath).Append(" の素材に未設定のレイヤーがあります。\n");
                    continue;
                }

                AttributeColorResolver.Colors colors = AttributeColorResolver.Resolve(palette, definition);
                string hash = ExpectedHash(definition, colors);

                if (!force && definition.BakedThumbnail != null && definition.BakedThumbnailHash == hash)
                {
                    report.Append("up to date: ").Append(definitionPath).Append('\n');
                    continue;
                }

                if (!layerCache.TryGetValue(definition.Art, out Color32[][] layers))
                {
                    layers = ReadLayers(definition.Art);
                    layerCache[definition.Art] = layers;
                }

                Color[] composed = CoreBeastThumbnailComposer.ComposeLinear(layers[0], layers[1], layers[2], layers[3], colors);
                Color32[] thumbnail = CoreBeastThumbnailComposer.Downsample(
                    composed, CoreBeastArtSet.SourceSize, CoreBeastArtSet.ThumbnailSize);

                string outputPath = OutputPath(definition);
                WritePng(outputPath, thumbnail, CoreBeastArtSet.ThumbnailSize);
                ConfigureImporter(outputPath);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);

                if (sprite == null)
                {
                    report.Append("ERROR: ").Append(outputPath).Append(" を Sprite として読めません。\n");
                    continue;
                }

                SerializedObject serialized = new SerializedObject(definition);
                serialized.FindProperty("bakedThumbnail").objectReferenceValue = sprite;
                serialized.FindProperty("bakedThumbnailHash").stringValue = hash;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);

                report.Append("baked: ").Append(definitionPath).Append(" -> ").Append(outputPath).Append('\n');
            }

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>定義の今の入力（配色・素材・式の版）から求めたハッシュ。</summary>
        public static string ExpectedHash(CoreBeastDefinition definition, AttributeColorResolver.Colors colors)
        {
            return CoreBeastThumbnailComposer.Hash(colors, definition.Art.EditorLayerIdentity(), CoreBeastArtSet.ThumbnailSize);
        }

        public static string OutputPath(CoreBeastDefinition definition)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition.Art.BaseTexture)).Replace('\\', '/');

            return folder + "/Thumbnails/" + definition.name + "_thumb.png";
        }

        /// <summary>元の PNG（圧縮前）を、元の絵・一次・二次・発光の順に読みます。</summary>
        private static Color32[][] ReadLayers(CoreBeastArtSet art)
        {
            return new[]
            {
                ReadPixels(art.BaseTexture),
                ReadPixels(art.PrimaryMask),
                ReadPixels(art.SecondaryMask),
                ReadPixels(art.EmissionMask),
            };
        }

        private static Color32[] ReadPixels(Texture2D layer)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(layer)));
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void WritePng(string path, Color32[] pixels, int size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// サムネイルの取り込み設定。表示は最大でも約 350px なので 512 のまま、iOS は ASTC 6x6 です。
        /// </summary>
        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = CoreBeastArtSet.ThumbnailSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iOS",
                overridden = true,
                maxTextureSize = CoreBeastArtSet.ThumbnailSize,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 50,
            });

            importer.SaveAndReimport();
        }
    }
}
