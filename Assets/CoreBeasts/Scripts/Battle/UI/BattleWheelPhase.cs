namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// リング表示だけが持つ進行段階。
    ///
    /// <see cref="BattleSession"/>へは何も足しません。
    /// <see cref="BattleUiState"/>にも段階を足さず、
    /// 「出撃の移動中」と「履歴へ片付け中」という表示だけの2つをここで足します。
    /// </summary>
    public enum BattleWheelPhase
    {
        /// <summary>まだ準備できていません。</summary>
        Idle = 0,

        /// <summary>リングを回せて、中央を上スライドできます。</summary>
        Selecting = 1,

        /// <summary>中央個体を戦闘位置へ移動中。入力はすべて止めます。</summary>
        Deploying = 2,

        /// <summary>対戦演出中。</summary>
        Resolving = 3,

        /// <summary>結果表示中。</summary>
        ShowingResult = 4,

        /// <summary>戦闘表示を履歴レーンへ片付け中。</summary>
        Archiving = 5,

        /// <summary>決着。REMATCHだけを受け付けます。</summary>
        Finished = 6,
    }

    /// <summary>
    /// 進行役の状態と、表示だけの2段階から、リングの段階を決めます。
    /// 判定を1箇所へ閉じ込めるため、コントローラもテストもここを通ります。
    /// </summary>
    public static class BattleWheelPhases
    {
        /// <summary>
        /// 表示段階を決めます。移動中・片付け中は、進行役の状態より優先します。
        /// </summary>
        public static BattleWheelPhase Resolve(
            BattleUiState state, bool isDeploying, bool isArchiving)
        {
            if (isDeploying)
            {
                return BattleWheelPhase.Deploying;
            }

            if (isArchiving)
            {
                return BattleWheelPhase.Archiving;
            }

            switch (state)
            {
                case BattleUiState.Selecting:
                    return BattleWheelPhase.Selecting;

                case BattleUiState.Resolving:
                    return BattleWheelPhase.Resolving;

                case BattleUiState.ShowingResult:
                    return BattleWheelPhase.ShowingResult;

                case BattleUiState.MatchFinished:
                    return BattleWheelPhase.Finished;

                default:
                    return BattleWheelPhase.Idle;
            }
        }

        /// <summary>
        /// リングの入力（横回転・上スライド）を受け付ける段階か。
        /// 受け付けるのは<see cref="BattleWheelPhase.Selecting"/>だけです。
        /// </summary>
        public static bool AllowsInput(BattleWheelPhase phase, bool settingsOpen)
        {
            return !settingsOpen && phase == BattleWheelPhase.Selecting;
        }
    }
}
