using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 敗北表現の共通値。戦闘表示とトレイで同じ灰色を使うため、ここへ集めます。
    ///
    /// 単なる低alphaではなく、属性レイヤーをニュートラルグレーへ置き換える方式です。
    /// 線画と陰影を持つ base レイヤーは色を変えないため、
    /// 暗くなってもシルエットと輪郭は読めます。
    /// </summary>
    public static class DefeatTint
    {
        /// <summary>属性レイヤーを置き換えるニュートラルグレー（#7E8490）。</summary>
        public static readonly Color Neutral = new Color32(0x7E, 0x84, 0x90, 0xFF);

        /// <summary>敗北したキャラクターの不透明度。</summary>
        public const float CharacterAlpha = 0.62f;

        /// <summary>敗北カードへ足す細いグレー枠（#6A707A）。</summary>
        public static readonly Color Frame = new Color32(0x6A, 0x70, 0x7A, 0xFF);
    }
}
