namespace CoreBeasts.Battle
{
    /// <summary>試合の節目に出す演出（Phase 3）。</summary>
    public enum MatchCueKind
    {
        /// <summary>何も出しません。</summary>
        None = 0,

        /// <summary>初めて 3対3 になった直後、次ラウンドの前に出す FINAL CORE。</summary>
        FinalCore = 1,

        /// <summary>どちらかが4勝目を得たときの CORE BREAK。</summary>
        CoreBreak = 2,
    }

    /// <summary>
    /// 1ラウンドの後に出す試合演出の設計値（Phase 3「勝利コア・FINAL CORE・CORE BREAK」）。
    ///
    /// Unityに依存しない純データです。勝敗もスコアも決めません。
    /// 確定済みのラウンド前後の勝利数を受け取り、「どのピップを光らせるか」
    /// 「FINAL CORE / CORE BREAK を出すか」「何秒かけるか」だけを決めます。
    ///
    /// 時系列（FX ON、試合の勝敗が確定したラウンド。4勝目・早期決着・最終ラウンドの引き分けによる決着）:
    ///   Phase 1・2 の衝突 → 勝利コア獲得（ラウンド勝者がいれば）→ スコア公開 → CORE BREAK
    ///   → 結果バナー → 片付け → 最終結果
    /// 時系列（FX ON、初めて 3対3 になったラウンド）:
    ///   Phase 1・2 の衝突 → 勝利コア獲得 → 結果バナー → 片付け → 次ラウンド準備 → FINAL CORE
    ///
    /// FX OFF では勝利コア獲得を省き、FINAL CORE と CORE BREAK は短い文字表示だけにします。
    /// Phase 1・2 の時間・距離・振動・決着エフェクトには触れません。
    /// </summary>
    public sealed class BattleMatchPresentationPlan
    {
        /// <summary>FINAL CORE を出す勝利数（双方がこの値で並んだとき）。</summary>
        public const int FinalCoreWins = BattleSession.WinsRequired - 1;

        // ---------------- 勝利コア獲得 ----------------

        /// <summary>白金色に点灯する時間（秒）。</summary>
        public const float VictoryCoreLightDuration = 0.08f;

        /// <summary>軽く拡大する時間（秒）。</summary>
        public const float VictoryCoreGrowDuration = 0.12f;

        /// <summary>ピップの定位置へ収束する時間（秒）。</summary>
        public const float VictoryCoreSettleDuration = 0.16f;

        /// <summary>拡大の最大倍率（ピップの大きさに対して）。</summary>
        public const float VictoryCorePeakScale = 1.35f;

        // ---------------- FINAL CORE ----------------

        /// <summary>FX ON の FINAL CORE（コアリング＋2回の明滅＋文字）。</summary>
        public const float FinalCoreDuration = 0.90f;

        /// <summary>FINAL CORE の明滅回数。</summary>
        public const int FinalCorePulses = 2;

        // ---------------- CORE BREAK ----------------

        /// <summary>中心コアが圧縮される時間（秒）。</summary>
        public const float CoreBreakCompressDuration = 0.30f;

        /// <summary>亀裂状の放射線が走る時間（秒）。</summary>
        public const float CoreBreakCrackDuration = 0.25f;

        /// <summary>白金フラッシュの時間（秒）。</summary>
        public const float CoreBreakFlashDuration = 0.15f;

        /// <summary>消滅までの時間（秒）。</summary>
        public const float CoreBreakVanishDuration = 0.35f;

        /// <summary>CORE BREAK の白金フラッシュの最大不透明度。Phase 2 の FxFlash と同じ上限です。</summary>
        public const float CoreBreakFlashPeakAlpha = 0.8f;

        // ---------------- FX OFF ----------------

        /// <summary>FX OFF の FINAL CORE / CORE BREAK。文字だけを出しておく時間（秒）。</summary>
        public const float TextOnlyDuration = 0.60f;

        // ---------------- 上限 ----------------

        /// <summary>FINAL CORE のリングの最大半径（参照解像度上のpx）。</summary>
        public const float FinalCoreMaxRadius = 150f;

