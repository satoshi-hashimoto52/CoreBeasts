using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 4（ATTRIBUTE LINK）の表示と演出が、Battle.unity 上で正しく配線されているか。
    ///
    /// LINK 表示は戦闘中の Info の中、履歴マーカーは HistoryLane の中、演出は戦闘表示領域（FxImpact と同じ範囲）
    /// の中だけにあり、HUD や立ち絵を動かす経路を持たないことを、シーンを開いて確かめます。保存は一切行いません。
    /// </summary>
    public sealed class BattleAttributeLinkSceneWiringTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";

        private static readonly string[] UnmovableNames =
        {
            "Canvas", "SafeArea", "Header", "ScoreLabel", "RoundLabel", "ScorePips",
            "SettingsButton", "BattleRoot", "PlayerWheel", "HistoryLane", "ResultView",
            "RematchButton", "FinalHomeButton", "FxFlash", "SettingsRoot",
        };

        private Scene scene;
        private SceneSetup[] savedSetup;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            savedSetup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            List<SceneSetup> restorable = new List<SceneSetup>();

            if (savedSetup != null)
            {
                foreach (SceneSetup entry in savedSetup)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.path))
                    {
                        restorable.Add(entry);
                    }
                }
            }

            if (restorable.Count == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }

            if (!restorable.Exists(s => s.isActive))
            {
                restorable[0].isActive = true;
            }

            EditorSceneManager.RestoreSceneManagerSetup(restorable.ToArray());
        }

        [Test]
        public void EachCombatantHasAHiddenLinkLabelInsideItsInfo()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            foreach (string side in new[] { "playerCombatant", "cpuCombatant" })
            {
                BattleCombatantView combatant = (BattleCombatantView)GetField(controller, side);
                TMP_Text label = combatant.LinkLabel;

                Assert.That(label, Is.Not.Null, side + ".linkLabel が配線されていません。");
                Assert.That(label.name, Is.EqualTo("LinkLabel"));
                Assert.That(label.transform.parent.name, Is.EqualTo("Info"), "戦闘中の情報の中に置きます。");
                Assert.That(label.transform.IsChildOf(combatant.transform), Is.True);
                Assert.That(label.gameObject.activeSelf, Is.False, "LINK していない間は出しません。");
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.transform.IsChildOf(combatant.PortraitTransform), Is.False, "揺れる立ち絵の中には置きません。");
            }
        }

        [Test]
        public void TheHistoryLaneHasOneMarkerGraphicWiredToIt()
        {
            BattleHistoryLaneView lane = FindOne<BattleHistoryLaneView>();
            BattleHistoryLinkMarkerGraphic markers = FindOne<BattleHistoryLinkMarkerGraphic>();

            Assert.That(lane.LinkMarkers, Is.SameAs(markers));
            Assert.That(markers.transform.parent, Is.SameAs(lane.transform));
            Assert.That(markers.raycastTarget, Is.False, "枠の操作を妨げません。");
            Assert.That(markers.material, Is.SameAs(Graphic.defaultGraphicMaterial));
            Assert.That(markers.MarkerCount, Is.EqualTo(0));
        }

        [Test]
        public void TheLinkViewIsWiredToTheControllerAndTheSamePortraitsAsTheFx()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();
            BattleAttributeLinkView view = FindOne<BattleAttributeLinkView>();
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            Assert.That(controller.AttributeLinkView, Is.SameAs(view));
            Assert.That(view.HasRequiredReferences(), Is.True);
            Assert.That(view.Graphic, Is.SameAs(FindOne<AttributeLinkGraphic>()));
            Assert.That(GetField(view, "playerPortrait"), Is.SameAs(fx.PlayerShakeTarget), "立ち絵は読むだけで、同じものを指します。");
            Assert.That(GetField(view, "cpuPortrait"), Is.SameAs(fx.CpuShakeTarget));
            Assert.That(view.PlayerLabel.text, Is.Empty);
            Assert.That(view.CpuLabel.text, Is.Empty);
            Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
            Assert.That(view.CpuLabelAlpha, Is.EqualTo(0f));
            Assert.That(view.PlayerLabel.raycastTarget, Is.False);
            Assert.That(view.CpuLabel.raycastTarget, Is.False);
        }

        [Test]
        public void TheLinkRootCoversExactlyTheImpactAreaAndClips()
        {
            RectTransform link = (RectTransform)Find("FxLink");
            RectTransform impact = (RectTransform)Find("FxImpact");

            Assert.That(link.parent, Is.SameAs(Find("BattleRoot")));
            Assert.That(link.anchorMin, Is.EqualTo(impact.anchorMin));
            Assert.That(link.anchorMax, Is.EqualTo(impact.anchorMax));
            Assert.That(link.offsetMin, Is.EqualTo(impact.offsetMin));
            Assert.That(link.offsetMax, Is.EqualTo(impact.offsetMax));
            Assert.That(link.GetComponent<RectMask2D>(), Is.Not.Null, "戦闘表示領域の外へ出ません。");

            Canvas nested = link.GetComponent<Canvas>();

            Assert.That(nested, Is.Not.Null);
            Assert.That(nested.overrideSorting, Is.False);
            Assert.That(link.GetComponent<GraphicRaycaster>(), Is.Null, "入力を受けません。");

            foreach (Graphic g in link.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(g.raycastTarget, Is.False, g.name + " は入力を受けません。");
                Assert.That(g is Image || g is RawImage, Is.False, "画像は使いません。");
            }
        }

        [Test]
        public void TheLinkRootLeavesThePhaseTwoImpactRootUntouched()
        {
            Transform impact = Find("FxImpact");

            Assert.That(impact.childCount, Is.EqualTo(1));
            Assert.That(impact.GetChild(0).GetComponent<ImpactBurstGraphic>(), Is.Not.Null);
            Assert.That(Find("FxLink").IsChildOf(impact), Is.False);
        }

        [Test]
        public void TheLinkRootDrawsAboveTheCombatantsAndBelowTheHud()
        {
            Transform link = Find("FxLink");
            int index = link.GetSiblingIndex();

            Assert.That(index, Is.GreaterThan(Find("PlayerCombatant").GetSiblingIndex()));
            Assert.That(index, Is.GreaterThan(Find("CpuCombatant").GetSiblingIndex()));
            Assert.That(index, Is.GreaterThan(Find("FxImpact").GetSiblingIndex()));

            foreach (string name in new[] { "HistoryLane", "PlayerWheel", "FxFlash", "ResultView" })
            {
                Assert.That(index, Is.LessThan(Find(name).GetSiblingIndex()), name + " は LINK 演出より手前です。");
            }
        }

        [Test]
        public void TheLinkRootContainsNoHudAndNoPortrait()
        {
            Transform link = Find("FxLink");
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            foreach (string name in UnmovableNames)
            {
                Assert.That(Find(name).IsChildOf(link), Is.False, name + " が LINK 演出の子になっています。");
            }

            Assert.That(fx.PlayerShakeTarget.IsChildOf(link), Is.False);
            Assert.That(fx.CpuShakeTarget.IsChildOf(link), Is.False);
            Assert.That(link.IsChildOf(fx.PlayerShakeTarget), Is.False);
            Assert.That(link.IsChildOf(fx.CpuShakeTarget), Is.False);
            Assert.That(link.GetComponentsInChildren<Selectable>(true), Is.Empty);
        }

        // ---------------- 補助 ----------------

        private Transform Find(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
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
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(found.Count, Is.EqualTo(1), typeof(T).Name + " はシーンに1個だけです。");

            return found[0];
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }
    }
}
