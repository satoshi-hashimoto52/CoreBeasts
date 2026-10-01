using System;
using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 既存の所持一覧から、CPU編成を7体ぶん組み立てます。
    ///
    /// 初期版は戦略を持たず、所持一覧から重複なしで7体を無作為に選ぶだけです。
    /// 乱数は<see cref="IRandomSource"/>を注入するため、テストでは固定できます。
    ///
    /// 内部IDには接頭辞を付けます。プレイヤーとCPUが同じ所持データを使っても、
    /// 対戦中の同一性判定が混ざりません。
    /// </summary>
    public sealed class RosterCpuSideBuilder : ICpuSideBuilder
    {
        /// <summary>CPU側の内部IDに付ける既定の接頭辞。</summary>
        public const string DefaultInstanceIdPrefix = "cpu:";

        private readonly CoreBeastRoster roster;
        private readonly IRandomSource randomSource;
        private readonly string instanceIdPrefix;

        public RosterCpuSideBuilder(CoreBeastRoster roster, IRandomSource randomSource)
            : this(roster, randomSource, DefaultInstanceIdPrefix)
        {
        }

        public RosterCpuSideBuilder(
            CoreBeastRoster roster,
            IRandomSource randomSource,
            string instanceIdPrefix)
        {
            this.roster = roster;

            this.randomSource = randomSource
                ?? throw new ArgumentNullException(nameof(randomSource));

            this.instanceIdPrefix = string.IsNullOrEmpty(instanceIdPrefix)
                ? DefaultInstanceIdPrefix
                : instanceIdPrefix;
        }

        /// <summary>
        /// 所持一覧から重複なしで7体を選び、CPU編成を作ります。
        /// 出せる個体が7体に満たない場合は作れません。
        /// </summary>
        public bool TryBuild(out BattleSideRoster side, out BattleError error)
        {
            side = null;

            if (roster == null)
            {
                error = BattleError.NullSquad;
                return false;
            }

            List<OwnedCoreBeast> candidates = CollectCandidates();

            if (candidates.Count < BattleSquad.UnitCount)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            Shuffle(candidates);

            List<BattleUnitCard> cards =
                new List<BattleUnitCard>(BattleSquad.UnitCount);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                OwnedCoreBeast owned = candidates[i];
                string instanceId = instanceIdPrefix + owned.InstanceId;

                if (!BattleUnitCard.TryCreate(owned, instanceId, out BattleUnitCard card))
                {
                    error = BattleError.UnconvertibleUnit;
                    return false;
                }

                cards.Add(card);
            }

            return BattleSideRoster.TryCreate(cards, out side, out error);
        }

        /// <summary>対戦へ出せる所持個体だけを集めます。内部IDの重複は取り除きます。</summary>
        private List<OwnedCoreBeast> CollectCandidates()
        {
            IReadOnlyList<OwnedCoreBeast> owned = roster.Owned;

            List<OwnedCoreBeast> candidates = new List<OwnedCoreBeast>(owned.Count);
            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < owned.Count; i++)
            {
                OwnedCoreBeast candidate = owned[i];

                if (candidate == null || !candidate.IsValid)
                {
                    continue;
                }

                if (seenIds.Add(candidate.InstanceId))
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// 注入された乱数源だけで並びを混ぜます。
        /// 範囲外の値が返っても候補の範囲へ収めるため、実装差で落ちません。
        /// </summary>
        private void Shuffle(List<OwnedCoreBeast> candidates)
        {
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = randomSource.NextInt(i + 1);

                if (j < 0)
                {
                    j = 0;
                }
                else if (j > i)
                {
                    j = i;
                }

                OwnedCoreBeast swapped = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swapped;
            }
        }
    }
}
