using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 勝利コアが収束した瞬間を、画面へ正しく知らせるか（表示スコアの更新合図）。
    ///
    /// <see cref="VictoryCoreView.PlayRoutine"/>の onConverged は、光が消えて定位置へ収束しきった
    /// 同じフレームで1回だけ呼ばれ、中断されたときは呼ばれないことを確かめます。
    /// </summary>
    public sealed class BattleScoreRevealTests
    {
        private const float Frame = 1f / 60f;

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

        [Test]
        public void TheConvergenceIsReportedOnceInTheFrameTheGlowSettles()
        {
            VictoryCoreView view = CreateVictoryCore(out CoreBreakGraphic graphic);

            int calls = 0;
            int frames = 0;
            int calledAtFrame = -1;
            bool showingWhenCalled = true;
            bool playingWhenCalled = true;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(RoundWinner.Player, 1, 0.36f, () =>
            {
                calls++;
                calledAtFrame = frames;
                showingWhenCalled = graphic.IsShowing;
                playingWhenCalled = view.IsPlaying;
            }));

            while (StepFrame(stack))
            {
                frames++;

                Assert.That(calls, Is.EqualTo(0), "収束する前に知らせました。");
            }

            // 最後の MoveNext（yield しないで抜けたフレーム）で呼ばれます。
            Assert.That(calls, Is.EqualTo(1), "収束は1回だけ知らせます。");
            Assert.That(calledAtFrame, Is.EqualTo(frames), "収束したフレームで知らせます。");
            Assert.That(showingWhenCalled, Is.False, "光が消えて定位置へ収束した後です。");
            Assert.That(playingWhenCalled, Is.False);
            Assert.That((frames + 1) * Frame, Is.EqualTo(0.36f).Within(Frame * 1.01f), "時間は計画どおりです。");
        }

        [Test]
        public void AnInterruptedGlowNeverReportsConvergence()
        {
            VictoryCoreView view = CreateVictoryCore(out CoreBreakGraphic graphic);

            int calls = 0;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(RoundWinner.Cpu, 0, 0.36f, () => calls++));

            for (int i = 0; i < 5; i++)
            {
                StepFrame(stack);
            }

            Assert.That(graphic.IsShowing, Is.True);

            view.ResetVisuals();

            while (StepFrame(stack))
            {
            }

            Assert.That(calls, Is.EqualTo(0), "中断されたら知らせません（画面側が別の経路で追いつかせます）。");
        }

        [Test]
        public void TheGlowNeverChangesThePipItself()
        {
            VictoryCoreView view = CreateVictoryCore(out _, out Image[] pips);

            Color before = pips[1].color;
            Vector2 position = pips[1].rectTransform.anchoredPosition;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(RoundWinner.Player, 1, 0.36f, () => { }));

            while (StepFrame(stack))
            {
                Assert.That(pips[1].color, Is.EqualTo(before), "ピップの色は画面側が変えます。光は重ねるだけです。");
                Assert.That(pips[1].rectTransform.anchoredPosition, Is.EqualTo(position));
            }
        }

        // ---------------- 補助 ----------------

        private VictoryCoreView CreateVictoryCore(out CoreBreakGraphic graphic)
        {
            return CreateVictoryCore(out graphic, out _);
        }

        private VictoryCoreView CreateVictoryCore(out CoreBreakGraphic graphic, out Image[] playerPips)
        {
            GameObject header = views.CreateObject("Header");
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 214f);

            GameObject pipRoot = views.CreateObject("ScorePips", header.transform);
            playerPips = new Image[4];
            Image[] cpuPips = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                playerPips[i] = views.CreateImage("PipP" + (i + 1), pipRoot.transform);
                playerPips[i].rectTransform.sizeDelta = new Vector2(22f, 22f);
                playerPips[i].rectTransform.anchoredPosition = new Vector2(-120f + i * 30f, 0f);

                cpuPips[i] = views.CreateImage("PipC" + (i + 1), pipRoot.transform);
                cpuPips[i].rectTransform.sizeDelta = new Vector2(22f, 22f);
                cpuPips[i].rectTransform.anchoredPosition = new Vector2(30f + i * 30f, 0f);
            }

            BattleScorePipsView pips = pipRoot.AddComponent<BattleScorePipsView>();
            TestBattleViews.SetField(pips, "playerPips", playerPips);
            TestBattleViews.SetField(pips, "cpuPips", cpuPips);
            pips.Refresh(0, 0);

            GameObject root = views.CreateObject("VictoryCoreFx", header.transform);
            GameObject burst = views.CreateObject("VictoryCoreBurst", root.transform);
            burst.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 214f);

            graphic = burst.AddComponent<CoreBreakGraphic>();

            VictoryCoreView view = root.AddComponent<VictoryCoreView>();
            TestBattleViews.SetField(view, "scorePips", pips);
            TestBattleViews.SetField(view, "graphic", graphic);
            TestBattleViews.SetField(view, "deltaTimeSource", (Func<float>)(() => Frame));

            return view;
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
