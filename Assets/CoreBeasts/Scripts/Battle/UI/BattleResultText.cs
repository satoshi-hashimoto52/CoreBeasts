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

            return result.Decision == RoundDecision.AttributeAdvantage
                ? text.AttributeWin
                : text.PowerWin;
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
