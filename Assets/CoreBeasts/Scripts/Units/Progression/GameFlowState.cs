namespace CoreBeasts.Progression
{
    /// <summary>シーンをまたぐ直近の報酬・獲得表示。永続値そのものはProfileへ保存します。</summary>
    public static class GameFlowState
    {
        private static int pendingReward;
        private static int pendingBattles;

        public static int PendingReward => pendingReward;
        public static int PendingBattles => pendingBattles;
        public static bool HasPendingReward => pendingReward > 0;

        public static void AddPendingReward(int amount)
        {
            if (amount > 0)
            {
                pendingReward += amount;
                pendingBattles++;
            }
        }

        public static int ConsumePendingReward()
        {
            int value = pendingReward;
            pendingReward = 0;
            pendingBattles = 0;
            return value;
        }

        public static void Reset()
        {
            pendingReward = 0;
            pendingBattles = 0;
        }
    }
}
