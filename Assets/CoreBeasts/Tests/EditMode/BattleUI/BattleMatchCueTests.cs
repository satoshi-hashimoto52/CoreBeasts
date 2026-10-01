using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 3 の表示（<see cref="CoreBreakGraphic"/> / <see cref="VictoryCoreView"/> / <see cref="BattleMatchCueView"/>）。
    ///
    /// 頂点を直接読み、モードごとの頂点数・広がり・フラッシュの上限を確かめます。
    /// View はフレーム時間を 1/60 秒に固定し、終了・中断で必ず初期状態へ戻ること、
    /// 勝利コア獲得でピップ自体が動かないこと、FX OFF では文字だけになることを確かめます。
    /// </summary>
    public sealed class BattleMatchCueTests
    {
        private const float Frame = 1f / 60f;

        private static readonly CoreBreakGraphic.Mode[] Modes =
        {
            CoreBreakGraphic.Mode.VictoryCore,
            CoreBreakGraphic.Mode.FinalCore,
            CoreBreakGraphic.Mode.CoreBreak,
        };

        private TestBattleViews views;

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

        // ---------------- CoreBreakGraphic ----------------

        [Test]
        public void EveryModeBuildsItsFixedVertexCountWithinTheBudgetAndRadius()
        {
            CoreBreakGraphic graphic = CreateGraphic();

            Assert.That(CoreBreakGraphic.VerticesOf(CoreBreakGraphic.Mode.VictoryCore), Is.EqualTo(49));
            Assert.That(CoreBreakGraphic.VerticesOf(CoreBreakGraphic.Mode.FinalCore), Is.EqualTo(128));
            Assert.That(CoreBreakGraphic.VerticesOf(CoreBreakGraphic.Mode.CoreBreak), Is.EqualTo(158));

            foreach (CoreBreakGraphic.Mode mode in Modes)
            {
                Vector3 centre = graphic.rectTransform.TransformPoint(new Vector3(20f, -30f, 0f));
                Vector3 edge = graphic.rectTransform.TransformPoint(new Vector3(20f + 18f, -30f, 0f));

                graphic.Begin(mode, centre, edge);

                for (float p = 0f; p < 1f; p += 0.02f)
                {
                    graphic.SetProgress(p);

                    List<UIVertex> vertices = Build(graphic);
                    string label = mode + " @" + p.ToString("0.00");

                    Assert.That(vertices.Count, Is.EqualTo(CoreBreakGraphic.VerticesOf(mode)), label);
                    Assert.That(vertices.Count, Is.LessThanOrEqualTo(BattleMatchPresentationPlan.VertexBudget), label);
                    Assert.That(graphic.LastMaxDistance, Is.LessThanOrEqualTo(graphic.MaxRadiusOf(mode) + 0.01f), label);
                }
            }
        }

        [Test]
        public void TheVictoryCoreGrowsSlightlyThenSettlesOntoThePip()
        {
            CoreBreakGraphic graphic = CreateGraphic();

            graphic.Begin(
                CoreBreakGraphic.Mode.VictoryCore,
                graphic.rectTransform.position,
                graphic.rectTransform.TransformPoint(new Vector3(10f, 0f, 0f)));

            Assert.That(graphic.BaseRadius, Is.EqualTo(10f).Within(0.01f));

            float max = 0f;
            float last = 0f;

            for (float p = 0f; p < 1f; p += 0.02f)
            {
                graphic.SetProgress(p);
                Build(graphic);

                max = Mathf.Max(max, graphic.LastMaxDistance);
                last = graphic.LastMaxDistance;
            }

            Assert.That(max, Is.EqualTo(10f * BattleMatchPresentationPlan.VictoryCorePeakScale).Within(0.2f), "軽く拡大します。");
            Assert.That(last, Is.LessThan(10.5f), "最後はピップの大きさへ収束します。");
        }

        [Test]
        public void TheCoreBreakFlashNeverExceedsItsPeakAndTheCoreEndsGone()
        {
            CoreBreakGraphic graphic = CreateGraphic();
            int flashVertices = 17;

            graphic.Begin(CoreBreakGraphic.Mode.CoreBreak, graphic.rectTransform.position);

            float flashMax = 0f;
            float coreEndAlpha = 1f;

            for (float p = 0f; p < 1f; p += 0.01f)
            {
                graphic.SetProgress(p);

                List<UIVertex> vertices = Build(graphic);

                for (int i = vertices.Count - flashVertices; i < vertices.Count; i++)
                {
                    flashMax = Mathf.Max(flashMax, ((Color)vertices[i].color).a);
                }

                coreEndAlpha = ((Color)vertices[BreakCoreCentreIndex()].color).a;
            }

            Assert.That(flashMax, Is.GreaterThan(0.5f), "白金フラッシュが出ます。");
            Assert.That(flashMax, Is.LessThanOrEqualTo(BattleMatchPresentationPlan.CoreBreakFlashPeakAlpha + 0.005f));
            Assert.That(coreEndAlpha, Is.LessThan(0.05f), "中心コアは消滅します。");
        }

        [Test]
        public void HidingOrDisablingLeavesNothingDrawn()
        {
            CoreBreakGraphic graphic = CreateGraphic();

            foreach (CoreBreakGraphic.Mode mode in Modes)
            {
                graphic.Begin(mode, graphic.rectTransform.position, graphic.rectTransform.TransformPoint(Vector3.right * 8f));
                graphic.SetProgress(0.5f);
                Assert.That(Build(graphic).Count, Is.GreaterThan(0));

                graphic.SetProgress(1f);
                Assert.That(graphic.IsShowing, Is.False, mode + " は終わりまで進めると消えます。");
                Assert.That(Build(graphic).Count, Is.EqualTo(0));
            }

            graphic.Begin(CoreBreakGraphic.Mode.CoreBreak, graphic.rectTransform.position);
            graphic.SetProgress(0.3f);
            Build(graphic);

            graphic.gameObject.SetActive(false);

            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(graphic.LastVertexCount, Is.EqualTo(0));

            graphic.gameObject.SetActive(true);

            Assert.That(Build(graphic).Count, Is.EqualTo(0), "再表示で前の続きは出ません。");

            graphic.Begin(CoreBreakGraphic.Mode.None, graphic.rectTransform.position);
            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(graphic.raycastTarget, Is.False);
        }

        // ---------------- VictoryCoreView ----------------

        [Test]
        public void TheVictoryCoreHighlightsOnlyTheNewPipAndNeverMovesAnyPip()
        {
            VictoryCoreView view = CreateVictoryCore(out BattleScorePipsView pips, out CoreBreakGraphic graphic, out Image[] playerPips, out Image[] cpuPips);

            pips.Refresh(2, 1);

            List<string> before = Snapshot(playerPips, cpuPips);
            RectTransform target = playerPips[1].rectTransform;
            Vector3 expectedCentre = target.TransformPoint(target.rect.center);

            bool sawCentre = false;

            Run(view.PlayRoutine(RoundWinner.Player, 1, 0.36f), () =>
            {
                Assert.That(Snapshot(playerPips, cpuPips), Is.EqualTo(before), "ピップ自体の位置・大きさ・色は変わりません。");

                if (graphic.IsShowing)
                {
                    Vector3 centre = graphic.rectTransform.TransformPoint(graphic.Centre);

                    Assert.That((centre - expectedCentre).magnitude, Is.LessThan(0.01f), "新しいピップの位置です。");
                    Assert.That(graphic.Current, Is.EqualTo(CoreBreakGraphic.Mode.VictoryCore));
                    sawCentre = true;
                }
            });

            Assert.That(sawCentre, Is.True);
            Assert.That(view.LastPip, Is.SameAs(target));
            Assert.That(graphic.IsShowing, Is.False, "終了後に残りません。");
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(Snapshot(playerPips, cpuPips), Is.EqualTo(before));
        }

        [Test]
        public void TheVictoryCoreIgnoresMissingPipsAndStopsOnReset()
        {
            VictoryCoreView view = CreateVictoryCore(out _, out CoreBreakGraphic graphic, out _, out _);

            Run(view.PlayRoutine(RoundWinner.Draw, 0, 0.36f), () => Assert.That(graphic.IsShowing, Is.False));
            Run(view.PlayRoutine(RoundWinner.Player, 9, 0.36f), () => Assert.That(graphic.IsShowing, Is.False));

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(RoundWinner.Cpu, 0, 0.36f));

            StepFrame(stack);
            StepFrame(stack);
            Assert.That(graphic.IsShowing, Is.True);

            // EditMode では MonoBehaviour の OnDisable が呼ばれないため、同じ処理（ResetVisuals）を直接呼びます。
            // 無効化の経路は PlayMode で確かめます。
            view.ResetVisuals();

            Assert.That(graphic.IsShowing, Is.False, "中断で即座に消えます。");
            Assert.That(view.IsPlaying, Is.False);

            while (StepFrame(stack))
            {
                Assert.That(graphic.IsShowing, Is.False, "古いルーチンはもう書き込みません。");
            }
        }

        // ---------------- BattleMatchCueView ----------------

        [TestCase(MatchCueKind.FinalCore, BattleMatchCueView.FinalCoreText, CoreBreakGraphic.Mode.FinalCore)]
        [TestCase(MatchCueKind.CoreBreak, BattleMatchCueView.CoreBreakText, CoreBreakGraphic.Mode.CoreBreak)]
        public void FxOnShowsTheRingAndTheTextThenClearsEverything(MatchCueKind kind, string text, CoreBreakGraphic.Mode mode)
        {
            BattleMatchCueView view = CreateCue(out CoreBreakGraphic graphic, out TMP_Text label, out CanvasGroup group);
            RectTransform area = (RectTransform)view.transform;
            Vector3 expectedCentre = area.TransformPoint(area.rect.center);

            float duration = kind == MatchCueKind.FinalCore
                ? BattleMatchPresentationPlan.FinalCoreDuration
                : BattleMatchPresentationPlan.FullCoreBreakDuration;

            int frames = 0;
            float maxAlpha = 0f;

            Run(view.PlayRoutine(kind, true, duration), () =>
            {
                frames++;

                Assert.That(view.Current, Is.EqualTo(kind));
                Assert.That(label.text, Is.EqualTo(text));

                if (graphic.IsShowing)
                {
                    Assert.That(graphic.Current, Is.EqualTo(mode));
                    Assert.That((graphic.rectTransform.TransformPoint(graphic.Centre) - expectedCentre).magnitude, Is.LessThan(0.01f), "戦闘表示領域の中央です。");
                }

                maxAlpha = Mathf.Max(maxAlpha, group.alpha);
            });

            Assert.That(frames * Frame, Is.EqualTo(duration).Within(Frame * 1.5f));
            Assert.That(maxAlpha, Is.GreaterThan(0.9f), "文字が見えます。");
            AssertCleared(view, graphic, label, group);
        }

        [TestCase(MatchCueKind.FinalCore, BattleMatchCueView.FinalCoreText)]
        [TestCase(MatchCueKind.CoreBreak, BattleMatchCueView.CoreBreakText)]
        public void FxOffShowsOnlyTheShortText(MatchCueKind kind, string text)
        {
            BattleMatchCueView view = CreateCue(out CoreBreakGraphic graphic, out TMP_Text label, out CanvasGroup group);

            int frames = 0;

            Run(view.PlayRoutine(kind, false, BattleMatchPresentationPlan.TextOnlyDuration), () =>
            {
                frames++;

                Assert.That(graphic.IsShowing, Is.False, "FX OFF ではリング・亀裂・フラッシュを出しません。");
                Assert.That(label.text, Is.EqualTo(text));
                Assert.That(group.alpha, Is.EqualTo(1f));
            });

            Assert.That(frames * Frame, Is.EqualTo(BattleMatchPresentationPlan.TextOnlyDuration).Within(Frame * 1.5f));
            AssertCleared(view, graphic, label, group);
        }

        [Test]
        public void ResettingMidCueRestoresTheRestState()
        {
            // EditMode では MonoBehaviour の OnDisable が呼ばれないため、無効化の経路は PlayMode で確かめます。
            BattleMatchCueView view = CreateCue(out CoreBreakGraphic graphic, out TMP_Text label, out CanvasGroup group);
            Stack<IEnumerator> stack = new Stack<IEnumerator>();

            stack.Push(view.PlayRoutine(MatchCueKind.CoreBreak, true, BattleMatchPresentationPlan.FullCoreBreakDuration));

            for (int i = 0; i < 40; i++)
            {
                StepFrame(stack);
            }

            Assert.That(view.IsPlaying, Is.True);
            Assert.That(graphic.IsShowing, Is.True);
            Assert.That(label.text, Is.EqualTo(BattleMatchCueView.CoreBreakText));

            view.ResetVisuals();

            AssertCleared(view, graphic, label, group);

            while (StepFrame(stack))
            {
                Assert.That(graphic.IsShowing, Is.False, "古いルーチンはもう書き込みません。");
                Assert.That(label.text, Is.Empty);
            }

            AssertCleared(view, graphic, label, group);
        }

        [Test]
        public void RepeatedCuesCreateNoObjectsOrComponents()
        {
            BattleMatchCueView view = CreateCue(out _, out _, out _);

            int transforms = view.GetComponentsInChildren<Transform>(true).Length;
            int components = view.GetComponentsInChildren<Component>(true).Length;

            for (int i = 0; i < 10; i++)
            {
                MatchCueKind kind = i % 2 == 0 ? MatchCueKind.FinalCore : MatchCueKind.CoreBreak;

                Run(view.PlayRoutine(kind, i % 3 != 0, 0.5f), null);
            }

            Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
            Assert.That(view.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(components));
            Assert.That(view.FinalCorePlays, Is.EqualTo(5));
            Assert.That(view.CoreBreakPlays, Is.EqualTo(5));

            view.ResetCounters();

            Assert.That(view.FinalCorePlays, Is.EqualTo(0));
        }

        // ---------------- Phase 1・2 を変えない ----------------

        [Test]
        public void ThePhase2FlashPeakIsUnchanged()
        {
            Assert.That(BattleFxPlayer.FlashPeakAlpha, Is.EqualTo(0.8f));
        }

        // ---------------- 補助 ----------------

        private static int BreakCoreCentreIndex()
        {
            // CoreBreak の頂点順: 圧縮リング(64) → 中心コア円盤（中心1点が先頭）。
            return 64;
        }

        private CoreBreakGraphic CreateGraphic(Transform parent = null)
        {
            GameObject go = views.CreateObject("CoreBreak", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 900f);

            return go.AddComponent<CoreBreakGraphic>();
        }

        private VictoryCoreView CreateVictoryCore(
            out BattleScorePipsView pips,
            out CoreBreakGraphic graphic,
            out Image[] playerPips,
            out Image[] cpuPips)
        {
            GameObject header = views.CreateObject("Header");
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 214f);

            GameObject pipRoot = views.CreateObject("ScorePips", header.transform);
            playerPips = new Image[4];
            cpuPips = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                playerPips[i] = views.CreateImage("PipP" + (i + 1), pipRoot.transform);
                playerPips[i].rectTransform.sizeDelta = new Vector2(22f, 22f);
                playerPips[i].rectTransform.anchoredPosition = new Vector2(-120f + i * 30f, 0f);

                cpuPips[i] = views.CreateImage("PipC" + (i + 1), pipRoot.transform);
                cpuPips[i].rectTransform.sizeDelta = new Vector2(22f, 22f);
                cpuPips[i].rectTransform.anchoredPosition = new Vector2(30f + i * 30f, 0f);
            }

            pips = pipRoot.AddComponent<BattleScorePipsView>();
            TestBattleViews.SetField(pips, "playerPips", playerPips);
            TestBattleViews.SetField(pips, "cpuPips", cpuPips);

            GameObject root = views.CreateObject("VictoryCoreFx", header.transform);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject burst = views.CreateObject("VictoryCoreBurst", root.transform);
            RectTransform burstRect = burst.GetComponent<RectTransform>();
            burstRect.anchorMin = Vector2.zero;
            burstRect.anchorMax = Vector2.one;
            burstRect.offsetMin = Vector2.zero;
            burstRect.offsetMax = Vector2.zero;

            graphic = burst.AddComponent<CoreBreakGraphic>();

            VictoryCoreView view = root.AddComponent<VictoryCoreView>();
            TestBattleViews.SetField(view, "scorePips", pips);
            TestBattleViews.SetField(view, "graphic", graphic);
            TestBattleViews.SetField(view, "deltaTimeSource", (Func<float>)(() => Frame));

            Assert.That(view.HasRequiredReferences(), Is.True);

            return view;
        }

        private BattleMatchCueView CreateCue(out CoreBreakGraphic graphic, out TMP_Text label, out CanvasGroup group)
        {
            GameObject root = views.CreateObject("FxMatchCue");
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);

            GameObject burst = views.CreateObject("CueBurst", root.transform);
            burst.GetComponent<RectTransform>().sizeDelta = new Vector2(1032f, 951f);
            graphic = burst.AddComponent<CoreBreakGraphic>();

            GameObject text = views.CreateObject("CueLabel", root.transform);
            label = text.AddComponent<TextMeshProUGUI>();
            group = text.AddComponent<CanvasGroup>();

            BattleMatchCueView view = root.AddComponent<BattleMatchCueView>();
            TestBattleViews.SetField(view, "graphic", graphic);
            TestBattleViews.SetField(view, "label", label);
            TestBattleViews.SetField(view, "labelGroup", group);
            TestBattleViews.SetField(view, "deltaTimeSource", (Func<float>)(() => Frame));

            view.ResetVisuals();

            Assert.That(view.HasRequiredReferences(), Is.True);

            return view;
        }

        private static void AssertCleared(BattleMatchCueView view, CoreBreakGraphic graphic, TMP_Text label, CanvasGroup group)
        {
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.Current, Is.EqualTo(MatchCueKind.None));
            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(graphic.LastVertexCount == 0 || Build(graphic).Count == 0, Is.True);
            Assert.That(label.text, Is.Empty);
            Assert.That(group.alpha, Is.EqualTo(0f));
        }

        private static List<string> Snapshot(Image[] playerPips, Image[] cpuPips)
        {
            List<string> state = new List<string>();

            foreach (Image[] side in new[] { playerPips, cpuPips })
            {
                foreach (Image pip in side)
                {
                    RectTransform r = pip.rectTransform;

                    state.Add(r.anchoredPosition.ToString("F4") + r.localScale.ToString("F4") + r.sizeDelta.ToString("F4") + pip.color);
                }
            }

            return state;
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

        private static List<UIVertex> Build(CoreBreakGraphic graphic)
        {
            List<UIVertex> vertices = new List<UIVertex>();

            using (VertexHelper vh = new VertexHelper())
            {
                graphic.FillMesh(vh);

                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    UIVertex v = new UIVertex();
                    vh.PopulateUIVertex(ref v, i);
                    vertices.Add(v);
                }
            }

            return vertices;
        }
    }
}
