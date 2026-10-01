namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面が使う表示文字列の供給元。
    ///
    /// 文字列そのものはアセット（<see cref="BattleTextCatalog"/>）側が持ち、
    /// コードは内部enumと数値だけを扱います。海外展開時はアセットを差し替えます。
    /// この境界があるため、文言を組み立てる処理はUnityへ依存せずテストできます。
    /// </summary>
    public interface IBattleTextSource
    {
        /// <summary>画面名。</summary>
        string Battle { get; }

        /// <summary>決定ボタン。</summary>
        string Deploy { get; }

        /// <summary>CPUが選出を済ませたことだけを示す表示。個体は特定できません。</summary>
        string Ready { get; }

        /// <summary>非公開の枠に出す記号。</summary>
        string Hidden { get; }

        /// <summary>対戦表示の区切り。</summary>
        string Versus { get; }

        /// <summary>演出ONの表示。</summary>
        string FxOn { get; }

        /// <summary>演出OFFの表示。</summary>
        string FxOff { get; }

        /// <summary>設定パネルの見出し。</summary>
        string Settings { get; }

        /// <summary>設定パネルの演出項目名。</summary>
        string Fx { get; }

        /// <summary>ONの値表示。項目名と組にして使います。</summary>
        string On { get; }

        /// <summary>OFFの値表示。項目名と組にして使います。</summary>
        string Off { get; }

        /// <summary>設定パネルを閉じるボタン。</summary>
        string Close { get; }

        /// <summary>画面を離れるボタン。設定パネルへ入れます。</summary>
        string Home { get; }

        /// <summary>自陣の呼び名。</summary>
        string Player { get; }

        /// <summary>敵陣の呼び名。</summary>
        string Cpu { get; }

        /// <summary>再戦ボタン。</summary>
        string Rematch { get; }

        /// <summary>編成不足の見出し。</summary>
        string SquadRequired { get; }

        /// <summary>編成不足のときに何をすればよいか。</summary>
        string SquadRequiredHint { get; }

        /// <summary>使用済みの印。</summary>
        string Used { get; }

        /// <summary>属性相性で決着した表示。</summary>
        string AttributeWin { get; }

        /// <summary>POWER比較で決着した表示。</summary>
        string PowerWin { get; }

        /// <summary>引き分けの表示。</summary>
        string RoundDraw { get; }

        /// <summary>プレイヤー勝利の表示。</summary>
        string PlayerWin { get; }

        /// <summary>CPU勝利の表示。</summary>
        string CpuWin { get; }

        /// <summary>マッチ引き分けの表示。</summary>
        string MatchDraw { get; }

        /// <summary>ラウンド表示（例: ROUND 3 / 7）。</summary>
        string FormatRound(int round, int maxRounds);

        /// <summary>勝利数表示（例: PLAYER 2  -  1 CPU）。</summary>
        string FormatScore(int playerWins, int cpuWins);

        /// <summary>残り枚数表示（例: LEFT 5 / 7）。個体の内容は含みません。</summary>
        string FormatRemaining(int remaining, int total);

        /// <summary>属性とPOWERを1行にまとめた表示（例: R/B  76）。</summary>
        string FormatUnitSummary(string attributeSymbol, string powerLine);
    }
}
