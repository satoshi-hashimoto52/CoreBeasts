namespace CoreBeasts.Battle.UI
{
    /// <summary>プレイヤーの7枠それぞれの見た目の状態。</summary>
    public enum BattleSlotState
    {
        /// <summary>未使用。タップで選べます。</summary>
        Available = 0,

        /// <summary>今ラウンドに選択中。枠を強調し、少し浮かせます。</summary>
        Selected = 1,

        /// <summary>この対戦で使用済み。暗くし、再選択できません。</summary>
        Used = 2,
    }

    /// <summary>
    /// 枠の見た目の状態を決めます。使用済みかどうかの判定そのものは
    /// <see cref="BattleSession"/>が持ち、ここは受け取った事実を写すだけです。
    /// </summary>
    public static class BattleSlotStates
    {
        /// <summary>使用済みが最優先、次に選択中、それ以外は未使用です。</summary>
        public static BattleSlotState Resolve(
            string instanceId,
            string selectedInstanceId,
            bool used)
        {
            if (used)
            {
                return BattleSlotState.Used;
            }

            if (!string.IsNullOrEmpty(instanceId) && instanceId == selectedInstanceId)
            {
                return BattleSlotState.Selected;
            }

            return BattleSlotState.Available;
        }
    }
}
