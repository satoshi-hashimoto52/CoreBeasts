using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// Unity YAML（*.unity / *.prefab / *.asset）をテキストとして読み、
    /// class type header の構造だけを検証します。
    /// OpenScene を使わないため、Unity がパースできないほど壊れたファイルでも
    /// 原因の行番号を提示できます。
    ///
    /// 検出対象は「改行欠落による癒着」です。例:
    ///   %TAG !u! tag:unity3d.com,2011:--- !u!29 &amp;1
    ///   m_OcclusionCullingData: {fileID: 0}--- !u!104 &amp;2
    /// これらは Unity 側では
    ///   "Failed to parse data as no preceding class type header in text file"
    /// として現れます。
    /// </summary>
    public sealed class UnityYamlHeaderIntegrityTests
    {
        private const string ScanRoot = "CoreBeasts";
        private const string YamlDirective = "%YAML 1.1";
        private const string TagDirective = "%TAG !u! tag:unity3d.com,2011:";
        private const string HeaderMarker = "--- !u!";

        /// <summary>--- !u!&lt;ClassID&gt; &amp;&lt;fileID&gt; [stripped] だけを正当なヘッダーとみなします。</summary>
        private static readonly Regex HeaderPattern =
            new Regex(@"^--- !u!\d+ &-?\d+( stripped)?$", RegexOptions.Compiled);

        /// <summary>
        /// プロジェクトルート（Assets の親）。Unity 上では Application.dataPath を使い、
        /// Unity 外のテキスト実行では作業ディレクトリへフォールバックします。
        /// </summary>
        private static string ProjectRoot
        {
            get
            {
                string dataPath = null;

                try
                {
                    dataPath = Application.dataPath;
                }
                catch (Exception)
                {
                    // Unity ランタイム外ではネイティブ側を解決できないため無視します。
                }

                if (!string.IsNullOrEmpty(dataPath) && Directory.Exists(dataPath))
                {
                    return Directory.GetParent(dataPath).FullName;
                }

                return Directory.GetCurrentDirectory();
            }
        }

        private static string AssetsPath
        {
            get { return Path.Combine(ProjectRoot, "Assets"); }
        }

        /// <summary>
        /// 走査対象。.meta / C# / Library / Packages / Temp は Assets/CoreBeasts 配下のみを
        /// 拡張子で絞ることで自動的に除外されます。
        /// </summary>
        private static IEnumerable<string> YamlFiles()
        {
            string root = Path.Combine(AssetsPath, ScanRoot);
            string[] patterns = { "*.unity", "*.prefab", "*.asset" };
            List<string> found = new List<string>();

            for (int i = 0; i < patterns.Length; i++)
            {
                found.AddRange(Directory.GetFiles(root, patterns[i], SearchOption.AllDirectories));
            }

            found.Sort(StringComparer.Ordinal);

            for (int i = 0; i < found.Count; i++)
            {
                // テスト名を読みやすくするため Assets/ 相対で渡します。
                yield return "Assets/" + found[i].Substring(AssetsPath.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        private static string[] ReadLines(string relativePath)
        {
            string full = Path.Combine(ProjectRoot, relativePath);
            Assume.That(File.Exists(full), Is.True, relativePath + " が見つかりません。");

            byte[] bytes = File.ReadAllBytes(full);

            // ForceBinary シリアライズされた資産はテキスト検証の対象外です。
            int probe = Math.Min(bytes.Length, 512);
            for (int i = 0; i < probe; i++)
            {
                if (bytes[i] == 0)
                {
                    Assert.Ignore(relativePath + " はバイナリシリアライズのため対象外です。");
                }
            }

            return new UTF8Encoding(false).GetString(bytes).Split('\n');
        }

        private static void Report(string relativePath, List<string> violations)
        {
            if (violations.Count == 0)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(relativePath).Append(" に ").Append(violations.Count).Append(" 件の問題があります。");

            for (int i = 0; i < violations.Count; i++)
            {
                sb.Append('\n').Append("  ").Append(relativePath).Append(':').Append(violations[i]);
            }

            Assert.Fail(sb.ToString());
        }

        [Test]
        public void ScanFindsUnityYamlFiles()
        {
            List<string> files = new List<string>(YamlFiles());
            Assert.That(files, Is.Not.Empty, "Assets/" + ScanRoot + " 配下に Unity YAML が見つかりません。");
        }

        /// <summary>--- !u! は必ず行頭から始まること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void ClassTypeHeaderStartsAtLineStart(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                int at = line.IndexOf(HeaderMarker, StringComparison.Ordinal);

                if (at > 0)
                {
                    violations.Add((i + 1) + " class type header が行頭にありません（" + at
                        + " 文字目に癒着）: " + line);
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>前ドキュメント末尾とヘッダーの癒着（}--- !u!）が無いこと。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void NoDocumentBodyGluedToClassTypeHeader(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (line.Contains("}" + HeaderMarker))
                {
                    violations.Add((i + 1) + " 直前のドキュメント末尾にヘッダーが癒着しています: " + line);
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>%TAG 行が単独行であり、--- を含まないこと。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void TagDirectiveIsStandaloneLine(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (!line.StartsWith("%TAG", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.Contains("---"))
                {
                    violations.Add((i + 1) + " %TAG 行に --- が含まれています（改行欠落）: " + line);
                }
                else if (line != TagDirective)
                {
                    violations.Add((i + 1) + " %TAG 行が \"" + TagDirective + "\" と一致しません: " + line);
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>先頭 3 行が %YAML / %TAG / 最初の class type header であること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void FileStartsWithExpectedPreamble(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            if (lines.Length < 3)
            {
                violations.Add("1 行数が 3 未満のため Unity YAML として不正です。");
                Report(relativePath, violations);
                return;
            }

            if (lines[0].TrimEnd('\r') != YamlDirective)
            {
                violations.Add("1 1 行目が \"" + YamlDirective + "\" ではありません: " + lines[0].TrimEnd('\r'));
            }

            if (lines[1].TrimEnd('\r') != TagDirective)
            {
                violations.Add("2 2 行目が \"" + TagDirective + "\" ではありません: " + lines[1].TrimEnd('\r'));
            }

            if (!HeaderPattern.IsMatch(lines[2].TrimEnd('\r')))
            {
                violations.Add("3 3 行目が class type header ではありません: " + lines[2].TrimEnd('\r'));
            }

            Report(relativePath, violations);
        }

        /// <summary>各ドキュメントヘッダーが独立した 1 行で、正しい書式であること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void EachDocumentHeaderOccupiesItsOwnLine(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (!line.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!HeaderPattern.IsMatch(line))
                {
                    violations.Add((i + 1) + " ドキュメント区切りが \"--- !u!<ClassID> &<fileID>\" 単独行ではありません: " + line);
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>
        /// 1つのドキュメントの中に、同じマッピングキーが二度現れないこと。
        ///
        /// YAML では重複キーは不正です。Unity は静かに読み飛ばすか既定値へ戻すため、
        /// Console にエラーが出ないまま「Imageが白い」「raycastTargetが効かない」
        /// といった形で表面化します。
        ///
        /// 特に、ドキュメントの型キー（例: MonoBehaviour:）が二度出る形は、
        /// 既存ドキュメントを雛形として書き出したときに起こりがちです。
        /// </summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void NoDocumentRepeatsAMappingKey(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();

            Regex typeLine = new Regex(@"^([A-Za-z_]\w*):\s*$");
            Regex topKey = new Regex(@"^  ([A-Za-z_]\w*):");

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            bool seenType = false;
            int headerLine = 0;
            string header = string.Empty;

            for (int i = 0; i <= lines.Length; i++)
            {
                bool end = i == lines.Length;
                string line = end ? string.Empty : lines[i].TrimEnd('\r');

                if (end || HeaderPattern.IsMatch(line))
                {
                    seen.Clear();
                    seenType = false;
                    headerLine = i + 1;
                    header = line;

                    continue;
                }

                if (headerLine == 0)
                {
                    continue;
                }

                if (typeLine.IsMatch(line))
                {
                    if (seenType)
                    {
                        violations.Add(
                            (i + 1) + " 型キーが二度あります: " + line.Trim() +
                            "（ドキュメント " + header + "）");
                    }

                    seenType = true;

                    continue;
                }

                Match key = topKey.Match(line);

                if (key.Success && !seen.Add(key.Groups[1].Value))
                {
                    violations.Add(
                        (i + 1) + " 同じキーが二度あります: " + key.Groups[1].Value +
                        "（ドキュメント " + header + "）");
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>
        /// ヘッダー無しで始まるデータブロックが無いこと。
        /// Unity の "no preceding class type header" と同じ条件を先回りで検出します。
        /// </summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void NoDataBlockWithoutPrecedingClassTypeHeader(string relativePath)
        {
            string[] lines = ReadLines(relativePath);
            List<string> violations = new List<string>();
            bool insideDocument = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (HeaderPattern.IsMatch(line))
                {
                    insideDocument = true;
                    continue;
                }

                if (insideDocument || line.Length == 0)
                {
                    continue;
                }

                // ヘッダー到達前に許されるのは %YAML / %TAG のディレクティブだけです。
                if (line.StartsWith("%", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add((i + 1) + " class type header より前にデータ行があります: " + line);

                if (violations.Count >= 5)
                {
                    violations.Add((i + 2) + " 以降は同様のため省略します。");
                    break;
                }
            }

            Report(relativePath, violations);
        }
    }
}
