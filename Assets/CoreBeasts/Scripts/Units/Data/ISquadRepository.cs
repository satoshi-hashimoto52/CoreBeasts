namespace CoreBeasts.Units
{
    /// <summary>
    /// 編成の保存先。UI処理とデータ保持処理を分離するための境界です。
    /// 将来 PlayerPrefs / JSONファイル / サーバー実装へ差し替えられます。
    /// </summary>
    public interface ISquadRepository
    {
        bool TryLoad(string setId, out SquadSnapshot snapshot);

        void Save(string setId, SquadSnapshot snapshot);
    }
}
