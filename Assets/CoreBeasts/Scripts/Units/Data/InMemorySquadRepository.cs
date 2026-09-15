using System.Collections.Generic;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 今回のスコープ用の保存先。アプリ実行中だけ編成を保持します。
    /// セーブデータは未実装のため、アプリ終了で消えます。
    /// </summary>
    public sealed class InMemorySquadRepository : ISquadRepository
    {
        private readonly Dictionary<string, SquadSnapshot> storage =
            new Dictionary<string, SquadSnapshot>();

        public bool TryLoad(string setId, out SquadSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(setId))
            {
                snapshot = null;
                return false;
            }

            return storage.TryGetValue(setId, out snapshot);
        }

        public void Save(string setId, SquadSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(setId) || snapshot == null)
            {
                return;
            }

            storage[setId] = snapshot;
        }
    }
}
