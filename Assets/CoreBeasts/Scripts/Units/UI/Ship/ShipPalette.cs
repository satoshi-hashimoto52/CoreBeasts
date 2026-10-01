using UnityEngine;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 高次元航行宇宙船の内装で使う色。3画面で同じ船に見せるため、ここだけに置きます。
    ///
    /// 属性色（赤・緑・青）と結果色はここに含めません。
    /// あれは「ユニットと勝敗を見分けるための色」で、内装へ使うと意味が薄れます。
    /// 背景は黒紺・グラファイトと、冷たいシアン／青紫だけで組み立てます。
    /// </summary>
    public static class ShipPalette
    {
        /// <summary>いちばん奥の虚空。</summary>
        public static readonly Color BaseVoidTop = new Color32(0x08, 0x0B, 0x16, 0xFF);

        public static readonly Color BaseVoidBottom = new Color32(0x05, 0x07, 0x10, 0xFF);

        /// <summary>奥の船体。</summary>
        public static readonly Color DeepHull = new Color32(0x0D, 0x13, 0x20, 0xFF);

        public static readonly Color DeepHullLit = new Color32(0x15, 0x1C, 0x2A, 0xFF);

        /// <summary>手前の装甲板。</summary>
        public static readonly Color RaisedHull = new Color32(0x1A, 0x24, 0x33, 0xFF);

        public static readonly Color RaisedHullLit = new Color32(0x26, 0x32, 0x47, 0xFF);

        /// <summary>継ぎ目・構造線。</summary>
        public static readonly Color StructuralLine = new Color32(0x30, 0x40, 0x56, 0xFF);

        public static readonly Color StructuralLineLit = new Color32(0x42, 0x54, 0x6B, 0xFF);

        /// <summary>稼働部の冷たいシアン。</summary>
        public static readonly Color ColdCyan = new Color32(0x28, 0xC7, 0xE8, 0xFF);

        public static readonly Color ColdCyanBright = new Color32(0x5A, 0xDC, 0xF4, 0xFF);

        /// <summary>高次元構造の青紫。</summary>
        public static readonly Color BlueViolet = new Color32(0x65, 0x74, 0xE8, 0xFF);

        public static readonly Color BlueVioletBright = new Color32(0x8B, 0x72, 0xF2, 0xFF);

        /// <summary>透明。</summary>
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>UIの背後へ敷くときの、文字を邪魔しない濃さ。</summary>
        public const float BehindTextAlpha = 0.18f;

        /// <summary>alphaだけ差し替えます。</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
