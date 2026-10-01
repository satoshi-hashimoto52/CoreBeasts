using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 片側7体ぶんの、表示用データと対戦用編成の組。
    ///
    /// 生成時に<see cref="BattleSquad"/>の検証を通るため、
    /// 作れた時点で「対戦へ持ち込める7体」であることが保証されます。
    /// プレイヤー側とCPU側で同じ型を使い、表示処理を共通化します。
    /// </summary>
    public sealed class BattleSideRoster
    {
        private readonly BattleUnitCard[] cards;
        private readonly ReadOnlyCollection<BattleUnitCard> readOnlyCards;

        private BattleSideRoster(BattleUnitCard[] cards, BattleSquad squad)
        {
            this.cards = cards;
            readOnlyCards = Array.AsReadOnly(cards);
            Squad = squad;
        }

        /// <summary>7体ぶんの表示用データ。編成の並び順と一致します。</summary>
        public IReadOnlyList<BattleUnitCard> Cards => readOnlyCards;

        /// <summary>対戦へ渡す編成。</summary>
        public BattleSquad Squad { get; }

        /// <summary>個体数。常に<see cref="BattleSquad.UnitCount"/>です。</summary>
        public int Count => cards.Length;

        /// <summary>内部IDで表示用データを引きます。いなければ null。</summary>
        public BattleUnitCard Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (string.Equals(cards[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return cards[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 7体から作ります。数が足りない、IDが重複しているなど、
        /// 対戦へ持ち込めない編成は作れません。入力コレクションは変更しません。
        /// </summary>
        public static bool TryCreate(
            IReadOnlyList<BattleUnitCard> source,
            out BattleSideRoster side,
            out BattleError error)
        {
            side = null;

            if (source == null)
            {
                error = BattleError.NullSquad;
                return false;
            }

            if (source.Count != BattleSquad.UnitCount)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            List<BattleUnit> units = new List<BattleUnit>(source.Count);
            BattleUnitCard[] copy = new BattleUnitCard[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                BattleUnitCard card = source[i];

                if (card == null)
                {
                    error = BattleError.NullUnit;
                    return false;
                }

                copy[i] = card;
                units.Add(card.Unit);
            }

            if (!BattleSquad.TryCreate(units, out BattleSquad squad, out error))
            {
                return false;
            }

            side = new BattleSideRoster(copy, squad);
            error = BattleError.None;

            return true;
        }
    }
}
