namespace CoreBeasts.Battle
{
    /// <summary>1ラウンドの勝者。</summary>
    public enum RoundWinner
    {
        /// <summary>ラウンド引き分け。どちらの勝利数にも加算しません。</summary>
        Draw = 0,

        /// <summary>プレイヤー側の勝利。</summary>
        Player = 1,

        /// <summary>CPU側の勝利。</summary>
        Cpu = 2,
    }
}
