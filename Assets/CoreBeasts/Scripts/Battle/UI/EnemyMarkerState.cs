namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// CPU側7枠の状態。個体の内容は表しません。
    /// 解決前に相手の名前・属性・POWERが漏れないよう、状態だけを持ちます。
    /// </summary>
    public enum EnemyMarkerState
    {
        /// <summary>未使用。</summary>
        Unused = 0,

        /// <summary>今ラウンドの選出済み。どれを選んだかは表しません。</summary>
        Pending = 1,

        /// <summary>使用済み。</summary>
        Used = 2,
    }

    /// <summary>
    /// CPU側7枠の状態を、使用済み数と選出済みかどうかだけから決めます。
    ///
    /// 入力に個体情報が含まれないため、この計算から相手の編成が漏れることはありません。
    /// 選出中の枠は「使用済みの次の枠」を機械的に使い、実際の選出個体とは無関係です。
    /// </summary>
    public static class EnemyMarkerStates
    {
        /// <summary>指定位置の枠の状態。</summary>
        public static EnemyMarkerState Resolve(
            int index,
            int usedCount,
            bool hasPendingSelection)
        {
            if (index < usedCount)
            {
                return EnemyMarkerState.Used;
            }

            if (hasPendingSelection && index == usedCount)
            {
                return EnemyMarkerState.Pending;
            }

            return EnemyMarkerState.Unused;
        }
    }
}
