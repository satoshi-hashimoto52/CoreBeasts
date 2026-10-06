using System;
using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Progression
{
    public interface IGachaRandomSource
    {
        int Next(int maxExclusive);
    }

    public sealed class SystemGachaRandomSource : IGachaRandomSource
    {
        private readonly Random random;

        public SystemGachaRandomSource(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public int Next(int maxExclusive)
        {
            return maxExclusive > 0 ? random.Next(maxExclusive) : 0;
        }
    }

    public sealed class GachaResult
    {
        public GachaResult(OwnedCoreBeast beast, bool isNew, int copies, int coins)
        {
            Beast = beast;
            IsNew = isNew;
            Copies = copies;
            RemainingCoins = coins;
        }

        public OwnedCoreBeast Beast { get; }
        public bool IsNew { get; }
        public int Copies { get; }
        public int RemainingCoins { get; }
    }

    /// <summary>100コインを消費し、カタログから等確率で1体を獲得します。</summary>
    public sealed class GachaService
    {
        private readonly IGachaRandomSource random;

        public GachaService(IGachaRandomSource randomSource)
        {
            random = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public bool TryPull(
            PlayerProfile profile,
            CoreBeastRoster catalog,
            out GachaResult result)
        {
            result = null;

            if (profile == null || catalog == null)
            {
                return false;
            }

            List<OwnedCoreBeast> candidates = new List<OwnedCoreBeast>();
            List<OwnedCoreBeast> newCandidates = new List<OwnedCoreBeast>();
            IReadOnlyList<OwnedCoreBeast> entries = catalog.Owned;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].IsValid)
                {
                    candidates.Add(entries[i]);

                    if (!profile.Owns(entries[i].InstanceId))
                    {
                        newCandidates.Add(entries[i]);
                    }
                }
            }

            if (candidates.Count == 0 || !profile.TrySpendCoins(GameEconomy.GachaCost))
            {
                return false;
            }

            // 未獲得が残っているあいだは必ず新規個体を出し、最初の一周を止めません。
            // 全種を獲得した後だけ、全カタログから重複を抽選します。
            List<OwnedCoreBeast> pool = newCandidates.Count > 0
                ? newCandidates
                : candidates;
            OwnedCoreBeast selected = pool[random.Next(pool.Count)];
            bool isNew = profile.Acquire(selected.InstanceId);

            result = new GachaResult(
                selected,
                isNew,
                profile.CopiesOf(selected.InstanceId),
                profile.Coins);

            return true;
        }
    }
}
