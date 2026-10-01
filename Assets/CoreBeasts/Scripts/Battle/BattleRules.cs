using System;
using System.Collections.Generic;

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
        /// 自分の色のうち1つでも相手の全色に勝てれば true。
        ///
        /// 単色どうし・2色対単色（共通色なし）の判定に使います。
        /// 2色が絡む残りの形は<see cref="ResolveRound"/>の表で個別に決めます。
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

            IReadOnlyList<AttributePower> mine = attacker.AttributePowers;

            for (int i = 0; i < mine.Count; i++)
            {
                if (BeatsEveryAttribute(mine[i].Attribute, defender))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 両者の選出から1ラウンドの結果を求めます。状態を持たない純粋な評価です。
        /// CPUの選択も、画面のプレビューも、必ずここを通してください。
        ///
        /// 色は1〜2色。3属性の三すくみなので、次の6通りですべてです。
        ///
        ///   単色 VS 単色（色が違う）   : 三すくみで決着
        ///   単色 VS 単色（同じ色）     : その色のPOWER → CORE → DRAW
        ///   2色 VS 単色               : 2色側が相手に勝てる色を1つでも持てば属性勝ち。
        ///                                 もう片方が負けていても相殺しません
        ///                                 （多色は有利な色を選んで戦えるものとします）。
        ///                                 勝てる色が無いときは必ず共通色があるので、
        ///                                 その色のPOWER → CORE → DRAW
        ///   2色 VS 2色（同じ構成）     : 互いの平均POWER → CORE → DRAW
        ///   2色 VS 2色（違う構成）     : 必ず1色が共通する。共通色を除いた
        ///                                 余剰色どうしを三すくみで比べて決着
        ///
        /// 平均は割り算をしません。件数を掛けた整数で比べるため、
        /// 丸めで勝敗が変わることがありません。
        ///
        /// 左右を入れ替えても必ず勝者が反転します。
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

            bool playerDual = playerUnit.IsDual;
            bool cpuDual = cpuUnit.IsDual;

            if (!playerDual && !cpuDual)
            {
                return ResolveSingleVersusSingle(playerUnit, cpuUnit);
            }

            if (playerDual && cpuDual)
            {
                return ResolveDualVersusDual(playerUnit, cpuUnit);
            }

            // 片側だけが2色。どちらが2色でも同じ表を使い、最後に向きを揃えます。
            return playerDual
                ? ResolveDualVersusSingle(playerUnit, cpuUnit, dualIsPlayer: true)
                : Flip(ResolveDualVersusSingle(cpuUnit, playerUnit, dualIsPlayer: false));
        }

        // ---------------- 単色 VS 単色 ----------------

        private static RoundOutcome ResolveSingleVersusSingle(
            BattleUnit playerUnit, BattleUnit cpuUnit)
        {
            UnitAttribute playerColor = playerUnit.PrimaryAttribute;
            UnitAttribute cpuColor = cpuUnit.PrimaryAttribute;

            if (playerColor != cpuColor)
            {
                // 3色の三すくみなので、違う色ならどちらかが必ず勝ちます。
                if (Beats(playerColor, cpuColor))
                {
                    return Attribute(RoundWinner.Player, playerColor, cpuColor, null);
                }

                return Attribute(RoundWinner.Cpu, cpuColor, playerColor, null);
            }

            // 同じ色。その色のPOWERで比べます。
            return ComparePower(
                playerUnit,
                cpuUnit,
                playerUnit.PowerOf(playerColor),
                cpuUnit.PowerOf(cpuColor),
                playerColor,
                playerColor,
                null,
                null);
        }

        // ---------------- 2色 VS 単色 ----------------

        /// <summary>
        /// <paramref name="dual"/>が2色、<paramref name="single"/>が単色として解きます。
        /// 戻り値は常に「2色側をプレイヤー」とみなした向きです。
        /// 呼び出し側が<paramref name="dualIsPlayer"/>を見て必要なら反転します。
        ///
        /// 2色は「相手へ有利な色を選んで戦える」ものとして扱います。
        /// 持っている色のどれか1つでも相手に勝てば、それで属性勝ちです。
        /// もう片方の色が負けていても、それで打ち消すことはしません。
        /// </summary>
        private static RoundOutcome ResolveDualVersusSingle(
            BattleUnit dual, BattleUnit single, bool dualIsPlayer)
        {
            UnitAttribute singleColor = single.PrimaryAttribute;

            IReadOnlyList<AttributePower> mine = dual.AttributePowers;

            // 1. 相手へ勝てる色を1つでも持っていれば、そこで決まります。
            //    POWERもCOREも見ません。
            for (int i = 0; i < mine.Count; i++)
            {
                if (Beats(mine[i].Attribute, singleColor))
                {
                    return Attribute(
                        RoundWinner.Player,
                        mine[i].Attribute,
                        singleColor,
                        dual.HasAttribute(singleColor) ? singleColor : (UnitAttribute?)null,
                        playerSurplus: OtherAttributeOf(dual, mine[i].Attribute));
                }
            }

            // 2. 勝てる色が無いときは、相手と同じ色を必ず持っています。
            //    その共通色のPOWERで比べます。
            if (dual.HasAttribute(singleColor))
            {
                return ComparePower(
                    dual,
                    single,
                    dual.PowerOf(singleColor),
                    single.PowerOf(singleColor),
                    singleColor,
                    singleColor,
                    playerSurplus: OtherAttributeOf(dual, singleColor),
                    cpuSurplus: null);
            }

            // 3. ここへは来ません。
            //    3属性・最大2色なら、相手の色を持たない2色は残る2色すべてを持ち、
            //    そのうち片方は必ず相手に勝ちます。
            //    黙って平均POWERへ逃がすと、誤った勝敗をそのまま表示してしまいます。
            throw new ArgumentException(
                "2色 " + dual + " と 単色 " + single +
                " で、勝てる色も共通色もありません。属性構成が仕様から外れています。",
                nameof(dual));
        }

        // ---------------- 2色 VS 2色 ----------------

        private static RoundOutcome ResolveDualVersusDual(
            BattleUnit playerUnit, BattleUnit cpuUnit)
        {
            UnitAttribute p0 = playerUnit.AttributePowers[0].Attribute;
            UnitAttribute p1 = playerUnit.AttributePowers[1].Attribute;

            bool sameSet = cpuUnit.HasAttribute(p0) && cpuUnit.HasAttribute(p1);

            if (sameSet)
            {
                // 登録順が違っても同じ色集合なら同一構成です。
                // 互いの平均POWERで比べます。件数が同じなので合計のまま比べられます。
                return ComparePower(
                    playerUnit,
                    cpuUnit,
                    playerUnit.SumOfAttributePower,
                    cpuUnit.SumOfAttributePower,
                    decidingAttribute: null,
                    shared: null,
                    playerSurplus: null,
                    cpuSurplus: null,
                    playerDisplayValue: playerUnit.SumOfAttributePower / 2,
                    cpuDisplayValue: cpuUnit.SumOfAttributePower / 2);
            }

            // 3色で2色ずつなら、必ず1色だけが共通します。
            UnitAttribute shared = cpuUnit.HasAttribute(p0) ? p0 : p1;

            UnitAttribute playerSurplus = OtherAttributeOf(playerUnit, shared);
            UnitAttribute cpuSurplus = OtherAttributeOf(cpuUnit, shared);

            if (Beats(playerSurplus, cpuSurplus))
            {
                return Attribute(
                    RoundWinner.Player, playerSurplus, cpuSurplus, shared,
                    playerSurplus, cpuSurplus);
            }

            return Attribute(
                RoundWinner.Cpu, cpuSurplus, playerSurplus, shared,
                playerSurplus, cpuSurplus);
        }

        // ---------------- 共通処理 ----------------

        /// <summary>POWER → CORE → DRAW の順で決めます。</summary>
        private static RoundOutcome ComparePower(
            BattleUnit playerUnit,
            BattleUnit cpuUnit,
            int playerValue,
            int cpuValue,
            UnitAttribute? decidingAttribute,
            UnitAttribute? shared,
            UnitAttribute? playerSurplus,
            UnitAttribute? cpuSurplus,
            int playerDisplayValue = int.MinValue,
            int cpuDisplayValue = int.MinValue)
        {
            int shownPlayer =
                playerDisplayValue == int.MinValue ? playerValue : playerDisplayValue;

            int shownCpu =
                cpuDisplayValue == int.MinValue ? cpuValue : cpuDisplayValue;

            if (playerValue != cpuValue)
            {
                return new RoundOutcome(
                    playerValue > cpuValue ? RoundWinner.Player : RoundWinner.Cpu,
                    RoundDecision.PowerComparison,
                    decidingAttribute,
                    shownPlayer,
                    shownCpu,
                    shared,
                    playerSurplus,
                    cpuSurplus);
            }

            // POWERが同値。COREで決めます。
            if (playerUnit.Core != cpuUnit.Core)
            {
                return new RoundOutcome(
                    playerUnit.Core > cpuUnit.Core ? RoundWinner.Player : RoundWinner.Cpu,
                    RoundDecision.CoreComparison,
                    null,
                    playerUnit.Core,
                    cpuUnit.Core,
                    shared,
                    playerSurplus,
                    cpuSurplus);
            }

            return new RoundOutcome(
                RoundWinner.Draw,
                RoundDecision.Draw,
                null,
                playerUnit.Core,
                cpuUnit.Core,
                shared,
                playerSurplus,
                cpuSurplus);
        }

        private static RoundOutcome Attribute(
            RoundWinner winner,
            UnitAttribute winningAttribute,
            UnitAttribute losingAttribute,
            UnitAttribute? shared,
            UnitAttribute? playerSurplus = null,
            UnitAttribute? cpuSurplus = null)
        {
            return new RoundOutcome(
                winner,
                RoundDecision.AttributeAdvantage,
                winningAttribute,
                0,
                0,
                shared,
                playerSurplus,
                cpuSurplus);
        }

        /// <summary>左右を入れ替えた結果へ変換します。</summary>
        private static RoundOutcome Flip(RoundOutcome outcome)
        {
            RoundWinner winner = outcome.Winner;

            if (winner == RoundWinner.Player)
            {
                winner = RoundWinner.Cpu;
            }
            else if (winner == RoundWinner.Cpu)
            {
                winner = RoundWinner.Player;
            }

            return new RoundOutcome(
                winner,
                outcome.Decision,
                outcome.DecidingAttribute,
                outcome.CpuComparedValue,
                outcome.PlayerComparedValue,
                outcome.SharedAttribute,
                outcome.CpuSurplusAttribute,
                outcome.PlayerSurplusAttribute);
        }

        /// <summary>2色の個体から、指定色ではないほうの色を返します。</summary>
        private static UnitAttribute OtherAttributeOf(BattleUnit dual, UnitAttribute known)
        {
            IReadOnlyList<AttributePower> powers = dual.AttributePowers;

            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i].Attribute != known)
                {
                    return powers[i].Attribute;
                }
            }

            return known;
        }

        /// <summary><paramref name="attacker"/>が相手の持つ色すべてに勝てるか。</summary>
        private static bool BeatsEveryAttribute(UnitAttribute attacker, BattleUnit defender)
        {
            IReadOnlyList<AttributePower> theirs = defender.AttributePowers;

            for (int i = 0; i < theirs.Count; i++)
            {
                if (!Beats(attacker, theirs[i].Attribute))
                {
                    return false;
                }
            }

            return theirs.Count > 0;
        }
    }
}
