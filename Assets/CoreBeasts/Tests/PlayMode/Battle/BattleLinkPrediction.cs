using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// PlayMode テストが「この個体を出せばどうなるか」を予想するための道具。
    ///
    /// Phase 4 以降、実際のラウンドは ATTRIBUTE LINK を反映した POWER で判定されるため、
    /// 予想も同じく、セッションの公開履歴から両陣営の LINK を求めて反映した個体で
    /// 本物の <see cref="BattleRules"/> に判定させます（判定の写しは作りません）。
    /// </summary>
    internal static class BattleLinkPrediction
    {
        internal static RoundOutcome Resolve(BattleSession session, BattleUnit player, BattleUnit cpu)
        {
            IReadOnlyList<RoundResult> history = session.History;
            RoundResult last = history.Count > 0 ? history[history.Count - 1] : null;

            return Resolve(
                last?.PlayerUnit, last != null ? last.PlayerLink.ChainCount : 0, player,
                last?.CpuUnit, last != null ? last.CpuLink.ChainCount : 0, cpu,
                out _, out _);
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
            RoundResult last = history.Count > 0 ? history[history.Count - 1] : null;

            for (int i = 0; i < 2; i++)
            {
                BattleUnit now = available[i];
                BattleUnit next = available[1 - i];

                RoundOutcome first = Resolve(
                    last?.PlayerUnit, last != null ? last.PlayerLink.ChainCount : 0, now,
                    last?.CpuUnit, last != null ? last.CpuLink.ChainCount : 0, cpu,
                    out AttributeLinkResult playerLink, out AttributeLinkResult cpuLink);

                RoundOutcome second = Resolve(now, playerLink.ChainCount, next, cpu, cpuLink.ChainCount, lastCpu, out _, out _);

                plans.Add(new TwoRoundPlan(now, first.Winner, second.Winner));
            }

            return plans;
        }

        private static object Read(BattleSession session, string name)
        {
            FieldInfo field = typeof(BattleSession).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, "BattleSession." + name + " が見つかりません。");

            return field.GetValue(session);
        }

        /// <summary>
        /// 直前の個体とチェーン数を与えて1ラウンドを予想します（2ラウンド先を読むときに使います）。
        /// </summary>
        internal static RoundOutcome Resolve(
            BattleUnit previousPlayer, int previousPlayerChain, BattleUnit player,
            BattleUnit previousCpu, int previousCpuChain, BattleUnit cpu,
            out AttributeLinkResult playerLink, out AttributeLinkResult cpuLink)
        {
            playerLink = AttributeLink.Evaluate(previousPlayer, player, previousPlayerChain);
            cpuLink = AttributeLink.Evaluate(previousCpu, cpu, previousCpuChain);

            return BattleRules.ResolveRound(AttributeLink.Apply(player, playerLink), AttributeLink.Apply(cpu, cpuLink));
        }
    }
}
