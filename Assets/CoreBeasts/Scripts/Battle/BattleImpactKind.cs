namespace CoreBeasts.Battle
{
    /// <summary>
    /// 接触の瞬間に出す決着エフェクトの種類（Phase 2「属性別インパクト」）。
    ///
    /// 決着理由（<see cref="RoundDecision"/>）と勝因の属性から決まります。
    /// 見た目だけの区別で、勝敗・スコア・進行には一切使いません。
    /// </summary>
    public enum BattleImpactKind
    {
        /// <summary>何も出しません。FX OFF と、決着理由が読めないときの安全側です。</summary>
        None = 0,

        /// <summary>RED の属性勝ち。鋭い放射状の斬撃と火花。</summary>
        Red = 1,

        /// <summary>BLUE の属性勝ち。滑らかに広がる円形の衝撃波。</summary>
        Blue = 2,

        /// <summary>GREEN の属性勝ち。螺旋に舞い広がる葉と粒子。</summary>
        Green = 3,

        /// <summary>POWER 比較での決着。属性色を使わない白〜金の圧縮リング。</summary>
        Power = 4,

        /// <summary>CORE 比較での決着。簡素な白金色のコアフラッシュ。</summary>
        Core = 5,

        /// <summary>引き分け。勝者色を出さない、薄い灰色の対称な衝撃波。</summary>
        Draw = 6,
    }
}
