using System;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// PlayMode テストで、狙った展開を毎回同じ手順で作るための CPU の台本です。
    ///
    /// 以前は CPU が起動時刻の seed で無作為に選び、狙った展開になるまで最大40試合を試し直していました。
    /// そのため結果が乱数次第で、失敗することがありました。
    /// 今は seed を固定し（<see cref="Seed"/>）、CPU の選び方を <see cref="BattleScreenController.TestCpuSelectorFactory"/>
    /// から差し替えます。勝敗は従来どおり本物の <see cref="BattleRules"/> / <see cref="BattleSession"/> が決めます。
    /// </summary>
    internal static class BattleCpuScript
    {
        /// <summary>CPU 編成の組み方と、台本の外での選出に使う固定 seed。</summary>
        internal const int Seed = 20261016;

        /// <summary>
        /// シーンを読み込む前に呼びます。<paramref name="choose"/> が null を返したラウンドは、固定 seed の無作為選出です。
        /// </summary>
        internal static void Install(Func<BattleSession, IReadOnlyList<BattleUnit>, BattleUnit> choose)
        {
            BattleScreenController.TestCpuRandomSeed = Seed;
            BattleScreenController.TestCpuSelectorFactory = random => new ScriptedCpuSelector(random, choose);
        }

        /// <summary>TearDown で必ず呼びます。製品の既定（無作為・起動時刻の seed）へ戻します。</summary>
        internal static void Uninstall()
        {
            BattleScreenController.TestCpuRandomSeed = null;
            BattleScreenController.TestCpuSelectorFactory = null;
        }

        /// <summary>
        /// 次のラウンドで PLAYER が出せる勝敗の種類がいちばん多い個体を選びます。
        /// 次にどの勝敗を狙うかが CPU の選出時点で決まっていないテスト（1ラウンドずつ狙う）向けです。
        /// </summary>
        internal static BattleUnit MostVersatile(BattleSession session, IReadOnlyList<BattleUnit> candidates)
        {
            if (session == null)
            {
                return null;
            }

            BattleUnit best = null;
            int bestCount = -1;

            foreach (BattleUnit cpu in candidates)
            {
                HashSet<RoundWinner> outcomes = new HashSet<RoundWinner>();

                foreach (BattleUnit player in session.PlayerAvailableUnits)
                {
                    outcomes.Add(BattleLinkPrediction.Resolve(session, player, cpu).Winner);
                }

                if (outcomes.Count > bestCount)
                {
                    best = cpu;
                    bestCount = outcomes.Count;
                }
            }

            return best;
        }

        /// <summary>CPU 編成（非公開の状態は読むだけです）。</summary>
        internal static BattleSquad CpuSquadOf(BattleSession session)
        {
            return (BattleSquad)Read(session, "cpuSquad");
        }

        /// <summary>CPU が今のラウンドに選んだ個体（非公開の状態は読むだけです）。</summary>
        internal static BattleUnit PendingCpuUnitOf(BattleSession session)
        {
            return (BattleUnit)Read(session, "pendingCpuUnit");
        }

        internal static HashSet<string> UsedCpuIdsOf(BattleSession session)
        {
            return (HashSet<string>)Read(session, "usedCpuIds");
        }

        private static object Read(BattleSession session, string name)
        {
            FieldInfo field = typeof(BattleSession).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, "BattleSession." + name + " が見つかりません。");

            return field.GetValue(session);
        }

        private static BattleSession CurrentSession()
        {
            BattleScreenController controller = UnityEngine.Object.FindAnyObjectByType<BattleScreenController>();

            return controller != null && controller.Coordinator != null ? controller.Coordinator.Session : null;
        }

        private sealed class ScriptedCpuSelector : IBattleUnitSelector
        {
            private readonly RandomUnitSelector fallback;
            private readonly Func<BattleSession, IReadOnlyList<BattleUnit>, BattleUnit> choose;

            internal ScriptedCpuSelector(IRandomSource random, Func<BattleSession, IReadOnlyList<BattleUnit>, BattleUnit> choose)
            {
                fallback = new RandomUnitSelector(random);
                this.choose = choose;
            }

            public BattleUnit Select(IReadOnlyList<BattleUnit> availableUnits)
            {
                BattleUnit chosen = choose != null ? choose(CurrentSession(), availableUnits) : null;

                if (chosen != null)
                {
                    foreach (BattleUnit unit in availableUnits)
                    {
                        if (unit.InstanceId == chosen.InstanceId)
                        {
                            return unit;
                        }
                    }

                    Assert.Fail("台本の個体 " + chosen.InstanceId + " は CPU の未使用候補にありません。");
                }

                return fallback.Select(availableUnits);
            }
        }
    }

    /// <summary>
    /// 試合の残りの出し順（PLAYER と CPU の組）を、製品と同じ純粋関数 <see cref="BattleRoundEvaluator.Evaluate"/> で
    /// 先読みして探します。ラウンドの文脈（直前の個体・リンクの連鎖・勝敗）は製品の履歴と同じ作り方です。
    /// </summary>
    internal sealed class BattleMatchPlan
    {
        /// <summary>探索で評価する組み合わせ数の上限（探索が終わらない事態を防ぐため）。</summary>
        private const int EvaluationLimit = 400000;

        internal enum Step
        {
            /// <summary>この先で狙いに届く可能性がある。</summary>
            Continue,

            /// <summary>狙いに届いた（残りのラウンドは何でもよい）。</summary>
            Complete,

            /// <summary>狙いから外れた。</summary>
            Dead,
        }

        internal readonly struct Round
        {
            internal Round(BattleUnit player, BattleUnit cpu, RoundWinner winner)
            {
                Player = player;
                Cpu = cpu;
                Winner = winner;
            }

            internal BattleUnit Player { get; }

            internal BattleUnit Cpu { get; }

            internal RoundWinner Winner { get; }
        }

        private readonly List<Round> rounds;

        private BattleMatchPlan(BattleSession session, int firstRound, List<Round> rounds)
        {
            Session = session;
            FirstRound = firstRound;
            this.rounds = rounds;
        }

        /// <summary>この台本を作った試合。再戦で別の試合になれば使いません。</summary>
        internal BattleSession Session { get; }

        /// <summary>台本の最初のラウンドの、それまでに終わったラウンド数。</summary>
        internal int FirstRound { get; }

        internal IReadOnlyList<Round> Rounds => rounds;

        /// <summary>
        /// 今の試合の状態（使用済み・履歴・CPU が今回選んだ個体）から、狙いに届く出し順を探します。
        /// <paramref name="check"/> には、この試合の全ラウンドの勝敗（終わった分＋予想）を渡します。
        /// 届く出し順が無ければ null です。
        /// </summary>
        internal static BattleMatchPlan Find(BattleSession session, Func<IReadOnlyList<RoundWinner>, Step> check)
        {
            List<RoundWinner> outcomes = new List<RoundWinner>();
            int playerWins = 0;
            int cpuWins = 0;

            foreach (RoundResult result in session.History)
            {
                outcomes.Add(result.Winner);
                playerWins += result.Winner == RoundWinner.Player ? 1 : 0;
                cpuWins += result.Winner == RoundWinner.Cpu ? 1 : 0;
            }

            HashSet<string> usedCpu = new HashSet<string>(BattleCpuScript.UsedCpuIdsOf(session));
            List<BattleUnit> cpuLeft = new List<BattleUnit>();

            foreach (BattleUnit unit in BattleCpuScript.CpuSquadOf(session).Units)
            {
                if (!usedCpu.Contains(unit.InstanceId))
                {
                    cpuLeft.Add(unit);
                }
            }

            Search search = new Search
            {
                Check = check,
                Outcomes = outcomes,
                PlayerLeft = new List<BattleUnit>(session.PlayerAvailableUnits),
                CpuLeft = cpuLeft,
                FixedCpu = BattleCpuScript.PendingCpuUnitOf(session),
                Path = new List<Round>(),
            };

            bool found = search.Run(
                RoundSideContext.ForPlayer(session.History),
                RoundSideContext.ForCpu(session.History),
                playerWins,
                cpuWins);

            return found ? new BattleMatchPlan(session, session.CompletedRounds, search.Path) : null;
        }

        /// <summary>今のラウンドの台本。台本の外（狙いに届いた後）なら false です。</summary>
        internal bool TryGet(BattleSession session, out Round round)
        {
            int index = session.CompletedRounds - FirstRound;

            if (session == Session && index >= 0 && index < rounds.Count)
            {
                round = rounds[index];
                return true;
            }

            round = default;
            return false;
        }

        internal string Describe()
        {
            List<string> parts = new List<string>();

            foreach (Round r in rounds)
            {
                parts.Add(r.Player.InstanceId + " vs " + r.Cpu.InstanceId + " → " + r.Winner);
            }

            return "plan from round " + (FirstRound + 1) + ": " + string.Join(", ", parts);
        }

        private sealed class Search
        {
            internal Func<IReadOnlyList<RoundWinner>, Step> Check;
            internal List<RoundWinner> Outcomes;
            internal List<BattleUnit> PlayerLeft;
            internal List<BattleUnit> CpuLeft;
            internal BattleUnit FixedCpu;
            internal List<Round> Path;

            private int evaluations;

            internal bool Run(RoundSideContext player, RoundSideContext cpu, int playerWins, int cpuWins)
            {
                if (PlayerLeft.Count == 0 || evaluations > EvaluationLimit)
                {
                    return false;
                }

                // 最初のラウンドの CPU は選出済みです。2ラウンド目以降は台本で決めます。
                List<BattleUnit> cpuChoices = FixedCpu != null && Path.Count == 0
                    ? new List<BattleUnit> { FixedCpu }
                    : Distinct(CpuLeft);

                foreach (BattleUnit p in Distinct(PlayerLeft))
                {
                    foreach (BattleUnit c in cpuChoices)
                    {
                        evaluations++;

                        RoundEvaluation e = BattleRoundEvaluator.Evaluate(p, c, player, cpu);
                        RoundWinner winner = e.Outcome.Winner;
                        int pw = playerWins + (winner == RoundWinner.Player ? 1 : 0);
                        int cw = cpuWins + (winner == RoundWinner.Cpu ? 1 : 0);

                        Outcomes.Add(winner);
                        Path.Add(new Round(p, c, winner));
                        PlayerLeft.Remove(p);
                        CpuLeft.Remove(c);

                        Step step = Check(Outcomes);
                        bool finished = BattleSession.EvaluateMatchState(pw, cw, Outcomes.Count, BattleSession.MaxRounds) !=
                                        BattleMatchState.InProgress;

                        if (step == Step.Complete)
                        {
                            return true;
                        }

                        if (step == Step.Continue && !finished &&
                            Run(
                                new RoundSideContext(p, e.PlayerLink.ChainCount, RoundSideContext.ResultFor(winner, RoundWinner.Player)),
                                new RoundSideContext(c, e.CpuLink.ChainCount, RoundSideContext.ResultFor(winner, RoundWinner.Cpu)),
                                pw,
                                cw))
                        {
                            return true;
                        }

                        Outcomes.RemoveAt(Outcomes.Count - 1);
                        Path.RemoveAt(Path.Count - 1);
                        PlayerLeft.Add(p);
                        CpuLeft.Add(c);
                    }
                }

                return false;
            }

            /// <summary>
            /// 同じ属性・POWER・CORE・スキルの個体は勝敗も文脈も同じなので、1体だけ試します（順序は保ちます）。
            /// </summary>
            private static List<BattleUnit> Distinct(List<BattleUnit> units)
            {
                List<BattleUnit> distinct = new List<BattleUnit>();
                HashSet<string> seen = new HashSet<string>();
                List<BattleUnit> ordered = new List<BattleUnit>(units);

                ordered.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));

                foreach (BattleUnit unit in ordered)
                {
                    if (seen.Add(Signature(unit)))
                    {
                        distinct.Add(unit);
                    }
                }

                return distinct;
            }

            private static string Signature(BattleUnit unit)
            {
                List<string> parts = new List<string>();

                foreach (AttributePower ap in unit.AttributePowers)
                {
                    parts.Add(ap.Attribute + ":" + ap.Power);
                }

                return string.Join("/", parts) + "|" + unit.Core + "|" + unit.Skill;
            }
        }
    }
}
