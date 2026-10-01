namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の入力ゲート。
    ///
    /// 「今この操作を受け付けるか」の判断は<see cref="BattleFlowCoordinator"/>が持ちます。
    /// ここはその答えへ、画面側だけの事情である
    /// 「設定パネルを開いているか」「演出を再生中か」を重ねるだけです。
    ///
    /// Unityへ依存しないため、そのままテストできます。
    /// </summary>
    public static class BattleInputGate
    {
        /// <summary>編成トレイのタップを受け付けるか。</summary>
        public static bool AllowsTraySelection(bool coordinatorAllows, bool settingsOpen)
        {
            return coordinatorAllows && !settingsOpen;
        }

        /// <summary>DEPLOYを受け付けるか。</summary>
        public static bool AllowsDeploy(
            bool coordinatorAllows,
            bool settingsOpen,
            bool presenting)
        {
            return coordinatorAllows && !settingsOpen && !presenting;
        }

        /// <summary>REMATCHを受け付けるか。</summary>
        public static bool AllowsRematch(
            bool coordinatorAllows,
            bool settingsOpen,
            bool presenting)
        {
            return coordinatorAllows && !settingsOpen && !presenting;
        }

        /// <summary>
        /// 歯車の設定ボタンを押せるか。
        /// 演出と結果表示のあいだは押せません。途中で設定を変えられると、
        /// 再生中の見た目と設定が食い違います。
        /// </summary>
        public static bool AllowsSettings(BattleUiState state, bool presenting)
        {
            return !presenting &&
                   state != BattleUiState.Resolving &&
                   state != BattleUiState.ShowingResult;
        }
    }
}
