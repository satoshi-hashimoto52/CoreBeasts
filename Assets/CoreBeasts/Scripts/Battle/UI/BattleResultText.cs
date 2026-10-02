namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ラウンド結果・最終結果の文言を組み立てます。
    ///
    /// 勝敗の再判定は一切しません。<see cref="RoundResult"/>と
    /// <see cref="BattleMatchState"/>が持つ確定済みの値を、表示文字列へ写すだけです。
    /// </summary>
    public static class BattleResultText
    {
        /// <summary>
        /// 決着理由の表示。引き分けは理由ではなく引き分けとして出します。
        /// </summary>
        public static string BuildDecision(RoundResult result, IBattleTextSource text)
        {
            if (result == null || text == null)
            {
                return string.Empty;
            }

            if (result.IsDraw)
            {
                return text.RoundDraw;
            }

            if (result.Decision == RoundDecision.AttributeAdvantage)
            {
                return text.AttributeWin;
            }

            // ATTRIBUTE LINK が POWER 比較に効いたときだけ、基礎値と加算値を分けて出します。
            if (result.Decision == RoundDecision.PowerComparison &&
                (result.PlayerLink.BonusPower > 0 || result.CpuLink.BonusPower > 0))
            {
                return text.FormatLinkPowerDecision(
                    BuildLinkedPower(result.PlayerComparedPower, result.PlayerLink.BonusPower, text),
                    BuildLinkedPower(result.CpuComparedPower, result.CpuLink.BonusPower, text));
            }

            return text.PowerWin;
        }

        /// <summary>
        /// 比較した値（LINK 反映後）を、基礎値と加算値へ分けます。加算値はその側の数値のすぐ後ろに付くので、
        /// どちらへ加算されたかを取り違えません。
        /// LINK は各属性へ同じ値を足すため、基礎値は比較値からボーナスを引いた値です。
        /// </summary>
        private static string BuildLinkedPower(int comparedPower, int bonusPower, IBattleTextSource text)
        {
            if (bonusPower <= 0)
            {
                return text.FormatUnlinkedPower(comparedPower);
            }

            return text.FormatLinkedPower(comparedPower - bonusPower, bonusPower);
        }

        /// <summary>ラウンド勝者の表示。</summary>
        public static string BuildRoundWinner(RoundWinner winner, IBattleTextSource text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            switch (winner)
            {
                case RoundWinner.Player:
                    return text.PlayerWin;

                case RoundWinner.Cpu:
                    return text.CpuWin;

                default:
                    return text.RoundDraw;
            }
        }

        /// <summary>最終結果の表示。</summary>
        public static string BuildMatchResult(BattleMatchState state, IBattleTextSource text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            switch (state)
            {
                case BattleMatchState.PlayerWin:
                    return text.PlayerWin;

                case BattleMatchState.CpuWin:
                    return text.CpuWin;

                default:
                    return text.MatchDraw;
            }
        }
    }
}
