using System;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の進行役。Unityのシーン・コンポーネント・時間へ依存しないため、
    /// 通常のC#オブジェクトとして生成でき、そのままテストできます。
    ///
    /// 勝敗判定と使用済み管理は行いません。すべて<see cref="BattleSession"/>へ委ねます。
    /// ここが持つのは「今どの入力を受け付けるか」という画面側の状態だけです。
    ///
    /// 1ラウンドの流れ:
    ///   1. <see cref="Begin"/> / <see cref="AdvanceToNextRound"/> がラウンドを開始し、
    ///      その場で<see cref="BattleSession.SelectCpuUnit"/>を呼びます（非公開選出）
    ///   2. <see cref="SelectPlayerUnit"/> で出す個体を選ぶ（何度でも変更可）
    ///   3. <see cref="Deploy"/> で確定し、その場で解決まで進む
    ///   4. 演出が終わったら<see cref="CompleteResolve"/>
    ///   5. 演出と結果表示が終わったら<see cref="PublishPendingOutcome"/>
    ///      （ここで初めて今回の勝敗バッジを公開します）
    ///   6. <see cref="AdvanceToNextRound"/>
    ///
    /// CPUの選出内容はここからは一切公開しません。
    /// 解決後の<see cref="LastResult"/>だけが双方の選出を持ちます。
    /// </summary>
    public sealed class BattleFlowCoordinator
    {
        private readonly IBattleMatchSource matchSource;

        /// <summary>
        /// 「表示してよい」と確定した結果を、PLAYER側の個体IDごとに控えます。
        /// 進行そのものには使いません。トレイのバッジ表示だけのための控えです。
        ///
        /// 解決直後の結果はここへ入れません。演出と結果表示が終わり、
        /// <see cref="PublishPendingOutcome"/>が呼ばれてから移します。
        /// </summary>
        private readonly BattleOutcomeLedger presentedOutcomes =
            new BattleOutcomeLedger();

        /// <summary>
        /// 「画面上で勝敗が公開済み」と確定した結果を、PLAYER側の個体IDごとに控えます。
        ///
        /// <see cref="presentedOutcomes"/>（W/L/Dバッジ）より一段早い控えです。
        /// 結果バナーを出す時点で<see cref="RevealRoundOutcome"/>から記録し、
        /// 敗者のグレー化だけを先に適用できるようにします。
        /// バッジの公開時期は従来どおり変えません。
        /// </summary>
        private readonly BattleOutcomeLedger revealedOutcomes =
            new BattleOutcomeLedger();

        /// <summary>
        /// 解決済みだが、まだ公開していないラウンド。
        /// DEPLOY直後にここへ入り、演出と結果表示が終わってから
        /// <see cref="presentedOutcomes"/>へ移ります。
        /// DEPLOYを押した瞬間に勝敗が分かってしまうのを防ぐための仕切りです。
        /// </summary>
        private RoundResult pendingResult;

        /// <summary>未公開の結果を、すでに画面へ出したか（二重適用よけ）。</summary>
        private bool pendingRevealed;

        public BattleFlowCoordinator(IBattleMatchSource matchSource)
        {
            this.matchSource = matchSource
                ?? throw new ArgumentNullException(nameof(matchSource));
        }

        /// <summary>画面の状態。</summary>
        public BattleUiState State { get; private set; } = BattleUiState.Loading;

        /// <summary>進行中のマッチ。未開始・編成不足なら null。</summary>
        public BattleSession Session { get; private set; }

        /// <summary>プレイヤーが今ラウンドに選んでいる個体ID。未選択なら null。</summary>
        public string SelectedPlayerInstanceId { get; private set; }

        /// <summary>直近に解決したラウンド。まだ1ラウンドも解決していなければ null。</summary>
        public RoundResult LastResult { get; private set; }

        /// <summary>
        /// 個体IDごとの「公開済み」の結果。トレイへ渡すのは常にこれです。
        /// 演出中のラウンドは含みません。新しいマッチを始めるたびに空へ戻します。
        /// </summary>
        public IBattleSlotOutcomeSource Outcomes => presentedOutcomes;

        /// <summary>
        /// <see cref="Outcomes"/>と同じ実体。
        /// 「公開済みだけを渡している」ことを呼び出し側で明示したいときに使います。
        /// </summary>
        public IBattleSlotOutcomeSource PresentedOutcomes => presentedOutcomes;

        /// <summary>
        /// 個体IDごとの「画面上で勝敗が公開済み」の結果。
        /// 敗者のグレー化はここだけを見ます。演出中のラウンドは含みません。
        /// </summary>
        public IBattleSlotOutcomeSource RevealedOutcomes => revealedOutcomes;

        /// <summary>
        /// 直近に公開したラウンドの、PLAYERから見た結果。
        /// 次のラウンドが始まると<see cref="BattleSlotOutcome.None"/>へ戻ります。
        /// 出場中の2枠のどちらをグレー化するかは、これだけで決まります。
        /// </summary>
        public BattleSlotOutcome LastRevealedOutcome { get; private set; } =
            BattleSlotOutcome.None;

        /// <summary>直近に公開したラウンドでPLAYERが出した個体ID。無ければ null。</summary>
        public string LastRevealedPlayerInstanceId { get; private set; }

        /// <summary>まだ公開していない結果があるか。</summary>
        public bool HasPendingOutcome => pendingResult != null;

        /// <summary>
        /// まだ公開していない結果。無ければ<see cref="BattleSlotOutcome.None"/>。
        /// 勝敗はここでも決めず、確定済みの<see cref="RoundResult.Winner"/>を
        /// 言い換えるだけです。
        /// </summary>
        public BattleSlotOutcome PendingOutcome =>
            pendingResult != null
                ? BattleSlotOutcomes.FromWinner(pendingResult.Winner)
                : BattleSlotOutcome.None;

        /// <summary>まだ公開していない結果の対象個体ID。無ければ null。</summary>
        public string PendingOutcomeInstanceId =>
            pendingResult != null && pendingResult.PlayerUnit != null
                ? pendingResult.PlayerUnit.InstanceId
                : null;

        /// <summary>直近に受け付けられなかった操作の理由。</summary>
        public BattleError LastError { get; private set; }

        /// <summary>状態が変わったときに発火します。</summary>
        public event Action<BattleUiState> StateChanged;

        /// <summary>プレイヤーの選択が変わったときに発火します。</summary>
        public event Action SelectionChanged;

        /// <summary>ラウンドが解決したときに発火します。ここで初めてCPUの選出が分かります。</summary>
        public event Action<RoundResult> RoundResolved;

        /// <summary>プレイヤーのラウンド勝利数。</summary>
        public int PlayerWins => Session != null ? Session.PlayerWins : 0;

        /// <summary>CPUのラウンド勝利数。</summary>
        public int CpuWins => Session != null ? Session.CpuWins : 0;

        /// <summary>これから解決するラウンド番号。未開始なら0。</summary>
        public int CurrentRound => Session != null ? Session.CurrentRound : 0;

        /// <summary>1マッチの最大ラウンド数。</summary>
        public int MaxRounds => BattleSession.MaxRounds;

        /// <summary>マッチの決着状態。</summary>
        /// <summary>
        /// 敵の残存戦力。中核が作った読み取りをそのまま渡します。
        /// 画面側で敵の選出を推測したり数え直したりはしません。
        /// </summary>
        public EnemyForceReadout DescribeEnemyForce(EnemyForceDisclosure policy)
        {
            return Session != null
                ? Session.DescribeEnemyForce(policy)
                : EnemyForceModel.Build(
                    new BattleUnit[0], BattleSession.MaxRounds, policy);
        }

        public BattleMatchState MatchState =>
            Session != null ? Session.State : BattleMatchState.InProgress;

        /// <summary>プレイヤーが今ラウンドの個体を選んでいるか。</summary>
        public bool HasSelection => !string.IsNullOrEmpty(SelectedPlayerInstanceId);

        /// <summary>今、編成トレイのタップを受け付けるか。</summary>
        public bool CanSelect => State == BattleUiState.Selecting;

        /// <summary>今、DEPLOYを押せるか。</summary>
        public bool CanDeploy => State == BattleUiState.Selecting && HasSelection;

        /// <summary>今、REMATCHを押せるか。</summary>
        public bool CanRematch =>
            State == BattleUiState.MatchFinished || State == BattleUiState.SquadRequired;

        /// <summary>
        /// 指定個体の「公開済み」の結果。
        /// 未戦闘、または演出がまだ終わっていないラウンドなら
        /// <see cref="BattleSlotOutcome.None"/>を返します。
        /// </summary>
        public BattleSlotOutcome GetPlayerOutcome(string instanceId)
        {
            return presentedOutcomes.GetOutcome(instanceId);
        }

        /// <summary>指定個体がこの対戦で使用済みか。未開始なら常に false。</summary>
        public bool IsPlayerUnitUsed(string instanceId)
        {
            return Session != null && Session.IsPlayerUnitUsed(instanceId);
        }

        /// <summary>
        /// マッチを用意して第1ラウンドを開始します。
        /// 編成が揃っていない場合は<see cref="BattleUiState.SquadRequired"/>へ移ります。
        /// </summary>
        public bool Begin()
        {
            Session = null;
            LastResult = null;
            SelectedPlayerInstanceId = null;

            // 前のマッチのバッジを残さないよう、編成を組む前に捨てます。
            // 公開済みと未公開の両方を捨てます（REMATCHもここを通ります）。
            presentedOutcomes.Clear();
            revealedOutcomes.Clear();
            pendingResult = null;
            pendingRevealed = false;

            LastRevealedOutcome = BattleSlotOutcome.None;
            LastRevealedPlayerInstanceId = null;

            bool created = matchSource.TryCreateMatch(
                out BattleSquad playerSquad,
                out BattleSquad cpuSquad,
                out IBattleUnitSelector cpuSelector,
                out BattleError error);

            if (!created || playerSquad == null || cpuSquad == null || cpuSelector == null)
            {
                LastError = created ? BattleError.NullSquad : error;
                SetState(BattleUiState.SquadRequired);

                return false;
            }

            Session = new BattleSession(playerSquad, cpuSquad, cpuSelector);
            LastError = BattleError.None;

            return StartRound();
        }

        /// <summary>
        /// 同じ条件でもう1マッチ行います。勝利数・使用済み・履歴はすべて作り直します。
        /// 決着後、または編成不足の画面からのみ受け付けます。
        /// </summary>
        public bool Rematch()
        {
            if (!CanRematch)
            {
                return false;
            }

            return Begin();
        }

        /// <summary>
        /// 出す個体を選びます。決定前なら何度でも選び直せます。
        /// 使用済み・編成外・選択中以外の状態では受け付けず、状態も変えません。
        /// </summary>
        public bool SelectPlayerUnit(string instanceId)
        {
            if (State != BattleUiState.Selecting || Session == null)
            {
                LastError = BattleError.AlreadySelected;
                return false;
            }

            if (string.IsNullOrEmpty(instanceId))
            {
                LastError = BattleError.EmptyInstanceId;
                return false;
            }

            if (Session.PlayerSquad.Find(instanceId) == null)
            {
                LastError = BattleError.UnitNotInSquad;
                return false;
            }

            if (Session.IsPlayerUnitUsed(instanceId))
            {
                LastError = BattleError.UnitAlreadyUsed;
                return false;
            }

            LastError = BattleError.None;

            if (string.Equals(SelectedPlayerInstanceId, instanceId, StringComparison.Ordinal))
            {
                return false;
            }

            SelectedPlayerInstanceId = instanceId;
            SelectionChanged?.Invoke();

            return true;
        }

        /// <summary>選択を取り消します。</summary>
        public bool ClearSelection()
        {
            if (State != BattleUiState.Selecting || !HasSelection)
            {
                return false;
            }

            SelectedPlayerInstanceId = null;
            SelectionChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// 選択を確定し、その場でラウンドを解決します。
        /// 成功すると<see cref="BattleUiState.Resolving"/>へ移るため、
        /// 二度目以降の呼び出しは受け付けません（連打対策）。
        /// </summary>
        public bool Deploy()
        {
            if (!CanDeploy)
            {
                LastError = BattleError.AlreadySelected;
                return false;
            }

            SelectionResult selection = Session.SelectPlayerUnit(SelectedPlayerInstanceId);

            if (!selection.Success)
            {
                LastError = selection.Error;
                return false;
            }

            if (!Session.TryResolveRound(out RoundResult result, out BattleError error))
            {
                LastError = error;
                return false;
            }

            LastResult = result;
            LastError = BattleError.None;

            // 勝敗は再判定せず、確定した結果をそのまま控えます。
            // ただしここでは「未公開」に留め、トレイへは渡しません。
            // DEPLOY直後にバッジが出ると、演出より先に勝敗が分かってしまいます。
            pendingResult = result;
            pendingRevealed = false;

            SetState(BattleUiState.Resolving);

            RoundResolved?.Invoke(result);

            return true;
        }

        /// <summary>演出の再生が終わったことを伝え、結果バナーの表示へ移ります。</summary>
        public bool CompleteResolve()
        {
            if (State != BattleUiState.Resolving)
            {
                return false;
            }

            SetState(BattleUiState.ShowingResult);

            return true;
        }

        /// <summary>
        /// 指定したプレイヤー個体を今ラウンドに出した場合の ATTRIBUTE LINK（副作用なし）。
        /// 選択前の予告だけに使います。CPU の選出や CPU 側の LINK は返しません。
        /// 選出待ち以外では <see cref="AttributeLinkResult.None"/> です。
        /// </summary>
        public AttributeLinkResult PreviewPlayerLink(string instanceId)
        {
            if (Session == null || State != BattleUiState.Selecting)
            {
                return AttributeLinkResult.None;
            }

            return Session.PreviewPlayerLink(instanceId);
        }

        /// <summary>
        /// 指定したプレイヤー個体を今ラウンドに出した場合のユニークスキルの予告（副作用なし）。
        /// プレイヤー自身の履歴だけから作り、CPU の情報は使いません。選出待ち以外では予告しません。
        /// </summary>
        public UniqueSkillPreview PreviewPlayerSkill(string instanceId)
        {
            if (Session == null || State != BattleUiState.Selecting)
            {
                return UniqueSkillPreview.None;
            }

            return Session.PreviewPlayerSkill(instanceId);
        }

        /// <summary>
        /// 結果バナーを閉じ、次ラウンドまたは最終結果へ移ります。
        /// </summary>
        public bool AdvanceToNextRound()
        {
            if (State != BattleUiState.ShowingResult || Session == null)
            {
                return false;
            }

            if (Session.IsFinished)
            {
                SetState(BattleUiState.MatchFinished);
                return true;
            }

            return StartRound();
        }

        /// <summary>
        /// 対戦演出が終わり、結果を画面へ出す合図です。
        /// ここで初めて敗者が確定表示になります（グレー化の起点）。
        ///
        /// W/L/Dバッジはここでは出しません。バッジは従来どおり
        /// <see cref="PublishPendingOutcome"/>まで待ちます。
        /// 未公開の結果が無ければ何もせず false を返すため、二度呼んでも二重には適用されません。
        /// </summary>
        public bool RevealRoundOutcome()
        {
            if (pendingResult == null || pendingRevealed)
            {
                return false;
            }

            // 勝敗は再判定しません。確定済みの結果をそのまま控えへ移します。
            revealedOutcomes.Record(pendingResult);

            LastRevealedOutcome = BattleSlotOutcomes.FromWinner(pendingResult.Winner);

            LastRevealedPlayerInstanceId = pendingResult.PlayerUnit != null
                ? pendingResult.PlayerUnit.InstanceId
                : null;

            pendingRevealed = true;

            return true;
        }

        /// <summary>
        /// 演出と結果表示が終わったことを伝え、今回の結果を公開します。
        /// ここで初めて、今ラウンドで出した個体へバッジが付きます。
        ///
        /// 未公開の結果が無ければ何もせず false を返すため、
        /// 二度呼んでも二重には公開されません。
        /// </summary>
        public bool PublishPendingOutcome()
        {
            if (pendingResult == null)
            {
                return false;
            }

            // 勝敗は再判定しません。確定済みの結果をそのまま控えへ移します。
            presentedOutcomes.Record(pendingResult);
            pendingResult = null;
            pendingRevealed = false;

            return true;
        }

        /// <summary>
        /// 演出を中断して進行だけを先に進めます。
        /// シーン離脱や OnDisable の場面で、
        /// 状態が Resolving のまま取り残されるのを防ぎます。
        ///
        /// 解決済みラウンドの結果はここで公開します。
        /// 「出したのに結果が分からない個体」を残さないためです。
        /// </summary>
        public bool AbortPresentation()
        {
            bool changed = CompleteResolve();

            // 途中離脱でも「出したのに結果が分からない個体」を残さないため、
            // グレー化とバッジの両方をここで確定させます。
            bool revealed = RevealRoundOutcome();
            bool published = PublishPendingOutcome();

            return AdvanceToNextRound() || changed || revealed || published;
        }

        /// <summary>
        /// ラウンドを開始し、その場でCPUの非公開選出を済ませます。
        /// プレイヤーの選択は引数にも状態にも現れないため、
        /// CPUが相手の手を見て選ぶことは構造的にできません。
        /// </summary>
        private bool StartRound()
        {
            SelectedPlayerInstanceId = null;

            // 出場中の2枠は次ラウンド用に作り直されます。
            // 個体ごとの控え(revealedOutcomes)は残るため、トレイのグレーは続きます。
            LastRevealedOutcome = BattleSlotOutcome.None;
            LastRevealedPlayerInstanceId = null;

            SelectionResult cpu = Session.SelectCpuUnit();

            if (!cpu.Success)
            {
                LastError = cpu.Error;
                SetState(BattleUiState.MatchFinished);

                return false;
            }

            SetState(BattleUiState.Selecting);
            SelectionChanged?.Invoke();

            return true;
        }

        private void SetState(BattleUiState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
