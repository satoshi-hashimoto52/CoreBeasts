using System;
using System.Collections.Generic;
using System.Text;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 「色別POWER」と「CORE」の一行表示を組み立てる共通処理。
    ///
    /// ステータス画面とバトル画面で同じ規則・同じ色を使うため、
    /// 文字列づくりと色の決定はここだけに置きます。画面ごとに複製しません。
    ///
    /// 出す形:
    ///   単色  58
    ///   2色   28 / 13
    ///
    /// COREの数字はここが返し、COREのアイコンは画面側の小さなGraphicが出します。
    /// アイコンを文字（◇など）で出すと、英語フォントに無い字で警告になるためです。
    /// </summary>
    public static class StatLinePresenter
    {
        /// <summary>2色を区切るスラッシュの色。暗めのニュートラルグレー。</summary>
        public static readonly Color SlashColor = new Color(0.45f, 0.48f, 0.53f, 1f);

        /// <summary>COREの数字。暗背景でも読めるシルバーグレー。</summary>
        public static readonly Color CoreValueColor = new Color(0.80f, 0.83f, 0.88f, 1f);

        /// <summary>COREアイコン。数字より少しだけ落としたシルバーグレー。</summary>
        public static readonly Color CoreIconColor = new Color(0.68f, 0.72f, 0.78f, 1f);

        /// <summary>2色のあいだに入れる区切り。前後の空白を含みます。</summary>
        public const string Separator = " / ";

        /// <summary>
        /// 色別POWERの一行。数字はそれぞれの属性色で出します。
        /// 並び順はユニットデータへの登録順をそのまま使います。
        /// </summary>
        public static string BuildPowerLine(
            IReadOnlyList<AttributePower> attributePowers,
            AttributePalette palette)
        {
            return BuildPowerLine(
                attributePowers, attribute => ResolveColor(attribute, palette));
        }

        /// <summary>
        /// 色の決め方を差し替えられる形。製品はパレットを渡します。
        /// 検査では、Unityのアセットを作らずに色を指定するために使います。
        /// </summary>
        public static string BuildPowerLine(
            IReadOnlyList<AttributePower> attributePowers,
            Func<UnitAttribute, Color> colorOf)
        {
            if (attributePowers == null || attributePowers.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < attributePowers.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(Wrap(Separator, SlashColor));
                }

                builder.Append(Wrap(
                    attributePowers[i].Power.ToString(),
                    colorOf != null
                        ? colorOf(attributePowers[i].Attribute)
                        : Color.white));
            }

            return builder.ToString();
        }

        /// <summary>色を付けない生の一行。長さの検査やログに使います。</summary>
        public static string BuildPlainPowerLine(
            IReadOnlyList<AttributePower> attributePowers)
        {
            if (attributePowers == null || attributePowers.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < attributePowers.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(Separator);
                }

                builder.Append(attributePowers[i].Power);
            }

            return builder.ToString();
        }

        /// <summary>COREの数字。アイコンは画面側のGraphicが出します。</summary>
        public static string BuildCoreValue(int core)
        {
            return Wrap(core.ToString(), CoreValueColor);
        }

        /// <summary>その色の表示色。パレットが無ければ白で出します。</summary>
        public static Color ResolveColor(UnitAttribute attribute, AttributePalette palette)
        {
            if (palette == null)
            {
                return Color.white;
            }

            return palette.GetColors(attribute).PrimaryColor;
        }

        /// <summary>
        /// 属性POWER・COREアイコン・CORE値を、1つのステータス行として並べます。
        ///
        /// 幅はTMPの実測値（preferredWidth）から取ります。固定座標で
        /// 偶然そろえるのではないので、単色（76）でも2色（40 / 21）でも
        /// 間隔が同じに見え、文字サイズを変えても崩れません。
        ///
        /// 座標は必ず<paramref name="container"/>の中央を原点として扱い、
        /// 行全体をまず測ってから安全域へ収めます。
        /// こうしないと、行が長い2色表示のときに左端が親の外へ出て、
        /// 先頭の「40 /」が画面外へ切れてしまいます。
        ///
        /// 置き直すだけで入力には関与しません。呼び出し側が
        /// raycastTarget を false のままにしてください。
        /// </summary>
        /// <param name="power">属性POWERのラベル。</param>
        /// <param name="icon">COREアイコン。null なら詰めて並べます。</param>
        /// <param name="core">CORE値のラベル。</param>
        /// <param name="container">表示先の親。ここからはみ出させません。</param>
        /// <param name="leftEdgeFromContainerLeft">
        /// 親の左端からの開始位置。<c>float.NaN</c> なら中央寄せにします。
        /// </param>
        /// <param name="padding">親の内側に残す左右の余白。</param>
        /// <param name="powerToCoreGap">POWER群とCORE群のあいだ。</param>
        /// <param name="iconToValueGap">アイコンとCORE値のあいだ。</param>
        /// <param name="iconSize">アイコンの一辺。</param>
        /// <param name="forbiddenLeft">
        /// 立ち絵など、行を置いてはいけない帯の左端（親中央基準）。
        /// <c>float.NaN</c> なら禁止帯なし。
        /// </param>
        /// <param name="forbiddenRight">禁止帯の右端（親中央基準）。</param>
        /// <returns>行全体の幅。</returns>
        public static float LayoutRow(
            TMP_Text power,
            Graphic icon,
            TMP_Text core,
            RectTransform container,
            float leftEdgeFromContainerLeft,
            float padding,
            float powerToCoreGap,
            float iconToValueGap,
            float iconSize,
            float forbiddenLeft = float.NaN,
            float forbiddenRight = float.NaN)
        {
            if (power == null || core == null || container == null)
            {
                return 0f;
            }

            // 1. まず行全体を測ります。
            power.ForceMeshUpdate();
            core.ForceMeshUpdate();

            float powerWidth = power.preferredWidth;
            float coreWidth = core.preferredWidth;
            float iconWidth = icon != null ? iconSize : 0f;
            float iconGap = icon != null ? iconToValueGap : 0f;

            float total = powerWidth + powerToCoreGap + iconWidth + iconGap + coreWidth;

            // 2. 親の中央を原点にした、置いてよい範囲を出します。
            float half = container.rect.width * 0.5f;
            float safeMin = -half + padding;
            float safeMax = half - padding;

            // 立ち絵の帯とは重ねません。広いほうの余白側へ寄せます。
            if (!float.IsNaN(forbiddenLeft) && !float.IsNaN(forbiddenRight)
                && forbiddenRight > safeMin && forbiddenLeft < safeMax)
            {
                float leftRoom = forbiddenLeft - safeMin;
                float rightRoom = safeMax - forbiddenRight;

                if (leftRoom >= rightRoom)
                {
                    safeMax = forbiddenLeft;
                }
                else
                {
                    safeMin = forbiddenRight;
                }
            }

            // 3. 希望位置を決め、安全域へクランプします。
            float left = float.IsNaN(leftEdgeFromContainerLeft)
                ? -total * 0.5f
                : -half + leftEdgeFromContainerLeft;

            if (left + total > safeMax)
            {
                left = safeMax - total;
            }

            if (left < safeMin)
            {
                left = safeMin;
            }

            // 4. 親中央を原点として置きます。
            float cursor = left;

            PlaceFromCentre(power.rectTransform, cursor, powerWidth);
            cursor += powerWidth + powerToCoreGap;

            if (icon != null)
            {
                PlaceFromCentre(icon.rectTransform, cursor, iconWidth, iconWidth);
                cursor += iconWidth + iconGap;
            }

            PlaceFromCentre(core.rectTransform, cursor, coreWidth);

            return total;
        }

        /// <summary>
        /// 親の中央を原点に、左端をそろえて置きます。
        /// 縦位置と縦アンカーはそのままにします。
        /// </summary>
        private static void PlaceFromCentre(
            RectTransform rect, float leftFromCentre, float width, float height = float.NaN)
        {
            if (rect == null)
            {
                return;
            }

            // 横は必ず親の中央アンカー。左端アンカーのままだと、
            // 中央基準で計算した負の座標が親の外へ出ます。
            rect.anchorMin = new Vector2(0.5f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(0.5f, rect.anchorMax.y);
            rect.pivot = new Vector2(0f, rect.pivot.y);

            rect.anchoredPosition =
                new Vector2(leftFromCentre, rect.anchoredPosition.y);

            rect.sizeDelta = new Vector2(
                width, float.IsNaN(height) ? rect.sizeDelta.y : height);
        }

        /// <summary>TMPのリッチテキストで色を付けます。</summary>
        public static string Wrap(string value, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + value + "</color>";
        }
    }
}
