namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// CPU編成を用意する役。
    ///
    /// 画面側のControllerへ直接埋め込まず、この境界の向こうで組み立てます。
    /// プレイヤーの編成も選択も引数に現れないため、
    /// CPU編成が相手を見て作られることは構造的にありません。
    /// </summary>
    public interface ICpuSideBuilder
    {
        /// <summary>CPU編成を1つ作ります。作れない場合は false を返します。</summary>
        bool TryBuild(out BattleSideRoster side, out BattleError error);
    }
}