        /// <summary>CORE BREAK の最大半径（参照解像度上のpx）。</summary>
        public const float CoreBreakMaxRadius = 220f;

        /// <summary>Phase 3 のどの Graphic も1フレームで超えない頂点数。</summary>
        public const int VertexBudget = 160;

        /// <summary>
        /// Phase 3 で画面を揺らす量（px）。CORE BREAK の強さは半径とフラッシュで出し、
        /// SafeArea・HUD・立ち絵は一切揺らしません。
        /// </summary>
        public const float ShakeAmplitude = 0f;

        private BattleMatchPresentationPlan()
        {
        }

        /// <summary>FX を再生するか。</summary>
        public bool FxEnabled { get; private set; }

        /// <summary>勝利コア獲得の対象側。対象が無ければ <see cref="RoundWinner.Draw"/> です。</summary>
        public RoundWinner VictorySide { get; private set; } = RoundWinner.Draw;

        /// <summary>新しく点灯したピップの番号（0始まり）。対象が無ければ -1 です。</summary>
        public int VictoryPipIndex { get; private set; } = -1;

        /// <summary>新しく増えたピップがあるか（FX設定によらず、スコアの事実として）。</summary>
        public bool HasNewPip => VictoryPipIndex >= 0;

        /// <summary>勝利コア獲得の演出を再生するか。FX OFF では省きます。</summary>
        public bool PlaysVictoryCore => FxEnabled && HasNewPip;

        /// <summary>このラウンドの後に FINAL CORE を出すか。</summary>
        public bool ShowsFinalCore { get; private set; }

        /// <summary>このラウンドで CORE BREAK を出すか。</summary>
        public bool ShowsCoreBreak { get; private set; }

        /// <summary>CORE BREAK を起こした側。出さないときは <see cref="RoundWinner.Draw"/> です。</summary>
        public RoundWinner CoreBreakSide { get; private set; } = RoundWinner.Draw;

        /// <summary>FINAL CORE / CORE BREAK を大きなリングやフラッシュ付きで出すか。false なら文字だけです。</summary>
        public bool PlaysCueGraphics => FxEnabled;

        /// <summary>勝利コア獲得の時間（秒）。再生しないときは0です。</summary>
        public float VictoryCoreDuration =>
            PlaysVictoryCore
                ? VictoryCoreLightDuration + VictoryCoreGrowDuration + VictoryCoreSettleDuration
                : 0f;

        /// <summary>FINAL CORE の時間（秒）。出さないときは0です。</summary>
        public float FinalCoreCueDuration =>
            !ShowsFinalCore ? 0f : FxEnabled ? FinalCoreDuration : TextOnlyDuration;

        /// <summary>このラウンドで試合の勝敗が確定したときの試合勝者。確定していなければ <see cref="RoundWinner.Draw"/>。</summary>
        public RoundWinner MatchWinner { get; private set; } = RoundWinner.Draw;

        /// <summary>CORE BREAK の時間（秒）。出さないときは0です。</summary>
        public float CoreBreakCueDuration =>
            !ShowsCoreBreak ? 0f : FxEnabled ? FullCoreBreakDuration : TextOnlyDuration;

        /// <summary>FX ON の CORE BREAK 全体（圧縮＋亀裂＋フラッシュ＋消滅）。</summary>
        public static float FullCoreBreakDuration =>
            CoreBreakCompressDuration + CoreBreakCrackDuration + CoreBreakFlashDuration + CoreBreakVanishDuration;

        /// <summary>Phase 3 が1ラウンドへ足す時間の合計（秒）。</summary>
        public float AddedDuration => VictoryCoreDuration + FinalCoreCueDuration + CoreBreakCueDuration;

