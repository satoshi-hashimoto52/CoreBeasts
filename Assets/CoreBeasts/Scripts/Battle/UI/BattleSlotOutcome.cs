namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// プレイヤーの7枠それぞれに出す「戦闘済みの結果」。
    ///
    /// <see cref="BattleSlotState"/>（未使用・選択中・使用済み）とは別の概念です。
    /// あちらは今この枠を触れるかどうか、こちらは終わった戦いがどうだったかを表します。
    /// 両者を1つの列挙へまとめないのは、
    /// 「選択中だが未解決」と「引き分けで決着済み」を取り違えないためです。
    /// </summary>
    public enum BattleSlotOutcome
    {
        /// <summary>まだ戦っていない。バッジは出しません。</summary>
        None = 0,

        /// <summary>このラウンドはプレイヤーの勝ち。</summary>
        Win = 1,

        /// <summary>このラウンドはCPUの勝ち。</summary>
        Loss = 2,

        /// <summary>このラウンドは引き分け。</summary>
        Draw = 3,
    }

    /// <summary>
    /// 勝敗の言い換えだけを行います。ここでは判定を一切しません。
    /// 勝敗そのものは<see cref="BattleSession"/>が決め、
    /// <see cref="RoundResult.Winner"/>として渡ってきます。
    /// </summary>
    public static class BattleSlotOutcomes
    {
        /// <summary>勝利バッジの文字。色だけに頼らないため、必ず出します。</summary>
        public const string WinSymbol = "W";

        /// <summary>敗北バッジの文字。</summary>
        public const string LossSymbol = "L";

        /// <summary>引き分けバッジの文字。</summary>
        public const string DrawSymbol = "D";

        /// <summary>
        /// ラウンドの勝者を、PLAYER側から見た結果へ言い換えます。
        /// 属性・POWER・スコアからの再計算は行いません。
        /// </summary>
        public static BattleSlotOutcome FromWinner(RoundWinner winner)
        {
            switch (winner)
            {
                case RoundWinner.Player:
                    return BattleSlotOutcome.Win;

                case RoundWinner.Cpu:
                    return BattleSlotOutcome.Loss;

                default:
                    return BattleSlotOutcome.Draw;
            }
        }

        /// <summary>バッジへ出す文字。未戦闘は空文字です。</summary>
        public static string Symbol(BattleSlotOutcome outcome)
        {
            switch (outcome)
            {
                case BattleSlotOutcome.Win:
                    return WinSymbol;

                case BattleSlotOutcome.Loss:
                    return LossSymbol;

                case BattleSlotOutcome.Draw:
                    return DrawSymbol;

                default:
                    return string.Empty;
            }
        }
    }
}
