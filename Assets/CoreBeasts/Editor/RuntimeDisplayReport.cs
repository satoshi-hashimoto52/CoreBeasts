using System.Collections.Generic;
using System.Text;

using CoreBeasts.Units;

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.EditorTools
{
    /// <summary>
    /// いま画面に出ている機械獣をすべて列挙する診断。
    ///
    /// 「1体のはずの機械獣が、カード列の下へもう1体、大きく重複表示される」
    /// を実行時に突き止めるために使います。
    /// 推測で座標を動かす前に、まず何が表示されているのかをここで確定します。
    ///
    /// Play Mode 中でもそのまま実行できます。何も変更しません。
    /// </summary>
    public static class RuntimeDisplayReport
    {
        private const string Menu = "CoreBeasts/診断/画面上の機械獣を全列挙";

        [MenuItem(Menu)]
        public static void Run()
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine("[CoreBeasts] 画面上の機械獣 全列挙");
            report.AppendLine("  Play Mode: " + Application.isPlaying);
            report.AppendLine();

            AppendLoadedScenes(report);

            List<RawImage> layers = CollectVisibleRawImages();

            AppendTextureCensus(layers, report);
            AppendLayerDetails(layers, report);
            AppendGhosts(report);
            AppendCardStates(report);

            Debug.Log(report.ToString());
        }

        private static void AppendLoadedScenes(StringBuilder report)
        {
            report.AppendLine("== ロード中のScene ==");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                report
                    .Append("  [").Append(i).Append("] ")
                    .Append(string.IsNullOrEmpty(scene.name) ? "(無名)" : scene.name)
                    .Append("  path=").Append(scene.path)
                    .Append("  loaded=").Append(scene.isLoaded)
                    .Append("  roots=").Append(scene.isLoaded ? scene.rootCount : 0)
                    .AppendLine();
            }

            report.AppendLine();
        }

        /// <summary>
        /// いま実際に描かれ得る RawImage を集めます。
        /// <see cref="Resources.FindObjectsOfTypeAll{T}"/> を使うため、
        /// 実行時生成物・DontDestroyOnLoad・一時シーンの中身も漏らしません。
        /// </summary>
        private static List<RawImage> CollectVisibleRawImages()
        {
            List<RawImage> found = new List<RawImage>();

            foreach (RawImage image in Resources.FindObjectsOfTypeAll<RawImage>())
            {
                // Prefab アセットそのものは画面に出ていません。
                if (!image.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!image.gameObject.activeInHierarchy || !image.enabled)
                {
                    continue;
                }

                found.Add(image);
            }

            return found;
        }

        private static void AppendTextureCensus(
            List<RawImage> layers, StringBuilder report)
        {
            Dictionary<Texture, int> counts = new Dictionary<Texture, int>();

            for (int i = 0; i < layers.Count; i++)
            {
                Texture texture = layers[i].texture;

                if (texture == null)
                {
                    continue;
                }

                counts.TryGetValue(texture, out int n);
                counts[texture] = n + 1;
            }

            report.AppendLine("== 同じTextureを出している可視RawImageの総数 ==");

            foreach (KeyValuePair<Texture, int> entry in counts)
            {
                report
                    .Append("  ").Append(entry.Key.name)
                    .Append("  x ").Append(entry.Value)
                    .AppendLine();
            }

            report.AppendLine(
                "  ※ 機械獣1体につき3枚（Base / Primary / Secondary）です。" +
                "カード枚数×3 を超えていたら、余分な表示が出ています。");
            report.AppendLine();
        }

        private static void AppendLayerDetails(
            List<RawImage> layers, StringBuilder report)
        {
            report.AppendLine("== 可視RawImage の内訳 ==");

            for (int i = 0; i < layers.Count; i++)
            {
                RawImage layer = layers[i];
                RectTransform rect = layer.rectTransform;

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);

                Canvas canvas = layer.canvas;

                report
                    .Append("  ").Append(layer.gameObject.name)
                    .Append("  InstanceID=").Append(layer.GetInstanceID())
                    .AppendLine();

                report.Append("      path      : ").AppendLine(PathOf(rect));

                report
                    .Append("      scene     : ")
                    .Append(layer.gameObject.scene.name)
                    .Append("   activeInHierarchy=")
                    .Append(layer.gameObject.activeInHierarchy)
                    .AppendLine();

                report
                    .Append("      texture   : ")
                    .AppendLine(layer.texture != null ? layer.texture.name : "(なし)");

                report
                    .Append("      localPos  : ").Append(rect.localPosition)
                    .Append("   anchoredPos: ").Append(rect.anchoredPosition)
                    .AppendLine();

                report
                    .Append("      corners   : ")
                    .Append(corners[0]).Append(" ")
                    .Append(corners[1]).Append(" ")
                    .Append(corners[2]).Append(" ")
                    .Append(corners[3])
                    .AppendLine();

                if (canvas != null)
                {
                    report
                        .Append("      canvas    : ").Append(PathOf(canvas.transform))
                        .Append("   overrideSorting=").Append(canvas.overrideSorting)
                        .Append("   sortingOrder=").Append(canvas.sortingOrder)
                        .AppendLine();
                }
                else
                {
                    report.AppendLine("      canvas    : (なし)");
                }
            }

            report.AppendLine();
        }

        private static void AppendGhosts(StringBuilder report)
        {
            report.AppendLine("== DragGhost ==");

            int found = 0;

            foreach (DragGhostView view in Resources.FindObjectsOfTypeAll<DragGhostView>())
            {
                if (!view.gameObject.scene.IsValid())
                {
                    continue;
                }

                found++;

                report
                    .Append("  ").Append(view.gameObject.name)
                    .Append("  InstanceID=").Append(view.GetInstanceID())
                    .Append("  active=").Append(view.gameObject.activeInHierarchy)
                    .AppendLine();

                report.Append("      path: ").AppendLine(PathOf(view.transform));
            }

            if (found == 0)
            {
                report.AppendLine("  (なし)");
            }

            foreach (DragGhostPresenter presenter in
                Resources.FindObjectsOfTypeAll<DragGhostPresenter>())
            {
                if (!presenter.gameObject.scene.IsValid())
                {
                    continue;
                }

                report
                    .Append("  Presenter ").Append(PathOf(presenter.transform))
                    .Append("  IsShowing=").Append(presenter.IsShowing)
                    .Append("  VisibleGhostCount=").Append(presenter.VisibleGhostCount)
                    .AppendLine();
            }

            report.AppendLine();
        }

        private static void AppendCardStates(StringBuilder report)
        {
            report.AppendLine("== カードの操作状態 ==");

            foreach (BeastCardView card in Resources.FindObjectsOfTypeAll<BeastCardView>())
            {
                if (!card.gameObject.scene.IsValid())
                {
                    continue;
                }

                report
                    .Append("  ").Append(card.gameObject.name)
                    .Append("  state=").Append(card.CurrentState)
                    .Append("  dragReadyVisual=").Append(card.IsDragReadyVisual)
                    .AppendLine();
            }

            report.AppendLine(
                "  ※ state が SquadDragging 以外なのに DragGhost が出ていたら、" +
                "それが残留した重複表示です。");
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
