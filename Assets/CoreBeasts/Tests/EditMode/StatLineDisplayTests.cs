using System.Collections.Generic;
using System.Text.RegularExpressions;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 「色別POWER」と「CORE」の一行表示。
    /// 文字列づくりと色はステータス画面もバトル画面も同じ処理を通します。
    /// </summary>
    public sealed class StatLineDisplayTests
    {
        /// <summary>
        /// 検査用の色。Unityのアセットを作らずに済むよう、関数で差し込みます。
        /// 製品は<see cref="AttributePalette"/>を渡しますが、
        /// 組み立ての規則はどちらでも同じです。
        /// </summary>
        private static Color Palette(UnitAttribute attribute)
        {
            switch (attribute)
            {
                case UnitAttribute.Red:
                    return new Color(0.89f, 0.30f, 0.30f);

                case UnitAttribute.Green:
                    return new Color(0.34f, 0.76f, 0.42f);

                default:
                    return new Color(0.29f, 0.56f, 0.89f);
            }
        }

        private static List<AttributePower> Single(UnitAttribute a, int p)
        {
            return new List<AttributePower> { new AttributePower(a, p) };
        }

        private static List<AttributePower> Dual(
            UnitAttribute a, int pa, UnitAttribute b, int pb)
        {
            return new List<AttributePower>
            {
                new AttributePower(a, pa),
                new AttributePower(b, pb),
            };
        }

        private static string Strip(string rich)
        {
            return Regex.Replace(rich, "<.*?>", string.Empty);
        }

        private static int ColourTagCount(string rich)
        {
            return Regex.Matches(rich, "<color=#").Count;
        }

        // ---------------- 単色 ----------------

        [Test]
        public void ASingleColourShowsExactlyOneNumber()
        {
            string line = StatLinePresenter.BuildPowerLine(
                Single(UnitAttribute.Red, 58), Palette);

            Assert.That(Strip(line), Is.EqualTo("58"));
            Assert.That(
                Strip(line),
                Does.Not.Contain("/"),
                "単色にスラッシュを出してはいけません。");

            Assert.That(ColourTagCount(line), Is.EqualTo(1), "数値は1個です。");
        }

        // ---------------- 2色 ----------------

        [Test]
        public void TwoColoursShowTwoNumbersAndOneSlash()
        {
            string line = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Red, 28, UnitAttribute.Blue, 13), Palette);

            Assert.That(Strip(line), Is.EqualTo("28 / 13"));

            Assert.That(
                Regex.Matches(Strip(line), "/").Count,
                Is.EqualTo(1),
                "スラッシュは1個です。");

            // 数値2個 + スラッシュ1個 = 色タグ3個。
            Assert.That(ColourTagCount(line), Is.EqualTo(3));
        }

        [Test]
        public void TheOrderFollowsTheRegisteredOrder()
        {
            string forward = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Red, 40, UnitAttribute.Blue, 21), Palette);

            string reversed = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Blue, 21, UnitAttribute.Red, 40), Palette);

            Assert.That(Strip(forward), Is.EqualTo("40 / 21"));
            Assert.That(
                Strip(reversed),
                Is.EqualTo("21 / 40"),
                "並び順は登録順のままにします。");
        }

        // ---------------- 色 ----------------

        [Test]
        public void EachNumberCarriesItsOwnAttributeColour()
        {
            string line = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Red, 28, UnitAttribute.Blue, 13), Palette);

            string red = ColorUtility.ToHtmlStringRGB(Palette(UnitAttribute.Red));
            string blue = ColorUtility.ToHtmlStringRGB(Palette(UnitAttribute.Blue));

            Assert.That(red, Is.Not.EqualTo(blue), "属性ごとに色が違うこと。");

            Assert.That(line, Does.Contain("<color=#" + red + ">28</color>"));
            Assert.That(line, Does.Contain("<color=#" + blue + ">13</color>"));
        }

        [Test]
        public void TheSlashIsADarkNeutralGreyNotAnAttributeColour()
        {
            Color slash = StatLinePresenter.SlashColor;

            Assert.That(slash.r, Is.EqualTo(slash.g).Within(0.12f), "無彩色にします。");
            Assert.That(slash.g, Is.EqualTo(slash.b).Within(0.12f));
            Assert.That(slash.r, Is.LessThan(0.6f), "暗めにします。");

            string line = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Red, 28, UnitAttribute.Blue, 13), Palette);

            Assert.That(
                line,
                Does.Contain("<color=#" + ColorUtility.ToHtmlStringRGB(slash) + "> / </color>"));
        }

        [Test]
        public void TheCoreValueIsAReadableSilverGrey()
        {
            Color core = StatLinePresenter.CoreValueColor;

            Assert.That(core.r, Is.EqualTo(core.g).Within(0.1f), "シルバーグレーにします。");
            Assert.That(core.g, Is.EqualTo(core.b).Within(0.1f));

            Assert.That(
                core.r,
                Is.GreaterThan(0.7f),
                "暗背景に埋もれない明るさにします。");

            // 使用済み表示（DefeatTint）より明るいこと。
            Assert.That(
                core.r,
                Is.GreaterThan(DefeatTint.Neutral.r),
                "COREが使用済み表示より暗くなっています。");

            string line = StatLinePresenter.BuildCoreValue(84);

            Assert.That(Strip(line), Is.EqualTo("84"));
            Assert.That(
                line,
                Does.Contain("<color=#" + ColorUtility.ToHtmlStringRGB(core) + ">"));
        }

        [Test]
        public void TheCoreIconIsSilverGreyAndDimmerThanTheNumber()
        {
            Assert.That(
                StatLinePresenter.CoreIconColor.r,
                Is.LessThan(StatLinePresenter.CoreValueColor.r),
                "アイコンは数字より少し落とします。");

            Color icon = StatLinePresenter.CoreIconColor;

            Assert.That(icon.r, Is.EqualTo(icon.g).Within(0.1f));
            Assert.That(icon.g, Is.EqualTo(icon.b).Within(0.1f));
        }

        // ---------------- 文字として出してはいけないもの ----------------

        [Test]
        public void NoPowerOrCoreWordAppearsInTheLine()
        {
            string power = StatLinePresenter.BuildPowerLine(
                Dual(UnitAttribute.Red, 28, UnitAttribute.Blue, 13), Palette);

            string core = StatLinePresenter.BuildCoreValue(84);

            Assert.That(power, Does.Not.Contain("POWER"), "常設ラベルは廃止です。");
            Assert.That(core, Does.Not.Contain("CORE"), "常設ラベルは廃止です。");
        }

        [Test]
        public void TheLineIsPlainAsciiSoNoGlyphIsMissing()
        {
            // COREアイコンは Graphic が出します。◇ のような文字は使いません。
            string line =
                Strip(StatLinePresenter.BuildPowerLine(
                    Dual(UnitAttribute.Red, 28, UnitAttribute.Blue, 13), Palette)) +
                Strip(StatLinePresenter.BuildCoreValue(84));

            for (int i = 0; i < line.Length; i++)
            {
                Assert.That(
                    line[i],
                    Is.LessThanOrEqualTo((char)0x7E),
                    "英語フォントに無い文字が混ざっています: " + line[i]);
            }
        }

        // ---------------- 両画面で同じ値 ----------------

        [Test]
        public void BothScreensBuildTheSameStringFromTheSameData()
        {
            List<AttributePower> data = Dual(UnitAttribute.Red, 40, UnitAttribute.Blue, 21);

            // ステータス画面もバトル画面も、この1つの処理だけを呼びます。
            string first = StatLinePresenter.BuildPowerLine(data, Palette);
            string second = StatLinePresenter.BuildPowerLine(data, Palette);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(Strip(first), Is.EqualTo("40 / 21"));
        }

        [Test]
        public void ThePlainLineMatchesTheColouredOne()
        {
            List<AttributePower> data = Dual(UnitAttribute.Red, 40, UnitAttribute.Blue, 21);

            Assert.That(
                StatLinePresenter.BuildPlainPowerLine(data),
                Is.EqualTo(Strip(StatLinePresenter.BuildPowerLine(data, Palette))));
        }

        // ---------------- 幅 ----------------

        [Test]
        public void TheLineStaysShortEnoughForOneRow()
        {
            // いちばん長い形（3桁 / 3桁 と 3桁のCORE）でも短いままであること。
            string line =
                Strip(StatLinePresenter.BuildPowerLine(
                    Dual(UnitAttribute.Red, 999, UnitAttribute.Blue, 999), Palette));

            Assert.That(
                line.Length,
                Is.LessThanOrEqualTo(10),
                "1行に収まらない長さです: " + line);

            Assert.That(Strip(StatLinePresenter.BuildCoreValue(999)).Length, Is.EqualTo(3));
        }

        [Test]
        public void AnEmptyLoadoutProducesNothingRatherThanBreaking()
        {
            Assert.That(
                StatLinePresenter.BuildPowerLine(new List<AttributePower>(), Palette),
                Is.Empty);

            Assert.That(StatLinePresenter.BuildPowerLine(null, Palette), Is.Empty);
        }
    }
}
