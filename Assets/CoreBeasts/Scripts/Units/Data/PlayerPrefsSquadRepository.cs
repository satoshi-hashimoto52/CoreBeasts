using System;
using System.Collections.Generic;

using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>編成セットをPlayerPrefsへJSON保存します。</summary>
    public sealed class PlayerPrefsSquadRepository : ISquadRepository
    {
        private const string DefaultKey = "CoreBeasts.Squads.v1";

        [Serializable]
        private sealed class SaveData
        {
            [SerializeField] private List<Entry> entries = new List<Entry>();

            internal List<Entry> Entries => entries ??= new List<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string setId;
            [SerializeField] private SquadSnapshot snapshot;

            internal Entry(string id, SquadSnapshot value)
            {
                setId = id;
                snapshot = value;
            }

            internal string SetId => setId;
            internal SquadSnapshot Snapshot
            {
                get => snapshot;
                set => snapshot = value;
            }
        }

        private readonly string key;
        private SaveData cache;

        public PlayerPrefsSquadRepository(string storageKey = DefaultKey)
        {
            key = string.IsNullOrEmpty(storageKey) ? DefaultKey : storageKey;
        }

        public bool TryLoad(string setId, out SquadSnapshot snapshot)
        {
            snapshot = null;

            if (string.IsNullOrEmpty(setId))
            {
                return false;
            }

            List<Entry> entries = Load().Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry != null && entry.SetId == setId && entry.Snapshot != null)
                {
                    snapshot = entry.Snapshot;
                    return true;
                }
            }

            return false;
        }

        public void Save(string setId, SquadSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(setId) || snapshot == null)
            {
                return;
            }

            List<Entry> entries = Load().Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry != null && entry.SetId == setId)
                {
                    entry.Snapshot = snapshot;
                    Persist();
                    return;
                }
            }

            entries.Add(new Entry(setId, snapshot));
            Persist();
        }

        private SaveData Load()
        {
            if (cache != null)
            {
                return cache;
            }

            string json = PlayerPrefs.GetString(key, string.Empty);
            cache = string.IsNullOrEmpty(json)
                ? new SaveData()
                : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();

            return cache;
        }

        private void Persist()
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(Load()));
            PlayerPrefs.Save();
        }
    }
}
