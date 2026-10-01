namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 戦績履歴レーンの1枠。使い終わった個体1体ぶんです。
    ///
    /// 勝敗はここでは決めません。<see cref="RoundResult.Winner"/>を
    /// <see cref="BattleSlotOutcomes.FromWinner"/>で言い換えた値を受け取るだけです。
    /// </summary>
    public readonly struct BattleHistoryEntry
    {
        /// <summary>出した個体のID。</summary>
        public readonly string InstanceId;

        /// <summary>元の編成番号（1〜7）。</summary>
        public readonly int SquadNumber;

        /// <summary>PLAYERから見た結果。</summary>
        public readonly BattleSlotOutcome Outcome;

        public BattleHistoryEntry(
            string instanceId, int squadNumber, BattleSlotOutcome outcome)
        {
            InstanceId = instanceId;
            SquadNumber = squadNumber;
            Outcome = outcome;
        }

        /// <summary>中身のある枠か。</summary>
        public bool IsValid => !string.IsNullOrEmpty(InstanceId);
    }
}
