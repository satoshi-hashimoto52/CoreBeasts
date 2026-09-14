using System;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 属性ごとの配色を保持するデータアセット。
    /// 色の定義はこのアセットへ集約し、CoreBeastViewなどのコード側では持ちません。
    /// </summary>
    [CreateAssetMenu(
        fileName = "AttributePalette_Default",
        menuName = "CoreBeasts/Attribute Palette"
    )]
    public sealed class AttributePalette : ScriptableObject
    {
        /// <summary>発光強度の下限値。</summary>
        public const float MinEmissionStrength = 0f;

        /// <summary>1属性ぶんの配色設定。</summary>
        [Serializable]
        public sealed class AttributeColors
        {
            [SerializeField]
            [Tooltip("03_mask_primaryが示す一次領域の色")]
            private Color primaryColor = Color.gray;

            [SerializeField]
            [Tooltip("04_mask_secondaryが示す二次領域の色")]
            private Color secondaryColor = Color.gray;

            [SerializeField]
            [Tooltip("05_mask_emissionが示す発光領域（目・胸部コア）の色")]
            private Color emissionColor = Color.white;

            [SerializeField]
            [Min(MinEmissionStrength)]
            [Tooltip("発光の強さ。負の値は許可しません。")]
            private float emissionStrength = 1f;

            public Color PrimaryColor => primaryColor;

            public Color SecondaryColor => secondaryColor;

            public Color EmissionColor => emissionColor;

            /// <summary>
            /// 発光強度。シリアライズ値が負であっても
            /// <see cref="MinEmissionStrength"/>を下回りません。
            /// </summary>
            public float EmissionStrength =>
                Mathf.Max(MinEmissionStrength, emissionStrength);

            /// <summary>シリアライズ値そのものを下限へ丸めます。</summary>
            internal void ClampEmissionStrength()
            {
                if (emissionStrength < MinEmissionStrength)
                {
                    emissionStrength = MinEmissionStrength;
                }
            }
        }

        [SerializeField]
        private AttributeColors red = new AttributeColors();

        [SerializeField]
        private AttributeColors green = new AttributeColors();

        [SerializeField]
        private AttributeColors blue = new AttributeColors();

        /// <summary>
        /// 属性から配色を取得します。
        /// 属性と配色の対応づけはこのメソッド1箇所へ集約しています。
        /// </summary>
        public AttributeColors GetColors(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return red;

                case UnitAttribute.Green:
                    return green;

                case UnitAttribute.Blue:
                    return blue;

                default:
                    Debug.LogError(
                        $"[AttributePalette] 未知の属性です: {attribute}。" +
                        "Redの配色で代替します。",
                        this
                    );

                    return red;
            }
        }

        private void OnValidate()
        {
            red?.ClampEmissionStrength();
            green?.ClampEmissionStrength();
            blue?.ClampEmissionStrength();
        }
    }
}
