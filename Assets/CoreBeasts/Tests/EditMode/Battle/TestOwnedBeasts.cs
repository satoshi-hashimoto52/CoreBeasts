using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using UnityEngine;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 既存の所持データ層と同じ形のテスト用個体を組み立てるヘルパー。
    /// 本番コードへテスト専用のセッターを足さずに済ませるため、
    /// 既存テストと同じくシリアライズ項目へ直接値を入れます。
    ///
    /// 作成した定義アセットはインスタンスごとに保持し、
    /// <see cref="Cleanup"/>で破棄します。テスト間で状態を共有しません。
    /// </summary>
    internal sealed class TestOwnedBeasts
    {
        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<CoreBeastDefinition> created =
            new List<CoreBeastDefinition>();

        internal OwnedCoreBeast Create(
            string instanceId,
            UnitAttribute primary,
            int power)
        {
            return Build(instanceId, primary, false, primary, power);
        }

        internal OwnedCoreBeast CreateDual(
            string instanceId,
            UnitAttribute primary,
            UnitAttribute secondary,
            int power)
        {
            return Build(instanceId, primary, true, secondary, power);
        }

        /// <summary>定義を持たない（対戦へ出せない）個体。</summary>
        internal OwnedCoreBeast CreateWithoutDefinition(string instanceId)
        {
            OwnedCoreBeast owned = new OwnedCoreBeast();
            SetField(owned, "instanceId", instanceId);

            return owned;
        }

        internal List<OwnedCoreBeast> CreateMany(string idPrefix, int count)
        {
            List<OwnedCoreBeast> owned = new List<OwnedCoreBeast>(count);

            for (int i = 0; i < count; i++)
            {
                owned.Add(Create(idPrefix + i, (UnitAttribute)(i % 3), 40 + i));
            }

            return owned;
        }

        /// <summary>作成した定義アセットを破棄します。</summary>
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

        private OwnedCoreBeast Build(
            string instanceId,
            UnitAttribute primary,
            bool hasSecondary,
            UnitAttribute secondary,
            int power)
        {
            CoreBeastDefinition definition =
                ScriptableObject.CreateInstance<CoreBeastDefinition>();

            definition.name = "CB_Battle_" + instanceId;

            SetField(definition, "beastId", "battle_" + instanceId);
            SetField(definition, "primaryAttribute", primary);
            SetField(definition, "hasSecondaryAttribute", hasSecondary);
            SetField(definition, "secondaryAttribute", secondary);
            SetField(definition, "power", power);

            created.Add(definition);

            OwnedCoreBeast owned = new OwnedCoreBeast();
            SetField(owned, "instanceId", instanceId);
            SetField(owned, "definition", definition);
            SetField(owned, "level", 1);

            return owned;
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
