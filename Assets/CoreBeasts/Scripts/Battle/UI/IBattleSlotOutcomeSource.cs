namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 個体IDごとの「戦闘済みの結果」を引ける表示モデル。
    /// トレイ側はここから受け取るだけで、勝敗を自分では決めません。
    /// </summary>
    public interface IBattleSlotOutcomeSource
    {
        /// <summary>
        /// 指定個体の結果。未戦闘・未知のIDなら<see cref="BattleSlotOutcome.None"/>。
        /// </summary>
        BattleSlotOutcome GetOutcome(string instanceId);
    }
}
