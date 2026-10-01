using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// CPUの公平性。
    ///
    /// CPUはプレイヤーが選ぶ前に自分の個体を決め、
    /// プレイヤーが何を選んでも決め直しません。
    /// 「相手の手を見てから選ぶ」ことが構造的にできないことを確かめます。
    /// </summary>
    public sealed class CpuFairnessTests
    {
        private static BattleSession Session(
            UnitAttribute playerAttribute, IBattleUnitSelector selector = null)
        {
            return new BattleSession(
                TestBattleUnits.CreateSquad("p", playerAttribute, 50),
                TestBattleUnits.CreateSquad("c", UnitAttribute.Red, 50),
                selector ?? RecordingUnitSelector.First());
        }

        [Test]
        public void TheCpuIsDecidedBeforeThePlayerChooses()
        {
            BattleSession session = Session(UnitAttribute.Red);

            Assert.That(session.HasCpuSelected, Is.False);

            session.SelectCpuUnit();

            Assert.That(session.HasCpuSelected, Is.True, "先にCPUが確定します。");
            Assert.That(session.HasPlayerSelected, Is.False, "プレイヤーはまだ未選出です。");

            Assert.That(
                session.PendingCpuInstanceId,
                Is.Not.Null.And.Not.Empty,
                "CPUの個体が決まっていません。");
        }

        [Test]
        public void ThePlayerChoiceNeverChangesTheCpuUnit()
        {
            BattleSession session = Session(UnitAttribute.Red);

            session.SelectCpuUnit();

            string decided = session.PendingCpuInstanceId;

            // プレイヤーが選んでも、CPUは動きません。
            session.SelectPlayerUnit("p0");

            Assert.That(session.PendingCpuInstanceId, Is.EqualTo(decided));

            // 別の個体を選ぼうとしても（二重選択は拒否されます）CPUは不変です。
            session.SelectPlayerUnit("p3");

            Assert.That(
                session.PendingCpuInstanceId,
                Is.EqualTo(decided),
                "プレイヤーの選択でCPUが変わっています。");
        }

        [Test]
        public void TheCpuCannotSeeThePlayerAttribute()
        {
            // プレイヤーの属性が違っても、同じ選択器は同じ順で選びます。
            string[] picked = new string[3];

            UnitAttribute[] attributes =
            {
                UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue,
            };

            for (int i = 0; i < attributes.Length; i++)
            {
                BattleSession session = Session(attributes[i]);

                session.SelectPlayerUnit("p0");
                session.SelectCpuUnit();

                picked[i] = session.PendingCpuInstanceId;
            }

            Assert.That(picked[1], Is.EqualTo(picked[0]), "相手の属性で選び直しています。");
            Assert.That(picked[2], Is.EqualTo(picked[0]));
        }

        [Test]
        public void TheSameSeedGivesTheSameCpuChoice()
        {
            string[] picked = new string[2];

            for (int i = 0; i < 2; i++)
            {
                BattleSession session = new BattleSession(
                    TestBattleUnits.CreateSquad("p", UnitAttribute.Red, 50),
                    TestBattleUnits.CreateSquad("c", UnitAttribute.Red, 50),
                    new RandomUnitSelector(new FakeRandomSource(3, 1, 4, 1, 5)));

                session.SelectCpuUnit();

                picked[i] = session.PendingCpuInstanceId;
            }

            Assert.That(picked[1], Is.EqualTo(picked[0]), "同じseedなら同じ選択です。");
        }

        [Test]
        public void TheSelectorOnlyEverSeesItsOwnAvailableUnits()
        {
            RecordingUnitSelector selector = RecordingUnitSelector.First();

            BattleSession session = new BattleSession(
                TestBattleUnits.CreateSquad("p", UnitAttribute.Red, 50),
                TestBattleUnits.CreateSquad("c", UnitAttribute.Green, 50),
                selector);

            session.SelectPlayerUnit("p0");
            session.SelectCpuUnit();

            Assert.That(selector.ReceivedCandidateIds, Is.Not.Empty);

            string[] candidates = selector.ReceivedCandidateIds[0];

            for (int i = 0; i < candidates.Length; i++)
            {
                Assert.That(
                    candidates[i],
                    Does.StartWith("c"),
                    "選択器へプレイヤーの個体が渡っています。");
            }

            Assert.That(
                candidates.Length,
                Is.EqualTo(BattleSession.MaxRounds),
                "候補はCPU自身の未使用個体だけです。");
        }

        [Test]
        public void TheRemainingForceNeverPointsAtThePendingUnit()
        {
            BattleSession session = Session(UnitAttribute.Red);

            session.SelectCpuUnit();

            EnemyForceReadout readout =
                session.DescribeEnemyForce(EnemyForceDisclosure.FullComposition);

            // 選出しただけでは、残存は減りません。
            Assert.That(
                readout.RemainingCount,
                Is.EqualTo(BattleSession.MaxRounds),
                "Pending で残存が減っています（次の敵が分かってしまいます）。");

            Assert.That(readout.UsedCount, Is.Zero);

            // どの構成が選ばれたかを示す印はありません。
            Assert.That(readout.Compositions.Count, Is.EqualTo(BattleSession.MaxRounds));
        }

        [Test]
        public void TheRemainingForceDropsOnlyAfterTheRoundIsResolved()
        {
            BattleSession session = Session(UnitAttribute.Red);

            session.SelectPlayerUnit("p0");
            session.SelectCpuUnit();

            Assert.That(
                session.DescribeEnemyForce().RemainingCount,
                Is.EqualTo(7),
                "解決前は減りません。");

            Assert.That(
                session.TryResolveRound(out RoundResult _, out BattleError _),
                Is.True);

            Assert.That(
                session.DescribeEnemyForce().RemainingCount,
                Is.EqualTo(6),
                "解決後に1体ぶん減ります。");
        }

        [Test]
        public void ARematchRestoresAllSevenEnemyUnits()
        {
            BattleSession session = Session(UnitAttribute.Red);

            session.SelectPlayerUnit("p0");
            session.SelectCpuUnit();
            session.TryResolveRound(out RoundResult _, out BattleError _);

            Assert.That(session.DescribeEnemyForce().RemainingCount, Is.EqualTo(6));

            // マッチを作り直すと、7体そろった状態から始まります。
            BattleSession fresh = Session(UnitAttribute.Red);

            Assert.That(fresh.DescribeEnemyForce().RemainingCount, Is.EqualTo(7));
            Assert.That(fresh.DescribeEnemyForce().UsedCount, Is.Zero);
        }
    }
}
