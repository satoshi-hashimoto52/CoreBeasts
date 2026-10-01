using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1ラウンドの判定結果。誰が勝ったかと、何で決着したかを持ちます。
    ///
    /// 画面が勝敗理由を計算し直さなくて済むよう、
    /// 「どの色で決まったか」「何と何を比べたか」もここへ載せます。
    /// どの個体が出たかは<see cref="RoundResult"/>が保持します。
    /// </summary>
    public readonly struct RoundOutcome
    {
        public RoundOutcome(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute = null,
            int playerComparedValue = 0,
            int cpuComparedValue = 0,
            UnitAttribute? sharedAttribute = null,
            UnitAttribute? playerSurplusAttribute = null,
            UnitAttribute? cpuSurplusAttribute = null)
        {
            Winner = winner;
            Decision = decision;
            DecidingAttribute = decidingAttribute;
            PlayerComparedValue = playerComparedValue;
            CpuComparedValue = cpuComparedValue;
            SharedAttribute = sharedAttribute;
            PlayerSurplusAttribute = playerSurplusAttribute;
            CpuSurplusAttribute = cpuSurplusAttribute;
        }

        /// <summary>勝者。引き分けなら<see cref="RoundWinner.Draw"/>。</summary>
        public RoundWinner Winner { get; }

        /// <summary>決着理由。</summary>
        public RoundDecision Decision { get; }

        /// <summary>
        /// 勝敗を決めた属性。
        /// 属性勝ちなら勝因になった色、POWER勝負なら比べた色です。
        /// 平均POWERの比較とCORE比較、引き分けでは null になります。
        /// </summary>
        public UnitAttribute? DecidingAttribute { get; }

        /// <summary>
        /// プレイヤー側の比較値。POWER比較ならPOWER、CORE比較ならCOREです。
        /// 平均で比べた場合は合計ではなく、表示に使える平均値（切り捨て）を入れます。
        /// </summary>
        public int PlayerComparedValue { get; }

        /// <summary>CPU側の比較値。</summary>
        public int CpuComparedValue { get; }

        /// <summary>両者が共通して持っていた色。無い場合は null。</summary>
        public UnitAttribute? SharedAttribute { get; }

        /// <summary>プレイヤー側の余剰色（共通色を除いた残り）。</summary>
        public UnitAttribute? PlayerSurplusAttribute { get; }

        /// <summary>CPU側の余剰色。</summary>
        public UnitAttribute? CpuSurplusAttribute { get; }

        /// <summary>ラウンド引き分けか。</summary>
        public bool IsDraw => Winner == RoundWinner.Draw;
    }
}
