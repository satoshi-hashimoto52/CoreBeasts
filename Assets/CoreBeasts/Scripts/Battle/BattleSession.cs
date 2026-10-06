using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using CoreBeasts.Units;

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
        /// <summary>この勝利数へ到達した時点で即勝利。</summary>
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

        /// <summary>
        /// CPUが今ラウンドに選んだ個体のID。まだなら null。
        ///
        /// 公平性（プレイヤーより先に確定していること）を確かめるためだけのもので、
        /// <c>internal</c> にしてテストアセンブリからしか見えないようにしています。
        /// 画面へ出すと Reveal 前に次の敵が分かってしまうため、UI からは触れません。
        /// </summary>
        internal string PendingCpuInstanceId =>
            pendingCpuUnit != null ? pendingCpuUnit.InstanceId : null;

        /// <summary>プレイヤーが今ラウンドに選んだ個体。未選出なら null。</summary>
        public BattleUnit SelectedPlayerUnit => pendingPlayerUnit;

        /// <summary>解決済みラウンドの記録。読み取り専用です。</summary>
        public IReadOnlyList<RoundResult> History => readOnlyHistory;

        /// <summary>プレイヤーの編成。</summary>
        public BattleSquad PlayerSquad => playerSquad;

        /// <summary>
        /// 指定したプレイヤー個体を今ラウンドに出した場合の ATTRIBUTE LINK（副作用なし）。
        /// 選択前の予告に使います。使用済み・編成外・終了後は <see cref="AttributeLinkResult.None"/> です。
        ///
        /// プレイヤー自身の履歴だけを見ます。CPU の選出や CPU 側の LINK は返しません。
        /// </summary>
        public AttributeLinkResult PreviewPlayerLink(string instanceId)
        {
            if (IsFinished || string.IsNullOrEmpty(instanceId) || usedPlayerIds.Contains(instanceId))
            {
                return AttributeLinkResult.None;
            }

            BattleUnit candidate = playerSquad.Find(instanceId);

            if (candidate == null)
            {
                return AttributeLinkResult.None;
            }

            RoundSideContext context = RoundSideContext.ForPlayer(history);

            return AttributeLink.Evaluate(context.PreviousUnit, candidate, context.PreviousLinkChain);
        }

        /// <summary>
        /// 指定したプレイヤー個体を今ラウンドに出した場合のユニークスキルの予告（副作用なし）。
        /// 使用済み・編成外・終了後は <see cref="UniqueSkillPreview.None"/> です。
        ///
        /// プレイヤー自身の直前の個体と結果だけを見ます。CPU の選出・属性・発動は使わず、返しません。
        /// 相手の選出で決まる STORM BITE は、発動を予告しません（<see cref="UniqueSkillPreviewState.DependsOnOpponent"/>）。
        /// </summary>
        public UniqueSkillPreview PreviewPlayerSkill(string instanceId)
        {
            if (IsFinished || string.IsNullOrEmpty(instanceId) || usedPlayerIds.Contains(instanceId))
            {
                return UniqueSkillPreview.None;
            }

            BattleUnit candidate = playerSquad.Find(instanceId);

            if (candidate == null)
            {
                return UniqueSkillPreview.None;
            }

            RoundSideContext context = RoundSideContext.ForPlayer(history);

            return UniqueSkill.Preview(candidate, context.PreviousUnit, context.PreviousResult);
        }

        /// <summary>プレイヤーの未使用個体。呼ぶたびに読み取り専用の新しい一覧を返します。</summary>
        public IReadOnlyList<BattleUnit> PlayerAvailableUnits =>
            CreateAvailableUnits(playerSquad, usedPlayerIds);

        /// <summary>
        /// 敵の残存戦力を、方針に応じた読み取りとして返します。
        ///
        /// 個体そのものは渡しません。画面は返ってきた値を出すだけで、
        /// 誰が次に出るかを計算することはできません。
        ///
        /// 使用済みになるのはラウンドを解決したときだけです。
        /// 選出しただけ（Pending）では残存数は減りません。
        /// </summary>
        public EnemyForceReadout DescribeEnemyForce(
            EnemyForceDisclosure policy = EnemyForceDisclosure.FullComposition,
            int revealCompositions = 0,
            IReadOnlyList<UnitAttribute> revealAttributeCounts = null)
        {
            return EnemyForceModel.Build(
                CreateAvailableUnits(cpuSquad, usedCpuIds),
                MaxRounds,
                policy,
                revealCompositions,
                revealAttributeCounts);
        }

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

            // ATTRIBUTE LINK（Phase 4）とユニークスキル（Phase 5）: 双方に同じ規則で、自分の履歴だけから求めます。
            // 元の個体は書き換えず、反映した一時的な個体を変更していない BattleRules で判定します。
            // ここまでは状態を一切変えません。判定が例外を投げても、チェーンもスキルも履歴も進みません。
            RoundEvaluation evaluation = BattleRoundEvaluator.Evaluate(
                playerUnit,
                cpuUnit,
                RoundSideContext.ForPlayer(history),
                RoundSideContext.ForCpu(history));

            RoundOutcome outcome = evaluation.Outcome;
            RoundResult round = new RoundResult(CurrentRound, evaluation);

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
            State = EvaluateMatchState(
                PlayerWins,
                CpuWins,
                history.Count,
                MaxRounds);
        }

        /// <summary>
        /// マッチの決着判定。状態を持たない純粋関数です。
        /// 同じ入力からは常に同じ結果を返し、ここ以外で勝敗を数え直しません。
        ///
        /// 判定の順序:
        ///   1. どちらかが<see cref="WinsRequired"/>勝へ到達したら、その時点で勝利
        ///   2. 残り全勝しても相手が追いつけないなら、その時点で勝利
        ///      （「同点へ追いつける」可能性が残っているあいだは終わりません）
        ///   3. まだラウンドが残っていれば進行中
        ///   4. 使い切っていれば、単純に勝利数を比べる。同数だけが引き分け
        ///
        /// ラウンドの引き分けはどちらの勝利数にも入りません。
        /// そのため 3勝2敗2分は「3 &gt; 2」でプレイヤーの勝ちになり、
        /// 3勝3敗1分のような同数だけが引き分けになります。
        /// </summary>
        /// <param name="playerWins">プレイヤーのラウンド勝利数。</param>
        /// <param name="cpuWins">CPUのラウンド勝利数。</param>
        /// <param name="completedRounds">解決済みのラウンド数。</param>
        /// <param name="maxRounds">1マッチの最大ラウンド数。</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 実際の進行では起こり得ない値を渡した場合。
        /// 黙って辻褄を合わせると誤った勝敗をそのまま表示してしまうため、
        /// ここで止めます。
        /// </exception>
        public static BattleMatchState EvaluateMatchState(
            int playerWins,
            int cpuWins,
            int completedRounds,
            int maxRounds)
        {
            if (maxRounds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRounds), maxRounds, "最大ラウンド数は1以上です。");
            }

            if (playerWins < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerWins), playerWins, "勝利数は負になりません。");
            }

            if (cpuWins < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cpuWins), cpuWins, "勝利数は負になりません。");
            }

            if (completedRounds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedRounds),
                    completedRounds,
                    "完了ラウンド数は負になりません。");
            }

            if (completedRounds > maxRounds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedRounds),
                    completedRounds,
                    "完了ラウンド数が最大ラウンド数を超えています。");
            }

            if (playerWins + cpuWins > completedRounds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedRounds),
                    completedRounds,
                    "勝利数の合計が完了ラウンド数を超えています。" +
                    "引き分けは勝利数へ数えません。");
            }

            int remainingRounds = maxRounds - completedRounds;

            // 1. 先取到達。
            if (playerWins >= WinsRequired)
            {
                return BattleMatchState.PlayerWin;
            }

            if (cpuWins >= WinsRequired)
            {
                return BattleMatchState.CpuWin;
            }

            // 2. 残り全勝でも追いつけないなら確定。
            //    等号では終わりません（同点まで追いつける余地が残っています）。
            if (playerWins > cpuWins + remainingRounds)
            {
                return BattleMatchState.PlayerWin;
            }

            if (cpuWins > playerWins + remainingRounds)
            {
                return BattleMatchState.CpuWin;
            }

            // 3. まだ戦えるなら続行。リードしているだけでは終わりません。
            if (remainingRounds > 0)
            {
                return BattleMatchState.InProgress;
            }

            // 4. 使い切ったので勝利数を比べます。同数だけが引き分けです。
            if (playerWins > cpuWins)
            {
                return BattleMatchState.PlayerWin;
            }

            if (cpuWins > playerWins)
            {
                return BattleMatchState.CpuWin;
            }

            return BattleMatchState.Draw;
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
