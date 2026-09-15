using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 所持データ（<see cref="OwnedCoreBeast"/>／<see cref="CoreBeastDefinition"/>）から
    /// バトル中核ロジックの値へ変換する境界。
    ///
    /// 変換はここだけで行い、既存の所持・編成レイヤーはバトルの型を知りません。
    /// バトル側も、ここを通す前の提示用データ（表示名、立ち絵、レベル）を扱いません。
    /// </summary>
    public static class BattleUnitFactory
    {
        /// <summary>
        /// 所持個体1体を変換します。
        /// 定義が無い、内部IDが空などで変換できない場合は false を返します。
        /// </summary>
        public static bool TryCreateUnit(OwnedCoreBeast owned, out BattleUnit unit)
        {
            unit = null;

            if (owned == null || !owned.IsValid)
            {
                return false;
            }

            CoreBeastDefinition definition = owned.Definition;

            unit = definition.HasSecondaryAttribute
                ? new BattleUnit(
                    owned.InstanceId,
                    definition.PrimaryAttribute,
                    definition.SecondaryAttribute,
                    definition.Power)
                : new BattleUnit(
                    owned.InstanceId,
                    definition.PrimaryAttribute,
                    definition.Power);

            return true;
        }

        /// <summary>
        /// 所持個体の一覧から編成を作ります。
        /// 変換できない個体が含まれる場合も、編成条件を満たさない場合も失敗を返し、
        /// 入力コレクションは変更しません。
        /// </summary>
        public static bool TryCreateSquad(
            IReadOnlyList<OwnedCoreBeast> owned,
            out BattleSquad squad,
            out BattleError error)
        {
            squad = null;

            if (owned == null)
            {
                error = BattleError.NullSquad;
                return false;
            }

            if (owned.Count != BattleSquad.UnitCount)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            List<BattleUnit> units = new List<BattleUnit>(owned.Count);

            for (int i = 0; i < owned.Count; i++)
            {
                if (!TryCreateUnit(owned[i], out BattleUnit unit))
                {
                    error = owned[i] == null
                        ? BattleError.NullUnit
                        : BattleError.UnconvertibleUnit;

                    return false;
                }

                units.Add(unit);
            }

            return BattleSquad.TryCreate(units, out squad, out error);
        }

        /// <summary>
        /// 編成枠から編成を作ります。空き枠が残っている場合は失敗を返します。
        /// </summary>
        public static bool TryCreateSquad(
            SquadFormation formation,
            out BattleSquad squad,
            out BattleError error)
        {
            squad = null;

            if (formation == null)
            {
                error = BattleError.NullSquad;
                return false;
            }

            List<OwnedCoreBeast> owned =
                new List<OwnedCoreBeast>(SquadFormation.SlotCount);

            for (int i = 0; i < SquadFormation.SlotCount; i++)
            {
                OwnedCoreBeast slot = formation.GetAt(i);

                if (slot == null)
                {
                    error = BattleError.InvalidSquadSize;
                    return false;
                }

                owned.Add(slot);
            }

            return TryCreateSquad(owned, out squad, out error);
        }
    }
}
