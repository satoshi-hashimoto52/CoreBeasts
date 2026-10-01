using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoreBeasts.EditorTools
{
    /// <summary>
    /// Missing Script（参照先を失った MonoBehaviour）を洗い出す診断。
    ///
    /// Console の
    ///   "The referenced script on this Behaviour (Game Object '&lt;null&gt;') is missing!"
    /// が、プロジェクトの資産に由来するのか、Play Mode の一時シーンに由来するのかを
    /// 切り分けるために使います。
    ///
    /// 何の参照だったか分からないまま消すのは危険なので、この診断は報告だけを行い、
    /// 自動では何も削除しません。
    /// </summary>
    public static class MissingScriptReport
    {
        private const string Menu = "CoreBeasts/診断/Missing Script を探す";

        [MenuItem(Menu)]
        public static void Run()
        {
            StringBuilder report = new StringBuilder();
            int total = 0;

            total += ScanOpenScenes(report);
            total += ScanPrefabs(report);
            total += ScanScriptableObjects(report);

            if (total == 0)
            {
                Debug.Log(
                    "[CoreBeasts] Missing Script は見つかりませんでした。\n" +
                    "Console に出ていたものは、Play Mode の一時シーン" +
                    "（Temp/__Backupscenes）由来の可能性があります。");

                return;
            }

            Debug.LogWarning(
                "[CoreBeasts] Missing Script を " + total + " 件見つけました。\n" +
                report);
        }

        private const string FullMenu = "CoreBeasts/診断/Missing Script を全走査（Play Mode 可）";

        /// <summary>
        /// ロード中の全Scene・DontDestroyOnLoad・実行時生成物まで含めて走査します。
        ///
        /// <see cref="Run"/> は「開いているシーンと資産」しか見ないため、
        /// Play Mode 中にだけ現れる一時オブジェクト（DragGhost など）や
        /// DontDestroyOnLoad へ移った object を取りこぼします。
        /// Console の警告が資産由来かどうかを確定させるには、こちらを Play Mode 中に実行します。
        ///
        /// 報告だけを行い、自動では何も削除しません。
        /// </summary>
        [MenuItem(FullMenu)]
        public static void RunFullScan()
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine("[CoreBeasts] Missing Script 全走査");
            report.AppendLine("  Play Mode: " + Application.isPlaying);
            report.AppendLine();

            report.AppendLine("== ロード中のScene ==");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                report
                    .Append("  [").Append(i).Append("] ")
                    .Append(string.IsNullOrEmpty(scene.name) ? "(無名)" : scene.name)
                    .Append("  path=").Append(scene.path)
                    .Append("  loaded=").Append(scene.isLoaded)
                    .AppendLine();
            }

            report.AppendLine();

            // Resources.FindObjectsOfTypeAll は、Hierarchy に出ない object も含めて
            // 「いまメモリにあるもの」をすべて返します。
            // 実行時生成物・DontDestroyOnLoad・一時シーンの中身がここへ入ります。
            HashSet<GameObject> seen = new HashSet<GameObject>();
            int scanned = 0;
            int total = 0;

            foreach (GameObject target in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!seen.Add(target))
                {
                    continue;
                }

                scanned++;

                int missing =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);

                if (missing <= 0)
                {
                    continue;
                }

                total += missing;

                Scene owner = target.scene;

                report
                    .Append("  件数 ").Append(missing)
                    .Append("  InstanceID=").Append(target.GetInstanceID())
                    .AppendLine();

                report.Append("      path      : ").AppendLine(PathOf(target.transform));

                report
                    .Append("      scene     : ")
                    .Append(owner.IsValid()
                        ? (string.IsNullOrEmpty(owner.name) ? "(無名)" : owner.name)
                        : "(シーンに属さない＝アセットまたは生成直後)")
                    .Append("  path=").Append(owner.IsValid() ? owner.path : "-")
                    .AppendLine();

                report
                    .Append("      active    : ").Append(target.activeInHierarchy)
                    .Append("  hideFlags=").Append(target.hideFlags)
                    .AppendLine();

                Component[] components = target.GetComponents<Component>();

                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        report
                            .Append("      component index ")
                            .Append(c)
                            .AppendLine(" が null（script を解決できません）");
                    }
                }
            }

            report
                .Append("走査した GameObject: ").Append(scanned)
                .Append("   Missing Script 合計: ").Append(total)
                .AppendLine();

            if (total == 0)
            {
                Debug.Log(report.ToString());
                return;
            }

            Debug.LogWarning(report.ToString());
        }

        /// <summary>開いているシーンを走査します。</summary>
        private static int ScanOpenScenes(StringBuilder report)
        {
            int found = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    found += ScanHierarchy(root, scene.path, report);
                }
            }

            return found;
        }

        /// <summary>プロジェクト内の Prefab をすべて開いて走査します。</summary>
        private static int ScanPrefabs(StringBuilder report)
        {
            int found = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    continue;
                }

                found += ScanHierarchy(prefab, path, report);
            }

            return found;
        }

        /// <summary>ScriptableObject アセットの型が失われていないか調べます。</summary>
        private static int ScanScriptableObjects(StringBuilder report)
        {
            int found = 0;

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (asset == null)
                {
                    report.AppendLine("  ScriptableObject: " + path + "（型を解決できません）");
                    found++;
                }
            }

            return found;
        }

        /// <summary>1つの階層を走査し、見つけた場所を記録します。</summary>
        private static int ScanHierarchy(GameObject root, string assetPath, StringBuilder report)
        {
            int found = 0;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                GameObject target = all[i].gameObject;

                int missing =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);

                if (missing <= 0)
                {
                    continue;
                }

                found += missing;

                report
                    .Append("  ")
                    .Append(assetPath)
                    .Append("  Hierarchy: ")
                    .Append(PathOf(all[i]))
                    .Append("  件数: ")
                    .Append(missing)
                    .AppendLine();

                // どのコンポーネント枠が欠けているかまで出します。
                Component[] components = target.GetComponents<Component>();

                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        report
                            .Append("      component index ")
                            .Append(c)
                            .AppendLine(" が null（script が解決できません）");
                    }
                }
            }

            return found;
        }

        private static string PathOf(Transform node)
        {
            string path = node.name;

            while (node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }

            return path;
        }
    }
}