        /// <summary>
        /// ラウンド前後の勝利数と試合の状態から試合演出を決めます。例外は投げません。
        /// 試合の状態は <see cref="BattleSession"/> が判定した値を読むだけで、ここでは数え直しません。
        ///
        /// 勝利コア獲得: 片側だけがちょうど1勝増えたとき、その側の新しいピップ（0始まりで 後の勝利数−1）。
        ///               引き分け・変化なし・不正な増え方では対象なし。
        /// FINAL CORE  : 後が 3対3、前が 3対3 ではなく、この試合でまだ出しておらず、試合が続くとき。
        /// CORE BREAK  : このラウンドで試合が未決着から決着へ移り、試合勝者がいるとき。
        ///               4勝目・残りラウンドで逆転できなくなった早期決着・最終ラウンドの引き分けで
        ///               勝者が決まった場合のどれでも出し、対象は試合全体の勝者側です。
        ///               試合そのものが引き分けなら出しません。
        /// </summary>
        public static BattleMatchPresentationPlan Create(
            int playerWinsBefore,
            int cpuWinsBefore,
            int playerWinsAfter,
            int cpuWinsAfter,
            bool finalCoreAlreadyShown,
            BattleMatchState matchStateBefore,
            BattleMatchState matchStateAfter,
            bool fxEnabled)
        {
            bool matchContinues = matchStateAfter == BattleMatchState.InProgress;

            BattleMatchPresentationPlan plan = new BattleMatchPresentationPlan { FxEnabled = fxEnabled };

            int playerGain = playerWinsAfter - playerWinsBefore;
            int cpuGain = cpuWinsAfter - cpuWinsBefore;

            if (playerGain == 1 && cpuGain == 0 && playerWinsBefore >= 0)
            {
                plan.VictorySide = RoundWinner.Player;
                plan.VictoryPipIndex = playerWinsAfter - 1;
            }
            else if (cpuGain == 1 && playerGain == 0 && cpuWinsBefore >= 0)
            {
                plan.VictorySide = RoundWinner.Cpu;
                plan.VictoryPipIndex = cpuWinsAfter - 1;
            }

            // 未決着 → 決着（勝者あり）へ移ったラウンドだけが CORE BREAK です。
            if (matchStateBefore == BattleMatchState.InProgress)
            {
                if (matchStateAfter == BattleMatchState.PlayerWin)
                {
                    plan.MatchWinner = RoundWinner.Player;
                }
                else if (matchStateAfter == BattleMatchState.CpuWin)
                {
                    plan.MatchWinner = RoundWinner.Cpu;
                }
            }

            if (plan.MatchWinner != RoundWinner.Draw)
            {
                plan.ShowsCoreBreak = true;
                plan.CoreBreakSide = plan.MatchWinner;
            }

            bool tiedAtFinalCore = playerWinsAfter == FinalCoreWins && cpuWinsAfter == FinalCoreWins;
            bool wasTiedBefore = playerWinsBefore == FinalCoreWins && cpuWinsBefore == FinalCoreWins;

            plan.ShowsFinalCore =
                tiedAtFinalCore &&
                !wasTiedBefore &&
                !finalCoreAlreadyShown &&
                matchContinues &&
                !plan.ShowsCoreBreak;

            return plan;
        }

        /// <summary>
        /// 解決済みラウンドの結果と、解決後の勝利数・試合状態から作ります。
        /// 解決前の勝利数は、勝者側から1を引いて戻します（引き分けでは同じ値）。
        /// ラウンドは試合が進行中のときにしか解決できないため、解決前の試合状態は進行中です。
        /// </summary>
        public static BattleMatchPresentationPlan ForRound(
            RoundWinner winner,
            int playerWinsAfter,
            int cpuWinsAfter,
            bool finalCoreAlreadyShown,
            BattleMatchState matchStateAfter,
            bool fxEnabled)
        {
            int playerBefore = winner == RoundWinner.Player ? playerWinsAfter - 1 : playerWinsAfter;
            int cpuBefore = winner == RoundWinner.Cpu ? cpuWinsAfter - 1 : cpuWinsAfter;

            return Create(
                playerBefore, cpuBefore, playerWinsAfter, cpuWinsAfter,
                finalCoreAlreadyShown, BattleMatchState.InProgress, matchStateAfter, fxEnabled);
        }
    }
}
