using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 詳細立ち絵の参照が実行時に解決され、画面の立ち絵を実際に動かしていることを確かめます。
    ///
    /// 正式な契約（UnitSet の stripped MonoBehaviour スタブへ m_Script を補った修正後）:
    ///   - 実行時の BeastDetailPanel.portraitView が null ではない
    ///   - portraitView は UnitSet シーンの VolxPortrait（シーン唯一の CoreBeastView）を指す
    ///   - portraitView の SpriteRenderer が、PortraitCamera に映っている唯一の SpriteRenderer
    ///   - Portrait RawImage.texture と PortraitCamera.targetTexture が同一参照
    ///   - RED から GREEN を選ぶと PropertyBlock と RenderTexture が変わる
    ///   - シーンを読み直しても同じ契約が成り立つ
    ///
    /// 実行時の InstanceID や YAML の fileID 番号は成功条件にしません。
    /// 集めた実測値（下の分類を含む）は、失敗したときだけ失敗メッセージに載ります。
    ///
    /// 診断の分類:
    ///   A. VolxPortrait GameObject 自体が生成されていない
    ///   B. VolxPortrait はあるが CoreBeastView が Missing Script
    ///   C. 両方あるが BeastDetailPanel.portraitView の参照だけ切れている
    ///
    /// 製品コード・prefab・シーンには一切触れません。
    /// </summary>
    public sealed class PortraitReferenceResolutionDiagnosticsTests
    {
        private const string SceneName = "UnitSet";
        private const int WarmUpFrames = 5;

        private static int emptySceneCounter;

        [UnityTest]
        public IEnumerator TheUnitSetSceneReportsHowThePortraitReferenceResolves()
        {
            // 空のシーンから始めます。
            Scene bootstrap = SceneManager.CreateScene(
                "PortraitRefDiag_Bootstrap_" + emptySceneCounter++);

            SceneManager.SetActiveScene(bootstrap);

            yield return null;

            // 保存状態や共有 Repository には触れません。
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            yield return EndOfFrame();
            yield return null;

            StringBuilder report = new StringBuilder();

            report.AppendLine("=== 詳細立ち絵 参照解決 診断 ===");

            // ---------- 1. アクティブシーン ----------
            Scene active = SceneManager.GetActiveScene();

            report.AppendLine();
            report.AppendLine("[1] ActiveScene");
            report.AppendLine("  name      : " + active.name);
            report.AppendLine("  path      : " + active.path);
            report.AppendLine("  isLoaded  : " + active.isLoaded);
            report.AppendLine("  rootCount : " + active.rootCount);
            report.AppendLine("  sceneCount(全体): " + SceneManager.sceneCount);

            GameObject[] roots = active.isLoaded
                ? active.GetRootGameObjects()
                : new GameObject[0];

            // ---------- 2. ルート一覧 ----------
            report.AppendLine();
            report.AppendLine("[2] ルート GameObject 一覧 (" + roots.Length + " 件)");

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];

                report.AppendLine("  - name              : " + root.name);
                report.AppendLine("    InstanceID        : " + root.GetInstanceID());
                report.AppendLine("    activeSelf        : " + root.activeSelf);
                report.AppendLine("    activeInHierarchy : " + root.activeInHierarchy);
                report.AppendLine("    path              : " + PathOf(root.transform));
                report.AppendLine("    scene             : " + root.scene.name);
            }

            // 全 GameObject を非アクティブ含めて集めます。
            List<GameObject> all = new List<GameObject>();

            for (int i = 0; i < roots.Length; i++)
            {
                Collect(roots[i].transform, all);
            }

            // ---------- 3. 全 GameObject の Component 走査 ----------
            report.AppendLine();
            report.AppendLine("[3] 全 GameObject の Component (" + all.Count + " 件)");

            int nullComponentTotal = 0;
            List<string> nullComponentOwners = new List<string>();

            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                Component[] components = go.GetComponents<Component>();

                List<int> nullIndices = new List<int>();
                List<string> typeNames = new List<string>();

                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        nullIndices.Add(c);
                        nullComponentTotal++;
                    }
                    else
                    {
                        typeNames.Add(components[c].GetType().FullName);
                    }
                }

                bool interesting = nullIndices.Count > 0;

                if (interesting)
                {
                    nullComponentOwners.Add(PathOf(go.transform));
                }

                report.AppendLine("  " + PathOf(go.transform));
                report.AppendLine("    InstanceID : " + go.GetInstanceID()
                    + "  components=" + components.Length
                    + "  null=" + (nullIndices.Count == 0 ? "なし" : Join(nullIndices)));
                report.AppendLine("    types      : " + string.Join(", ", typeNames));
            }

            report.AppendLine();
            report.AppendLine("  null Component 合計 : " + nullComponentTotal);

            // ---------- 4. 名前による抽出 ----------
            report.AppendLine();
            report.AppendLine("[4] 名前による抽出");

            List<GameObject> volxExact = ByExactName(all, "VolxPortrait");
            List<GameObject> detailExact = ByExactName(all, "DetailPanel");

            report.AppendLine("  完全一致 'VolxPortrait' : " + volxExact.Count + " 件");
            AppendObjects(report, volxExact);

            report.AppendLine("  完全一致 'DetailPanel'  : " + detailExact.Count + " 件");
            AppendObjects(report, detailExact);

            List<GameObject> containsPortrait = ByNameContains(all, "Portrait");
            List<GameObject> containsDetail = ByNameContains(all, "Detail");

            report.AppendLine("  名前に 'Portrait' を含む : " + containsPortrait.Count + " 件");
            AppendObjects(report, containsPortrait);

            report.AppendLine("  名前に 'Detail' を含む   : " + containsDetail.Count + " 件");
            AppendObjects(report, containsDetail);

            // ---------- 5. CoreBeastView の2経路検索 ----------
            report.AppendLine();
            report.AppendLine("[5] CoreBeastView");

            List<CoreBeastView> viewsInScene = FindInScene<CoreBeastView>(all);

            report.AppendLine("  (a) シーン走査 : " + viewsInScene.Count + " 件");
            AppendComponents(report, viewsInScene.ToArray());

            CoreBeastView[] viewsAll = Resources.FindObjectsOfTypeAll<CoreBeastView>();

            report.AppendLine("  (b) Resources.FindObjectsOfTypeAll : " + viewsAll.Length + " 件");
            AppendComponents(report, viewsAll);

            // ---------- 6. BeastDetailPanel の2経路検索 ----------
            report.AppendLine();
            report.AppendLine("[6] BeastDetailPanel");

            List<BeastDetailPanel> panelsInScene = FindInScene<BeastDetailPanel>(all);

            report.AppendLine("  (a) シーン走査 : " + panelsInScene.Count + " 件");
            AppendComponents(report, panelsInScene.ToArray());

            BeastDetailPanel[] panelsAll =
                Resources.FindObjectsOfTypeAll<BeastDetailPanel>();

            report.AppendLine("  (b) Resources.FindObjectsOfTypeAll : " + panelsAll.Length + " 件");
            AppendComponents(report, panelsAll);

            // ---------- 7. portraitView の実値 ----------
            report.AppendLine();
            report.AppendLine("[7] BeastDetailPanel.portraitView（reflection）");

            FieldInfo field = typeof(BeastDetailPanel).GetField(
                "portraitView", BindingFlags.NonPublic | BindingFlags.Instance);

            report.AppendLine("  FieldInfo : " + (field == null ? "見つかりません" : "取得成功"));

            int panelsWithReference = 0;
            int panelsChecked = 0;

            for (int i = 0; i < panelsAll.Length; i++)
            {
                BeastDetailPanel panel = panelsAll[i];

                panelsChecked++;

                report.AppendLine("  - panel path : " + PathOf(panel.transform));
                report.AppendLine("    InstanceID : " + panel.GetInstanceID()
                    + "  scene=" + panel.gameObject.scene.name
                    + "  hideFlags=" + panel.gameObject.hideFlags);

                if (field == null)
                {
                    continue;
                }

                object raw = field.GetValue(panel);

                // Unity の擬似 null と純粋な null を区別します。
                bool csharpNull = raw == null;
                CoreBeastView asView = raw as CoreBeastView;
                bool unityNull = asView == null;

                report.AppendLine("    portraitView (C# null) : " + csharpNull);
                report.AppendLine("    portraitView (Unity null): " + unityNull);

                if (!unityNull)
                {
                    panelsWithReference++;

                    report.AppendLine("    -> name       : " + asView.name);
                    report.AppendLine("    -> path       : " + PathOf(asView.transform));
                    report.AppendLine("    -> InstanceID : " + asView.GetInstanceID());
                    report.AppendLine("    -> scene      : " + asView.gameObject.scene.name);
                }
            }

            // ---------- 8. VolxPortrait の詳細 ----------
            report.AppendLine();
            report.AppendLine("[8] VolxPortrait の詳細");

            bool volxHasNullComponent = false;
            int volxCoreBeastViews = 0;

            if (volxExact.Count == 0)
            {
                report.AppendLine("  シーン走査では見つかりません。");
            }

            for (int i = 0; i < volxExact.Count; i++)
            {
                GameObject volx = volxExact[i];

                report.AppendLine("  - path : " + PathOf(volx.transform));
                report.AppendLine("    InstanceID : " + volx.GetInstanceID());

                Component[] components = volx.GetComponents<Component>();

                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        volxHasNullComponent = true;

                        report.AppendLine("    [" + c + "] null (Missing Script)");
                    }
                    else
                    {
                        report.AppendLine("    [" + c + "] " + components[c].GetType().FullName);
                    }
                }

                CoreBeastView direct = volx.GetComponent<CoreBeastView>();

                report.AppendLine("    GetComponent<CoreBeastView>() : "
                    + (direct == null ? "null" : "取得 InstanceID=" + direct.GetInstanceID()));

                CoreBeastView[] inChildren =
                    volx.GetComponentsInChildren<CoreBeastView>(true);

                volxCoreBeastViews += inChildren.Length;

                report.AppendLine("    子階層の CoreBeastView : " + inChildren.Length + " 件");
                AppendComponents(report, inChildren);
            }

            // ---------- 9. Resources 経由の VolxPortrait ----------
            report.AppendLine();
            report.AppendLine("[9] Resources.FindObjectsOfTypeAll<GameObject>() での VolxPortrait");

            GameObject[] everything = Resources.FindObjectsOfTypeAll<GameObject>();

            int volxViaResources = 0;

            for (int i = 0; i < everything.Length; i++)
            {
                GameObject go = everything[i];

                if (go.name != "VolxPortrait")
                {
                    continue;
                }

                volxViaResources++;

                report.AppendLine("  - InstanceID : " + go.GetInstanceID());
                report.AppendLine("    path       : " + PathOf(go.transform));
                report.AppendLine("    hideFlags  : " + go.hideFlags);
                report.AppendLine("    scene      : '" + go.scene.name
                    + "'  isValid=" + go.scene.IsValid()
                    + "  activeInHierarchy=" + go.activeInHierarchy);
            }

            report.AppendLine("  Resources 走査での件数 : " + volxViaResources
                + " / シーン走査での件数 : " + volxExact.Count);
            report.AppendLine("  （走査対象の GameObject 総数 : " + everything.Length + "）");

            // ---------- 10. 自動分類 ----------
            string classification;

            if (volxExact.Count == 0 && volxViaResources == 0)
            {
                classification = "A_GAMEOBJECT_MISSING";
            }
            else if (volxExact.Count > 1 || panelsAll.Length > 1
                || viewsAll.Length > 1 || volxViaResources > 1)
            {
                classification = "D_MULTIPLE_OR_UNEXPECTED";
            }
            else if (volxHasNullComponent || volxCoreBeastViews == 0
                || viewsInScene.Count == 0)
            {
                classification = "B_COMPONENT_MISSING";
            }
            else if (panelsWithReference == 0 && panelsChecked > 0)
            {
                classification = "C_SERIALIZED_REFERENCE_BROKEN";
            }
            else if (panelsWithReference > 0)
            {
                classification = "REFERENCE_RESOLVED";
            }
            else
            {
                classification = "D_MULTIPLE_OR_UNEXPECTED";
            }

            report.AppendLine();
            report.AppendLine("[10] 集計");
            report.AppendLine("  VolxPortrait (シーン走査)   : " + volxExact.Count);
            report.AppendLine("  VolxPortrait (Resources)    : " + volxViaResources);
            report.AppendLine("  VolxPortrait の null Component : " + volxHasNullComponent);
            report.AppendLine("  CoreBeastView (シーン走査)  : " + viewsInScene.Count);
            report.AppendLine("  CoreBeastView (Resources)   : " + viewsAll.Length);
            report.AppendLine("  BeastDetailPanel (Resources): " + panelsAll.Length);
            report.AppendLine("  参照が生きている panel      : " + panelsWithReference
                + " / " + panelsChecked);
            report.AppendLine("  null Component 合計         : " + nullComponentTotal);

            if (nullComponentOwners.Count > 0)
            {
                report.AppendLine("  null Component を持つ GameObject:");

                for (int i = 0; i < nullComponentOwners.Count; i++)
                {
                    report.AppendLine("    " + nullComponentOwners[i]);
                }
            }

            report.AppendLine();
            report.AppendLine("CLASSIFICATION=" + classification);

            // ---------- 11. 正式契約 ----------
            // 実測値は、失敗したときに原因が分かるよう失敗メッセージへ添えます。
            string diagnostics = "\n" + report;

            Assert.That(classification, Is.EqualTo("REFERENCE_RESOLVED"), diagnostics);

            yield return VerifyContract("初回読込", diagnostics);

            // ---------- 12. 読み直しても解決する ----------
            Scene away = SceneManager.CreateScene("PortraitRefDiag_Away_" + emptySceneCounter++);
            Scene unitSet = SceneManager.GetSceneByName(SceneName);

            SceneManager.SetActiveScene(away);

            yield return SceneManager.UnloadSceneAsync(unitSet);
            yield return null;

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            yield return EndOfFrame();
            yield return null;

            yield return VerifyContract("再読込後", diagnostics);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return Cleanup();
        }

        // ---------------- 正式契約の検査 ----------------

        private static IEnumerator VerifyContract(string stage, string diagnostics)
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);

            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, stage + ": " + SceneName + " が読み込まれていません。");

            List<GameObject> all = new List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Collect(root.transform, all);
            }

            List<BeastDetailPanel> panels = FindInScene<BeastDetailPanel>(all);
            List<CoreBeastView> views = FindInScene<CoreBeastView>(all);
            List<PortraitRenderTarget> targets = FindInScene<PortraitRenderTarget>(all);

            Assert.That(panels.Count, Is.EqualTo(1), stage + ": BeastDetailPanel は1個です。" + diagnostics);
            Assert.That(views.Count, Is.EqualTo(1), stage + ": CoreBeastView は1個です。" + diagnostics);
            Assert.That(targets.Count, Is.EqualTo(1), stage + ": PortraitRenderTarget は1個です。" + diagnostics);

            CoreBeastView bound = (CoreBeastView)GetField(panels[0], "portraitView");

            Assert.That(bound != null, Is.True, stage + ": BeastDetailPanel.portraitView が実行時に null です。" + diagnostics);
            Assert.That(bound, Is.SameAs(views[0]), stage + ": portraitView がシーンの CoreBeastView を指していません。" + diagnostics);
            Assert.That(bound.gameObject.name, Is.EqualTo("VolxPortrait"), stage + diagnostics);
            Assert.That(bound.gameObject.scene, Is.EqualTo(scene), stage + ": portraitView が UnitSet 以外のシーンにあります。" + diagnostics);

            SpriteRenderer renderer = (SpriteRenderer)GetField(bound, "spriteRenderer");
            Camera camera = (Camera)GetField(targets[0], "portraitCamera");
            RawImage image = (RawImage)GetField(targets[0], "targetImage");

            Assert.That(renderer != null, Is.True, stage + ": CoreBeastView.spriteRenderer が null です。" + diagnostics);
            Assert.That(camera != null && image != null, Is.True, stage + ": PortraitCamera / RawImage が未設定です。" + diagnostics);

            List<Renderer> seen = RenderersSeenBy(camera, all);

            Assert.That(seen, Does.Contain(renderer), stage + ": PortraitCamera に portraitView の SpriteRenderer が映っていません。" + diagnostics);

            foreach (Renderer other in seen)
            {
                Assert.That(
                    other == renderer || !(other is SpriteRenderer),
                    Is.True,
                    stage + ": 別の SpriteRenderer が PortraitCamera に映っています: " + PathOf(other.transform) + diagnostics);
            }

            Assert.That(camera.targetTexture != null, Is.True, stage + ": PortraitCamera.targetTexture が null です。" + diagnostics);
            Assert.That(image.texture, Is.SameAs(camera.targetTexture), stage + ": RawImage が別の Texture を表示しています。" + diagnostics);

            // RED → GREEN で、PropertyBlock と RenderTexture の両方が変わること。
            List<BeastCardView> cards = new List<BeastCardView>();

            foreach (GameObject go in all)
            {
                if (go.name == "Content")
                {
                    cards.AddRange(go.GetComponentsInChildren<BeastCardView>(true));
                }
            }

            yield return Tap(CardWith(cards, UnitAttribute.Red));

            Color redColour = PrimaryColourOf(renderer);
            uint redHash = Hash(camera.targetTexture);

            yield return Tap(CardWith(cards, UnitAttribute.Green));

            Color greenColour = PrimaryColourOf(renderer);
            uint greenHash = Hash(camera.targetTexture);

            Assert.That(bound.PrimaryAttribute, Is.EqualTo(UnitAttribute.Green), stage + diagnostics);
            Assert.That(greenColour, Is.Not.EqualTo(redColour),
                stage + ": GREEN を選んでも _Primary_Color が変わりません (" + redColour + ")。" + diagnostics);
            Assert.That(greenHash, Is.Not.EqualTo(redHash),
                stage + ": GREEN を選んでも RenderTexture が変わりません (" + redHash.ToString("X8") + ")。" + diagnostics);
        }

        /// <summary>WaitForEndOfFrame まで待ちます。batchmode では使えないため、2フレーム進めて代えます。</summary>
        private static IEnumerator EndOfFrame()
        {
            if (Application.isBatchMode)
            {
                yield return null;
                yield return null;
                yield break;
            }

            yield return new WaitForEndOfFrame();
        }

        /// <summary>実際の選択経路（短いタップ）でカードを選びます。</summary>
        private static IEnumerator Tap(BeastCardView card)
        {
            RectTransform rect = (RectTransform)card.transform;
            Canvas root = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = 0,
                position = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center)),
            };

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);

            yield return null;

            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            yield return EndOfFrame();
            yield return null;
        }

        private static BeastCardView CardWith(List<BeastCardView> cards, UnitAttribute attribute)
        {
            foreach (BeastCardView card in cards)
            {
                OwnedCoreBeast beast = card.Beast;

                if (beast != null && beast.IsValid &&
                    beast.Definition.PrimaryAttribute == attribute &&
                    !beast.Definition.HasSecondaryAttribute)
                {
                    return card;
                }
            }

            Assert.Fail("一覧に " + attribute + " 単色の個体がありません。");
            return null;
        }

        private static Color PrimaryColourOf(SpriteRenderer renderer)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            return block.GetColor("_Primary_Color");
        }

        /// <summary>RenderTexture の内容の FNV-1a ハッシュ。</summary>
        private static uint Hash(RenderTexture target)
        {
            Texture2D readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                readback.Apply(false);

                uint hash = 2166136261u;

                foreach (Color32 p in readback.GetPixels32())
                {
                    hash = (hash ^ p.r) * 16777619u;
                    hash = (hash ^ p.g) * 16777619u;
                    hash = (hash ^ p.b) * 16777619u;
                    hash = (hash ^ p.a) * 16777619u;
                }

                return hash;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        /// <summary>カメラの視野（正射影の箱）とカリングマスクに入っている、有効な Renderer。</summary>
        private static List<Renderer> RenderersSeenBy(Camera camera, List<GameObject> all)
        {
            List<Renderer> seen = new List<Renderer>();

            float halfHeight = camera.orthographicSize;
            Vector3 origin = camera.transform.position;

            Bounds view = new Bounds(
                new Vector3(origin.x, origin.y, origin.z + (camera.nearClipPlane + camera.farClipPlane) * 0.5f),
                new Vector3(halfHeight * camera.aspect * 2f, halfHeight * 2f, camera.farClipPlane - camera.nearClipPlane));

            foreach (Renderer renderer in FindInScene<Renderer>(all))
            {
                if (renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
                    renderer.bounds.Intersects(view))
                {
                    seen.Add(renderer);
                }
            }

            return seen;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        // ---------------- 後始末 ----------------

        private static IEnumerator Cleanup()
        {
            Scene loaded = SceneManager.GetSceneByName(SceneName);

            Scene empty = SceneManager.CreateScene(
                "PortraitRefDiag_TearDown_" + emptySceneCounter++);

            SceneManager.SetActiveScene(empty);

            if (loaded.IsValid() && loaded.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loaded);
            }

            yield return null;
        }

        // ---------------- 道具 ----------------

        private static void Collect(Transform node, List<GameObject> into)
        {
            into.Add(node.gameObject);

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), into);
            }
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

        private static string Join(List<int> values)
        {
            string text = string.Empty;

            for (int i = 0; i < values.Count; i++)
            {
                text += (i == 0 ? string.Empty : ",") + values[i];
            }

            return text;
        }

        private static List<GameObject> ByExactName(List<GameObject> all, string name)
        {
            List<GameObject> found = new List<GameObject>();

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].name == name)
                {
                    found.Add(all[i]);
                }
            }

            return found;
        }

        private static List<GameObject> ByNameContains(List<GameObject> all, string part)
        {
            List<GameObject> found = new List<GameObject>();

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].name.Contains(part))
                {
                    found.Add(all[i]);
                }
            }

            return found;
        }

        private static List<T> FindInScene<T>(List<GameObject> all) where T : Component
        {
            List<T> found = new List<T>();

            for (int i = 0; i < all.Count; i++)
            {
                T component = all[i].GetComponent<T>();

                if (component != null)
                {
                    found.Add(component);
                }
            }

            return found;
        }

        private static void AppendObjects(StringBuilder report, List<GameObject> objects)
        {
            if (objects.Count == 0)
            {
                report.AppendLine("      (なし)");
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject go = objects[i];

                report.AppendLine("      " + PathOf(go.transform)
                    + "  InstanceID=" + go.GetInstanceID()
                    + "  activeSelf=" + go.activeSelf
                    + "  activeInHierarchy=" + go.activeInHierarchy
                    + "  scene=" + go.scene.name);
            }
        }

        private static void AppendComponents<T>(StringBuilder report, T[] components)
            where T : Component
        {
            if (components.Length == 0)
            {
                report.AppendLine("      (なし)");
                return;
            }

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];

                report.AppendLine("      " + component.gameObject.name
                    + "  path=" + PathOf(component.transform)
                    + "  InstanceID=" + component.GetInstanceID()
                    + "  activeInHierarchy=" + component.gameObject.activeInHierarchy
                    + "  scene='" + component.gameObject.scene.name + "'"
                    + "  hideFlags=" + component.gameObject.hideFlags);
            }
        }
    }
}
