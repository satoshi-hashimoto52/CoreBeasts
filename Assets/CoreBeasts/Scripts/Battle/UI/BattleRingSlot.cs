namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// リングが1枠ぶん持つ情報。個体IDと、元の編成番号だけです。
    ///
    /// 勝敗も使用済みも持ちません。使用済みかどうかは
    /// <see cref="BattleUnitRingModel"/>が「リングに居るかどうか」で表します。
    /// </summary>
    public readonly struct BattleRingSlot
    {
        /// <summary>個体ID。リング内で一意です。</summary>
        public readonly string InstanceId;

        /// <summary>元の編成番号（1〜7）。使用済みが抜けても変わりません。</summary>
        public readonly int SquadNumber;

        public BattleRingSlot(string instanceId, int squadNumber)
        {
            InstanceId = instanceId;
            SquadNumber = squadNumber;
        }

        /// <summary>中身のある枠か。</summary>
        public bool IsValid => !string.IsNullOrEmpty(InstanceId);
    }
}
