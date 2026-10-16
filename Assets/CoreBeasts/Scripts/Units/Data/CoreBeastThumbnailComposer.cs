using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 6レイヤー素材と属性色から、事前生成サムネイルの画素を作る純粋な計算です（Phase 8）。
    ///
    /// 式は詳細表示のシェーダー（<c>CoreBeastTint.shadergraph</c>）と同じで、リニア色空間で計算します:
    ///   c = base
    ///   c = lerp(c, base × 一次色, 一次マスク.a)
    ///   c = lerp(c, base × 二次色, 二次マスク.a)
    ///   c = lerp(c, 発光色 × 強さ, 発光マスク.a)
    ///   アルファ = base.a
    /// 色は <see cref="AttributeColorResolver"/> の解決結果（sRGB）を <see cref="CoreBeastTint.ToLinear"/> で
    /// リニアへ1回だけ変換して使います（詳細表示の CoreBeastView と同じ関数です）。
    /// 画素は Unity の <c>GetPixels32</c> と同じ並び（左下から行ごと）です。
    /// </summary>
    public static class CoreBeastThumbnailComposer
    {
        /// <summary>式や縮小方法を変えたら上げます。上がると、事前生成済みのサムネイルは作り直しが必要になります。</summary>
        public const int Version = 1;

        /// <summary>
        /// 元画像と同じ大きさで合成します。戻り値はリニアの色（RGB は 1 を超えません）と、元の絵のアルファです。
        /// </summary>
        public static Color[] ComposeLinear(
            Color32[] basePixels,
            Color32[] primaryMask,
            Color32[] secondaryMask,
            Color32[] emissionMask,
            AttributeColorResolver.Colors colors)
        {
            if (basePixels == null || primaryMask == null || secondaryMask == null || emissionMask == null)
            {
                throw new ArgumentNullException(nameof(basePixels));
            }

            int count = basePixels.Length;

            if (primaryMask.Length != count || secondaryMask.Length != count || emissionMask.Length != count)
            {
                throw new ArgumentException("6レイヤーの大きさが揃っていません。");
            }

            // 詳細表示（CoreBeastView）と同じ変換です。
            Color primary = CoreBeastTint.ToLinear(colors.Primary);
            Color secondary = CoreBeastTint.ToLinear(colors.Secondary);
            Color emission = CoreBeastTint.ToLinear(colors.Emission) * colors.EmissionStrength;

            float[] toLinear = SrgbToLinearTable();
            Color[] result = new Color[count];

            for (int i = 0; i < count; i++)
            {
                Color32 b = basePixels[i];
                float br = toLinear[b.r];
                float bg = toLinear[b.g];
                float bb = toLinear[b.b];

                float r = br;
                float g = bg;
                float bl = bb;

                float t = primaryMask[i].a / 255f;
                r += (br * primary.r - r) * t;
                g += (bg * primary.g - g) * t;
                bl += (bb * primary.b - bl) * t;

                t = secondaryMask[i].a / 255f;
                r += (br * secondary.r - r) * t;
                g += (bg * secondary.g - g) * t;
                bl += (bb * secondary.b - bl) * t;

                t = emissionMask[i].a / 255f;
                r += (emission.r - r) * t;
                g += (emission.g - g) * t;
                bl += (emission.b - bl) * t;

                // 画面（sRGB の 8bit）へ出るときと同じく、1 を超える分は切り捨てます。
                result[i] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(bl), b.a / 255f);
            }

            return result;
        }

        /// <summary>
        /// 一辺 <paramref name="size"/> の合成結果を、一辺 <paramref name="target"/> へ縮小して sRGB の 8bit にします。
        /// 縮小はアルファで重み付けした平均（リニア空間）なので、縁に黒いにじみが出ません。
        /// </summary>
        public static Color32[] Downsample(Color[] linear, int size, int target)
        {
            if (linear == null || linear.Length != size * size)
            {
                throw new ArgumentException("大きさが合いません。", nameof(linear));
            }

            if (target <= 0 || size % target != 0)
            {
                throw new ArgumentException("縮小率は整数にしてください。", nameof(target));
            }

            int step = size / target;
            float area = step * step;
            Color32[] result = new Color32[target * target];

            for (int ty = 0; ty < target; ty++)
            {
                for (int tx = 0; tx < target; tx++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;

                    for (int y = 0; y < step; y++)
                    {
                        int row = (ty * step + y) * size + tx * step;

                        for (int x = 0; x < step; x++)
                        {
                            Color c = linear[row + x];
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    result[ty * target + tx] = a > 0f
                        ? new Color32(ToSrgb8(r / a), ToSrgb8(g / a), ToSrgb8(b / a), ToByte(a / area))
                        : new Color32(0, 0, 0, 0);
                }
            }

            return result;
        }

        /// <summary>リニアの 0〜1 を sRGB の 8bit へ。</summary>
        public static byte ToSrgb8(float linear)
        {
            return ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)));
        }

        /// <summary>
        /// 事前生成の入力を表すハッシュ。配色（<paramref name="colors"/>）・素材の識別子・式の版が同じなら同じ値です。
        /// 素材の識別子にはエディタ側で各レイヤーの GUID と更新ハッシュを渡します。
        /// </summary>
        public static string Hash(AttributeColorResolver.Colors colors, string layerIdentity, int thumbnailSize)
        {
            StringBuilder text = new StringBuilder();

            text.Append("v").Append(Version).Append('|').Append(thumbnailSize).Append('|');
            Append(text, colors.Primary);
            Append(text, colors.Secondary);
            Append(text, colors.Emission);
            text.Append(colors.EmissionStrength.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            text.Append(layerIdentity ?? string.Empty);

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                StringBuilder hex = new StringBuilder(digest.Length * 2);

                foreach (byte d in digest)
                {
                    hex.Append(d.ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }

        private static void Append(StringBuilder text, Color c)
        {
            text.Append(c.r.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(c.g.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(c.b.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(c.a.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static float[] SrgbToLinearTable()
        {
            float[] table = new float[256];

            for (int i = 0; i < 256; i++)
            {
                table[i] = Mathf.GammaToLinearSpace(i / 255f);
            }

            return table;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
