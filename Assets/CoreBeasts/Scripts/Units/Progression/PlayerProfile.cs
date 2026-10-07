using System;
using System.Collections.Generic;

using UnityEngine;

namespace CoreBeasts.Progression
{
    /// <summary>端末へ保存するプレイヤー進行データ。</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private int coins;
        [SerializeField] private int battles;
        [SerializeField] private int wins;
        [SerializeField] private int draws;
        [SerializeField] private int losses;
        [SerializeField] private List<OwnedBeastProgress> beasts =
            new List<OwnedBeastProgress>();

        public int SchemaVersion => schemaVersion;
        public int Coins => Mathf.Max(0, coins);
        public int Battles => Mathf.Max(0, battles);
        public int Wins => Mathf.Max(0, wins);
        public int Draws => Mathf.Max(0, draws);
        public int Losses => Mathf.Max(0, losses);
        public IReadOnlyList<OwnedBeastProgress> Beasts => beasts;

        public static PlayerProfile CreateNew()
        {
            return new PlayerProfile
            {
                schemaVersion = CurrentSchemaVersion,
                coins = GameEconomy.StartingCoins,
            };
        }

        public bool Owns(string instanceId)
        {
            return CopiesOf(instanceId) > 0;
        }

        public int CopiesOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || beasts == null)
            {
                return 0;
            }

            for (int i = 0; i < beasts.Count; i++)
            {
                OwnedBeastProgress entry = beasts[i];

                if (entry != null && entry.InstanceId == instanceId)
                {
                    return entry.Copies;
                }
            }

            return 0;
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0 || coins < amount)
            {
                return false;
            }

            coins -= amount;
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount > 0)
            {
                coins += amount;
            }
        }

        public bool Acquire(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            beasts ??= new List<OwnedBeastProgress>();

            for (int i = 0; i < beasts.Count; i++)
            {
                OwnedBeastProgress entry = beasts[i];

                if (entry != null && entry.InstanceId == instanceId)
                {
                    entry.AddCopy();
                    return false;
                }
            }

            beasts.Add(new OwnedBeastProgress(instanceId, 1));
            return true;
        }

        public void RecordBattle(BattleRewardOutcome outcome, int reward)
        {
            battles++;

            switch (outcome)
            {
                case BattleRewardOutcome.Win:
                    wins++;
                    break;
                case BattleRewardOutcome.Draw:
                    draws++;
                    break;
                case BattleRewardOutcome.Loss:
                    losses++;
                    break;
            }

            AddCoins(reward);
        }

        internal void Repair()
        {
            schemaVersion = CurrentSchemaVersion;
            coins = Mathf.Max(0, coins);
            battles = Mathf.Max(0, battles);
            wins = Mathf.Max(0, wins);
            draws = Mathf.Max(0, draws);
            losses = Mathf.Max(0, losses);
            beasts ??= new List<OwnedBeastProgress>();

            for (int i = beasts.Count - 1; i >= 0; i--)
            {
                if (beasts[i] == null || string.IsNullOrEmpty(beasts[i].InstanceId))
                {
                    beasts.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class OwnedBeastProgress
    {
        [SerializeField] private string instanceId;
        [SerializeField] private int copies;

        public OwnedBeastProgress(string id, int count)
        {
            instanceId = id ?? string.Empty;
            copies = Mathf.Max(1, count);
        }

        public string InstanceId => instanceId ?? string.Empty;
        public int Copies => Mathf.Max(0, copies);

        internal void AddCopy()
        {
            copies = Mathf.Max(0, copies) + 1;
        }
    }
}
