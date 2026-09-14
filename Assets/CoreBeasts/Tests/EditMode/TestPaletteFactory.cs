using System;
using System.Reflection;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// テスト用のAttributePaletteを組み立てるヘルパー。
    /// 本番コードへテスト専用APIを足さないため、privateフィールドへは
    /// リフレクションで書き込みます。
    /// </summary>
    internal static class TestPaletteFactory
    {
        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>属性ごとに明確に異なる色を持つパレットを作ります。</summary>
        internal static AttributePalette Create()
        {
            AttributePalette palette =
                ScriptableObject.CreateInstance<AttributePalette>();

            SetColors(
                palette,
                "red",
                new Color(0.80f, 0.10f, 0.10f, 1f),
                new Color(0.90f, 0.40f, 0.30f, 1f),
                new Color(1.00f, 0.40f, 0.30f, 0.72f),
                1.5f
            );

            SetColors(
                palette,
                "green",
                new Color(0.10f, 0.70f, 0.20f, 1f),
                new Color(0.50f, 0.90f, 0.40f, 1f),
                new Color(0.40f, 1.00f, 0.50f, 0.72f),
                2.0f
            );

            SetColors(
                palette,
                "blue",
                new Color(0.10f, 0.30f, 0.90f, 1f),
                new Color(0.40f, 0.60f, 1.00f, 1f),
                new Color(0.30f, 0.70f, 1.00f, 0.72f),
                0.5f
            );

            return palette;
        }

        /// <summary>指定属性のemissionStrengthへ、検証用の値を直接書き込みます。</summary>
        internal static void OverwriteEmissionStrength(
            AttributePalette palette,
            UnitAttribute attribute,
            float rawValue)
        {
            object entry = GetEntry(palette, FieldNameOf(attribute));

            SetField(entry, "emissionStrength", rawValue);
        }

        private static string FieldNameOf(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return "red";

                case UnitAttribute.Green:
                    return "green";

                case UnitAttribute.Blue:
                    return "blue";

                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute));
            }
        }

        private static void SetColors(
            AttributePalette palette,
            string fieldName,
            Color primary,
            Color secondary,
            Color emission,
            float emissionStrength)
        {
            object entry = GetEntry(palette, fieldName);

            SetField(entry, "primaryColor", primary);
            SetField(entry, "secondaryColor", secondary);
            SetField(entry, "emissionColor", emission);
            SetField(entry, "emissionStrength", emissionStrength);
        }

        private static object GetEntry(AttributePalette palette, string fieldName)
        {
            FieldInfo field =
                typeof(AttributePalette).GetField(fieldName, FieldFlags);

            if (field == null)
            {
                throw new MissingFieldException(
                    nameof(AttributePalette),
                    fieldName
                );
            }

            object entry = field.GetValue(palette);

            if (entry == null)
            {
                entry = Activator.CreateInstance(
                    field.FieldType,
                    nonPublic: true
                );

                field.SetValue(palette, entry);
            }

            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, FieldFlags);

            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().Name,
                    fieldName
                );
            }

            field.SetValue(target, value);
        }
    }
}
