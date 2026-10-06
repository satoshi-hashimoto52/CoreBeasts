using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// ユニークスキル発動の演出設計（Phase 5）。時間・頂点数・振動・FX OFF を確かめます。
    /// </summary>
    public sealed class BattleSkillPresentationPlanTests
    {
        /// <summary>既存の DEPLOY 移動の時間（Phase 1）。スキル演出はこの中に収まります。</summary>
        private const float DeployMoveSeconds = 0.32f;

        private static UniqueSkillActivation On(UniqueSkillKind kind)
        {
            return new UniqueSkillActivation(kind, true, UniqueSkillReason.Unconditional, 4, 0);
        }

        private static UniqueSkillActivation Off(UniqueSkillKind kind)
        {
            return new UniqueSkillActivation(kind, false, UniqueSkillReason.FirstRound, 4, 0);
        }

        [Test]
        public void TheCueFitsInsideTheExistingDeployMove()
        {
            Assert.That(BattleSkillPresentationPlan.CueDuration, Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(BattleSkillPresentationPlan.CueDuration, Is.LessThan(DeployMoveSeconds), "接触より前に終わります。");
            Assert.That(BattleSkillPresentationPlan.TextOnlyDuration, Is.LessThan(DeployMoveSeconds));
        }

        [Test]
        public void TheCueNeverShakesAndKeepsWithinItsBudget()
        {
            Assert.That(BattleSkillPresentationPlan.ShakeAmplitude, Is.EqualTo(0f));
            Assert.That(BattleSkillPresentationPlan.VertexBudget, Is.LessThanOrEqualTo(96));
            Assert.That(BattleSkillPresentationPlan.MaxFrameSpread, Is.GreaterThan(0f));
        }

        [Test]
        public void OnlyActivatedSkillsAreShownAndBothCanBeShownTogether()
        {
            BattleSkillPresentationPlan none = BattleSkillPresentationPlan.Create(Off(UniqueSkillKind.CrimsonBite), UniqueSkillActivation.None, true);
            BattleSkillPresentationPlan player = BattleSkillPresentationPlan.Create(On(UniqueSkillKind.CrimsonBite), Off(UniqueSkillKind.StormBite), true);
            BattleSkillPresentationPlan both = BattleSkillPresentationPlan.Create(On(UniqueSkillKind.VerdantFang), On(UniqueSkillKind.TidalHowl), true);

            Assert.That(none.ShowsAny, Is.False, "発動しないスキルは出しません。");
            Assert.That(none.Duration, Is.EqualTo(0f));
            Assert.That(player.ShowsPlayer, Is.True);
            Assert.That(player.ShowsCpu, Is.False);
            Assert.That(both.ShowsPlayer && both.ShowsCpu, Is.True, "双方が発動すれば同時に出します。");
            Assert.That(both.Duration, Is.EqualTo(BattleSkillPresentationPlan.CueDuration));
        }

        [Test]
        public void FxOffShowsTextOnly()
        {
            BattleSkillPresentationPlan plan = BattleSkillPresentationPlan.Create(On(UniqueSkillKind.TidalHowl), UniqueSkillActivation.None, false);

            Assert.That(plan.ShowsAny, Is.True, "FX OFF でも発動は同じです。");
            Assert.That(plan.PlaysGraphics, Is.False, "FX OFF では図形を出しません。");
            Assert.That(plan.Duration, Is.EqualTo(BattleSkillPresentationPlan.TextOnlyDuration));
        }
    }
}
