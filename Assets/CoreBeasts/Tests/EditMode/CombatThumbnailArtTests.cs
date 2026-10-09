using System.Collections.Generic;

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    public sealed class CombatThumbnailArtTests
    {
        private const string Root = "Assets/CoreBeasts/Art/Units/Volx/Combat/";

        private readonly List<Object> created = new List<Object>();

        private static readonly object[] Cases =
        {
            new object[] { "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset", Root + "volx_combat_red.png" },
            new object[] { "Assets/CoreBeasts/Data/Testing/CB_Volx_Green_TEST.asset", Root + "volx_combat_green.png" },
            new object[] { "Assets/CoreBeasts/Data/Testing/CB_Volx_Blue_TEST.asset", Root + "volx_combat_blue.png" },
            new object[] { "Assets/CoreBeasts/Data/Testing/CB_Volx_RedBlue_TEST.asset", Root + "volx_combat_red_blue.png" },
        };

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [TestCaseSource(nameof(Cases))]
        public void DefinitionsUseTheirReadableCombatPortrait(string definitionPath, string spritePath)
        {
            CoreBeastDefinition definition =
                AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(definitionPath);
            Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            Assert.That(definition, Is.Not.Null, definitionPath);
            Assert.That(expected, Is.Not.Null, spritePath);
            Assert.That(definition.Thumbnail, Is.SameAs(expected));
            Assert.That(expected.texture.width, Is.EqualTo(1024));
            Assert.That(expected.texture.height, Is.EqualTo(1024));

            TextureImporter importer =
                AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.GetPlatformTextureSettings("iOS").maxTextureSize,
                Is.EqualTo(1024));
        }

        [Test]
        public void DefinitionPortraitReplacesLegacyMasksWithoutChangingTheDefinition()
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(
                "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset");
            BeastThumbnailView view = CreateView(
                out RawImage baseLayer,
                out RawImage primaryLayer,
                out RawImage secondaryLayer,
                out Texture2D legacy);

            view.Show(definition, null);

            Assert.That(baseLayer.texture, Is.SameAs(definition.Thumbnail.texture));
            Assert.That(baseLayer.enabled, Is.True);
            Assert.That(primaryLayer.enabled, Is.False);
            Assert.That(secondaryLayer.enabled, Is.False);
            Assert.That(definition.Thumbnail.texture, Is.Not.SameAs(legacy));
        }

        [Test]
        public void LegacyThreeLayerArtStillWorksWhenDefinitionHasNoPortrait()
        {
            CoreBeastDefinition asset = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(
                "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset");
            CoreBeastDefinition legacyDefinition = Object.Instantiate(asset);
            created.Add(legacyDefinition);

            SerializedObject serialized = new SerializedObject(legacyDefinition);
            serialized.FindProperty("thumbnail").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BeastThumbnailView view = CreateView(
                out RawImage baseLayer,
                out RawImage primaryLayer,
                out RawImage secondaryLayer,
                out Texture2D legacy);

            view.Show(asset, null);
            view.Show(legacyDefinition, null);

            Assert.That(baseLayer.texture, Is.SameAs(legacy));
            Assert.That(primaryLayer.enabled, Is.True);
            Assert.That(secondaryLayer.enabled, Is.True);
        }

        private BeastThumbnailView CreateView(
            out RawImage baseLayer,
            out RawImage primaryLayer,
            out RawImage secondaryLayer,
            out Texture2D legacy)
        {
            GameObject root = new GameObject("Thumbnail", typeof(RectTransform));
            created.Add(root);

            baseLayer = Layer(root.transform, "Base");
            primaryLayer = Layer(root.transform, "Primary");
            secondaryLayer = Layer(root.transform, "Secondary");

            legacy = new Texture2D(2, 2);
            created.Add(legacy);
            baseLayer.texture = legacy;

            BeastThumbnailView view = root.AddComponent<BeastThumbnailView>();
            view.Bind(baseLayer, primaryLayer, secondaryLayer);
            return view;
        }

        private static RawImage Layer(Transform parent, string name)
        {
            GameObject layer = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            layer.transform.SetParent(parent, false);
            return layer.GetComponent<RawImage>();
        }
    }
}
