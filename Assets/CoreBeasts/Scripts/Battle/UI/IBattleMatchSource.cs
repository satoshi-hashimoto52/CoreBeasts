namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 1マッチぶんの編成と CPU 選択器を用意する役。
    ///
    /// 進行役（<see cref="BattleFlowCoordinator"/>）は編成の作り方を知りません。
    /// プレイヤー編成の読み込みも CPU 編成の生成も、この境界の向こう側にあります。
    /// テストでは決定論的な実装へ差し替えられます。
    /// </summary>
    public interface IBattleMatchSource
    {
        /// <summary>
        /// 新しい1マッチぶんを用意します。
        /// 用意できない場合は false を返し、<paramref name="error"/>へ理由が入ります。
        /// </summary>
        bool TryCreateMatch(
            out BattleSquad playerSquad,
            out BattleSquad cpuSquad,
            out IBattleUnitSelector cpuSelector,
            out BattleError error);
    }
}
