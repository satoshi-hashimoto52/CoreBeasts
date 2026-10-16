using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// <see cref="AttributePalette"/>の配色を、MaterialPropertyBlock経由で
    /// SpriteRendererへ個体別に適用します。
    /// 共有Material（M_CoreBeastTint）およびそのアセット値は変更しません。
    ///
    /// <see cref="CoreBeastArtSet"/>を渡すと、その種の6レイヤー素材（元の絵とマスク3枚）も
    /// 同じ MaterialPropertyBlock で差し替えます（Phase 8）。素材が無ければ Material の既定（VOLX）のままです。
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

        private static readonly int BaseTextureId = Shader.PropertyToID("_Base_Texture");
        private static readonly int PrimaryMaskId = Shader.PropertyToID("_Primary_Mask");
        private static readonly int SecondaryMaskId = Shader.PropertyToID("_Secondary_Mask");
        private static readonly int EmissionMaskId = Shader.PropertyToID("_Emission_Mask");

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

        [SerializeField]
        [Tooltip("6レイヤーの立ち絵素材。未設定なら Material の既定の素材のままです。")]
        private CoreBeastArtSet art;

        private MaterialPropertyBlock propertyBlock;
        private bool hasLoggedMissingReference;
        private Sprite defaultSprite;
        private bool hasDefaultSprite;
        private bool appliedArtTextures;

        /// <summary>一次属性。</summary>
        public UnitAttribute PrimaryAttribute => primaryAttribute;

        /// <summary>二次属性。</summary>
        public UnitAttribute SecondaryAttribute => secondaryAttribute;

        /// <summary>2属性として扱うかどうか。</summary>
        public bool UseSecondaryAttribute => useSecondaryAttribute;

        /// <summary>今の素材。null なら Material の既定です。</summary>
        public CoreBeastArtSet Art => art;

        /// <summary>
        /// 種の素材を設定します（null で既定へ戻します）。色は変えません。
        /// 属性の設定（<see cref="SetAttribute"/> など）の前に呼ぶと、1回の反映で済みます。
        /// </summary>
        public void SetArt(CoreBeastArtSet value)
        {
            art = value;

            Apply();
        }

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

            // 既存のブロック内容を保ったまま色と素材だけを差し替えます。
            spriteRenderer.GetPropertyBlock(propertyBlock);

            ApplyArt();

            propertyBlock.SetVector(PrimaryColorId, ToRenderColor(colors.Primary));
            propertyBlock.SetVector(SecondaryColorId, ToRenderColor(colors.Secondary));

            // 発光は混色せず、一次属性側をそのまま使います。
            propertyBlock.SetVector(EmissionColorId, ToRenderColor(colors.Emission));
            propertyBlock.SetFloat(EmissionStrengthId, colors.EmissionStrength);

            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// 素材があれば、SpriteRenderer の Sprite（形と範囲）とシェーダーの4レイヤーを差し替えます。
        /// 無ければ、差し替えていた分だけを Material と prefab の既定へ戻します。
        /// </summary>
        private void ApplyArt()
        {
            if (!hasDefaultSprite)
            {
                defaultSprite = spriteRenderer.sprite;
                hasDefaultSprite = true;
            }

            if (art != null && art.IsComplete)
            {
                if (spriteRenderer.sprite != art.Preview)
                {
                    spriteRenderer.sprite = art.Preview;
                }

                propertyBlock.SetTexture(BaseTextureId, art.BaseTexture);
                propertyBlock.SetTexture(PrimaryMaskId, art.PrimaryMask);
                propertyBlock.SetTexture(SecondaryMaskId, art.SecondaryMask);
                propertyBlock.SetTexture(EmissionMaskId, art.EmissionMask);
                appliedArtTextures = true;

                return;
            }

            if (spriteRenderer.sprite != defaultSprite)
            {
                spriteRenderer.sprite = defaultSprite;
            }

            if (appliedArtTextures)
            {
                // MaterialPropertyBlock から1項目だけ消す手段が無いため、空にしてから色を書き直します。
                propertyBlock.Clear();
                appliedArtTextures = false;
            }
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
        /// Inspectorで指定した色はsRGBです。Linearカラースペースでは1回だけリニアへ変換し、
        /// 変換の入らない SetVector でシェーダへ渡します（Phase 8）。
        ///
        /// 以前は変換済みの値を SetColor へ渡していました。SetColor は Unity が自動で sRGB → リニアを行うため、
        /// 2回変換されてパレットより暗く表示されていました。変換は事前生成サムネイルと同じ
        /// <see cref="CoreBeastTint.ToLinear"/> です。
        /// </summary>
        private static Vector4 ToRenderColor(Color color)
        {
            return CoreBeastTint.ToShaderVector(color, QualitySettings.activeColorSpace);
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
