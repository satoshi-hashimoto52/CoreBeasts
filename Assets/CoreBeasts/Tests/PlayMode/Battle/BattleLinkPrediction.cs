using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// PlayMode テストが「この個体を出せばどうなるか」を予想するための道具。
    ///
    /// 実際のラウンドは ATTRIBUTE LINK（Phase 4）とユニークスキル（Phase 5）を反映した POWER で判定されるため、
    /// 予想も同じく、セッションの公開履歴から両陣営の文脈を作り、製品と同じ純粋関数
    /// <see cref="BattleRoundEvaluator.Evaluate"/> に判定させます（判定の写しは作りません）。
    /// </summary>
    internal static class BattleLinkPrediction
    {
        internal static RoundOutcome Resolve(BattleSession session, BattleUnit player, BattleUnit cpu)
        {
            IReadOnlyList<RoundResult> history = session.History;

            return BattleRoundEvaluator.Evaluate(
                player, cpu, RoundSideContext.ForPlayer(history), RoundSideContext.ForCpu(history)).Outcome;
        }

        /// <summary>残り2ラウンドの出し順1通りぶんの予想。</summary>
        internal readonly struct TwoRoundPlan
        {
            internal TwoRoundPlan(BattleUnit now, RoundWinner first, RoundWinner second)
            {
                Now = now;
                First = first;
                Second = second;
            }

            /// <summary>今回出す個体（もう1体は最終ラウンドに出ます）。</summary>
            internal BattleUnit Now { get; }

            internal RoundWinner First { get; }

            internal RoundWinner Second { get; }
        }

        /// <summary>
        /// プレイヤーの残りが2体なら、CPU の最後の1体も決まっています（編成から使用済みと今回の選出を除いた残り）。
        /// 2通りの出し順それぞれについて、今回と最終ラウンドの勝敗を ATTRIBUTE LINK のチェーンも含めて予想します。
        /// CPU の非公開の状態は読むだけで、書き換えません。残りが2体でなければ空です。
        /// </summary>
        internal static List<TwoRoundPlan> PredictLastTwoRounds(BattleSession session)
        {
            List<TwoRoundPlan> plans = new List<TwoRoundPlan>();
            IReadOnlyList<BattleUnit> available = session.PlayerAvailableUnits;

            if (available.Count != 2)
            {
                return plans;
            }

            BattleUnit cpu = (BattleUnit)Read(session, "pendingCpuUnit");
            BattleSquad cpuSquad = (BattleSquad)Read(session, "cpuSquad");
            HashSet<string> usedCpu = (HashSet<string>)Read(session, "usedCpuIds");
            BattleUnit lastCpu = null;

            foreach (BattleUnit unit in cpuSquad.Units)
            {
                if (cpu != null && !usedCpu.Contains(unit.InstanceId) && unit.InstanceId != cpu.InstanceId)
                {
                    lastCpu = unit;
                }
            }

            if (cpu == null || lastCpu == null)
            {
                return plans;
            }

            IReadOnlyList<RoundResult> history = session.History;
            RoundSideContext playerContext = RoundSideContext.ForPlayer(history);
            RoundSideContext cpuContext = RoundSideContext.ForCpu(history);

            for (int i = 0; i < 2; i++)
            {
                BattleUnit now = available[i];
                BattleUnit next = available[1 - i];

                RoundEvaluation first = BattleRoundEvaluator.Evaluate(now, cpu, playerContext, cpuContext);

                // 最終ラウンドの文脈は、今回の個体・チェーン・結果から作ります（製品の履歴と同じ作り方）。
                RoundSideContext playerNext = new RoundSideContext(
                    now, first.PlayerLink.ChainCount, RoundSideContext.ResultFor(first.Outcome.Winner, RoundWinner.Player));
                RoundSideContext cpuNext = new RoundSideContext(
                    cpu, first.CpuLink.ChainCount, RoundSideContext.ResultFor(first.Outcome.Winner, RoundWinner.Cpu));

                RoundEvaluation second = BattleRoundEvaluator.Evaluate(next, lastCpu, playerNext, cpuNext);

                plans.Add(new TwoRoundPlan(now, first.Outcome.Winner, second.Outcome.Winner));
            }

            return plans;
        }

        private static object Read(BattleSession session, string name)
        {
            FieldInfo field = typeof(BattleSession).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, "BattleSession." + name + " が見つかりません。");

            return field.GetValue(session);
        }

    }
}
