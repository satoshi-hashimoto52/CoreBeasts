namespace CoreBeasts.Battle
{
    /// <summary>
    /// 選出APIの結果。失敗を例外ではなく値で返すため、
    /// 呼び出し側は理由を見て画面表示や再入力へ分岐できます。
    /// </summary>
    public readonly struct SelectionResult
    {
        private SelectionResult(bool success, BattleError error)
        {
            Success = success;
            Error = error;
        }

        /// <summary>選出を受け付けたか。</summary>
        public bool Success { get; }

        /// <summary>受け付けなかった理由。成功時は<see cref="BattleError.None"/>。</summary>
        public BattleError Error { get; }

        /// <summary>成功。</summary>
        public static SelectionResult Ok()
        {
            return new SelectionResult(true, BattleError.None);
        }

        /// <summary>失敗。セッションの状態は変わっていません。</summary>
        public static SelectionResult Fail(BattleError error)
        {
            return new SelectionResult(false, error);
        }
    }
}
