using System.Collections.Generic;

using CoreBeasts.Units;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 進行役のテスト用に、中核の値だけで編成を組み立てるヘルパー。
    /// Unityのオブジェクトを作らないため、純粋なC#として実行できます。
    /// </summary>
    internal static class TestBattleSquads
    {
        /// <summary>同じ属性・同じPOWERの7体編成。</summary>
        internal static BattleSquad Uniform(
            string prefix,
            UnitAttribute attribute,
            int power)
        {
            List<BattleUnit> units = new List<BattleUnit>(BattleSquad.UnitCount);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                units.Add(new BattleUnit(prefix + i, attribute, power));
            }

            BattleSquad.TryCreate(units, out BattleSquad squad, out BattleError _);

            return squad;
        }

        /// <summary>同じ属性で、POWERだけ個体ごとに変えた7体編成。</summary>
        internal static BattleSquad WithPowers(
            string prefix,
            UnitAttribute attribute,
            int[] powers)
        {
            List<BattleUnit> units = new List<BattleUnit>(BattleSquad.UnitCount);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                units.Add(new BattleUnit(prefix + i, attribute, powers[i]));
            }

            BattleSquad.TryCreate(units, out BattleSquad squad, out BattleError _);

            return squad;
        }

        /// <summary>個体ごとに属性とPOWERを指定した7体編成。</summary>
        internal static BattleSquad Create(
            string prefix,
            UnitAttribute[] attributes,
            int[] powers)
        {
            List<BattleUnit> units = new List<BattleUnit>(BattleSquad.UnitCount);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                units.Add(new BattleUnit(prefix + i, attributes[i], powers[i]));
            }

            BattleSquad.TryCreate(units, out BattleSquad squad, out BattleError _);

            return squad;
        }

        /// <summary>編成の先頭から数えた個体ID。</summary>
        internal static string IdAt(BattleSquad squad, int index)
        {
            return squad.Units[index].InstanceId;
        }
    }
}
