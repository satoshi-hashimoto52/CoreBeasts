using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// <see cref="AttributePalette"/>の配色を、MaterialPropertyBlock経由で
    /// SpriteRendererへ個体別に適用します。
    /// 共有Material（M_CoreBeastTint）およびそのアセット値は変更しません。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class CoreBeastView : MonoBehaviour
    {
        private static readonly int PrimaryColorId =
            Shader.PropertyToID("_Primary_Color");

        private static readonly int SecondaryColorId =
            Shader.PropertyToID("_Secondary_Color");

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_Emission_Color");

        private static readonly int EmissionStrengthId =
            Shader.PropertyToID("_Emission_Strength");

        [SerializeField]
        [Tooltip("色を適用する対象のSpriteRenderer")]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        [Tooltip("属性ごとの配色を持つデータアセット")]
        private AttributePalette palette;

        [SerializeField]
        [Tooltip("一次属性。_Primary_Colorの取得元になります。")]
        private UnitAttribute primaryAttribute = UnitAttribute.Red;

        [SerializeField]
        [Tooltip("ONにすると2属性として扱い、_Secondary_Colorを二次属性から取ります。")]
        private bool useSecondaryAttribute;

        [SerializeField]
        [Tooltip("二次属性。2属性使用時のみ_Secondary_Colorへ反映されます。")]
        private UnitAttribute secondaryAttribute = UnitAttribute.Blue;

        private MaterialPropertyBlock propertyBlock;
        private bool hasLoggedMissingReference;

        /// <summary>一次属性。</summary>
        public UnitAttribute PrimaryAttribute => primaryAttribute;

        /// <summary>二次属性。</summary>
        public UnitAttribute SecondaryAttribute => secondaryAttribute;

        /// <summary>2属性として扱うかどうか。</summary>
        public bool UseSecondaryAttribute => useSecondaryAttribute;

        /// <summary>単属性として設定し、即座に反映します。</summary>
        public void SetAttribute(UnitAttribute attribute)
        {
            primaryAttribute = attribute;
            useSecondaryAttribute = false;

            Apply();
        }

        /// <summary>2属性として設定し、即座に反映します。</summary>
        public void SetAttributes(UnitAttribute primary, UnitAttribute secondary)
        {
            primaryAttribute = primary;
            secondaryAttribute = secondary;
            useSecondaryAttribute = true;

            Apply();
        }

        /// <summary>
        /// 現在の属性設定をMaterialPropertyBlockへ書き込み、
        /// SpriteRendererへ適用します。
        /// </summary>
        public void Apply()
        {
            if (spriteRenderer == null || palette == null)
            {
                LogMissingReferenceOnce();
                return;
            }

            hasLoggedMissingReference = false;

            // 縮小立ち絵(UI)と同じ規則で解決するため、共通処理へ委譲します。
            AttributeColorResolver.Colors colors = AttributeColorResolver.Resolve(
                palette, primaryAttribute, useSecondaryAttribute, secondaryAttribute);

            propertyBlock ??= new MaterialPropertyBlock();

            // 既存のブロック内容を保ったまま色だけを差し替えます。
            spriteRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(PrimaryColorId, ToRenderColor(colors.Primary));
            propertyBlock.SetColor(SecondaryColorId, ToRenderColor(colors.Secondary));

            // 発光は混色せず、一次属性側をそのまま使います。
            propertyBlock.SetColor(EmissionColorId, ToRenderColor(colors.Emission));
            propertyBlock.SetFloat(EmissionStrengthId, colors.EmissionStrength);

            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnEnable()
        {
            hasLoggedMissingReference = false;

            Apply();
        }

        // OnValidateはEditorからのみ呼ばれます。
        // UnityEditor名前空間へは依存しないため、ビルドへ影響しません。
        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Apply();
        }

        /// <summary>
        /// Inspectorで指定した色はsRGBです。
        /// Linearカラースペースでは、Materialアセットと同じ見え方になるよう
        /// リニアへ変換してからシェーダへ渡します。
        /// </summary>
        private static Color ToRenderColor(Color color)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            {
                return color;
            }

            Color converted = color.linear;
            converted.a = color.a;

            return converted;
        }

        private void LogMissingReferenceOnce()
        {
            if (hasLoggedMissingReference)
            {
                return;
            }

            hasLoggedMissingReference = true;

            string missing;

            if (spriteRenderer == null && palette == null)
            {
                missing = "SpriteRendererとAttributePalette";
            }
            else if (spriteRenderer == null)
            {
                missing = "SpriteRenderer";
            }
            else
            {
                missing = "AttributePalette";
            }

            Debug.LogError(
                $"[CoreBeastView] GameObject「{name}」の{missing}が" +
                "未設定のため、属性カラーを適用できません。",
                this
            );
        }
    }
}
