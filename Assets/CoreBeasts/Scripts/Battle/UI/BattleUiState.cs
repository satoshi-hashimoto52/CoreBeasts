namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の表示状態。進行管理はこの列挙だけで分岐し、
    /// 「今どの入力を受け付けるか」を1箇所で決めます。
    /// </summary>
    public enum BattleUiState
    {
        /// <summary>初期化前。まだ編成もセッションもありません。</summary>
        Loading = 0,

        /// <summary>7体の編成が揃っていないため、対戦を開始できません。</summary>
        SquadRequired = 1,

        /// <summary>プレイヤーが出す個体を選べます。DEPLOYも押せます。</summary>
        Selecting = 2,

        /// <summary>解決済み。演出の再生中で、入力は受け付けません。</summary>
        Resolving = 3,

        /// <summary>結果バナーの表示中。入力は受け付けません。</summary>
        ShowingResult = 4,

        /// <summary>マッチ終了。REMATCH と HOME だけを受け付けます。</summary>
        MatchFinished = 5,
    }
}
