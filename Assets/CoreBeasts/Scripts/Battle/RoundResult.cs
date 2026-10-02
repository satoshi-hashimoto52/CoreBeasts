using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 解決済み1ラウンドの記録。双方の選出、決着理由、勝者を保持する不変データです。
    ///
    /// Phase 4 で ATTRIBUTE LINK の情報を追加しました。既存の項目は意味を変えていません。
    /// <see cref="PlayerUnit"/> / <see cref="CpuUnit"/> は出した元の個体（元のPOWER）のままで、
    /// LINK を反映した判定用の個体は <see cref="PlayerEffectiveUnit"/> / <see cref="CpuEffectiveUnit"/> です。
    /// </summary>
    public sealed class RoundResult
    {
        public RoundResult(
            int roundNumber,
            BattleUnit playerUnit,
            BattleUnit cpuUnit,
            RoundWinner winner,
            RoundDecision decision)
            : this(
                roundNumber,
                playerUnit,
                cpuUnit,
                winner,
                decision,
                null,
                AttributeLinkResult.None,
                AttributeLinkResult.None,
                playerUnit,
                cpuUnit)
        {
        }

        /// <summary>
        /// LINK と勝因の属性まで含めて作ります。
        /// <paramref name="playerEffectiveUnit"/> / <paramref name="cpuEffectiveUnit"/>が null なら元の個体を使います。
        /// </summary>
        public RoundResult(
            int roundNumber,
            BattleUnit playerUnit,
            BattleUnit cpuUnit,
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute,
            AttributeLinkResult playerLink,
            AttributeLinkResult cpuLink,
            BattleUnit playerEffectiveUnit,
            BattleUnit cpuEffectiveUnit,
            int playerComparedPower = 0,
            int cpuComparedPower = 0)
        {
            RoundNumber = roundNumber;
            PlayerUnit = playerUnit;
            CpuUnit = cpuUnit;
            Winner = winner;
            Decision = decision;
            DecidingAttribute = decidingAttribute;
            PlayerLink = playerLink;
            CpuLink = cpuLink;
            PlayerEffectiveUnit = playerEffectiveUnit ?? playerUnit;
            CpuEffectiveUnit = cpuEffectiveUnit ?? cpuUnit;
            PlayerComparedPower = playerComparedPower;
            CpuComparedPower = cpuComparedPower;
        }

        /// <summary>1から始まるラウンド番号。</summary>
        public int RoundNumber { get; }

        /// <summary>プレイヤーが出した個体（元のPOWER）。</summary>
        public BattleUnit PlayerUnit { get; }

        /// <summary>CPUが出した個体（元のPOWER）。</summary>
        public BattleUnit CpuUnit { get; }

        /// <summary>このラウンドの勝者。</summary>
        public RoundWinner Winner { get; }

        /// <summary>決着理由。</summary>
        public RoundDecision Decision { get; }

        /// <summary>ラウンド引き分けか。</summary>
        public bool IsDraw => Winner == RoundWinner.Draw;

        /// <summary>
        /// 勝敗を決めた属性。LINK を反映した個体での判定結果（<see cref="RoundOutcome.DecidingAttribute"/>）を
        /// そのまま記録したもので、画面はこれを正本として読みます。
        /// 平均POWERの比較・CORE比較・引き分けでは null です。
        /// </summary>
        public UnitAttribute? DecidingAttribute { get; }

        /// <summary>プレイヤー側の ATTRIBUTE LINK。</summary>
        public AttributeLinkResult PlayerLink { get; }

        /// <summary>CPU側の ATTRIBUTE LINK。</summary>
        public AttributeLinkResult CpuLink { get; }

        /// <summary>判定に使ったプレイヤー側の個体（LINK のボーナスを各属性POWERへ足したもの）。</summary>
        public BattleUnit PlayerEffectiveUnit { get; }

        /// <summary>判定に使ったCPU側の個体（LINK のボーナスを各属性POWERへ足したもの）。</summary>
        public BattleUnit CpuEffectiveUnit { get; }

        /// <summary>
        /// POWER比較で比べたプレイヤー側の値（LINK 反映後）。平均で比べた場合は平均値（切り捨て）。
        /// POWER比較以外では判定の比較値（<see cref="RoundOutcome.PlayerComparedValue"/>）をそのまま持ちます。
        /// 元の値は <c>PlayerComparedPower - PlayerLink.BonusPower</c> です（各属性へ同じ値を足すため）。
        /// </summary>
        public int PlayerComparedPower { get; }

        /// <summary>POWER比較で比べたCPU側の値（LINK 反映後）。</summary>
        public int CpuComparedPower { get; }
    }
}
