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
    /// Phase 3 の表示ルートが Battle.unity 上で「専用の領域だけ」を使う配線になっているか。
    ///
    /// 勝利コアはヘッダーを覆う専用ルート、FINAL CORE / CORE BREAK は FxImpact と同じ
    /// 戦闘表示領域の専用ルートの中だけで描きます。保存は一切行いません。
    /// </summary>
    public sealed class BattleMatchCueSceneWiringTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";

        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");
        }

        [Test]
        public void TheScreenIsWiredToBothCueViews()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            Assert.That(GetField(controller, "victoryCore"), Is.SameAs(FindOne<VictoryCoreView>()));
            Assert.That(GetField(controller, "matchCue"), Is.SameAs(FindOne<BattleMatchCueView>()));
            Assert.That(FindOne<VictoryCoreView>().HasRequiredReferences(), Is.True);
            Assert.That(FindOne<BattleMatchCueView>().HasRequiredReferences(), Is.True);
        }

        [Test]
        public void TheVictoryCoreDrawsInItsOwnHeaderLayerAboveThePips()
        {
            VictoryCoreView view = FindOne<VictoryCoreView>();
            Transform root = view.transform;
            Transform header = Find("Header");

            Assert.That(root.parent, Is.SameAs(header));
            Assert.That(root.GetSiblingIndex(), Is.GreaterThan(Find("ScorePips").GetSiblingIndex()), "ピップより手前に描きます。");
            Assert.That(root.GetComponent<Canvas>(), Is.Not.Null, "頂点更新をヘッダーの再バッチから切り離します。");
            Assert.That(root.GetComponent<Canvas>().overrideSorting, Is.False);
            Assert.That(root.GetComponentsInChildren<Graphic>(true), Is.EqualTo(new Graphic[] { view.Graphic }));
            Assert.That(view.Graphic.raycastTarget, Is.False);
            Assert.That(view.Graphic.material, Is.SameAs(Graphic.defaultGraphicMaterial), "専用 Material を使いません。");

            BattleScorePipsView pips = FindOne<BattleScorePipsView>();

            Assert.That(GetField(view, "scorePips"), Is.SameAs(pips));
            Assert.That(root.IsChildOf(pips.transform), Is.False, "ピップの階層へは入れません。");
        }

        [Test]
        public void TheMatchCueUsesTheSameCombatAreaAsTheImpact()
        {
            BattleMatchCueView view = FindOne<BattleMatchCueView>();
            RectTransform root = (RectTransform)view.transform;
            RectTransform impact = (RectTransform)Find("FxImpact");

            Assert.That(root.parent, Is.SameAs(Find("BattleRoot")));
            Assert.That(root.anchorMin, Is.EqualTo(impact.anchorMin));
            Assert.That(root.anchorMax, Is.EqualTo(impact.anchorMax));
            Assert.That(root.offsetMin, Is.EqualTo(impact.offsetMin));
            Assert.That(root.offsetMax, Is.EqualTo(impact.offsetMax));

            Assert.That(root.GetComponent<RectMask2D>(), Is.Not.Null, "戦闘表示領域の外へ出ないよう切り抜きます。");
            Assert.That(root.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(root.GetComponent<Canvas>().overrideSorting, Is.False);
            Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Null, "入力を受けません。");

            int index = root.GetSiblingIndex();

            Assert.That(index, Is.GreaterThan(impact.GetSiblingIndex()));

            foreach (string name in new[] { "HistoryLane", "PlayerWheel", "FxFlash", "ResultView" })
            {
                Assert.That(index, Is.LessThan(Find(name).GetSiblingIndex()), name + " は節目の表示より手前です。");
            }
        }

        [Test]
        public void TheCueLabelIsAnEmptyHiddenTextThatTakesNoInput()
        {
            BattleMatchCueView view = FindOne<BattleMatchCueView>();
            TMP_Text label = view.Label;

            Assert.That(label, Is.Not.Null);
            Assert.That(label.font, Is.Not.Null, "既存のフォントを使います。");
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.text, Is.Empty, "待機中は何も表示しません。");
            Assert.That(label.transform.IsChildOf(view.transform), Is.True);

            CanvasGroup group = label.GetComponent<CanvasGroup>();

            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(group.blocksRaycasts, Is.False);

            foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " が入力を奪います。");
            }
        }

        [Test]
        public void NoHudObjectLivesInsideTheCueRoots()
        {
            string[] hud =
            {
                "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton", "SafeArea",
                "BattleRoot", "PlayerWheel", "HistoryLane", "ResultView", "RematchButton", "FinalHomeButton",
                "FxFlash", "SettingsRoot", "PlayerCombatant", "CpuCombatant",
            };

            Transform victory = FindOne<VictoryCoreView>().transform;
            Transform cue = FindOne<BattleMatchCueView>().transform;

            foreach (string name in hud)
            {
                Transform target = Find(name);

                Assert.That(target.IsChildOf(victory), Is.False, name);
                Assert.That(target.IsChildOf(cue), Is.False, name);
            }
        }

        // ---------------- 補助 ----------------

        private Transform Find(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }
            }

            Assert.Fail(name + " が見つかりません。");
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
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }
    }
}
