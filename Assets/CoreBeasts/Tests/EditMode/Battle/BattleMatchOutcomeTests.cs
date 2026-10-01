using System;

using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// マッチの決着判定。<see cref="BattleSession.EvaluateMatchState"/>は純粋関数なので、
    /// セッションを組み立てずにそのまま確かめられます。
    ///
    /// ルール:
    ///   最大7ラウンド。先に4勝した側はその時点で勝ち。
    ///   4勝へ届かなくても、相手が残り全勝しても追いつけないならその時点で勝ち。
    ///   使い切ったら勝利数を比べ、同数だけが引き分け。
    ///   ラウンドの引き分けはどちらの勝利数にも入れません。
    /// </summary>
    public sealed class BattleMatchOutcomeTests
    {
        private static BattleMatchState Evaluate(
            int playerWins, int cpuWins, int completedRounds)
        {
            return BattleSession.EvaluateMatchState(
                playerWins, cpuWins, completedRounds, BattleSession.MaxRounds);
        }

        /// <summary>残りラウンド数から完了ラウンド数を出します。読みやすさのためです。</summary>
        private static BattleMatchState WithRemaining(
            int playerWins, int cpuWins, int remaining)
        {
            return Evaluate(playerWins, cpuWins, BattleSession.MaxRounds - remaining);
        }

        // ---------------- 4勝到達 ----------------

        [TestCase(4, 0, 3, BattleMatchState.PlayerWin)]
        [TestCase(0, 4, 3, BattleMatchState.CpuWin)]
        [TestCase(4, 3, 0, BattleMatchState.PlayerWin)]
        [TestCase(3, 4, 0, BattleMatchState.CpuWin)]
        [TestCase(4, 2, 1, BattleMatchState.PlayerWin)]
        public void ReachingFourWinsEndsTheMatchImmediately(
            int playerWins, int cpuWins, int remaining, BattleMatchState expected)
        {
            Assert.That(
                WithRemaining(playerWins, cpuWins, remaining),
                Is.EqualTo(expected),
                playerWins + "対" + cpuWins + "・残り" + remaining);
        }

        // ---------------- 逆転不能 ----------------

        [TestCase(3, 0, 2, BattleMatchState.PlayerWin)]
        [TestCase(0, 3, 2, BattleMatchState.CpuWin)]
        [TestCase(2, 0, 1, BattleMatchState.PlayerWin)]
        [TestCase(0, 2, 1, BattleMatchState.CpuWin)]
        [TestCase(3, 1, 1, BattleMatchState.PlayerWin)]
        [TestCase(1, 3, 1, BattleMatchState.CpuWin)]
        public void AnUnreachableLeadEndsTheMatchEarly(
            int playerWins, int cpuWins, int remaining, BattleMatchState expected)
        {
            Assert.That(
                WithRemaining(playerWins, cpuWins, remaining),
                Is.EqualTo(expected),
                playerWins + "対" + cpuWins + "・残り" + remaining +
                " は相手が残り全勝しても届きません。");
        }

        // ---------------- 継続 ----------------

        [TestCase(3, 0, 3)]
        [TestCase(0, 3, 3)]
        [TestCase(3, 2, 1)]
        [TestCase(2, 3, 1)]
        [TestCase(2, 2, 1)]
        [TestCase(2, 0, 2)]
        [TestCase(0, 0, 7)]
        [TestCase(1, 1, 5)]
        public void TheMatchContinuesWhileTheOpponentCanStillCatchUp(
            int playerWins, int cpuWins, int remaining)
        {
            Assert.That(
                WithRemaining(playerWins, cpuWins, remaining),
                Is.EqualTo(BattleMatchState.InProgress),
                playerWins + "対" + cpuWins + "・残り" + remaining +
                " はまだ同点へ追いつけます。");
        }

        [Test]
        public void LeadingAloneNeverEndsTheMatch()
        {
            // 3対0でも、残り3あればCPUは3対3へ追いつけます。
            Assert.That(
                WithRemaining(3, 0, 3),
                Is.EqualTo(BattleMatchState.InProgress));

            // 1つ減ると届かなくなります。
            Assert.That(
                WithRemaining(3, 0, 2),
                Is.EqualTo(BattleMatchState.PlayerWin));
        }

        [Test]
        public void TieingIsStillEnoughToKeepPlaying()
        {
            // ちょうど同点まで届く場合は終わりません（等号では終わらない）。
            Assert.That(
                WithRemaining(3, 2, 1),
                Is.EqualTo(BattleMatchState.InProgress));

            Assert.That(
                WithRemaining(2, 0, 2),
                Is.EqualTo(BattleMatchState.InProgress));
        }

        // ---------------- 7ラウンド終了 ----------------

        [TestCase(3, 2, 2, BattleMatchState.PlayerWin)]
        [TestCase(2, 3, 2, BattleMatchState.CpuWin)]
        [TestCase(3, 3, 1, BattleMatchState.Draw)]
        [TestCase(2, 2, 3, BattleMatchState.Draw)]
        [TestCase(1, 0, 6, BattleMatchState.PlayerWin)]
        [TestCase(0, 1, 6, BattleMatchState.CpuWin)]
        [TestCase(0, 0, 7, BattleMatchState.Draw)]
        [TestCase(2, 1, 4, BattleMatchState.PlayerWin)]
        public void AfterTheLastRoundTheWinCountsAreSimplyCompared(
            int playerWins, int cpuWins, int draws, BattleMatchState expected)
        {
            Assume.That(
                playerWins + cpuWins + draws,
                Is.EqualTo(BattleSession.MaxRounds),
                "テストデータが7ラウンドぶんになっていません。");

            Assert.That(
                Evaluate(playerWins, cpuWins, BattleSession.MaxRounds),
                Is.EqualTo(expected),
                playerWins + "勝" + cpuWins + "敗" + draws + "分");
        }

        [Test]
        public void RoundDrawsAreNeverCountedAsWins()
        {
            // 引き分けが何回あっても、勝利数の差だけで決まります。
            for (int draws = 0; draws <= 5; draws++)
            {
                int playerWins = 1;
                int cpuWins = BattleSession.MaxRounds - draws - playerWins;

                if (cpuWins < 0 || playerWins + cpuWins > BattleSession.MaxRounds)
                {
                    continue;
                }

                BattleMatchState expected = playerWins > cpuWins
                    ? BattleMatchState.PlayerWin
                    : (cpuWins > playerWins
                        ? BattleMatchState.CpuWin
                        : BattleMatchState.Draw);

                Assert.That(
                    Evaluate(playerWins, cpuWins, BattleSession.MaxRounds),
                    Is.EqualTo(expected),
                    "引き分け " + draws + " 回のとき");
            }
        }

        [Test]
        public void ThreeTwoAndTwoDrawsIsAPlayerWinNotADraw()
        {
            // 「PLAYER 3 - 2 CPU」で DRAW を出してはいけません。
            Assert.That(
                Evaluate(3, 2, BattleSession.MaxRounds),
                Is.EqualTo(BattleMatchState.PlayerWin));

            Assert.That(
                Evaluate(3, 2, BattleSession.MaxRounds),
                Is.Not.EqualTo(BattleMatchState.Draw));
        }

        [Test]
        public void OnlyAnEqualScoreIsADraw()
        {
            for (int playerWins = 0; playerWins <= 3; playerWins++)
            {
                for (int cpuWins = 0; cpuWins <= 3; cpuWins++)
                {
                    if (playerWins + cpuWins > BattleSession.MaxRounds)
                    {
                        continue;
                    }

                    BattleMatchState state =
                        Evaluate(playerWins, cpuWins, BattleSession.MaxRounds);

                    Assert.That(
                        state == BattleMatchState.Draw,
                        Is.EqualTo(playerWins == cpuWins),
                        playerWins + "対" + cpuWins + " の引き分け判定が誤りです。");
                }
            }
        }

        // ---------------- 境界 ----------------

        [Test]
        public void NoRoundsPlayedYetIsAlwaysInProgress()
        {
            Assert.That(
                Evaluate(0, 0, 0),
                Is.EqualTo(BattleMatchState.InProgress));
        }

        [Test]
        public void TheLastRoundLeavesNoRemainingRounds()
        {
            // completedRounds == maxRounds なので残り 0。勝数比較へ落ちます。
            Assert.That(
                Evaluate(2, 1, BattleSession.MaxRounds),
                Is.EqualTo(BattleMatchState.PlayerWin));

            Assert.That(
                Evaluate(1, 2, BattleSession.MaxRounds),
                Is.EqualTo(BattleMatchState.CpuWin));
        }

        [Test]
        public void EveryConsistentCombinationIsDecidedExactlyOnce()
        {
            // 勝数とDraw数の合計が completedRounds と一致する組み合わせを総当たりし、
            // 「残りがあるのに決着した」「使い切ったのに進行中」が無いことを見ます。
            for (int completed = 0; completed <= BattleSession.MaxRounds; completed++)
            {
                for (int playerWins = 0; playerWins <= completed; playerWins++)
                {
                    for (int cpuWins = 0; cpuWins + playerWins <= completed; cpuWins++)
                    {
                        int draws = completed - playerWins - cpuWins;

                        Assume.That(draws, Is.GreaterThanOrEqualTo(0));

                        BattleMatchState state = Evaluate(playerWins, cpuWins, completed);

                        int remaining = BattleSession.MaxRounds - completed;

                        if (remaining == 0)
                        {
                            Assert.That(
                                state,
                                Is.Not.EqualTo(BattleMatchState.InProgress),
                                "使い切ったのに進行中です。");
                        }

                        if (state == BattleMatchState.Draw)
                        {
                            Assert.That(
                                playerWins,
                                Is.EqualTo(cpuWins),
                                "同数でないのに引き分けになりました。");

                            Assert.That(
                                remaining,
                                Is.Zero,
                                "ラウンドが残っているのに引き分けになりました。");
                        }
                    }
                }
            }
        }

        [TestCase(-1, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 0, -1)]
        public void NegativeInputsAreRejected(
            int playerWins, int cpuWins, int completedRounds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Evaluate(playerWins, cpuWins, completedRounds),
                "負の値を黙って受け入れてはいけません。");
        }

        [Test]
        public void MoreCompletedRoundsThanTheMaximumIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Evaluate(0, 0, BattleSession.MaxRounds + 1));
        }

        [Test]
        public void MoreWinsThanCompletedRoundsIsRejected()
        {
            // 引き分けは勝利数へ入れないため、勝数の合計は完了数を超えません。
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(3, 3, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(1, 0, 0));
        }

        [Test]
        public void AMaximumOfZeroRoundsIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleSession.EvaluateMatchState(0, 0, 0, 0));
        }

        [Test]
        public void TheRuleIsIndependentOfTheMatchLength()
        {
            // 最大ラウンド数を変えても同じ考え方で決まります。
            // 全3ラウンド。1対0で残り2なら、まだ同点へ追いつけます。
            Assert.That(
                BattleSession.EvaluateMatchState(1, 0, 1, 3),
                Is.EqualTo(BattleMatchState.InProgress));

            // 2対0で残り1になると、CPUは最大1勝しかできず届きません。
            Assert.That(
                BattleSession.EvaluateMatchState(2, 0, 2, 3),
                Is.EqualTo(BattleMatchState.PlayerWin));

            // 使い切れば勝数比較です。
            Assert.That(
                BattleSession.EvaluateMatchState(1, 1, 3, 3),
                Is.EqualTo(BattleMatchState.Draw));
        }
    }
}
