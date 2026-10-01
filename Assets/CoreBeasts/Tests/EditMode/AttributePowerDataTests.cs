using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 全ユニットアセットが「単色または2色 ＋ 色別POWER」の仕様どおりかを、
    /// テキストとして読んで確かめます。
    ///
    /// Scene も AssetDatabase も開かないため、そのまま実行できます。
    /// 3色ユニットや、色とPOWERの件数ずれを、黙って通さないための網です。
    /// </summary>
    public sealed class AttributePowerDataTests
    {
        /// <summary>
        /// 実行時の作業ディレクトリに依存せず、上へ辿って Assets を探します。
        /// </summary>
        private static string DataRoot
        {
            get
            {
                string directory = Directory.GetCurrentDirectory();

                for (int depth = 0; depth < 8 && directory != null; depth++)
                {
                    string candidate = Path.Combine(directory, "Assets/CoreBeasts/Data");

                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = Path.GetDirectoryName(directory);
                }

                return "Assets/CoreBeasts/Data";
            }
        }

        /// <summary>1アセットから読み取った定義。</summary>
        private sealed class Definition
        {
            public string Path;
            public string BeastId;
            public string DisplayName;
            public int Core;
            public int LegacyPower;
            public readonly List<AttributePower> Powers = new List<AttributePower>();
        }

        private static List<Definition> LoadAll()
        {
            List<Definition> all = new List<Definition>();

            if (!Directory.Exists(DataRoot))
            {
                return all;
            }

            string[] files = Directory.GetFiles(DataRoot, "*.asset", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);

                if (!text.Contains("beastId:"))
                {
                    continue;
                }

                Definition definition = new Definition
                {
                    Path = files[i].Replace('\\', '/'),
                    BeastId = Scalar(text, "beastId"),
                    DisplayName = Scalar(text, "displayName"),
                    Core = Number(text, "core"),
                    LegacyPower = Number(text, "power"),
                };

                foreach (Match match in Regex.Matches(
                    text, @"^  - attribute: (\d+)\r?\n    power: (-?\d+)", RegexOptions.Multiline))
                {
                    definition.Powers.Add(new AttributePower(
                        (UnitAttribute)int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value)));
                }

                all.Add(definition);
            }

            return all;
        }

        private static string Scalar(string text, string key)
        {
            Match match = Regex.Match(text, @"^  " + key + @": (.*)$", RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static int Number(string text, string key)
        {
            string raw = Scalar(text, key);

            return int.TryParse(raw, out int value) ? value : 0;
        }

        [Test]
        public void ThereIsAtLeastOneUnitToCheck()
        {
            Assert.That(LoadAll().Count, Is.GreaterThan(0), "ユニットアセットが見つかりません。");
        }

        [Test]
        public void EveryUnitHasOneOrTwoColours()
        {
            foreach (Definition definition in LoadAll())
            {
                Assert.That(
                    definition.Powers.Count,
                    Is.InRange(AttributeLoadout.MinAttributes, AttributeLoadout.MaxAttributes),
                    definition.Path + " の色数が 1〜2 ではありません（" +
                    definition.Powers.Count + "）。");
            }
        }

        [Test]
        public void NoUnitHasThreeOrMoreColours()
        {
            foreach (Definition definition in LoadAll())
            {
                Assert.That(
                    definition.Powers.Count,
                    Is.LessThanOrEqualTo(2),
                    definition.Path + " が3色以上です。3色ユニットは実装しません。");
            }
        }

        [Test]
        public void NoUnitRepeatsAColour()
        {
            foreach (Definition definition in LoadAll())
            {
                HashSet<UnitAttribute> seen = new HashSet<UnitAttribute>();

                for (int i = 0; i < definition.Powers.Count; i++)
                {
                    Assert.That(
                        seen.Add(definition.Powers[i].Attribute),
                        Is.True,
                        definition.Path + " が同じ色を2回持っています。");
                }
            }
        }

        [Test]
        public void EveryColourPowerIsPositive()
        {
            foreach (Definition definition in LoadAll())
            {
                for (int i = 0; i < definition.Powers.Count; i++)
                {
                    Assert.That(
                        definition.Powers[i].Power,
                        Is.GreaterThan(0),
                        definition.Path + " の " + definition.Powers[i].Attribute +
                        " のPOWERが0以下です。");
                }
            }
        }

        [Test]
        public void EveryUnitPassesTheSharedValidator()
        {
            foreach (Definition definition in LoadAll())
            {
                Assert.That(
                    AttributeLoadout.Validate(definition.Powers),
                    Is.EqualTo(AttributeLoadoutError.None),
                    definition.Path + " の構成が仕様から外れています。");
            }
        }

        [Test]
        public void SingleColourUnitsHaveExactlyOneValueAndDualUnitsHaveTwo()
        {
            foreach (Definition definition in LoadAll())
            {
                bool dual = definition.Powers.Count == 2;

                Assert.That(
                    definition.Powers.Count,
                    Is.EqualTo(dual ? 2 : 1),
                    definition.Path + " の色数とPOWERの件数が一致しません。");
            }
        }

        [Test]
        public void EveryUnitKeepsItsIdentityAndCore()
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (Definition definition in LoadAll())
            {
                Assert.That(
                    string.IsNullOrEmpty(definition.BeastId),
                    Is.False,
                    definition.Path + " の beastId が空です。");

                Assert.That(
                    ids.Add(definition.BeastId),
                    Is.True,
                    definition.BeastId + " が重複しています。");

                Assert.That(
                    string.IsNullOrEmpty(definition.DisplayName),
                    Is.False,
                    definition.Path + " の displayName が空です。");

                Assert.That(
                    definition.Core,
                    Is.GreaterThan(0),
                    definition.Path + " の CORE が 0 以下です。");
            }
        }

        [Test]
        public void DualColourPowersAreHeldBelowTheOldSinglePower()
        {
            // 2色ユニットはPOWER勝負で単色に不利になりやすい値にします。
            // 主属性は旧POWERの 45〜55%、副属性は 20〜35% が目安です。
            foreach (Definition definition in LoadAll())
            {
                if (definition.Powers.Count != 2)
                {
                    continue;
                }

                int legacy = definition.LegacyPower;

                Assume.That(
                    legacy,
                    Is.GreaterThan(0),
                    definition.Path + " に旧POWERが残っていません。");

                float primary = definition.Powers[0].Power / (float)legacy;
                float secondary = definition.Powers[1].Power / (float)legacy;

                Assert.That(
                    primary,
                    Is.InRange(0.45f, 0.55f),
                    definition.Path + " の主属性POWERが旧値の45〜55%から外れています。");

                Assert.That(
                    secondary,
                    Is.InRange(0.20f, 0.35f),
                    definition.Path + " の副属性POWERが旧値の20〜35%から外れています。");

                Assert.That(
                    definition.Powers[0].Power,
                    Is.GreaterThan(definition.Powers[1].Power),
                    definition.Path + " は主属性のほうを高くします。");

                // 2色の合計が、単色1体ぶんより明確に小さいこと。
                Assert.That(
                    definition.Powers[0].Power + definition.Powers[1].Power,
                    Is.LessThan(legacy),
                    definition.Path + " の2色合計が旧POWER以上です。抑えが効いていません。");
            }
        }

        [Test]
        public void SingleColourUnitsKeepTheirOldPower()
        {
            foreach (Definition definition in LoadAll())
            {
                if (definition.Powers.Count != 1)
                {
                    continue;
                }

                Assume.That(definition.LegacyPower, Is.GreaterThan(0));

                Assert.That(
                    definition.Powers[0].Power,
                    Is.EqualTo(definition.LegacyPower),
                    definition.Path + " の単色POWERが旧値から変わっています。");
            }
        }
    }
}
