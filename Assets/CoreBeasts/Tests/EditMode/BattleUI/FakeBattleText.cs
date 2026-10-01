using System.Globalization;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 表示文字列の供給元のテスト実装。
    /// アセットを作らずに済むため、文言の組み立てを純粋なC#として試せます。
    /// </summary>
    internal sealed class FakeBattleText : IBattleTextSource
    {
        public string Battle => "BATTLE";

        public string Deploy => "DEPLOY";

        public string Ready => "READY";

        public string Hidden => "?";

        public string Versus => "VS";

        public string FxOn => "FX ON";

        public string FxOff => "FX OFF";

        public string Settings => "SETTINGS";

        public string Fx => "FX";

        public string On => "ON";

        public string Off => "OFF";

        public string Close => "CLOSE";

        public string Home => "HOME";

        public string Player => "PLAYER";

        public string Cpu => "CPU";

        public string Rematch => "REMATCH";

        public string SquadRequired => "SQUAD REQUIRED";

        public string SquadRequiredHint => "SET 7 CORE BEASTS";

        public string Used => "USED";

        public string AttributeWin => "ATTRIBUTE WIN";

        public string PowerWin => "POWER WIN";

        public string RoundDraw => "DRAW";

        public string PlayerWin => "PLAYER WIN";

        public string CpuWin => "CPU WIN";

        public string MatchDraw => "MATCH DRAW";

        public string FormatRound(int round, int maxRounds)
        {
            return "ROUND " + Number(round) + " / " + Number(maxRounds);
        }

        public string FormatScore(int playerWins, int cpuWins)
        {
            return "PLAYER " + Number(playerWins) + "  -  " + Number(cpuWins) + " CPU";
        }

        public string FormatRemaining(int remaining, int total)
        {
            return "LEFT " + Number(remaining) + " / " + Number(total);
        }

        public string FormatUnitSummary(string attributeSymbol, string powerLine)
        {
            return attributeSymbol + "  " + powerLine;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
