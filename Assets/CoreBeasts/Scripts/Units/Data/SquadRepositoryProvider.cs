namespace CoreBeasts.Units
{
    /// <summary>
    /// アプリ内で共有する編成の保存先。
    ///
    /// <see cref="InMemorySquadRepository"/>は実体ごとに別の中身を持つため、
    /// 画面が自前で作ると、UnitSetで保存した編成をBattleから読めません。
    /// 保存先の実体をここで1つに揃えます。
    ///
    /// 保存先そのものの差し替え（PlayerPrefs / JSON / サーバー）は
    /// <see cref="ISquadRepository"/>の実装を<see cref="SetShared"/>へ渡すだけで済みます。
    /// アプリを終了すると内容が消える点は、既定実装のままで変わりません。
    /// </summary>
    public static class SquadRepositoryProvider
    {
        private static ISquadRepository shared;

        /// <summary>共有の保存先。未設定なら既定の実装を作って使い回します。</summary>
        public static ISquadRepository Shared => shared ??= new InMemorySquadRepository();

        /// <summary>保存先を差し替えます。null を渡すと既定へ戻ります。</summary>
        public static void SetShared(ISquadRepository repository)
        {
            shared = repository;
        }

        /// <summary>共有の保存先を捨てます。テストが状態を持ち越さないために使います。</summary>
        public static void Reset()
        {
            shared = null;
        }
    }
}
