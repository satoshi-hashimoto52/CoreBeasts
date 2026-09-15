using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>テスト用の所持一覧を組み立てるヘルパー。</summary>
    internal static class TestRosterFactory
    {
        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        internal static CoreBeastRoster Create(int count)
        {
            CoreBeastRoster roster =
                ScriptableObject.CreateInstance<CoreBeastRoster>();

            List<OwnedCoreBeast> owned = new List<OwnedCoreBeast>();

            for (int i = 0; i < count; i++)
            {
                CoreBeastDefinition definition =
                    ScriptableObject.CreateInstance<CoreBeastDefinition>();
                definition.name = "CB_Test" + i;

                SetField(definition, "beastId", "test_" + i);
                SetField(definition, "displayName", "TEST" + i);
                SetField(definition, "primaryAttribute", (UnitAttribute)(i % 3));

                OwnedCoreBeast beast = new OwnedCoreBeast();
                SetField(beast, "instanceId", "inst_" + i);
                SetField(beast, "definition", definition);
                SetField(beast, "level", i + 1);

                owned.Add(beast);
            }

            SetField(roster, "owned", owned);

            return roster;
        }

        internal static void Destroy(CoreBeastRoster roster)
        {
            if (roster == null)
            {
                return;
            }

            for (int i = 0; i < roster.Owned.Count; i++)
            {
                CoreBeastDefinition definition = roster.Owned[i].Definition;

                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }

            UnityEngine.Object.DestroyImmediate(roster);
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
