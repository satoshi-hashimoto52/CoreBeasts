using System;
using System.Collections;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 接触フラッシュの最大不透明度（決着エフェクトを覆わないための調整）。
    ///
    /// 最大値だけが従来の 1.0 より低い <see cref="BattleFxPlayer.FlashPeakAlpha"/> になり、
    /// 出る時刻・消えるまでの時間・減衰の形は変わらないことを確かめます。
    /// </summary>
    public sealed class BattleFlashPeakTests
    {
        private const float Frame = 1f / 60f;

        /// <summary>調整前の最大不透明度。</summary>
        private const float PreviousPeakAlpha = 1f;

        /// <summary>シーンと同じフラッシュ時間（flashSeconds）。</summary>
        private const float FlashSeconds = 0.12f;

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
        public void ThePeakIsAboutEightyPercentOfThePreviousValue()
        {
            Assert.That(BattleFxPlayer.FlashPeakAlpha, Is.LessThan(PreviousPeakAlpha));
            Assert.That(BattleFxPlayer.FlashPeakAlpha, Is.EqualTo(PreviousPeakAlpha * 0.8f).Within(0.0001f));
        }

        [TestCase(BattleImpactKind.Red)]
        [TestCase(BattleImpactKind.Blue)]
        [TestCase(BattleImpactKind.Green)]
        [TestCase(BattleImpactKind.Power)]
        [TestCase(BattleImpactKind.Core)]
        [TestCase(BattleImpactKind.Draw)]
        [TestCase(BattleImpactKind.None)]
        public void TheFlashNeverExceedsThePeakAndKeepsItsTiming(BattleImpactKind kind)
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            IEnumerator routine = kind == BattleImpactKind.None
                ? fx.PlayClashRoutine(RoundWinner.Player, Color.white)
                : Play(fx, kind);

            float max = 0f;
            float firstReleaseAlpha = -1f;
            int litFrames = 0;
            bool litBeforeRelease = false;
            List<float> releaseAlphas = new List<float>();

            PlayToEnd(routine, () =>
            {
                float alpha = parts.FlashGroup.alpha;

                max = Mathf.Max(max, alpha);

                if (fx.Step == BattleFxPlayer.ClashStep.Release)
                {
                    if (firstReleaseAlpha < 0f)
                    {
                        firstReleaseAlpha = alpha;
                    }

                    releaseAlphas.Add(alpha);
                }
                else if (alpha > 0f && firstReleaseAlpha < 0f)
                {
                    litBeforeRelease = true;
                }

                if (alpha > 0f)
                {
                    litFrames++;
                }
            });

            Assert.That(max, Is.LessThanOrEqualTo(BattleFxPlayer.FlashPeakAlpha + 0.0001f), kind + " が最大値を超えました。");
            Assert.That(max, Is.LessThan(PreviousPeakAlpha), kind + " は従来より弱くなります。");
            Assert.That(litBeforeRelease, Is.False, kind + " はヒットストップ明けまで光りません。");

            // 時間は変えません。1フレーム目は 1/60 秒ぶん減った値で、flashSeconds で消えます。
            float expectedFirst = BattleFxPlayer.FlashPeakAlpha * (1f - Frame / FlashSeconds);

            Assert.That(firstReleaseAlpha, Is.EqualTo(expectedFirst).Within(0.0001f), kind.ToString());
            Assert.That(litFrames * Frame, Is.EqualTo(FlashSeconds).Within(Frame * 1.01f), kind + " の点灯時間が変わりました。");

            // 減衰の形は従来どおり直線で、最大値に比例して縮んだだけです。
            for (int i = 0; i < releaseAlphas.Count; i++)
            {
                float elapsed = (i + 1) * Frame;
                float previous = PreviousPeakAlpha * (1f - Mathf.Clamp01(elapsed / FlashSeconds));

                Assert.That(
                    releaseAlphas[i],
                    Is.EqualTo(previous * BattleFxPlayer.FlashPeakAlpha).Within(0.0001f),
                    kind + " frame " + i);
            }

            Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f), kind + " の終了後に残っています。");
        }

        // ---------------- 補助 ----------------

        private static IEnumerator Play(BattleFxPlayer fx, BattleImpactKind kind)
        {
            switch (kind)
            {
                case BattleImpactKind.Red:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.AttributeAdvantage, UnitAttribute.Red, Color.red);

                case BattleImpactKind.Blue:
                    return fx.PlayClashRoutine(RoundWinner.Cpu, RoundDecision.AttributeAdvantage, UnitAttribute.Blue, Color.blue);

                case BattleImpactKind.Green:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.AttributeAdvantage, UnitAttribute.Green, Color.green);

                case BattleImpactKind.Power:
                    return fx.PlayClashRoutine(RoundWinner.Cpu, RoundDecision.PowerComparison, UnitAttribute.Red, Color.red);

                case BattleImpactKind.Core:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.CoreComparison, null, Color.green);

                default:
                    return fx.PlayClashRoutine(RoundWinner.Draw, RoundDecision.Draw, null, Color.white);
            }
        }

        private BattleFxPlayer CreateFx(out TestBattleViews.FxParts parts)
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out parts);

            TestBattleViews.SetField(fx, "deltaTimeSource", (Func<float>)(() => Frame));

            parts.CpuPortrait.localScale = new Vector3(-1f, 1f, 1f);
            fx.FxEnabled = true;

            return fx;
        }

        private static void PlayToEnd(IEnumerator routine, Action onFrame)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            int guard = 0;

            while (StepFrame(stack))
            {
                onFrame();

                Assert.That(++guard, Is.LessThan(600), "演出が終わりません。");
            }
        }

        /// <summary>入れ子の IEnumerator も Unity と同じ順で辿り、1フレーム進めます。</summary>
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
