using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// Phase 3「勝利コア・FINAL CORE・CORE BREAK」の演出設計値。Unityへ依存しないため、そのまま実行できます。
    ///
    /// どのピップを光らせるか、FINAL CORE と CORE BREAK をいつ出すか、FX OFF の短縮、
    /// 時間・頂点数・振幅の上限、そして Phase 1・2 の定数が変わっていないことを固定します。
    /// </summary>
    public sealed class BattleMatchPresentationPlanTests
    {
        private static BattleMatchPresentationPlan Plan(
            int pb, int cb, int pa, int ca, bool shown = false,
            BattleMatchState after = BattleMatchState.InProgress, bool fx = true)
        {
            return BattleMatchPresentationPlan.Create(
                pb, cb, pa, ca, shown, BattleMatchState.InProgress, after, fx);
        }

        // ---------------- 勝利コア獲得 ----------------

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        public void APlayerWinLightsOnlyTheNewPlayerPip(int before, int after)
        {
            BattleMatchPresentationPlan plan = Plan(before, 2, after, 2,
                after: after < BattleSession.WinsRequired ? BattleMatchState.InProgress : BattleMatchState.PlayerWin);

            Assert.That(plan.VictorySide, Is.EqualTo(RoundWinner.Player));
            Assert.That(plan.VictoryPipIndex, Is.EqualTo(after - 1), "新しく点灯したピップだけです。");
            Assert.That(plan.PlaysVictoryCore, Is.True);
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        public void ACpuWinLightsOnlyTheNewCpuPip(int before, int after)
        {
            BattleMatchPresentationPlan plan = Plan(1, before, 1, after,
                after: after < BattleSession.WinsRequired ? BattleMatchState.InProgress : BattleMatchState.CpuWin);

            Assert.That(plan.VictorySide, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(plan.VictoryPipIndex, Is.EqualTo(after - 1));
            Assert.That(plan.PlaysVictoryCore, Is.True);
        }

        [TestCase(0, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 3)]
        public void ADrawLightsNothingAndNeverBreaksTheCore(int player, int cpu)
        {
            BattleMatchPresentationPlan plan = Plan(player, cpu, player, cpu);

            Assert.That(plan.HasNewPip, Is.False);
            Assert.That(plan.VictoryPipIndex, Is.EqualTo(-1));
            Assert.That(plan.VictorySide, Is.EqualTo(RoundWinner.Draw));
            Assert.That(plan.PlaysVictoryCore, Is.False);
            Assert.That(plan.ShowsCoreBreak, Is.False);
            Assert.That(plan.VictoryCoreDuration, Is.EqualTo(0f));
        }

        [Test]
        public void ForRoundRebuildsTheScoreBeforeTheRound()
        {
            BattleMatchPresentationPlan player = BattleMatchPresentationPlan.ForRound(RoundWinner.Player, 3, 1, false, BattleMatchState.InProgress, true);
            BattleMatchPresentationPlan cpu = BattleMatchPresentationPlan.ForRound(RoundWinner.Cpu, 3, 3, false, BattleMatchState.InProgress, true);
            BattleMatchPresentationPlan draw = BattleMatchPresentationPlan.ForRound(RoundWinner.Draw, 3, 3, false, BattleMatchState.InProgress, true);

            Assert.That(player.VictorySide, Is.EqualTo(RoundWinner.Player));
            Assert.That(player.VictoryPipIndex, Is.EqualTo(2));
            Assert.That(cpu.VictoryPipIndex, Is.EqualTo(2));
            Assert.That(cpu.ShowsFinalCore, Is.True, "2対3→3対3（CPUの勝利）で初めて並びました。");
            Assert.That(draw.HasNewPip, Is.False);
            Assert.That(draw.ShowsFinalCore, Is.False, "3対3のまま引き分けても再表示しません。");
        }

        [Test]
        public void MalformedScoreChangesNeverThrowAndLightNothing()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(Plan(0, 0, 2, 0).HasNewPip, Is.False, "2勝増えることはありません。");
                Assert.That(Plan(1, 1, 2, 2).HasNewPip, Is.False, "両側が同時に増えることはありません。");
                Assert.That(Plan(2, 0, 1, 0).HasNewPip, Is.False, "減ることはありません。");
                Assert.That(Plan(-1, 0, 0, 0).HasNewPip, Is.False);
                Assert.That(Plan(0, 0, 2, 0).ShowsCoreBreak, Is.False);
            });
        }

        // ---------------- FINAL CORE ----------------

        [Test]
        public void FinalCoreAppearsOnlyWhenTheScoreFirstReachesThreeAll()
        {
            Assert.That(Plan(2, 3, 3, 3).ShowsFinalCore, Is.True, "PLAYER の勝利で 3対3");
            Assert.That(Plan(3, 2, 3, 3).ShowsFinalCore, Is.True, "CPU の勝利で 3対3");

            Assert.That(Plan(2, 2, 3, 2).ShowsFinalCore, Is.False);
            Assert.That(Plan(1, 2, 2, 2).ShowsFinalCore, Is.False);
            Assert.That(Plan(3, 3, 4, 3).ShowsFinalCore, Is.False, "4勝目は CORE BREAK です。");
        }

        [Test]
        public void DrawsAtThreeAllNeverReplayFinalCore()
        {
            Assert.That(Plan(3, 3, 3, 3).ShowsFinalCore, Is.False, "3対3のまま引き分け");
            Assert.That(Plan(3, 3, 3, 3, shown: false).ShowsFinalCore, Is.False, "記録が無くても、前が 3対3 なら出しません。");
        }

        [Test]
        public void FinalCoreAppearsAtMostOncePerMatch()
        {
            Assert.That(Plan(2, 3, 3, 3, shown: true).ShowsFinalCore, Is.False, "この試合で出し済み");
        }

        [Test]
        public void FinalCoreNeedsANextRound()
        {
            Assert.That(Plan(2, 3, 3, 3, after: BattleMatchState.Draw).ShowsFinalCore, Is.False, "次ラウンドが無ければ出しません。");
        }

        // ---------------- CORE BREAK ----------------

        [TestCase(3, 3, 4, 3, RoundWinner.Player)]
        [TestCase(3, 3, 3, 4, RoundWinner.Cpu)]
        [TestCase(3, 1, 4, 1, RoundWinner.Player)]
        [TestCase(0, 3, 0, 4, RoundWinner.Cpu)]
        [TestCase(3, 0, 4, 0, RoundWinner.Player)]
        public void TheFourthWinAlwaysBreaksTheCore(int pb, int cb, int pa, int ca, RoundWinner side)
        {
            BattleMatchPresentationPlan plan = Plan(pb, cb, pa, ca,
                after: side == RoundWinner.Player ? BattleMatchState.PlayerWin : BattleMatchState.CpuWin);

            Assert.That(plan.ShowsCoreBreak, Is.True);
            Assert.That(plan.CoreBreakSide, Is.EqualTo(side));
            Assert.That(plan.PlaysVictoryCore, Is.True, "勝利コア獲得の後に CORE BREAK です。");
            Assert.That(plan.ShowsFinalCore, Is.False);
        }

        // ---------------- 試合の勝敗確定（早期決着・引き分けによる決着） ----------------

        [Test]
        public void APlayerEarlyClinchAtThreeToOneBreaksTheCoreForThePlayer()
        {
            // W/L/W/D/D/W: 6ラウンド終了で 3対1、残り1ラウンドで CPU は追いつけません。
            Assert.That(BattleSession.EvaluateMatchState(3, 1, 6, BattleSession.MaxRounds), Is.EqualTo(BattleMatchState.PlayerWin),
                "早期決着は既存の判定です。");

            BattleMatchPresentationPlan plan = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Player, 3, 1, false, BattleMatchState.PlayerWin, true);

            Assert.That(plan.ShowsCoreBreak, Is.True, "4勝に届かない早期決着でも CORE BREAK です。");
            Assert.That(plan.CoreBreakSide, Is.EqualTo(RoundWinner.Player));
            Assert.That(plan.MatchWinner, Is.EqualTo(RoundWinner.Player));
            Assert.That(plan.PlaysVictoryCore, Is.True, "ラウンド勝者の勝利コアの後に CORE BREAK です。");
            Assert.That(plan.VictoryPipIndex, Is.EqualTo(2));
            Assert.That(plan.ShowsFinalCore, Is.False);
        }

        [Test]
        public void ACpuEarlyClinchAtOneToThreeBreaksTheCoreForTheCpu()
        {
            Assert.That(BattleSession.EvaluateMatchState(1, 3, 6, BattleSession.MaxRounds), Is.EqualTo(BattleMatchState.CpuWin));

            BattleMatchPresentationPlan plan = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Cpu, 1, 3, false, BattleMatchState.CpuWin, true);

            Assert.That(plan.ShowsCoreBreak, Is.True);
            Assert.That(plan.CoreBreakSide, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(plan.PlaysVictoryCore, Is.True);
        }

        [Test]
        public void AFinalDrawThatConfirmsTheMatchWinnerBreaksTheCoreForTheMatchWinner()
        {
            // 最終ラウンドの引き分けで 3対2 のまま終わると、試合は PLAYER の勝ちです。
            Assert.That(BattleSession.EvaluateMatchState(3, 2, 7, BattleSession.MaxRounds), Is.EqualTo(BattleMatchState.PlayerWin));

            BattleMatchPresentationPlan plan = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Draw, 3, 2, false, BattleMatchState.PlayerWin, true);

            Assert.That(plan.ShowsCoreBreak, Is.True, "引き分けのラウンドでも、試合勝者が決まれば CORE BREAK です。");
            Assert.That(plan.CoreBreakSide, Is.EqualTo(RoundWinner.Player), "対象はラウンド勝者ではなく試合勝者です。");
            Assert.That(plan.HasNewPip, Is.False, "ラウンドは引き分けなので勝利コアはありません。");
            Assert.That(plan.PlaysVictoryCore, Is.False);

            BattleMatchPresentationPlan cpu = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Draw, 1, 2, false, BattleMatchState.CpuWin, true);

            Assert.That(cpu.ShowsCoreBreak, Is.True);
            Assert.That(cpu.CoreBreakSide, Is.EqualTo(RoundWinner.Cpu));
        }

        [Test]
        public void ADrawnMatchNeverBreaksTheCore()
        {
            Assert.That(BattleSession.EvaluateMatchState(3, 3, 7, BattleSession.MaxRounds), Is.EqualTo(BattleMatchState.Draw));

            BattleMatchPresentationPlan byDraw = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Draw, 3, 3, false, BattleMatchState.Draw, true);
            BattleMatchPresentationPlan byWin = BattleMatchPresentationPlan.ForRound(
                RoundWinner.Cpu, 2, 2, false, BattleMatchState.Draw, true);

            Assert.That(byDraw.ShowsCoreBreak, Is.False, "試合の引き分けでは CORE BREAK を出しません。");
            Assert.That(byWin.ShowsCoreBreak, Is.False);
            Assert.That(byWin.PlaysVictoryCore, Is.True, "ラウンド勝者の勝利コアは出ます。");
            Assert.That(byDraw.MatchWinner, Is.EqualTo(RoundWinner.Draw));
        }

        [Test]
        public void TheCoreBreaksOnlyOnTheRoundThatDecidesTheMatch()
        {
            // すでに決着していれば（進行中から移っていなければ）出しません。
            Assert.That(
                BattleMatchPresentationPlan.Create(4, 1, 4, 1, false, BattleMatchState.PlayerWin, BattleMatchState.PlayerWin, true).ShowsCoreBreak,
                Is.False);

            Assert.That(
                BattleMatchPresentationPlan.Create(2, 1, 3, 1, false, BattleMatchState.InProgress, BattleMatchState.InProgress, true).ShowsCoreBreak,
                Is.False, "まだ決着していません。");
        }

        [TestCase(0, 0, 1, 0)]
        [TestCase(2, 2, 3, 2)]
        [TestCase(2, 3, 3, 3)]
        public void EarlierWinsNeverBreakTheCore(int pb, int cb, int pa, int ca)
        {
            Assert.That(Plan(pb, cb, pa, ca).ShowsCoreBreak, Is.False);
        }

        // ---------------- FX OFF ----------------

        [Test]
        public void FxOffSkipsTheGraphicsButKeepsTheCuesAsShortText()
        {
            BattleMatchPresentationPlan victory = Plan(1, 0, 2, 0, fx: false);
            BattleMatchPresentationPlan finalCore = Plan(2, 3, 3, 3, fx: false);
            BattleMatchPresentationPlan coreBreak = Plan(3, 3, 4, 3, after: BattleMatchState.PlayerWin, fx: false);

            Assert.That(victory.HasNewPip, Is.True, "スコアの事実は変わりません。");
            Assert.That(victory.PlaysVictoryCore, Is.False, "勝利コア獲得の演出は省きます。");
            Assert.That(victory.VictoryCoreDuration, Is.EqualTo(0f));

            Assert.That(finalCore.ShowsFinalCore, Is.True);
            Assert.That(finalCore.PlaysCueGraphics, Is.False, "リングと明滅は出しません。");
            Assert.That(finalCore.FinalCoreCueDuration, Is.EqualTo(BattleMatchPresentationPlan.TextOnlyDuration));

            Assert.That(coreBreak.ShowsCoreBreak, Is.True);
            Assert.That(coreBreak.PlaysCueGraphics, Is.False, "圧縮・亀裂・フラッシュは出しません。");
            Assert.That(coreBreak.CoreBreakCueDuration, Is.EqualTo(BattleMatchPresentationPlan.TextOnlyDuration));
            Assert.That(coreBreak.VictoryCoreDuration, Is.EqualTo(0f));

            Assert.That(coreBreak.AddedDuration, Is.LessThan(Plan(3, 3, 4, 3, after: BattleMatchState.PlayerWin).AddedDuration));
        }

        // ---------------- 上限 ----------------

        [Test]
        public void EveryCueStaysWithinItsTimeLimit()
        {
            BattleMatchPresentationPlan victory = Plan(0, 0, 1, 0);

            Assert.That(victory.VictoryCoreDuration, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That(victory.VictoryCoreDuration, Is.LessThanOrEqualTo(0.4f));
            Assert.That(BattleMatchPresentationPlan.FinalCoreDuration, Is.InRange(0.6f, 1.0f));
            Assert.That(BattleMatchPresentationPlan.FullCoreBreakDuration, Is.EqualTo(1.05f).Within(0.0001f));
            Assert.That(BattleMatchPresentationPlan.FullCoreBreakDuration, Is.LessThanOrEqualTo(1.2f));
            Assert.That(BattleMatchPresentationPlan.TextOnlyDuration, Is.LessThanOrEqualTo(0.6f));

            // 最も長いラウンド（4勝目）でも、Phase 3 が足すのは 1.5 秒以内です。
            Assert.That(Plan(3, 3, 4, 3, after: BattleMatchState.PlayerWin).AddedDuration, Is.LessThanOrEqualTo(1.5f));
        }

        [Test]
        public void TheCueGraphicsStayWithinTheirVertexRadiusAndShakeLimits()
        {
            Assert.That(BattleMatchPresentationPlan.VertexBudget, Is.LessThanOrEqualTo(160));
            Assert.That(BattleMatchPresentationPlan.FinalCoreMaxRadius, Is.LessThanOrEqualTo(180f));
            Assert.That(BattleMatchPresentationPlan.CoreBreakMaxRadius, Is.LessThanOrEqualTo(240f));
            Assert.That(BattleMatchPresentationPlan.VictoryCorePeakScale, Is.InRange(1.1f, 1.5f));
            Assert.That(BattleMatchPresentationPlan.CoreBreakFlashPeakAlpha, Is.LessThanOrEqualTo(0.8f));
            Assert.That(BattleMatchPresentationPlan.ShakeAmplitude, Is.EqualTo(0f), "Phase 3 は何も揺らしません。");
        }

        [Test]
        public void TheCueThresholdsFollowTheExistingWinCondition()
        {
            Assert.That(BattleSession.WinsRequired, Is.EqualTo(4), "勝利条件は変えていません。");
            Assert.That(BattleMatchPresentationPlan.FinalCoreWins, Is.EqualTo(3));
        }

        // ---------------- Phase 1・2 を変えない ----------------

        [Test]
        public void ThePhase1And2ConstantsAreUnchanged()
        {
            Assert.That(BattleRoundPresentationPlan.StandardImpactHoldDuration, Is.EqualTo(0.06f));
            Assert.That(BattleRoundPresentationPlan.StandardLiftDuration, Is.EqualTo(0.10f));
            Assert.That(BattleRoundPresentationPlan.StandardApproachDuration, Is.EqualTo(0.22f));
            Assert.That(BattleRoundPresentationPlan.StandardKnockbackDuration, Is.EqualTo(0.20f));
            Assert.That(BattleRoundPresentationPlan.StandardLandingDuration, Is.EqualTo(0.14f));
            Assert.That(BattleRoundPresentationPlan.StandardReturnDuration, Is.EqualTo(0.18f));
            Assert.That(BattleRoundPresentationPlan.StandardShakeDuration, Is.EqualTo(0.12f));
            Assert.That(BattleRoundPresentationPlan.StandardShakeAmplitude, Is.EqualTo(5f));
            Assert.That(BattleRoundPresentationPlan.StandardKnockbackDistance, Is.EqualTo(40f));
            Assert.That(BattleRoundPresentationPlan.StandardWinnerRecoilDistance, Is.EqualTo(10f));
            Assert.That(BattleRoundPresentationPlan.StandardDrawRecoilDistance, Is.EqualTo(18f));

            Assert.That(AttributeEffectProfile.Red.Duration, Is.EqualTo(0.24f));
            Assert.That(AttributeEffectProfile.Blue.Duration, Is.EqualTo(0.30f));
            Assert.That(AttributeEffectProfile.Green.Duration, Is.EqualTo(0.30f));
            Assert.That(AttributeEffectProfile.Power.Duration, Is.EqualTo(0.26f));
            Assert.That(AttributeEffectProfile.Core.Duration, Is.EqualTo(0.22f));
            Assert.That(AttributeEffectProfile.Draw.Duration, Is.EqualTo(0.28f));
            Assert.That(AttributeEffectProfile.Red.MaxVertices, Is.EqualTo(44));
            Assert.That(AttributeEffectProfile.Blue.MaxVertices, Is.EqualTo(128));
            Assert.That(AttributeEffectProfile.Green.MaxVertices, Is.EqualTo(80));
            Assert.That(AttributeEffectProfile.Power.MaxVertices, Is.EqualTo(81));
            Assert.That(AttributeEffectProfile.Core.MaxVertices, Is.EqualTo(61));
            Assert.That(AttributeEffectProfile.Draw.MaxVertices, Is.EqualTo(80));
            Assert.That(AttributeEffectProfile.Blue.MaxRadius, Is.EqualTo(170f));
        }
    }
}
