namespace CoreBeasts.Battle
{
    /// <summary>
    /// 解決済み1ラウンドの記録。双方の選出、決着理由、勝者を保持する不変データです。
    /// </summary>
    public sealed class RoundResult
    {
        public RoundResult(
            int roundNumber,
            BattleUnit playerUnit,
            BattleUnit cpuUnit,
            RoundWinner winner,
            RoundDecision decision)
        {
            RoundNumber = roundNumber;
            PlayerUnit = playerUnit;
            CpuUnit = cpuUnit;
            Winner = winner;
            Decision = decision;
        }

        /// <summary>1から始まるラウンド番号。</summary>
        public int RoundNumber { get; }

        /// <summary>プレイヤーが出した個体。</summary>
        public BattleUnit PlayerUnit { get; }

        /// <summary>CPUが出した個体。</summary>
        public BattleUnit CpuUnit { get; }

        /// <summary>このラウンドの勝者。</summary>
        public RoundWinner Winner { get; }

        /// <summary>決着理由。</summary>
        public RoundDecision Decision { get; }

        /// <summary>ラウンド引き分けか。</summary>
        public bool IsDraw => Winner == RoundWinner.Draw;
    }
}
