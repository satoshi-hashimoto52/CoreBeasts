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
    /// Phase 5（ユニークスキル）の表示と演出が、Battle.unity 上で正しく配線されているか。
    ///
    /// スキル表示は戦闘中の Info の中、演出は戦闘表示領域（FxLink と同じ範囲）の中だけにあり、
    /// HUD や立ち絵を動かす経路を持たないことを、シーンを開いて確かめます。保存は一切行いません。
    /// </summary>
    public sealed class BattleUniqueSkillSceneWiringTests
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

        private const string JapaneseFontName = "NotoSansJP-Bold-CoreBeastsLink SDF";

        [Test]
        public void EachCombatantHasAHiddenSkillLabelInsideItsInfo()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();

            foreach (string side in new[] { "playerCombatant", "cpuCombatant" })
            {
                BattleCombatantView combatant = (BattleCombatantView)GetField(controller, side);
                TMP_Text label = combatant.SkillLabel;

                Assert.That(label, Is.Not.Null, side + ".skillLabel が配線されていません。");
                Assert.That(label.name, Is.EqualTo("SkillLabel"));
                Assert.That(label.transform.parent.name, Is.EqualTo("Info"), "戦闘中の情報の中に置きます。");
                Assert.That(label.transform.parent, Is.SameAs(combatant.LinkLabel.transform.parent));
                Assert.That(label.gameObject.activeSelf, Is.False, "発動していない間は出しません。");
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.font.name, Is.EqualTo(JapaneseFontName), "実行中に TMP の SubMesh を作らないよう、日本語サブセットで描きます。");
                Assert.That(label.transform.IsChildOf(combatant.PortraitTransform), Is.False, "揺れる立ち絵の中には置きません。");
                Assert.That(label.rectTransform.anchoredPosition.y, Is.Not.EqualTo(combatant.LinkLabel.rectTransform.anchoredPosition.y),
                    "LINK 表示と別の行に置きます。");
            }
        }

        [Test]
        public void TheSkillCueViewIsWiredToTheControllerAndTheSamePortraitsAsTheFx()
        {
            BattleScreenController controller = FindOne<BattleScreenController>();
            BattleSkillCueView view = FindOne<BattleSkillCueView>();
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            Assert.That(controller.SkillCueView, Is.SameAs(view));
            Assert.That(view.HasRequiredReferences(), Is.True);
            Assert.That(view.Graphic, Is.SameAs(FindOne<UniqueSkillCueGraphic>()));
            Assert.That(GetField(view, "playerPortrait"), Is.SameAs(fx.PlayerShakeTarget), "立ち絵は読むだけで、同じものを指します。");
            Assert.That(GetField(view, "cpuPortrait"), Is.SameAs(fx.CpuShakeTarget));

            foreach (TMP_Text label in new[] { view.PlayerLabel, view.CpuLabel })
            {
                Assert.That(label.text, Is.Empty);
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.font.name, Is.EqualTo(JapaneseFontName));
            }

            Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
            Assert.That(view.CpuLabelAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void TheSkillRootCoversExactlyTheLinkAreaAndClips()
        {
            RectTransform skill = (RectTransform)Find("FxSkill");
            RectTransform link = (RectTransform)Find("FxLink");

            Assert.That(skill.parent, Is.SameAs(Find("BattleRoot")));
            Assert.That(skill.anchorMin, Is.EqualTo(link.anchorMin));
            Assert.That(skill.anchorMax, Is.EqualTo(link.anchorMax));
            Assert.That(skill.offsetMin, Is.EqualTo(link.offsetMin));
            Assert.That(skill.offsetMax, Is.EqualTo(link.offsetMax));
            Assert.That(skill.GetComponent<RectMask2D>(), Is.Not.Null, "戦闘表示領域の外へ出ません。");

            Canvas nested = skill.GetComponent<Canvas>();

            Assert.That(nested, Is.Not.Null);
            Assert.That(nested.overrideSorting, Is.False);
            Assert.That(skill.GetComponent<GraphicRaycaster>(), Is.Null, "入力を受けません。");

            foreach (Graphic g in skill.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(g.raycastTarget, Is.False, g.name + " は入力を受けません。");
                Assert.That(g is Image || g is RawImage, Is.False, "画像は使いません。");
            }

            Assert.That(skill.GetComponentsInChildren<Selectable>(true), Is.Empty);
        }

        [Test]
        public void TheSkillRootDrawsAboveTheLinkCueAndBelowTheHud()
        {
            int index = Find("FxSkill").GetSiblingIndex();

            Assert.That(index, Is.EqualTo(Find("FxLink").GetSiblingIndex() + 1));

            foreach (string name in new[] { "HistoryLane", "PlayerWheel", "FxFlash", "ResultView" })
            {
                Assert.That(index, Is.LessThan(Find(name).GetSiblingIndex()), name + " はスキル演出より手前です。");
            }
        }

        [Test]
        public void TheSkillRootContainsNoHudAndNoPortraitAndLeavesTheLinkRootUntouched()
        {
            Transform skill = Find("FxSkill");
            BattleFxPlayer fx = FindOne<BattleFxPlayer>();

            foreach (string name in UnmovableNames)
            {
                Assert.That(Find(name).IsChildOf(skill), Is.False, name + " がスキル演出の子になっています。");
            }

            Assert.That(fx.PlayerShakeTarget.IsChildOf(skill), Is.False);
            Assert.That(fx.CpuShakeTarget.IsChildOf(skill), Is.False);

            Transform link = Find("FxLink");

            Assert.That(link.childCount, Is.EqualTo(3), "FxLink は Phase 4 のままです。");
            Assert.That(link.GetComponent<BattleAttributeLinkView>(), Is.Not.Null);
            Assert.That(link.GetComponent<BattleSkillCueView>(), Is.Null);
            Assert.That(FindOne<BattleAttributeLinkView>().Graphic, Is.SameAs(FindOne<AttributeLinkGraphic>()));
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
