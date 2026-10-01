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
    /// Phase 1 の接触演出が、Battle.unity 上で「戦闘表示だけ」を動かす配線になっているか。
    ///
    /// 振動対象は2体の立ち絵だけで、SafeArea・Canvas・ヘッダー・スコア・ボタンを
    /// 含まないことを、シーンを開いて確かめます。保存は一切行いません。
    /// </summary>
    public sealed class BattleImpactSceneWiringTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";

        /// <summary>
        /// 戦闘表示の片付けフェード（秒）。<see cref="BattleScreenController"/>の
        /// FadeOutCombatantsRoutine と同じ値です。
        /// </summary>
        private const float ArchiveFadeSeconds = 0.22f;

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

            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
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
        public void TheShakeTargetsAreExactlyTheTwoCombatantPortraits()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();
            BattleScreenController controller = FindOne<BattleScreenController>();

            BattleCombatantView player = (BattleCombatantView)GetField(controller, "playerCombatant");
            BattleCombatantView cpu = (BattleCombatantView)GetField(controller, "cpuCombatant");

            Assert.That(fx.PlayerShakeTarget, Is.SameAs(player.PortraitTransform));
            Assert.That(fx.CpuShakeTarget, Is.SameAs(cpu.PortraitTransform));
            Assert.That(fx.PlayerShakeTarget.name, Is.EqualTo("Portrait"));
            Assert.That(fx.CpuShakeTarget.name, Is.EqualTo("Portrait"));
        }

        [Test]
        public void TheShakeTargetsAreNotTheSafeAreaCanvasOrAnyParentOfTheHud()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();
            RectTransform[] targets = { fx.PlayerShakeTarget, fx.CpuShakeTarget };

            foreach (string name in UnmovableNames)
            {
                Transform unmovable = Find(name);

                foreach (RectTransform target in targets)
                {
                    Assert.That(target, Is.Not.SameAs(unmovable), name + " を振動させてはいけません。");

                    Assert.That(
                        unmovable.IsChildOf(target),
                        Is.False,
                        name + " が振動対象の子になっています。");
                }
            }
        }

        [Test]
        public void TheShakeTargetsHoldNoButtonsOrText()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            foreach (RectTransform target in new[] { fx.PlayerShakeTarget, fx.CpuShakeTarget })
            {
                Assert.That(target.GetComponentsInChildren<Selectable>(true), Is.Empty);
                Assert.That(target.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
                Assert.That(target.GetComponentsInChildren<Canvas>(true), Is.Empty, "入れ子のCanvasを作りません。");
            }
        }

        [Test]
        public void TheSafeAreaKeepsOnlyItsOwnComponents()
        {
            Transform safeArea = Find("SafeArea");

            Component[] components = safeArea.GetComponents<Component>();

            // SafeAreaController は Assembly-CSharp にあり参照できないため、型名で確かめます。
            Assert.That(components.Length, Is.EqualTo(2), "RectTransform と SafeAreaController だけです。");
            Assert.That(components[0], Is.InstanceOf<RectTransform>());
            Assert.That(components[1].GetType().Name, Is.EqualTo("SafeAreaController"));
        }

        [Test]
        public void ANormalRoundTakesTwoToThreeSecondsWithTheExistingBanner()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();
            BattleDeployTransitionView deploy = FindOne<BattleDeployTransitionView>();

            float deploySeconds = (float)GetField(deploy, "fxSeconds");
            float flashSeconds = (float)GetField(fx, "flashSeconds");
            float bannerSeconds = (float)GetField(fx, "fxBannerSeconds");

            float clash = BattleRoundPresentationPlan.Create(RoundWinner.Player).ClashDuration;
            float banner = flashSeconds + bannerSeconds + flashSeconds;
            float total = deploySeconds + clash + banner + ArchiveFadeSeconds;

            Assert.That(total, Is.InRange(2f, 3f), "deploy + clash + banner + archive = " + total);
        }

        // ---------------- Phase 2: 決着エフェクトの表示ルート ----------------

        [Test]
        public void TheImpactBurstIsTheOnlyGraphicInItsOwnEffectRoot()
        {
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();
            ImpactBurstGraphic burst = FindOne<ImpactBurstGraphic>();

            Assert.That(fx.ImpactBurst, Is.SameAs(burst), "BattleFxPlayer.impactBurst が配線されていません。");
            Assert.That(fx.HasRequiredReferences(), Is.True);

            Transform root = burst.transform.parent;

            Assert.That(root.name, Is.EqualTo("FxImpact"));
            Assert.That(root.parent, Is.SameAs(Find("BattleRoot")));
            Assert.That(root.GetComponentsInChildren<Graphic>(true), Is.EqualTo(new Graphic[] { burst }));
            Assert.That(root.childCount, Is.EqualTo(1));
            Assert.That(burst.transform.childCount, Is.EqualTo(0));
            Assert.That(burst.raycastTarget, Is.False);
            Assert.That(burst.material, Is.SameAs(Graphic.defaultGraphicMaterial), "専用 Material を使いません。");
        }

        [Test]
        public void TheEffectRootClipsAndIsolatesItsOwnRebuilds()
        {
            Transform root = Find("FxImpact");

            Assert.That(root.GetComponent<RectMask2D>(), Is.Not.Null, "戦闘表示領域の外へ出ないよう切り抜きます。");

            Canvas nested = root.GetComponent<Canvas>();

            Assert.That(nested, Is.Not.Null, "頂点更新を親 Canvas の再バッチから切り離します。");
            Assert.That(nested.overrideSorting, Is.False, "描画順は階層どおりです。");
            Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Null, "入力を受けません。");
        }

        [Test]
        public void TheEffectRootContainsNoHudAndNoCombatant()
        {
            Transform root = Find("FxImpact");

            foreach (string name in UnmovableNames)
            {
                Transform hud = Find(name);

                Assert.That(hud.IsChildOf(root), Is.False, name + " がエフェクトの子になっています。");
                Assert.That(root.IsChildOf(hud) && name != "Canvas" && name != "SafeArea" && name != "BattleRoot",
                    Is.False, "エフェクトが " + name + " の中にあります。");
            }

            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            Assert.That(fx.PlayerShakeTarget.IsChildOf(root), Is.False);
            Assert.That(fx.CpuShakeTarget.IsChildOf(root), Is.False);
            Assert.That(root.IsChildOf(fx.PlayerShakeTarget), Is.False, "振動対象と別の RectTransform です。");
            Assert.That(root.IsChildOf(fx.CpuShakeTarget), Is.False, "振動対象と別の RectTransform です。");
        }

        [Test]
        public void TheEffectDrawsAboveTheCombatantsAndBelowEveryHudLayer()
        {
            Transform root = Find("FxImpact");
            int index = root.GetSiblingIndex();

            Assert.That(index, Is.GreaterThan(Find("PlayerCombatant").GetSiblingIndex()));
            Assert.That(index, Is.GreaterThan(Find("CpuCombatant").GetSiblingIndex()));

            foreach (string name in new[] { "HistoryLane", "PlayerWheel", "FxFlash", "ResultView" })
            {
                Assert.That(index, Is.LessThan(Find(name).GetSiblingIndex()), name + " はエフェクトより手前です。");
            }
        }

        [Test]
        public void TheEffectRootSpansOnlyTheCombatArea()
        {
            RectTransform root = (RectTransform)Find("FxImpact");
            RectTransform player = (RectTransform)Find("PlayerCombatant");
            RectTransform cpu = (RectTransform)Find("CpuCombatant");

            // 下端は PlayerCombatant の下端、上端は CpuCombatant の上端（どちらも親の端からの距離）。
            Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(root.offsetMin.y, Is.EqualTo(player.anchoredPosition.y - player.sizeDelta.y).Within(0.001f));
            Assert.That(root.offsetMax.y, Is.EqualTo(cpu.anchoredPosition.y).Within(0.001f));
            Assert.That(root.offsetMin.x, Is.EqualTo(24f).Within(0.001f));
            Assert.That(root.offsetMax.x, Is.EqualTo(-24f).Within(0.001f));
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
