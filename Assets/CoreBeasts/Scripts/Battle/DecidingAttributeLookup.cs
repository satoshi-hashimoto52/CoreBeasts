using System;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 解決済みラウンドの「勝因になった属性」を、演出のためだけに引きます。
    ///
    /// <see cref="RoundResult"/>は決着理由までしか持たないため、同じ2体を
    /// <see cref="BattleRules.ResolveRound"/>へもう一度渡して<see cref="RoundOutcome.DecidingAttribute"/>を読みます。
    /// ResolveRound は副作用のない決定的な関数なので、勝敗もスコアも進行も変わりません。
    ///
    /// 読み直した勝者か決着理由が<see cref="RoundResult"/>と食い違う場合や、判定が例外を
    /// 投げた場合は null を返します。演出側はそのとき属性色を推測せず、安全側へ倒します。
    /// </summary>
    public static class DecidingAttributeLookup
    {
        /// <summary>勝因の属性。分からなければ null です。例外は投げません。</summary>
        public static UnitAttribute? Of(RoundResult result)
        {
            if (result == null || result.PlayerUnit == null || result.CpuUnit == null)
            {
                return null;
            }

            RoundOutcome outcome;

            try
            {
                outcome = BattleRules.ResolveRound(result.PlayerUnit, result.CpuUnit);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (outcome.Winner != result.Winner || outcome.Decision != result.Decision)
            {
                return null;
            }

            return outcome.DecidingAttribute;
        }
    }
}
