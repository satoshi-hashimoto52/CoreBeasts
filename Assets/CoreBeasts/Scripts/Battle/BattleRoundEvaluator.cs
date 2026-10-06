using System;
using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1陣営が今回のラウンドへ持ち込む「自分の直前」の情報。自分の履歴だけから作ります。
    /// 初戦は <see cref="First"/>（直前の個体なし・チェーン0・結果なし）です。
    /// </summary>
    public readonly struct RoundSideContext
    {
        public RoundSideContext(BattleUnit previousUnit, int previousLinkChain, PreviousRoundResult previousResult)
        {
            PreviousUnit = previousUnit;
            PreviousLinkChain = previousLinkChain;
            PreviousResult = previousResult;
        }

        /// <summary>初戦。</summary>
        public static RoundSideContext First => new RoundSideContext(null, 0, PreviousRoundResult.None);

        /// <summary>自分が直前に出した個体（元の個体）。初戦は null。</summary>
        public BattleUnit PreviousUnit { get; }

        /// <summary>直前の個体の ATTRIBUTE LINK のチェーン数。</summary>
        public int PreviousLinkChain { get; }

        /// <summary>自分から見た直前のラウンドの結果。</summary>
        public PreviousRoundResult PreviousResult { get; }

        /// <summary>履歴の最後のラウンドから、PLAYER 側の文脈を作ります。</summary>
        public static RoundSideContext ForPlayer(IReadOnlyList<RoundResult> history)
        {
            RoundResult last = Last(history);

            return last == null
                ? First
                : new RoundSideContext(last.PlayerUnit, last.PlayerLink.ChainCount, ResultFor(last.Winner, RoundWinner.Player));
        }

        /// <summary>履歴の最後のラウンドから、CPU 側の文脈を作ります。</summary>
        public static RoundSideContext ForCpu(IReadOnlyList<RoundResult> history)
        {
            RoundResult last = Last(history);

            return last == null
                ? First
                : new RoundSideContext(last.CpuUnit, last.CpuLink.ChainCount, ResultFor(last.Winner, RoundWinner.Cpu));
        }

        /// <summary>勝者と自分の側から、自分にとっての結果を求めます。</summary>
        public static PreviousRoundResult ResultFor(RoundWinner winner, RoundWinner self)
        {
            if (winner == RoundWinner.Draw)
            {
                return PreviousRoundResult.Draw;
            }

            return winner == self ? PreviousRoundResult.Won : PreviousRoundResult.Lost;
        }

        private static RoundResult Last(IReadOnlyList<RoundResult> history)
        {
            return history != null && history.Count > 0 ? history[history.Count - 1] : null;
        }
    }

    /// <summary>
    /// POWER の段で比べた値の内訳（1陣営ぶん）。表示はこれを読むだけで、最終値から逆算しません。
    ///
    /// <see cref="Final"/> は判定に使った値そのものです。<see cref="Base"/> + <see cref="Link"/> + <see cref="SelfSkill"/>
    /// - <see cref="OpponentPenalty"/> + <see cref="FloorAdjustment"/> = <see cref="Final"/> が常に成り立ちます
    /// （<see cref="FloorAdjustment"/> は下限1へ揃えたぶん。通常は0）。
    /// 属性勝ちでは POWER を比べないため <see cref="IsAvailable"/> は false です。
    /// </summary>
    public readonly struct ComparedPowerBreakdown
    {
        public ComparedPowerBreakdown(int basePower, int link, int selfSkill, int opponentPenalty, int final)
        {
            IsAvailable = true;
            Base = basePower;
            Link = link;
            SelfSkill = selfSkill;
            OpponentPenalty = opponentPenalty;
            Final = final;
        }

        /// <summary>POWER を比べなかった（属性勝ち）。</summary>
        public static ComparedPowerBreakdown Unavailable => default;

        /// <summary>POWER の段まで進んだか。</summary>
        public bool IsAvailable { get; }

        /// <summary>基礎の比較POWER（元の個体の値。2色どうしの平均は切り捨て）。</summary>
        public int Base { get; }

        /// <summary>自分の ATTRIBUTE LINK による加算。</summary>
        public int Link { get; }

        /// <summary>自分のユニークスキルによる加算。</summary>
        public int SelfSkill { get; }

        /// <summary>相手のユニークスキルから受けた減算（正の値）。</summary>
        public int OpponentPenalty { get; }

        /// <summary>最終的な比較POWER（判定に使った値）。</summary>
        public int Final { get; }

        /// <summary>下限1へ揃えたぶん。</summary>
        public int FloorAdjustment => Final - (Base + Link + SelfSkill - OpponentPenalty);

        /// <summary>基礎値から変わったか。</summary>
        public bool HasModifiers => IsAvailable && (Link != 0 || SelfSkill != 0 || OpponentPenalty != 0);
    }

    /// <summary>1ラウンドの評価結果。<see cref="BattleRoundEvaluator.Evaluate"/> が作ります。</summary>
    public sealed class RoundEvaluation
    {
        internal RoundEvaluation(
            BattleUnit playerUnit,
            BattleUnit cpuUnit,
            AttributeLinkResult playerLink,
            AttributeLinkResult cpuLink,
            UniqueSkillActivation playerSkill,
            UniqueSkillActivation cpuSkill,
            BattleUnit playerEffective,
            BattleUnit cpuEffective,
            RoundOutcome outcome,
            ComparedPowerBreakdown playerPower,
            ComparedPowerBreakdown cpuPower)
        {
            PlayerUnit = playerUnit;
            CpuUnit = cpuUnit;
            PlayerLink = playerLink;
            CpuLink = cpuLink;
            PlayerSkill = playerSkill;
            CpuSkill = cpuSkill;
            PlayerEffectiveUnit = playerEffective;
            CpuEffectiveUnit = cpuEffective;
            Outcome = outcome;
            PlayerPower = playerPower;
            CpuPower = cpuPower;
        }

        public BattleUnit PlayerUnit { get; }

        public BattleUnit CpuUnit { get; }

        public AttributeLinkResult PlayerLink { get; }

        public AttributeLinkResult CpuLink { get; }

        public UniqueSkillActivation PlayerSkill { get; }

        public UniqueSkillActivation CpuSkill { get; }

        public BattleUnit PlayerEffectiveUnit { get; }

        public BattleUnit CpuEffectiveUnit { get; }

        /// <summary>LINK とスキルを反映した個体での判定結果（勝因の属性の正本）。</summary>
        public RoundOutcome Outcome { get; }

        public ComparedPowerBreakdown PlayerPower { get; }

        public ComparedPowerBreakdown CpuPower { get; }
    }

    /// <summary>
    /// 1ラウンドの評価（Phase 5）。Unity に依存しない純粋な関数です。
    ///
    /// 計算順序（固定）:
    ///   1. 基礎POWER
    ///   2. 自分の ATTRIBUTE LINK 加算（+3 / +6）
    ///   3. 自分のユニークスキル加算
    ///   4. 相手のユニークスキルによる減算
    ///   5. 属性ごとのPOWERを下限1へ補正
    ///   6. 変更していない <see cref="BattleRules.ResolveRound"/> で判定
    ///
    /// LINK の成立とスキルの条件は、元の個体の属性と自分の履歴（<see cref="RoundSideContext"/>）だけで決めます。
    /// 元の個体は書き換えず、状態も持ちません。判定が例外を投げても何も進みません。
    /// </summary>
    public static class BattleRoundEvaluator
    {
        public static RoundEvaluation Evaluate(
            BattleUnit playerUnit, BattleUnit cpuUnit, RoundSideContext player, RoundSideContext cpu)
        {
            if (playerUnit == null)
            {
                throw new ArgumentNullException(nameof(playerUnit));
            }

            if (cpuUnit == null)
            {
                throw new ArgumentNullException(nameof(cpuUnit));
            }

            AttributeLinkResult playerLink = AttributeLink.Evaluate(player.PreviousUnit, playerUnit, player.PreviousLinkChain);
            AttributeLinkResult cpuLink = AttributeLink.Evaluate(cpu.PreviousUnit, cpuUnit, cpu.PreviousLinkChain);

            UniqueSkillActivation playerSkill = UniqueSkill.Evaluate(playerUnit, player.PreviousUnit, player.PreviousResult, cpuUnit);
            UniqueSkillActivation cpuSkill = UniqueSkill.Evaluate(cpuUnit, cpu.PreviousUnit, cpu.PreviousResult, playerUnit);

            BattleUnit playerEffective = UniqueSkill.ApplyModifiers(
                playerUnit, playerLink.BonusPower, playerSkill.SelfBonus, cpuSkill.OpponentPenalty);
            BattleUnit cpuEffective = UniqueSkill.ApplyModifiers(
                cpuUnit, cpuLink.BonusPower, cpuSkill.SelfBonus, playerSkill.OpponentPenalty);

            RoundOutcome outcome = BattleRules.ResolveRound(playerEffective, cpuEffective);

            ComparedPowerBreakdown playerPower = Breakdown(
                outcome, playerUnit, playerEffective, playerLink.BonusPower, playerSkill.SelfBonus, cpuSkill.OpponentPenalty);
            ComparedPowerBreakdown cpuPower = Breakdown(
                outcome, cpuUnit, cpuEffective, cpuLink.BonusPower, cpuSkill.SelfBonus, playerSkill.OpponentPenalty);

            return new RoundEvaluation(
                playerUnit, cpuUnit, playerLink, cpuLink, playerSkill, cpuSkill,
                playerEffective, cpuEffective, outcome, playerPower, cpuPower);
        }

        /// <summary>
        /// POWER の段で比べた値の内訳。比べた色は判定結果の <see cref="RoundOutcome.SharedAttribute"/> を読み
        /// （同じ色・2色対単色はその色、2色どうしの同一構成は共通色が無く平均）、判定し直しません。
        /// </summary>
        private static ComparedPowerBreakdown Breakdown(
            RoundOutcome outcome, BattleUnit unit, BattleUnit effective, int link, int selfSkill, int opponentPenalty)
        {
            if (outcome.Decision == RoundDecision.AttributeAdvantage)
            {
                return ComparedPowerBreakdown.Unavailable;
            }

            int basePower;
            int finalPower;

            if (outcome.SharedAttribute.HasValue)
            {
                basePower = unit.PowerOf(outcome.SharedAttribute.Value);
                finalPower = effective.PowerOf(outcome.SharedAttribute.Value);
            }
            else
            {
                basePower = Sum(unit) / unit.AttributeCount;
                finalPower = Sum(effective) / effective.AttributeCount;
            }

            return new ComparedPowerBreakdown(basePower, link, selfSkill, opponentPenalty, finalPower);
        }

        private static int Sum(BattleUnit unit)
        {
            int total = 0;

            for (int i = 0; i < unit.AttributePowers.Count; i++)
            {
                total += unit.AttributePowers[i].Power;
            }

            return total;
        }
    }
}
