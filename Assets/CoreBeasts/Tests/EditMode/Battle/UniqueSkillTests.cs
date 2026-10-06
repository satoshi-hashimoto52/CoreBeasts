using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// Phase 5「ユニークスキル」のルール。条件・数値・計算順序・左右対称・不変性・内訳・予告・公平性を確かめます。
    /// </summary>
    public sealed class UniqueSkillTests
    {
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute G = UnitAttribute.Green;
        private const UnitAttribute B = UnitAttribute.Blue;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
        }

        private static BattleUnit One(string id, UnitAttribute a, int power, UniqueSkillKind skill = UniqueSkillKind.None, int core = 0)
        {
            return new BattleUnit(id, new[] { new AttributePower(a, power) }, core, skill);
        }

        private static BattleUnit Two(string id, UnitAttribute a, int pa, UnitAttribute b, int pb, UniqueSkillKind skill = UniqueSkillKind.None, int core = 0)
        {
            return new BattleUnit(id, new[] { new AttributePower(a, pa), new AttributePower(b, pb) }, core, skill);
        }

        // ---------------- データ ----------------

        [Test]
        public void TheFourDefinitionAssetsMapToTheirSkillKinds()
        {
            AssertDefinition("Assets/CoreBeasts/Data/Beasts/CB_Volx.asset", UniqueSkillKind.CrimsonBite, "CRIMSON BITE", "直前に出した個体がREDなら、POWER +4。");
            AssertDefinition("Assets/CoreBeasts/Data/Testing/CB_Volx_Blue_TEST.asset", UniqueSkillKind.TidalHowl, "TIDAL HOWL", "登場時、相手のPOWER -4。");
            AssertDefinition("Assets/CoreBeasts/Data/Testing/CB_Volx_Green_TEST.asset", UniqueSkillKind.VerdantFang, "VERDANT FANG", "直前のラウンドで敗北していたら、POWER +6。");
            AssertDefinition("Assets/CoreBeasts/Data/Testing/CB_Volx_RedBlue_TEST.asset", UniqueSkillKind.StormBite, "STORM BITE", "相手がREDかBLUEなら、POWER +3。");
        }

        [Test]
        public void ANewDefinitionHasNoSkillAndTheFactoryCarriesTheKind()
        {
            CoreBeastDefinition plain = Definition("plain", R, 50, UniqueSkillKind.None, "SKILL");
            CoreBeastDefinition fang = Definition("fang", G, 50, UniqueSkillKind.VerdantFang, "VERDANT FANG");

            Assert.That(ScriptableObject.CreateInstance<CoreBeastDefinition>().SkillKind, Is.EqualTo(UniqueSkillKind.None));

            Assert.That(BattleUnitFactory.TryCreateUnit(Owned("a", plain), out BattleUnit a), Is.True);
            Assert.That(BattleUnitFactory.TryCreateUnit(Owned("b", fang), out BattleUnit b), Is.True);
            Assert.That(a.Skill, Is.EqualTo(UniqueSkillKind.None));
            Assert.That(b.Skill, Is.EqualTo(UniqueSkillKind.VerdantFang));
        }

        [Test]
        public void RenamingTheSkillDoesNotChangeTheRule()
        {
            // 名前は TIDAL HOWL でも、種類が CRIMSON BITE ならルールは CRIMSON BITE です。
            CoreBeastDefinition misnamed = Definition("misnamed", R, 50, UniqueSkillKind.CrimsonBite, "TIDAL HOWL");

            Assert.That(BattleUnitFactory.TryCreateUnit(Owned("x", misnamed), out BattleUnit unit), Is.True);

            UniqueSkillActivation skill = UniqueSkill.Evaluate(unit, One("prev", R, 40), PreviousRoundResult.Won, One("o", R, 50));

            Assert.That(skill.Kind, Is.EqualTo(UniqueSkillKind.CrimsonBite));
            Assert.That(skill.SelfBonus, Is.EqualTo(4));
            Assert.That(skill.OpponentPenalty, Is.EqualTo(0));
        }

        [Test]
        public void ExistingConstructorsCreateUnitsAndResultsWithoutSkills()
        {
            Assert.That(new BattleUnit("a", R, 50).Skill, Is.EqualTo(UniqueSkillKind.None));
            Assert.That(new BattleUnit("b", R, 50, B, 40).Skill, Is.EqualTo(UniqueSkillKind.None));
            Assert.That(new BattleUnit("c", new[] { new AttributePower(G, 30) }, 5).Skill, Is.EqualTo(UniqueSkillKind.None));

            RoundResult old = new RoundResult(1, One("p", R, 50), One("c", R, 40), RoundWinner.Player, RoundDecision.PowerComparison);

            Assert.That(old.PlayerSkill, Is.EqualTo(UniqueSkillActivation.None));
            Assert.That(old.CpuSkill, Is.EqualTo(UniqueSkillActivation.None));
            Assert.That(old.PlayerPower.SelfSkill, Is.EqualTo(0));
            Assert.That(old.PlayerPower.OpponentPenalty, Is.EqualTo(0));

            Assert.That(UniqueSkill.Evaluate(One("p", R, 50), One("q", R, 50), PreviousRoundResult.Lost, One("o", B, 50)),
                Is.EqualTo(UniqueSkillActivation.None), "スキルなしは何も起きません。");
        }

        // ---------------- 各スキル ----------------

        [Test]
        public void CrimsonBiteNeedsAPreviousUnitWithRed()
        {
            BattleUnit self = One("self", R, 50, UniqueSkillKind.CrimsonBite);
            BattleUnit opponent = One("o", G, 50);

            UniqueSkillActivation first = UniqueSkill.Evaluate(self, null, PreviousRoundResult.None, opponent);
            UniqueSkillActivation mismatch = UniqueSkill.Evaluate(self, One("prev", G, 40), PreviousRoundResult.Lost, opponent);
            UniqueSkillActivation match = UniqueSkill.Evaluate(self, One("prev", R, 40), PreviousRoundResult.Lost, opponent);
            UniqueSkillActivation dual = UniqueSkill.Evaluate(self, Two("prev", B, 30, R, 20), PreviousRoundResult.Won, opponent);

            Assert.That(first.Activated, Is.False, "初戦では発動しません。");
            Assert.That(first.Reason, Is.EqualTo(UniqueSkillReason.FirstRound));
            Assert.That(mismatch.Activated, Is.False);
            Assert.That(mismatch.Reason, Is.EqualTo(UniqueSkillReason.PreviousUnitHadNoRed));
            Assert.That(match.Activated, Is.True, "直前の勝敗は問いません。");
            Assert.That(match.SelfBonus, Is.EqualTo(4));
            Assert.That(match.Reason, Is.EqualTo(UniqueSkillReason.PreviousUnitHadRed));
            Assert.That(dual.Activated, Is.True, "2属性でも RED を含めば発動します。");
            Assert.That(dual.SelfBonus, Is.EqualTo(4), "加算は1回だけです。");
        }

        [Test]
        public void TidalHowlAlwaysLowersTheOpponentDownToOne()
        {
            BattleUnit howl = One("howl", B, 50, UniqueSkillKind.TidalHowl);

            UniqueSkillActivation skill = UniqueSkill.Evaluate(howl, null, PreviousRoundResult.None, One("o", B, 50));

            Assert.That(skill.Activated, Is.True, "初戦でも無条件に発動します。");
            Assert.That(skill.Reason, Is.EqualTo(UniqueSkillReason.Unconditional));
            Assert.That(skill.SelfBonus, Is.EqualTo(0));
            Assert.That(skill.OpponentPenalty, Is.EqualTo(4));

            RoundEvaluation weak = BattleRoundEvaluator.Evaluate(howl, Two("o", B, 3, R, 50), RoundSideContext.First, RoundSideContext.First);

            Assert.That(weak.CpuEffectiveUnit.PowerOf(B), Is.EqualTo(1), "POWER の下限は1です。");
            Assert.That(weak.CpuEffectiveUnit.PowerOf(R), Is.EqualTo(46), "全属性へ同じように効きます。");
        }

        [Test]
        public void VerdantFangNeedsAPreviousLoss()
        {
            BattleUnit fang = One("fang", G, 50, UniqueSkillKind.VerdantFang);
            BattleUnit opponent = One("o", G, 50);
            BattleUnit previous = One("prev", G, 40);

            Assert.That(UniqueSkill.Evaluate(fang, null, PreviousRoundResult.None, opponent).Reason, Is.EqualTo(UniqueSkillReason.FirstRound));
            Assert.That(UniqueSkill.Evaluate(fang, null, PreviousRoundResult.None, opponent).Activated, Is.False, "初戦では発動しません。");
            Assert.That(UniqueSkill.Evaluate(fang, previous, PreviousRoundResult.Won, opponent).Activated, Is.False, "勝った後は発動しません。");
            Assert.That(UniqueSkill.Evaluate(fang, previous, PreviousRoundResult.Draw, opponent).Activated, Is.False, "引き分けの後は発動しません。");
            Assert.That(UniqueSkill.Evaluate(fang, previous, PreviousRoundResult.Draw, opponent).Reason, Is.EqualTo(UniqueSkillReason.DrewPreviousRound));

            UniqueSkillActivation lost = UniqueSkill.Evaluate(fang, previous, PreviousRoundResult.Lost, opponent);

            Assert.That(lost.Activated, Is.True);
            Assert.That(lost.SelfBonus, Is.EqualTo(6));
            Assert.That(lost.Reason, Is.EqualTo(UniqueSkillReason.LostPreviousRound));
        }

        [Test]
        public void StormBiteNeedsAnOpponentWithRedOrBlueAndAddsOnce()
        {
            BattleUnit storm = Two("storm", R, 40, B, 21, UniqueSkillKind.StormBite);

            Assert.That(UniqueSkill.Evaluate(storm, null, PreviousRoundResult.None, One("o", R, 50)).SelfBonus, Is.EqualTo(3));
            Assert.That(UniqueSkill.Evaluate(storm, null, PreviousRoundResult.None, One("o", B, 50)).SelfBonus, Is.EqualTo(3));
            Assert.That(UniqueSkill.Evaluate(storm, null, PreviousRoundResult.None, Two("o", R, 40, B, 21)).SelfBonus, Is.EqualTo(3), "RED/BLUE の2属性でも1回だけです。");
            Assert.That(UniqueSkill.Evaluate(storm, null, PreviousRoundResult.None, Two("o", G, 40, R, 21)).Activated, Is.True, "RED を含めば発動します。");

            UniqueSkillActivation green = UniqueSkill.Evaluate(storm, null, PreviousRoundResult.None, One("o", G, 50));

            Assert.That(green.Activated, Is.False, "GREEN 単属性には発動しません。");
            Assert.That(green.SelfBonus, Is.EqualTo(0));
            Assert.That(green.Reason, Is.EqualTo(UniqueSkillReason.OpponentHasNoRedOrBlue));
        }

        // ---------------- 計算順序・対称性・不変性 ----------------

        [Test]
        public void TheOrderIsLinkThenOwnSkillThenOpponentPenaltyThenFloor()
        {
            // PLAYER: CRIMSON BITE（直前 RED）。直前も今回も RED なので LINK x2 (+3)。CPU: TIDAL HOWL（-4）。
            BattleUnit player = One("p", R, 2, UniqueSkillKind.CrimsonBite);
            BattleUnit cpu = One("c", R, 50, UniqueSkillKind.TidalHowl);
            RoundSideContext playerContext = new RoundSideContext(One("prev", R, 40), 1, PreviousRoundResult.Won);

            RoundEvaluation e = BattleRoundEvaluator.Evaluate(player, cpu, playerContext, RoundSideContext.First);

            // 2 + 3 + 4 - 4 = 5。下限を途中で掛けていたら（2 - 4 → 1 から足す）8 になります。
            Assert.That(e.PlayerLink.BonusPower, Is.EqualTo(3));
            Assert.That(e.PlayerSkill.SelfBonus, Is.EqualTo(4));
            Assert.That(e.CpuSkill.OpponentPenalty, Is.EqualTo(4));
            Assert.That(e.PlayerEffectiveUnit.PowerOf(R), Is.EqualTo(5));
            Assert.That(e.PlayerPower.Base, Is.EqualTo(2));
            Assert.That(e.PlayerPower.Link, Is.EqualTo(3));
            Assert.That(e.PlayerPower.SelfSkill, Is.EqualTo(4));
            Assert.That(e.PlayerPower.OpponentPenalty, Is.EqualTo(4));
            Assert.That(e.PlayerPower.Final, Is.EqualTo(5));
            Assert.That(e.PlayerPower.FloorAdjustment, Is.EqualTo(0));

            // 加算が無く、妨害だけで0以下になるときだけ下限1へ揃えます。
            RoundEvaluation floor = BattleRoundEvaluator.Evaluate(One("p", R, 2), cpu, RoundSideContext.First, RoundSideContext.First);

            Assert.That(floor.PlayerEffectiveUnit.PowerOf(R), Is.EqualTo(1));
            Assert.That(floor.PlayerPower.Final, Is.EqualTo(1));
            Assert.That(floor.PlayerPower.FloorAdjustment, Is.EqualTo(3), "2 - 4 = -2 を 1 へ揃えたぶんです。");
        }

        [Test]
        public void TheLinkIsJudgedFromTheOriginalAttributesOnly()
        {
            // スキルは POWER だけを変え、属性は変えません。LINK は元の個体の属性で決まります。
            BattleUnit player = One("p", G, 50, UniqueSkillKind.VerdantFang);
            RoundSideContext context = new RoundSideContext(One("prev", R, 50), 1, PreviousRoundResult.Lost);

            RoundEvaluation e = BattleRoundEvaluator.Evaluate(player, One("c", G, 50), context, RoundSideContext.First);

            Assert.That(e.PlayerLink.IsActive, Is.False, "RED → GREEN は LINK しません。");
            Assert.That(e.PlayerSkill.Activated, Is.True);
            Assert.That(e.PlayerEffectiveUnit.PowerOf(G), Is.EqualTo(56));
        }

        [Test]
        public void PlayerAndCpuAreTreatedSymmetrically()
        {
            BattleUnit storm = Two("storm", R, 40, B, 21, UniqueSkillKind.StormBite);
            BattleUnit howl = One("howl", B, 58, UniqueSkillKind.TidalHowl);
            RoundSideContext stormContext = new RoundSideContext(One("prev", B, 30), 2, PreviousRoundResult.Lost);
            RoundSideContext howlContext = new RoundSideContext(One("prev2", G, 30), 1, PreviousRoundResult.Won);

            RoundEvaluation forward = BattleRoundEvaluator.Evaluate(storm, howl, stormContext, howlContext);
            RoundEvaluation reverse = BattleRoundEvaluator.Evaluate(howl, storm, howlContext, stormContext);

            Assert.That(reverse.CpuSkill, Is.EqualTo(forward.PlayerSkill));
            Assert.That(reverse.PlayerSkill, Is.EqualTo(forward.CpuSkill));
            Assert.That(reverse.CpuLink, Is.EqualTo(forward.PlayerLink));
            Assert.That(reverse.CpuPower.Final, Is.EqualTo(forward.PlayerPower.Final));
            Assert.That(reverse.PlayerPower.Final, Is.EqualTo(forward.CpuPower.Final));
            Assert.That(reverse.Outcome.Decision, Is.EqualTo(forward.Outcome.Decision));
            Assert.That(reverse.Outcome.Winner, Is.EqualTo(Mirror(forward.Outcome.Winner)), "左右を入れ替えると勝者も入れ替わります。");
        }

        [Test]
        public void TheOriginalUnitsAndDefinitionsAreNeverChanged()
        {
            CoreBeastDefinition definition = Definition("def", R, 50, UniqueSkillKind.CrimsonBite, "CRIMSON BITE");

            Assert.That(BattleUnitFactory.TryCreateUnit(Owned("p", definition), out BattleUnit player), Is.True);

            BattleUnit cpu = One("c", R, 50, UniqueSkillKind.TidalHowl);
            RoundSideContext context = new RoundSideContext(One("prev", R, 40), 2, PreviousRoundResult.Lost);

            RoundEvaluation e = BattleRoundEvaluator.Evaluate(player, cpu, context, RoundSideContext.First);

            Assert.That(e.PlayerEffectiveUnit, Is.Not.SameAs(player));
            Assert.That(player.PowerOf(R), Is.EqualTo(50), "元の個体の POWER は変わりません。");
            Assert.That(cpu.PowerOf(R), Is.EqualTo(50));
            Assert.That(definition.PowerOf(R), Is.EqualTo(50), "定義の POWER は変わりません。");
            Assert.That(definition.SkillKind, Is.EqualTo(UniqueSkillKind.CrimsonBite));
            Assert.That(e.PlayerEffectiveUnit.Skill, Is.EqualTo(UniqueSkillKind.CrimsonBite), "有効な個体もスキルの種類を保ちます。");
            Assert.That(e.PlayerEffectiveUnit.Core, Is.EqualTo(player.Core));
        }

        // ---------------- 勝敗への効き方 ----------------

        [Test]
        public void SkillsNeverOverturnAnAttributeWin()
        {
            // GREEN（VERDANT FANG +6 と LINK +6）でも、RED の属性勝ちは覆りません。
            BattleUnit red = One("red", R, 1);
            BattleUnit green = One("green", G, 99, UniqueSkillKind.VerdantFang);
            RoundSideContext greenContext = new RoundSideContext(One("prev", G, 40), 2, PreviousRoundResult.Lost);

            RoundEvaluation e = BattleRoundEvaluator.Evaluate(red, green, RoundSideContext.First, greenContext);

            Assert.That(e.CpuSkill.Activated, Is.True);
            Assert.That(e.Outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(e.Outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(e.PlayerPower.IsAvailable, Is.False, "属性勝ちでは POWER を比べません。");
        }

        [Test]
        public void SkillsCanDecideAPowerComparisonOrLeadToCoreOrDraw()
        {
            // POWER 比較: TIDAL HOWL で相手 52 → 48 になり、50 が勝ちます。
            RoundEvaluation power = BattleRoundEvaluator.Evaluate(
                One("p", B, 50, UniqueSkillKind.TidalHowl), One("c", B, 52), RoundSideContext.First, RoundSideContext.First);

            Assert.That(power.Outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(power.Outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(power.CpuPower.Final, Is.EqualTo(48));

            // CORE 比較: 双方が TIDAL HOWL で同じだけ下がり、POWER が並んで CORE で決まります。
            RoundEvaluation core = BattleRoundEvaluator.Evaluate(
                One("p", B, 50, UniqueSkillKind.TidalHowl, core: 80), One("c", B, 50, UniqueSkillKind.TidalHowl, core: 70),
                RoundSideContext.First, RoundSideContext.First);

            Assert.That(core.Outcome.Decision, Is.EqualTo(RoundDecision.CoreComparison));
            Assert.That(core.Outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(core.PlayerPower.Final, Is.EqualTo(46));

            // 引き分け: POWER も CORE も並びます。
            RoundEvaluation draw = BattleRoundEvaluator.Evaluate(
                One("p", B, 50, UniqueSkillKind.TidalHowl, core: 70), One("c", B, 50, UniqueSkillKind.TidalHowl, core: 70),
                RoundSideContext.First, RoundSideContext.First);

            Assert.That(draw.Outcome.Decision, Is.EqualTo(RoundDecision.Draw));
        }

        [Test]
        public void ASameSetDualComparisonUsesTheAverageBreakdown()
        {
            RoundEvaluation e = BattleRoundEvaluator.Evaluate(
                Two("p", R, 40, B, 21, UniqueSkillKind.StormBite), Two("c", B, 30, R, 30),
                RoundSideContext.First, RoundSideContext.First);

            // (40 + 21) / 2 = 30。STORM BITE +3 → (43 + 24) / 2 = 33。
            Assert.That(e.Outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(e.PlayerPower.Base, Is.EqualTo(30));
            Assert.That(e.PlayerPower.SelfSkill, Is.EqualTo(3));
            Assert.That(e.PlayerPower.Final, Is.EqualTo(33));
            Assert.That(e.PlayerPower.Final, Is.EqualTo(e.Outcome.PlayerComparedValue), "最終値は判定の比較値そのものです。");
            Assert.That(e.Outcome.Winner, Is.EqualTo(RoundWinner.Player), "33 対 30");
        }

        // ---------------- セッション ----------------

        [Test]
        public void TheRoundResultRecordsTheMeasuredBreakdown()
        {
            BattleUnit[] players =
            {
                One("p1", R, 60, UniqueSkillKind.CrimsonBite), One("p2", R, 50, UniqueSkillKind.CrimsonBite),
                One("p3", G, 41), One("p4", G, 42), One("p5", G, 43), One("p6", G, 44), One("p7", G, 45),
            };
            BattleUnit[] cpus =
            {
                One("c1", R, 59), One("c2", R, 55, UniqueSkillKind.TidalHowl),
                One("c3", B, 41), One("c4", B, 42), One("c5", B, 43), One("c6", B, 44), One("c7", B, 45),
            };

            BattleSession session = Session(players, cpus, "c1", "c2");

            RoundResult first = Play(session, "p1");

            Assert.That(first.PlayerSkill.Kind, Is.EqualTo(UniqueSkillKind.CrimsonBite));
            Assert.That(first.PlayerSkill.Activated, Is.False, "初戦では発動しません。");

            RoundResult second = Play(session, "p2");

            // PLAYER: 50 + LINK 3（RED → RED）+ CRIMSON BITE 4 - TIDAL HOWL 4 = 53。CPU: 55 + LINK 3 = 58。
            Assert.That(second.PlayerSkill.Activated, Is.True);
            Assert.That(second.CpuSkill.Activated, Is.True);
            Assert.That(second.PlayerPower.Base, Is.EqualTo(50));
            Assert.That(second.PlayerPower.Link, Is.EqualTo(3));
            Assert.That(second.PlayerPower.SelfSkill, Is.EqualTo(4));
            Assert.That(second.PlayerPower.OpponentPenalty, Is.EqualTo(4));
            Assert.That(second.PlayerPower.Final, Is.EqualTo(53));
            Assert.That(second.CpuPower.Base, Is.EqualTo(55));
            Assert.That(second.CpuPower.Link, Is.EqualTo(3));
            Assert.That(second.CpuPower.SelfSkill, Is.EqualTo(0));
            Assert.That(second.CpuPower.OpponentPenalty, Is.EqualTo(0));
            Assert.That(second.CpuPower.Final, Is.EqualTo(58));
            Assert.That(second.PlayerComparedPower, Is.EqualTo(53), "内訳の最終値は実際の比較値と一致します。");
            Assert.That(second.CpuComparedPower, Is.EqualTo(58));
            Assert.That(second.PlayerEffectiveUnit.PowerOf(R), Is.EqualTo(53));
            Assert.That(second.PlayerUnit.PowerOf(R), Is.EqualTo(50), "PlayerUnit は元の個体です。");
            Assert.That(second.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(second.DecidingAttribute, Is.EqualTo(R), "勝因の属性は反映後の判定のものです。");
        }

        [Test]
        public void VerdantFangFollowsTheSessionHistory()
        {
            BattleUnit[] players =
            {
                One("p1", G, 40), One("p2", G, 50, UniqueSkillKind.VerdantFang), One("p3", G, 50, UniqueSkillKind.VerdantFang),
                One("p4", G, 44), One("p5", G, 45), One("p6", G, 46), One("p7", G, 47),
            };
            BattleUnit[] cpus =
            {
                One("c1", G, 60), One("c2", G, 52), One("c3", G, 50),
                One("c4", B, 44), One("c5", B, 45), One("c6", B, 46), One("c7", B, 47),
            };

            BattleSession session = Session(players, cpus, "c1", "c2", "c3");

            Assert.That(Play(session, "p1").Winner, Is.EqualTo(RoundWinner.Cpu));

            RoundResult afterLoss = Play(session, "p2");

            Assert.That(afterLoss.PlayerSkill.Activated, Is.True, "負けた次は発動します。");
            Assert.That(afterLoss.Winner, Is.EqualTo(RoundWinner.Player), "50 + LINK 3 + 6 = 59 対 52 + LINK 3 = 55");

            RoundResult afterWin = Play(session, "p3");

            Assert.That(afterWin.PlayerSkill.Activated, Is.False, "勝った次は発動しません。");
            Assert.That(afterWin.PlayerSkill.Reason, Is.EqualTo(UniqueSkillReason.WonPreviousRound));
        }

        [Test]
        public void ThePreviewHasNoSideEffectsAndHidesWhatDependsOnTheOpponent()
        {
            BattleUnit[] players =
            {
                One("red", R, 50), One("crimson", R, 50, UniqueSkillKind.CrimsonBite), One("howl", B, 50, UniqueSkillKind.TidalHowl),
                One("fang", G, 50, UniqueSkillKind.VerdantFang), Two("storm", R, 40, B, 21, UniqueSkillKind.StormBite),
                One("g1", G, 41), One("g2", G, 42),
            };
            BattleUnit[] cpus =
            {
                One("c1", B, 60), One("c2", B, 52), One("c3", B, 50),
                One("c4", B, 44), One("c5", B, 45), One("c6", B, 46), One("c7", B, 47),
            };

            BattleSession session = Session(players, cpus, "c1", "c2");

            Assert.That(session.PreviewPlayerSkill("crimson").Activates, Is.False, "初戦は CRIMSON BITE を予告しません。");
            Assert.That(session.PreviewPlayerSkill("howl").Activates, Is.True, "TIDAL HOWL は固定効果として予告できます。");
            Assert.That(session.PreviewPlayerSkill("howl").OpponentPenalty, Is.EqualTo(4));

            Assert.That(Play(session, "red").Winner, Is.EqualTo(RoundWinner.Cpu), "BLUE に負けます。");

            int round = session.CurrentRound;
            int history = session.History.Count;
            int available = session.PlayerAvailableUnits.Count;
            Assert.That(session.SelectCpuUnit().Success, Is.True);
            string pendingCpu = session.PendingCpuInstanceId;

            for (int i = 0; i < 3; i++)
            {
                UniqueSkillPreview crimson = session.PreviewPlayerSkill("crimson");
                UniqueSkillPreview fang = session.PreviewPlayerSkill("fang");
                UniqueSkillPreview storm = session.PreviewPlayerSkill("storm");

                Assert.That(crimson.Activates, Is.True, "直前が RED なので予告します。");
                Assert.That(crimson.SelfBonus, Is.EqualTo(4));
                Assert.That(fang.Activates, Is.True, "直前に負けたので予告します。");
                Assert.That(fang.SelfBonus, Is.EqualTo(6));
                Assert.That(storm.Activates, Is.False, "相手の選出で決まる STORM BITE は発動を予告しません。");
                Assert.That(storm.State, Is.EqualTo(UniqueSkillPreviewState.DependsOnOpponent));
                Assert.That(storm.SelfBonus, Is.EqualTo(0));
                Assert.That(session.PreviewPlayerSkill("red").State, Is.EqualTo(UniqueSkillPreviewState.None));
                Assert.That(session.PreviewPlayerSkill("missing").State, Is.EqualTo(UniqueSkillPreviewState.None));
            }

            Assert.That(session.CurrentRound, Is.EqualTo(round), "予告はセッションを進めません。");
            Assert.That(session.History.Count, Is.EqualTo(history));
            Assert.That(session.PlayerAvailableUnits.Count, Is.EqualTo(available));
            Assert.That(session.HasPlayerSelected, Is.False);
            Assert.That(session.PendingCpuInstanceId, Is.EqualTo(pendingCpu), "予告は CPU の選出に触れません。");
            Assert.That(session.PreviewPlayerSkill("red").State, Is.EqualTo(UniqueSkillPreviewState.None), "使用済みは予告しません。");
        }

        [Test]
        public void AFailedResolveDoesNotAdvanceSkillsAndARematchStartsFresh()
        {
            BattleUnit[] players =
            {
                One("p1", R, 60), One("p2", R, 50, UniqueSkillKind.CrimsonBite),
                One("p3", G, 41), One("p4", G, 42), One("p5", G, 43), One("p6", G, 44), One("p7", G, 45),
            };
            BattleUnit[] cpus =
            {
                One("c1", R, 59), One("c2", R, 55),
                One("c3", B, 41), One("c4", B, 42), One("c5", B, 43), One("c6", B, 44), One("c7", B, 45),
            };

            BattleSession session = Session(players, cpus, "c1", "c2");

            Play(session, "p1");

            // 選出が揃わないまま解決しようとしても、何も進みません。
            Assert.That(session.TryResolveRound(out RoundResult none, out BattleError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleError.SelectionIncomplete));
            Assert.That(none, Is.Null);
            Assert.That(session.History.Count, Is.EqualTo(1));
            Assert.That(session.PreviewPlayerSkill("p2").Activates, Is.True);

            // 新しいセッション（再戦）では履歴が空なので、初戦として扱います。
            BattleSession rematch = Session(players, cpus, "c2");

            Assert.That(rematch.PreviewPlayerSkill("p2").Activates, Is.False);
            Assert.That(Play(rematch, "p2").PlayerSkill.Reason, Is.EqualTo(UniqueSkillReason.FirstRound));
        }

        [Test]
        public void TheCpuChoiceStaysFairAndReproducibleWithSkills()
        {
            string[] picked = new string[3];
            string[] playerChoices = { "p1", "p2", "p3" };

            for (int i = 0; i < picked.Length; i++)
            {
                BattleSession session = new BattleSession(
                    TestBattleUnits.CreateSquad(SkillSquad("p")),
                    TestBattleUnits.CreateSquad(SkillSquad("c")),
                    new RandomUnitSelector(new FakeRandomSource(3, 1, 4, 1, 5)));

                Assert.That(session.SelectCpuUnit().Success, Is.True);
                picked[i] = session.PendingCpuInstanceId;

                // PLAYER がどのスキル持ちを出しても、CPU の選出は先に決まっていて変わりません。
                Assert.That(session.SelectPlayerUnit(playerChoices[i]).Success, Is.True);
                Assert.That(session.PendingCpuInstanceId, Is.EqualTo(picked[i]));
            }

            Assert.That(picked[1], Is.EqualTo(picked[0]), "同じ seed なら同じ選択です。");
            Assert.That(picked[2], Is.EqualTo(picked[0]));
        }

        // ---------------- 補助 ----------------

        private static IReadOnlyList<BattleUnit> SkillSquad(string prefix)
        {
            return new[]
            {
                One(prefix + "1", R, 76, UniqueSkillKind.CrimsonBite), One(prefix + "2", B, 58, UniqueSkillKind.TidalHowl),
                One(prefix + "3", G, 64, UniqueSkillKind.VerdantFang), Two(prefix + "4", R, 40, B, 21, UniqueSkillKind.StormBite),
                One(prefix + "5", R, 70), One(prefix + "6", B, 60), One(prefix + "7", G, 50),
            };
        }

        private static RoundWinner Mirror(RoundWinner winner)
        {
            return winner == RoundWinner.Player ? RoundWinner.Cpu : winner == RoundWinner.Cpu ? RoundWinner.Player : RoundWinner.Draw;
        }

        private static void AssertDefinition(string path, UniqueSkillKind kind, string skillName, string description)
        {
            CoreBeastDefinition definition = AssetDatabase.LoadAssetAtPath<CoreBeastDefinition>(path);

            Assert.That(definition, Is.Not.Null, path);
            Assert.That(definition.SkillKind, Is.EqualTo(kind), path);
            Assert.That(definition.SkillName, Is.EqualTo(skillName), path + ": スキル名は英語のままです。");
            Assert.That(definition.SkillDescription, Is.EqualTo(description), path);
        }

        private CoreBeastDefinition Definition(string id, UnitAttribute attribute, int power, UniqueSkillKind kind, string skillName)
        {
            CoreBeastDefinition definition = ScriptableObject.CreateInstance<CoreBeastDefinition>();
            created.Add(definition);

            SetField(definition, "beastId", id);
            SetField(definition, "attributePowers", new List<AttributePower> { new AttributePower(attribute, power) });
            SetField(definition, "skillKind", kind);
            SetField(definition, "skillName", skillName);

            return definition;
        }

        private static OwnedCoreBeast Owned(string instanceId, CoreBeastDefinition definition)
        {
            OwnedCoreBeast owned = new OwnedCoreBeast();

            SetField(owned, "instanceId", instanceId);
            SetField(owned, "definition", definition);

            return owned;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);

            field.SetValue(target, value);
        }

        private static BattleSession Session(BattleUnit[] players, BattleUnit[] cpus, params string[] cpuOrder)
        {
            int next = 0;

            return new BattleSession(
                TestBattleUnits.CreateSquad(players),
                TestBattleUnits.CreateSquad(cpus),
                new RecordingUnitSelector(candidates =>
                {
                    string id = next < cpuOrder.Length ? cpuOrder[next++] : candidates[0].InstanceId;

                    foreach (BattleUnit unit in candidates)
                    {
                        if (unit.InstanceId == id)
                        {
                            return unit;
                        }
                    }

                    return candidates[0];
                }));
        }

        private static RoundResult Play(BattleSession session, string playerId)
        {
            if (!session.HasCpuSelected)
            {
                Assert.That(session.SelectCpuUnit().Success, Is.True, "CPU の選出");
            }

            Assert.That(session.SelectPlayerUnit(playerId).Success, Is.True, "PLAYER の選出 " + playerId);
            Assert.That(session.TryResolveRound(out RoundResult result, out BattleError error), Is.True, error.ToString());

            return result;
        }
    }
}
