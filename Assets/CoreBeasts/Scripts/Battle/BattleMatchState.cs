namespace CoreBeasts.Battle
{
    /// <summary>マッチ全体の進行状態。</summary>
    public enum BattleMatchState
    {
        /// <summary>進行中。まだ選出と解決を受け付けます。</summary>
        InProgress = 0,

        /// <summary>プレイヤーのマッチ勝利。</summary>
        PlayerWin = 1,

        /// <summary>CPUのマッチ勝利。</summary>
        CpuWin = 2,

        /// <summary>規定ラウンドを終えても先取数へ届かなかった引き分け。</summary>
        Draw = 3,
    }
}
