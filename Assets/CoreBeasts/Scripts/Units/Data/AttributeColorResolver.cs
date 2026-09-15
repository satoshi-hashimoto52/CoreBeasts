using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 属性から表示色を決める唯一の窓口。
    /// 上部詳細の立ち絵（SpriteRenderer + シェーダ）と、
    /// 一覧カード・編成枠・ドラッグゴーストの縮小立ち絵（UI）が
    /// 同じ規則で着色されるよう、ここへ集約しています。
    /// </summary>
    public static class AttributeColorResolver
    {
        /// <summary>解決済みの配色。</summary>
        public readonly struct Colors
        {
            public readonly Color Primary;
            public readonly Color Secondary;
            public readonly Color Emission;
            public readonly float EmissionStrength;

            public Colors(Color primary, Color secondary, Color emission, float strength)
            {
                Primary = primary;
                Secondary = secondary;
                Emission = emission;
                EmissionStrength = strength;
            }
        }

        /// <summary>
        /// 属性の組み合わせから配色を解決します。
        /// 単属性  : 一次・二次とも同じ属性の色
        /// 2属性   : 一次は一次属性、二次は二次属性。発光は一次属性側
        /// </summary>
        public static Colors Resolve(
            AttributePalette palette,
            UnitAttribute primaryAttribute,
            bool hasSecondaryAttribute,
            UnitAttribute secondaryAttribute)
        {
            if (palette == null)
            {
                return new Colors(Color.white, Color.white, Color.white, 1f);
            }

            AttributePalette.AttributeColors primary =
                palette.GetColors(primaryAttribute);

            AttributePalette.AttributeColors secondary = hasSecondaryAttribute
                ? palette.GetColors(secondaryAttribute)
                : primary;

            return new Colors(
                primary.PrimaryColor,
                secondary.SecondaryColor,
                primary.EmissionColor,
                primary.EmissionStrength
            );
        }

        /// <summary>個体定義から配色を解決します。</summary>
        public static Colors Resolve(
            AttributePalette palette,
            CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return new Colors(Color.white, Color.white, Color.white, 1f);
            }

            return Resolve(
                palette,
                definition.PrimaryAttribute,
                definition.HasSecondaryAttribute,
                definition.SecondaryAttribute
            );
        }
    }
}
