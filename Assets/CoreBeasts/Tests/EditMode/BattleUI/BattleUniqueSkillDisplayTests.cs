using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 5（ユニークスキル）の表示と演出の View・Graphic・文言。
    /// </summary>
    public sealed class BattleUniqueSkillDisplayTests
    {
        private const float Frame = 1f / 60f;
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute B = UnitAttribute.Blue;

        private TestBattleViews views;
        private TestBattleCards cards;
        private readonly FakeBattleText text = new FakeBattleText();

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
            cards = new TestBattleCards();
        }

        [TearDown]
        public void TearDown()
        {
            views.Cleanup();
            cards.Cleanup();
        }

        private static UniqueSkillActivation On(UniqueSkillKind kind, int self, int penalty)
        {
            return new UniqueSkillActivation(kind, true, UniqueSkillReason.Unconditional, self, penalty);
        }

        // ---------------- 段の表示 ----------------

        [Test]
        public void TheCombatantShowsOnlyActivatedSkills()
        {
            BattleCombatantView view = CreateCombatant(out TMP_Text label);

            view.ShowSkill(On(UniqueSkillKind.CrimsonBite, 4, 0));
            Assert.That(label.gameObject.activeSelf, Is.True);
            Assert.That(label.text, Is.EqualTo("スキル発動 POWER +4"));

            view.ShowSkill(On(UniqueSkillKind.TidalHowl, 0, 4));
            Assert.That(label.text, Is.EqualTo("スキル発動 相手の POWER -4"));

            view.ShowSkill(new UniqueSkillActivation(UniqueSkillKind.VerdantFang, false, UniqueSkillReason.WonPreviousRound, 6, 0));
            Assert.That(label.gameObject.activeSelf, Is.False, "発動しなければ何も出しません。");
            Assert.That(label.text, Is.Empty);
            Assert.That(view.ShownSkill, Is.EqualTo(UniqueSkillActivation.None));
        }

        [Test]
        public void ThePreviewShowsOnlyWhatTheOwnHistoryDecides()
        {
            BattleCombatantView view = CreateCombatant(out TMP_Text label);

            view.ShowSkillPreview(new UniqueSkillPreview(UniqueSkillKind.VerdantFang, UniqueSkillPreviewState.Activates, 6, 0));
            Assert.That(label.text, Is.EqualTo("スキル発動 POWER +6"));

            view.ShowSkillPreview(new UniqueSkillPreview(UniqueSkillKind.StormBite, UniqueSkillPreviewState.DependsOnOpponent, 3, 0));
            Assert.That(label.gameObject.activeSelf, Is.False, "相手の選出で決まるスキルは予告しません。");

            view.ShowSkillPreview(new UniqueSkillPreview(UniqueSkillKind.TidalHowl, UniqueSkillPreviewState.Activates, 0, 4));
            Assert.That(label.text, Is.EqualTo("スキル発動 相手の POWER -4"));

            view.ShowHidden();
            Assert.That(label.gameObject.activeSelf, Is.False, "伏せた段にスキル表示を残しません。");

            view.ShowSkill(On(UniqueSkillKind.CrimsonBite, 4, 0));
            view.ShowEmpty();
            Assert.That(label.gameObject.activeSelf, Is.False);
            Assert.That(view.ShownSkillPreview.State, Is.EqualTo(UniqueSkillPreviewState.None));
        }

        [Test]
        public void TheCardCarriesTheSkillKindFromTheDefinition()
        {
            CoreBeastDefinition definition = cards.CreateDefinition("fang", UnitAttribute.Green, 50);
            TestBattleViews.SetField(definition, "skillKind", UniqueSkillKind.VerdantFang);
            TestBattleViews.SetField(definition, "skillName", "ANY NAME");

            Assert.That(BattleUnitCard.TryCreate("fang-1", 3, definition, out BattleUnitCard card), Is.True);
            Assert.That(card.Unit.Skill, Is.EqualTo(UniqueSkillKind.VerdantFang), "表示名ではなく種類を引き継ぎます。");
        }

        // ---------------- 結果理由 ----------------

        [Test]
        public void TheResultReasonSplitsLinkSkillAndPenaltyFromTheRecordedBreakdown()
        {
            RoundResult result = Result(
                new ComparedPowerBreakdown(54, 3, 4, 0, 61),
                new ComparedPowerBreakdown(50, 0, 0, 4, 46));

            Assert.That(
                BattleResultText.BuildDecision(result, text),
                Is.EqualTo("POWER勝利  54<size=70%>（リンク+3／スキル+4）</size>対 50<size=70%>（妨害-4）</size>"));

            RoundResult cpuSide = Result(
                new ComparedPowerBreakdown(60, 0, 0, 0, 60),
                new ComparedPowerBreakdown(50, 6, 3, 4, 55));

            Assert.That(
                BattleResultText.BuildDecision(cpuSide, text),
                Is.EqualTo("POWER勝利  60 対 50<size=70%>（リンク+6／スキル+3／妨害-4）</size>"),
                "加算・減算はその側の数値のすぐ後ろに付けます。");
        }

        [Test]
        public void TheResultReasonUsesTheRecordedValuesNotTheFinalValue()
        {
            // 最終値から逆算すると 61 - 3 - 4 = 54 ですが、記録した基礎値（ここでは 50）を出します。
            RoundResult result = Result(new ComparedPowerBreakdown(50, 3, 4, 0, 61), new ComparedPowerBreakdown(40, 0, 0, 0, 40));

            Assert.That(BattleResultText.BuildDecision(result, text), Does.StartWith("POWER勝利  50<size=70%>（リンク+3／スキル+4）</size>"));
        }

        [Test]
        public void ReasonsWithoutModifiersKeepTheirExistingText()
        {
            Assert.That(
                BattleResultText.BuildDecision(Result(new ComparedPowerBreakdown(60, 0, 0, 0, 60), new ComparedPowerBreakdown(50, 0, 0, 0, 50)), text),
                Is.EqualTo(text.PowerWin));

            RoundResult attribute = new RoundResult(
                1, Unit("p", R), Unit("c", UnitAttribute.Green), RoundWinner.Player, RoundDecision.AttributeAdvantage, R,
                AttributeLinkResult.None, AttributeLinkResult.None, null, null, 0, 0,
                On(UniqueSkillKind.CrimsonBite, 4, 0), UniqueSkillActivation.None,
                ComparedPowerBreakdown.Unavailable, ComparedPowerBreakdown.Unavailable);

            Assert.That(BattleResultText.BuildDecision(attribute, text), Is.EqualTo(text.AttributeWin), "属性勝ちはスキルで表記を変えません。");
        }

        // ---------------- 文言 ----------------

        [Test]
        public void TheCatalogDefaultsAndTheShippedAssetUseTheConfirmedSkillTexts()
        {
            BattleTextCatalog defaults = ScriptableObject.CreateInstance<BattleTextCatalog>();

            try
            {
                AssertSkillTexts(defaults, "初期値");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
            }

            BattleTextCatalog shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleTextCatalog>("Assets/CoreBeasts/Data/BattleTextCatalog_EN.asset");

            Assert.That(shipped, Is.Not.Null);
            AssertSkillTexts(shipped, "BattleTextCatalog_EN.asset");
        }

        private static void AssertSkillTexts(BattleTextCatalog catalog, string label)
        {
            Assert.That(catalog.FormatSkillCue("CRIMSON BITE", UniqueSkillKind.CrimsonBite, 4), Is.EqualTo("CRIMSON BITE 発動\n同じ属性の流れで POWER +4"), label);
            Assert.That(catalog.FormatSkillCue("TIDAL HOWL", UniqueSkillKind.TidalHowl, 4), Is.EqualTo("TIDAL HOWL 発動\n相手の POWER -4"), label);
            Assert.That(catalog.FormatSkillCue("VERDANT FANG", UniqueSkillKind.VerdantFang, 6), Is.EqualTo("VERDANT FANG 発動\n直前の敗北で POWER +6"), label);
            Assert.That(catalog.FormatSkillCue("STORM BITE", UniqueSkillKind.StormBite, 3), Is.EqualTo("STORM BITE 発動\n相手がREDかBLUEで POWER +3"), label);
            Assert.That(catalog.FormatSkillCue("X", UniqueSkillKind.None, 0), Is.Empty, label);
            Assert.That(catalog.FormatSkillSelfBonus(4), Is.EqualTo("スキル発動 POWER +4"), label);
            Assert.That(catalog.FormatSkillOpponentPenalty(4), Is.EqualTo("スキル発動 相手の POWER -4"), label);
            Assert.That(
                catalog.FormatPowerBreakdown(54, catalog.FormatLinkPart(3) + catalog.BreakdownSeparator + catalog.FormatSkillPart(4)),
                Is.EqualTo("54<size=70%>（リンク+3／スキル+4）</size>"), label);
            Assert.That(catalog.FormatPenaltyPart(4), Is.EqualTo("妨害-4"), label);
            Assert.That(catalog.FormatLinkedPower(54, 3), Is.EqualTo("54<size=70%>（リンク+3）</size>"), label + ": LINK だけの表記は Phase 4 と同じです。");
        }

        // ---------------- 演出 ----------------

        [Test]
        public void TheCueShowsBothSidesTogetherAndClearsAfterwards()
        {
            BattleSkillCueView view = CreateCueView(out Parts parts);
            BattleSkillPresentationPlan plan = BattleSkillPresentationPlan.Create(
                On(UniqueSkillKind.CrimsonBite, 4, 0), On(UniqueSkillKind.TidalHowl, 0, 4), true);

            string playerText = text.FormatSkillCue("CRIMSON BITE", UniqueSkillKind.CrimsonBite, 4);
            string cpuText = text.FormatSkillCue("TIDAL HOWL", UniqueSkillKind.TidalHowl, 4);
            Vector2 playerPortrait = parts.PlayerPortrait.anchoredPosition;
            Vector2 cpuPortrait = parts.CpuPortrait.anchoredPosition;

            int frames = 0;
            bool sawBoth = false;

            Run(view.PlayRoutine(plan, playerText, cpuText), () =>
            {
                frames++;

                Assert.That(parts.PlayerLabel.text, Is.EqualTo(playerText));
                Assert.That(parts.CpuLabel.text, Is.EqualTo(cpuText));

                sawBoth |= parts.Graphic.IsShowingPlayer && parts.Graphic.IsShowingCpu && view.PlayerLabelAlpha > 0f && view.CpuLabelAlpha > 0f;

                Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(playerPortrait), "立ち絵は動かしません。");
                Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(cpuPortrait));
            });

            Assert.That(sawBoth, Is.True, "双方が発動したら同時に出します。");
            Assert.That(frames * Frame, Is.EqualTo(BattleSkillPresentationPlan.CueDuration).Within(Frame * 1.5f));
            AssertCleared(view, parts);
        }

        [Test]
        public void TheCueDoesNotShowTheSideThatDidNotActivate()
        {
            BattleSkillCueView view = CreateCueView(out Parts parts);
            BattleSkillPresentationPlan plan = BattleSkillPresentationPlan.Create(
                new UniqueSkillActivation(UniqueSkillKind.StormBite, false, UniqueSkillReason.OpponentHasNoRedOrBlue, 3, 0),
                On(UniqueSkillKind.VerdantFang, 6, 0), true);

            Run(view.PlayRoutine(plan, "SHOULD NOT SHOW", text.FormatSkillCue("VERDANT FANG", UniqueSkillKind.VerdantFang, 6)), () =>
            {
                Assert.That(parts.PlayerLabel.text, Is.Empty);
                Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
                Assert.That(parts.Graphic.IsShowingPlayer, Is.False);
            });

            AssertCleared(view, parts);
        }

        [Test]
        public void FxOffShowsOnlyTheTextForTheSameTime()
        {
            BattleSkillCueView view = CreateCueView(out Parts parts);
            BattleSkillPresentationPlan plan = BattleSkillPresentationPlan.Create(On(UniqueSkillKind.TidalHowl, 0, 4), UniqueSkillActivation.None, false);

            int frames = 0;

            Run(view.PlayRoutine(plan, text.FormatSkillCue("TIDAL HOWL", UniqueSkillKind.TidalHowl, 4), null), () =>
            {
                frames++;

                Assert.That(parts.Graphic.IsShowing, Is.False, "FX OFF では図形を出しません。");
                Assert.That(view.PlayerLabelAlpha, Is.EqualTo(1f));
            });

            Assert.That(frames * Frame, Is.EqualTo(BattleSkillPresentationPlan.TextOnlyDuration).Within(Frame * 1.5f));
            AssertCleared(view, parts);
        }

        [Test]
        public void ResettingMidCueClearsEverythingAndTheOldRoutineStops()
        {
            BattleSkillCueView view = CreateCueView(out Parts parts);
            Vector2 rest = parts.PlayerLabel.rectTransform.anchoredPosition;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(
                BattleSkillPresentationPlan.Create(On(UniqueSkillKind.CrimsonBite, 4, 0), On(UniqueSkillKind.TidalHowl, 0, 4), true), "A", "B"));

            for (int i = 0; i < 5; i++)
            {
                StepFrame(stack);
            }

            Assert.That(view.IsPlaying, Is.True);

            view.ResetVisuals();

            AssertCleared(view, parts);
            Assert.That(parts.PlayerLabel.rectTransform.anchoredPosition, Is.EqualTo(rest), "文字の位置も元へ戻します。");

            while (StepFrame(stack))
            {
                Assert.That(parts.Graphic.IsShowing, Is.False);
                Assert.That(parts.PlayerLabel.text, Is.Empty);
            }
        }

        [Test]
        public void TheGraphicStaysWithinItsVertexBudgetAndSpread()
        {
            UniqueSkillCueGraphic graphic = views.CreateObject("SkillBurst").AddComponent<UniqueSkillCueGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(1000f, 900f);

            graphic.Begin(true, new Rect(100f, -300f, 360f, 60f), true, new Rect(-400f, 250f, 300f, 60f));

            for (float p = 0f; p < 1f; p += 0.05f)
            {
                graphic.SetProgress(p);

                int count = Build(graphic.FillMesh);

                Assert.That(count, Is.EqualTo(UniqueSkillCueGraphic.VerticesPerSide * 2));
                Assert.That(count, Is.LessThanOrEqualTo(BattleSkillPresentationPlan.VertexBudget));
                Assert.That(graphic.LastMaxSpread, Is.LessThanOrEqualTo(BattleSkillPresentationPlan.MaxFrameSpread + 0.01f));
            }

            graphic.SetProgress(1f);

            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(Build(graphic.FillMesh), Is.EqualTo(0));
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void RepeatedCuesCreateNoObjectsOrComponents()
        {
            BattleSkillCueView view = CreateCueView(out _);

            int transforms = view.GetComponentsInChildren<Transform>(true).Length;
            int components = view.GetComponentsInChildren<Component>(true).Length;

            for (int i = 0; i < 10; i++)
            {
                Run(view.PlayRoutine(
                    BattleSkillPresentationPlan.Create(On(UniqueSkillKind.CrimsonBite, 4, 0), On(UniqueSkillKind.TidalHowl, 0, 4), i % 2 == 0),
                    "CRIMSON BITE", "TIDAL HOWL"), null);
            }

            Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
            Assert.That(view.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(components));
        }

        // ---------------- 補助 ----------------

        private struct Parts
        {
            internal UniqueSkillCueGraphic Graphic;
            internal TMP_Text PlayerLabel;
            internal TMP_Text CpuLabel;
            internal RectTransform PlayerPortrait;
            internal RectTransform CpuPortrait;
        }

        private static BattleUnit Unit(string id, UnitAttribute a)
        {
            return new BattleUnit(id, a, 50);
        }

        private static RoundResult Result(ComparedPowerBreakdown player, ComparedPowerBreakdown cpu)
        {
            BattleUnit p = Unit("p", B);
            BattleUnit c = Unit("c", B);

            return new RoundResult(
                1, p, c, player.Final >= cpu.Final ? RoundWinner.Player : RoundWinner.Cpu, RoundDecision.PowerComparison, B,
                AttributeLinkResult.None, AttributeLinkResult.None, p, c, player.Final, cpu.Final,
                UniqueSkillActivation.None, UniqueSkillActivation.None, player, cpu);
        }

        private BattleCombatantView CreateCombatant(out TMP_Text skillLabel)
        {
            BattleCombatantView view = views.CreateCombatant(out TestBattleViews.CombatantParts _);

            skillLabel = views.CreateLabel("SkillLabel", view.transform);
            TestBattleViews.SetField(view, "skillLabel", skillLabel);
            view.Bind(null, null, text);

            return view;
        }

        private BattleSkillCueView CreateCueView(out Parts parts)
        {
            GameObject area = views.CreateObject("FxSkill");
            area.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);

            GameObject stage = views.CreateObject("Stage");
            RectTransform player = views.CreateObject("PlayerPortrait", stage.transform).GetComponent<RectTransform>();
            RectTransform cpu = views.CreateObject("CpuPortrait", stage.transform).GetComponent<RectTransform>();
            player.sizeDelta = new Vector2(300f, 360f);
            cpu.sizeDelta = new Vector2(300f, 360f);
            player.anchoredPosition = new Vector2(300f, -280f);
            cpu.anchoredPosition = new Vector2(-300f, 290f);

            GameObject burst = views.CreateObject("SkillBurst", area.transform);
            burst.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);
            UniqueSkillCueGraphic graphic = burst.AddComponent<UniqueSkillCueGraphic>();

            TMP_Text playerLabel = views.CreateLabel("PlayerSkillLabel", area.transform);
            TMP_Text cpuLabel = views.CreateLabel("CpuSkillLabel", area.transform);
            playerLabel.rectTransform.sizeDelta = new Vector2(620f, 80f);
            cpuLabel.rectTransform.sizeDelta = new Vector2(620f, 80f);
            CanvasGroup playerGroup = playerLabel.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup cpuGroup = cpuLabel.gameObject.AddComponent<CanvasGroup>();

            BattleSkillCueView view = area.AddComponent<BattleSkillCueView>();
            TestBattleViews.SetField(view, "graphic", graphic);
            TestBattleViews.SetField(view, "playerLabel", playerLabel);
            TestBattleViews.SetField(view, "playerLabelGroup", playerGroup);
            TestBattleViews.SetField(view, "cpuLabel", cpuLabel);
            TestBattleViews.SetField(view, "cpuLabelGroup", cpuGroup);
            TestBattleViews.SetField(view, "playerPortrait", player);
            TestBattleViews.SetField(view, "cpuPortrait", cpu);
            TestBattleViews.SetField(view, "deltaTimeSource", (Func<float>)(() => Frame));
            view.ResetVisuals();

            Assert.That(view.HasRequiredReferences(), Is.True);

            parts = new Parts { Graphic = graphic, PlayerLabel = playerLabel, CpuLabel = cpuLabel, PlayerPortrait = player, CpuPortrait = cpu };

            return view;
        }

        private static void AssertCleared(BattleSkillCueView view, Parts parts)
        {
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(parts.Graphic.IsShowing, Is.False);
            Assert.That(parts.Graphic.Progress, Is.EqualTo(0f));
            Assert.That(parts.PlayerLabel.text, Is.Empty);
            Assert.That(parts.CpuLabel.text, Is.Empty);
            Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
            Assert.That(view.CpuLabelAlpha, Is.EqualTo(0f));
        }

        private static int Build(Action<VertexHelper> fill)
        {
            using (VertexHelper vh = new VertexHelper())
            {
                fill(vh);
                return vh.currentVertCount;
            }
        }

        private static void Run(IEnumerator routine, Action onFrame)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            int guard = 0;

            while (StepFrame(stack))
            {
                onFrame?.Invoke();

                Assert.That(++guard, Is.LessThan(600));
            }
        }

        private static bool StepFrame(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();

                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
