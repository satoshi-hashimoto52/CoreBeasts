using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 7体対7体1マッチの進行。選出、使用済み管理、ラウンド解決、勝利数、終了判定を持ちます。
    /// UI・シーン・時刻へ依存せず、通常のC#オブジェクトとして生成できます。
    ///
    /// 1ラウンドの流れ:
    ///   1. プレイヤーとCPUがそれぞれ未使用の1体を選ぶ（順不同・相手の選択は見えない）
    ///   2. 双方が揃ってから<see cref="TryResolveRound"/>で解決する
    ///
    /// CPUの選出結果は解決前には公開しません。解決後に
    /// <see cref="History"/>のラウンド記録から参照できます。
    /// 受け付けられなかった操作はセッションの状態を一切変更しません。
    /// </summary>
    public sealed class BattleSession
    {
        /// <summary>マッチ勝利に必要な勝利数。</summary>
        public const int WinsRequired = 4;

        /// <summary>1マッチの最大ラウンド数。</summary>
        public const int MaxRounds = BattleSquad.UnitCount;

        private readonly BattleSquad playerSquad;
        private readonly BattleSquad cpuSquad;
        private readonly IBattleUnitSelector cpuSelector;

        private readonly HashSet<string> usedPlayerIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> usedCpuIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly List<RoundResult> history = new List<RoundResult>();
        private readonly ReadOnlyCollection<RoundResult> readOnlyHistory;

        private BattleUnit pendingPlayerUnit;
        private BattleUnit pendingCpuUnit;

        public BattleSession(
            BattleSquad playerSquad,
            BattleSquad cpuSquad,
            IBattleUnitSelector cpuSelector)
        {
            this.playerSquad = playerSquad
                ?? throw new ArgumentNullException(nameof(playerSquad));

            this.cpuSquad = cpuSquad
                ?? throw new ArgumentNullException(nameof(cpuSquad));

            this.cpuSelector = cpuSelector
                ?? throw new ArgumentNullException(nameof(cpuSelector));

            readOnlyHistory = history.AsReadOnly();
            State = BattleMatchState.InProgress;
        }

        /// <summary>マッチの進行状態。</summary>
        public BattleMatchState State { get; private set; }

        /// <summary>マッチが決着しているか。</summary>
        public bool IsFinished => State != BattleMatchState.InProgress;

        /// <summary>プレイヤーのラウンド勝利数。</summary>
        public int PlayerWins { get; private set; }

        /// <summary>CPUのラウンド勝利数。</summary>
        public int CpuWins { get; private set; }

        /// <summary>解決済みラウンド数。</summary>
        public int CompletedRounds => history.Count;

        /// <summary>これから解決するラウンド番号。1から始まります。</summary>
        public int CurrentRound => history.Count + 1;

        /// <summary>プレイヤーが今ラウンドの選出を済ませたか。</summary>
        public bool HasPlayerSelected => pendingPlayerUnit != null;

        /// <summary>CPUが今ラウンドの選出を済ませたか。どの個体かは公開しません。</summary>
        public bool HasCpuSelected => pendingCpuUnit != null;

        /// <summary>プレイヤーが今ラウンドに選んだ個体。未選出なら null。</summary>
        public BattleUnit SelectedPlayerUnit => pendingPlayerUnit;

        /// <summary>解決済みラウンドの記録。読み取り専用です。</summary>
        public IReadOnlyList<RoundResult> History => readOnlyHistory;

        /// <summary>プレイヤーの編成。</summary>
        public BattleSquad PlayerSquad => playerSquad;

        /// <summary>プレイヤーの未使用個体。呼ぶたびに読み取り専用の新しい一覧を返します。</summary>
        public IReadOnlyList<BattleUnit> PlayerAvailableUnits =>
            CreateAvailableUnits(playerSquad, usedPlayerIds);

        /// <summary>指定個体をプレイヤーが使用済みか。</summary>
        public bool IsPlayerUnitUsed(string instanceId)
        {
            return !string.IsNullOrEmpty(instanceId)
                && usedPlayerIds.Contains(instanceId);
        }

        /// <summary>
        /// プレイヤーの選出。受け付けた場合だけ状態が進みます。
        /// 相手編成の個体、存在しないID、使用済み個体、二重選択、
        /// 終了後の操作はいずれも拒否され、状態は変わりません。
        /// </summary>
        public SelectionResult SelectPlayerUnit(string instanceId)
        {
            if (IsFinished)
            {
                return SelectionResult.Fail(BattleError.MatchAlreadyFinished);
            }

            if (HasPlayerSelected)
            {
                return SelectionResult.Fail(BattleError.AlreadySelected);
            }

            if (string.IsNullOrEmpty(instanceId))
            {
                return SelectionResult.Fail(BattleError.EmptyInstanceId);
            }

            BattleUnit unit = playerSquad.Find(instanceId);

            if (unit == null)
            {
                return SelectionResult.Fail(BattleError.UnitNotInSquad);
            }

            if (usedPlayerIds.Contains(instanceId))
            {
                return SelectionResult.Fail(BattleError.UnitAlreadyUsed);
            }

            pendingPlayerUnit = unit;

            return SelectionResult.Ok();
        }

        /// <summary>
        /// CPUの選出。注入された選択器へ未使用候補だけを渡します。
        /// プレイヤーの今回の選択は渡さないため、相手の手を見て選ぶことはできません。
        /// </summary>
        public SelectionResult SelectCpuUnit()
        {
            if (IsFinished)
            {
                return SelectionResult.Fail(BattleError.MatchAlreadyFinished);
            }

            if (HasCpuSelected)
            {
                return SelectionResult.Fail(BattleError.AlreadySelected);
            }

            IReadOnlyList<BattleUnit> candidates =
                CreateAvailableUnits(cpuSquad, usedCpuIds);

            if (candidates.Count == 0)
            {
                return SelectionResult.Fail(BattleError.NoAvailableUnit);
            }

            BattleUnit chosen = cpuSelector.Select(candidates);

            if (chosen == null)
            {
                return SelectionResult.Fail(BattleError.InvalidSelectorResult);
            }

            // 選択器は外部実装なので、候補外や使用済みを返していないか確かめます。
            if (!cpuSquad.Contains(chosen.InstanceId)
                || usedCpuIds.Contains(chosen.InstanceId))
            {
                return SelectionResult.Fail(BattleError.InvalidSelectorResult);
            }

            pendingCpuUnit = chosen;

            return SelectionResult.Ok();
        }

        /// <summary>
        /// 双方の選出が揃ったラウンドを解決します。
        /// 成功した場合だけ、使用済み登録、勝利数、履歴、終了判定が更新されます。
        /// </summary>
        public bool TryResolveRound(out RoundResult result, out BattleError error)
        {
            result = null;

            if (IsFinished)
            {
                error = BattleError.MatchAlreadyFinished;
                return false;
            }

            if (!HasPlayerSelected || !HasCpuSelected)
            {
                error = BattleError.SelectionIncomplete;
                return false;
            }

            BattleUnit playerUnit = pendingPlayerUnit;
            BattleUnit cpuUnit = pendingCpuUnit;

            RoundOutcome outcome = BattleRules.ResolveRound(playerUnit, cpuUnit);

            RoundResult round = new RoundResult(
                CurrentRound,
                playerUnit,
                cpuUnit,
                outcome.Winner,
                outcome.Decision);

            usedPlayerIds.Add(playerUnit.InstanceId);
            usedCpuIds.Add(cpuUnit.InstanceId);

            history.Add(round);

            if (outcome.Winner == RoundWinner.Player)
            {
                PlayerWins++;
            }
            else if (outcome.Winner == RoundWinner.Cpu)
            {
                CpuWins++;
            }

            pendingPlayerUnit = null;
            pendingCpuUnit = null;

            UpdateState();

            result = round;
            error = BattleError.None;

            return true;
        }

        private void UpdateState()
        {
            if (PlayerWins >= WinsRequired)
            {
                State = BattleMatchState.PlayerWin;
                return;
            }

            if (CpuWins >= WinsRequired)
            {
                State = BattleMatchState.CpuWin;
                return;
            }

            if (history.Count >= MaxRounds)
            {
                State = BattleMatchState.Draw;
            }
        }

        private static IReadOnlyList<BattleUnit> CreateAvailableUnits(
            BattleSquad squad,
            HashSet<string> usedIds)
        {
            List<BattleUnit> available = new List<BattleUnit>(squad.Count);
            IReadOnlyList<BattleUnit> units = squad.Units;

            for (int i = 0; i < units.Count; i++)
            {
                BattleUnit unit = units[i];

                if (!usedIds.Contains(unit.InstanceId))
                {
                    available.Add(unit);
                }
            }

            return available.AsReadOnly();
        }
    }
}
