using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class UiTextCatalogTests
    {
        private const string CatalogPath =
            "Assets/CoreBeasts/Data/UiTextCatalog_EN.asset";

        private UiTextCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<UiTextCatalog>(CatalogPath);
        }

        [Test]
        public void Catalog_UsesLetterInitialsForAttributes()
        {
            Assume.That(catalog, Is.Not.Null, CatalogPath + " が読み込めません。");

            Assert.That(catalog.GetAttributeSymbol(UnitAttribute.Red), Is.EqualTo("R"));
            Assert.That(catalog.GetAttributeSymbol(UnitAttribute.Green), Is.EqualTo("G"));
            Assert.That(catalog.GetAttributeSymbol(UnitAttribute.Blue), Is.EqualTo("B"));

            Assert.That(catalog.GetAttributeLabel(UnitAttribute.Red), Is.EqualTo("RED"));
            Assert.That(catalog.GetAttributeLabel(UnitAttribute.Green), Is.EqualTo("GREEN"));
            Assert.That(catalog.GetAttributeLabel(UnitAttribute.Blue), Is.EqualTo("BLUE"));
        }

        [Test]
        public void Catalog_BuildsDualAttributeTextForRedBlue()
        {
            Assume.That(catalog, Is.Not.Null);

            CoreBeastDefinition definition =
                ScriptableObject.CreateInstance<CoreBeastDefinition>();

            try
            {
                SetField(definition, "primaryAttribute", UnitAttribute.Red);
                SetField(definition, "hasSecondaryAttribute", true);
                SetField(definition, "secondaryAttribute", UnitAttribute.Blue);

                Assert.That(
                    catalog.BuildAttributeSymbol(definition),
                    Is.EqualTo("R/B")
                );

                Assert.That(
                    catalog.BuildAttributeLabel(definition),
                    Is.EqualTo("RED / BLUE")
                );
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Catalog_ProvidesRemovalMessage()
        {
            Assume.That(catalog, Is.Not.Null);

            Assert.That(
                catalog.RemovedFromSquad,
                Is.EqualTo("REMOVED FROM SQUAD")
            );
        }

        [Test]
        public void Catalog_UsesOnlyAsciiSoTheDefaultFontCanRenderIt()
        {
            Assume.That(catalog, Is.Not.Null);

            string[] values =
            {
                catalog.Home, catalog.UnitSet, catalog.CoreBeasts,
                catalog.MySquad, catalog.SaveSet, catalog.EmptySlot,
                catalog.NoSelection, catalog.RemovedFromSquad,
                catalog.GetAttributeSymbol(UnitAttribute.Red),
                catalog.GetAttributeSymbol(UnitAttribute.Green),
                catalog.GetAttributeSymbol(UnitAttribute.Blue),
                catalog.FormatSetName("1"), catalog.FormatLevel(18),
                catalog.FormatCost(24), catalog.FormatPower(76),
                catalog.FormatCore(62), catalog.FormatSaved("SET 1"),
            };

            foreach (string value in values)
            {
                foreach (char c in value ?? string.Empty)
                {
                    Assert.That(
                        c, Is.LessThan((char)128),
                        $"既定フォントに無い文字が含まれています: '{c}' in \"{value}\""
                    );
                }
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.That(field, Is.Not.Null, name + " が見つかりません。");
            field.SetValue(target, value);
        }
    }
}
