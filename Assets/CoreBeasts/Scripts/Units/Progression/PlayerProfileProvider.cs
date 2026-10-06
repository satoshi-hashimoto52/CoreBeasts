using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Progression
{
    /// <summary>全画面が同じプロフィールを参照する入口。</summary>
    public static class PlayerProfileProvider
    {
        private static IPlayerProfileRepository repository;
        private static PlayerProfile current;

        public static IPlayerProfileRepository Repository =>
            repository ??= new PlayerPrefsPlayerProfileRepository();

        public static PlayerProfile Get(CoreBeastRoster catalog)
        {
            if (current == null)
            {
                if (!Repository.TryLoad(out current) || current == null)
                {
                    current = PlayerProfile.CreateNew();
                }

                EnsureStarterCollection(current, catalog);
                Repository.Save(current);
            }

            return current;
        }

        public static void Save()
        {
            if (current != null)
            {
                Repository.Save(current);
            }
        }

        public static void SetRepository(IPlayerProfileRepository value)
        {
            repository = value;
            current = null;
        }

        public static void Reset()
        {
            repository = null;
            current = null;
        }

        public static List<OwnedCoreBeast> OwnedFrom(
            PlayerProfile profile,
            CoreBeastRoster catalog)
        {
            List<OwnedCoreBeast> result = new List<OwnedCoreBeast>();

            if (profile == null || catalog == null)
            {
                return result;
            }

            IReadOnlyList<OwnedCoreBeast> entries = catalog.Owned;

            for (int i = 0; i < entries.Count; i++)
            {
                OwnedCoreBeast beast = entries[i];

                if (beast != null && beast.IsValid && profile.Owns(beast.InstanceId))
                {
                    result.Add(beast);
                }
            }

            return result;
        }

        /// <summary>
        /// 初回起動時だけ、最初の7体でSET 1を作ります。
        /// 保存済みの有効な編成は変更しません。
        /// </summary>
        public static void EnsureStarterSquad(
            PlayerProfile profile,
            CoreBeastRoster catalog,
            ISquadRepository squads,
            string setId)
        {
            if (profile == null || catalog == null || squads == null ||
                string.IsNullOrEmpty(setId))
            {
                return;
            }

            if (squads.TryLoad(setId, out SquadSnapshot saved) &&
                IsUsableSquad(saved, profile, catalog))
            {
                return;
            }

            List<OwnedCoreBeast> owned = OwnedFrom(profile, catalog);

            if (owned.Count < SquadFormation.SlotCount)
            {
                return;
            }

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = owned[i].InstanceId;
            }

            squads.Save(setId, new SquadSnapshot(ids));
        }

        private static void EnsureStarterCollection(
            PlayerProfile profile,
            CoreBeastRoster catalog)
        {
            if (profile == null || catalog == null || profile.Beasts.Count > 0)
            {
                return;
            }

            IReadOnlyList<OwnedCoreBeast> entries = catalog.Owned;
            int target = entries.Count < GameEconomy.StarterBeastCount
                ? entries.Count
                : GameEconomy.StarterBeastCount;

            for (int i = 0; i < target; i++)
            {
                OwnedCoreBeast beast = entries[i];

                if (beast != null && beast.IsValid)
                {
                    profile.Acquire(beast.InstanceId);
                }
            }
        }

        private static bool IsUsableSquad(
            SquadSnapshot snapshot,
            PlayerProfile profile,
            CoreBeastRoster catalog)
        {
            if (snapshot == null)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                string instanceId = snapshot.GetInstanceId(i);

                if (string.IsNullOrEmpty(instanceId) ||
                    !profile.Owns(instanceId) ||
                    catalog.Find(instanceId) == null ||
                    !seen.Add(instanceId))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
