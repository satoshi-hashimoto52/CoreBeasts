using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// UnitSet画面で保存した編成を、対戦へ持ち込める形へ読み直します。
    ///
    /// 保存形式（<see cref="SquadSnapshot"/>）は内部IDだけを持つため、
    /// 所持一覧（<see cref="CoreBeastRoster"/>）と突き合わせて個体を復元します。
    /// 1枠でも欠けていれば対戦を開始しません。
    /// </summary>
    public static class PlayerSideLoader
    {
        /// <summary>
        /// 保存済み編成を読み込みます。
        /// 未保存・欠番・定義切れのいずれでも失敗し、編成不足として扱えます。
        /// </summary>
        public static bool TryLoad(
            ISquadRepository repository,
            string setId,
            CoreBeastRoster roster,
            out BattleSideRoster side,
            out BattleError error)
        {
            side = null;

            if (repository == null || roster == null || string.IsNullOrEmpty(setId))
            {
                error = BattleError.NullSquad;
                return false;
            }

            if (!repository.TryLoad(setId, out SquadSnapshot snapshot) || snapshot == null)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            List<BattleUnitCard> cards =
                new List<BattleUnitCard>(SquadFormation.SlotCount);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                string instanceId = snapshot.GetInstanceId(i);

                if (string.IsNullOrEmpty(instanceId))
                {
                    error = BattleError.InvalidSquadSize;
                    return false;
                }

                OwnedCoreBeast owned = roster.Find(instanceId);

                if (owned == null)
                {
                    error = BattleError.InvalidSquadSize;
                    return false;
                }

                if (!BattleUnitCard.TryCreate(owned, out BattleUnitCard card))
                {
                    error = BattleError.UnconvertibleUnit;
                    return false;
                }

                cards.Add(card);
            }

            return BattleSideRoster.TryCreate(cards, out side, out error);
        }
    }
}
