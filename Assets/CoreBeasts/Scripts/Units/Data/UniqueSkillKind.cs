namespace CoreBeasts.Units
{
    /// <summary>
    /// ユニークスキルの種類（Phase 5）。
    ///
    /// ルールはこの値だけで分岐します。<see cref="CoreBeastDefinition.SkillName"/> などの表示文字列は
    /// 画面に出すためだけのもので、名前を変えてもルールは変わりません。
    /// 値はアセットへ数値で保存されるため、既存の値の番号は変えないでください。
    /// </summary>
    public enum UniqueSkillKind
    {
        /// <summary>スキルなし。</summary>
        None = 0,

        /// <summary>自分の直前の個体が RED を含むなら、自分の全属性POWER +4。</summary>
        CrimsonBite = 1,

        /// <summary>無条件に、そのラウンドだけ相手の全属性POWER -4（下限1）。</summary>
        TidalHowl = 2,

        /// <summary>自分が直前のラウンドで負けていたら、自分の全属性POWER +6。</summary>
        VerdantFang = 3,

        /// <summary>相手が RED か BLUE を含むなら、自分の全属性POWER +3。</summary>
        StormBite = 4,
    }
}
