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

        /// <summary>
        /// 所持カードの外周フレームと薄い背景に使う色を解決します。
        ///
        /// 立ち絵のマスク着色（<see cref="Resolve(AttributePalette, CoreBeastDefinition)"/>）が
        /// 一次色と二次色を混ぜるのに対し、カードは「属性そのものの色」を出します。
        /// そのため両側とも各属性の<see cref="AttributePalette.AttributeColors.PrimaryColor"/>を使い、
        /// 単属性では左上・右下が同じ色になります。
        ///
        /// 色の実体は<see cref="AttributePalette"/>だけが持ちます。ここでは選ぶだけです。
        /// </summary>
        public static void ResolveCardColors(
            AttributePalette palette,
            CoreBeastDefinition definition,
            out Color primary,
            out Color secondary,
            out bool isDual)
        {
            if (palette == null || definition == null)
            {
                primary = Color.white;
                secondary = Color.white;
                isDual = false;

                return;
            }

            primary = palette.GetColors(definition.PrimaryAttribute).PrimaryColor;
            isDual = definition.HasSecondaryAttribute;

            secondary = isDual
                ? palette.GetColors(definition.SecondaryAttribute).PrimaryColor
                : primary;
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
