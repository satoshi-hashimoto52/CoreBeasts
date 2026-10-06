using UnityEngine;

namespace CoreBeasts.Progression
{
    /// <summary>PlayerPrefsへJSONで保存する本番用プロフィール保存先。</summary>
    public sealed class PlayerPrefsPlayerProfileRepository : IPlayerProfileRepository
    {
        public const string DefaultKey = "CoreBeasts.PlayerProfile.v1";

        private readonly string key;

        public PlayerPrefsPlayerProfileRepository(string storageKey = DefaultKey)
        {
            key = string.IsNullOrEmpty(storageKey) ? DefaultKey : storageKey;
        }

        public bool TryLoad(out PlayerProfile profile)
        {
            profile = null;

            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            string json = PlayerPrefs.GetString(key, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            profile = JsonUtility.FromJson<PlayerProfile>(json);

            if (profile == null)
            {
                return false;
            }

            profile.Repair();
            return true;
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }
    }
}
