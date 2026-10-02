using NUnit.Framework;

using CoreBeasts.Units;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// ATTRIBUTE LINK の演出設計（Phase 4C）。時間・頂点数・振動・FX OFF を確かめます。
    /// </summary>
    public sealed class BattleAttributeLinkPresentationPlanTests
    {
        /// <summary>既存の DEPLOY 移動の時間（Phase 1）。LINK 演出はこの中に収まります。</summary>
        private const float DeployMoveSeconds = 0.32f;

        private static AttributeLinkResult Link(int chain, UnitAttribute attribute)
        {
            return new AttributeLinkResult(chain, AttributeLink.BonusFor(chain), AttributeLink.BitOf(attribute));
        }

        [Test]
        public void TheCueFitsInsideTheExistingDeployMove()
        {
            Assert.That(BattleAttributeLinkPresentationPlan.CueDuration, Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(BattleAttributeLinkPresentationPlan.CueDuration, Is.LessThanOrEqualTo(DeployMoveSeconds));
            Assert.That(BattleAttributeLinkPresentationPlan.TextOnlyDuration, Is.LessThanOrEqualTo(DeployMoveSeconds));
        }

        [Test]
        public void TheCueNeverShakesAndKeepsWithinItsBudget()
        {
            Assert.That(BattleAttributeLinkPresentationPlan.ShakeAmplitude, Is.EqualTo(0f));
            Assert.That(BattleAttributeLinkPresentationPlan.VertexBudget, Is.LessThanOrEqualTo(200));
            Assert.That(BattleAttributeLinkPresentationPlan.MaxRadius, Is.GreaterThan(0f));
        }

        [Test]
        public void OnlyTheLinkedSidesAreShown()
        {
            BattleAttributeLinkPresentationPlan none = BattleAttributeLinkPresentationPlan.Create(AttributeLinkResult.None, AttributeLinkResult.None, true);
            BattleAttributeLinkPresentationPlan player = BattleAttributeLinkPresentationPlan.Create(Link(2, UnitAttribute.Red), AttributeLinkResult.None, true);
            BattleAttributeLinkPresentationPlan both = BattleAttributeLinkPresentationPlan.Create(Link(3, UnitAttribute.Red), Link(5, UnitAttribute.Blue), true);

            Assert.That(none.ShowsAny, Is.False);
            Assert.That(none.PlaysGraphics, Is.False);
            Assert.That(none.Duration, Is.EqualTo(0f));

            Assert.That(player.ShowsPlayer, Is.True);
            Assert.That(player.ShowsCpu, Is.False);
            Assert.That(player.Duration, Is.EqualTo(BattleAttributeLinkPresentationPlan.CueDuration));

            Assert.That(both.ShowsPlayer && both.ShowsCpu, Is.True, "双方が LINK すれば同時に出します。");
            Assert.That(both.Cpu.ChainCount, Is.EqualTo(5), "実チェーン数を保ちます。");
            Assert.That(both.Cpu.BonusPower, Is.EqualTo(6));
        }

        [Test]
        public void FxOffShowsTextOnly()
        {
            BattleAttributeLinkPresentationPlan plan = BattleAttributeLinkPresentationPlan.Create(Link(2, UnitAttribute.Green), Link(2, UnitAttribute.Green), false);

            Assert.That(plan.ShowsAny, Is.True);
            Assert.That(plan.PlaysGraphics, Is.False, "FX OFF では図形を出しません。");
            Assert.That(plan.Duration, Is.EqualTo(BattleAttributeLinkPresentationPlan.TextOnlyDuration));
        }
    }
}
