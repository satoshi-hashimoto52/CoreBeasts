namespace CoreBeasts.Battle
{
    /// <summary>
    /// バトル中核ロジックが入力を受け付けなかった理由。
    /// 呼び出し側が例外に頼らず分岐できるよう、失敗はこの列挙で返します。
    /// </summary>
    public enum BattleError
    {
        /// <summary>失敗していません。</summary>
        None = 0,

        /// <summary>編成データそのものが null でした。</summary>
        NullSquad = 1,

        /// <summary>編成の個体数が規定数と一致しませんでした。</summary>
        InvalidSquadSize = 2,

        /// <summary>編成に null の個体が含まれていました。</summary>
        NullUnit = 3,

        /// <summary>個体IDが空でした。</summary>
        EmptyInstanceId = 4,

        /// <summary>同じ個体IDが編成内で重複していました。</summary>
        DuplicateInstanceId = 5,

        /// <summary>属性が定義済みの値ではありませんでした。</summary>
        InvalidAttribute = 6,

        /// <summary>POWERが負の値でした。</summary>
        NegativePower = 7,

        /// <summary>マッチが既に終了しています。</summary>
        MatchAlreadyFinished = 8,

        /// <summary>このラウンドでは既に選出済みです。</summary>
        AlreadySelected = 9,

        /// <summary>指定された個体が自分の編成にいません。</summary>
        UnitNotInSquad = 10,

        /// <summary>指定された個体はこの対戦で使用済みです。</summary>
        UnitAlreadyUsed = 11,

        /// <summary>未使用の候補が残っていません。</summary>
        NoAvailableUnit = 12,

        /// <summary>選択器が候補外の結果を返しました。</summary>
        InvalidSelectorResult = 13,

        /// <summary>双方の選出が揃っていません。</summary>
        SelectionIncomplete = 14,

        /// <summary>所持データからバトル用の値へ変換できませんでした。</summary>
        UnconvertibleUnit = 15,
    }
}
