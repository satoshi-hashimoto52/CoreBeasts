namespace CoreBeasts.Battle
{
    /// <summary>
    /// 敵の残存戦力をどこまで見せるか。
    ///
    /// 難易度やキャンペーン進行で差し替えられるよう、方針だけを分けています。
    /// 画面やViewへ条件分岐を書かず、この値を渡すだけにします。
    ///
    /// どの方針でも、嘘の情報は出しません。見せる量が減るだけです。
    /// </summary>
    public enum EnemyForceDisclosure
    {
        /// <summary>残っている各構成をすべて公開します。初期ゲームの既定です。</summary>
        FullComposition = 0,

        /// <summary>色ごとの保有数と2色の総数だけを公開します。構成は見せません。</summary>
        AggregateCounts = 1,

        /// <summary>残数だけを見せます。色は隠します。</summary>
        Masked = 2,
    }
}
