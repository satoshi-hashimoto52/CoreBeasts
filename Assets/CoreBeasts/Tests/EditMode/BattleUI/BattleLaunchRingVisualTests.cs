using System.Collections.Generic;
using System.Reflection;

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
    /// CORE LAUNCH RING の見た目と構造。
    ///
    /// 発進台・コアゲート・CORE NODE・勝利ピップを、
    /// 実際の Scene / Prefab から読んで値で確かめます。
    /// </summary>
    public sealed class BattleLaunchRingVisualTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";
        private const string WheelItemPath = "Assets/CoreBeasts/Prefabs/BattleWheelItem.prefab";
        private const string HistorySlotPath =
            "Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab";

        private Scene scene;
        private readonly List<Object> spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // Additive だと、直前のテストが開いたシーンと同居して
            // Global Light 2D が2つになり、URP 2D が Error を出します。
            // NUnit は想定外の Error ログを失敗として扱うため、常に単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
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

            // 単独で開いているときは閉じられません（唯一のシーンは閉じられない）。
            // 次の SetUp が Single で開き直すので、ここでは残しておきます。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private T FindOne<T>() where T : Component
        {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(found.Count, Is.EqualTo(1), typeof(T).Name + " は1個だけです。");

            return found[0];
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
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

        private GameObject Spawn(string path)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(asset, Is.Not.Null, path + " が読めません。");

            GameObject instance = Object.Instantiate(asset);
            spawned.Add(instance);

            return instance;
        }

        // ---------------- 発進台 ----------------

        [Test]
        public void TheWheelItemIsALaunchPedestalNotACard()
        {
            GameObject item = Spawn(WheelItemPath);
            BattleUnitWheelItemView view = item.GetComponent<BattleUnitWheelItemView>();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.HasRequiredReferences(), Is.True);

            Assert.That(
                FindDeep(item.transform, "PedestalFill"), Is.Not.Null, "発進台の面がありません。");
            Assert.That(
                FindDeep(item.transform, "AttributeRim"), Is.Not.Null, "発進台の外周がありません。");
            Assert.That(
                FindDeep(item.transform, "ActiveGlow"), Is.Not.Null, "中央の発光がありません。");
            Assert.That(
                FindDeep(item.transform, "CharacterRoot"), Is.Not.Null, "キャラクターがありません。");

            // 大きな長方形カードの背景は持ちません。
            Assert.That(
                item.GetComponentsInChildren<Image>(true),
                Is.Empty,
                "リング項目に長方形の Image を残してはいけません。");

            Assert.That(
                item.GetComponentsInChildren<DiagonalAttributeCardGraphic>(true),
                Is.Empty,
                "UnitSet のカード表現をそのまま使ってはいけません。");
        }

        [Test]
        public void TheWheelItemNeverPutsTextOverTheCharacter()
        {
            GameObject item = Spawn(WheelItemPath);

            TMP_Text[] labels = item.GetComponentsInChildren<TMP_Text>(true);

            Assert.That(
                labels.Length,
                Is.EqualTo(2),
                "残す文字は編成番号とレベルだけです（個体名は載せません）。");

            Transform character = FindDeep(item.transform, "CharacterRoot");
            Rect characterRect = WorldRect((RectTransform)character);

            for (int i = 0; i < labels.Length; i++)
            {
                Assert.That(
                    WorldRect(labels[i].rectTransform).Overlaps(characterRect),
                    Is.False,
                    labels[i].name + " がキャラクターへ重なっています。");
            }
        }

        [Test]
        public void EveryWheelItemGraphicIsDrawingOnly()
        {
            GameObject item = Spawn(WheelItemPath);

            Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics, Is.Not.Empty);

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " が入力を奪います。");
            }
        }

        [Test]
        public void ThePedestalRimTakesItsColourFromTheSharedPalette()
        {
            GameObject item = Spawn(WheelItemPath);
            BattleUnitWheelItemView view = item.GetComponent<BattleUnitWheelItemView>();

            AttributePalette palette = TestPalette();
            UiTextCatalog catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            spawned.Add(catalog);

            TestBattleCards cards = new TestBattleCards();

            try
            {
                CoreBeastDefinition dual =
                    cards.CreateDefinition("rb", UnitAttribute.Red, 50, true, UnitAttribute.Blue);

                BattleUnitCard.TryCreate("p0", 1, dual, out BattleUnitCard card);

                view.Bind(new BattleRingSlot("p0", 1), card, palette, catalog);

                LaunchPedestalGraphic rim = view.AttributeRim;

                Assert.That(rim, Is.Not.Null);
                Assert.That(rim.IsDualAttribute, Is.True, "2属性が反映されていません。");

                Assert.That(
                    rim.PrimaryColor,
                    Is.EqualTo(palette.GetColors(UnitAttribute.Red).PrimaryColor),
                    "第一属性色が Palette と一致しません。");

                Assert.That(
                    rim.SecondaryColor,
                    Is.EqualTo(palette.GetColors(UnitAttribute.Blue).PrimaryColor),
                    "第二属性色が Palette と一致しません。");
            }
            finally
            {
                cards.Cleanup();
                Object.DestroyImmediate(palette);
            }
        }

        private AttributePalette TestPalette()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            AttributePalette scene = (AttributePalette)GetField(controller, "palette");

            Assert.That(scene, Is.Not.Null);

            return Object.Instantiate(scene);
        }

        [Test]
        public void TheCentreIsLargerAndTheOuterIsSmaller()
        {
            Assert.That(
                BattleRingLayout.CenterScale,
                Is.InRange(1.20f, 1.28f),
                "中央の倍率が仕様の範囲外です。");

            Assert.That(
                BattleRingLayout.AdjacentScale,
                Is.InRange(0.85f, 0.95f),
                "隣接の倍率が仕様の範囲外です。");

            Assert.That(
                BattleRingLayout.OuterScale,
                Is.InRange(0.65f, 0.78f),
                "外側の倍率が仕様の範囲外です。");

            Assert.That(
                BattleRingLayout.CenterScale,
                Is.GreaterThan(BattleRingLayout.AdjacentScale));

            Assert.That(
                BattleRingLayout.OuterScale,
                Is.LessThanOrEqualTo(BattleRingLayout.AdjacentScale));

            // 中央ほど明るく。
            Assert.That(
                BattleRingLayout.CenterAlpha,
                Is.GreaterThan(BattleRingLayout.AdjacentAlpha));

            Assert.That(
                BattleRingLayout.AdjacentAlpha,
                Is.GreaterThan(BattleRingLayout.OuterAlpha));
        }

        [Test]
        public void TheCharacterIsNeverTiltedByTheRing()
        {
            GameObject item = Spawn(WheelItemPath);
            BattleUnitWheelItemView view = item.GetComponent<BattleUnitWheelItemView>();

            view.ApplySample(BattleRingLayout.Evaluate(2f), 2f, 180f, 2.43f);

            Assert.That(
                view.Root.localRotation,
                Is.EqualTo(Quaternion.identity),
                "キャラクターを傾けてはいけません。円弧へ沿わせるのは位置だけです。");
        }

        // ---------------- 案内 ----------------

        [Test]
        public void ThereIsNoWhiteCentreLineAndNoAlwaysOnSlideUpText()
        {
            BattleSlideUpGuideView guide = FindOne<BattleSlideUpGuideView>();

            Assert.That(
                guide.GetComponentsInChildren<TMP_Text>(true),
                Is.Empty,
                "常時表示の SLIDE UP 文字は撤去します。");

            Assert.That(
                FindDeep(guide.transform, "GuideLine"),
                Is.Null,
                "白い中心線は撤去します。");

            // 残っている装飾が純白でないこと。
            Graphic[] graphics = guide.GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                Color color = graphics[i].color;

                bool pureWhite = color.r > 0.97f && color.g > 0.97f && color.b > 0.97f;

                Assert.That(
                    pureWhite,
                    Is.False,
                    graphics[i].name + " が純白です。シアンかアンバーにします。");

                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " が入力を奪います。");
            }
        }

        [Test]
        public void TheLaunchRailOnlyAppearsWhileSlidingUp()
        {
            BattleSlideUpGuideView guide = FindOne<BattleSlideUpGuideView>();

            Assert.That(guide.HasRequiredReferences(), Is.True);

            guide.HideRail();
            Assert.That(guide.IsRailVisible, Is.False, "通常時にレールを出しません。");

            guide.ShowRail(0.5f);
            Assert.That(guide.IsRailVisible, Is.True, "上ロック中はレールを出します。");
            Assert.That(guide.Progress, Is.EqualTo(0.5f).Within(0.0001f));

            guide.HideRail();
            Assert.That(
                guide.IsRailVisible,
                Is.False,
                "PointerUp / キャンセル後にレールが残ってはいけません。");

            Assert.That(guide.Progress, Is.EqualTo(0f));
        }

        [Test]
        public void TheIdleHintIsJustTheChevron()
        {
            BattleSlideUpGuideView guide = FindOne<BattleSlideUpGuideView>();

            guide.SetHintVisible(true);

            Assert.That(guide.IsHintVisible, Is.True);
            Assert.That(
                guide.IsRailVisible,
                Is.False,
                "通常時の案内はシェブロンだけです。");

            guide.SetHintVisible(false);
            Assert.That(guide.IsHintVisible, Is.False);
        }

        // ---------------- CORE NODE ----------------

        [Test]
        public void TheHistorySlotIsACoreNode()
        {
            GameObject node = Spawn(HistorySlotPath);
            BattleHistorySlotView view = node.GetComponent<BattleHistorySlotView>();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.HasRequiredReferences(), Is.True);

            Assert.That(
                FindDeep(node.transform, "NodeRing"), Is.Not.Null, "ノード外周がありません。");
            Assert.That(
                FindDeep(node.transform, "NodeSocket"), Is.Not.Null, "ノード内側がありません。");

            Assert.That(
                view.NodeRing,
                Is.Not.Null,
                "結果色で点灯する外周が未配線です。");

            Graphic[] graphics = node.GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " : 履歴は入力を取りません。");
            }
        }

        [Test]
        public void TheNodeRingLightsWithTheResult()
        {
            GameObject node = Spawn(HistorySlotPath);
            BattleHistorySlotView view = node.GetComponent<BattleHistorySlotView>();

            AttributePalette palette = TestPalette();
            UiTextCatalog catalog = ScriptableObject.CreateInstance<UiTextCatalog>();
            spawned.Add(catalog);

            try
            {
                view.Show(
                    new BattleHistoryEntry("p0", 1, BattleSlotOutcome.Win),
                    null, palette, catalog);

                Color win = view.NodeRing.PrimaryColor;

                view.Show(
                    new BattleHistoryEntry("p1", 2, BattleSlotOutcome.Loss),
                    null, palette, catalog);

                Color loss = view.NodeRing.PrimaryColor;

                Assert.That(
                    win,
                    Is.Not.EqualTo(loss),
                    "Win と Loss でノードの色が同じです。");

                Assert.That(view.IsDefeated, Is.True, "Loss はグレー扱いにします。");

                view.Show(
                    new BattleHistoryEntry("p2", 3, BattleSlotOutcome.Draw),
                    null, palette, catalog);

                Assert.That(view.IsDefeated, Is.False, "Draw はグレーにしません。");
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        // ---------------- 戦闘終了の片付け ----------------

        [Test]
        public void TheCombatantFadesWhereItStandsInsteadOfMoving()
        {
            BattleCombatantView player =
                (BattleCombatantView)GetField(
                    FindOne<BattleScreenController>(), "playerCombatant");

            RectTransform rect = player.GetComponent<RectTransform>();

            Vector2 before = rect.anchoredPosition;

            player.SetFadeOut(1f);

            Assert.That(player.FadeOut, Is.EqualTo(1f));
            Assert.That(
                rect.anchoredPosition,
                Is.EqualTo(before),
                "戦闘表示を履歴の位置へ動かしてはいけません。");

            Assert.That(
                player.PortraitGroup.alpha,
                Is.EqualTo(0f).Within(0.0001f),
                "その場で消えていません。");

            player.SetFadeOut(0f);

            Assert.That(player.PortraitGroup.alpha, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(before));
        }

        // ---------------- コアゲートと勝利ピップ ----------------

        [Test]
        public void TheCoreGateSitsBetweenTheStagesAndTakesNoInput()
        {
            BattleCoreGateView gate = FindOne<BattleCoreGateView>();

            Assert.That(gate.HasRequiredReferences(), Is.True);

            Graphic[] graphics = gate.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics.Length, Is.GreaterThanOrEqualTo(3), "同心の円弧で作ります。");

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " が入力を奪います。");

                Color color = graphics[i].color;

                Assert.That(
                    color.r > 0.97f && color.g > 0.97f && color.b > 0.97f,
                    Is.False,
                    graphics[i].name + " が純白です。");
            }

            // 上側（CPU）と下側（PLAYER）のあいだに置きます。
            BattleScreenController controller = FindOne<BattleScreenController>();

            RectTransform cpu = ((BattleCombatantView)GetField(controller, "cpuCombatant"))
                .GetComponent<RectTransform>();
            RectTransform player = ((BattleCombatantView)GetField(controller, "playerCombatant"))
                .GetComponent<RectTransform>();

            float gateY = WorldRect(gate.GetComponent<RectTransform>()).center.y;

            Assert.That(gateY, Is.LessThan(WorldRect(cpu).center.y));
            Assert.That(gateY, Is.GreaterThan(WorldRect(player).center.y));
        }

        [Test]
        public void TheCoreGateChangesWithTheRevealedResult()
        {
            BattleCoreGateView gate = FindOne<BattleCoreGateView>();

            gate.SetIdle();
            float idle = gate.Intensity;

            gate.SetOutcome(BattleSlotOutcome.Win);

            Assert.That(
                gate.Intensity,
                Is.GreaterThan(idle),
                "結果公開時に光量が上がっていません。");

            gate.SetOutcome(BattleSlotOutcome.None);

            Assert.That(gate.Intensity, Is.EqualTo(idle), "通常時へ戻りません。");
        }

        [Test]
        public void TheVictoryPipsTrackTheScore()
        {
            BattleScorePipsView pips = FindOne<BattleScorePipsView>();

            Assert.That(pips.HasRequiredReferences(), Is.True);
            Assert.That(pips.PipsPerSide, Is.InRange(3, 4), "片側 4 個程度にします。");

            pips.Refresh(0, 0);
            Assert.That(pips.IsPlayerPipLit(0), Is.False);

            pips.Refresh(2, 1);

            Assert.That(pips.PlayerWins, Is.EqualTo(2));
            Assert.That(pips.CpuWins, Is.EqualTo(1));
            Assert.That(pips.IsPlayerPipLit(0), Is.True);
            Assert.That(pips.IsPlayerPipLit(1), Is.True);
            Assert.That(
                pips.IsPlayerPipLit(2),
                Is.False,
                "勝利数より多く点灯しています。");

            // 引き分けでは増えません（勝利数が変わらないため）。
            pips.Refresh(2, 1);
            Assert.That(pips.IsPlayerPipLit(2), Is.False);

            Graphic[] graphics = pips.GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(graphics[i].raycastTarget, Is.False);
            }
        }

        // ---------------- 重なりと余白（実測） ----------------

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

        private static void AssertNoOverlap(
            RectTransform a, RectTransform b, string because)
        {
            Assert.That(
                WorldRect(a).Overlaps(WorldRect(b)),
                Is.False,
                because + "（" + a.name + " と " + b.name + " が重なっています）");
        }

        [Test]
        public void TheScorePipsNeverTouchTheScoreText()
        {
            RectTransform score = Rect("ScoreLabel");
            RectTransform pips = Rect("ScorePips");

            Rect scoreRect = WorldRect(score);

            for (int i = 0; i < pips.childCount; i++)
            {
                Rect pip = WorldRect((RectTransform)pips.GetChild(i));

                Assert.That(
                    pip.Overlaps(scoreRect),
                    Is.False,
                    pips.GetChild(i).name + " がスコア文字へ重なっています。");
            }

            // Player 側は左、CPU 側は右。
            for (int i = 0; i < pips.childCount; i++)
            {
                RectTransform pip = (RectTransform)pips.GetChild(i);
                float centre = WorldRect(pip).center.x;

                if (pip.name.StartsWith("PipP"))
                {
                    Assert.That(centre, Is.LessThan(scoreRect.center.x));
                }
                else
                {
                    Assert.That(centre, Is.GreaterThan(scoreRect.center.x));
                }
            }
        }

        [Test]
        public void TheHeaderKeepsThreeSeparateRows()
        {
            // TitleLabel は複数の画面に存在するため、Header の中だけを見ます。
            RectTransform header = Rect("Header");

            RectTransform titleRect =
                (RectTransform)FindDeep(header, "TitleLabel");

            Assert.That(titleRect, Is.Not.Null, "Header に TitleLabel がありません。");

            Rect title = WorldRect(titleRect);
            Rect round = WorldRect(Rect("RoundLabel"));
            Rect score = WorldRect(Rect("ScoreLabel"));

            Assert.That(title.yMin, Is.GreaterThanOrEqualTo(round.yMax - 0.01f),
                "BATTLE と ROUND が同じ帯に居ます。");

            Assert.That(round.yMin, Is.GreaterThanOrEqualTo(score.yMax - 0.01f),
                "ROUND とスコアが同じ帯に居ます。");

            // 歯車のタップ領域は縮めません。
            RectTransform gear = Rect("SettingsButton");

            Assert.That(gear.sizeDelta.x, Is.GreaterThanOrEqualTo(110f));
            Assert.That(gear.sizeDelta.y, Is.GreaterThanOrEqualTo(110f));

            AssertNoOverlap(gear, Rect("RoundLabel"), "歯車が ROUND へ重なります");
        }

        [Test]
        public void TheCoreGateNeverCrossesTheSurroundingText()
        {
            RectTransform gate = Rect("CoreGate");

            AssertNoOverlap(gate, Rect("RemainingLabel"), "CoreGate が LEFT n/7 へ重なります");
            AssertNoOverlap(gate, Rect("PlayerCombatant"), "CoreGate が PLAYER STAGE へ重なります");
            AssertNoOverlap(gate, Rect("CpuCombatant"), "CoreGate が ENEMY STAGE へ重なります");
            AssertNoOverlap(gate, Rect("EnemySquadStatus"), "CoreGate が敵HUDへ重なります");

            // VS はゲートのコア内へ収めます（意図的に内側）。
            Rect vs = WorldRect(Rect("VsLabel"));
            Rect core = WorldRect(Rect("GateCore"));

            Assert.That(
                core.Contains(vs.center),
                Is.True,
                "VS がコア中央に収まっていません。");

            // 線が細すぎてゴミに見えないこと。
            LaunchPedestalGraphic[] rings =
                gate.GetComponentsInChildren<LaunchPedestalGraphic>(true);

            for (int i = 0; i < rings.Length; i++)
            {
                FieldInfo width = typeof(LaunchPedestalGraphic).GetField(
                    "rimWidth", BindingFlags.NonPublic | BindingFlags.Instance);

                Assert.That(
                    (float)width.GetValue(rings[i]),
                    Is.GreaterThanOrEqualTo(0.06f),
                    rings[i].name + " の線が細すぎます。");
            }
        }

        [Test]
        public void TheStagesAreThinFloorsInsteadOfBigPanels()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            foreach (string side in new[] { "cpuCombatant", "playerCombatant" })
            {
                BattleCombatantView view =
                    (BattleCombatantView)GetField(controller, side);

                RectTransform stage = view.GetComponent<RectTransform>();
                float stageHeight = WorldRect(stage).height;

                Transform edge = FindDeep(stage, "Edge");
                Transform background = FindDeep(stage, "Background");

                Assert.That(edge, Is.Not.Null);
                Assert.That(background, Is.Not.Null);

                // 床は舞台の高さのごく一部にします（大きな塗り潰しパネルにしない）。
                Assert.That(
                    WorldRect((RectTransform)edge).height / stageHeight,
                    Is.LessThan(0.2f),
                    side + " の背景がまだ大きな長方形パネルです。");

                Assert.That(
                    WorldRect((RectTransform)background).height / stageHeight,
                    Is.LessThan(0.2f),
                    side + " の背景がまだ大きな長方形パネルです。");
            }
        }

        [Test]
        public void ThereIsNoLargeEmptyBandBetweenThePlayerStageAndTheRing()
        {
            Rect player = WorldRect(Rect("PlayerCombatant"));
            Rect history = WorldRect(Rect("HistoryLane"));
            Rect wheel = WorldRect(Rect("PlayerWheel"));

            float safeHeight = WorldRect(Rect("SafeArea")).height;

            float gapA = player.yMin - history.yMax;
            float gapB = history.yMin - wheel.yMax;

            Assert.That(
                gapA / safeHeight,
                Is.LessThan(0.05f),
                "PLAYER STAGE と履歴のあいだに無目的な空白があります。");

            Assert.That(
                gapB / safeHeight,
                Is.LessThan(0.05f),
                "履歴とリングのあいだに無目的な空白があります。");

            Assert.That(gapA, Is.GreaterThanOrEqualTo(0f));
            Assert.That(gapB, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void TheSevenCoreNodesFitInsideTheSafeArea()
        {
            RectTransform lane = Rect("HistoryLane");

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HistorySlotPath);
            RectTransform node = asset.GetComponent<RectTransform>();

            float needed = node.sizeDelta.x * BattleHistoryModel.MaxEntries;

            Assert.That(
                needed,
                Is.LessThanOrEqualTo(WorldRect(lane).width),
                "CORE NODE 7個がレーンに収まりません。");

            // 読める大きさであること。
            Assert.That(node.sizeDelta.x, Is.GreaterThanOrEqualTo(96f));
            Assert.That(node.sizeDelta.y, Is.GreaterThanOrEqualTo(108f));

            GameObject instance = Object.Instantiate(asset);
            spawned.Add(instance);

            TMP_Text badge = FindDeep(instance.transform, "OutcomeLabel")
                .GetComponent<TMP_Text>();

            Assert.That(
                badge.rectTransform.parent.GetComponent<RectTransform>().sizeDelta.x,
                Is.GreaterThanOrEqualTo(26f),
                "W/L/D バッジが小さすぎます。");

            AssertNoOverlap(
                (RectTransform)FindDeep(instance.transform, "OrderLabel"),
                (RectTransform)FindDeep(instance.transform, "OutcomeBadge"),
                "編成番号と結果文字が重なります");
        }

        [Test]
        public void TheCentrePedestalReadsAsALaunchPad()
        {
            GameObject item = Spawn(WheelItemPath);

            RectTransform character =
                (RectTransform)FindDeep(item.transform, "CharacterRoot");
            RectTransform rim = (RectTransform)FindDeep(item.transform, "AttributeRim");
            RectTransform glow = (RectTransform)FindDeep(item.transform, "ActiveGlow");

            // 発進台はキャラクターの足元として読める幅を持つこと。
            Assert.That(
                rim.sizeDelta.x / character.sizeDelta.x,
                Is.GreaterThan(0.8f),
                "発進台がキャラクターに対して小さすぎます。");

            // Rim と Glow が同じ太さだと区別がつきません。
            FieldInfo width = typeof(LaunchPedestalGraphic).GetField(
                "rimWidth", BindingFlags.NonPublic | BindingFlags.Instance);

            float rimWidth = (float)width.GetValue(rim.GetComponent<LaunchPedestalGraphic>());
            float glowWidth = (float)width.GetValue(glow.GetComponent<LaunchPedestalGraphic>());

            Assert.That(
                rimWidth,
                Is.Not.EqualTo(glowWidth),
                "属性Rim と ActiveGlow が区別できません。");

            Assert.That(character.sizeDelta.y, Is.GreaterThanOrEqualTo(140f));
        }

        [Test]
        public void TheChevronSitsBelowTheRingAndNeverOverTheCharacter()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();
            BattleScreenController controller = FindOne<BattleScreenController>();

            UiTextCatalog text = (UiTextCatalog)GetField(controller, "text");
            AttributePalette palette = (AttributePalette)GetField(controller, "palette");

            List<string> ids = new List<string>();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                ids.Add("p" + i);
            }

            BattleUnitRingModel ring = new BattleUnitRingModel();
            ring.Build(ids);

            wheel.Bind(ring, palette, text);
            wheel.Refresh();

            RectTransform content = (RectTransform)GetField(wheel, "content");

            for (int i = 0; i < content.childCount; i++)
            {
                spawned.Add(content.GetChild(i).gameObject);
            }

            // 中央項目のキャラクターと、案内が重ならないこと。
            BattleUnitWheelItemView centre = null;

            for (int i = 0; i < content.childCount; i++)
            {
                BattleUnitWheelItemView item =
                    content.GetChild(i).GetComponent<BattleUnitWheelItemView>();

                if (item != null && item.IsCentre)
                {
                    centre = item;
                }
            }

            Assert.That(centre, Is.Not.Null, "中央項目が見つかりません。");

            RectTransform character =
                (RectTransform)FindDeep(centre.transform, "CharacterRoot");

            Rect guideRect = WorldRect(Rect("SlideUpGuide"));

            Assert.That(
                guideRect.Overlaps(WorldRect(character)),
                Is.False,
                "案内がキャラクターへ重なっています。足元へ置きます。");

            Assert.That(
                guideRect.center.y,
                Is.LessThan(WorldRect(character).center.y),
                "案内がキャラクターの頭上に出ています。");

            // 通常時にレールは出しません。
            BattleSlideUpGuideView view = FindOne<BattleSlideUpGuideView>();

            view.HideRail();
            Assert.That(view.IsRailVisible, Is.False);
        }

        [Test]
        public void TheDrawOrderPutsOverlaysInFront()
        {
            RectTransform root = Rect("BattleRoot");

            Dictionary<string, int> index = new Dictionary<string, int>();

            for (int i = 0; i < root.childCount; i++)
            {
                index[root.GetChild(i).name] = i;
            }

            Assert.That(index["CoreGate"], Is.LessThan(index["CpuCombatant"]),
                "CoreGate は Combatant より背面です。");

            Assert.That(index["PlayerCombatant"], Is.LessThan(index["HistoryLane"]));
            Assert.That(index["HistoryLane"], Is.LessThan(index["PlayerWheel"]));
            Assert.That(index["PlayerWheel"], Is.LessThan(index["SlideUpGuide"]));
            Assert.That(index["SlideUpGuide"], Is.LessThan(index["DeployGhostLayer"]));
            Assert.That(index["DeployGhostLayer"], Is.LessThan(index["ResultView"]));

            // 設定パネルは Battle UI のさらに前面（SafeArea 直下で後ろの兄弟）。
            Transform settings = Rect("SettingsRoot");

            Assert.That(
                settings.GetSiblingIndex(),
                Is.GreaterThan(root.GetSiblingIndex()),
                "Settings Panel が Battle UI より前面にありません。");
        }

        // ---------------- ENEMY / PLAYER STAGE ----------------

        /// <summary><paramref name="scope"/>の下だけを探します。同名が両陣営にあるためです。</summary>
        private static RectTransform Under(RectTransform scope, string name)
        {
            Transform found = FindDeep(scope, name);

            Assert.That(found, Is.Not.Null, scope.name + " の下に " + name + " がありません。");

            return (RectTransform)found;
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 0.5f
                && inner.xMax <= outer.xMax + 0.5f
                && inner.yMin >= outer.yMin - 0.5f
                && inner.yMax <= outer.yMax + 0.5f;
        }

        private static readonly string[] HudRows =
        {
            "SideLabel",
            "NameLabel",
            "LevelLabel",
            "AttributeChip",
            "PowerLabel",
            "CoreLabel",
            "SkillNameLabel",
            "SkillDescriptionLabel",
        };

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStageHudStaysInsideItsOwnStage(string stageName)
        {
            RectTransform stage = Rect(stageName);
            RectTransform info = Under(stage, "Info");

            Rect stageRect = WorldRect(stage);

            Assert.That(
                Contains(stageRect, WorldRect(info)),
                Is.True,
                stageName + " の HUD が自分の段からはみ出しています。");

            // 行そのものもはみ出しません。文字は段の外へ流れません。
            for (int i = 0; i < HudRows.Length; i++)
            {
                RectTransform row = Under(info, HudRows[i]);

                Assert.That(
                    Contains(stageRect, WorldRect(row)),
                    Is.True,
                    stageName + " の " + HudRows[i] + " が段の外へ出ています。");
            }
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStageHudSitsBesideTheCharacterNotOverIt(string stageName)
        {
            RectTransform stage = Rect(stageName);
            RectTransform info = Under(stage, "Info");
            RectTransform thumb = Under(stage, "Thumb");

            Rect infoRect = WorldRect(info);
            Rect thumbRect = WorldRect(thumb);

            Assert.That(
                infoRect.Overlaps(thumbRect),
                Is.False,
                stageName + " の文字がキャラクターへ重なっています。");

            // 横に並ぶこと。上下に積むと段が高くなります。
            bool sideBySide =
                infoRect.xMax <= thumbRect.xMin + 0.5f ||
                infoRect.xMin >= thumbRect.xMax - 0.5f;

            Assert.That(
                sideBySide,
                Is.True,
                stageName + " の HUD がキャラクターの横に並んでいません。");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStageHudRowsNeverOverlapEachOther(string stageName)
        {
            // 実際に個体を出してから測ります。ステータス行は実測幅で組み直されるためです。
            ShowUnit(stageName, true);

            RectTransform info = Under(Rect(stageName), "Info");

            for (int i = 0; i < HudRows.Length; i++)
            {
                for (int j = i + 1; j < HudRows.Length; j++)
                {
                    // PowerLabel と CoreLabel は同じステータス行に並びます。
                    // 横に並ぶことは ThePowerAndCoreFormASingleStatRow が確かめます。
                    if (IsSameStatRow(HudRows[i], HudRows[j]))
                    {
                        continue;
                    }

                    RectTransform a = Under(info, HudRows[i]);
                    RectTransform b = Under(info, HudRows[j]);

                    Assert.That(
                        WorldRect(a).Overlaps(WorldRect(b)),
                        Is.False,
                        stageName + " で " + HudRows[i] + " と " + HudRows[j] +
                        " が重なっています。");
                }
            }
        }

        /// <summary>同じステータス行に横並びする組み合わせか。</summary>
        private static bool IsSameStatRow(string a, string b)
        {
            return (a == "PowerLabel" || a == "CoreLabel" || a == "CoreIcon")
                && (b == "PowerLabel" || b == "CoreLabel" || b == "CoreIcon");
        }

        [Test]
        public void TheEnemyHudNeverReachesTheCoreGate()
        {
            RectTransform info = Under(Rect("CpuCombatant"), "Info");
            RectTransform gate = Rect("CoreGate");

            AssertNoOverlap(info, gate, "敵HUDがコアゲートまで垂れ下がっています");

            RectTransform remaining = Rect("RemainingLabel");

            AssertNoOverlap(
                info, remaining, "敵HUDが LEFT n/7 へ重なっています");
        }

        [Test]
        public void BothStagesShowTheSameCompactHud()
        {
            Rect player = WorldRect(Under(Rect("PlayerCombatant"), "Info"));
            Rect cpu = WorldRect(Under(Rect("CpuCombatant"), "Info"));

            Assert.That(
                player.height,
                Is.EqualTo(cpu.height).Within(0.5f),
                "両陣営の HUD の高さが違います。");

            Assert.That(
                player.width,
                Is.EqualTo(cpu.width).Within(0.5f),
                "両陣営の HUD の幅が違います。");

            // 段の高さに対して、HUDが占めるのは一部だけです。大きな矩形にしません。
            // ENEMY 段は画面比に応じて伸びるため、高さが固定の PLAYER 段で測ります。
            float stageHeight = WorldRect(Rect("PlayerCombatant")).height;

            Assert.That(
                player.height / stageHeight,
                Is.LessThanOrEqualTo(0.92f),
                "HUD が段をほぼ埋めています。もっと詰めます。");

            // ENEMY 段は伸びる側なので、いちばん縮んだときでも入ることを見ます。
            Assert.That(
                cpu.height,
                Is.LessThanOrEqualTo(WorldRect(Rect("CpuCombatant")).height),
                "HUD が ENEMY 段からはみ出します。");
        }

        // ---------------- 画面比が変わっても空白が開かないこと ----------------

        private static readonly string[] BottomStack =
        {
            "PlayerWheel",
            "SlideUpGuide",
            "HistoryLane",
            "PlayerCombatant",
            "CoreGate",
            "VsLabel",
        };

        [Test]
        public void TheWholeLowerStackIsAnchoredToTheSameEdge()
        {
            // 失敗の元：PLAYER STAGE だけ上アンカー、履歴とリングが下アンカーでした。
            // 画面が縦長になると、その差が両者のあいだの空白として一気に開きます。
            // 同じ辺へそろえておけば、すき間は画面比に関わらず一定です。
            for (int i = 0; i < BottomStack.Length; i++)
            {
                RectTransform rect = Rect(BottomStack[i]);

                Assert.That(
                    rect.anchorMin.y,
                    Is.EqualTo(0f).Within(0.0001f),
                    BottomStack[i] + " が下アンカーではありません。");

                Assert.That(
                    rect.anchorMax.y,
                    Is.EqualTo(0f).Within(0.0001f),
                    BottomStack[i] + " が下アンカーではありません。");
            }

            // 余りは ENEMY STAGE が引き受けます（上下へ伸びる1枚だけ）。
            RectTransform enemy = Rect("CpuCombatant");

            Assert.That(enemy.anchorMin.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(enemy.anchorMax.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TheGapsAreIndependentOfTheScreenShape()
        {
            // 下アンカーどうしのすき間は、親の高さに一切依存しません。
            // シリアライズ値だけで決まることを、そのまま計算して確かめます。
            float wheelTop = TopOfBottomAnchored(Rect("PlayerWheel"));
            float laneBottom = BottomOfBottomAnchored(Rect("HistoryLane"));
            float laneTop = TopOfBottomAnchored(Rect("HistoryLane"));
            float playerBottom = BottomOfBottomAnchored(Rect("PlayerCombatant"));

            float gapA = playerBottom - laneTop;
            float gapB = laneBottom - wheelTop;

            Assert.That(gapA, Is.GreaterThanOrEqualTo(0f), "PLAYER 段が履歴へ食い込みます。");
            Assert.That(gapB, Is.GreaterThanOrEqualTo(0f), "履歴がリングへ食い込みます。");

            // 参照解像度 1920 でも、縦長の 2118 でも 5% を超えません。
            Assert.That(gapA, Is.LessThan(1890f * 0.05f), "PLAYER 段と履歴のすき間が大きすぎます。");
            Assert.That(gapB, Is.LessThan(1890f * 0.05f), "履歴とリングのすき間が大きすぎます。");
        }

        /// <summary>下アンカー固定の矩形の上辺（親の下端からの距離）。</summary>
        private static float TopOfBottomAnchored(RectTransform rect)
        {
            Assert.That(rect.anchorMin.y, Is.EqualTo(0f).Within(0.0001f), rect.name);
            Assert.That(rect.anchorMax.y, Is.EqualTo(0f).Within(0.0001f), rect.name);

            float height = rect.sizeDelta.y;

            return rect.anchoredPosition.y + (1f - rect.pivot.y) * height;
        }

        /// <summary>下アンカー固定の矩形の下辺（親の下端からの距離）。</summary>
        private static float BottomOfBottomAnchored(RectTransform rect)
        {
            return TopOfBottomAnchored(rect) - rect.sizeDelta.y;
        }

        // ---------------- 二重シェブロン ----------------

        private static readonly string[] ChevronStrokes =
        {
            "UpperLeft",
            "UpperRight",
            "LowerLeft",
            "LowerRight",
        };

        [Test]
        public void TheChevronIsFourRotatedStrokesNotTwoVerticalBars()
        {
            RectTransform chevron = Rect("Chevron");

            Assert.That(
                chevron.childCount,
                Is.EqualTo(4),
                "二重シェブロンは4本のStrokeで作ります。");

            for (int i = 0; i < ChevronStrokes.Length; i++)
            {
                Transform stroke = chevron.Find(ChevronStrokes[i]);

                Assert.That(stroke, Is.Not.Null, ChevronStrokes[i] + " がありません。");

                float z = stroke.localEulerAngles.z;

                if (z > 180f)
                {
                    z -= 360f;
                }

                float magnitude = z < 0f ? -z : z;

                Assert.That(
                    magnitude,
                    Is.InRange(35f, 45f),
                    ChevronStrokes[i] + " の傾きが仕様の範囲外です（縦線に見えます）。");

                bool left = ChevronStrokes[i].EndsWith("Left");

                Assert.That(
                    left ? z < 0f : z > 0f,
                    Is.True,
                    ChevronStrokes[i] + " の傾きが山形（⌃）の向きではありません。");

                // 文字やUnicodeは使いません。短いStrokeだけです。
                Assert.That(
                    stroke.GetComponent<TMP_Text>(),
                    Is.Null,
                    ChevronStrokes[i] + " に文字を使ってはいけません。");

                Image image = stroke.GetComponent<Image>();

                Assert.That(image, Is.Not.Null, ChevronStrokes[i] + " は Image です。");
                Assert.That(
                    image.raycastTarget,
                    Is.False,
                    ChevronStrokes[i] + " が入力を奪っています。");

                // 純白ではなく、控えめなシアン。
                Assert.That(
                    image.color.b,
                    Is.GreaterThan(image.color.r + 0.2f),
                    ChevronStrokes[i] + " がシアンではありません。");
            }

            // 上の山は下の山より上にあります（⌃ が2つ縦に並びます）。
            Assert.That(
                chevron.Find("UpperLeft").GetComponent<RectTransform>().anchoredPosition.y,
                Is.GreaterThan(
                    chevron.Find("LowerLeft").GetComponent<RectTransform>()
                        .anchoredPosition.y),
                "2つの山が縦に並んでいません。");
        }

        [Test]
        public void TheEnergyRailIsHiddenWhileIdle()
        {
            BattleSlideUpGuideView guide = FindOne<BattleSlideUpGuideView>();

            guide.HideRail();

            Assert.That(guide.IsRailVisible, Is.False, "通常時にレールを出しません。");
            Assert.That(Rect("EnergyRail").gameObject.activeSelf, Is.False);

            // レールとシェブロンは別物です。
            Assert.That(
                Rect("EnergyRail").parent,
                Is.Not.EqualTo(Rect("Chevron")),
                "レールをシェブロンの一部にしてはいけません。");
        }

        // ---------------- CORE NODE ----------------

        [Test]
        public void TheCoreNodeIsBigEnoughToRead()
        {
            GameObject slot = Spawn(HistorySlotPath);
            RectTransform rect = (RectTransform)slot.transform;

            // 以前は 104x118。20〜30% 大きくします。
            Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(104f * 1.2f));
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(118f * 1.2f));

            BattleHistorySlotView view = slot.GetComponent<BattleHistorySlotView>();

            TMP_Text order = (TMP_Text)GetField(view, "orderLabel");
            TMP_Text badge = (TMP_Text)GetField(view, "badgeLabel");
            Image badgeImage = (Image)GetField(view, "badgeImage");

            Assert.That(order.fontSize, Is.GreaterThanOrEqualTo(22f));
            Assert.That(badge.fontSize, Is.GreaterThanOrEqualTo(20f));

            // 編成番号と W/L/D は重ねません。
            Assert.That(
                WorldRect(order.rectTransform)
                    .Overlaps(WorldRect(badgeImage.rectTransform)),
                Is.False,
                "編成番号と W/L/D が重なっています。");
        }

        [TestCase(1080f)]
        [TestCase(978f)]
        [TestCase(966f)]
        public void SevenCoreNodesFitTheLaneWidth(float canvasWidth)
        {
            GameObject slot = Spawn(HistorySlotPath);

            float node = ((RectTransform)slot.transform).sizeDelta.x;

            HorizontalLayoutGroup layout =
                Rect("HistoryContent").GetComponent<HorizontalLayoutGroup>();

            Assert.That(layout, Is.Not.Null, "履歴レーンは等間隔に並べます。");

            Assert.That(
                layout.childAlignment,
                Is.EqualTo(TextAnchor.MiddleCenter),
                "7枠は中央そろえにします。");

            float lane = canvasWidth + Rect("HistoryLane").sizeDelta.x;
            float needed = node * 7f + layout.spacing * 6f;

            Assert.That(
                needed,
                Is.LessThanOrEqualTo(lane),
                "幅 " + canvasWidth + " で 7 個の CORE NODE が収まりません。");

            // ノードの高さもレーンに収まること。
            Assert.That(
                ((RectTransform)slot.transform).sizeDelta.y,
                Is.LessThanOrEqualTo(Rect("HistoryLane").sizeDelta.y),
                "CORE NODE がレーンの高さを超えています。");
        }

        // ---------------- Player HUD ----------------

        private static readonly string[][] HudFontFloors =
        {
            // 属性POWERとCOREを最優先にした新しい基準です。
            new[] { "PowerLabel", "46" },
            new[] { "CoreLabel", "42" },
            new[] { "NameLabel", "42" },
            new[] { "SkillNameLabel", "24" },
            new[] { "SkillDescriptionLabel", "16" },
            new[] { "LevelLabel", "15" },
        };

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheHudTextIsLargeEnoughToReadOnADevice(string stageName)
        {
            RectTransform info = Under(Rect(stageName), "Info");

            for (int i = 0; i < HudFontFloors.Length; i++)
            {
                string field = HudFontFloors[i][0];
                float floor = float.Parse(HudFontFloors[i][1]);

                TMP_Text label = Under(info, field).GetComponent<TMP_Text>();

                Assert.That(label, Is.Not.Null, field + " が TMP_Text ではありません。");

                Assert.That(
                    label.fontSize,
                    Is.GreaterThanOrEqualTo(floor),
                    stageName + " の " + field + " が小さすぎます。");

                // autosize 任せにすると、長い文で勝手に潰れます。
                Assert.That(
                    label.enableAutoSizing,
                    Is.False,
                    stageName + " の " + field + " が autosize 任せです。");

                Assert.That(
                    label.fontSizeMin,
                    Is.GreaterThanOrEqualTo(floor * 0.6f),
                    stageName + " の " + field + " の最小値が低すぎます。");
            }

            // 属性はチップの中の文字です。
            // 正式な文字サイズ仕様は 18〜22（TheStatRowTextIsLargeEnoughToDecideWith と同じ）。
            // 属性POWERとCOREを最優先にしたぶん、ここは控えめにしています。
            TMP_Text attribute =
                Under(info, "AttributeLabel").GetComponent<TMP_Text>();

            Assert.That(
                attribute.fontSize,
                Is.InRange(18f, 22f),
                stageName + " の 属性名 が仕様（18〜22）から外れています。");

            Assert.That(
                attribute.enableAutoSizing,
                Is.False,
                stageName + " の 属性名 が autosize 任せです。");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheHudSurvivesALongSkillNameAndDescription(string stageName)
        {
            RectTransform info = Under(Rect(stageName), "Info");

            TMP_Text skillName = Under(info, "SkillNameLabel").GetComponent<TMP_Text>();
            TMP_Text skillText =
                Under(info, "SkillDescriptionLabel").GetComponent<TMP_Text>();

            // 画面は英語UIです。日本語を流すと LiberationSans SDF に字が無く、
            // missing character 警告が大量に出ます。フォントの問題ではなく
            // テストデータの問題なので、同等以上に長い ASCII へ置き換えます。
            // 全角は半角のおよそ2倍の幅なので、文字数も2倍以上を取ります。
            skillName.text = "OVERLOAD CASCADE BREAKER EXTREME ANNIHILATION MODE";

            skillText.text =
                "Tears deeply into the opposing CORE, then restores your own POWER " +
                "at the start of the next round. If the attribute matches as well, " +
                "it shreds an additional chunk of the enemy CORE before the clash ends.";

            // 以前の日本語データと同じだけレイアウトへ負荷を掛けていることを確かめます。
            Assert.That(
                skillName.text.Length,
                Is.GreaterThanOrEqualTo(50),
                "スキル名が短すぎて長文の検証になりません。");

            Assert.That(
                skillText.text.Length,
                Is.GreaterThanOrEqualTo(122),
                "説明文が短すぎて長文の検証になりません。");

            Assert.That(
                IsAscii(skillName.text) && IsAscii(skillText.text),
                Is.True,
                "英語UIのフォントに無い文字を流してはいけません。");

            Canvas.ForceUpdateCanvases();

            Rect stageRect = WorldRect(Rect(stageName));

            Assert.That(
                Contains(stageRect, WorldRect(skillName.rectTransform)),
                Is.True,
                stageName + " で長いスキル名が段からはみ出します。");

            Assert.That(
                Contains(stageRect, WorldRect(skillText.rectTransform)),
                Is.True,
                stageName + " で長い説明文が段からはみ出します。");

            Assert.That(
                WorldRect(skillText.rectTransform)
                    .Overlaps(WorldRect(Under(Rect(stageName), "Thumb"))),
                Is.False,
                stageName + " で説明文がキャラクターへ重なります。");
        }

        [Test]
        public void ThePlayerHudNeverEntersTheCoreGate()
        {
            AssertNoOverlap(
                Under(Rect("PlayerCombatant"), "Info"),
                Rect("CoreGate"),
                "PLAYER HUD がコアゲートへ入り込んでいます");
        }

        // ---------------- Reveal 前の ENEMY STAGE ----------------

        [Test]
        public void TheReadyBadgeSitsInTheMiddleOfTheEnemyStage()
        {
            RectTransform stage = Rect("CpuCombatant");
            RectTransform hidden = Under(stage, "Hidden");
            RectTransform label = Under(hidden, "HiddenLabel");

            Rect stageRect = WorldRect(stage);
            Rect labelRect = WorldRect(label);

            Assert.That(
                labelRect.center.x,
                Is.EqualTo(stageRect.center.x).Within(stageRect.width * 0.05f),
                "READY が左へ寄っています。段の中央へ置きます。");

            Assert.That(
                labelRect.center.y,
                Is.EqualTo(stageRect.center.y).Within(stageRect.height * 0.08f),
                "READY が段の中央にありません。");

            // 背後は小さな六角輪郭。大きな暗い四角ではありません。
            RectTransform socket = Under(hidden, "HiddenBg");

            Assert.That(
                socket.GetComponent<LaunchPedestalGraphic>(),
                Is.Not.Null,
                "READY の背後は六角ソケットにします。");

            Assert.That(
                socket.GetComponent<Image>(),
                Is.Null,
                "READY の背後に長方形の Image を置いてはいけません。");

            Assert.That(
                WorldRect(socket).width / stageRect.width,
                Is.LessThan(0.4f),
                "READY の背後が大きすぎます。");

            Assert.That(
                WorldRect(socket).height / stageRect.height,
                Is.LessThan(0.6f),
                "READY の背後が大きすぎます。");
        }

        [Test]
        public void NothingAboutTheEnemyLeaksBeforeTheReveal()
        {
            BattleCombatantView cpu = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BattleCombatantView[] found =
                    root.GetComponentsInChildren<BattleCombatantView>(true);

                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i].name == "CpuCombatant")
                    {
                        cpu = found[i];
                    }
                }
            }

            Assert.That(cpu, Is.Not.Null);

            cpu.ShowHidden();

            GameObject hidden = (GameObject)GetField(cpu, "hiddenRoot");
            GameObject info = (GameObject)GetField(cpu, "infoRoot");

            Assert.That(hidden.activeSelf, Is.True, "READY を出します。");
            Assert.That(info.activeSelf, Is.False, "Reveal前に情報を出してはいけません。");
            Assert.That(cpu.IsRevealed, Is.False);
            Assert.That(cpu.Current, Is.Null, "Reveal前に個体を持ってはいけません。");

            // 段の高さは Reveal の前後で変わりません。
            float before = WorldRect(Rect("CpuCombatant")).height;

            cpu.ShowEmpty();
            Canvas.ForceUpdateCanvases();

            Assert.That(
                WorldRect(Rect("CpuCombatant")).height,
                Is.EqualTo(before).Within(0.01f),
                "Reveal前後で段の高さが跳ねています。");

            // 床線は Reveal 前でも残ります。
            Assert.That(Under(Rect("CpuCombatant"), "Edge").gameObject.activeSelf, Is.True);
        }

        /// <summary>英語UIのフォントで確実に出せる文字だけか。</summary>
        private static bool IsAscii(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 0x7E)
                {
                    return false;
                }
            }

            return true;
        }

        // ---------------- ステータス行（色別POWER と CORE） ----------------

        /// <summary>
        /// ステータス行は実測幅で組み直されます。
        /// 検査も、実際に個体を出したあとの姿で行います。
        /// </summary>
        private BattleCombatantView ShowUnit(string stageName, bool dual)
        {
            BattleCombatantView view = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BattleCombatantView[] found =
                    root.GetComponentsInChildren<BattleCombatantView>(true);

                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i].name == stageName)
                    {
                        view = found[i];
                    }
                }
            }

            Assert.That(view, Is.Not.Null, stageName + " が見つかりません。");

            BattleScreenController controller = FindOne<BattleScreenController>();

            AttributePalette palette = (AttributePalette)GetField(controller, "palette");
            UiTextCatalog text = (UiTextCatalog)GetField(controller, "text");
            BattleTextCatalog battleText =
                (BattleTextCatalog)GetField(controller, "battleText");

            view.Bind(palette, text, battleText);

            CoreBeastDefinition definition = dual
                ? AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(
                    "Assets/CoreBeasts/Data/Testing/CB_Volx_RedBlue_TEST.asset")
                : AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(
                    "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset");

            Assert.That(definition, Is.Not.Null, "ユニット定義が読めません。");

            BattleUnitCard.TryCreate("u0", 18, definition, out BattleUnitCard card);

            view.Show(card);
            Canvas.ForceUpdateCanvases();

            return view;
        }

        [TestCase("PlayerCombatant", false)]
        [TestCase("PlayerCombatant", true)]
        [TestCase("CpuCombatant", false)]
        [TestCase("CpuCombatant", true)]
        public void ThePowerAndCoreFormASingleStatRow(string stageName, bool dual)
        {
            ShowUnit(stageName, dual);

            RectTransform info = Under(Rect(stageName), "Info");

            RectTransform power = Under(info, "PowerLabel");
            RectTransform core = Under(info, "CoreLabel");
            RectTransform icon = Under(info, "CoreIcon");

            Rect powerRect = WorldRect(power);
            Rect coreRect = WorldRect(core);
            Rect iconRect = WorldRect(icon);

            float scale = power.lossyScale.x;

            Assume.That(scale, Is.GreaterThan(0f));

            // 同じ行、縦中央がそろっていること。
            Assert.That(
                coreRect.center.y,
                Is.EqualTo(powerRect.center.y).Within(2f),
                stageName + " の POWER と CORE が同じ行にありません。");

            Assert.That(
                iconRect.center.y,
                Is.EqualTo(powerRect.center.y).Within(2f),
                stageName + " の COREアイコンが縦中央にありません。");

            // 左から POWER → アイコン → CORE値。
            float powerToIcon = (iconRect.xMin - powerRect.xMax) / scale;
            float iconToCore = (coreRect.xMin - iconRect.xMax) / scale;

            Assert.That(
                powerToIcon,
                Is.GreaterThanOrEqualTo(28f).And.LessThanOrEqualTo(34f),
                stageName + " の POWER群 と CORE群 の間隔が 28〜34u ではありません: " +
                powerToIcon.ToString("F1"));

            Assert.That(
                iconToCore,
                Is.GreaterThanOrEqualTo(7f).And.LessThanOrEqualTo(10f),
                stageName + " の アイコンとCORE値 の間隔が 7〜10u ではありません: " +
                iconToCore.ToString("F1"));

            // COREアイコンの大きさも固定します。
            Assert.That(
                iconRect.height / scale,
                Is.GreaterThanOrEqualTo(32f).And.LessThanOrEqualTo(36f),
                stageName + " の COREアイコンが 32〜36u ではありません: " +
                (iconRect.height / scale).ToString("F1"));

            // 左右へ離しすぎないこと（1つの行としてまとまる）。
            float rowWidth = (coreRect.xMax - powerRect.xMin) / scale;

            Assert.That(
                rowWidth,
                Is.LessThan(WorldRect(info).width / scale * 0.75f),
                stageName + " の ステータス行が左右へ広がりすぎです。");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStatRowTextIsLargeEnoughToDecideWith(string stageName)
        {
            RectTransform info = Under(Rect(stageName), "Info");

            TMP_Text power = Under(info, "PowerLabel").GetComponent<TMP_Text>();
            TMP_Text core = Under(info, "CoreLabel").GetComponent<TMP_Text>();
            TMP_Text skillName = Under(info, "SkillNameLabel").GetComponent<TMP_Text>();
            TMP_Text skillText =
                Under(info, "SkillDescriptionLabel").GetComponent<TMP_Text>();
            TMP_Text name = Under(info, "NameLabel").GetComponent<TMP_Text>();
            TMP_Text side = Under(info, "SideLabel").GetComponent<TMP_Text>();

            TMP_Text attribute =
                Under(info, "AttributeLabel").GetComponent<TMP_Text>();

            TMP_Text level = Under(info, "LevelLabel").GetComponent<TMP_Text>();

            // 属性POWER と CORE を最優先にした大きさであること。
            Assert.That(power.fontSize, Is.InRange(46f, 52f), "属性POWER は 46〜52。");
            Assert.That(core.fontSize, Is.InRange(42f, 48f), "CORE は 42〜48。");
            Assert.That(name.fontSize, Is.InRange(42f, 46f), "ユニット名 は 42〜46。");
            Assert.That(attribute.fontSize, Is.InRange(18f, 22f), "属性名 は 18〜22。");
            Assert.That(skillName.fontSize, Is.InRange(24f, 28f), "スキル名 は 24〜28。");
            Assert.That(skillText.fontSize, Is.InRange(16f, 20f), "スキル説明 は 16〜20。");
            Assert.That(side.fontSize, Is.InRange(13f, 15f), "PLAYER / CPU は 13〜15。");
            Assert.That(level.fontSize, Is.InRange(15f, 17f), "レベル は 15〜17。");

            // 旧基準（40）より必ず大きいこと。縮めて収めるのは禁止です。
            Assert.That(
                power.fontSize,
                Is.GreaterThan(40f),
                "属性POWER が旧基準 40 以下へ縮んでいます。");

            // 視認順：属性POWER・CORE > ユニット名 > 属性 > スキル名 > スキル説明 > 補助
            Assert.That(
                power.fontSize,
                Is.GreaterThanOrEqualTo(core.fontSize),
                "属性POWER が CORE より小さくなっています。");

            Assert.That(
                power.fontSize,
                Is.GreaterThanOrEqualTo(name.fontSize),
                "属性POWER がユニット名より小さくなっています。");

            Assert.That(
                name.fontSize,
                Is.GreaterThan(attribute.fontSize),
                "ユニット名が属性より小さくなっています。");

            Assert.That(
                attribute.fontSize,
                Is.LessThan(skillName.fontSize),
                "属性がスキル名より大きくなっています。");

            Assert.That(
                skillName.fontSize,
                Is.GreaterThan(skillText.fontSize),
                "スキル名がスキル説明より小さくなっています。");

            Assert.That(
                skillText.fontSize,
                Is.GreaterThan(level.fontSize),
                "スキル説明が補助情報より小さくなっています。");

            Assert.That(
                power.fontSize,
                Is.GreaterThan(skillText.fontSize * 2f),
                "属性POWER がスキル説明と同程度の存在感しかありません。");

            Assert.That(power.enableAutoSizing, Is.False);
            Assert.That(core.enableAutoSizing, Is.False);
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheAttributeLabelNeverRepeatsTheSymbolAndTheName(string stageName)
        {
            ShowUnit(stageName, true);

            TMP_Text attribute =
                Under(Under(Rect(stageName), "Info"), "AttributeLabel")
                    .GetComponent<TMP_Text>();

            // "R/B RED / BLUE" のような重複を出しません。
            Assert.That(
                attribute.text,
                Is.EqualTo("RED / BLUE"),
                "属性表示に記号と名前の重複があります: " + attribute.text);

            Assert.That(attribute.text, Does.Not.Contain("R/B"));
        }

        [TestCase("PlayerCombatant", false)]
        [TestCase("PlayerCombatant", true)]
        [TestCase("CpuCombatant", false)]
        [TestCase("CpuCombatant", true)]
        public void TheStatRowStaysInsideTheInfoRect(string stageName, bool dual)
        {
            ShowUnit(stageName, dual);

            RectTransform info = Under(Rect(stageName), "Info");

            RectTransform power = Under(info, "PowerLabel");
            RectTransform core = Under(info, "CoreLabel");
            RectTransform icon = Under(info, "CoreIcon");

            Rect area = WorldRect(info);
            float scale = power.lossyScale.x;

            Assume.That(scale, Is.GreaterThan(0f));

            float padding = 8f * scale;

            // 先頭の「40 /」が親の外へ出ると、画面端で切れて読めなくなります。
            Assert.That(
                WorldRect(power).xMin,
                Is.GreaterThanOrEqualTo(area.xMin + padding - 0.5f),
                stageName + (dual ? "(2色)" : "(単色)") +
                " の 属性POWER が Info の左端より外にあります。");

            Assert.That(
                WorldRect(core).xMax,
                Is.LessThanOrEqualTo(area.xMax - padding + 0.5f),
                stageName + (dual ? "(2色)" : "(単色)") +
                " の CORE が Info の右端より外にあります。");

            Assert.That(
                WorldRect(icon).xMin,
                Is.GreaterThanOrEqualTo(area.xMin - 0.5f));

            // 立ち絵と重ならないこと。
            Rect thumb = WorldRect(Under(Rect(stageName), "Thumb"));

            Assert.That(WorldRect(power).Overlaps(thumb), Is.False,
                stageName + " の 属性POWER がキャラクターへ重なっています。");
            Assert.That(WorldRect(core).Overlaps(thumb), Is.False,
                stageName + " の CORE がキャラクターへ重なっています。");
            Assert.That(WorldRect(icon).Overlaps(thumb), Is.False,
                stageName + " の COREアイコンがキャラクターへ重なっています。");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheDualPowerTextIsNeverTruncated(string stageName)
        {
            ShowUnit(stageName, true);

            TMP_Text power =
                Under(Under(Rect(stageName), "Info"), "PowerLabel").GetComponent<TMP_Text>();

            string plain = System.Text.RegularExpressions.Regex.Replace(
                power.text, "<.*?>", string.Empty);

            // 2色ユニット（Red 40 / Blue 21）の全文字がそろっていること。
            Assert.That(
                plain,
                Is.EqualTo("40 / 21"),
                stageName + " の 2色POWER が欠けています: " + plain);

            Assert.That(power.textInfo.lineCount, Is.EqualTo(1), "1行であること。");

            // 実際の描画範囲が、矩形の中に収まっていること（切れていない）。
            power.ForceMeshUpdate();

            Assert.That(
                power.preferredWidth,
                Is.LessThanOrEqualTo(power.rectTransform.rect.width + 0.5f),
                stageName + " の 2色POWER が矩形からあふれています。");

            Assert.That(
                power.isTextOverflowing,
                Is.False,
                stageName + " の 2色POWER があふれています。");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void EveryHudRowFitsInsideTheInfoArea(string stageName)
        {
            ShowUnit(stageName, true);

            RectTransform info = Under(Rect(stageName), "Info");

            Rect area = WorldRect(info);

            string[] rows =
            {
                "SideLabel", "NameLabel", "LevelLabel", "AttributeChip",
                "PowerLabel", "CoreIcon", "CoreLabel",
                "SkillNameLabel", "SkillDescriptionLabel",
            };

            for (int i = 0; i < rows.Length; i++)
            {
                Rect row = WorldRect(Under(info, rows[i]));

                Assert.That(
                    row.yMin,
                    Is.GreaterThanOrEqualTo(area.yMin - 0.5f),
                    stageName + " の " + rows[i] + " が Info の下へはみ出しています。");

                Assert.That(
                    row.yMax,
                    Is.LessThanOrEqualTo(area.yMax + 0.5f),
                    stageName + " の " + rows[i] + " が Info の上へはみ出しています。");
            }

            // 行どうしが重ならないこと（ステータス行の3つは同じ行なので除く）。
            string[] stacked =
            {
                "SideLabel", "NameLabel", "LevelLabel", "AttributeChip",
                "PowerLabel", "SkillNameLabel", "SkillDescriptionLabel",
            };

            for (int i = 0; i < stacked.Length; i++)
            {
                for (int j = i + 1; j < stacked.Length; j++)
                {
                    Assert.That(
                        WorldRect(Under(info, stacked[i]))
                            .Overlaps(WorldRect(Under(info, stacked[j]))),
                        Is.False,
                        stageName + " の " + stacked[i] + " と " + stacked[j] +
                        " が重なっています。");
                }
            }
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStatRowNeverOverlapsTheCharacterOrLeavesTheStage(string stageName)
        {
            RectTransform stage = Rect(stageName);
            RectTransform info = Under(stage, "Info");

            Rect stageRect = WorldRect(stage);

            string[] parts = { "PowerLabel", "CoreLabel", "CoreIcon" };

            for (int i = 0; i < parts.Length; i++)
            {
                RectTransform part = Under(info, parts[i]);

                Assert.That(
                    Contains(stageRect, WorldRect(part)),
                    Is.True,
                    stageName + " の " + parts[i] + " が段の外へ出ています。");

                Assert.That(
                    WorldRect(part).Overlaps(WorldRect(Under(stage, "Thumb"))),
                    Is.False,
                    stageName + " の " + parts[i] + " がキャラクターへ重なっています。");
            }
        }

        [Test]
        public void TheStatRowNeverReachesTheCoreGate()
        {
            RectTransform info = Under(Rect("PlayerCombatant"), "Info");

            AssertNoOverlap(
                Under(info, "CoreLabel"), Rect("CoreGate"),
                "ステータス行がコアゲートへ届いています");

            AssertNoOverlap(
                Under(info, "PowerLabel"), Rect("CoreGate"),
                "ステータス行がコアゲートへ届いています");
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void TheStatRowFitsOnOneLineForTheLongestValues(string stageName)
        {
            BattleCombatantView view = ShowUnit(stageName, true);

            RectTransform info = Under(Rect(stageName), "Info");

            TMP_Text power = Under(info, "PowerLabel").GetComponent<TMP_Text>();
            TMP_Text core = Under(info, "CoreLabel").GetComponent<TMP_Text>();

            string powerBefore = power.text;
            string coreBefore = core.text;

            try
            {
                // いちばん長い形（3桁 / 3桁 と 3桁のCORE）でも折り返さないこと。
                power.text = "999 / 999";
                core.text = "999";

                // 文字を変えたら、製品と同じ手順で行を組み直します。
                // 組み直さずに測ると、シーンの既定幅を見ているだけになります。
                view.RefreshStatRow();
                Canvas.ForceUpdateCanvases();

                Assert.That(
                    power.textInfo.lineCount,
                    Is.EqualTo(1),
                    stageName + " の POWER が折り返しています。");

                Assert.That(
                    core.textInfo.lineCount,
                    Is.EqualTo(1),
                    stageName + " の CORE が折り返しています。");

                Assert.That(
                    power.GetPreferredValues(power.text, Mathf.Infinity, 0f).x,
                    Is.LessThanOrEqualTo(power.rectTransform.rect.width + 0.5f),
                    stageName + " の POWER が枠を超えます。");

                Assert.That(
                    core.GetPreferredValues(core.text, Mathf.Infinity, 0f).x,
                    Is.LessThanOrEqualTo(core.rectTransform.rect.width + 0.5f),
                    stageName + " の CORE が枠を超えます。");
            }
            finally
            {
                power.text = powerBefore;
                core.text = coreBefore;
                view.RefreshStatRow();
            }
        }

        [Test]
        public void TheCoreIconIsAGraphicNotALetterAndTakesNoInput()
        {
            RectTransform icon = Under(Under(Rect("PlayerCombatant"), "Info"), "CoreIcon");

            Assert.That(
                icon.GetComponent<TMP_Text>(),
                Is.Null,
                "COREアイコンに文字を使ってはいけません（フォント警告の元です）。");

            Graphic graphic = icon.GetComponent<Graphic>();

            Assert.That(graphic, Is.Not.Null, "COREアイコンは Graphic で描きます。");
            Assert.That(
                graphic.raycastTarget,
                Is.False,
                "COREアイコンが入力を奪っています。");
        }

        [Test]
        public void ThereIsNoPowerBarLeftAnywhereInTheBattleHud()
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Image[] images = root.GetComponentsInChildren<Image>(true);

                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].type != Image.Type.Filled)
                    {
                        continue;
                    }

                    Assert.That(
                        images[i].name,
                        Does.Not.Contain("Power"),
                        "POWERバーの残骸があります: " + images[i].name);
                }
            }
        }

        // ---------------- 画面端の切れ（実測） ----------------

        /// <summary>SafeArea の矩形。</summary>
        private Rect SafeArea()
        {
            return WorldRect(Rect("SafeArea"));
        }

        [TestCase("PlayerCombatant", false)]
        [TestCase("PlayerCombatant", true)]
        [TestCase("CpuCombatant", false)]
        [TestCase("CpuCombatant", true)]
        public void TheWholeHudStaysInsideTheStageAndSafeArea(string stageName, bool dual)
        {
            ShowUnit(stageName, dual);

            RectTransform stage = Rect(stageName);
            RectTransform info = Under(stage, "Info");

            float scale = info.lossyScale.x;

            Assume.That(scale, Is.GreaterThan(0f));

            Rect stageRect = WorldRect(stage);
            Rect hud = WorldRect(info);
            Rect safe = SafeArea();

            float margin = 24f * scale;

            // HUD 全体が Stage の内側で、左右に 24u 以上の余白を持つこと。
            Assert.That(
                hud.xMin - stageRect.xMin,
                Is.GreaterThanOrEqualTo(margin - 0.5f),
                stageName + (dual ? "(2色)" : "(単色)") + " の HUD 左余白が足りません。");

            Assert.That(
                stageRect.xMax - hud.xMax,
                Is.GreaterThanOrEqualTo(margin - 0.5f),
                stageName + (dual ? "(2色)" : "(単色)") + " の HUD 右余白が足りません。");

            // SafeArea からもはみ出さないこと。
            Assert.That(hud.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 0.5f));
            Assert.That(hud.xMax, Is.LessThanOrEqualTo(safe.xMax + 0.5f));
            Assert.That(hud.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f));
            Assert.That(hud.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.5f));
        }

        [TestCase("PlayerCombatant")]
        [TestCase("CpuCombatant")]
        public void ALongUnitNameStillFitsInsideTheStage(string stageName)
        {
            ShowUnit(stageName, true);

            RectTransform stage = Rect(stageName);
            RectTransform info = Under(stage, "Info");

            TMP_Text name = Under(info, "NameLabel").GetComponent<TMP_Text>();

            string before = name.text;

            try
            {
                name.text = "OVERLOADED CASCADE ANNIHILATOR PRIME";
                name.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();

                Assert.That(
                    WorldRect(name.rectTransform).xMax,
                    Is.LessThanOrEqualTo(WorldRect(stage).xMax + 0.5f),
                    stageName + " で長い名前が段からはみ出します。");

                Assert.That(
                    WorldRect(name.rectTransform).xMin,
                    Is.GreaterThanOrEqualTo(WorldRect(stage).xMin - 0.5f));
            }
            finally
            {
                name.text = before;
                name.ForceMeshUpdate();
            }
        }

        [Test]
        public void TheSevenHistoryNodesStayInsideTheSafeArea()
        {
            RectTransform lane = Rect("HistoryContent");

            HorizontalLayoutGroup layout = lane.GetComponent<HorizontalLayoutGroup>();

            Assert.That(layout, Is.Not.Null);

            GameObject slot = Spawn(HistorySlotPath);

            float node = ((RectTransform)slot.transform).sizeDelta.x;
            float need = node * 7f + layout.spacing * 6f;
            float available = Rect("HistoryLane").rect.width;

            Assert.That(
                need,
                Is.LessThanOrEqualTo(available),
                "7ノードがレーンに収まりません。");

            // 左右に均等な余白が残ること。
            Assert.That(
                (available - need) * 0.5f,
                Is.GreaterThanOrEqualTo(20f),
                "履歴ノードの左右余白が足りません（端で切れます）。");
        }

        [Test]
        public void TheOutermostRingItemsStayInsideTheSafeArea()
        {
            BattleUnitWheelView wheel = FindOne<BattleUnitWheelView>();

            float spacing = wheel.ItemSpacingPoints;
            float toUnits = wheel.PointsToUnits;

            // いちばん外の項目（中央からの距離2）の端を求めます。
            float offset = BattleRingLayout.HorizontalOffset(2f, spacing) * toUnits;

            GameObject item = Spawn(WheelItemPath);

            float width = ((RectTransform)item.transform).sizeDelta.x
                * BattleRingLayout.OuterScale;

            float edge = offset + width * 0.5f;
            float half = Rect("SafeArea").rect.width * 0.5f;

            Assert.That(
                edge,
                Is.LessThanOrEqualTo(half - 24f),
                "外周ユニットが SafeArea 端で切れます（端 " + edge.ToString("F1") +
                " / 半幅 " + half.ToString("F1") + "）。");

            // 中央のほうが大きいままであること。
            Assert.That(
                BattleRingLayout.CenterScale,
                Is.GreaterThan(BattleRingLayout.OuterScale));
        }

        // ---------------- Outcome Banner ----------------

        private static readonly string[] BannerRows =
        {
            "DecisionLabel", "WinnerLabel", "MatchupLabel", "BannerScoreLabel",
        };

        [Test]
        public void TheOutcomeBannerRowsNeverOverlapEachOther()
        {
            RectTransform banner = Rect("Banner");

            for (int i = 0; i < BannerRows.Length; i++)
            {
                for (int j = i + 1; j < BannerRows.Length; j++)
                {
                    Assert.That(
                        WorldRect(Under(banner, BannerRows[i]))
                            .Overlaps(WorldRect(Under(banner, BannerRows[j]))),
                        Is.False,
                        BannerRows[i] + " と " + BannerRows[j] + " が重なっています。");
                }

                // 行が枠から出ないこと。
                Assert.That(
                    Contains(WorldRect(banner), WorldRect(Under(banner, BannerRows[i]))),
                    Is.True,
                    BannerRows[i] + " が Banner の外へ出ています。");

                // 溢れて下の行へ食い込まないよう、切り詰めます。
                TMP_Text label = Under(banner, BannerRows[i]).GetComponent<TMP_Text>();

                Assert.That(
                    label.overflowMode,
                    Is.Not.EqualTo(TextOverflowModes.Overflow),
                    BannerRows[i] + " が溢れたまま下の行へ重なります。");

                Assert.That(label.enableAutoSizing, Is.False);
            }
        }

        [Test]
        public void TheOutcomeBannerNeverCoversEitherHud()
        {
            RectTransform banner = Rect("Banner");

            Rect area = WorldRect(banner);

            string[] stages = { "PlayerCombatant", "CpuCombatant" };

            for (int i = 0; i < stages.Length; i++)
            {
                RectTransform info = Under(Rect(stages[i]), "Info");

                Assert.That(
                    area.Overlaps(WorldRect(info)),
                    Is.False,
                    stages[i] + " の HUD が結果バナーに隠れます。");
            }
        }

        [Test]
        public void TheOutcomeBannerKeepsAllTheResultInformation()
        {
            RectTransform banner = Rect("Banner");

            // 勝敗情報そのものは消しません。
            for (int i = 0; i < BannerRows.Length; i++)
            {
                Assert.That(
                    Under(banner, BannerRows[i]),
                    Is.Not.Null,
                    BannerRows[i] + " を削ってはいけません。");
            }

            BattleResultView view = FindOne<BattleResultView>();

            Assert.That(GetField(view, "decisionLabel"), Is.Not.Null);
            Assert.That(GetField(view, "winnerLabel"), Is.Not.Null);
            Assert.That(GetField(view, "matchupLabel"), Is.Not.Null);
            Assert.That(GetField(view, "bannerScoreLabel"), Is.Not.Null);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }
    }
}
