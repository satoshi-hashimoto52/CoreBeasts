using System;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1ラウンドの勝敗を決める純粋判定。状態を持たず、同じ入力からは常に同じ結果を返します。
    ///
    /// 三すくみ:
    ///   Red   は Green に勝つ
    ///   Green は Blue  に勝つ
    ///   Blue  は Red   に勝つ
    ///
    /// 属性は集合として扱います。個体が持つ属性の並び順（一次・二次）で結果は変わりません。
    ///
    /// 「属性有利」の定義:
    ///   自分の属性のうち1つでも、相手の属性すべてに勝てるものがあれば属性有利とみなします。
    ///   相手の属性の一部にしか勝てない属性は、有利とみなしません。
    ///
    ///   例) Red/Blue vs Green
    ///       Red は Green（相手の全属性）に勝つ  → Red/Blue 側が属性有利
    ///       Green は Blue に勝つが Red には勝てない → Green 側は属性有利ではない
    ///       片側だけが属性有利 → Red/Blue 側の属性勝利
    ///
    ///   例) Red/Blue vs Green/Blue
    ///       Red は Green に勝つが Blue には勝てない
    ///       Blue は Red に勝つが Green には勝てない
    ///       どちらも相手の全属性を制圧できない → 相性は付かず POWER比較
    ///
    ///   例) Red/Blue vs Red/Blue
    ///       同上。相手の全属性に勝てる属性が双方に無い → POWER比較
    ///
    /// 属性で優劣が付かないときはPOWERを比較し、POWERも同値ならラウンド引き分けです。
    /// </summary>
    public static class BattleRules
    {
        /// <summary>三すくみで<paramref name="attacker"/>が<paramref name="defender"/>に勝つか。</summary>
        public static bool Beats(UnitAttribute attacker, UnitAttribute defender)
        {
            switch (attacker)
            {
                case UnitAttribute.Red:
                    return defender == UnitAttribute.Green;

                case UnitAttribute.Green:
                    return defender == UnitAttribute.Blue;

                case UnitAttribute.Blue:
                    return defender == UnitAttribute.Red;

                default:
                    return false;
            }
        }

        /// <summary>
        /// <paramref name="attacker"/>が<paramref name="defender"/>へ属性有利か。
        /// 自分の属性のうち1つでも相手の全属性に勝てれば true。
        /// </summary>
        public static bool HasAttributeAdvantage(BattleUnit attacker, BattleUnit defender)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            if (BeatsEveryAttribute(attacker.PrimaryAttribute, defender))
            {
                return true;
            }

            return attacker.HasSecondaryAttribute
                && BeatsEveryAttribute(attacker.SecondaryAttribute, defender);
        }

        /// <summary>
        /// 両者の選出から1ラウンドの結果を求めます。
        /// 属性有利が片側だけにあればその側の属性勝利、
        /// 双方にある場合と双方に無い場合はPOWER比較、POWERも同値なら引き分けです。
        /// </summary>
        public static RoundOutcome ResolveRound(BattleUnit playerUnit, BattleUnit cpuUnit)
        {
            if (playerUnit == null)
            {
                throw new ArgumentNullException(nameof(playerUnit));
            }

            if (cpuUnit == null)
            {
                throw new ArgumentNullException(nameof(cpuUnit));
            }

            bool playerAdvantage = HasAttributeAdvantage(playerUnit, cpuUnit);
            bool cpuAdvantage = HasAttributeAdvantage(cpuUnit, playerUnit);

            if (playerAdvantage && !cpuAdvantage)
            {
                return new RoundOutcome(
                    RoundWinner.Player,
                    RoundDecision.AttributeAdvantage);
            }

            if (cpuAdvantage && !playerAdvantage)
            {
                return new RoundOutcome(
                    RoundWinner.Cpu,
                    RoundDecision.AttributeAdvantage);
            }

            // 属性有利が双方にある（相殺）か、双方に無い場合はPOWERで決めます。
            if (playerUnit.Power > cpuUnit.Power)
            {
                return new RoundOutcome(
                    RoundWinner.Player,
                    RoundDecision.PowerComparison);
            }

            if (cpuUnit.Power > playerUnit.Power)
            {
                return new RoundOutcome(
                    RoundWinner.Cpu,
                    RoundDecision.PowerComparison);
            }

            return new RoundOutcome(RoundWinner.Draw, RoundDecision.PowerComparison);
        }

        /// <summary><paramref name="attacker"/>が相手の持つ属性すべてに勝てるか。</summary>
        private static bool BeatsEveryAttribute(UnitAttribute attacker, BattleUnit defender)
        {
            if (!Beats(attacker, defender.PrimaryAttribute))
            {
                return false;
            }

            return !defender.HasSecondaryAttribute
                || Beats(attacker, defender.SecondaryAttribute);
        }
    }
}
