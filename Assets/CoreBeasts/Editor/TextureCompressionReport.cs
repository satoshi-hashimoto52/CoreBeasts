using System.Collections.Generic;
using System.IO;
using System.Text;

using CoreBeasts.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace CoreBeasts.EditorTools
{
    /// <summary>
    /// 立ち絵の6レイヤーを iOS 向けに圧縮したときの画質とメモリを比べる診断です（Phase 8）。
    ///
    /// 元 PNG（2048）を、取り込み設定の最大サイズ（1024）へ縮小したものを基準にし、
    /// ASTC 6x6 と ASTC 4x4 で実際に圧縮・展開した画素と比べます（展開は GPU に任せます）。
    /// さらに、各定義の属性色で合成した最終的な色（<see cref="CoreBeastThumbnailComposer"/>）への影響も測ります。
    /// 比べるのはマスクではアルファ（シェーダーが使う値）、元の絵では RGB とアルファです。
    ///
    /// この診断は何も書き換えません。結果は Console と、指定したフォルダ（コマンドラインでは環境変数 CB_REPORT_DIR）へ出します。
    /// </summary>
    public static class TextureCompressionReport
    {
        private const string Menu = "CoreBeasts/診断/立ち絵の圧縮の画質を比べる";
        private const int TargetSize = 1024;

        private static readonly TextureFormat[] Formats = { TextureFormat.ASTC_6x6, TextureFormat.ASTC_4x4 };

        [MenuItem(Menu)]
        public static void RunFromMenu()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CoreBeastsTextureReport");
            Debug.Log(Run(folder));
        }

        public static void RunFromCommandLine()
        {
            string folder = System.Environment.GetEnvironmentVariable("CB_REPORT_DIR");

            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(Path.GetTempPath(), "CoreBeastsTextureReport");
            }

            Debug.Log(Run(folder));
            EditorApplication.Exit(0);
        }

        public static string Run(string folder)
        {
            Directory.CreateDirectory(folder);

            StringBuilder report = new StringBuilder("[TextureCompressionReport]\n");
            AttributePalette palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(CoreBeastThumbnailBaker.PalettePath);

            foreach (string guid in AssetDatabase.FindAssets("t:CoreBeastArtSet", new[] { "Assets/CoreBeasts" }))
            {
                CoreBeastArtSet art = AssetDatabase.LoadAssetAtPath<CoreBeastArtSet>(AssetDatabase.GUIDToAssetPath(guid));

                report.Append("== ").Append(art.name).Append('\n');

                Layer[] layers =
                {
                    new Layer("06_preview_neutral", art.BaseTexture, true),
                    new Layer("03_mask_primary", art.PrimaryMask, false),
                    new Layer("04_mask_secondary", art.SecondaryMask, false),
                    new Layer("05_mask_emission", art.EmissionMask, false),
                };

                foreach (Layer layer in layers)
                {
                    layer.Reference = Downscale(ReadPng(layer.Texture), CoreBeastArtSet.SourceSize, TargetSize, layer.Srgb);

                    foreach (TextureFormat format in Formats)
                    {
                        layer.Decoded[format] = CompressAndDecode(layer.Reference, layer.Srgb, format, out long bytes);
                        layer.Bytes[format] = bytes;

                        Metrics m = Compare(layer.Reference, layer.Decoded[format], layer.Srgb);

                        report.Append(layer.Name).Append(' ').Append(format)
                            .Append(": memory ").Append((AstcBytes(format, TargetSize) / 1024f / 1024f).ToString("0.000")).Append(" MiB")
                            .Append(" (Profiler ").Append((bytes / 1024f / 1024f).ToString("0.000")).Append(" MiB、エディタでは CPU 側の複製を含みます)")
                            .Append(" | ").Append(m).Append('\n');
                    }

                    report.Append(layer.Name).Append(" 参考: 非圧縮 RGBA32 2048: memory ")
                        .Append((CoreBeastArtSet.SourceSize * CoreBeastArtSet.SourceSize * 4 / 1024f / 1024f).ToString("0.000")).Append(" MiB\n");
                }

                foreach (string definitionGuid in AssetDatabase.FindAssets("t:CoreBeastDefinition", new[] { "Assets/CoreBeasts" }))
                {
                    CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(AssetDatabase.GUIDToAssetPath(definitionGuid));

                    if (definition == null || definition.Art != art)
                    {
                        continue;
                    }

                    AttributeColorResolver.Colors colors = AttributeColorResolver.Resolve(palette, definition);
                    Color32[] reference = Encode(CoreBeastThumbnailComposer.ComposeLinear(
                        layers[0].Reference, layers[1].Reference, layers[2].Reference, layers[3].Reference, colors));

                    foreach (TextureFormat format in Formats)
                    {
                        Color32[] compressed = Encode(CoreBeastThumbnailComposer.ComposeLinear(
                            layers[0].Decoded[format], layers[1].Decoded[format], layers[2].Decoded[format], layers[3].Decoded[format], colors));

                        report.Append("composite ").Append(definition.name).Append(' ').Append(format).Append(": ")
                            .Append(Compare(reference, compressed, true)).Append('\n');

                        WriteCrop(Path.Combine(folder, definition.name + "_" + format + "_crop.png"), reference, compressed);
                    }
                }
            }

            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            return report.ToString();
        }

        private sealed class Layer
        {
            internal readonly string Name;
            internal readonly Texture2D Texture;
            internal readonly bool Srgb;
            internal Color32[] Reference;
            internal readonly Dictionary<TextureFormat, Color32[]> Decoded = new Dictionary<TextureFormat, Color32[]>();
            internal readonly Dictionary<TextureFormat, long> Bytes = new Dictionary<TextureFormat, long>();

            internal Layer(string name, Texture2D texture, bool srgb)
            {
                Name = name;
                Texture = texture;
                Srgb = srgb;
            }
        }

        private readonly struct Metrics
        {
            internal Metrics(double psnrColour, double psnrAlpha, int maxColour, int maxAlpha, double meanColour, double meanAlpha, double over8)
            {
                PsnrColour = psnrColour;
                PsnrAlpha = psnrAlpha;
                MaxColour = maxColour;
                MaxAlpha = maxAlpha;
                MeanColour = meanColour;
                MeanAlpha = meanAlpha;
                Over8 = over8;
            }

            internal double PsnrColour { get; }
            internal double PsnrAlpha { get; }
            internal int MaxColour { get; }
            internal int MaxAlpha { get; }
            internal double MeanColour { get; }
            internal double MeanAlpha { get; }
            internal double Over8 { get; }

            public override string ToString()
            {
                return "RGB PSNR " + PsnrColour.ToString("0.0") + " dB max " + MaxColour + " mean " + MeanColour.ToString("0.00") +
                       " | A PSNR " + PsnrAlpha.ToString("0.0") + " dB max " + MaxAlpha + " mean " + MeanAlpha.ToString("0.00") +
                       " | >8/255 " + (Over8 * 100).ToString("0.00") + "%";
            }
        }

        /// <summary>RGB は不透明な画素（基準のアルファ ≥ 128）だけ、アルファは全画素で比べます。</summary>
        private static Metrics Compare(Color32[] reference, Color32[] actual, bool hasColour)
        {
            double colourSq = 0;
            double alphaSq = 0;
            double colourSum = 0;
            double alphaSum = 0;
            int colourCount = 0;
            int maxColour = 0;
            int maxAlpha = 0;
            int over = 0;

            for (int i = 0; i < reference.Length; i++)
            {
                Color32 a = reference[i];
                Color32 b = actual[i];
                int da = Mathf.Abs(a.a - b.a);

                alphaSq += da * da;
                alphaSum += da;
                maxAlpha = Mathf.Max(maxAlpha, da);

                int worst = da;

                if (hasColour && a.a >= 128)
                {
                    int dr = Mathf.Abs(a.r - b.r);
                    int dg = Mathf.Abs(a.g - b.g);
                    int db = Mathf.Abs(a.b - b.b);

                    colourSq += dr * dr + dg * dg + db * db;
                    colourSum += dr + dg + db;
                    colourCount += 3;
                    maxColour = Mathf.Max(maxColour, Mathf.Max(dr, Mathf.Max(dg, db)));
                    worst = Mathf.Max(worst, Mathf.Max(dr, Mathf.Max(dg, db)));
                }

                if (worst > 8)
                {
                    over++;
                }
            }

            return new Metrics(
                colourCount > 0 ? Psnr(colourSq / colourCount) : double.PositiveInfinity,
                Psnr(alphaSq / reference.Length),
                maxColour,
                maxAlpha,
                colourCount > 0 ? colourSum / colourCount : 0,
                alphaSum / reference.Length,
                over / (double)reference.Length);
        }

        private static double Psnr(double mse)
        {
            return mse <= 0 ? double.PositiveInfinity : 10.0 * System.Math.Log10(255.0 * 255.0 / mse);
        }

        private static Color32[] ReadPng(Texture2D layer)
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

        /// <summary>整数倍の縮小（平均）。sRGB の色はリニアで平均します。アルファはそのまま平均します。</summary>
        private static Color32[] Downscale(Color32[] source, int size, int target, bool srgb)
        {
            int step = size / target;
            Color32[] result = new Color32[target * target];

            for (int ty = 0; ty < target; ty++)
            {
                for (int tx = 0; tx < target; tx++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int y = 0; y < step; y++)
                    {
                        for (int x = 0; x < step; x++)
                        {
                            Color32 p = source[(ty * step + y) * size + tx * step + x];
                            r += srgb ? Mathf.GammaToLinearSpace(p.r / 255f) : p.r / 255f;
                            g += srgb ? Mathf.GammaToLinearSpace(p.g / 255f) : p.g / 255f;
                            b += srgb ? Mathf.GammaToLinearSpace(p.b / 255f) : p.b / 255f;
                            a += p.a / 255f;
                        }
                    }

                    float n = step * step;
                    result[ty * target + tx] = srgb
                        ? new Color32(CoreBeastThumbnailComposer.ToSrgb8(r / n), CoreBeastThumbnailComposer.ToSrgb8(g / n), CoreBeastThumbnailComposer.ToSrgb8(b / n), ToByte(a / n))
                        : new Color32(ToByte(r / n), ToByte(g / n), ToByte(b / n), ToByte(a / n));
                }
            }

            return result;
        }

        /// <summary>エディタの ASTC エンコーダーで圧縮し、GPU で展開した画素を読みます。</summary>
        private static Color32[] CompressAndDecode(Color32[] pixels, bool srgb, TextureFormat format, out long bytes)
        {
            Texture2D texture = new Texture2D(TargetSize, TargetSize, TextureFormat.RGBA32, false, !srgb);

            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Normal);

                bytes = Profiler.GetRuntimeMemorySizeLong(texture);

                RenderTexture rt = new RenderTexture(new RenderTextureDescriptor(TargetSize, TargetSize, RenderTextureFormat.ARGB32, 0)
                {
                    sRGB = srgb,
                    msaaSamples = 1,
                    useMipMap = false,
                });
                Texture2D readback = new Texture2D(TargetSize, TargetSize, TextureFormat.RGBA32, false, !srgb);
                RenderTexture previous = RenderTexture.active;

                try
                {
                    rt.Create();
                    Graphics.Blit(texture, rt);
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0f, 0f, TargetSize, TargetSize), 0, 0);
                    readback.Apply(false);
                    return readback.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(readback);
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>合成した色（リニア）を、濃紺の背景へ重ねた sRGB の 8bit にします（画面での見え方）。</summary>
        private static Color32[] Encode(Color[] linear)
        {
            Color background = new Color(0.03f, 0.05f, 0.10f);
            Color32[] result = new Color32[linear.Length];

            for (int i = 0; i < linear.Length; i++)
            {
                Color c = linear[i];
                result[i] = new Color32(
                    CoreBeastThumbnailComposer.ToSrgb8(c.r * c.a + background.r * (1f - c.a)),
                    CoreBeastThumbnailComposer.ToSrgb8(c.g * c.a + background.g * (1f - c.a)),
                    CoreBeastThumbnailComposer.ToSrgb8(c.b * c.a + background.b * (1f - c.a)),
                    255);
            }

            return result;
        }

        /// <summary>顔と胸のあたり（256 x 256）を、基準と圧縮後で左右に並べて2倍に拡大した PNG。</summary>
        private static void WriteCrop(string path, Color32[] reference, Color32[] compressed)
        {
            const int crop = 256;
            const int zoom = 2;
            int x0 = TargetSize * 22 / 100;
            int y0 = TargetSize * 52 / 100;
            int width = crop * zoom * 2 + 8;
            int height = crop * zoom;
            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < crop * zoom; x++)
                {
                    int source = (y0 + y / zoom) * TargetSize + x0 + x / zoom;
                    pixels[y * width + x] = reference[source];
                    pixels[y * width + x + crop * zoom + 8] = compressed[source];
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

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
        }

        /// <summary>ASTC の大きさ（1ブロック 16 バイト、mipmap なし）。</summary>
        public static long AstcBytes(TextureFormat format, int size)
        {
            int block = format == TextureFormat.ASTC_4x4 ? 4 : format == TextureFormat.ASTC_6x6 ? 6 : 8;
            long blocks = (size + block - 1) / block;

            return blocks * blocks * 16;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
