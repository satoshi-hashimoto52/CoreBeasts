using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class AttributePaletteTests
    {
        private const string DefaultAssetPath =
            "Assets/CoreBeasts/Data/AttributePalette_Default.asset";

        private AttributePalette palette;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
        }

        [TearDown]
        public void TearDown()
        {
            if (palette != null)
            {
                UnityEngine.Object.DestroyImmediate(palette);
                palette = null;
            }
        }

        [Test]
        public void UnitAttribute_DefinesExactlyRedGreenBlue()
        {
            Assert.That(
                Enum.GetNames(typeof(UnitAttribute)),
                Is.EquivalentTo(new[] { "Red", "Green", "Blue" })
            );
        }

        [Test]
        public void GetColors_ReturnsColorsForEveryAttribute()
        {
            foreach (UnitAttribute attribute in
                     Enum.GetValues(typeof(UnitAttribute)))
            {
                Assert.That(
                    palette.GetColors(attribute),
                    Is.Not.Null,
                    $"{attribute}の配色が取得できません。"
                );
            }
        }

        [Test]
        public void GetColors_ReturnsDistinctColorsPerAttribute()
        {
            Color red = palette.GetColors(UnitAttribute.Red).PrimaryColor;
            Color green = palette.GetColors(UnitAttribute.Green).PrimaryColor;
            Color blue = palette.GetColors(UnitAttribute.Blue).PrimaryColor;

            Assert.That(red, Is.Not.EqualTo(green));
            Assert.That(green, Is.Not.EqualTo(blue));
            Assert.That(blue, Is.Not.EqualTo(red));
        }

        [Test]
        public void EmissionStrength_NeverGoesBelowZero()
        {
            TestPaletteFactory.OverwriteEmissionStrength(
                palette,
                UnitAttribute.Red,
                -5f
            );

            Assert.That(
                palette.GetColors(UnitAttribute.Red).EmissionStrength,
                Is.EqualTo(AttributePalette.MinEmissionStrength)
            );

            Assert.That(
                palette.GetColors(UnitAttribute.Red).EmissionStrength,
                Is.GreaterThanOrEqualTo(0f)
            );
        }

        [Test]
        public void DefaultAsset_ExistsAndDefinesDistinctAttributeColors()
        {
            AttributePalette asset =
                AssetDatabase.LoadAssetAtPath<AttributePalette>(DefaultAssetPath);

            Assert.That(
                asset,
                Is.Not.Null,
                $"{DefaultAssetPath} が読み込めません。"
            );

            Color red = asset.GetColors(UnitAttribute.Red).PrimaryColor;
            Color green = asset.GetColors(UnitAttribute.Green).PrimaryColor;
            Color blue = asset.GetColors(UnitAttribute.Blue).PrimaryColor;

            Assert.That(red, Is.Not.EqualTo(green));
            Assert.That(green, Is.Not.EqualTo(blue));
            Assert.That(blue, Is.Not.EqualTo(red));

            foreach (UnitAttribute attribute in
                     Enum.GetValues(typeof(UnitAttribute)))
            {
                Assert.That(
                    asset.GetColors(attribute).EmissionStrength,
                    Is.GreaterThanOrEqualTo(0f)
                );
            }
        }
    }
}
