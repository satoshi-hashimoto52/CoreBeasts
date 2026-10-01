using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 表示用データを伴うテスト素材を組み立てるヘルパー。
    /// 本番コードへテスト専用のセッターを足さずに済ませるため、
    /// 既存テストと同じくシリアライズ項目へ直接値を入れます。
    ///
    /// 作成したアセットは<see cref="Cleanup"/>で破棄します。
    /// </summary>
    internal sealed class TestBattleCards
    {
        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<Object> created = new List<Object>();

        /// <summary>1種ぶんの定義を作ります。</summary>
        internal CoreBeastDefinition CreateDefinition(
            string beastId,
            UnitAttribute primary,
            int power,
            bool hasSecondary = false,
            UnitAttribute secondary = UnitAttribute.Blue)
        {
            CoreBeastDefinition definition =
                ScriptableObject.CreateInstance<CoreBeastDefinition>();

            definition.name = "CB_" + beastId;

            SetField(definition, "beastId", beastId);
            SetField(definition, "displayName", beastId.ToUpperInvariant());
            SetField(definition, "primaryAttribute", primary);
            SetField(definition, "hasSecondaryAttribute", hasSecondary);
            SetField(definition, "secondaryAttribute", secondary);
            SetField(definition, "power", power);
            SetField(definition, "core", power / 2);
            SetField(definition, "skillName", "SKILL " + beastId);
            SetField(definition, "skillDescription", "Test skill for " + beastId);

            created.Add(definition);

            return definition;
        }

        /// <summary>所持個体を作ります。</summary>
        internal OwnedCoreBeast CreateOwned(
            string instanceId,
            UnitAttribute primary,
            int power,
            int level = 1)
        {
            OwnedCoreBeast owned = new OwnedCoreBeast();

            SetField(owned, "instanceId", instanceId);
            SetField(owned, "definition", CreateDefinition(instanceId, primary, power));
            SetField(owned, "level", level);

            return owned;
        }

        /// <summary>所持一覧を作ります。属性は Red / Green / Blue を順に割り当てます。</summary>
        internal CoreBeastRoster CreateRoster(int count, string prefix = "own_")
        {
            CoreBeastRoster roster =
                ScriptableObject.CreateInstance<CoreBeastRoster>();

            roster.name = "Roster_BattleUiTest";

            List<OwnedCoreBeast> owned = new List<OwnedCoreBeast>(count);

            for (int i = 0; i < count; i++)
            {
                owned.Add(CreateOwned(
                    prefix + i,
                    (UnitAttribute)(i % 3),
                    40 + i,
                    i + 1));
            }

            SetField(roster, "owned", owned);
            created.Add(roster);

            return roster;
        }

        /// <summary>7体ぶんの表示用データを作ります。</summary>
        internal List<BattleUnitCard> CreateCards(int count, string prefix = "card_")
        {
            List<BattleUnitCard> cards = new List<BattleUnitCard>(count);

            for (int i = 0; i < count; i++)
            {
                BattleUnitCard.TryCreate(
                    prefix + i,
                    i + 1,
                    CreateDefinition(prefix + i, (UnitAttribute)(i % 3), 40 + i),
                    out BattleUnitCard card);

                cards.Add(card);
            }

            return cards;
        }

        /// <summary>7体そろった編成を作ります。</summary>
        internal BattleSideRoster CreateSide(string prefix = "card_")
        {
            BattleSideRoster.TryCreate(
                CreateCards(BattleSquad.UnitCount, prefix),
                out BattleSideRoster side,
                out BattleError _);

            return side;
        }

        /// <summary>作成したアセットを破棄します。</summary>
        internal void Cleanup()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, FieldFlags);

            if (field == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, name);
            }

            field.SetValue(target, value);
        }
    }
}
