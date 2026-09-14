using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoreBeasts.Units.Tests
{
    public sealed class CoreBeastViewTests
    {
        private const string SharedMaterialPath =
            "Assets/CoreBeasts/Shaders/M_CoreBeastTint.mat";

        private static readonly int PrimaryColorId =
            Shader.PropertyToID("_Primary_Color");

        private static readonly int SecondaryColorId =
            Shader.PropertyToID("_Secondary_Color");

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_Emission_Color");

        private static readonly int EmissionStrengthId =
            Shader.PropertyToID("_Emission_Strength");

        private AttributePalette palette;
        private GameObject gameObjectUnderTest;
        private SpriteRenderer spriteRenderer;
        private CoreBeastView view;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();

            gameObjectUnderTest = new GameObject("VolxUnderTest");

            // 参照を差し込む前にOnEnableが走らないよう、無効な状態で組み立てます。
            gameObjectUnderTest.SetActive(false);

            spriteRenderer = gameObjectUnderTest.AddComponent<SpriteRenderer>();
            view = gameObjectUnderTest.AddComponent<CoreBeastView>();

            SetPrivateField(view, "spriteRenderer", spriteRenderer);
            SetPrivateField(view, "palette", palette);

            gameObjectUnderTest.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObjectUnderTest != null)
            {
                Object.DestroyImmediate(gameObjectUnderTest);
                gameObjectUnderTest = null;
            }

            if (palette != null)
            {
                Object.DestroyImmediate(palette);
                palette = null;
            }
        }

        [Test]
        public void SingleAttribute_UsesSameAttributeForPrimaryAndSecondary()
        {
            view.SetAttribute(UnitAttribute.Green);
            Color singleGreenPrimary = ReadColor(PrimaryColorId);
            Color singleGreenSecondary = ReadColor(SecondaryColorId);

            view.SetAttribute(UnitAttribute.Red);
            Color singleRedPrimary = ReadColor(PrimaryColorId);
            Color singleRedSecondary = ReadColor(SecondaryColorId);

            Assert.That(singleGreenPrimary, Is.Not.EqualTo(singleRedPrimary));
            Assert.That(singleGreenSecondary, Is.Not.EqualTo(singleRedSecondary));

            // 単属性の二次色は、2属性で同じ属性を二次に指定したときと一致するはず。
            view.SetAttributes(UnitAttribute.Red, UnitAttribute.Green);

            Assert.That(
                ReadColor(SecondaryColorId),
                Is.EqualTo(singleGreenSecondary),
                "単属性時の_Secondary_Colorが、その属性のsecondaryColorではありません。"
            );
        }

        [Test]
        public void DualAttribute_PutsSecondAttributeColorIntoSecondarySlot()
        {
            view.SetAttribute(UnitAttribute.Red);
            Color redPrimary = ReadColor(PrimaryColorId);
            Color redSecondary = ReadColor(SecondaryColorId);

            view.SetAttribute(UnitAttribute.Blue);
            Color blueSecondary = ReadColor(SecondaryColorId);

            view.SetAttributes(UnitAttribute.Red, UnitAttribute.Blue);

            Assert.That(
                ReadColor(PrimaryColorId),
                Is.EqualTo(redPrimary),
                "_Primary_Colorは一次属性から取る必要があります。"
            );

            Assert.That(
                ReadColor(SecondaryColorId),
                Is.EqualTo(blueSecondary),
                "_Secondary_Colorは二次属性から取る必要があります。"
            );

            Assert.That(
                ReadColor(SecondaryColorId),
                Is.Not.EqualTo(redSecondary),
                "2属性なのに二次色が一次属性のままです。"
            );
        }

        [Test]
        public void DualAttribute_KeepsEmissionFromPrimaryAttribute()
        {
            view.SetAttribute(UnitAttribute.Red);
            Color redEmission = ReadColor(EmissionColorId);
            float redStrength = ReadFloat(EmissionStrengthId);

            view.SetAttributes(UnitAttribute.Red, UnitAttribute.Blue);

            Assert.That(ReadColor(EmissionColorId), Is.EqualTo(redEmission));
            Assert.That(ReadFloat(EmissionStrengthId), Is.EqualTo(redStrength));
        }

        [Test]
        public void SetAttribute_ReturnsToSingleAttribute()
        {
            view.SetAttribute(UnitAttribute.Red);
            Color redSecondary = ReadColor(SecondaryColorId);

            view.SetAttributes(UnitAttribute.Red, UnitAttribute.Blue);
            Assert.That(view.UseSecondaryAttribute, Is.True);

            view.SetAttribute(UnitAttribute.Red);

            Assert.That(view.UseSecondaryAttribute, Is.False);
            Assert.That(view.PrimaryAttribute, Is.EqualTo(UnitAttribute.Red));
            Assert.That(ReadColor(SecondaryColorId), Is.EqualTo(redSecondary));
        }

        [Test]
        public void Apply_UsesPropertyBlockAndLeavesSharedMaterialUntouched()
        {
            Material shared =
                AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);

            Assume.That(
                shared,
                Is.Not.Null,
                $"{SharedMaterialPath} が読み込めません。"
            );

            Assume.That(shared.HasProperty(PrimaryColorId), Is.True);

            spriteRenderer.sharedMaterial = shared;

            Color materialPrimaryBefore = shared.GetColor(PrimaryColorId);
            Color materialSecondaryBefore = shared.GetColor(SecondaryColorId);

            view.SetAttribute(UnitAttribute.Green);

            Assert.That(
                shared.GetColor(PrimaryColorId),
                Is.EqualTo(materialPrimaryBefore),
                "共有Materialの_Primary_Colorが書き換えられました。"
            );

            Assert.That(
                shared.GetColor(SecondaryColorId),
                Is.EqualTo(materialSecondaryBefore),
                "共有Materialの_Secondary_Colorが書き換えられました。"
            );

            Assert.That(
                spriteRenderer.sharedMaterial,
                Is.SameAs(shared),
                "Materialのインスタンス化が発生しました。"
            );

            Assert.That(
                spriteRenderer.HasPropertyBlock(),
                Is.True,
                "MaterialPropertyBlockが適用されていません。"
            );

            Assert.That(
                ReadColor(PrimaryColorId),
                Is.Not.EqualTo(materialPrimaryBefore),
                "PropertyBlockへ属性カラーが書き込まれていません。"
            );
        }

        [Test]
        public void AppliedEmissionStrength_IsNeverNegative()
        {
            TestPaletteFactory.OverwriteEmissionStrength(
                palette,
                UnitAttribute.Blue,
                -12f
            );

            view.SetAttribute(UnitAttribute.Blue);

            Assert.That(ReadFloat(EmissionStrengthId), Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Apply_WithMissingReferences_LogsOnceAndDoesNotThrow()
        {
            GameObject orphan = new GameObject("OrphanBeast");
            orphan.SetActive(false);

            CoreBeastView orphanView = orphan.AddComponent<CoreBeastView>();

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[CoreBeastView\].*OrphanBeast")
            );

            // OnEnableでApplyが走り、ここで1回だけエラーが出ます。
            orphan.SetActive(true);

            // 2回目以降は同じエラーを繰り返しません。
            Assert.DoesNotThrow(() => orphanView.Apply());
            Assert.DoesNotThrow(() => orphanView.Apply());

            LogAssert.NoUnexpectedReceived();

            Object.DestroyImmediate(orphan);
        }

        private Color ReadColor(int propertyId)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);

            return block.GetColor(propertyId);
        }

        private float ReadFloat(int propertyId)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);

            return block.GetFloat(propertyId);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            Assert.That(
                field,
                Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} が見つかりません。"
            );

            field.SetValue(target, value);
        }
    }
}
