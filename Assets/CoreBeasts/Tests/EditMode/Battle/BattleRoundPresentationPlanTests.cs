using System;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// Phase 1「戦闘の手応え」の演出設計値。Unityへ依存しないため、そのまま実行できます。
    ///
    /// ヒットストップの長さ、勝敗ごとのノックバック対象、振動の上限を固定します。
    /// FINAL CORE・CORE BREAK・Reduced/Off を持ち込んでいないことも確かめます。
    /// Phase 2 の属性別インパクトは<see cref="BattleImpactPlanTests"/>が確かめます。
    /// </summary>
    public sealed class BattleRoundPresentationPlanTests
    {
        private static readonly RoundWinner[] AllWinners =
        {
            RoundWinner.Player,
            RoundWinner.Cpu,
            RoundWinner.Draw,
        };

        // ---------------- ヒットストップ ----------------

        [Test]
        public void TheImpactHoldStaysBetween40And80Milliseconds()
        {
            foreach (RoundWinner winner in AllWinners)
            {
                BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(winner);

                Assert.That(
                    plan.ImpactHoldDuration,
                    Is.InRange(0.04f, 0.08f),
                    winner + " のヒットストップが範囲外です。");
            }
        }

        [Test]
        public void TheStandardImpactHoldIs60Milliseconds()
        {
            Assert.That(
                BattleRoundPresentationPlan.StandardImpactHoldDuration,
                Is.EqualTo(0.06f).Within(1e-6f));

            Assert.That(
                BattleRoundPresentationPlan.Create(RoundWinner.Player).ImpactHoldDuration,
                Is.EqualTo(0.06f).Within(1e-6f));
        }

        [Test]
        public void TheImpactHoldLimitsAreTheAgreedValues()
        {
            Assert.That(BattleRoundPresentationPlan.MinImpactHoldDuration, Is.EqualTo(0.04f));
            Assert.That(BattleRoundPresentationPlan.MaxImpactHoldDuration, Is.EqualTo(0.08f));
        }

        // ---------------- 尺 ----------------

        [Test]
        public void EveryPhaseHasAPositiveDuration()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Cpu);

            Assert.That(plan.LiftDuration, Is.GreaterThan(0f));
            Assert.That(plan.ApproachDuration, Is.GreaterThan(0f));
            Assert.That(plan.ImpactHoldDuration, Is.GreaterThan(0f));
            Assert.That(plan.KnockbackDuration, Is.GreaterThan(0f));
            Assert.That(plan.LandingDuration, Is.GreaterThan(0f));
            Assert.That(plan.ReturnDuration, Is.GreaterThan(0f));
            Assert.That(plan.ShakeDuration, Is.GreaterThan(0f));
        }

        [Test]
        public void ThePhase1ClashFitsBetween0Point8And1Second()
        {
            foreach (RoundWinner winner in AllWinners)
            {
                BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(winner);

                Assert.That(plan.ClashDuration, Is.InRange(0.8f, 1.0f), winner.ToString());

                Assert.That(
                    plan.ClashDuration,
                    Is.EqualTo(
                        plan.LiftDuration +
                        plan.ApproachDuration +
                        plan.ImpactHoldDuration +
                        plan.KnockbackDuration +
                        plan.LandingDuration +
                        plan.ReturnDuration).Within(1e-5f));
            }
        }

        [Test]
        public void TheImpactHoldIsShorterThanTheApproach()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            Assert.That(
                plan.ImpactHoldDuration,
                Is.LessThan(plan.ApproachDuration),
                "止めている時間が突進より長いと、衝撃ではなく停止に見えます。");
        }

        [Test]
        public void TheShakeEndsBeforeTheLandingFinishes()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            Assert.That(plan.ShakeDuration, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(plan.ShakeDuration, Is.LessThanOrEqualTo(plan.ReleaseDuration));
        }

        // ---------------- 勝敗とノックバック ----------------

        [Test]
        public void APlayerWinKnocksBackOnlyTheCpu()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            Assert.That(plan.CpuKnockback, Is.True);
            Assert.That(plan.PlayerKnockback, Is.False);
            Assert.That(plan.KnockbackEnabled, Is.True);
            Assert.That(plan.CpuDisplacement, Is.GreaterThan(plan.PlayerDisplacement));
        }

        [Test]
        public void ACpuWinKnocksBackOnlyThePlayer()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Cpu);

            Assert.That(plan.PlayerKnockback, Is.True);
            Assert.That(plan.CpuKnockback, Is.False);
            Assert.That(plan.KnockbackEnabled, Is.True);
            Assert.That(plan.PlayerDisplacement, Is.GreaterThan(plan.CpuDisplacement));
        }

        [Test]
        public void ADrawNeverTreatsOnlyOneSideAsTheLoser()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Draw);

            Assert.That(plan.IsDraw, Is.True);
            Assert.That(plan.PlayerKnockback, Is.EqualTo(plan.CpuKnockback));
            Assert.That(plan.KnockbackEnabled, Is.False, "引き分けではどちらも敗者扱いしません。");
            Assert.That(plan.PlayerDisplacement, Is.EqualTo(plan.CpuDisplacement));

            Assert.That(
                plan.LandedDisplacement(RoundWinner.Player),
                Is.EqualTo(plan.LandedDisplacement(RoundWinner.Cpu)));
        }

        [Test]
        public void TheLoserStaysPushedBackWhileTheWinnerLands()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            Assert.That(plan.LandedDisplacement(RoundWinner.Cpu), Is.EqualTo(plan.CpuDisplacement));

            Assert.That(
                plan.LandedDisplacement(RoundWinner.Player),
                Is.LessThan(plan.PlayerDisplacement),
                "勝者は反動の一部を戻して着地します。");
        }

        [Test]
        public void TheKnockbackDistanceIsTheAgreedValue()
        {
            Assert.That(BattleRoundPresentationPlan.StandardKnockbackDistance, Is.EqualTo(40f));
            Assert.That(BattleRoundPresentationPlan.StandardWinnerRecoilDistance, Is.EqualTo(10f));
            Assert.That(BattleRoundPresentationPlan.StandardDrawRecoilDistance, Is.EqualTo(18f));
        }

        // ---------------- 振動 ----------------

        [Test]
        public void TheShakeAmplitudeStaysWithinTheLimit()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Draw);

            Assert.That(plan.ShakeEnabled, Is.True);
            Assert.That(plan.ShakeAmplitude, Is.InRange(4f, 6f));
            Assert.That(
                plan.ShakeAmplitude,
                Is.LessThanOrEqualTo(BattleRoundPresentationPlan.MaxShakeAmplitude));
        }

        [Test]
        public void NoShakeSampleExceedsTheAmplitude()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            for (int i = 0; i <= 600; i++)
            {
                float t = i / 3000f;

                plan.SampleShake(t, out float x, out float y);

                Assert.That(
                    Math.Sqrt(x * x + y * y),
                    Is.LessThanOrEqualTo(plan.ShakeAmplitude + 1e-4f),
                    "t=" + t);
            }
        }

        [Test]
        public void TheShakeIsZeroOutsideItsWindow()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            float[] outside = { -0.01f, plan.ShakeDuration, plan.ShakeDuration + 0.01f, 5f };

            foreach (float t in outside)
            {
                plan.SampleShake(t, out float x, out float y);

                Assert.That(x, Is.EqualTo(0f), "t=" + t);
                Assert.That(y, Is.EqualTo(0f), "t=" + t);
            }
        }

        [Test]
        public void TheShakeActuallyMovesAtTheStart()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            plan.SampleShake(0f, out float x, out float y);

            Assert.That(
                Math.Sqrt(x * x + y * y),
                Is.EqualTo(plan.ShakeAmplitude).Within(1e-4f),
                "開始直後は振幅いっぱいに揺れます。");
        }

        [Test]
        public void TheSameSeedGivesTheSameShake()
        {
            BattleRoundPresentationPlan a = BattleRoundPresentationPlan.Create(RoundWinner.Cpu, 1234);
            BattleRoundPresentationPlan b = BattleRoundPresentationPlan.Create(RoundWinner.Cpu, 1234);

            for (int i = 0; i < 12; i++)
            {
                float t = i * 0.01f;

                a.SampleShake(t, out float ax, out float ay);
                b.SampleShake(t, out float bx, out float by);

                Assert.That(ax, Is.EqualTo(bx));
                Assert.That(ay, Is.EqualTo(by));
            }
        }

        [Test]
        public void ADifferentSeedGivesADifferentShake()
        {
            BattleRoundPresentationPlan a = BattleRoundPresentationPlan.Create(RoundWinner.Cpu, 1);
            BattleRoundPresentationPlan b = BattleRoundPresentationPlan.Create(RoundWinner.Cpu, 2);

            bool differs = false;

            for (int i = 0; i < 12 && !differs; i++)
            {
                float t = i * 0.01f;

                a.SampleShake(t, out float ax, out float ay);
                b.SampleShake(t, out float bx, out float by);

                differs = Math.Abs(ax - bx) > 1e-4f || Math.Abs(ay - by) > 1e-4f;
            }

            Assert.That(differs, Is.True);
        }

        [Test]
        public void TheDefaultSeedIsFixed()
        {
            BattleRoundPresentationPlan plan = BattleRoundPresentationPlan.Create(RoundWinner.Player);

            Assert.That(plan.ShakeSeed, Is.EqualTo(BattleRoundPresentationPlan.DefaultShakeSeed));
        }

        // ---------------- Phase 3 以降を持ち込まない ----------------

        /// <summary>
        /// Phase 1 の2つの入口（勝者だけ／勝者＋seed）はそのまま残し、
        /// Phase 2 で足した入口は「勝者・決着理由・勝因の属性・FX設定・seed」だけを受け取ります。
        /// FINAL CORE の段階や強度のような Phase 3 の入力は受け取りません。
        /// </summary>
        [Test]
        public void ThePlanOnlyDependsOnTheRoundVerdictAndTheShakeSeed()
        {
            MethodInfo[] factories = typeof(BattleRoundPresentationPlan)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(BattleRoundPresentationPlan.Create))
                .ToArray();

            Type[][] allowed =
            {
                new[] { typeof(RoundWinner) },
                new[] { typeof(RoundWinner), typeof(int) },
                new[] { typeof(RoundWinner), typeof(RoundDecision), typeof(CoreBeasts.Units.UnitAttribute?), typeof(bool) },
                new[] { typeof(RoundWinner), typeof(RoundDecision), typeof(CoreBeasts.Units.UnitAttribute?), typeof(bool), typeof(int) },
            };

            Assert.That(factories.Length, Is.EqualTo(allowed.Length));

            foreach (MethodInfo factory in factories)
            {
                Type[] parameters = factory.GetParameters().Select(p => p.ParameterType).ToArray();

                Assert.That(
                    allowed.Any(a => a.SequenceEqual(parameters)),
                    Is.True,
                    "想定外の入口: Create(" + string.Join(", ", parameters.Select(p => p.Name)) + ")");
            }
        }

        [Test]
        public void ThePhase1FactoriesNeverProduceAnImpact()
        {
            foreach (RoundWinner winner in AllWinners)
            {
                Assert.That(BattleRoundPresentationPlan.Create(winner).ImpactKind, Is.EqualTo(BattleImpactKind.None));
                Assert.That(BattleRoundPresentationPlan.Create(winner, 7).ImpactKind, Is.EqualTo(BattleImpactKind.None));
            }
        }

        [Test]
        public void ThePlanHasNoReducedFinalOrCoreBreakSurface()
        {
            string[] forbidden =
            {
                "Reduced", "Final", "CoreBreak", "Break", "Link", "Skill", "Intensity",
            };

            string[] names = typeof(BattleRoundPresentationPlan)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(m => m.Name)
                .ToArray();

            foreach (string name in names)
            {
                foreach (string word in forbidden)
                {
                    Assert.That(
                        name.IndexOf(word, StringComparison.OrdinalIgnoreCase),
                        Is.LessThan(0),
                        name + " は Phase 2 までの範囲外です。");
                }
            }
        }
    }
}
