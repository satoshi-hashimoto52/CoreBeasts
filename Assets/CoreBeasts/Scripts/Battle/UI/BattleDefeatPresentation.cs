namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 「今、出場中のどちらをグレー化するか」だけを決めます。
    ///
    /// 勝敗そのものは決めません。<see cref="BattleSession"/>が確定させ、
    /// <see cref="BattleFlowCoordinator.RevealRoundOutcome"/>が公開した結果を
    /// 受け取って言い換えるだけです。
    ///
    /// Unityへ依存しないため、そのままテストできます。
    /// <see cref="BattleScreenController"/>もテストも同じこの規則を通ります。
    /// </summary>
    public static class BattleDefeatPresentation
    {
        /// <summary>
        /// 出場中の表示へ敗北を反映してよい状態か。
        ///
        /// 反映してよいのは、勝敗を画面へ出した後だけです。
        /// 選択中（<see cref="BattleUiState.Selecting"/>）と
        /// 解決・演出中（<see cref="BattleUiState.Resolving"/>）では反映しません。
        /// DEPLOY直後や、接触・フラッシュの最中に先出ししないのはこの判定のためです。
        /// </summary>
        public static bool ShowsResolvedRound(BattleUiState state)
        {
            return state == BattleUiState.ShowingResult ||
                   state == BattleUiState.MatchFinished;
        }

        /// <summary>
        /// PLAYER側の出場表示をグレー化するか。
        /// CPUが勝ったラウンド（PLAYERから見て Loss）だけ true です。
        /// </summary>
        public static bool GreysPlayer(BattleUiState state, BattleSlotOutcome revealed)
        {
            return ShowsResolvedRound(state) && revealed == BattleSlotOutcome.Loss;
        }

        /// <summary>
        /// CPU側の出場表示をグレー化するか。
        /// PLAYERが勝ったラウンド（PLAYERから見て Win）だけ true です。
        /// 引き分けはどちらも false のままです。
        /// </summary>
        public static bool GreysCpu(BattleUiState state, BattleSlotOutcome revealed)
        {
            return ShowsResolvedRound(state) && revealed == BattleSlotOutcome.Win;
        }

        /// <summary>
        /// 自軍トレイの枠をグレー化するか。
        /// 公開済みの結果が Loss の個体だけ true です。個体IDで引くため、
        /// 同じDefinitionの別個体へは波及しません。
        /// </summary>
        public static bool GreysTraySlot(BattleSlotOutcome revealed)
        {
            return revealed == BattleSlotOutcome.Loss;
        }
    }
}
