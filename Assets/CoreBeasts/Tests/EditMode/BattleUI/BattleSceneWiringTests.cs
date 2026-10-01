using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Battle.unity の配線を、シーンごと開いて検証します。
    /// Prefab や Scene を作り直して fileID が動いた場合、ここで参照切れを検出します。
    /// </summary>
    public sealed class BattleSceneWiringTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";
        private const string WheelItemPrefabPath =
            "Assets/CoreBeasts/Prefabs/BattleWheelItem.prefab";

        private const string HistorySlotPrefabPath =
            "Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab";

        private const string TraySlotPrefabPath =
            "Assets/CoreBeasts/Prefabs/BattleTraySlot.prefab";
        private const string EnemyMarkerPrefabPath =
            "Assets/CoreBeasts/Prefabs/BattleEnemyMarker.prefab";
        private const string BattleTextPath =
            "Assets/CoreBeasts/Data/BattleTextCatalog_EN.asset";

        private static readonly string[] OtherScenePaths =
        {
            "Assets/CoreBeasts/Scenes/Boot.unity",
            "Assets/CoreBeasts/Scenes/Home.unity",
            "Assets/CoreBeasts/Scenes/UnitSet.unity",
        };

        private Scene scene;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private SceneSetup[] savedSetup;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // テスト開始前のシーン構成を控え、終了後に必ず元へ戻します。
            savedSetup = EditorSceneManager.GetSceneManagerSetup();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            RestoreSceneSetup();
        }

        [SetUp]
        public void SetUp()
        {
            // Additive だと複数シーンの Global Light 2D が同居し、
            // URP 2D が「More than one global light」警告を出します。
            // 検査は常に 1 シーン単独で行います。
            scene = OpenSceneAlone(ScenePath);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();

            // Single で開いているため、ここでは閉じません。
            // （唯一のシーンは Close できない）
            // 次の SetUp がディスクから開き直し、最後は
            // OneTimeTearDown が元のシーン構成へ復元します。保存は一切行いません。
            scene = default(Scene);
        }

        /// <summary>
        /// 指定シーンだけが開いている状態にします。保存は行いません。
        /// </summary>
        private static Scene OpenSceneAlone(string scenePath)
        {
            Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Assume.That(opened.IsValid(), Is.True, scenePath + " を開けません。");

            return opened;
        }

        /// <summary>
        /// テスト開始前のシーン構成へ戻します。
        /// 未保存（Untitled）シーンは復元できないため除外します。
        /// </summary>
        private void RestoreSceneSetup()
        {
            List<SceneSetup> restorable = new List<SceneSetup>();

            if (savedSetup != null)
            {
                for (int i = 0; i < savedSetup.Length; i++)
                {
                    SceneSetup entry = savedSetup[i];

                    if (entry != null && !string.IsNullOrEmpty(entry.path))
                    {
                        restorable.Add(entry);
                    }
                }
            }

            if (restorable.Count == 0)
            {
                // 元が未保存シーンだけなら、空シーン 1 枚へ戻します。
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }

            bool hasActive = false;

            for (int i = 0; i < restorable.Count; i++)
            {
                if (restorable[i].isActive)
                {
                    hasActive = true;
                    break;
                }
            }

            if (!hasActive)
            {
                // 除外したシーンがアクティブだった場合の保険です。
                restorable[0].isActive = true;
            }

            EditorSceneManager.RestoreSceneManagerSetup(restorable.ToArray());
        }

        /// <summary>シーンから名前で RectTransform を1つ取り出します。</summary>
        private RectTransform Rect(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return (RectTransform)found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private T FindOne<T>() where T : Component
        {
            List<T> found = FindAll<T>();

            Assert.That(
                found.Count,
                Is.EqualTo(1),
                typeof(T).Name + " はシーンに1個だけ存在する必要があります。");

            return found[0];
        }

        private List<T> FindAll<T>() where T : Component
        {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private static void AssertAssigned(object target, params string[] names)
        {
            foreach (string name in names)
            {
                object value = GetField(target, name);
                Object unityObject = value as Object;
                bool missing = unityObject != null ? unityObject == null : value == null;

                Assert.That(
                    missing,
                    Is.False,
                    $"{target.GetType().Name}.{name} が未設定です。");
            }
        }

        // ---------------- 進行役 ----------------

        [Test]
        public void ScreenController_ExistsOnceAndIsFullyWired()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            AssertAssigned(controller,
                "roster", "palette", "text", "battleText",
                "playerCombatant", "cpuCombatant", "enemyStatus",
                "playerWheel", "historyLane", "deployTransition", "slideUpGuide",
                "scoreView", "resultView", "fxPlayer", "settingsView",
                "battleRoot", "squadRequiredRoot",
                "rematchButton",
                "screenTitleLabel", "rematchLabel",
                "squadRequiredTitleLabel", "squadRequiredHintLabel");

            Assert.That((string)GetField(controller, "setId"), Is.Not.Empty);
        }

        [Test]
        public void ScreenController_PointsAtTheSameRosterAsUnitSet()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            CoreBeastRoster roster = (CoreBeastRoster)GetField(controller, "roster");

            Assert.That(roster, Is.Not.Null);
            Assert.That(
                roster.Owned.Count,
                Is.GreaterThanOrEqualTo(BattleSquad.UnitCount),
                "CPU編成を7体そろえるには、所持一覧が7体以上必要です。");
        }

        [Test]
        public void ScreenController_UsesTheBattleTextAsset()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            BattleTextCatalog asset =
                AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(BattleTextPath);

            Assert.That(asset, Is.Not.Null, BattleTextPath + " が読めません。");
            Assert.That(GetField(controller, "battleText"), Is.SameAs(asset));

            Assert.That(asset.Deploy, Is.EqualTo("DEPLOY"));
            Assert.That(asset.SquadRequired, Is.EqualTo("SQUAD REQUIRED"));
            Assert.That(asset.SquadRequiredHint, Is.EqualTo("SET 7 CORE BEASTS"));
            Assert.That(asset.Rematch, Is.EqualTo("REMATCH"));
            Assert.That(asset.FxOn, Is.EqualTo("FX ON"));
            Assert.That(asset.FxOff, Is.EqualTo("FX OFF"));
            Assert.That(asset.FormatScore(2, 1), Is.EqualTo("PLAYER 2  -  1 CPU"));
        }

        [Test]
        public void TheRoundLineSaysFourWinsClinchesRatherThanFirstToFour()
        {
            BattleTextCatalog asset =
                AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(BattleTextPath);

            Assert.That(asset, Is.Not.Null, BattleTextPath + " が読めません。");

            string line = asset.FormatRound(3, 7);

            Assert.That(
                line,
                Is.EqualTo("ROUND 3 / 7   4 WINS TO CLINCH"),
                "ラウンド行の文言が合意と違います。");

            // 「FIRST TO 4」だと完全な4勝先取に読めます。
            // 実際は4勝で即決着、届かなければ7ラウンド終了時の勝数比較です。
            Assert.That(
                line,
                Does.Not.Contain("FIRST TO"),
                "4勝必須と誤解される表現を使ってはいけません。");

            Assert.That(line, Does.StartWith("ROUND 3 / 7"));
            Assert.That(line, Does.Contain("4 WINS TO CLINCH"));

            // 最大ラウンド数は書式の引数から出ます。固定値を埋め込みません。
            Assert.That(asset.FormatRound(1, 7), Does.StartWith("ROUND 1 / 7"));
            Assert.That(asset.FormatRound(7, 7), Does.StartWith("ROUND 7 / 7"));
        }

        /// <summary>
        /// iPhone 12 mini（1080x2340）の Canvas 幅。
        /// CanvasScaler は参照 1080x1920・match 0.5 なので、
        /// 幅 = sqrt(1080 * 1920 * 画面のアスペクト比) になります。
        /// </summary>
        private static float CanvasWidthOf(float screenWidth, float screenHeight)
        {
            return Mathf.Sqrt(1080f * 1920f * (screenWidth / screenHeight));
        }

        // 1080x2340 = iPhone 12 mini（いちばん細い）、1080x2400 = 20:9、
        // 1080x1920 = 参照解像度。
        [TestCase(1080f, 2340f)]
        [TestCase(1080f, 2400f)]
        [TestCase(1080f, 1920f)]
        public void TheRoundLineFitsOnOneLine(float screenWidth, float screenHeight)
        {
            BattleTextCatalog asset =
                AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(BattleTextPath);

            TMP_Text label = Rect("RoundLabel").GetComponent<TMP_Text>();

            Assert.That(label, Is.Not.Null, "RoundLabel が TMP_Text ではありません。");

            // 折り返しも自動縮小もしない前提の行です。
            Assert.That(
                label.enableAutoSizing,
                Is.False,
                "ラウンド行を autosize 任せにしません。");

            float labelWidth =
                CanvasWidthOf(screenWidth, screenHeight) + label.rectTransform.sizeDelta.x;

            Assume.That(labelWidth, Is.GreaterThan(0f));

            // いちばん長くなるのは「ROUND 7 / 7」の行です。
            string longest = asset.FormatRound(7, 7);
            string original = label.text;

            float needed;

            try
            {
                label.text = longest;
                label.ForceMeshUpdate();

                needed = label.GetPreferredValues(longest, Mathf.Infinity, 0f).x;
            }
            finally
            {
                label.text = original;
            }

            Assert.That(
                needed,
                Is.LessThanOrEqualTo(labelWidth),
                screenWidth + "x" + screenHeight + "（Canvas幅 " +
                CanvasWidthOf(screenWidth, screenHeight).ToString("F0") +
                "）で ラウンド行が入りません。必要 " + needed.ToString("F0") +
                " / 使える幅 " + labelWidth.ToString("F0"));

            // 上は端末幅での計算です。合わせて、いま開いている Canvas 幅でも
            // 実際に1行のままであることを TMP の組版結果から見ます。
            label.text = longest;
            label.ForceMeshUpdate();

            int lines = label.textInfo.lineCount;

            label.text = original;
            label.ForceMeshUpdate();

            Assert.That(lines, Is.EqualTo(1), "ラウンド行が折り返しています。");
        }

        [Test]
        public void TheRoundLineNeverReachesTheSettingsButton()
        {
            BattleTextCatalog asset =
                AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(BattleTextPath);

            TMP_Text label = Rect("RoundLabel").GetComponent<TMP_Text>();

            string original = label.text;

            label.text = asset.FormatRound(7, 7);
            label.ForceMeshUpdate();

            Canvas.ForceUpdateCanvases();

            Vector3[] text = new Vector3[4];
            label.rectTransform.GetWorldCorners(text);

            Vector3[] gear = new Vector3[4];
            Rect("SettingsButton").GetWorldCorners(gear);

            // ラウンド行は中央そろえです。歯車へ届かないことを見ます。
            float half = label.GetPreferredValues(
                label.text, Mathf.Infinity, 0f).x * 0.5f;

            float centre = (text[0].x + text[2].x) * 0.5f;
            float scale = label.rectTransform.lossyScale.x;

            label.text = original;
            label.ForceMeshUpdate();

            Assert.That(
                centre + half * scale,
                Is.LessThanOrEqualTo(gear[0].x + 0.5f),
                "ラウンド行が設定ボタンへ重なります。");
        }

        [Test]
        public void ScreenController_StartsOnTheBattleRootNotTheSquadPrompt()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            GameObject battleRoot = (GameObject)GetField(controller, "battleRoot");
            GameObject squadRequired =
                (GameObject)GetField(controller, "squadRequiredRoot");

            Assert.That(battleRoot.activeSelf, Is.True);
            Assert.That(
                squadRequired.activeSelf,
                Is.False,
                "編成不足の案内は、必要になるまで出しません。");
        }

        // ---------------- 各View ----------------

        [Test]
        public void Combatants_ExistForBothSidesAndAreFullyWired()
        {
            List<BattleCombatantView> combatants = FindAll<BattleCombatantView>();

            Assert.That(combatants.Count, Is.EqualTo(2));

            for (int i = 0; i < combatants.Count; i++)
            {
                Assert.That(
                    combatants[i].HasRequiredReferences(),
                    Is.True,
                    combatants[i].name + " の参照が不足しています。");
            }
        }

        [Test]
        public void Combatants_AreMirroredWithoutMirroringText()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            BattleCombatantView cpu =
                (BattleCombatantView)GetField(controller, "cpuCombatant");
            BattleCombatantView player =
                (BattleCombatantView)GetField(controller, "playerCombatant");

            RectTransform cpuPortrait = cpu.PortraitTransform;
            RectTransform playerPortrait = player.PortraitTransform;

            Assert.That(
                cpuPortrait.localScale.x * playerPortrait.localScale.x,
                Is.LessThan(0f),
                "CPUとプレイヤーの立ち絵は、向きが逆になります。");

            // 立ち絵は左右が逆の位置に置きます。
            Assert.That(cpuPortrait.anchorMin.x, Is.EqualTo(0f));
            Assert.That(playerPortrait.anchorMax.x, Is.EqualTo(1f));

            // テキストは反転させません。
            RectTransform cpuInfo =
                ((GameObject)GetField(cpu, "infoRoot")).GetComponent<RectTransform>();
            RectTransform playerInfo =
                ((GameObject)GetField(player, "infoRoot")).GetComponent<RectTransform>();

            Assert.That(cpuInfo.lossyScale.x, Is.GreaterThan(0f));
            Assert.That(playerInfo.lossyScale.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Views_AreFullyWired()
        {
            Assert.That(FindOne<BattleScoreView>().HasRequiredReferences(), Is.True);
            Assert.That(FindOne<BattleResultView>().HasRequiredReferences(), Is.True);
            Assert.That(FindOne<BattleFxPlayer>().HasRequiredReferences(), Is.True);

            AssertAssigned(FindOne<BattleUnitWheelView>(), "content", "itemPrefab");
            AssertAssigned(
                FindOne<BattleHistoryLaneView>(), "content", "slotPrefab", "canvasGroup");
            Assert.That(FindOne<BattleSlideUpGuideView>().HasRequiredReferences(), Is.True);
            AssertAssigned(
                FindOne<BattleDeployTransitionView>(), "ghostRoot", "ghostPrefab");
            AssertAssigned(
                FindOne<EnemySquadStatusView>(),
                "content", "markerPrefab", "remainingLabel");
        }

        [Test]
        public void Fx_TargetsTheTwoPortraits()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();
            BattleScreenController controller = FindOne<BattleScreenController>();

            BattleCombatantView cpu =
                (BattleCombatantView)GetField(controller, "cpuCombatant");
            BattleCombatantView player =
                (BattleCombatantView)GetField(controller, "playerCombatant");

            Assert.That(
                GetField(fx, "playerPortrait"),
                Is.SameAs(player.PortraitTransform));

            Assert.That(
                GetField(fx, "cpuPortrait"),
                Is.SameAs(cpu.PortraitTransform));
        }

        // ---------------- プレハブ ----------------

        [Test]
        public void Prefabs_AreValidAndReferencedByTheScene()
        {
            BattleUnitWheelItemView slotAsset =
                AssetDatabase.LoadAssetAtPath<BattleUnitWheelItemView>(WheelItemPrefabPath);
            EnemyMarkerView markerAsset =
                AssetDatabase.LoadAssetAtPath<EnemyMarkerView>(EnemyMarkerPrefabPath);

            Assert.That(slotAsset, Is.Not.Null, WheelItemPrefabPath + " が読めません。");
            Assert.That(markerAsset, Is.Not.Null, EnemyMarkerPrefabPath + " が読めません。");

            Assert.That(slotAsset.HasRequiredReferences(), Is.True);
            Assert.That(markerAsset.HasRequiredReferences(), Is.True);

            Assert.That(
                GetField(FindOne<BattleUnitWheelView>(), "itemPrefab"),
                Is.SameAs(slotAsset),
                "itemPrefab が BattleWheelItem.prefab を指していません（fileIDのずれ）。");

            BattleHistorySlotView historyAsset =
                AssetDatabase.LoadAssetAtPath<BattleHistorySlotView>(
                    HistorySlotPrefabPath);

            Assert.That(historyAsset, Is.Not.Null, HistorySlotPrefabPath + " が読めません。");
            Assert.That(historyAsset.HasRequiredReferences(), Is.True);

            Assert.That(
                GetField(FindOne<BattleHistoryLaneView>(), "slotPrefab"),
                Is.SameAs(historyAsset),
                "slotPrefab が BattleHistorySlot.prefab を指していません（fileIDのずれ）。");

            Assert.That(
                GetField(FindOne<EnemySquadStatusView>(), "markerPrefab"),
                Is.SameAs(markerAsset),
                "markerPrefab が BattleEnemyMarker.prefab を指していません（fileIDのずれ）。");
        }

        /// <summary>
        /// Prefab参照が「正しい型のコンポーネント」を指しているかを確かめます。
        ///
        /// GUIDが合っていても、fileIDがPrefabルートのRectTransformを指していると
        /// Inspectorでは空欄になり、Instantiateが null で止まります。
        /// GUIDの存在確認だけでは見抜けないため、実体の型まで確かめます。
        /// </summary>
        private static void AssertPrefabReference(
            Object owner, string fieldName, System.Type expected, string prefabPath)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);

            Assert.That(
                property,
                Is.Not.Null,
                owner.GetType().Name + "." + fieldName + " が見つかりません。");

            Object value = property.objectReferenceValue;

            string guid = string.Empty;
            long fileId = 0;

            if (value != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out fileId);
            }

            string detail =
                "フィールド名: " + owner.GetType().Name + "." + fieldName +
                " / expected型: " + expected.Name +
                " / actual型: " + (value == null ? "null" : value.GetType().Name) +
                " / GUID: " + (value == null ? "-" : guid) +
                " / fileID: " + (value == null ? "-" : fileId.ToString());

            Assert.That(value, Is.Not.Null, detail);
            Assert.That(value, Is.InstanceOf(expected), detail);

            Assert.That(
                AssetDatabase.GetAssetPath(value),
                Is.EqualTo(prefabPath),
                detail);
        }

        [Test]
        public void PrefabReferencesPointAtTheExpectedComponentTypes()
        {
            AssertPrefabReference(
                FindOne<BattleUnitWheelView>(), "itemPrefab",
                typeof(BattleUnitWheelItemView), WheelItemPrefabPath);

            AssertPrefabReference(
                FindOne<BattleHistoryLaneView>(), "slotPrefab",
                typeof(BattleHistorySlotView), HistorySlotPrefabPath);

            AssertPrefabReference(
                FindOne<BattleDeployTransitionView>(), "ghostPrefab",
                typeof(BattleUnitWheelItemView), WheelItemPrefabPath);
        }

        [Test]
        public void TheWheelAndTheGhostShareTheSameWheelItemComponent()
        {
            SerializedObject wheel = new SerializedObject(FindOne<BattleUnitWheelView>());
            SerializedObject ghost =
                new SerializedObject(FindOne<BattleDeployTransitionView>());

            Object item = wheel.FindProperty("itemPrefab").objectReferenceValue;
            Object ghostItem = ghost.FindProperty("ghostPrefab").objectReferenceValue;

            Assert.That(item, Is.Not.Null);
            Assert.That(ghostItem, Is.Not.Null);

            Assert.That(
                ghostItem,
                Is.SameAs(item),
                "出撃ゴーストはリング項目と同じ見た目を使います。");

            Assert.That(
                AssetDatabase.GetAssetPath(item),
                Is.EqualTo(WheelItemPrefabPath));
        }

        [Test]
        public void TheHistoryLanePointsAtTheHistorySlotComponent()
        {
            SerializedObject lane =
                new SerializedObject(FindOne<BattleHistoryLaneView>());

            Object slot = lane.FindProperty("slotPrefab").objectReferenceValue;

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot, Is.InstanceOf<BattleHistorySlotView>());

            Assert.That(
                AssetDatabase.GetAssetPath(slot),
                Is.EqualTo(HistorySlotPrefabPath),
                "履歴枠はリング項目とは別のPrefabです。");

            Assert.That(
                slot,
                Is.Not.InstanceOf<BattleUnitWheelItemView>(),
                "リング項目と履歴項目の責務を混ぜてはいけません。");
        }

        [Test]
        public void HistorySlotPrefab_WiresTheOutcomeBadge()
        {
            BattleHistorySlotView slotAsset =
                AssetDatabase.LoadAssetAtPath<BattleHistorySlotView>(
                    HistorySlotPrefabPath);

            Assert.That(slotAsset, Is.Not.Null, HistorySlotPrefabPath + " が読めません。");

            AssertAssigned(slotAsset, "thumbnail", "orderLabel", "badgeImage", "badgeLabel");

            Image badge = (Image)GetField(slotAsset, "badgeImage");
            RectTransform badgeRect = badge.rectTransform;

            // CORE NODE を一回り大きくした合意値。W/L/D が読める大きさです。
            Assert.That(badgeRect.sizeDelta.x, Is.InRange(34f, 42f));
            Assert.That(badgeRect.sizeDelta.y, Is.InRange(34f, 42f));
            Assert.That(
                badgeRect.sizeDelta.x,
                Is.EqualTo(badgeRect.sizeDelta.y).Within(0.01f),
                "バッジは正方形にします。");

            // W/L/D は読めなければ意味がありません。最低値を固定します。
            TMP_Text badgeText = (TMP_Text)GetField(slotAsset, "badgeLabel");

            Assert.That(
                badgeText.enableAutoSizing,
                Is.False,
                "W/L/D を autosize 任せにしません。");

            Assert.That(
                badgeText.fontSize,
                Is.GreaterThanOrEqualTo(20f),
                "W/L/D の文字が小さすぎます。");
            Assert.That(
                badgeRect.anchorMin,
                Is.EqualTo(new Vector2(1f, 1f)),
                "バッジはカード右上に置きます。");

            // 履歴はタップもドラッグも受け付けません。
            Graphic[] graphics = slotAsset.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics, Is.Not.Empty);
        }

        [Test]
        public void Wheel_ShowsAtMostFiveFullyWiredItems()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            UiTextCatalog text = (UiTextCatalog)GetField(controller, "text");
            AttributePalette palette = (AttributePalette)GetField(controller, "palette");
            RectTransform content = (RectTransform)GetField(wheel, "content");

            List<string> ids = new List<string>();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                ids.Add("p" + i);
            }

            BattleUnitRingModel ring = new BattleUnitRingModel();
            ring.Build(ids);

            wheel.Bind(ring, palette, text);
            wheel.Refresh();

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }

            Assert.That(
                content.childCount,
                Is.EqualTo(BattleRingPresentation.MaxVisible),
                "7体でも同時に出すのは5体までです。");

            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < content.childCount; i++)
            {
                BattleUnitWheelItemView item =
                    content.GetChild(i).GetComponent<BattleUnitWheelItemView>();

                Assert.That(item, Is.Not.Null);
                Assert.That(item.HasRequiredReferences(), Is.True);

                Assert.That(
                    seen.Add(item.InstanceId),
                    Is.True,
                    "同じ個体が2箇所へ出ています。");
            }
        }

        [Test]
        public void EnemyStatus_BuildsSevenFullyWiredMarkers()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();
            EnemySquadStatusView status = FindOne<EnemySquadStatusView>();

            BattleTextCatalog battleText =
                (BattleTextCatalog)GetField(controller, "battleText");

            RectTransform content = (RectTransform)GetField(status, "content");

            status.Build(battleText);

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }

            Assert.That(content.childCount, Is.EqualTo(BattleSquad.UnitCount));

            for (int i = 0; i < content.childCount; i++)
            {
                EnemyMarkerView marker =
                    content.GetChild(i).GetComponent<EnemyMarkerView>();

                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.HasRequiredReferences(), Is.True);
                Assert.That(marker.State, Is.EqualTo(EnemyMarkerState.Unused));
            }
        }

        // ---------------- 画面遷移 ----------------

        [Test]
        public void SceneNavigation_OffersHomeAndUnitSet()
        {
            List<SceneLoadButton> buttons = FindAll<SceneLoadButton>();

            List<string> sceneNames = new List<string>();

            for (int i = 0; i < buttons.Count; i++)
            {
                AssertAssigned(buttons[i], "button", "sceneName");

                sceneNames.Add((string)GetField(buttons[i], "sceneName"));
            }

            Assert.That(sceneNames, Does.Contain("Home"), "HOMEへ戻れる必要があります。");
            Assert.That(
                sceneNames,
                Does.Contain("UnitSet"),
                "編成不足のときUnitSetへ行ける必要があります。");

            for (int i = 0; i < sceneNames.Count; i++)
            {
                Assert.That(
                    IsInBuildSettings(sceneNames[i]),
                    Is.True,
                    sceneNames[i] + " が Build Settings にありません。");
            }
        }

        [Test]
        public void SquadRequiredPrompt_OffersBothUnitSetAndHome()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            GameObject squadRequired =
                (GameObject)GetField(controller, "squadRequiredRoot");

            SceneLoadButton[] buttons =
                squadRequired.GetComponentsInChildren<SceneLoadButton>(true);

            List<string> sceneNames = new List<string>();

            for (int i = 0; i < buttons.Length; i++)
            {
                sceneNames.Add((string)GetField(buttons[i], "sceneName"));
            }

            Assert.That(sceneNames, Does.Contain("UnitSet"));
            Assert.That(sceneNames, Does.Contain("Home"));
        }

        [Test]
        public void RematchAndFxButtonsExistWithoutPersistentCalls()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            Button rematch = (Button)GetField(controller, "rematchButton");

            BattleSettingsView settings = FindOne<BattleSettingsView>();
            Button fx = (Button)GetField(settings, "fxButton");

            // 呼び出しはコードから登録します。シーンへ固定の呼び出しは置きません。
            Assert.That(rematch.onClick.GetPersistentEventCount(), Is.EqualTo(0));
            Assert.That(fx.onClick.GetPersistentEventCount(), Is.EqualTo(0));

            // 大きなDEPLOYボタンは撤去しました。出撃は中央ユニットの上スライドだけです。
            Button[] buttons = controller.GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.That(
                    buttons[i].name,
                    Is.Not.EqualTo("DeployButton"),
                    "DEPLOYボタンが残っています。");
            }
        }

        // ---------------- ヘッダーと設定パネル ----------------

        /// <summary>
        /// Safe Area の内側かどうか。SafeAreaController は asmdef の外
        /// （Assembly-CSharp）にあるため、型ではなく名前で探します。
        /// </summary>
        private static bool IsInsideSafeArea(Transform target)
        {
            for (Transform node = target; node != null; node = node.parent)
            {
                Component[] components = node.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null &&
                        components[i].GetType().Name == "SafeAreaController")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>シーン上のヘッダー。</summary>
        private RectTransform Header()
        {
            RectTransform header = FindOne<BattleScoreView>()
                .GetComponent<RectTransform>();

            Assert.That(header, Is.Not.Null);

            return header;
        }

        private CanvasScaler Scaler()
        {
            return FindOne<CanvasScaler>();
        }

        [Test]
        public void Header_NoLongerCarriesTheHomeOrFxControls()
        {
            RectTransform header = Header();

            Selectable[] controls = header.GetComponentsInChildren<Selectable>(true);

            Assert.That(
                controls.Length,
                Is.EqualTo(1),
                "ヘッダーに置く操作は右上の歯車だけです。");

            Assert.That(controls[0].name, Is.EqualTo("SettingsButton"));

            SceneLoadButton[] navigation =
                header.GetComponentsInChildren<SceneLoadButton>(true);

            Assert.That(
                navigation,
                Is.Empty,
                "左上のHOMEは撤去しました。画面離脱は設定パネルからだけです。");

            TMP_Text[] labels = header.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < labels.Length; i++)
            {
                Assert.That(
                    labels[i].text,
                    Does.Not.Contain("FX"),
                    "常時表示の FX ON/OFF は撤去しました。");

                Assert.That(labels[i].text, Does.Not.EqualTo("HOME"));
            }
        }

        [Test]
        public void Header_ShowsTheTitleInTheCentreWithRoundAndScoreBelow()
        {
            RectTransform header = Header();

            RectTransform title = header.Find("TitleLabel") as RectTransform;
            BattleScoreView score = FindOne<BattleScoreView>();

            Assert.That(title, Is.Not.Null);
            Assert.That(
                title.GetComponent<TMP_Text>().text,
                Is.EqualTo("BATTLE"));

            Assert.That(title.anchorMin.x, Is.EqualTo(0.5f));
            Assert.That(title.anchorMax.x, Is.EqualTo(0.5f));
            Assert.That(title.anchoredPosition.x, Is.EqualTo(0f));

            RectTransform round =
                ((TMP_Text)GetField(score, "roundLabel")).rectTransform;
            RectTransform scoreLabel =
                ((TMP_Text)GetField(score, "scoreLabel")).rectTransform;

            // 下段はタイトルより下に置きます。
            Assert.That(
                round.anchoredPosition.y, Is.LessThan(title.anchoredPosition.y));
            Assert.That(
                scoreLabel.anchoredPosition.y, Is.LessThan(round.anchoredPosition.y));
        }

        [Test]
        public void SettingsGear_IsWiredAndLargeEnoughToTap()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            Button gear = (Button)GetField(settings, "settingsButton");

            Assert.That(gear, Is.Not.Null);
            Assert.That(gear.name, Is.EqualTo("SettingsButton"));

            RectTransform rect = gear.GetComponent<RectTransform>();

            // 右上に置きます。
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(1f, 1f)));

            MobileLayoutMetrics.AssertTapTarget(rect, Scaler());

            Assert.That(
                gear.onClick.GetPersistentEventCount(),
                Is.EqualTo(0),
                "呼び出しはコードから登録します。");

            Assert.That(
                gear.GetComponentInParent<BattleScoreView>(),
                Is.Not.Null,
                "歯車はヘッダーの中に置きます。");
        }

        [Test]
        public void SettingsGear_IsDrawnFromImagePrimitivesOnly()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            Button gear = (Button)GetField(settings, "settingsButton");

            Transform icon = gear.transform.Find("GearIcon");

            Assert.That(icon, Is.Not.Null, "歯車の絵はGearIconの下にまとめます。");

            Assert.That(
                gear.GetComponentsInChildren<TMP_Text>(true),
                Is.Empty,
                "「⚙」のような未収録文字へ依存しません。文字は一切使いません。");

            int teeth = 0;

            for (int i = 0; i < icon.childCount; i++)
            {
                if (icon.GetChild(i).name.StartsWith("Tooth"))
                {
                    teeth++;
                }
            }

            Assert.That(
                teeth,
                Is.InRange(6, 8),
                "歯車として読める数の歯（6〜8個）が必要です。");

            Assert.That(icon.Find("Ring"), Is.Not.Null, "外周がありません。");
            Assert.That(icon.Find("Hub"), Is.Not.Null, "中央の円がありません。");

            // 歯は中心から同じ距離に、別々の場所へ並べます。
            List<Vector2> positions = new List<Vector2>();
            List<float> radii = new List<float>();

            for (int i = 0; i < icon.childCount; i++)
            {
                Transform child = icon.GetChild(i);

                if (!child.name.StartsWith("Tooth"))
                {
                    continue;
                }

                Vector2 position =
                    ((RectTransform)child).anchoredPosition;

                for (int j = 0; j < positions.Count; j++)
                {
                    Assert.That(
                        Vector2.Distance(positions[j], position),
                        Is.GreaterThan(1f),
                        "歯が同じ場所に重なっています。");
                }

                positions.Add(position);
                radii.Add(position.magnitude);
            }

            Assert.That(positions.Count, Is.EqualTo(teeth));

            for (int i = 0; i < radii.Count; i++)
            {
                Assert.That(
                    radii[i],
                    Is.EqualTo(radii[0]).Within(1f),
                    "歯は中心から同じ距離へ並べます。");

                Assert.That(radii[i], Is.GreaterThan(0f));
            }

            Image[] parts = icon.GetComponentsInChildren<Image>(true);

            Assert.That(
                parts.Length,
                Is.GreaterThanOrEqualTo(teeth + 2),
                "歯・外周・中央円はすべてImageで作ります。");

            for (int i = 0; i < parts.Length; i++)
            {
                Assert.That(
                    parts[i].sprite,
                    Is.Not.Null,
                    parts[i].name + " は外部画像を足さずUnity内蔵のスプライトで描きます。");
            }
        }

        [Test]
        public void SettingsGear_TakesTapsOnTheRootOnly()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            Button gear = (Button)GetField(settings, "settingsButton");

            Image root = gear.GetComponent<Image>();

            Assert.That(
                root.raycastTarget,
                Is.True,
                "タップを受けるのはルートのボタンだけです。");

            Graphic[] children =
                gear.transform.Find("GearIcon").GetComponentsInChildren<Graphic>(true);

            Assert.That(children, Is.Not.Empty);

            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(
                    children[i].raycastTarget,
                    Is.False,
                    children[i].name + " がタップを奪っています。");
            }
        }

        [Test]
        public void SettingsPanel_ExistsOnceAndIsFullyWired()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();

            AssertAssigned(settings,
                "panelRoot", "panelBody",
                "settingsButton", "backdropButton",
                "fxButton", "homeButton", "closeButton",
                "titleLabel", "fxCaptionLabel", "fxValueLabel",
                "homeLabel", "closeLabel");

            Assert.That(settings.HasRequiredReferences(), Is.True);
        }

        [Test]
        public void SettingsPanel_StartsHidden()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();

            GameObject panel = (GameObject)GetField(settings, "panelRoot");

            Assert.That(
                panel.activeSelf,
                Is.False,
                "設定パネルの初期状態は非表示です。");

            Assert.That(settings.IsOpen, Is.False);
        }

        [Test]
        public void SettingsPanel_OffersFxHomeAndCloseWithTappableTargets()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            CanvasScaler scaler = Scaler();

            Button fx = (Button)GetField(settings, "fxButton");
            Button home = (Button)GetField(settings, "homeButton");
            Button close = (Button)GetField(settings, "closeButton");

            MobileLayoutMetrics.AssertTapTarget(
                fx.GetComponent<RectTransform>(), scaler);

            // HOME と CLOSE は横方向に伸縮するため、高さだけを見ます。
            float points = MobileLayoutMetrics.PointsPerUnit(scaler);

            Assert.That(
                home.GetComponent<RectTransform>().sizeDelta.y * points,
                Is.GreaterThanOrEqualTo(MobileLayoutMetrics.MinimumTapPoints));

            Assert.That(
                close.GetComponent<RectTransform>().sizeDelta.y * points,
                Is.GreaterThanOrEqualTo(MobileLayoutMetrics.MinimumTapPoints));

            Assert.That(
                ((TMP_Text)GetField(settings, "titleLabel")).text,
                Is.EqualTo("SETTINGS"));

            SceneLoadButton navigation = home.GetComponent<SceneLoadButton>();

            Assert.That(
                navigation,
                Is.Not.Null,
                "設定パネルのHOMEからHomeへ移動できる必要があります。");

            Assert.That((string)GetField(navigation, "sceneName"), Is.EqualTo("Home"));
            Assert.That(GetField(navigation, "button"), Is.SameAs(home));
        }

        [Test]
        public void SettingsPanel_BlocksTheBattleBehindItAndFitsInTheSafeArea()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            CanvasScaler scaler = Scaler();

            Button backdrop = (Button)GetField(settings, "backdropButton");
            RectTransform body = (RectTransform)GetField(settings, "panelBody");

            Assert.That(
                backdrop.GetComponent<Image>().raycastTarget,
                Is.True,
                "パネルの後ろへタップが抜けないようにします。");

            Assert.That(
                body.GetComponent<Image>().raycastTarget,
                Is.True,
                "パネル本体のタップが、外側の閉じる判定へ落ちないようにします。");

            // Safe Area の内側へ収まっていること。
            Assert.That(
                IsInsideSafeArea(settings.transform),
                Is.True,
                "設定パネルは Safe Area の下に置きます。");

            Vector2 size = MobileLayoutMetrics.FixedSize(body);

            Assert.That(
                size.x,
                Is.LessThanOrEqualTo(MobileLayoutMetrics.SafeAreaWidthUnits(scaler)));

            Assert.That(
                size.y,
                Is.LessThanOrEqualTo(MobileLayoutMetrics.SafeAreaHeightUnits(scaler)));
        }

        [Test]
        public void SettingsPanel_IsDrawnAboveTheBattleScreen()
        {
            BattleSettingsView settings = FindOne<BattleSettingsView>();
            BattleScreenController controller = FindOne<BattleScreenController>();

            GameObject battleRoot = (GameObject)GetField(controller, "battleRoot");

            Transform panel = settings.transform;

            Assert.That(
                panel.parent,
                Is.SameAs(battleRoot.transform.parent),
                "設定パネルとバトル本体は同じ親に並べます。");

            Assert.That(
                panel.GetSiblingIndex(),
                Is.GreaterThan(battleRoot.transform.GetSiblingIndex()),
                "設定パネルはバトル画面より手前に描きます。");
        }

        // ---------------- ファイルとしての健全性 ----------------

        [Test]
        public void SceneFile_HasNoDuplicateFileIds()
        {
            AssertNoDuplicateFileIds(ScenePath);
            AssertNoDuplicateFileIds(WheelItemPrefabPath);
            AssertNoDuplicateFileIds(HistorySlotPrefabPath);
            AssertNoDuplicateFileIds(EnemyMarkerPrefabPath);
        }

        private static void AssertNoDuplicateFileIds(string assetPath)
        {
            string full = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);

            Assert.That(File.Exists(full), Is.True, full + " がありません。");

            Regex anchor = new Regex(@"^--- !u!\d+ &(-?\d+)");
            HashSet<string> seen = new HashSet<string>();

            foreach (string line in File.ReadAllLines(full))
            {
                Match match = anchor.Match(line);

                if (!match.Success)
                {
                    continue;
                }

                Assert.That(
                    seen.Add(match.Groups[1].Value),
                    Is.True,
                    assetPath + " に重複した fileID があります: " + match.Groups[1].Value);
            }
        }

        [Test]
        public void OtherScenesDoNotContainBattleUiComponents()
        {
            for (int i = 0; i < OtherScenePaths.Length; i++)
            {
                // Battle.unity を残したまま重ねて開くと Global Light 2D が
                // 複数になるため、必ず単独（Single）で開きます。
                Scene other = OpenSceneAlone(OtherScenePaths[i]);

                List<BattleScreenController> controllers =
                    new List<BattleScreenController>();

                foreach (GameObject root in other.GetRootGameObjects())
                {
                    controllers.AddRange(
                        root.GetComponentsInChildren<BattleScreenController>(true));
                }

                Assert.That(
                    controllers,
                    Is.Empty,
                    OtherScenePaths[i] + " へ Battle UI が混入しています。");

                // 次の反復の Single オープンと OneTimeTearDown の復元で閉じるため、
                // ここでは CloseScene しません（唯一のシーンは閉じられない）。
            }
        }

        private static bool IsInBuildSettings(string sceneName)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (Path.GetFileNameWithoutExtension(scenes[i].path) == sceneName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
