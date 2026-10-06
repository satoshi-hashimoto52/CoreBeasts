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

            // ATTRIBUTE LINK とユニークスキルが POWER 比較に効いたときだけ、基礎値と内訳を分けて出します。
            // 内訳は RoundResult に記録した値を読むだけで、最終値から逆算しません。
            if (result.Decision == RoundDecision.PowerComparison &&
                (result.PlayerPower.HasModifiers || result.CpuPower.HasModifiers))
            {
                return text.FormatLinkPowerDecision(
                    BuildSidePower(result.PlayerPower, text),
                    BuildSidePower(result.CpuPower, text));
            }

            return text.PowerWin;
        }

        /// <summary>
        /// 1陣営の比較値。変化があれば「基礎値（リンク+3／スキル+4／妨害-4）」の形で、その側の数値のすぐ後ろに内訳を付けます。
        /// どちらへ加算・減算されたかを取り違えません。変化が無ければ最終値だけを出します。
        /// </summary>
        private static string BuildSidePower(ComparedPowerBreakdown power, IBattleTextSource text)
        {
            if (!power.HasModifiers)
            {
                return text.FormatUnlinkedPower(power.Final);
            }

            string parts = string.Empty;

            if (power.Link != 0)
            {
                parts = Join(parts, text.FormatLinkPart(power.Link), text);
            }

            if (power.SelfSkill != 0)
            {
                parts = Join(parts, text.FormatSkillPart(power.SelfSkill), text);
            }

            if (power.OpponentPenalty != 0)
            {
                parts = Join(parts, text.FormatPenaltyPart(power.OpponentPenalty), text);
            }

            return text.FormatPowerBreakdown(power.Base, parts);
        }

        private static string Join(string parts, string part, IBattleTextSource text)
        {
            return parts.Length == 0 ? part : parts + text.BreakdownSeparator + part;
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
