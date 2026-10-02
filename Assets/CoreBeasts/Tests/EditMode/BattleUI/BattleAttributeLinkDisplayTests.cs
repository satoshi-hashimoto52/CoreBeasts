using System;
using System.Collections;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 4B（LINK の表示）と 4C（LINK の演出）の View・Graphic。
    ///
    /// 戦闘中の LINK 表示、結果理由の基礎値と加算値の分離、履歴の LINK マーカー、
    /// 立ち絵まわりのエネルギーと文字、FX OFF の短縮、中断時の完全な片付けを確かめます。
    /// </summary>
    public sealed class BattleAttributeLinkDisplayTests
    {
        private const float Frame = 1f / 60f;
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute G = UnitAttribute.Green;
        private const UnitAttribute B = UnitAttribute.Blue;

        private TestBattleViews views;
        private readonly FakeBattleText text = new FakeBattleText();

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
        }

        [TearDown]
        public void TearDown()
        {
            views.Cleanup();
        }

        private static AttributeLinkResult Link(int chain, params UnitAttribute[] shared)
        {
            int mask = 0;

            foreach (UnitAttribute a in shared)
            {
                mask |= AttributeLink.BitOf(a);
            }

            return new AttributeLinkResult(chain, AttributeLink.BonusFor(chain), mask);
        }

        // ---------------- 4B: 戦闘中の LINK 表示 ----------------

        [Test]
        public void TheCombatantShowsLinkBonusOnlyWhenTheLinkIsActive()
        {
            BattleCombatantView view = CreateCombatant(out TMP_Text label);

            view.ShowLink(AttributeLinkResult.None);
            Assert.That(label.gameObject.activeSelf, Is.False, "x1 では何も出しません。");
            Assert.That(label.text, Is.Empty);

            view.ShowLink(Link(2, R));
            Assert.That(label.gameObject.activeSelf, Is.True);
            Assert.That(label.text, Is.EqualTo("同じ属性で POWER +3"));

            view.ShowLink(Link(3, R));
            Assert.That(label.text, Is.EqualTo("同じ属性で POWER +6"));

            view.ShowLink(Link(5, R));
            Assert.That(label.text, Is.EqualTo("同じ属性で POWER +6"), "ボーナスは +6 が上限です。");

            view.ShowLink(Link(2, R, B));
            Assert.That(label.text, Is.EqualTo("同じ属性で POWER +3"), "2色とも共有してもボーナス表記は1回だけです。");

            // 途切れたら LOST などは出さず、消すだけです。
            view.ShowLink(AttributeLinkResult.None);
            Assert.That(label.gameObject.activeSelf, Is.False);
            Assert.That(label.text, Is.Empty);
        }

        [Test]
        public void ShowingAnotherUnitOrHidingTheStageClearsTheLink()
        {
            BattleCombatantView view = CreateCombatant(out TMP_Text label);

            view.ShowLink(Link(2, G));
            view.ShowHidden();
            Assert.That(label.gameObject.activeSelf, Is.False, "伏せた段に LINK を残しません。");

            view.ShowLink(Link(2, G));
            view.ShowEmpty();
            Assert.That(label.gameObject.activeSelf, Is.False);
            Assert.That(view.ShownLink, Is.EqualTo(AttributeLinkResult.None));
        }

        [Test]
        public void TheCombatantKeepsShowingTheBasePowerLine()
        {
            BattleCombatantView view = CreateCombatant(out _);
            TMP_Text power = (TMP_Text)GetField(view, "powerLabel");
            power.text = "POWER 54";
            string before = power.text;

            view.ShowLink(Link(3, R));

            Assert.That(power.text, Is.EqualTo(before), "基礎POWERの表示は変えず、LINK は別の表示に出します。");
        }

        // ---------------- 4B: 結果理由 ----------------

        [Test]
        public void APowerWinDecidedWithLinkSplitsBaseAndBonus()
        {
            RoundResult linked = Result(RoundWinner.Player, RoundDecision.PowerComparison, Link(2, R), AttributeLinkResult.None, 57, 50);

            Assert.That(BattleResultText.BuildDecision(linked, text), Is.EqualTo("POWER勝利  54<size=70%>（リンク+3）</size>対 50"));

            RoundResult cpuOnly = Result(RoundWinner.Cpu, RoundDecision.PowerComparison, AttributeLinkResult.None, Link(2, R), 50, 53);

            Assert.That(BattleResultText.BuildDecision(cpuOnly, text), Is.EqualTo("POWER勝利  50 対 50<size=70%>（リンク+3）</size>"),
                "CPU 側の加算は CPU の数値の後ろに付け、PLAYER 側へ加算されたように見せません。");

            RoundResult both = Result(RoundWinner.Cpu, RoundDecision.PowerComparison, Link(2, G), Link(3, G), 53, 56);

            Assert.That(BattleResultText.BuildDecision(both, text), Is.EqualTo("POWER勝利  50<size=70%>（リンク+3）</size>対 50<size=70%>（リンク+6）</size>"));
        }

        [Test]
        public void DecisionsWithoutALinkBonusKeepTheirExistingText()
        {
            Assert.That(
                BattleResultText.BuildDecision(Result(RoundWinner.Player, RoundDecision.PowerComparison, AttributeLinkResult.None, AttributeLinkResult.None, 60, 50), text),
                Is.EqualTo(text.PowerWin));

            Assert.That(
                BattleResultText.BuildDecision(Result(RoundWinner.Player, RoundDecision.AttributeAdvantage, Link(3, R), AttributeLinkResult.None, 0, 0), text),
                Is.EqualTo(text.AttributeWin), "属性勝ちは LINK で表記を変えません。");

            Assert.That(
                BattleResultText.BuildDecision(Result(RoundWinner.Draw, RoundDecision.Draw, Link(2, R), Link(2, R), 0, 0), text),
                Is.EqualTo(text.RoundDraw));
        }

        [Test]
        public void TheCatalogDefaultsUseTheConfirmedLinkTexts()
        {
            BattleTextCatalog catalog = ScriptableObject.CreateInstance<BattleTextCatalog>();

            try
            {
                AssertConfirmedLinkTexts(catalog, "初期値");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TheShippedCatalogAssetUsesTheConfirmedLinkTexts()
        {
            BattleTextCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(
                "Assets/CoreBeasts/Data/BattleTextCatalog_EN.asset");

            Assert.That(catalog, Is.Not.Null);
            AssertConfirmedLinkTexts(catalog, "BattleTextCatalog_EN.asset");

            // LINK 以外の文言は変えません。
            Assert.That(catalog.PowerWin, Is.EqualTo("POWER WIN"));
            Assert.That(catalog.AttributeWin, Is.EqualTo("ATTRIBUTE WIN"));
        }

        private static void AssertConfirmedLinkTexts(BattleTextCatalog catalog, string label)
        {
            Assert.That(catalog.FormatLinkBonus(3), Is.EqualTo("同じ属性で POWER +3"), label);
            Assert.That(catalog.FormatLinkBonus(6), Is.EqualTo("同じ属性で POWER +6"), label);
            Assert.That(catalog.FormatLinkCue(2, 3), Is.EqualTo("属性リンク 2連鎖！\nPOWER +3"), label);
            Assert.That(catalog.FormatLinkCue(3, 6), Is.EqualTo("属性リンク 3連鎖！\nPOWER +6"), label);
            Assert.That(catalog.FormatLinkCue(4, 6), Is.EqualTo("属性リンク 4連鎖！\nPOWER +6"), label + ": 4連鎖以上も実際の数を出し、ボーナスは +6。");
            Assert.That(catalog.FormatLinkedPower(54, 3), Is.EqualTo("54<size=70%>（リンク+3）</size>"), label);
            Assert.That(catalog.FormatLinkPowerDecision(catalog.FormatLinkedPower(54, 3), catalog.FormatUnlinkedPower(50)),
                Is.EqualTo("POWER勝利  54<size=70%>（リンク+3）</size>対 50"), label);
            Assert.That(catalog.FormatLinkPowerDecision(catalog.FormatUnlinkedPower(50), catalog.FormatLinkedPower(50, 6)),
                Is.EqualTo("POWER勝利  50 対 50<size=70%>（リンク+6）</size>"), label);
        }

        // ---------------- 4B: 履歴の LINK マーカー ----------------

        [Test]
        public void TheHistoryRecordsTheLinkFlag()
        {
            BattleHistoryModel history = new BattleHistoryModel();

            history.Append("a", 1, BattleSlotOutcome.Win, false);
            history.Append("b", 2, BattleSlotOutcome.Draw, true);
            history.Append("c", 3, BattleSlotOutcome.Loss);

            Assert.That(history.Entries[0].Linked, Is.False);
            Assert.That(history.Entries[1].Linked, Is.True);
            Assert.That(history.Entries[2].Linked, Is.False, "既存の入口は LINK なしです。");
        }

        [Test]
        public void TheMarkerGraphicMarksOnlyLinkedSlots()
        {
            GameObject lane = views.CreateObject("HistoryLane");
            lane.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 172f);

            RectTransform[] slots = new RectTransform[BattleHistoryModel.MaxEntries];

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = views.CreateObject("Slot" + i, lane.transform).GetComponent<RectTransform>();
                slots[i].sizeDelta = new Vector2(120f, 150f);
                slots[i].anchoredPosition = new Vector2(-420f + i * 140f, 0f);
            }

            GameObject markerObject = views.CreateObject("LinkMarkers", lane.transform);
            markerObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 172f);
            BattleHistoryLinkMarkerGraphic markers = markerObject.AddComponent<BattleHistoryLinkMarkerGraphic>();

            BattleHistoryModel history = new BattleHistoryModel();
            history.Append("a", 1, BattleSlotOutcome.Win, false);
            history.Append("b", 2, BattleSlotOutcome.Loss, true);
            history.Append("c", 3, BattleSlotOutcome.Draw, true);

            markers.SetMarkers(slots, history);

            Assert.That(markers.MarkerCount, Is.EqualTo(2));
            Assert.That(markers.IsMarked(0), Is.False);
            Assert.That(markers.IsMarked(1), Is.True);
            Assert.That(markers.IsMarked(2), Is.True);
            Assert.That(markers.IsMarked(3), Is.False, "未使用の枠には出しません。");
            Assert.That(Build(markers.FillMesh), Is.EqualTo(2 * BattleHistoryLinkMarkerGraphic.VerticesPerMarker));

            markers.Clear();

            Assert.That(markers.MarkerCount, Is.EqualTo(0));
            Assert.That(Build(markers.FillMesh), Is.EqualTo(0));
            Assert.That(markers.raycastTarget, Is.False);
        }

        // ---------------- 4C: 立ち絵まわりのエネルギー ----------------

        [Test]
        public void TheLinkGraphicDrawsEachSideWithinItsBudgetAndRadius()
        {
            AttributeLinkGraphic graphic = CreateLinkGraphic();

            graphic.Begin(
                true, graphic.rectTransform.TransformPoint(new Vector3(0f, -250f, 0f)), 150f, Color.red, Color.red, false,
                true, graphic.rectTransform.TransformPoint(new Vector3(0f, 250f, 0f)), 150f, Color.green, Color.blue, true);

            for (float p = 0f; p < 1f; p += 0.05f)
            {
                graphic.SetProgress(p);

                int count = Build(graphic.FillMesh);

                Assert.That(count, Is.EqualTo(AttributeLinkGraphic.VerticesPerSide * 2), "双方を同時に描きます。");
                Assert.That(count, Is.LessThanOrEqualTo(BattleAttributeLinkPresentationPlan.VertexBudget));
                Assert.That(graphic.LastMaxDistance, Is.LessThanOrEqualTo(BattleAttributeLinkPresentationPlan.MaxRadius + 0.01f));
            }

            graphic.SetProgress(1f);
            Assert.That(graphic.IsShowing, Is.False, "終わりまで進めると消えます。");
            Assert.That(Build(graphic.FillMesh), Is.EqualTo(0));
        }

        [Test]
        public void ASingleSharedColourUsesThatColourAndTwoSharedColoursUseBoth()
        {
            AttributeLinkGraphic graphic = CreateLinkGraphic();

            graphic.Begin(
                true, graphic.rectTransform.position, 150f, Color.red, Color.red, false,
                false, Vector3.zero, 0f, Color.white, Color.white, false);
            graphic.SetProgress(0.4f);

            HashSet<Color32> single = Colours(graphic);

            Assert.That(single.Count, Is.EqualTo(1), "1色共有ならその色だけです。");
            Assert.That(single.Contains((Color32)Color.red), Is.True);

            graphic.Begin(
                false, Vector3.zero, 0f, Color.white, Color.white, false,
                true, graphic.rectTransform.position, 150f, Color.green, Color.blue, true);
            graphic.SetProgress(0.4f);

            HashSet<Color32> dual = Colours(graphic);

            Assert.That(dual.Count, Is.EqualTo(2), "2色共有なら1色へ決めつけず、2色を使います。");
            Assert.That(dual.Contains((Color32)Color.green), Is.True);
            Assert.That(dual.Contains((Color32)Color.blue), Is.True);
            Assert.That(graphic.IsShowingCpu, Is.True);
            Assert.That(graphic.IsShowingPlayer, Is.False, "片方だけでも描けます。");
        }

        // ---------------- 4C: View（文字・FX OFF・中断） ----------------

        [Test]
        public void TheViewShowsBothSidesTogetherWithTheChainAndBonus()
        {
            BattleAttributeLinkView view = CreateLinkView(out Parts parts);

            BattleAttributeLinkPresentationPlan plan = BattleAttributeLinkPresentationPlan.Create(Link(2, R), Link(4, G, B), true);

            Vector2 playerPortrait = parts.PlayerPortrait.anchoredPosition;
            Vector2 cpuPortrait = parts.CpuPortrait.anchoredPosition;

            int frames = 0;
            bool sawBoth = false;

            Run(view.PlayRoutine(plan), () =>
            {
                frames++;

                Assert.That(parts.PlayerLabel.text, Is.EqualTo("属性リンク 2連鎖！\nPOWER +3"));
                Assert.That(parts.CpuLabel.text, Is.EqualTo("属性リンク 4連鎖！\nPOWER +6"), "4連鎖も実際の数を出し、ボーナスは +6。");

                sawBoth |= parts.Graphic.IsShowingPlayer && parts.Graphic.IsShowingCpu &&
                           view.PlayerLabelAlpha > 0f && view.CpuLabelAlpha > 0f;

                Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(playerPortrait), "立ち絵は動かしません。");
                Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(cpuPortrait));
            });

            Assert.That(sawBoth, Is.True, "PLAYER と CPU が同時に LINK したら同時に出します。");
            Assert.That(frames * Frame, Is.EqualTo(BattleAttributeLinkPresentationPlan.CueDuration).Within(Frame * 1.5f));
            AssertCleared(view, parts);
        }

        [Test]
        public void OnlyTheLinkedSideIsShown()
        {
            BattleAttributeLinkView view = CreateLinkView(out Parts parts);

            Run(view.PlayRoutine(BattleAttributeLinkPresentationPlan.Create(AttributeLinkResult.None, Link(2, B), true)), () =>
            {
                Assert.That(parts.PlayerLabel.text, Is.Empty, "LINK していない側は何も出しません。");
                Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
                Assert.That(parts.Graphic.IsShowingPlayer, Is.False);
            });

            AssertCleared(view, parts);
        }

        [Test]
        public void FxOffShowsOnlyShortTextWithoutGraphics()
        {
            BattleAttributeLinkView view = CreateLinkView(out Parts parts);
            BattleAttributeLinkPresentationPlan plan = BattleAttributeLinkPresentationPlan.Create(Link(3, R), AttributeLinkResult.None, false);

            int frames = 0;

            Run(view.PlayRoutine(plan), () =>
            {
                frames++;

                Assert.That(parts.Graphic.IsShowing, Is.False, "FX OFF では図形を出しません。");
                Assert.That(parts.PlayerLabel.text, Is.EqualTo("属性リンク 3連鎖！\nPOWER +6"));
                Assert.That(view.PlayerLabelAlpha, Is.EqualTo(1f));
            });

            Assert.That(frames * Frame, Is.EqualTo(BattleAttributeLinkPresentationPlan.TextOnlyDuration).Within(Frame * 1.5f));
            AssertCleared(view, parts);
        }

        [Test]
        public void ResettingMidCueClearsVerticesTextAndProgress()
        {
            BattleAttributeLinkView view = CreateLinkView(out Parts parts);
            Vector2 labelRest = parts.PlayerLabel.rectTransform.anchoredPosition;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(BattleAttributeLinkPresentationPlan.Create(Link(2, R), Link(2, G), true)));

            for (int i = 0; i < 6; i++)
            {
                StepFrame(stack);
            }

            Assert.That(view.IsPlaying, Is.True);
            Assert.That(parts.Graphic.IsShowing, Is.True);

            view.ResetVisuals();

            AssertCleared(view, parts);
            Assert.That(parts.PlayerLabel.rectTransform.anchoredPosition, Is.EqualTo(labelRest), "文字の位置も元へ戻します。");

            while (StepFrame(stack))
            {
                Assert.That(parts.Graphic.IsShowing, Is.False, "古いルーチンはもう書き込みません。");
                Assert.That(parts.PlayerLabel.text, Is.Empty);
            }
        }

        [Test]
        public void NothingIsPlayedWithoutALink()
        {
            BattleAttributeLinkPresentationPlan none = BattleAttributeLinkPresentationPlan.Create(AttributeLinkResult.None, AttributeLinkResult.None, true);

            Assert.That(none.ShowsAny, Is.False);
            Assert.That(none.Duration, Is.EqualTo(0f));

            BattleAttributeLinkView view = CreateLinkView(out Parts parts);

            Run(view.PlayRoutine(none), () => Assert.Fail("LINK が無いラウンドでは何も再生しません。"));

            AssertCleared(view, parts);
        }

        [Test]
        public void RepeatedCuesCreateNoObjectsOrComponents()
        {
            BattleAttributeLinkView view = CreateLinkView(out _);

            int transforms = view.GetComponentsInChildren<Transform>(true).Length;
            int components = view.GetComponentsInChildren<Component>(true).Length;

            for (int i = 0; i < 10; i++)
            {
                Run(view.PlayRoutine(BattleAttributeLinkPresentationPlan.Create(Link(2 + i % 3, R), Link(2, G, B), i % 2 == 0)), null);
            }

            Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
            Assert.That(view.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(components));
        }

        // ---------------- 補助 ----------------

        private struct Parts
        {
            internal AttributeLinkGraphic Graphic;
            internal TMP_Text PlayerLabel;
            internal TMP_Text CpuLabel;
            internal RectTransform PlayerPortrait;
            internal RectTransform CpuPortrait;
        }

        private BattleCombatantView CreateCombatant(out TMP_Text linkLabel)
        {
            BattleCombatantView view = views.CreateCombatant(out TestBattleViews.CombatantParts parts);

            linkLabel = views.CreateLabel("LinkLabel", view.transform);
            TestBattleViews.SetField(view, "linkLabel", linkLabel);
            view.Bind(null, null, text);

            return view;
        }

        private AttributeLinkGraphic CreateLinkGraphic()
        {
            GameObject go = views.CreateObject("LinkBurst");
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);

            return go.AddComponent<AttributeLinkGraphic>();
        }

        private BattleAttributeLinkView CreateLinkView(out Parts parts)
        {
            GameObject area = views.CreateObject("FxLink");
            area.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);

            GameObject stage = views.CreateObject("Stage");
            RectTransform player = views.CreateObject("PlayerPortrait", stage.transform).GetComponent<RectTransform>();
            RectTransform cpu = views.CreateObject("CpuPortrait", stage.transform).GetComponent<RectTransform>();
            player.sizeDelta = new Vector2(300f, 360f);
            cpu.sizeDelta = new Vector2(300f, 360f);
            player.anchoredPosition = new Vector2(200f, -280f);
            cpu.anchoredPosition = new Vector2(-200f, 290f);

            GameObject burst = views.CreateObject("LinkBurst", area.transform);
            burst.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);
            AttributeLinkGraphic graphic = burst.AddComponent<AttributeLinkGraphic>();

            TMP_Text playerLabel = views.CreateLabel("PlayerLinkLabel", area.transform);
            TMP_Text cpuLabel = views.CreateLabel("CpuLinkLabel", area.transform);
            CanvasGroup playerGroup = playerLabel.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup cpuGroup = cpuLabel.gameObject.AddComponent<CanvasGroup>();

            BattleAttributeLinkView view = area.AddComponent<BattleAttributeLinkView>();
            TestBattleViews.SetField(view, "graphic", graphic);
            TestBattleViews.SetField(view, "playerLabel", playerLabel);
            TestBattleViews.SetField(view, "playerLabelGroup", playerGroup);
            TestBattleViews.SetField(view, "cpuLabel", cpuLabel);
            TestBattleViews.SetField(view, "cpuLabelGroup", cpuGroup);
            TestBattleViews.SetField(view, "playerPortrait", player);
            TestBattleViews.SetField(view, "cpuPortrait", cpu);
            TestBattleViews.SetField(view, "deltaTimeSource", (Func<float>)(() => Frame));
            view.Bind(null, text);
            view.ResetVisuals();

            Assert.That(view.HasRequiredReferences(), Is.True);

            parts = new Parts
            {
                Graphic = graphic,
                PlayerLabel = playerLabel,
                CpuLabel = cpuLabel,
                PlayerPortrait = player,
                CpuPortrait = cpu,
            };

            return view;
        }

        private static void AssertCleared(BattleAttributeLinkView view, Parts parts)
        {
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(parts.Graphic.IsShowing, Is.False);
            Assert.That(parts.Graphic.Progress, Is.EqualTo(0f));
            Assert.That(parts.PlayerLabel.text, Is.Empty);
            Assert.That(parts.CpuLabel.text, Is.Empty);
            Assert.That(view.PlayerLabelAlpha, Is.EqualTo(0f));
            Assert.That(view.CpuLabelAlpha, Is.EqualTo(0f));
        }

        private static RoundResult Result(
            RoundWinner winner, RoundDecision decision, AttributeLinkResult playerLink, AttributeLinkResult cpuLink,
            int playerCompared, int cpuCompared)
        {
            BattleUnit p = new BattleUnit("p", R, 50);
            BattleUnit c = new BattleUnit("c", R, 50);

            return new RoundResult(1, p, c, winner, decision, R, playerLink, cpuLink, p, c, playerCompared, cpuCompared);
        }

        private static HashSet<Color32> Colours(AttributeLinkGraphic graphic)
        {
            HashSet<Color32> colours = new HashSet<Color32>();

            using (VertexHelper vh = new VertexHelper())
            {
                graphic.FillMesh(vh);

                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    UIVertex v = new UIVertex();
                    vh.PopulateUIVertex(ref v, i);

                    Color32 c = v.color;
                    c.a = 255;
                    colours.Add(c);
                }
            }

            return colours;
        }

        private static int Build(Action<VertexHelper> fill)
        {
            using (VertexHelper vh = new VertexHelper())
            {
                fill(vh);
                return vh.currentVertCount;
            }
        }

        private static object GetField(object target, string name)
        {
            return target.GetType()
                .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(target);
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
