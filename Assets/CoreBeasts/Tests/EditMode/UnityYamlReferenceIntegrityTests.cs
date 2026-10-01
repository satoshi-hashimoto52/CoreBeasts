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
    /// ドキュメント間の「構造参照」を検証します。
    ///
    /// <see cref="UnityYamlHeaderIntegrityTests"/> が class type header の書式と改行を見るのに対し、
    /// こちらは Transform / RectTransform の親子関係と stripped ドキュメントの扱いを見ます。
    ///
    /// 誤検出を避けるため、検査するのは「確実にローカルと判定できる参照」だけです。
    ///   - {fileID: N} … ローカル参照。検査対象。
    ///   - {fileID: N, guid: ..., type: ...} … 外部アセット参照。対象外。
    ///   - {fileID: 0} … null。ルート扱いとして許可。
    ///   - stripped ドキュメント … PrefabInstance 由来のスタブ。実体を持たないため
    ///     相互参照（m_Father / m_Children の往復）は検査しません。
    /// </summary>
    public sealed class UnityYamlReferenceIntegrityTests
    {
        private const int TransformClassId = 4;
        private const int RectTransformClassId = 224;
        private const int MonoBehaviourClassId = 114;

        private const string UnitSetScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const long UnitSetPortraitViewFileId = 1700000001L;
        private const long UnitSetPortraitPrefabInstanceFileId = 1700000100L;

        private static readonly Regex HeaderPattern =
            new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?$", RegexOptions.Compiled);

        /// <summary>ローカル参照のみ。guid を持つ行はこの正規表現に一致しません。</summary>
        private static readonly Regex SequenceRefPattern =
            new Regex(@"^  - \{fileID: (-?\d+)\}$", RegexOptions.Compiled);

        private static readonly Regex FatherPattern =
            new Regex(@"^  m_Father: \{fileID: (-?\d+)\}$", RegexOptions.Compiled);

        private static readonly Regex GameObjectPattern =
            new Regex(@"^  m_GameObject: \{fileID: (-?\d+)\}$", RegexOptions.Compiled);

        // ---------------------------------------------------------------- model

        private sealed class UnityDocument
        {
            public int HeaderLine;
            public int ClassId;
            public long FileId;
            public bool Stripped;
            public string TypeName;

            /// <summary>m_Children の要素と、その要素が書かれている行番号。</summary>
            public readonly List<KeyValuePair<long, int>> Children = new List<KeyValuePair<long, int>>();

            public bool HasFather;
            public long Father;
            public int FatherLine;

            public bool HasGameObject;

            public bool IsTransform
            {
                get { return ClassId == TransformClassId || ClassId == RectTransformClassId; }
            }
        }

        private sealed class UnityYamlFile
        {
            public string RelativePath;
            public readonly List<UnityDocument> Documents = new List<UnityDocument>();
            public readonly Dictionary<long, UnityDocument> ById = new Dictionary<long, UnityDocument>();
        }

        // ------------------------------------------------------------ discovery

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

        private static IEnumerable<string> YamlFiles()
        {
            string root = Path.Combine(AssetsPath, "CoreBeasts");
            string[] patterns = { "*.unity", "*.prefab", "*.asset" };
            List<string> found = new List<string>();

            for (int i = 0; i < patterns.Length; i++)
            {
                found.AddRange(Directory.GetFiles(root, patterns[i], SearchOption.AllDirectories));
            }

            found.Sort(StringComparer.Ordinal);

            for (int i = 0; i < found.Count; i++)
            {
                yield return "Assets/" + found[i].Substring(AssetsPath.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        // --------------------------------------------------------------- parser

        private static UnityYamlFile Parse(string relativePath)
        {
            string full = Path.Combine(ProjectRoot, relativePath);
            Assume.That(File.Exists(full), Is.True, relativePath + " が見つかりません。");

            byte[] bytes = File.ReadAllBytes(full);
            int probe = Math.Min(bytes.Length, 512);

            for (int i = 0; i < probe; i++)
            {
                if (bytes[i] == 0)
                {
                    Assert.Ignore(relativePath + " はバイナリシリアライズのため対象外です。");
                }
            }

            string[] lines = new UTF8Encoding(false).GetString(bytes).Split('\n');
            UnityYamlFile file = new UnityYamlFile { RelativePath = relativePath };
            UnityDocument current = null;
            bool insideChildren = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                Match header = HeaderPattern.Match(line);

                if (header.Success)
                {
                    current = new UnityDocument
                    {
                        HeaderLine = i + 1,
                        ClassId = int.Parse(header.Groups[1].Value),
                        FileId = long.Parse(header.Groups[2].Value),
                        Stripped = header.Groups[3].Success,
                    };

                    file.Documents.Add(current);
                    file.ById[current.FileId] = current;
                    insideChildren = false;
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (current.TypeName == null)
                {
                    Match type = Regex.Match(line, @"^([A-Za-z_]\w*):\s*$");

                    if (type.Success)
                    {
                        current.TypeName = type.Groups[1].Value;
                        continue;
                    }
                }

                // m_Children / m_Father は Transform / RectTransform でのみ解釈します。
                // PrefabInstance の m_Modification など、別文脈のシーケンスを拾わないためです。
                if (!current.IsTransform)
                {
                    if (GameObjectPattern.IsMatch(line))
                    {
                        current.HasGameObject = true;
                    }

                    continue;
                }

                if (line.StartsWith("  m_Children:", StringComparison.Ordinal))
                {
                    insideChildren = !line.TrimEnd().EndsWith("[]", StringComparison.Ordinal);
                    continue;
                }

                if (insideChildren)
                {
                    Match child = SequenceRefPattern.Match(line);

                    if (child.Success)
                    {
                        current.Children.Add(new KeyValuePair<long, int>(
                            long.Parse(child.Groups[1].Value), i + 1));
                        continue;
                    }

                    insideChildren = false;
                }

                Match father = FatherPattern.Match(line);

                if (father.Success)
                {
                    current.HasFather = true;
                    current.Father = long.Parse(father.Groups[1].Value);
                    current.FatherLine = i + 1;
                    continue;
                }

                if (GameObjectPattern.IsMatch(line))
                {
                    current.HasGameObject = true;
                }
            }

            return file;
        }

        /// <summary>「ファイル:行番号 / 親fileID / 問題の参照fileID」形式で報告します。</summary>
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
                sb.Append('\n').Append("  ").Append(violations[i]);
            }

            Assert.Fail(sb.ToString());
        }

        private static string Describe(string relativePath, int line, long parentId, long refId, string reason)
        {
            return relativePath + ":" + line
                + " / 親fileID &" + parentId
                + " / 問題の参照fileID &" + refId
                + " — " + reason;
        }

        private static string ClassOf(UnityDocument doc)
        {
            return "!u!" + doc.ClassId + (doc.Stripped ? " (stripped)" : string.Empty);
        }

        // ------------------------------------------------------- A. m_Children

        /// <summary>m_Children のローカル参照が存在し、Transform / RectTransform であること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void ChildReferencesResolveToTransforms(string relativePath)
        {
            UnityYamlFile file = Parse(relativePath);
            List<string> violations = new List<string>();

            foreach (UnityDocument doc in file.Documents)
            {
                foreach (KeyValuePair<long, int> child in doc.Children)
                {
                    UnityDocument target;

                    if (!file.ById.TryGetValue(child.Key, out target))
                    {
                        violations.Add(Describe(relativePath, child.Value, doc.FileId, child.Key,
                            "m_Children の参照先ドキュメントが存在しません。"));
                        continue;
                    }

                    if (!target.IsTransform)
                    {
                        violations.Add(Describe(relativePath, child.Value, doc.FileId, child.Key,
                            "m_Children の参照先が Transform(!u!4) / RectTransform(!u!224) ではありません（"
                            + ClassOf(target) + " " + target.TypeName + "）。"));
                    }
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>m_Children に並ぶ子の m_Father が、その親を指し返していること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void ChildReferencesAreReciprocatedByFather(string relativePath)
        {
            UnityYamlFile file = Parse(relativePath);
            List<string> violations = new List<string>();

            foreach (UnityDocument doc in file.Documents)
            {
                foreach (KeyValuePair<long, int> child in doc.Children)
                {
                    UnityDocument target;

                    if (!file.ById.TryGetValue(child.Key, out target) || !target.IsTransform)
                    {
                        // 参照先の型は ChildReferencesResolveToTransforms が報告します。
                        continue;
                    }

                    // stripped は実体を持たないスタブのため m_Father を持ちません。
                    if (target.Stripped)
                    {
                        continue;
                    }

                    if (!target.HasFather || target.Father != doc.FileId)
                    {
                        violations.Add(Describe(relativePath, child.Value, doc.FileId, child.Key,
                            "子の m_Father が親を指していません（m_Father=&"
                            + (target.HasFather ? target.Father.ToString() : "なし") + "）。"));
                    }
                }
            }

            Report(relativePath, violations);
        }

        // --------------------------------------------------------- B. m_Father

        /// <summary>m_Father のローカル参照が存在し、Transform / RectTransform であること（0 はルート）。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void FatherReferencesResolveToTransforms(string relativePath)
        {
            UnityYamlFile file = Parse(relativePath);
            List<string> violations = new List<string>();

            foreach (UnityDocument doc in file.Documents)
            {
                if (!doc.HasFather || doc.Father == 0)
                {
                    // m_Father: {fileID: 0} はルートとして正当です。
                    continue;
                }

                UnityDocument parent;

                if (!file.ById.TryGetValue(doc.Father, out parent))
                {
                    violations.Add(Describe(relativePath, doc.FatherLine, doc.Father, doc.FileId,
                        "m_Father の参照先ドキュメントが存在しません。"));
                    continue;
                }

                if (!parent.IsTransform)
                {
                    violations.Add(Describe(relativePath, doc.FatherLine, doc.Father, doc.FileId,
                        "m_Father の参照先が Transform(!u!4) / RectTransform(!u!224) ではありません（"
                        + ClassOf(parent) + " " + parent.TypeName + "）。"));
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>m_Father が指す親の m_Children に、自分が含まれていること。</summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void FatherReferencesAreReciprocatedByChildren(string relativePath)
        {
            UnityYamlFile file = Parse(relativePath);
            List<string> violations = new List<string>();

            foreach (UnityDocument doc in file.Documents)
            {
                if (!doc.HasFather || doc.Father == 0)
                {
                    continue;
                }

                UnityDocument parent;

                if (!file.ById.TryGetValue(doc.Father, out parent) || !parent.IsTransform)
                {
                    // 参照先の型は FatherReferencesResolveToTransforms が報告します。
                    continue;
                }

                // stripped の親は m_Children を持たないため、往復は検査できません。
                if (parent.Stripped)
                {
                    continue;
                }

                bool listed = false;

                foreach (KeyValuePair<long, int> child in parent.Children)
                {
                    if (child.Key == doc.FileId)
                    {
                        listed = true;
                        break;
                    }
                }

                if (!listed)
                {
                    violations.Add(Describe(relativePath, doc.FatherLine, doc.Father, doc.FileId,
                        "親の m_Children に自分が含まれていません。"));
                }
            }

            Report(relativePath, violations);
        }

        // ------------------------------- D. 外部Prefabのコンポーネント参照

        /// <summary>
        /// GUID -> Assets 配下の資産パス。.meta を1度だけ走査します。
        /// </summary>
        private static Dictionary<string, string> BuildGuidIndex()
        {
            Dictionary<string, string> index = new Dictionary<string, string>();

            string[] metas = Directory.GetFiles(
                AssetsPath, "*.meta", SearchOption.AllDirectories);

            Regex guidLine = new Regex(@"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);

            for (int i = 0; i < metas.Length; i++)
            {
                string text;

                try
                {
                    text = File.ReadAllText(metas[i]);
                }
                catch (IOException)
                {
                    continue;
                }

                Match match = guidLine.Match(text);

                if (match.Success && !index.ContainsKey(match.Groups[1].Value))
                {
                    index[match.Groups[1].Value] = metas[i].Substring(0, metas[i].Length - 5);
                }
            }

            return index;
        }

        /// <summary>そのPrefabが持つ fileID と、各 fileID の ClassID。</summary>
        private static Dictionary<long, int> PrefabDocuments(string path)
        {
            Dictionary<long, int> docs = new Dictionary<long, int>();

            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                Match header = HeaderPattern.Match(lines[i].TrimEnd('\r'));

                if (header.Success)
                {
                    docs[long.Parse(header.Groups[2].Value)] =
                        int.Parse(header.Groups[1].Value);
                }
            }

            return docs;
        }

        /// <summary>
        /// Unityが予約している fileID。Prefabの中の文書ではないため、
        /// 「存在しない」と判定してはいけません。
        ///   100100000 : Prefab資産そのもの（m_SourcePrefab が指すもの）
        ///    11500000 : MonoScript
        /// </summary>
        private static readonly HashSet<long> ReservedFileIds =
            new HashSet<long> { 100100000L, 11500000L };

        private static readonly Regex ExternalRefPattern = new Regex(
            @"^\s*(?:- )?(\w+): \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}$",
            RegexOptions.Compiled);

        /// <summary>
        /// 外部Prefabを指す参照の fileID が、そのPrefabの中に実在するか。
        ///
        /// Prefabルートの RectTransform を指したまま MonoBehaviour 型の項目へ入れると、
        /// Unity上では空欄になり Instantiate が null で止まります。
        /// ここでは「実在するか」までを見ます（型の一致は Unity 側の
        /// BattleSceneWiringTests が SerializedObject で確かめます）。
        ///
        /// Unity組み込み参照（Assets の外にある GUID）と、
        /// Prefab以外の資産（ScriptableObject・フォント等）は対象外です。
        /// </summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void ExternalPrefabReferencesResolveInsideThatPrefab(string relativePath)
        {
            Dictionary<string, string> guids = BuildGuidIndex();
            Dictionary<string, Dictionary<long, int>> cache =
                new Dictionary<string, Dictionary<long, int>>();

            string[] lines = File.ReadAllLines(Path.Combine(ProjectRoot, relativePath));
            List<string> violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                Match match = ExternalRefPattern.Match(line);

                if (!match.Success || match.Groups[1].Value == "m_Script")
                {
                    continue;
                }

                string guid = match.Groups[3].Value;

                if (!guids.TryGetValue(guid, out string target) ||
                    !target.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    // Assets の外（Unity組み込み・パッケージ）と、Prefab以外は見ません。
                    continue;
                }

                if (!cache.TryGetValue(target, out Dictionary<long, int> docs))
                {
                    docs = PrefabDocuments(target);
                    cache[target] = docs;
                }

                long fileId = long.Parse(match.Groups[2].Value);

                if (ReservedFileIds.Contains(fileId))
                {
                    continue;
                }

                if (!docs.ContainsKey(fileId))
                {
                    violations.Add(
                        relativePath + ":" + (i + 1) +
                        " / 親fileID &0 / 問題の参照fileID &" + fileId +
                        " — " + match.Groups[1].Value + " が参照する fileID が " +
                        Path.GetFileName(target) + " に存在しません。");
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>
        /// 「この項目はこの型のコンポーネントを指すはず」という対応表。
        ///
        /// 期待する型はスクリプト名で持ち、GUIDは .cs.meta から引きます。
        /// GUIDを直接書かないので、資産を作り直しても表が古くなりません。
        /// </summary>
        private static readonly Dictionary<string, string> ExpectedComponentScripts =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // キーは「所有クラス.項目名」です。項目名だけだと
                // SquadBarView.slotPrefab のような同名項目を巻き込みます。
                { "BattleUnitWheelView.itemPrefab", "BattleUnitWheelItemView" },
                { "BattleDeployTransitionView.ghostPrefab", "BattleUnitWheelItemView" },
                { "BattleHistoryLaneView.slotPrefab", "BattleHistorySlotView" },
            };

        /// <summary>スクリプト名 -> .cs.meta の GUID。</summary>
        private static string ScriptGuid(string scriptName)
        {
            string[] metas = Directory.GetFiles(
                AssetsPath, scriptName + ".cs.meta", SearchOption.AllDirectories);

            if (metas.Length != 1)
            {
                return null;
            }

            Match match = Regex.Match(
                File.ReadAllText(metas[0]), @"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>Prefab内の1ドキュメントの ClassID と m_Script GUID。</summary>
        private static void PrefabDocument(
            string path, long fileId, out int classId, out string scriptGuid)
        {
            classId = 0;
            scriptGuid = null;

            string[] lines = File.ReadAllLines(path);
            bool inside = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                Match header = HeaderPattern.Match(line);

                if (header.Success)
                {
                    inside = long.Parse(header.Groups[2].Value) == fileId;

                    if (inside)
                    {
                        classId = int.Parse(header.Groups[1].Value);
                    }

                    continue;
                }

                if (!inside)
                {
                    continue;
                }

                Match script = Regex.Match(line, @"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})");

                if (script.Success)
                {
                    scriptGuid = script.Groups[1].Value;
                    return;
                }
            }
        }

        /// <summary>
        /// 既知のコンポーネント項目が、期待する型のMonoBehaviourを指しているか。
        ///
        /// GUIDが合っていてもPrefabルートのRectTransformを指していると、
        /// Unity上では空欄になり Instantiate が null で止まります。
        /// 「実在するか」だけでは見抜けないため、ClassIDと m_Script GUID まで見ます。
        ///
        /// 対応表に載っていない項目は一切触りません。
        /// 正当なGameObject参照やUnity組み込み参照を巻き添えにしないためです。
        /// </summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void KnownComponentReferencesPointAtTheExpectedScript(string relativePath)
        {
            Dictionary<string, string> guids = BuildGuidIndex();

            string[] lines = File.ReadAllLines(Path.Combine(ProjectRoot, relativePath));
            List<string> violations = new List<string>();

            // 今どのコンポーネントの中を読んでいるかを追い、所有クラスで絞り込みます。
            string owner = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (HeaderPattern.IsMatch(line))
                {
                    owner = null;
                    continue;
                }

                if (line.StartsWith("  m_EditorClassIdentifier:", StringComparison.Ordinal))
                {
                    string value = line.Substring(line.IndexOf(':') + 1).Trim();

                    if (value.Length > 0)
                    {
                        int dot = value.LastIndexOf('.');
                        owner = dot >= 0 ? value.Substring(dot + 1) : value;
                    }

                    continue;
                }

                Match match = ExternalRefPattern.Match(line);

                if (!match.Success || owner == null)
                {
                    continue;
                }

                string field = match.Groups[1].Value;

                if (!ExpectedComponentScripts.TryGetValue(
                        owner + "." + field, out string scriptName))
                {
                    continue;
                }

                long fileId = long.Parse(match.Groups[2].Value);
                string guid = match.Groups[3].Value;

                if (!guids.TryGetValue(guid, out string target) ||
                    !target.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    violations.Add(
                        relativePath + ":" + (i + 1) + " / " + field +
                        " / expected: " + scriptName +
                        " / GUID " + guid + " が Prefab として解決できません。");

                    continue;
                }

                PrefabDocument(target, fileId, out int classId, out string scriptGuid);

                string expectedGuid = ScriptGuid(scriptName);

                if (classId != MonoBehaviourClassId || scriptGuid != expectedGuid)
                {
                    violations.Add(
                        relativePath + ":" + (i + 1) +
                        " / フィールド名: " + field +
                        " / expected型: " + scriptName + " (!u!114, script " +
                        (expectedGuid ?? "?") + ")" +
                        " / actual: !u!" + classId +
                        " (script " + (scriptGuid ?? "なし") + ")" +
                        " / GUID: " + guid +
                        " / fileID: " + fileId +
                        " — " + Path.GetFileName(target) + " の中で型が一致しません。");
                }
            }

            Report(relativePath, violations);
        }

        // ------------------------------------------------- C. stripped の扱い

        /// <summary>
        /// stripped ドキュメントを通常ドキュメントと誤認しないこと。
        /// stripped は PrefabInstance 由来のスタブで、m_PrefabInstance を持ち m_GameObject を持ちません。
        /// </summary>
        [Test]
        [TestCaseSource(nameof(YamlFiles))]
        public void StrippedDocumentsAreRecognisedAsPrefabStubs(string relativePath)
        {
            UnityYamlFile file = Parse(relativePath);
            List<string> violations = new List<string>();

            foreach (UnityDocument doc in file.Documents)
            {
                if (!doc.Stripped)
                {
                    continue;
                }

                if (doc.HasGameObject)
                {
                    violations.Add(Describe(relativePath, doc.HeaderLine, doc.FileId, doc.FileId,
                        "stripped ドキュメントが m_GameObject を持っています（スタブとして不正）。"));
                }

                if (doc.Children.Count > 0)
                {
                    violations.Add(Describe(relativePath, doc.HeaderLine, doc.FileId, doc.FileId,
                        "stripped ドキュメントが m_Children を持っています（スタブとして不正）。"));
                }
            }

            Report(relativePath, violations);
        }

        /// <summary>
        /// UnitSet.unity の portraitView（&amp;1700000001）が stripped MonoBehaviour として存在すること。
        /// stripped が失われると Unity は通常の MonoBehaviour として読もうとし、参照が外れます。
        /// </summary>
        [Test]
        public void UnitSetPortraitViewIsStrippedMonoBehaviour()
        {
            UnityYamlFile file = Parse(UnitSetScenePath);
            UnityDocument doc;

            Assert.That(file.ById.TryGetValue(UnitSetPortraitViewFileId, out doc), Is.True,
                UnitSetScenePath + " に &" + UnitSetPortraitViewFileId + " が存在しません。");

            Assert.That(doc.ClassId, Is.EqualTo(MonoBehaviourClassId),
                UnitSetScenePath + ":" + doc.HeaderLine + " / 親fileID &" + UnitSetPortraitPrefabInstanceFileId
                + " / 問題の参照fileID &" + UnitSetPortraitViewFileId
                + " — MonoBehaviour(!u!114) ではありません。");

            Assert.That(doc.Stripped, Is.True,
                UnitSetScenePath + ":" + doc.HeaderLine + " / 親fileID &" + UnitSetPortraitPrefabInstanceFileId
                + " / 問題の参照fileID &" + UnitSetPortraitViewFileId
                + " — stripped キーワードが失われています。"
                + " 通常の MonoBehaviour と誤認され portraitView の参照が外れます。");

            Assert.That(doc.HasGameObject, Is.False,
                UnitSetScenePath + ":" + doc.HeaderLine + " / 親fileID &" + UnitSetPortraitPrefabInstanceFileId
                + " / 問題の参照fileID &" + UnitSetPortraitViewFileId
                + " — stripped スタブが m_GameObject を持っています。");
        }
    }
}
