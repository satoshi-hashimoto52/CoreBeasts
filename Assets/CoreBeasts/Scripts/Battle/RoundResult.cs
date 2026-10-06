using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 解決済み1ラウンドの記録。双方の選出、決着理由、勝者を保持する不変データです。
    ///
    /// Phase 4 で ATTRIBUTE LINK、Phase 5 でユニークスキルと比較POWERの内訳を追加しました。既存の項目は意味を変えていません。
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
            : this(
                roundNumber,
                playerUnit,
                cpuUnit,
                winner,
                decision,
                decidingAttribute,
                playerLink,
                cpuLink,
                playerEffectiveUnit,
                cpuEffectiveUnit,
                playerComparedPower,
                cpuComparedPower,
                UniqueSkillActivation.None,
                UniqueSkillActivation.None,
                LinkOnlyBreakdown(decision, playerComparedPower, playerLink),
                LinkOnlyBreakdown(decision, cpuComparedPower, cpuLink))
        {
        }

        /// <summary>
        /// 評価結果（<see cref="BattleRoundEvaluator"/>）からそのまま作ります。内訳も判定結果も評価時の値を写すだけで、
        /// 後から判定し直しません。
        /// </summary>
        public RoundResult(int roundNumber, RoundEvaluation evaluation)
            : this(
                roundNumber,
                Require(evaluation).PlayerUnit,
                evaluation.CpuUnit,
                evaluation.Outcome.Winner,
                evaluation.Outcome.Decision,
                evaluation.Outcome.DecidingAttribute,
                evaluation.PlayerLink,
                evaluation.CpuLink,
                evaluation.PlayerEffectiveUnit,
                evaluation.CpuEffectiveUnit,
                evaluation.Outcome.PlayerComparedValue,
                evaluation.Outcome.CpuComparedValue,
                evaluation.PlayerSkill,
                evaluation.CpuSkill,
                evaluation.PlayerPower,
                evaluation.CpuPower)
        {
        }

        /// <summary>すべての項目を受け取ります。</summary>
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
            int playerComparedPower,
            int cpuComparedPower,
            UniqueSkillActivation playerSkill,
            UniqueSkillActivation cpuSkill,
            ComparedPowerBreakdown playerPower,
            ComparedPowerBreakdown cpuPower)
        {
            PlayerSkill = playerSkill;
            CpuSkill = cpuSkill;
            PlayerPower = playerPower;
            CpuPower = cpuPower;
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

        /// <summary>判定に使ったプレイヤー側の個体（LINK・スキル・妨害を各属性POWERへ反映し、下限1へ揃えたもの）。</summary>
        public BattleUnit PlayerEffectiveUnit { get; }

        /// <summary>判定に使ったCPU側の個体（LINK・スキル・妨害を反映したもの）。</summary>
        public BattleUnit CpuEffectiveUnit { get; }

        /// <summary>
        /// POWER比較で比べたプレイヤー側の値（LINK・スキル反映後）。平均で比べた場合は平均値（切り捨て）。
        /// POWER比較以外では判定の比較値（<see cref="RoundOutcome.PlayerComparedValue"/>）をそのまま持ちます。
        /// 内訳は <see cref="PlayerPower"/> を読んでください。
        /// </summary>
        public int PlayerComparedPower { get; }

        /// <summary>POWER比較で比べたCPU側の値（LINK 反映後）。</summary>
        public int CpuComparedPower { get; }

        /// <summary>プレイヤー側のユニークスキル（持っているスキル・発動したか・理由・加算・減算）。</summary>
        public UniqueSkillActivation PlayerSkill { get; }

        /// <summary>CPU側のユニークスキル。</summary>
        public UniqueSkillActivation CpuSkill { get; }

        /// <summary>
        /// プレイヤー側の比較POWERの内訳（基礎・LINK・自分のスキル・相手からの妨害・最終値）。
        /// 表示はこれを読み、最終値から逆算しません。属性勝ちでは <see cref="ComparedPowerBreakdown.IsAvailable"/> が false です。
        /// </summary>
        public ComparedPowerBreakdown PlayerPower { get; }

        /// <summary>CPU側の比較POWERの内訳。</summary>
        public ComparedPowerBreakdown CpuPower { get; }

        /// <summary>
        /// LINK だけを受け取る従来の作り方での内訳。各属性へ同じ値を足すため、基礎値は比較値から LINK を引いた値です。
        /// </summary>
        private static ComparedPowerBreakdown LinkOnlyBreakdown(RoundDecision decision, int comparedPower, AttributeLinkResult link)
        {
            if (decision == RoundDecision.AttributeAdvantage)
            {
                return ComparedPowerBreakdown.Unavailable;
            }

            return new ComparedPowerBreakdown(comparedPower - link.BonusPower, link.BonusPower, 0, 0, comparedPower);
        }

        private static RoundEvaluation Require(RoundEvaluation evaluation)
        {
            return evaluation ?? throw new System.ArgumentNullException(nameof(evaluation));
        }
    }
}
