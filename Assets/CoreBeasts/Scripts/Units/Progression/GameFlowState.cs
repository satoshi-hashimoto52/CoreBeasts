namespace CoreBeasts.Progression
{
    /// <summary>
    /// シーンをまたぐ直近の報酬・獲得表示。永続値そのもの（コイン・戦績）は試合決着時に Profile へ保存済みで、
    /// ここは「まだ報酬画面で確認していない」という表示用の印だけを持ちます。
    /// </summary>
    public static class GameFlowState
    {
        private static int pendingReward;
        private static int pendingBattles;
        private static int pendingWins;
        private static int pendingDraws;
        private static int pendingLosses;

        public static int PendingReward => pendingReward;
        public static int PendingBattles => pendingBattles;
        public static bool HasPendingReward => pendingReward > 0;

        /// <summary>未確認の試合のうち勝ち・引き分け・負けの数（Phase 7）。</summary>
        public static int PendingWins => pendingWins;
        public static int PendingDraws => pendingDraws;
        public static int PendingLosses => pendingLosses;

        /// <summary>勝敗の分からない従来の記録。報酬額だけを積みます。</summary>
        public static void AddPendingReward(int amount)
        {
            if (amount > 0)
            {
                pendingReward += amount;
                pendingBattles++;
            }
        }

        /// <summary>試合決着時の記録。報酬額と勝敗を積みます（Phase 7）。</summary>
        public static void AddPendingReward(BattleRewardOutcome outcome, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            AddPendingReward(amount);

            switch (outcome)
            {
                case BattleRewardOutcome.Win:
                    pendingWins++;
                    break;
                case BattleRewardOutcome.Draw:
                    pendingDraws++;
                    break;
                default:
                    pendingLosses++;
                    break;
            }
        }

        public static int ConsumePendingReward()
        {
            int value = pendingReward;
            Reset();
            return value;
        }

        /// <summary>
        /// 未確認の報酬を報酬画面用の要約へ写し、未確認の印を解消します（一度だけ表示するため）。
        /// コインは試合決着時に加算済みなので、ここでは何も加算しません。
        /// <paramref name="balanceAfter"/>は現在の残高（加算後）です。
        /// </summary>
        public static RewardSummary ConsumePendingSummary(int balanceAfter)
        {
            RewardSummary summary = new RewardSummary(
                pendingReward, pendingBattles, pendingWins, pendingDraws, pendingLosses, balanceAfter);

            Reset();

            return summary;
        }

        public static void Reset()
        {
            pendingReward = 0;
            pendingBattles = 0;
            pendingWins = 0;
            pendingDraws = 0;
            pendingLosses = 0;
        }
    }
}
