using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 属性色を、立ち絵のシェーダー（<c>CoreBeastTint.shadergraph</c>）が計算に使う値へ変える唯一の窓口です（Phase 8）。
    ///
    /// パレットの色は sRGB で入力します。プロジェクトはリニア色空間なので、シェーダーはリニアの値で計算します。
    /// 変換はここで1回だけ行います。
    ///   - 詳細表示（<see cref="CoreBeastView"/>）は変換済みの値を <c>MaterialPropertyBlock.SetVector</c> で渡します。
    ///     <c>SetColor</c> は Unity が自動で sRGB → リニアを行うため、変換済みの値を渡すと2回変換されて暗くなります。
    ///   - 事前生成サムネイル（<see cref="CoreBeastThumbnailComposer"/>）も同じ <see cref="ToLinear"/> を使います。
    /// </summary>
    public static class CoreBeastTint
    {
        /// <summary>sRGB の色をリニアへ変えます。アルファは割合なので変えません。</summary>
        public static Color ToLinear(Color srgb)
        {
            Color linear = srgb.linear;
            linear.a = srgb.a;

            return linear;
        }

        /// <summary>
        /// シェーダーへ <c>SetVector</c> で渡す値。リニア色空間では <see cref="ToLinear"/>、
        /// ガンマ色空間では入力のままです（シェーダーがガンマの値で計算するため）。
        /// </summary>
        public static Vector4 ToShaderVector(Color srgb, ColorSpace colorSpace)
        {
            return colorSpace == ColorSpace.Linear ? (Vector4)ToLinear(srgb) : (Vector4)srgb;
        }
    }
}
