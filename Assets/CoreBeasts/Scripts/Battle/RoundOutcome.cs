namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1ラウンドの判定結果。誰が勝ったかと、何で決着したかだけを持ちます。
    /// どの個体が出たかは<see cref="RoundResult"/>が保持します。
    /// </summary>
    public readonly struct RoundOutcome
    {
        public RoundOutcome(RoundWinner winner, RoundDecision decision)
        {
            Winner = winner;
            Decision = decision;
        }

        /// <summary>勝者。引き分けなら<see cref="RoundWinner.Draw"/>。</summary>
        public RoundWinner Winner { get; }

        /// <summary>決着理由。</summary>
        public RoundDecision Decision { get; }

        /// <summary>ラウンド引き分けか。</summary>
        public bool IsDraw => Winner == RoundWinner.Draw;
    }
}
