using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Shared.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 3画面に共通する船内背景。
    ///
    /// 同じ宇宙船に見えること、既存の操作と表示を邪魔しないことを
    /// 実際のSceneから読んで確かめます。
    /// </summary>
    public sealed class ShipBackdropTests
    {
        private const string Title = "Assets/CoreBeasts/Scenes/Boot.unity";
        private const string UnitSet = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const string Battle = "Assets/CoreBeasts/Scenes/Battle.unity";
        private const string Home = "Assets/CoreBeasts/Scenes/Home.unity";

        private Scene scene;

        [TearDown]
        public void TearDown()
        {
            // 単独で開いているときは閉じられません（唯一のシーンは閉じられない）。
            // 次の SetUp が Single で開き直すので、ここでは残しておきます。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void Open(string path)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, path + " を開けません。");
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

        private List<T> FindAll<T>() where T : Component
        {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found;
        }

        private Transform Find(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = Deep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform Deep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = Deep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Rect World(RectTransform rect)
        {
            Vector3[] c = new Vector3[4];
            rect.GetWorldCorners(c);

            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }

        // ---------------- 3画面に共通の要素がある ----------------

        [TestCase(Title)]
        [TestCase(Home)]
        [TestCase(UnitSet)]
        [TestCase(Battle)]
        public void EveryScreenSharesTheSameShipBackdrop(string path)
        {
            Open(path);

            Assert.That(Find("ShipBackdrop"), Is.Not.Null, path + " に背景がありません。");

            // 同じ船に見えるよう、共通の層をどの画面も持ちます。
            Assert.That(FindAll<ShipVoidGraphic>().Count, Is.EqualTo(1), "Base Void");
            Assert.That(FindAll<DimensionalGridGraphic>().Count, Is.EqualTo(1), "Grid");
            Assert.That(FindAll<HullPanelGraphic>().Count, Is.GreaterThanOrEqualTo(2), "Hull");
            Assert.That(
                FindAll<EnergyConduitGraphic>().Count,
                Is.GreaterThanOrEqualTo(2),
                "Energy Conduit");

            // Home は封印リングとチャンバーの2枚を重ねます。
            // 「画面ごとの Chamber がある」ことが要点なので、枚数は1以上で見ます。
            // どの名前の Chamber があるかは EveryScreenHasItsOwnChamber が厳密に固定します。
            Assert.That(
                FindAll<ChamberFrameGraphic>().Count,
                Is.GreaterThanOrEqualTo(1),
                "Chamber");
            Assert.That(FindAll<ShipBackdropView>().Count, Is.EqualTo(1), "まとめ役");
        }

        [TestCase(Title, "DormantCoreChamber")]
        [TestCase(Home, "DormantCoreChamber")]
        [TestCase(Home, "SealRingOuter")]
        [TestCase(UnitSet, "HangarBayFrame")]
        [TestCase(Battle, "CombatChamberFrame")]
        public void EveryScreenHasItsOwnChamber(string path, string chamber)
        {
            Open(path);

            Transform found = Find(chamber);

            Assert.That(found, Is.Not.Null, path + " に " + chamber + " がありません。");

            // 名前だけの空オブジェクトを置かないこと。
            Assert.That(
                found.GetComponent<ChamberFrameGraphic>(),
                Is.Not.Null,
                chamber + " が実装を持っていません。");
        }

        [Test]
        public void TheTitleChamberIsDimmerThanTheOtherScreens()
        {
            Open(Title);
            float title = Find("DormantCoreChamber").GetComponent<ChamberFrameGraphic>().Glow;

            Open(Battle);
            float battle = Find("CombatChamberFrame").GetComponent<ChamberFrameGraphic>().Glow;

            Assert.That(
                title,
                Is.LessThan(battle),
                "タイトルは起動前なので、他画面より発光を弱くします。");
        }

        [Test]
        public void TheHomeScreenIsTheRealTitleAndKeepsItsButtons()
        {
            // 実際にユーザーが見るタイトル画面は Boot ではなく Home です。
            // Boot は起動・遷移用なので、Boot だけを完成扱いにしません。
            Open(Home);

            Assert.That(Find("ShipBackdrop"), Is.Not.Null, "Home に背景がありません。");
            Assert.That(Find("HomeTitleText"), Is.Not.Null, "HOME 見出しが必要です。");

            // ボタンは残し、タップ領域も変えません。
            Transform battle = Find("BattleButton");
            Transform unitSet = Find("UnitSetButton");

            Assert.That(battle, Is.Not.Null, "BATTLE ボタンが必要です。");
            Assert.That(unitSet, Is.Not.Null, "UNIT SET ボタンが必要です。");

            Assert.That(
                battle.GetComponent<Button>(),
                Is.Not.Null,
                "BATTLE が押せなくなっています。");

            Assert.That(
                unitSet.GetComponent<Button>(),
                Is.Not.Null,
                "UNIT SET が押せなくなっています。");

            // 背景はボタンより奥です。
            int backdrop = Find("ShipBackdrop").GetSiblingIndex();

            Assert.That(battle.GetSiblingIndex(), Is.GreaterThan(backdrop));
            Assert.That(unitSet.GetSiblingIndex(), Is.GreaterThan(backdrop));
            Assert.That(Find("HomeTitleText").GetSiblingIndex(), Is.GreaterThan(backdrop));
        }

        [Test]
        public void TheBootSceneIsOnlyAnEntryPointNotTheTitle()
        {
            // Boot にも背景は入れてありますが、遷移用の起動シーンです。
            Open(Title);

            Assert.That(Find("StartButton"), Is.Not.Null, "Boot は起動用です。");
            Assert.That(
                Find("BattleButton"),
                Is.Null,
                "Boot はタイトルではありません（メニューは Home）。");
        }

        // ---------------- 入力を奪わない ----------------

        [TestCase(Title)]
        [TestCase(Home)]
        [TestCase(UnitSet)]
        [TestCase(Battle)]
        public void NoBackdropGraphicTakesInput(string path)
        {
            Open(path);

            Transform backdrop = Find("ShipBackdrop");

            Graphic[] graphics = backdrop.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics.Length, Is.GreaterThan(0), "走査対象がありません。");

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    path + " の " + graphics[i].name + " が入力を奪っています。");
            }

            // 背景に押せる部品を置きません。
            Assert.That(backdrop.GetComponentsInChildren<Selectable>(true), Is.Empty);
        }

        // ---------------- 既存UIより背面 ----------------

        [TestCase(Title)]
        [TestCase(Home)]
        [TestCase(UnitSet)]
        [TestCase(Battle)]
        public void TheBackdropIsBehindEveryExistingScreenElement(string path)
        {
            Open(path);

            Transform backdrop = Find("ShipBackdrop");
            Transform safeArea = backdrop.parent;

            int backdropIndex = backdrop.GetSiblingIndex();

            for (int i = 0; i < safeArea.childCount; i++)
            {
                Transform child = safeArea.GetChild(i);

                if (child == backdrop)
                {
                    continue;
                }

                // 既存の平坦な塗り（Background）だけは背景より後ろで構いません。
                if (child.name == "Background")
                {
                    Assert.That(
                        child.GetSiblingIndex(),
                        Is.LessThan(backdropIndex),
                        "Background は背景より奥です。");

                    continue;
                }

                Assert.That(
                    child.GetSiblingIndex(),
                    Is.GreaterThan(backdropIndex),
                    path + " の " + child.name + " が背景より奥にあります。");
            }
        }

        [Test]
        public void TheOverlaysStayInFrontOfTheBackdropInBattle()
        {
            Open(Battle);

            int backdrop = Find("ShipBackdrop").GetSiblingIndex();

            string[] overlays =
            {
                "Header", "BattleRoot", "SettingsRoot",
            };

            for (int i = 0; i < overlays.Length; i++)
            {
                Assert.That(
                    Find(overlays[i]).GetSiblingIndex(),
                    Is.GreaterThan(backdrop),
                    overlays[i] + " が背景より奥にあります。");
            }

            // 結果表示・DeployGhost・FX は BattleRoot の中で最前面のままです。
            Transform root = Find("BattleRoot");

            Assert.That(
                Find("ResultView").GetSiblingIndex(),
                Is.GreaterThan(Find("PlayerWheel").GetSiblingIndex()),
                "ResultView がリングより奥にあります。");

            Assert.That(
                Find("DeployGhostLayer").GetSiblingIndex(),
                Is.GreaterThan(Find("HistoryLane").GetSiblingIndex()),
                "DeployGhostLayer が履歴より奥にあります。");

            Assert.That(root, Is.Not.Null);
        }

        // ---------------- Safe Area に収まる ----------------

        [TestCase(Title)]
        [TestCase(Home)]
        [TestCase(UnitSet)]
        [TestCase(Battle)]
        public void TheBackdropStaysInsideTheSafeArea(string path)
        {
            Open(path);

            RectTransform backdrop = (RectTransform)Find("ShipBackdrop");
            RectTransform safeArea = (RectTransform)backdrop.parent;

            Assert.That(safeArea.name, Is.EqualTo("SafeArea"));

            Canvas.ForceUpdateCanvases();

            Rect area = World(safeArea);
            Rect back = World(backdrop);

            Assert.That(back.xMin, Is.GreaterThanOrEqualTo(area.xMin - 0.5f));
            Assert.That(back.xMax, Is.LessThanOrEqualTo(area.xMax + 0.5f));
            Assert.That(back.yMin, Is.GreaterThanOrEqualTo(area.yMin - 0.5f));
            Assert.That(back.yMax, Is.LessThanOrEqualTo(area.yMax + 0.5f));

            // 主要フレームはすべて Safe Area の中です。
            // 1枚だけでなく、その画面が持つ Chamber を全部見ます。
            List<ChamberFrameGraphic> chambers = FindAll<ChamberFrameGraphic>();

            Assert.That(chambers.Count, Is.GreaterThanOrEqualTo(1), "Chamber がありません。");

            for (int i = 0; i < chambers.Count; i++)
            {
                Rect frame = World(chambers[i].rectTransform);
                string name = chambers[i].name;

                Assert.That(frame.xMin, Is.GreaterThanOrEqualTo(area.xMin - 0.5f), name + " 左");
                Assert.That(frame.xMax, Is.LessThanOrEqualTo(area.xMax + 0.5f), name + " 右");
                Assert.That(frame.yMin, Is.GreaterThanOrEqualTo(area.yMin - 0.5f), name + " 下");
                Assert.That(frame.yMax, Is.LessThanOrEqualTo(area.yMax + 0.5f), name + " 上");
            }
        }

        // ---------------- FX OFF と非表示 ----------------

        [Test]
        public void TheBackdropStopsMovingWhenEffectsAreOff()
        {
            Open(Battle);

            ShipBackdropView view = FindOne<ShipBackdropView>();

            Assert.That(view.IsAnimating, Is.True, "通常は動きます。");

            view.SetFxEnabled(false);

            Assert.That(view.IsAnimating, Is.False, "FX OFF では止めます。");

            // 止まっても絵として成立する明るさで静止します。
            Assert.That(
                view.CurrentGlow,
                Is.GreaterThan(0.5f),
                "FX OFF で真っ暗になってはいけません。");

            view.SetFxEnabled(true);

            Assert.That(view.IsAnimating, Is.True);
        }

        [Test]
        public void TheBackdropDoesNotRunWhileHidden()
        {
            Open(Battle);

            ShipBackdropView view = FindOne<ShipBackdropView>();

            view.gameObject.SetActive(false);

            Assert.That(
                view.IsAnimating,
                Is.False,
                "非表示のあいだは更新しません。");

            view.gameObject.SetActive(true);

            Assert.That(view.IsAnimating, Is.True);
        }

        [Test]
        public void TheBackdropUsesNoExtraMaterialOrSprite()
        {
            Open(Battle);

            Graphic[] graphics =
                Find("ShipBackdrop").GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].material,
                    Is.EqualTo(graphics[i].defaultMaterial),
                    graphics[i].name + " が専用マテリアルを持っています。");

                Image image = graphics[i] as Image;

                Assert.That(
                    image,
                    Is.Null,
                    graphics[i].name + " は頂点色だけで描きます（Sprite を増やしません）。");
            }
        }

        // ---------------- 既存の文字とキャラクターを邪魔しない ----------------

        [Test]
        public void TheBackdropNeverCoversTheBattleText()
        {
            Open(Battle);

            Transform backdrop = Find("ShipBackdrop");

            // 背景は既存UIより必ず奥なので、文字を覆いません。
            int backdropIndex = backdrop.GetSiblingIndex();

            TMP_Text[] labels = new TMP_Text[0];

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                labels = root.GetComponentsInChildren<TMP_Text>(true);

                if (labels.Length > 0)
                {
                    break;
                }
            }

            Assert.That(labels.Length, Is.GreaterThan(0), "文字が見つかりません。");

            // 背景の中に文字を置いていないこと。
            Assert.That(
                backdrop.GetComponentsInChildren<TMP_Text>(true),
                Is.Empty,
                "背景に文字を置いてはいけません。");

            Assert.That(backdropIndex, Is.LessThan(Find("Header").GetSiblingIndex()));
        }
    }
}
