using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 対戦へ持ち込む編成。生成時に検証を通った編成だけが存在します。
    /// 生成後は内容が変わらないため、セッション側は再検証せずに使えます。
    /// </summary>
    public sealed class BattleSquad
    {
        /// <summary>
        /// 対戦へ持ち込む個体数。編成画面の枠数
        /// （<see cref="SquadFormation.SlotCount"/>）と同数です。
        /// </summary>
        public const int UnitCount = SquadFormation.SlotCount;

        private readonly BattleUnit[] units;
        private readonly ReadOnlyCollection<BattleUnit> readOnlyUnits;

        private BattleSquad(BattleUnit[] units)
        {
            this.units = units;
            readOnlyUnits = Array.AsReadOnly(units);
        }

        /// <summary>
        /// 編成を検証して作ります。失敗時は<paramref name="squad"/>が null になり、
        /// <paramref name="error"/>へ理由が入ります。入力コレクションは変更しません。
        /// </summary>
        public static bool TryCreate(
            IReadOnlyList<BattleUnit> units,
            out BattleSquad squad,
            out BattleError error)
        {
            squad = null;

            if (units == null)
            {
                error = BattleError.NullSquad;
                return false;
            }

            if (units.Count != UnitCount)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            // 先に全件を検証し、問題が無いと分かってからコピーを作ります。
            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < units.Count; i++)
            {
                BattleUnit unit = units[i];

                if (unit == null)
                {
                    error = BattleError.NullUnit;
                    return false;
                }

                BattleError unitError = unit.Validate();

                if (unitError != BattleError.None)
                {
                    error = unitError;
                    return false;
                }

                if (!seenIds.Add(unit.InstanceId))
                {
                    error = BattleError.DuplicateInstanceId;
                    return false;
                }
            }

            BattleUnit[] copy = new BattleUnit[UnitCount];

            for (int i = 0; i < UnitCount; i++)
            {
                copy[i] = units[i];
            }

            squad = new BattleSquad(copy);
            error = BattleError.None;

            return true;
        }

        /// <summary>編成の個体一覧。読み取り専用で、外部からは変更できません。</summary>
        public IReadOnlyList<BattleUnit> Units => readOnlyUnits;

        /// <summary>編成の個体数。常に<see cref="UnitCount"/>です。</summary>
        public int Count => units.Length;

        /// <summary>内部IDで個体を引きます。いなければ null。</summary>
        public BattleUnit Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            for (int i = 0; i < units.Length; i++)
            {
                if (string.Equals(units[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return units[i];
                }
            }

            return null;
        }

        /// <summary>指定IDの個体が編成にいるか。</summary>
        public bool Contains(string instanceId)
        {
            return Find(instanceId) != null;
        }
    }
}
