using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 1「戦闘の手応え」の接触演出。
    ///
    /// フレーム時間を 1/60 秒に固定し、入れ子の Coroutine も含めて1フレームずつ進めます。
    /// ヒットストップ・加速・敗者だけのノックバック・勝者の着地・振動・完全復元を確かめます。
    /// </summary>
    public sealed class BattleFxImpactTests
    {
        private const float Frame = 1f / 60f;
        private const float Tolerance = 0.01f;

        private static readonly Vector2 PlayerRest = new Vector2(-18f, 0f);
        private static readonly Vector2 CpuRest = new Vector2(18f, 0f);

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

        /// <summary>1フレームぶんの記録。位置は振動を除いた値も控えます。</summary>
        private struct Sample
        {
            internal BattleFxPlayer.ClashStep Step;
            internal Vector2 Player;
            internal Vector2 Cpu;
            internal Vector2 Shake;
            internal float PlayerScale;
            internal float CpuScaleX;
            internal float PlayerAlpha;
            internal float CpuAlpha;
            internal float Flash;

            internal Vector2 PlayerMotion => Player - Shake;
            internal Vector2 CpuMotion => Cpu - Shake;
        }

        private BattleFxPlayer CreateFx(out TestBattleViews.FxParts parts)
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out parts);

            TestBattleViews.SetField(fx, "deltaTimeSource", (Func<float>)(() => Frame));

            // シーンと同じく、CPU側は左右反転し、位置も左右へ寄せてあります。
            parts.PlayerPortrait.anchoredPosition = PlayerRest;
            parts.CpuPortrait.anchoredPosition = CpuRest;
            parts.CpuPortrait.localScale = new Vector3(-1f, 1f, 1f);

            fx.FxEnabled = true;

            return fx;
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

        private static Sample Capture(BattleFxPlayer fx, TestBattleViews.FxParts parts)
        {
            return new Sample
            {
                Step = fx.Step,
                Player = parts.PlayerPortrait.anchoredPosition,
                Cpu = parts.CpuPortrait.anchoredPosition,
                Shake = fx.ShakeOffset,
                PlayerScale = parts.PlayerPortrait.localScale.y,
                CpuScaleX = parts.CpuPortrait.localScale.x,
                PlayerAlpha = parts.PlayerGroup.alpha,
                CpuAlpha = parts.CpuGroup.alpha,
                Flash = parts.FlashGroup.alpha,
            };
        }

        private static List<Sample> PlayToEnd(
            BattleFxPlayer fx,
            TestBattleViews.FxParts parts,
            RoundWinner winner,
            Action<Sample> onFrame = null)
        {
            List<Sample> samples = new List<Sample>();
            Stack<IEnumerator> stack = new Stack<IEnumerator>();

            stack.Push(fx.PlayClashRoutine(winner, Color.white));

            int guard = 0;

            while (StepFrame(stack))
            {
                Sample sample = Capture(fx, parts);

                samples.Add(sample);
                onFrame?.Invoke(sample);

                Assert.That(++guard, Is.LessThan(600), "演出が終わりません。");
            }

            return samples;
        }

        private static List<Sample> Of(List<Sample> samples, BattleFxPlayer.ClashStep step)
        {
            return samples.FindAll(s => s.Step == step);
        }

        /// <summary>接触位置からどれだけ後ろへ下がったか（振動を除く）。</summary>
        private static float PlayerRetreat(Sample s, Vector2 contact)
        {
            return contact.y - s.PlayerMotion.y;
        }

        private static float CpuRetreat(Sample s, Vector2 contact)
        {
            return s.CpuMotion.y - contact.y;
        }

        private static float MaxOf(List<Sample> samples, Func<Sample, float> value)
        {
            float max = float.MinValue;

            foreach (Sample s in samples)
            {
                max = Mathf.Max(max, value(s));
            }

            return max;
        }

        // シーンの既定値: liftOffset 14 / approach ±92
        private static readonly Vector2 PlayerContact = PlayerRest + new Vector2(0f, 14f + 92f);
        private static readonly Vector2 CpuContact = CpuRest + new Vector2(0f, -14f - 92f);

        // ---------------- 時系列 ----------------

        [Test]
        public void TheStepsRunInTheAgreedOrder()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> samples = PlayToEnd(fx, parts, RoundWinner.Player);

            List<BattleFxPlayer.ClashStep> order = new List<BattleFxPlayer.ClashStep>();

            foreach (Sample s in samples)
            {
                if (order.Count == 0 || order[order.Count - 1] != s.Step)
                {
                    order.Add(s.Step);
                }
            }

            Assert.That(
                order,
                Is.EqualTo(new[]
                {
                    BattleFxPlayer.ClashStep.Lift,
                    BattleFxPlayer.ClashStep.Approach,
                    BattleFxPlayer.ClashStep.ImpactHold,
                    BattleFxPlayer.ClashStep.Release,
                    BattleFxPlayer.ClashStep.Return,
                }));

            Assert.That(fx.Step, Is.EqualTo(BattleFxPlayer.ClashStep.Idle));
            Assert.That(fx.IsPlaying, Is.False);
        }

        [Test]
        public void TheWholeClashTakesThePlannedTime()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> samples = PlayToEnd(fx, parts, RoundWinner.Cpu);

            float planned = BattleRoundPresentationPlan.Create(RoundWinner.Cpu).ClashDuration;

            // 各段で最大1フレームの端数が出ます（5段 + ヒットストップ）。
            Assert.That(samples.Count * Frame, Is.InRange(planned, planned + 6f * Frame));
        }

        // ---------------- ヒットストップ ----------------

        [Test]
        public void TheImpactHoldFreezesBothPortraitsAtTheContactPoint()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> hold = Of(PlayToEnd(fx, parts, RoundWinner.Player), BattleFxPlayer.ClashStep.ImpactHold);

            Assert.That(hold.Count, Is.GreaterThan(0));

            foreach (Sample s in hold)
            {
                Assert.That(s.Player, Is.EqualTo(PlayerContact));
                Assert.That(s.Cpu, Is.EqualTo(CpuContact));
                Assert.That(s.Shake, Is.EqualTo(Vector2.zero), "止めている間は揺らしません。");
                Assert.That(s.Flash, Is.EqualTo(0f), "フラッシュはヒットストップ明けです。");
            }

            Assert.That(
                hold.Count * Frame,
                Is.InRange(0.06f, 0.06f + Frame),
                "ヒットストップは0.06秒です。");
        }

        [Test]
        public void TheApproachAcceleratesIntoTheContact()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> approach = Of(PlayToEnd(fx, parts, RoundWinner.Player), BattleFxPlayer.ClashStep.Approach);

            Assert.That(approach.Count, Is.GreaterThan(3));

            float previousStep = 0f;
            Vector2 previous = PlayerRest + new Vector2(0f, 14f);

            for (int i = 0; i < approach.Count; i++)
            {
                float step = approach[i].Player.y - previous.y;

                // 最後のフレームは端数を切り詰めるため、速度の単調増加は最後の1つ手前まで見ます。
                if (i < approach.Count - 1)
                {
                    Assert.That(step, Is.GreaterThan(previousStep), "frame " + i);
                }

                previousStep = step;
                previous = approach[i].Player;
            }

            Assert.That(approach[approach.Count - 1].Player, Is.EqualTo(PlayerContact));
        }

        // ---------------- 勝敗 ----------------

        [Test]
        public void APlayerWinKnocksBackOnlyTheCpuAndThePlayerLands()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> release = Of(PlayToEnd(fx, parts, RoundWinner.Player), BattleFxPlayer.ClashStep.Release);

            Assert.That(MaxOf(release, s => CpuRetreat(s, CpuContact)), Is.EqualTo(40f).Within(Tolerance));
            Assert.That(MaxOf(release, s => PlayerRetreat(s, PlayerContact)), Is.EqualTo(10f).Within(Tolerance));

            Sample landed = release[release.Count - 1];

            Assert.That(CpuRetreat(landed, CpuContact), Is.EqualTo(40f).Within(Tolerance), "敗者は押し戻されたままです。");
            Assert.That(PlayerRetreat(landed, PlayerContact), Is.EqualTo(5f).Within(Tolerance), "勝者は小さく反動して着地します。");
            Assert.That(landed.PlayerScale, Is.GreaterThan(1f));
            Assert.That(landed.CpuAlpha, Is.LessThan(1f));
            Assert.That(landed.PlayerAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void ACpuWinKnocksBackOnlyThePlayerAndTheCpuLands()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> release = Of(PlayToEnd(fx, parts, RoundWinner.Cpu), BattleFxPlayer.ClashStep.Release);

            Assert.That(MaxOf(release, s => PlayerRetreat(s, PlayerContact)), Is.EqualTo(40f).Within(Tolerance));
            Assert.That(MaxOf(release, s => CpuRetreat(s, CpuContact)), Is.EqualTo(10f).Within(Tolerance));

            Sample landed = release[release.Count - 1];

            Assert.That(PlayerRetreat(landed, PlayerContact), Is.EqualTo(40f).Within(Tolerance));
            Assert.That(CpuRetreat(landed, CpuContact), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(landed.PlayerAlpha, Is.LessThan(1f));
            Assert.That(landed.CpuAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void ADrawRecoilsBothSidesEquallyAndNeitherIsDimmed()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> release = Of(PlayToEnd(fx, parts, RoundWinner.Draw), BattleFxPlayer.ClashStep.Release);

            float playerMax = MaxOf(release, s => PlayerRetreat(s, PlayerContact));
            float cpuMax = MaxOf(release, s => CpuRetreat(s, CpuContact));

            Assert.That(playerMax, Is.EqualTo(18f).Within(Tolerance));
            Assert.That(cpuMax, Is.EqualTo(playerMax).Within(Tolerance), "片側だけを敗者扱いしません。");

            Sample landed = release[release.Count - 1];

            Assert.That(PlayerRetreat(landed, PlayerContact), Is.EqualTo(9f).Within(Tolerance));
            Assert.That(CpuRetreat(landed, CpuContact), Is.EqualTo(9f).Within(Tolerance));
            Assert.That(landed.PlayerAlpha, Is.EqualTo(1f));
            Assert.That(landed.CpuAlpha, Is.EqualTo(1f));
        }

        // ---------------- 振動 ----------------

        [Test]
        public void TheShakeOnlyHappensRightAfterTheImpactAndStaysSmall()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            List<Sample> samples = PlayToEnd(fx, parts, RoundWinner.Player);

            bool shook = false;

            foreach (Sample s in samples)
            {
                if (s.Shake != Vector2.zero)
                {
                    shook = true;

                    Assert.That(s.Step, Is.EqualTo(BattleFxPlayer.ClashStep.Release));
                    Assert.That(s.Shake.magnitude, Is.LessThanOrEqualTo(5f + 1e-3f));
                }
            }

            Assert.That(shook, Is.True, "振動対象は実際に動きます。");

            List<Sample> release = Of(samples, BattleFxPlayer.ClashStep.Release);
            int shaking = release.FindAll(s => s.Shake != Vector2.zero).Count;

            Assert.That(shaking * Frame, Is.InRange(0.12f - Frame, 0.12f + Frame), "振動は約0.12秒です。");
            Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TheShakeMovesOnlyThePortraitsNotTheirParentsOrSiblings()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            RectTransform root = (RectTransform)parts.PlayerPortrait.parent;
            RectTransform header = views.CreateObject("Header", root).GetComponent<RectTransform>();
            RectTransform score = views.CreateObject("Score", header).GetComponent<RectTransform>();
            RectTransform button = views.CreateObject("DeployButton", root).GetComponent<RectTransform>();

            header.anchoredPosition = new Vector2(0f, 300f);
            score.anchoredPosition = new Vector2(12f, -4f);
            button.anchoredPosition = new Vector2(0f, -300f);

            Vector2 rootRest = root.anchoredPosition;

            PlayToEnd(fx, parts, RoundWinner.Cpu, s =>
            {
                Assert.That(root.anchoredPosition, Is.EqualTo(rootRest));
                Assert.That(header.anchoredPosition, Is.EqualTo(new Vector2(0f, 300f)));
                Assert.That(score.anchoredPosition, Is.EqualTo(new Vector2(12f, -4f)));
                Assert.That(button.anchoredPosition, Is.EqualTo(new Vector2(0f, -300f)));
            });

            Assert.That(fx.PlayerShakeTarget, Is.SameAs(parts.PlayerPortrait));
            Assert.That(fx.CpuShakeTarget, Is.SameAs(parts.CpuPortrait));
        }

        [Test]
        public void TheShakeIsTheSameForAFixedSeed()
        {
            BattleFxPlayer first = CreateFx(out TestBattleViews.FxParts firstParts);
            first.ShakeSeed = 77;

            List<Vector2> a = PlayToEnd(first, firstParts, RoundWinner.Player).ConvertAll(s => s.Shake);

            BattleFxPlayer second = CreateFx(out TestBattleViews.FxParts secondParts);
            second.ShakeSeed = 77;

            List<Vector2> b = PlayToEnd(second, secondParts, RoundWinner.Player).ConvertAll(s => s.Shake);

            Assert.That(b, Is.EqualTo(a));
        }

        // ---------------- 復元 ----------------

        [Test]
        public void TheClashEndsBackAtTheRestPositions()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            PlayToEnd(fx, parts, RoundWinner.Player);

            Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(PlayerRest));
            Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(CpuRest));
            Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f));
            Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TenRoundsInARowNeverDriftThePose()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            RoundWinner[] winners = { RoundWinner.Player, RoundWinner.Cpu, RoundWinner.Draw };

            for (int round = 0; round < 10; round++)
            {
                PlayToEnd(fx, parts, winners[round % winners.Length]);

                // コントローラは結果表示の後で必ずこれを呼びます。
                fx.ResetVisuals();

                string label = "round " + (round + 1);

                Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(PlayerRest), label);
                Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(CpuRest), label);
                Assert.That(parts.PlayerPortrait.localScale, Is.EqualTo(Vector3.one), label);
                Assert.That(parts.CpuPortrait.localScale, Is.EqualTo(new Vector3(-1f, 1f, 1f)), label);
                Assert.That(parts.PlayerGroup.alpha, Is.EqualTo(1f), label);
                Assert.That(parts.CpuGroup.alpha, Is.EqualTo(1f), label);
                Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f), label);
                Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero), label);
            }
        }

        [Test]
        public void TheMirroredCpuPortraitStaysMirroredThroughoutTheClash()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            PlayToEnd(fx, parts, RoundWinner.Cpu, s =>
                Assert.That(s.CpuScaleX, Is.LessThan(0f)));
        }

        [Test]
        public void ResettingMidImpactRestoresEverythingAndStopsTheOldRoutine()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(fx.PlayClashRoutine(RoundWinner.Player, Color.white));

            // 振動とノックバックの最中まで進めます。
            int guard = 0;

            while (!(fx.Step == BattleFxPlayer.ClashStep.Release && fx.ShakeOffset != Vector2.zero))
            {
                Assert.That(StepFrame(stack), Is.True);
                Assert.That(++guard, Is.LessThan(200));
            }

            StepFrame(stack);

            Assert.That(parts.PlayerPortrait.anchoredPosition, Is.Not.EqualTo(PlayerRest));

            fx.ResetVisuals();

            AssertAtRest(fx, parts);

            // 古いルーチンを進めても、もう何も書き込みません。
            guard = 0;

            while (StepFrame(stack))
            {
                AssertAtRest(fx, parts);
                Assert.That(++guard, Is.LessThan(5), "打ち切ったルーチンがすぐに終わりません。");
            }

            AssertAtRest(fx, parts);
        }

        [Test]
        public void StartingASecondClashStopsTheFirstFromWriting()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            Stack<IEnumerator> first = new Stack<IEnumerator>();
            first.Push(fx.PlayClashRoutine(RoundWinner.Player, Color.white));

            for (int i = 0; i < 10; i++)
            {
                StepFrame(first);
            }

            Stack<IEnumerator> second = new Stack<IEnumerator>();
            second.Push(fx.PlayClashRoutine(RoundWinner.Cpu, Color.white));
            StepFrame(second);

            Vector2 afterSecond = parts.PlayerPortrait.anchoredPosition;

            int guard = 0;

            while (StepFrame(first))
            {
                Assert.That(++guard, Is.LessThan(5));
            }

            Assert.That(
                parts.PlayerPortrait.anchoredPosition,
                Is.EqualTo(afterSecond),
                "二重に走らせても、古い方は位置を奪いません。");
        }

        [Test]
        public void FxOffStillSkipsTheImpactEntirely()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);
            fx.FxEnabled = false;

            List<Sample> samples = PlayToEnd(fx, parts, RoundWinner.Player);

            Assert.That(samples.Count, Is.EqualTo(0), "FX OFF では1フレームも待ちません。");
            Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(PlayerRest));
            Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(CpuRest));
            Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(parts.PlayerPortrait.localScale.y, Is.GreaterThan(1f), "勝敗の差は残します。");
            Assert.That(parts.CpuGroup.alpha, Is.LessThan(1f));
        }

        // ---------------- 実装の約束 ----------------

        [Test]
        public void TheFxPlayerNeverTouchesTimeScale()
        {
            string[] sources =
            {
                "Assets/CoreBeasts/Scripts/Battle/UI/BattleFxPlayer.cs",
                "Assets/CoreBeasts/Scripts/Battle/UI/BattleScreenController.cs",
                "Assets/CoreBeasts/Scripts/Battle/BattleRoundPresentationPlan.cs",
            };

            foreach (string path in sources)
            {
                string code = File.ReadAllText(path);

                // コメントでの言及は許し、代入・参照の形だけを禁じます。
                Assert.That(code.Contains("Time.timeScale ="), Is.False, path);
                Assert.That(code.Contains("Time.timeScale;"), Is.False, path);
                Assert.That(code.Contains("Time.deltaTime"), Is.False, path);
            }
        }

        private static void AssertAtRest(BattleFxPlayer fx, TestBattleViews.FxParts parts)
        {
            Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(PlayerRest));
            Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(CpuRest));
            Assert.That(parts.PlayerPortrait.localScale, Is.EqualTo(Vector3.one));
            Assert.That(parts.CpuPortrait.localScale, Is.EqualTo(new Vector3(-1f, 1f, 1f)));
            Assert.That(parts.PlayerGroup.alpha, Is.EqualTo(1f));
            Assert.That(parts.CpuGroup.alpha, Is.EqualTo(1f));
            Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f));
            Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(fx.IsPlaying, Is.False);
            Assert.That(fx.Step, Is.EqualTo(BattleFxPlayer.ClashStep.Idle));
        }
    }
}
