using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// バトルテスト用の個体と編成を組み立てるヘルパー。
    /// 属性とPOWERを明示して作れるため、判定の期待値を読みやすく書けます。
    /// </summary>
    internal static class TestBattleUnits
    {
        internal const int DefaultPower = 50;

        internal static BattleUnit Single(
            string instanceId,
            UnitAttribute attribute,
            int power = DefaultPower)
        {
            return new BattleUnit(instanceId, attribute, power);
        }

        internal static BattleUnit Dual(
            string instanceId,
            UnitAttribute primary,
            UnitAttribute secondary,
            int power = DefaultPower)
        {
            return new BattleUnit(instanceId, primary, secondary, power);
        }

        /// <summary>
        /// 規定数の単属性個体を作ります。IDは "<paramref name="idPrefix"/>0" から連番です。
        /// </summary>
        internal static List<BattleUnit> CreateUnits(
            string idPrefix,
            UnitAttribute attribute,
            int count = BattleSquad.UnitCount,
            int power = DefaultPower)
        {
            List<BattleUnit> units = new List<BattleUnit>(count);

            for (int i = 0; i < count; i++)
            {
                units.Add(new BattleUnit(idPrefix + i, attribute, power));
            }

            return units;
        }

        /// <summary>検証を通った編成を作ります。作れない場合はテストを失敗させます。</summary>
        internal static BattleSquad CreateSquad(
            string idPrefix,
            UnitAttribute attribute = UnitAttribute.Red,
            int power = DefaultPower)
        {
            return CreateSquad(CreateUnits(idPrefix, attribute, BattleSquad.UnitCount, power));
        }

        /// <summary>指定した個体から編成を作ります。作れない場合はテストを失敗させます。</summary>
        internal static BattleSquad CreateSquad(IReadOnlyList<BattleUnit> units)
        {
            if (!BattleSquad.TryCreate(units, out BattleSquad squad, out BattleError error))
            {
                NUnit.Framework.Assert.Fail(
                    "テスト用の編成を作れませんでした: " + error);
            }

            return squad;
        }
    }
}
