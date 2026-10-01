using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// UnitSet の選択ユニット詳細カード。
    /// 色別POWERとCOREが、カードの主要情報として読めることを実測で確かめます。
    /// </summary>
    public sealed class UnitSetDetailCardTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/UnitSet.unity";
        private const string SinglePath = "Assets/CoreBeasts/Data/Beasts/CB_Volx.asset";
        private const string DualPath =
            "Assets/CoreBeasts/Data/Testing/CB_Volx_RedBlue_TEST.asset";

        private Scene scene;
        private BeastDetailPanel panel;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");

            List<BeastDetailPanel> found = new List<BeastDetailPanel>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<BeastDetailPanel>(true));
            }

            Assert.That(found.Count, Is.EqualTo(1), "詳細カードは1つだけです。");

            panel = found[0];
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, name + " が見つかりません。");

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

        private static Rect World(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new Rect(
                corners[0].x, corners[0].y,
                corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        /// <summary>1体を実際に表示させます。行の詰めはここで走ります。</summary>
        private void Show(bool dual)
        {
            CoreBeastDefinition definition =
                AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(
                    dual ? DualPath : SinglePath);

            Assert.That(definition, Is.Not.Null, "ユニット定義が読めません。");

            OwnedCoreBeast owned = new OwnedCoreBeast();

            typeof(OwnedCoreBeast)
                .GetField("instanceId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(owned, "inst");

            typeof(OwnedCoreBeast)
                .GetField("definition", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(owned, definition);

            typeof(OwnedCoreBeast)
                .GetField("level", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(owned, 18);

            panel.Show(owned);
            Canvas.ForceUpdateCanvases();
        }

        // ---------------- POWERバーの撤去 ----------------

        [Test]
        public void ThereIsNoPowerBarLeftInTheDetailCard()
        {
            Assert.That(
                FindDeep(Rect("DetailPanel"), "PowerGaugeBg"),
                Is.Null,
                "POWERバーの残骸があります。");

            Image[] images = Rect("DetailPanel").GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Assert.That(
                    images[i].type,
                    Is.Not.EqualTo(Image.Type.Filled),
                    "ゲージ（Filled Image）が残っています: " + images[i].name);
            }
        }

        [Test]
        public void TheCoreIconIsAHexRingNotAFilledSquare()
        {
            Graphic icon = (Graphic)GetField(panel, "coreIcon");

            Assert.That(icon, Is.Not.Null, "COREアイコンが結線されていません。");

            Assert.That(
                icon.GetComponent<Image>(),
                Is.Null,
                "COREアイコンが塗りつぶしの四角のままです。");

            Assert.That(
                icon.GetComponent<TMP_Text>(),
                Is.Null,
                "COREアイコンに文字を使ってはいけません。");

            Assert.That(
                icon.GetType().Name,
                Is.EqualTo("LaunchPedestalGraphic"),
                "六角のコアリングにします。");

            Assert.That(icon.raycastTarget, Is.False, "アイコンが入力を奪っています。");

            RectTransform rect = icon.rectTransform;

            // 高さが六角の実寸です。幅は表示時に実測幅で組み直されます。
            Assert.That(
                rect.sizeDelta.y,
                Is.InRange(22f, 48f),
                "COREアイコンの大きさが適正範囲外です: " + rect.sizeDelta.y);

            // 表示させたあとは、正方形（六角が潰れない）であること。
            Show(true);

            Assert.That(
                rect.sizeDelta.x,
                Is.EqualTo(rect.sizeDelta.y).Within(0.5f),
                "COREアイコンが正方形になっていません: " + rect.sizeDelta);

            Assert.That(
                rect.sizeDelta.x,
                Is.InRange(22f, 48f),
                "表示後の COREアイコンの大きさが適正範囲外です。");
        }

        // ---------------- 文字階層 ----------------

        [Test]
        public void TheStatValuesAreTheSecondMostProminentInformation()
        {
            TMP_Text name = Rect("NameLabel").GetComponent<TMP_Text>();
            TMP_Text power = Rect("PowerLabel").GetComponent<TMP_Text>();
            TMP_Text core = Rect("CoreLabel").GetComponent<TMP_Text>();
            TMP_Text skillName = Rect("SkillName").GetComponent<TMP_Text>();
            TMP_Text skillText = Rect("SkillDescription").GetComponent<TMP_Text>();
            TMP_Text level = Rect("LevelLabel").GetComponent<TMP_Text>();

            // 名前 > 属性POWER >= CORE > スキル名 > スキル説明。
            Assert.That(name.fontSize, Is.GreaterThan(power.fontSize), "1位は名前。");
            Assert.That(
                power.fontSize,
                Is.GreaterThanOrEqualTo(core.fontSize),
                "属性POWER は CORE 以上。");

            Assert.That(
                core.fontSize,
                Is.GreaterThan(skillName.fontSize),
                "CORE がスキル名より小さくなっています。");

            Assert.That(
                skillName.fontSize,
                Is.GreaterThan(skillText.fontSize),
                "スキル名がスキル説明より小さくなっています。");

            Assert.That(
                power.fontSize,
                Is.GreaterThan(level.fontSize),
                "属性POWER が補助情報より小さくなっています。");

            // 拡大前（36）より明確に大きいこと。
            Assert.That(
                power.fontSize,
                Is.GreaterThanOrEqualTo(50f),
                "属性POWER が小さいままです。");

            Assert.That(core.fontSize, Is.GreaterThanOrEqualTo(48f));

            Assert.That(power.enableAutoSizing, Is.False);
            Assert.That(core.enableAutoSizing, Is.False);
        }

        // ---------------- 行の詰め ----------------

        [TestCase(false)]
        [TestCase(true)]
        public void TheStatRowKeepsItsGapsForBothSingleAndDual(bool dual)
        {
            Show(dual);

            RectTransform power = Rect("PowerLabel");
            RectTransform icon = Rect("CoreIcon");
            RectTransform core = Rect("CoreLabel");

            float scale = power.lossyScale.x;

            Assume.That(scale, Is.GreaterThan(0f));

            Rect p = World(power);
            Rect i = World(icon);
            Rect c = World(core);

            float powerToIcon = (i.xMin - p.xMax) / scale;
            float iconToCore = (c.xMin - i.xMax) / scale;

            Assert.That(
                powerToIcon,
                Is.GreaterThanOrEqualTo(24f),
                (dual ? "2色" : "単色") + " で POWER群 と CORE群 の間隔が 24u 未満です: " +
                powerToIcon.ToString("F1"));

            Assert.That(
                iconToCore,
                Is.GreaterThan(0f).And.LessThanOrEqualTo(20f),
                (dual ? "2色" : "単色") + " で アイコンとCORE値 の間隔が不自然です: " +
                iconToCore.ToString("F1"));

            // 縦中央がそろうこと。
            Assert.That(i.center.y, Is.EqualTo(p.center.y).Within(2f));
            Assert.That(c.center.y, Is.EqualTo(p.center.y).Within(2f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheStatRowNeverTouchesTheCharacterOrLeavesTheCard(bool dual)
        {
            Show(dual);

            Rect card = World(Rect("DetailPanel"));
            Rect portrait = World(Rect("PortraitFrame"));

            string[] parts = { "PowerLabel", "CoreIcon", "CoreLabel" };

            for (int i = 0; i < parts.Length; i++)
            {
                Rect part = World(Rect(parts[i]));

                Assert.That(
                    part.Overlaps(portrait),
                    Is.False,
                    parts[i] + " がキャラクターへ重なっています。");

                Assert.That(
                    part.xMin >= card.xMin - 0.5f && part.xMax <= card.xMax + 0.5f &&
                    part.yMin >= card.yMin - 0.5f && part.yMax <= card.yMax + 0.5f,
                    Is.True,
                    parts[i] + " がカードの外へ出ています。");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheSkillTextStaysInsideTheCard(bool dual)
        {
            Show(dual);

            Rect card = World(Rect("DetailPanel"));

            string[] parts = { "NameLabel", "LevelLabel", "CostLabel", "SkillName", "SkillDescription" };

            for (int i = 0; i < parts.Length; i++)
            {
                Rect part = World(Rect(parts[i]));

                Assert.That(
                    part.yMin,
                    Is.GreaterThanOrEqualTo(card.yMin - 0.5f),
                    parts[i] + " がカードの下へはみ出しています。");

                Assert.That(
                    part.Overlaps(World(Rect("PortraitFrame"))),
                    Is.False,
                    parts[i] + " がキャラクターへ重なっています。");
            }
        }

        [Test]
        public void TheRowsNeverOverlapEachOther()
        {
            Show(true);

            string[] rows =
            {
                "NameLabel", "LevelLabel", "AttributeLabel",
                "PowerLabel", "SkillName", "SkillDescription",
            };

            for (int i = 0; i < rows.Length; i++)
            {
                for (int j = i + 1; j < rows.Length; j++)
                {
                    if (rows[i] == "LevelLabel" && rows[j] == "AttributeLabel")
                    {
                        continue;
                    }

                    Assert.That(
                        World(Rect(rows[i])).Overlaps(World(Rect(rows[j]))),
                        Is.False,
                        rows[i] + " と " + rows[j] + " が重なっています。");
                }
            }
        }

        [Test]
        public void TheAttributeLabelShowsOnlyTheNames()
        {
            Show(true);

            TMP_Text attribute = Rect("AttributeLabel").GetComponent<TMP_Text>();

            Assert.That(
                attribute.text,
                Is.EqualTo("RED / BLUE"),
                "属性表示に記号と名前の重複があります: " + attribute.text);
        }

        [Test]
        public void NothingInTheCardStealsInput()
        {
            Graphic[] graphics = Rect("DetailPanel").GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].GetComponent<Selectable>() != null)
                {
                    continue;
                }

                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    graphics[i].name + " が入力を奪っています。");
            }
        }
    }
}
