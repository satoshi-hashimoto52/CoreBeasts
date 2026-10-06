namespace CoreBeasts.Progression
{
    public enum BattleRewardOutcome
    {
        Win = 0,
        Draw = 1,
        Loss = 2,
    }

    /// <summary>縦導線で使う経済値の正本。</summary>
    public static class GameEconomy
    {
        public const int StartingCoins = 100;
        public const int GachaCost = 100;
        public const int WinReward = 30;
        public const int DrawReward = 20;
        public const int LossReward = 10;
        public const int StarterBeastCount = 7;

        public static int RewardFor(BattleRewardOutcome outcome)
        {
            switch (outcome)
            {
                case BattleRewardOutcome.Win:
                    return WinReward;
                case BattleRewardOutcome.Draw:
                    return DrawReward;
                default:
                    return LossReward;
            }
        }
    }
}
