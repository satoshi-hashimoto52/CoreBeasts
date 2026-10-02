using System;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// Phase 2「属性別インパクト」の演出設計値。Unityへ依存しないため、そのまま実行できます。
    ///
    /// 決着理由と勝因の属性から決着エフェクトの種類が一意に決まること、
    /// 範囲外の値でも例外にならず安全側（None）へ倒れること、
    /// 各エフェクトの時間・広がり・頂点数が上限内であること、
    /// そして Phase 1 の時間・距離・振動がどの種類でも変わらないことを固定します。
    /// </summary>
    public sealed class BattleImpactPlanTests
    {
        private static readonly RoundWinner[] Winners = { RoundWinner.Player, RoundWinner.Cpu };

        private static readonly RoundDecision[] AllDecisions =
        {
            RoundDecision.AttributeAdvantage,
            RoundDecision.PowerComparison,
            RoundDecision.CoreComparison,
            RoundDecision.Draw,
        };

        private static readonly UnitAttribute?[] AllAttributes =
        {
            UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue, null, (UnitAttribute)99,
        };

        private static BattleImpactKind Resolve(
            RoundWinner winner, RoundDecision decision, UnitAttribute? attribute, bool fx = true)
        {
            return BattleRoundPresentationPlan.Create(winner, decision, attribute, fx).ImpactKind;
        }

        // ---------------- 決着理由 → 種類 ----------------

        [Test]
        public void AnAttributeWinWithRedShowsTheRedImpact()
        {
            foreach (RoundWinner winner in Winners)
            {
                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, UnitAttribute.Red),
                    Is.EqualTo(BattleImpactKind.Red), winner.ToString());
            }
        }

        [Test]
        public void AnAttributeWinWithBlueShowsTheBlueImpact()
        {
            foreach (RoundWinner winner in Winners)
            {
                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, UnitAttribute.Blue),
                    Is.EqualTo(BattleImpactKind.Blue), winner.ToString());
            }
        }

        [Test]
        public void AnAttributeWinWithGreenShowsTheGreenImpact()
        {
            foreach (RoundWinner winner in Winners)
            {
                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, UnitAttribute.Green),
                    Is.EqualTo(BattleImpactKind.Green), winner.ToString());
            }
        }

        [Test]
        public void APowerComparisonShowsThePowerImpactWhateverColourWasCompared()
        {
            foreach (RoundWinner winner in Winners)
            {
                foreach (UnitAttribute? attribute in AllAttributes)
                {
                    Assert.That(
                        Resolve(winner, RoundDecision.PowerComparison, attribute),
                        Is.EqualTo(BattleImpactKind.Power),
                        winner + " / " + attribute);
                }
            }
        }

        [Test]
        public void ACoreComparisonShowsTheCoreImpact()
        {
            foreach (RoundWinner winner in Winners)
            {
                foreach (UnitAttribute? attribute in AllAttributes)
                {
                    Assert.That(
                        Resolve(winner, RoundDecision.CoreComparison, attribute),
                        Is.EqualTo(BattleImpactKind.Core),
                        winner + " / " + attribute);
                }
            }
        }

        [Test]
        public void ADrawAlwaysShowsTheDrawImpactAndNeverAWinnerColour()
        {
            foreach (RoundDecision decision in AllDecisions)
            {
                foreach (UnitAttribute? attribute in AllAttributes)
                {
                    Assert.That(
                        Resolve(RoundWinner.Draw, decision, attribute),
                        Is.EqualTo(BattleImpactKind.Draw),
                        decision + " / " + attribute);
                }
            }
        }

        // ---------------- 安全なフォールバック ----------------

        [Test]
        public void AnAttributeWinWithoutAKnownColourFallsBackToNoImpact()
        {
            foreach (RoundWinner winner in Winners)
            {
                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, null),
                    Is.EqualTo(BattleImpactKind.None));

                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, (UnitAttribute)99),
                    Is.EqualTo(BattleImpactKind.None));

                Assert.That(
                    Resolve(winner, RoundDecision.AttributeAdvantage, (UnitAttribute)(-1)),
                    Is.EqualTo(BattleImpactKind.None));
            }
        }

        [Test]
        public void OutOfRangeOrContradictoryInputsFallBackToNoImpactWithoutThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(Resolve((RoundWinner)42, RoundDecision.AttributeAdvantage, UnitAttribute.Red),
                    Is.EqualTo(BattleImpactKind.None));

                Assert.That(Resolve(RoundWinner.Player, (RoundDecision)42, UnitAttribute.Red),
                    Is.EqualTo(BattleImpactKind.None));

                // 勝者がいるのに決着理由が Draw という矛盾した組み合わせ。
                Assert.That(Resolve(RoundWinner.Cpu, RoundDecision.Draw, UnitAttribute.Blue),
                    Is.EqualTo(BattleImpactKind.None));
            });
        }

        [Test]
        public void FxOffAlwaysGivesNoImpact()
        {
            RoundWinner[] everyWinner = { RoundWinner.Player, RoundWinner.Cpu, RoundWinner.Draw };

            foreach (RoundWinner winner in everyWinner)
            {
                foreach (RoundDecision decision in AllDecisions)
                {
                    foreach (UnitAttribute? attribute in AllAttributes)
                    {
                        BattleRoundPresentationPlan plan =
                            BattleRoundPresentationPlan.Create(winner, decision, attribute, fxEnabled: false);

                        Assert.That(plan.ImpactKind, Is.EqualTo(BattleImpactKind.None));
                        Assert.That(plan.ImpactEnabled, Is.False);
                        Assert.That(plan.ImpactDuration, Is.EqualTo(0f));
                    }
                }
            }
        }

        [Test]
        public void AnUndefinedKindResolvesToTheEmptyProfile()
        {
            Assert.That(AttributeEffectProfile.For((BattleImpactKind)77), Is.SameAs(AttributeEffectProfile.None));
            Assert.That(AttributeEffectProfile.For(BattleImpactKind.None).IsVisible, Is.False);
            Assert.That(AttributeEffectProfile.None.MaxVertices, Is.EqualTo(0));
        }

        // ---------------- 時間・広がり・頂点数 ----------------

        [TestCase(BattleImpactKind.Red, 0.20f, 0.30f)]
        [TestCase(BattleImpactKind.Blue, 0.25f, 0.35f)]
        [TestCase(BattleImpactKind.Green, 0.25f, 0.35f)]
        [TestCase(BattleImpactKind.Power, 0.20f, 0.30f)]
        public void EachImpactLastsWithinItsAgreedWindow(BattleImpactKind kind, float min, float max)
        {
            Assert.That(AttributeEffectProfile.For(kind).Duration, Is.InRange(min, max));
        }

        [Test]
        public void RedIsShorterThanBlueAndGreen()
        {
            Assert.That(AttributeEffectProfile.Red.Duration, Is.LessThan(AttributeEffectProfile.Blue.Duration));
            Assert.That(AttributeEffectProfile.Red.Duration, Is.LessThan(AttributeEffectProfile.Green.Duration));
        }

        [Test]
        public void EveryImpactEndsBeforeTheLandingFinishes()
        {
            float release = BattleRoundPresentationPlan.Create(RoundWinner.Player).ReleaseDuration;

            foreach (AttributeEffectProfile profile in AttributeEffectProfile.Visible())
            {
                Assert.That(profile.Duration, Is.GreaterThan(0f), profile.ToString());
                Assert.That(profile.Duration, Is.LessThanOrEqualTo(release), profile.ToString());
            }
        }

        [Test]
        public void EveryImpactStaysWithinTheRadiusAndVertexBudget()
        {
            Assert.That(AttributeEffectProfile.VertexBudget, Is.LessThanOrEqualTo(128));
            Assert.That(AttributeEffectProfile.MaxRadiusLimit, Is.LessThanOrEqualTo(180f));

            foreach (AttributeEffectProfile profile in AttributeEffectProfile.Visible())
            {
                Assert.That(profile.MaxRadius, Is.GreaterThan(0f), profile.ToString());
                Assert.That(profile.MaxRadius, Is.LessThanOrEqualTo(AttributeEffectProfile.MaxRadiusLimit), profile.ToString());
                Assert.That(profile.MaxVertices, Is.GreaterThan(0), profile.ToString());
                Assert.That(profile.MaxVertices, Is.LessThanOrEqualTo(AttributeEffectProfile.VertexBudget), profile.ToString());
            }
        }

        // ---------------- 色 ----------------

        [Test]
        public void OnlyTheThreeAttributeImpactsUseAttributeColours()
        {
            Assert.That(AttributeEffectProfile.Red.UsesAttributeColor, Is.True);
            Assert.That(AttributeEffectProfile.Blue.UsesAttributeColor, Is.True);
            Assert.That(AttributeEffectProfile.Green.UsesAttributeColor, Is.True);
            Assert.That(AttributeEffectProfile.Power.UsesAttributeColor, Is.False);
            Assert.That(AttributeEffectProfile.Core.UsesAttributeColor, Is.False);
            Assert.That(AttributeEffectProfile.Draw.UsesAttributeColor, Is.False);
        }

        [Test]
        public void RedIsRedToOrange()
        {
            AssertHue(AttributeEffectProfile.Red, 0f, 30f);
        }

        [Test]
        public void BlueIsBlueToCyan()
        {
            AssertHue(AttributeEffectProfile.Blue, 180f, 230f);
        }

        [Test]
        public void GreenIsGreenToYellowGreen()
        {
            AssertHue(AttributeEffectProfile.Green, 70f, 150f);
        }

        [Test]
        public void PowerAndCoreAreWhiteToGoldAndNeverAnAttributeHue()
        {
            foreach (AttributeEffectProfile profile in new[] { AttributeEffectProfile.Power, AttributeEffectProfile.Core })
            {
                foreach (uint rgb in new[] { profile.InnerColor, profile.OuterColor })
                {
                    Hsv(rgb, out float hue, out float saturation, out float value);

                    Assert.That(value, Is.GreaterThan(0.9f), profile + " は明るい白〜金です。");
                    Assert.That(
                        saturation < 0.15f || (hue >= 38f && hue <= 60f),
                        Is.True,
                        profile + " の色 " + rgb.ToString("X6") + " が白〜金ではありません。");
                }
            }
        }

        [Test]
        public void DrawIsANearlyColourlessLightGrey()
        {
            foreach (uint rgb in new[] { AttributeEffectProfile.Draw.InnerColor, AttributeEffectProfile.Draw.OuterColor })
            {
                Hsv(rgb, out _, out float saturation, out float value);

                Assert.That(saturation, Is.LessThan(0.1f), rgb.ToString("X6"));
                Assert.That(value, Is.GreaterThan(0.7f), rgb.ToString("X6"));
            }

            Assert.That(AttributeEffectProfile.Draw.IsSymmetric, Is.True);
        }

        // ---------------- Phase 1 を変えない ----------------

        [Test]
        public void EveryImpactKeepsThePhase1TimingDistancesAndShake()
        {
            RoundWinner[] everyWinner = { RoundWinner.Player, RoundWinner.Cpu, RoundWinner.Draw };

            foreach (RoundWinner winner in everyWinner)
            {
                BattleRoundPresentationPlan phase1 = BattleRoundPresentationPlan.Create(winner);

                foreach (RoundDecision decision in AllDecisions)
                {
                    foreach (UnitAttribute? attribute in AllAttributes)
                    {
                        foreach (bool fx in new[] { true, false })
                        {
                            BattleRoundPresentationPlan plan =
                                BattleRoundPresentationPlan.Create(winner, decision, attribute, fx);

                            string label = winner + "/" + decision + "/" + attribute + "/fx=" + fx;

                            Assert.That(plan.LiftDuration, Is.EqualTo(phase1.LiftDuration), label);
                            Assert.That(plan.ApproachDuration, Is.EqualTo(phase1.ApproachDuration), label);
                            Assert.That(plan.ImpactHoldDuration, Is.EqualTo(phase1.ImpactHoldDuration), label);
                            Assert.That(plan.KnockbackDuration, Is.EqualTo(phase1.KnockbackDuration), label);
                            Assert.That(plan.LandingDuration, Is.EqualTo(phase1.LandingDuration), label);
                            Assert.That(plan.ReturnDuration, Is.EqualTo(phase1.ReturnDuration), label);
                            Assert.That(plan.ShakeDuration, Is.EqualTo(phase1.ShakeDuration), label);
                            Assert.That(plan.ShakeAmplitude, Is.EqualTo(phase1.ShakeAmplitude), label);
                            Assert.That(plan.ShakeSeed, Is.EqualTo(phase1.ShakeSeed), label);
                            Assert.That(plan.PlayerKnockback, Is.EqualTo(phase1.PlayerKnockback), label);
                            Assert.That(plan.CpuKnockback, Is.EqualTo(phase1.CpuKnockback), label);
                            Assert.That(plan.PlayerDisplacement, Is.EqualTo(phase1.PlayerDisplacement), label);
                            Assert.That(plan.CpuDisplacement, Is.EqualTo(phase1.CpuDisplacement), label);
                            Assert.That(plan.ClashDuration, Is.EqualTo(phase1.ClashDuration), label);
                        }
                    }
                }
            }
        }

        [Test]
        public void ThePhase1ConstantsAreTheAgreedValues()
        {
            Assert.That(BattleRoundPresentationPlan.StandardImpactHoldDuration, Is.EqualTo(0.06f));
            Assert.That(BattleRoundPresentationPlan.StandardLiftDuration, Is.EqualTo(0.10f));
            Assert.That(BattleRoundPresentationPlan.StandardApproachDuration, Is.EqualTo(0.22f));
            Assert.That(BattleRoundPresentationPlan.StandardKnockbackDuration, Is.EqualTo(0.20f));
            Assert.That(BattleRoundPresentationPlan.StandardLandingDuration, Is.EqualTo(0.14f));
            Assert.That(BattleRoundPresentationPlan.StandardReturnDuration, Is.EqualTo(0.18f));
            Assert.That(BattleRoundPresentationPlan.StandardShakeDuration, Is.EqualTo(0.12f));
            Assert.That(BattleRoundPresentationPlan.StandardShakeAmplitude, Is.EqualTo(5f));
            Assert.That(BattleRoundPresentationPlan.StandardKnockbackDistance, Is.EqualTo(40f));
            Assert.That(BattleRoundPresentationPlan.StandardWinnerRecoilDistance, Is.EqualTo(10f));
            Assert.That(BattleRoundPresentationPlan.StandardDrawRecoilDistance, Is.EqualTo(18f));
        }

        // ---------------- 勝因の属性の読み出し ----------------

        [Test]
        public void TheDecidingColourIsReadFromTheRulesForAnAttributeWin()
        {
            BattleUnit red = TestBattleUnits.Single("p", UnitAttribute.Red);
            BattleUnit green = TestBattleUnits.Single("c", UnitAttribute.Green);

            RoundOutcome outcome = BattleRules.ResolveRound(red, green);
            RoundResult result = Resolved(red, green, outcome);

            Assert.That(result.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(DecidingAttributeLookup.Of(result), Is.EqualTo(UnitAttribute.Red));
        }

        [Test]
        public void EveryColourPairingMapsToTheExpectedImpact()
        {
            UnitAttribute[] colours = { UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue };

            foreach (UnitAttribute player in colours)
            {
                foreach (UnitAttribute cpu in colours)
                {
                    BattleUnit p = TestBattleUnits.Single("p", player, 60);
                    BattleUnit c = TestBattleUnits.Single("c", cpu, 40);

                    RoundOutcome outcome = BattleRules.ResolveRound(p, c);
                    RoundResult result = Resolved(p, c, outcome);

                    BattleImpactKind kind = BattleRoundPresentationPlan.Create(
                        result.Winner, result.Decision, DecidingAttributeLookup.Of(result), true).ImpactKind;

                    string label = player + " vs " + cpu;

                    if (player == cpu)
                    {
                        Assert.That(kind, Is.EqualTo(BattleImpactKind.Power), label + " は POWER で決まります。");
                    }
                    else
                    {
                        UnitAttribute winnerColour = BattleRules.Beats(player, cpu) ? player : cpu;
                        BattleImpactKind expected =
                            winnerColour == UnitAttribute.Red ? BattleImpactKind.Red :
                            winnerColour == UnitAttribute.Blue ? BattleImpactKind.Blue :
                            BattleImpactKind.Green;

                        Assert.That(kind, Is.EqualTo(expected), label);
                    }
                }
            }
        }

        [Test]
        public void TheLookupIsSafeForMissingOrContradictoryResults()
        {
            BattleUnit red = TestBattleUnits.Single("p", UnitAttribute.Red);
            BattleUnit green = TestBattleUnits.Single("c", UnitAttribute.Green);

            Assert.DoesNotThrow(() =>
            {
                Assert.That(DecidingAttributeLookup.Of(null), Is.Null);
                Assert.That(DecidingAttributeLookup.Of(new RoundResult(1, null, green, RoundWinner.Cpu, RoundDecision.AttributeAdvantage)), Is.Null);
                Assert.That(DecidingAttributeLookup.Of(new RoundResult(1, red, null, RoundWinner.Player, RoundDecision.AttributeAdvantage)), Is.Null);

                // 記録と食い違う勝者・決着理由なら、色を推測しません。
                Assert.That(DecidingAttributeLookup.Of(new RoundResult(1, red, green, RoundWinner.Cpu, RoundDecision.AttributeAdvantage)), Is.Null);
                Assert.That(DecidingAttributeLookup.Of(new RoundResult(1, red, green, RoundWinner.Player, RoundDecision.PowerComparison)), Is.Null);
            });
        }

        // ---------------- 補助 ----------------

        /// <summary>
        /// <see cref="BattleSession"/>と同じく、判定結果の勝因属性を記録した結果を作ります（LINK なし）。
        /// Phase 4 から、勝因の属性は結果が正本で、画面側で判定し直しません。
        /// </summary>
        private static RoundResult Resolved(BattleUnit player, BattleUnit cpu, RoundOutcome outcome)
        {
            return new RoundResult(
                1, player, cpu, outcome.Winner, outcome.Decision, outcome.DecidingAttribute,
                AttributeLinkResult.None, AttributeLinkResult.None, player, cpu);
        }

        private static void AssertHue(AttributeEffectProfile profile, float min, float max)
        {
            foreach (uint rgb in new[] { profile.InnerColor, profile.OuterColor })
            {
                Hsv(rgb, out float hue, out float saturation, out _);

                Assert.That(saturation, Is.GreaterThan(0.5f), profile + " " + rgb.ToString("X6"));
                Assert.That(hue, Is.InRange(min, max), profile + " " + rgb.ToString("X6"));
            }
        }

        private static void Hsv(uint rgb, out float hue, out float saturation, out float value)
        {
            AttributeEffectProfile.Unpack(rgb, out float r, out float g, out float b);

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            value = max;
            saturation = max <= 0f ? 0f : delta / max;

            if (delta <= 0f)
            {
                hue = 0f;
            }
            else if (max == r)
            {
                hue = 60f * (((g - b) / delta) % 6f);
            }
            else if (max == g)
            {
                hue = 60f * ((b - r) / delta + 2f);
            }
            else
            {
                hue = 60f * ((r - g) / delta + 4f);
            }

            if (hue < 0f)
            {
                hue += 360f;
            }
        }
    }
}
