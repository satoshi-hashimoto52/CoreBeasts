namespace CoreBeasts.Units
{
    /// <summary>
    /// コアビーストの属性。
    /// 三すくみは Red → Green → Blue → Red の順に勝ちます。
    /// 勝敗判定そのものは CoreBeasts.Battle の BattleRules が持ちます。
    /// </summary>
    public enum UnitAttribute
    {
        Red = 0,
        Green = 1,
        Blue = 2,
    }
}
