using System.Collections;

using CoreBeasts.Shared.UI;
using CoreBeasts.Progression;
using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の進行役とUnity側の橋渡し。
    ///
    /// 勝敗も使用済み管理も持ちません。判定は<see cref="BattleSession"/>、
    /// 画面の状態は<see cref="BattleFlowCoordinator"/>、演出は<see cref="BattleFxPlayer"/>
    /// がそれぞれ受け持ちます。ここは「状態が変わったら、どのViewへ何を渡すか」だけです。
    ///
    /// CPUの選出内容は、解決済みの<see cref="RoundResult"/>を受け取るまで
    /// どのViewへも渡しません。
    ///
    /// 自軍の選択は循環リング（<see cref="BattleUnitWheelView"/>）が受け持ちます。
    /// リングを回しただけでは何も確定せず、中央個体の上スライドが成立したときにだけ
    /// <see cref="BattleFlowCoordinator.SelectPlayerUnit"/>と
    /// <see cref="BattleFlowCoordinator.Deploy"/>を通します。
    /// 両方が受理されたときだけ、その個体をリングから外します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleScreenController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CoreBeastRoster roster;
        [SerializeField] private AttributePalette palette;
        [SerializeField] private UiTextCatalog text;
        [SerializeField] private BattleTextCatalog battleText;

        [SerializeField]
        [Tooltip("読み込む編成セットの内部ID。UnitSet画面と同じ値にしてください。")]
        private string setId = "1";

        [SerializeField]
        [Tooltip("0以外にすると、CPUの編成と選出を再現できる固定seedになります。")]
        private int cpuRandomSeed;

        [Header("Views")]
        [SerializeField] private BattleCombatantView playerCombatant;
        [SerializeField] private BattleCombatantView cpuCombatant;

        [Tooltip("自軍の未使用個体を出す循環リング。横一列トレイの置き換えです。")]
        [SerializeField] private BattleUnitWheelView playerWheel;

        [Tooltip("使い終わった個体を並べる戦績履歴レーン。")]
        [SerializeField] private BattleHistoryLaneView historyLane;

        [Tooltip("リング中央から戦闘位置へ動かす表示。")]
        [SerializeField] private BattleDeployTransitionView deployTransition;

        [Tooltip("中央ユニットの上に出す発進案内。")]
        [SerializeField] private BattleSlideUpGuideView slideUpGuide;

        [Tooltip("船内背景。FX OFF で動きを止めます。")]
        [SerializeField] private ShipBackdropView shipBackdrop;

        [Tooltip("ENEMY と PLAYER の境目に置くコアゲート。")]
        [SerializeField] private BattleCoreGateView coreGate;

        [Tooltip("ヘッダーの勝利ピップ。")]
        [SerializeField] private BattleScorePipsView scorePips;
        [SerializeField] private EnemySquadStatusView enemyStatus;
        [SerializeField] private BattleScoreView scoreView;
        [SerializeField] private BattleResultView resultView;
        [SerializeField] private BattleFxPlayer fxPlayer;

        [Header("Match cues (Phase 3)")]
        [Tooltip("新しく点灯したピップ1個へ重ねる勝利コア獲得。未設定なら省きます（進行は変わりません）。")]
        [SerializeField] private VictoryCoreView victoryCore;

        [Tooltip("FINAL CORE / CORE BREAK。未設定なら省きます（進行は変わりません）。")]
        [SerializeField] private BattleMatchCueView matchCue;

        [Header("Attribute link (Phase 4)")]
        [Tooltip("ATTRIBUTE LINK の演出。未設定なら省きます（勝敗と進行は変わりません）。")]
        [SerializeField] private BattleAttributeLinkView attributeLinkView;

        [Header("Unique skill (Phase 5)")]
        [Tooltip("ユニークスキル発動の演出。未設定なら省きます（勝敗と進行は変わりません）。")]
        [SerializeField] private BattleSkillCueView skillCueView;

        [Tooltip("右上の歯車から開く設定パネル。HOMEとFXはここへ入れます。")]
        [SerializeField] private BattleSettingsView settingsView;

        [Header("Roots")]
        [SerializeField] private GameObject battleRoot;
        [SerializeField] private GameObject squadRequiredRoot;

        [Header("Buttons")]
        [SerializeField] private Button rematchButton;

        [Header("Static labels")]
        [SerializeField] private TMP_Text screenTitleLabel;
        [SerializeField] private TMP_Text rematchLabel;
        [SerializeField] private TMP_Text squadRequiredTitleLabel;
        [SerializeField] private TMP_Text squadRequiredHintLabel;

        private BattleFlowCoordinator coordinator;
        private BattleMatchSource matchSource;
        private Coroutine roundRoutine;

        /// <summary>
        /// DEPLOYから、演出・結果公開・フェード・アーカイブ・次ラウンドへの切替まで
        /// すべて終わるあいだ true です。入力とFX切替はこの間ずっと閉じます。
        /// コルーチンが1フレームも待たずに終わった場合でも、古い参照を残さないために
        /// <see cref="roundRoutine"/>とは別に持ちます。
        /// </summary>
        private bool roundActive;

        private bool isReady;

        /// <summary>未使用個体の循環リング。選出は確定させません。</summary>
        private readonly BattleUnitRingModel ring = new BattleUnitRingModel();

        /// <summary>使い終わった個体の戦績履歴。</summary>
        private readonly BattleHistoryModel history = new BattleHistoryModel();

        /// <summary>リング中央から戦闘位置へ移動中か（表示だけの段階）。</summary>
        private bool isDeploying;

        /// <summary>戦闘表示を履歴レーンへ片付け中か（表示だけの段階）。</summary>
        private bool isArchiving;

        /// <summary>移動が終わるまで、戦闘側の正式表示を伏せておく個体。</summary>
        private BattleUnitCard pendingCombatant;

        /// <summary>
        /// この試合で FINAL CORE を出したか。1試合につき最大1回にするための記録で、
        /// 再戦（<see cref="OnRematchClicked"/>）で false へ戻します。
        /// </summary>
        private bool finalCoreShown;

        /// <summary>同じ試合結果を画面更新のたびに二重加算しないための印。</summary>
        private bool rewardGrantedForMatch;

        /// <summary>結果画面に出す、今回の試合で確定した報酬。</summary>
        private int grantedMatchReward;

        /// <summary>
        /// 表示スコアをラウンド前の値に留めているか。
        ///
        /// 論理スコア（<see cref="BattleSession"/>）は DEPLOY の時点で確定しますが、
        /// 画面のスコアピップとスコア文字は、衝突・勝敗公開・勝利コアの収束が終わるまで
        /// ラウンド前の値のまま見せます。先に勝敗が分からないようにするためです。
        /// </summary>
        private bool holdScore;

        /// <summary>このラウンドで、留めていた表示スコアを実際に更新したか。</summary>
        private bool scoreRevealedThisRound;

        /// <summary>
        /// 表示ラウンド（RoundLabel）を処理中のラウンド番号に留めているか。
        ///
        /// 論理上のラウンド番号（<see cref="BattleSession.CurrentRound"/>）は DEPLOY で進みますが、
        /// 画面の RoundLabel は、演出・結果表示・片付け・次ラウンドの準備が終わり、
        /// 入力を開く直前まで、処理中のラウンド番号のまま見せます。
        /// </summary>
        private bool holdRound;

        private int heldRound;

        /// <summary>画面の RoundLabel が示しているラウンド番号。</summary>
        public int DisplayedRound => holdRound ? heldRound : LogicalDisplayRound();

        /// <summary>表示ラウンドを処理中のラウンドに留めている最中か。</summary>
        public bool IsRoundDisplayHeld => holdRound;

        private int heldPlayerWins;
        private int heldCpuWins;

        /// <summary>画面のスコアピップとスコア文字が示している PLAYER の勝利数。</summary>
        public int DisplayedPlayerWins =>
            holdScore ? heldPlayerWins : coordinator != null ? coordinator.PlayerWins : 0;

        /// <summary>画面のスコアピップとスコア文字が示している CPU の勝利数。</summary>
        public int DisplayedCpuWins =>
            holdScore ? heldCpuWins : coordinator != null ? coordinator.CpuWins : 0;

        /// <summary>表示スコアをラウンド前の値に留めている最中か。</summary>
        public bool IsScoreDisplayHeld => holdScore;

        /// <summary>直近のラウンドで使った試合演出の設計。未再生なら null です。</summary>
        public BattleMatchPresentationPlan LastMatchPlan { get; private set; }

        /// <summary>この試合で FINAL CORE を出したか。</summary>
        public bool FinalCoreShown => finalCoreShown;

        /// <summary>勝利コア獲得の表示。</summary>
        public VictoryCoreView VictoryCore => victoryCore;

        /// <summary>FINAL CORE / CORE BREAK の表示。</summary>
        public BattleMatchCueView MatchCue => matchCue;

        /// <summary>ATTRIBUTE LINK の演出。</summary>
        public BattleAttributeLinkView AttributeLinkView => attributeLinkView;

        /// <summary>ユニークスキル発動の演出。</summary>
        public BattleSkillCueView SkillCueView => skillCueView;

        /// <summary>進行役。テストから状態を確かめるために公開しています。</summary>
        public BattleFlowCoordinator Coordinator => coordinator;

        /// <summary>演出を再生するか。既定はONです。</summary>
        public bool FxEnabled => fxPlayer == null || fxPlayer.FxEnabled;

        /// <summary>設定パネルを出しているか。出ているあいだ、バトル操作は通しません。</summary>
        public bool IsSettingsOpen => settingsView != null && settingsView.IsOpen;

        /// <summary>今、演出や結果表示を出している最中か。</summary>
        public bool IsPresenting =>
            roundActive ||
            (coordinator != null && coordinator.State == BattleUiState.Resolving);

        /// <summary>
        /// 1ラウンドの提示（演出からアーカイブまで）を実行中か。
        /// true のあいだはDEPLOY・FX切替・REMATCHを受け付けません。
        /// </summary>
        public bool IsRoundInProgress => roundActive;

        /// <summary>リング表示だけが持つ進行段階。</summary>
        public BattleWheelPhase WheelPhase =>
            coordinator == null
                ? BattleWheelPhase.Idle
                : BattleWheelPhases.Resolve(coordinator.State, isDeploying, isArchiving);

        /// <summary>未使用個体の循環リング（確認・テスト用）。</summary>
        public BattleUnitRingModel Ring => ring;

        /// <summary>戦績履歴（確認・テスト用）。</summary>
        public BattleHistoryModel History => history;

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            ApplyStaticLabels();

            // 先に編成を用意します。Viewの初期化が編成を参照するためです。
            coordinator = new BattleFlowCoordinator(CreateMatchSource());

            BindViews();

            coordinator.StateChanged += HandleStateChanged;
            coordinator.SelectionChanged += HandleSelectionChanged;
            coordinator.RoundResolved += HandleRoundResolved;

            if (settingsView != null)
            {
                settingsView.FxToggleRequested += OnFxToggleClicked;
                settingsView.HomeRequested += HandleHomeRequested;
                settingsView.OpenStateChanged += HandleSettingsOpenChanged;
            }

            AddListeners();

            isReady = true;

            coordinator.Begin();

            RefreshAll();
        }

        private void OnDisable()
        {
            // 開いたままの設定パネルを残しません。
            if (settingsView != null)
            {
                settingsView.Close();
            }

            // 演出の途中でも、進行と見た目を矛盾なく止めます。
            AbortRoundPresentation();
        }

        private void OnDestroy()
        {
            RemoveListeners();

            if (coordinator != null)
            {
                coordinator.StateChanged -= HandleStateChanged;
                coordinator.SelectionChanged -= HandleSelectionChanged;
                coordinator.RoundResolved -= HandleRoundResolved;
            }

            if (settingsView != null)
            {
                settingsView.FxToggleRequested -= OnFxToggleClicked;
                settingsView.HomeRequested -= HandleHomeRequested;
                settingsView.OpenStateChanged -= HandleSettingsOpenChanged;
            }
        }

        // ---------------- 入力 ----------------

        /// <summary>
        /// リング中央の上スライドが成立したときに呼ばれます。
        ///
        /// ここで初めて<see cref="BattleSession"/>へ選出を渡します。
        /// 回しただけでは通らないので、スクロール中に選び直せなくなることがありません。
        ///
        /// リングから外すのは「選出もDeployも受理された後」だけです。
        /// どちらかが断られたら、リングも履歴も変えません。
        /// </summary>
        public void OnWheelDeployRequested(string instanceId)
        {
            if (!isReady ||
                !BattleWheelPhases.AllowsInput(WheelPhase, IsSettingsOpen) ||
                roundActive ||
                string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            // 1. UI上の中央個体を、既存の選択経路へ渡します。
            if (!coordinator.SelectPlayerUnit(instanceId))
            {
                return;
            }

            // 2. 受理されたので、既存のDeploy処理を始めます。
            //    論理スコアはここで確定しますが、画面のスコアはラウンド前の値に留めます。
            //    Deploy の中で画面が作り直されるため、呼ぶ前に留めておきます。
            HoldScoreDisplay();

            if (!coordinator.Deploy())
            {
                ReleaseScoreHold();
                ReleaseRoundHold();
                return;
            }

            // 3. ここまで通ったときだけ、リングから外します。
            ring.Remove(instanceId);

            if (!isActiveAndEnabled)
            {
                // 再生できない状況では、演出を挟まず進行だけを進めます。
                holdScore = false;
                holdRound = false;
                coordinator.AbortPresentation();
                ArchiveRevealedRound();
                RefreshAll();

                return;
            }

            // 開始前に閉じます。StartCoroutine は最初の yield まで同期で進むためです。
            roundActive = true;

            Coroutine started = StartCoroutine(RunDeployAndRound(instanceId));

            // 1フレームも待たずに終わっていたら、終わったハンドルを残しません。
            roundRoutine = roundActive ? started : null;
        }

        /// <summary>FX ON / OFF を切り替えます。バトル画面の中だけで保持します。</summary>
        public void OnFxToggleClicked()
        {
            if (!isReady || fxPlayer == null || roundActive)
            {
                return;
            }

            fxPlayer.FxEnabled = !fxPlayer.FxEnabled;
            fxPlayer.ResetVisuals();

            ApplyFxLabel();
        }

        /// <summary>
        /// 設定パネルのHOME。遷移そのものは<see cref="CoreBeasts.Units.SceneLoadButton"/>が
        /// 行うため、ここでは中途半端な表示を残さないための片付けだけを行います。
        /// </summary>
        private void HandleHomeRequested()
        {
            if (settingsView != null)
            {
                settingsView.Close();
            }

            AbortRoundPresentation();
        }

        /// <summary>
        /// リング中央が変わったときの通知。プレビューを作り直すだけで、
        /// <see cref="BattleSession"/>へは何も渡しません。
        /// </summary>
        private void OnWheelFocusChanged(string instanceId)
        {
            if (!isReady)
            {
                return;
            }

            RefreshSelection();
        }

        /// <summary>設定パネルの開閉に合わせて、バトル側の操作可否を作り直します。</summary>
        private void HandleSettingsOpenChanged()
        {
            if (!isReady)
            {
                return;
            }

            RefreshButtons();
        }

        /// <summary>REMATCH。勝利数・使用済み・履歴をすべて作り直します。</summary>
        public void OnRematchClicked()
        {
            if (!isReady ||
                !BattleInputGate.AllowsRematch(
                    coordinator.CanRematch, IsSettingsOpen, roundActive))
            {
                return;
            }

            if (!coordinator.Rematch())
            {
                return;
            }

            if (fxPlayer != null)
            {
                fxPlayer.ResetVisuals();
            }

            ResetMatchCues();

            // 新しい試合では FINAL CORE をもう一度出せます。
            finalCoreShown = false;
            rewardGrantedForMatch = false;
            grantedMatchReward = 0;
            holdScore = false;
            holdRound = false;
            LastMatchPlan = null;

            if (matchCue != null)
            {
                matchCue.ResetCounters();
            }

            if (deployTransition != null)
            {
                deployTransition.Cancel();
            }

            isDeploying = false;
            isArchiving = false;
            pendingCombatant = null;

            ClearDecisionHighlight();

            RebuildWheel();
            RefreshAll();
        }

        // ---------------- 進行 ----------------

        /// <summary>
        /// 解決済みラウンドの提示。
        /// 演出 → 結果バナー → 勝敗バッジの公開 → 次ラウンドの順に進めます。
        /// FX OFF でも同じ順序で、待ちだけが短くなります。
        ///
        /// 今回のバッジは、演出も結果表示も終わった後でしか公開しません。
        /// DEPLOY直後に出すと、演出より先に勝敗が分かってしまいます。
        /// </summary>
        private IEnumerator RunDeployAndRound(string instanceId)
        {
            // --- Deploying: 入力を止め、同じ個体が動いたように見せます ---
            isDeploying = true;
            RefreshAll();

            // ATTRIBUTE LINK（Phase 4）: 既存の移動と同時に始め、待ちません。
            // ラウンド全体の長さは変わらず、LINK のための yield もありません。
            PlayAttributeLink();

            // ユニークスキル（Phase 5）: 同じく移動と同時に始め、待ちません。
            PlaySkillCue();

            if (deployTransition != null && playerCombatant != null && playerWheel != null)
            {
                yield return deployTransition.PlayRoutine(
                    new BattleRingSlot(instanceId, SquadNumberOf(instanceId)),
                    FindPlayerCard(instanceId),
                    playerWheel.transform as RectTransform,
                    playerCombatant.transform as RectTransform,
                    palette,
                    text,
                    FxEnabled);
            }

            isDeploying = false;

            // 到着してから正式表示を出します。二重表示のフレームを作りません。
            if (playerCombatant != null && pendingCombatant != null)
            {
                playerCombatant.Show(pendingCombatant);

                if (coordinator.LastResult != null)
                {
                    playerCombatant.ShowLink(coordinator.LastResult.PlayerLink);
                    playerCombatant.ShowSkill(coordinator.LastResult.PlayerSkill);
                }
            }

            pendingCombatant = null;

            RefreshAll();

            yield return RunRoundPresentation();
        }

        /// <summary>
        /// 解決済みラウンドの提示。
        /// 演出 → 結果バナー → 勝敗バッジの公開 → 履歴へ片付け → 次ラウンドの順に進めます。
        /// FX OFF でも同じ順序で、待ちだけが短くなります。
        /// </summary>
        private IEnumerator RunRoundPresentation()
        {
            RoundResult result = coordinator.LastResult;
            RoundWinner winner = result != null ? result.Winner : RoundWinner.Draw;

            if (fxPlayer != null)
            {
                // 決着エフェクトの種類は、確定済みの決着理由と勝因の属性から決まります。
                // 勝敗はここでは決めません。
                RoundDecision decision = result != null ? result.Decision : RoundDecision.Draw;

                yield return fxPlayer.PlayClashRoutine(
                    winner,
                    decision,
                    DecidingAttributeLookup.Of(result),
                    ResolveFlashColor(winner));
            }

            coordinator.CompleteResolve();

            // 勝敗を画面へ出す合図。ここで初めて敗者がグレーになります。
            coordinator.RevealRoundOutcome();

            // 何で決まったのかが分かるよう、使われた部分だけを一時的に強調します。
            // 勝敗はここでは決めません。中核が返した理由を写すだけです。
            ApplyDecisionHighlight(result);

            RefreshAll();

            // --- Phase 3: 勝利コア獲得と CORE BREAK ---
            // 勝敗・スコアは確定済みの値を読むだけです。
            BattleMatchPresentationPlan matchPlan = CreateMatchPlan(result);

            LastMatchPlan = matchPlan;

            // 勝利コアが対象ピップへ収束した瞬間に、新しいピップとスコア文字を同じフレームで出します。
            if (matchPlan.PlaysVictoryCore && victoryCore != null)
            {
                yield return victoryCore.PlayRoutine(
                    matchPlan.VictorySide,
                    matchPlan.VictoryPipIndex,
                    matchPlan.VictoryCoreDuration,
                    ReleaseScoreHold);
            }

            // 引き分け・FX OFF・勝利コアが中断された場合は、勝敗公開の後のここで追いつかせます。
            ReleaseScoreHold();

            // 新しいピップとスコア文字だけが出たフレームを1回描いてから、CORE BREAK・結果バナーへ進みます。
            // 同じフレームで始めると、更新とバナーが画面上は同時に見えてしまうためです。
            if (scoreRevealedThisRound)
            {
                yield return null;
            }

            // 試合の勝敗が確定したラウンド（4勝目・早期決着・最終ラウンドの引き分けによる決着）:
            // 衝突・Impact・勝利コア獲得・スコア公開の後に CORE BREAK。最終結果はこの後です。
            if (matchPlan.ShowsCoreBreak && matchCue != null)
            {
                // 最終ラウンドの引き分けで決着したときは、勝利コアもスコア更新も無いため、
                // 結果公開と同じフレームで始まってしまいます。公開のフレームを1回描いてから始めます。
                if (!scoreRevealedThisRound)
                {
                    yield return null;
                }

                yield return matchCue.PlayRoutine(
                    MatchCueKind.CoreBreak, matchPlan.PlaysCueGraphics, matchPlan.CoreBreakCueDuration);
            }

            ShowRoundBanner();

            if (fxPlayer != null && resultView != null)
            {
                yield return fxPlayer.ShowBannerRoutine(resultView.BannerGroup);
            }

            if (resultView != null)
            {
                resultView.HideRound();
            }

            if (fxPlayer != null)
            {
                fxPlayer.ResetVisuals();
            }

            // 演出と結果表示が終わった、ここが公開の合図です。
            // 入力はまだ閉じたままです。フェードとアーカイブが終わるまで開けません。
            coordinator.PublishPendingOutcome();

            // --- Archiving: 戦闘表示をその場で薄くしてから履歴へ控えます ---
            isArchiving = true;
            RefreshAll();

            // 履歴の位置へ動かしません。いま居る場所のまま消します。
            yield return FadeOutCombatantsRoutine();

            // 消え終わってから、履歴ノードを別途点灯させます。
            ArchiveRevealedRound();
            RefreshAll();

            isArchiving = false;

            // 強調は必ず通常へ戻します。FX OFF でも同じ場所を通ります。
            ClearDecisionHighlight();

            // 決着なら、ここで最終結果（PLAYER WIN / CPU WIN）へ移ります。CORE BREAK の後です。
            coordinator.AdvanceToNextRound();

            // --- Phase 3: 初めて 3対3 になったら、次ラウンドの前に FINAL CORE ---
            if (matchPlan.ShowsFinalCore)
            {
                // 途中で打ち切られても、同じ試合で2回目は出しません。
                finalCoreShown = true;

                if (matchCue != null)
                {
                    yield return matchCue.PlayRoutine(
                        MatchCueKind.FinalCore, matchPlan.PlaysCueGraphics, matchPlan.FinalCoreCueDuration);
                }
            }

            // 次ラウンドの準備まで終わった。入力を開く直前に RoundLabel を進めます
            // （決着なら最後に戦ったラウンドのままです）。
            ReleaseRoundHold();

            // すべて終わった、ここで初めて入力を開けます。
            FinishRoundRoutine();
        }

        /// <summary>
        /// 解決済みラウンドから試合演出を決めます。勝利数は確定済みの値を読むだけです。
        /// </summary>
        private BattleMatchPresentationPlan CreateMatchPlan(RoundResult result)
        {
            RoundWinner winner = result != null ? result.Winner : RoundWinner.Draw;

            // 試合の状態は BattleSession が判定した値を読むだけです（早期決着もここに含まれます）。
            return BattleMatchPresentationPlan.ForRound(
                winner,
                coordinator.PlayerWins,
                coordinator.CpuWins,
                finalCoreShown,
                coordinator.MatchState,
                FxEnabled);
        }

        /// <summary>解決したラウンドの LINK 演出を、移動と並行して始めます。CPU はこの時点で公開済みです。</summary>
        private void PlayAttributeLink()
        {
            RoundResult result = coordinator.LastResult;

            if (attributeLinkView == null || result == null)
            {
                return;
            }

            attributeLinkView.Play(
                BattleAttributeLinkPresentationPlan.Create(result.PlayerLink, result.CpuLink, FxEnabled));
        }

        /// <summary>
        /// 解決したラウンドのユニークスキル発動の演出を、移動と並行して始めます。
        /// 双方の選出は確定済みで、CPU の個体・スキルはこの時点で初めて画面へ出ます。
        /// </summary>
        private void PlaySkillCue()
        {
            RoundResult result = coordinator.LastResult;

            if (skillCueView == null || result == null || battleText == null)
            {
                return;
            }

            skillCueView.Play(
                BattleSkillPresentationPlan.Create(result.PlayerSkill, result.CpuSkill, FxEnabled),
                SkillCueText(result.PlayerSkill, FindPlayerCard(result.PlayerUnit.InstanceId)),
                SkillCueText(result.CpuSkill, FindCpuCard(result.CpuUnit.InstanceId)));
        }

        /// <summary>演出の文字。スキル名は定義の表示名で、ルールには使いません。</summary>
        private string SkillCueText(UniqueSkillActivation skill, BattleUnitCard card)
        {
            if (!skill.Activated)
            {
                return string.Empty;
            }

            string skillName = card != null && card.Definition != null ? card.Definition.SkillName : string.Empty;
            int value = skill.SelfBonus > 0 ? skill.SelfBonus : skill.OpponentPenalty;

            return battleText.FormatSkillCue(skillName, skill.Kind, value);
        }

        /// <summary>勝利コア・FINAL CORE・CORE BREAK・LINK・スキルの表示を消し、再生中のルーチンを止めます。</summary>
        private void ResetMatchCues()
        {
            if (attributeLinkView != null)
            {
                attributeLinkView.ResetVisuals();
            }

            if (skillCueView != null)
            {
                skillCueView.ResetVisuals();
            }

            if (victoryCore != null)
            {
                victoryCore.ResetVisuals();
            }

            if (matchCue != null)
            {
                matchCue.ResetVisuals();
            }
        }

        /// <summary>
        /// ラウンド提示の正常終了。<see cref="roundRoutine"/>を null へ戻すのはここと
        /// <see cref="StopRoundRoutine"/>だけです。開けた後の状態でボタンを作り直します。
        /// </summary>
        private void FinishRoundRoutine()
        {
            roundRoutine = null;
            roundActive = false;

            RefreshAll();
        }

        /// <summary>
        /// 演出を途中で打ち切ります（OnDisable・HOME）。
        ///
        /// Coroutine を止め、立ち絵の位置・大きさ・明暗・振動、フラッシュ、
        /// 戦闘表示のフェード、判定強調、結果バナー、移動ゴーストをすべて初期状態へ戻します。
        /// 進行は<see cref="BattleFlowCoordinator.AbortPresentation"/>で矛盾なく先へ進め、
        /// 途中だったラウンドは履歴にも残します（二重には足されません）。
        /// </summary>
        private void AbortRoundPresentation()
        {
            bool wasInProgress = roundActive;

            StopRoundRoutine();

            // 途中離脱で一時ゴーストを残しません。
            if (deployTransition != null)
            {
                deployTransition.Cancel();
            }

            isDeploying = false;
            isArchiving = false;
            pendingCombatant = null;

            ApplyCombatantFade(0f);
            ClearDecisionHighlight();

            if (fxPlayer != null)
            {
                fxPlayer.ResetVisuals();
            }

            ResetMatchCues();

            // 中断では、表示スコアと表示ラウンドを論理状態へ戻します（留めたままにしません）。
            holdScore = false;
            holdRound = false;

            bool advanced = coordinator != null && coordinator.AbortPresentation();

            if (wasInProgress)
            {
                ArchiveRevealedRound();
            }

            if (advanced || wasInProgress)
            {
                RefreshAll();
            }
        }

        /// <summary>
        /// 判定に使われた部分を一時的に強調します。
        ///
        /// 勝敗は<see cref="BattleRules"/>が決めた結果をそのまま写すだけで、
        /// ここで理由を計算し直すことはありません。
        /// </summary>
        private void ApplyDecisionHighlight(RoundResult result)
        {
            if (result == null)
            {
                return;
            }

            RoundDecision decision = result.Decision;

            Color accent = ResolveFlashColor(result.Winner);

            if (playerCombatant != null)
            {
                playerCombatant.HighlightDecision(decision, accent);
            }

            if (cpuCombatant != null)
            {
                cpuCombatant.HighlightDecision(decision, accent);
            }
        }

        /// <summary>強調を解きます。どの経路からでも必ず通常へ戻します。</summary>
        private void ClearDecisionHighlight()
        {
            if (playerCombatant != null)
            {
                playerCombatant.ClearDecisionHighlight();
            }

            if (cpuCombatant != null)
            {
                cpuCombatant.ClearDecisionHighlight();
            }
        }

        /// <summary>
        /// 戦闘表示を現在位置のまま薄くします。
        ///
        /// Transform は一切動かしません。使用済み個体が履歴レーンへ
        /// 飛んでいくように見える演出を作らないためです。
        /// FX OFF では待たずに最終状態（消えた状態）にします。
        /// </summary>
        private IEnumerator FadeOutCombatantsRoutine()
        {
            float duration = FxEnabled ? 0.22f : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float amount = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

                ApplyCombatantFade(amount);

                yield return null;
            }

            ApplyCombatantFade(1f);
        }

        private void ApplyCombatantFade(float amount)
        {
            if (playerCombatant != null)
            {
                playerCombatant.SetFadeOut(amount);
            }

            if (cpuCombatant != null)
            {
                cpuCombatant.SetFadeOut(amount);
            }
        }

        /// <summary>
        /// 公開済みの結果を履歴レーンへ移します。
        /// 結果が公開される前は<see cref="BattleSlotOutcome.None"/>のため、何も足しません。
        /// </summary>
        private void ArchiveRevealedRound()
        {
            RoundResult result = coordinator != null ? coordinator.LastResult : null;

            if (result == null || result.PlayerUnit == null)
            {
                return;
            }

            string instanceId = result.PlayerUnit.InstanceId;

            history.Append(
                instanceId,
                SquadNumberOf(instanceId),
                coordinator.Outcomes.GetOutcome(instanceId),
                result.PlayerLink.IsActive);
        }

        /// <summary>
        /// 元の編成番号。リングから外れた後も引けるよう、編成の並びから数えます。
        /// </summary>
        private int SquadNumberOf(string instanceId)
        {
            int number = ring.SquadNumberOf(instanceId);

            if (number > 0)
            {
                return number;
            }

            BattleSideRoster side = matchSource != null ? matchSource.PlayerSide : null;

            if (side != null)
            {
                for (int i = 0; i < side.Cards.Count; i++)
                {
                    if (side.Cards[i] != null &&
                        side.Cards[i].InstanceId == instanceId)
                    {
                        return i + 1;
                    }
                }
            }

            return 0;
        }

        private void StopRoundRoutine()
        {
            if (roundRoutine != null)
            {
                StopCoroutine(roundRoutine);
                roundRoutine = null;
            }

            roundActive = false;

            if (resultView != null)
            {
                resultView.HideRound();
            }
        }

        private void HandleStateChanged(BattleUiState state)
        {
            if (!isReady)
            {
                return;
            }

            RefreshAll();
        }

        private void HandleSelectionChanged()
        {
            if (!isReady)
            {
                return;
            }

            RefreshSelection();
        }

        /// <summary>
        /// ラウンドが解決した瞬間に、初めてCPUの選出を公開します。
        /// これより前に、CPU側のViewへ個体を渡す経路はありません。
        /// </summary>
        private void HandleRoundResolved(RoundResult result)
        {
            if (!isReady || result == null)
            {
                return;
            }

            BattleUnitCard playerCard = FindPlayerCard(result.PlayerUnit.InstanceId);

            if (isDeploying)
            {
                // 移動表示が着くまでは伏せます。ゴーストと二重に出さないためです。
                pendingCombatant = playerCard;
            }
            else if (playerCombatant != null)
            {
                playerCombatant.Show(playerCard);
                playerCombatant.ShowLink(result.PlayerLink);
                playerCombatant.ShowSkill(result.PlayerSkill);
            }

            // CPU の個体と LINK は、解決したこの時点で初めて公開します（DEPLOY 前には出しません）。
            if (cpuCombatant != null)
            {
                cpuCombatant.Show(FindCpuCard(result.CpuUnit.InstanceId));
                cpuCombatant.ShowLink(result.CpuLink);
                cpuCombatant.ShowSkill(result.CpuSkill);
            }
        }

        // ---------------- 表示更新 ----------------

        private void RefreshAll()
        {
            if (!isReady)
            {
                return;
            }

            BattleUiState state = coordinator.State;
            bool squadReady = state != BattleUiState.SquadRequired;

            if (shipBackdrop != null)
            {
                // 背景の低速アニメーションも FX OFF で止めます。
                // 静止しても絵として成立する作りにしてあります。
                shipBackdrop.SetFxEnabled(FxEnabled);
            }

            SetActive(battleRoot, squadReady);
            SetActive(squadRequiredRoot, !squadReady);

            if (!squadReady)
            {
                return;
            }

            RefreshDisplayedScore();
            RefreshDisplayedRound();

            if (coreGate != null)
            {
                // 結果を画面へ出したあいだだけ、中央を強く灯します。
                coreGate.SetOutcome(
                    BattleDefeatPresentation.ShowsResolvedRound(state)
                        ? coordinator.LastRevealedOutcome
                        : BattleSlotOutcome.None);
            }

            if (enemyStatus != null)
            {
                enemyStatus.Refresh(
                    coordinator.Session != null ? coordinator.Session.CompletedRounds : 0,
                    state == BattleUiState.Selecting);
            }

            if (state == BattleUiState.Selecting)
            {
                if (cpuCombatant != null)
                {
                    cpuCombatant.ShowHidden();
                }

            }

            if (state == BattleUiState.MatchFinished)
            {
                // 報酬の確定はViewの有無に依存させません。
                GrantMatchRewardOnce();
            }

            if (resultView != null)
            {
                if (state == BattleUiState.MatchFinished)
                {
                    resultView.ShowFinal(
                        coordinator.MatchState,
                        coordinator.PlayerWins,
                        coordinator.CpuWins);
                    resultView.SetMatchReward(grantedMatchReward);
                }
                else
                {
                    resultView.HideFinal();
                }
            }

            RefreshSelection();
            ApplyCombatantDefeat();
            RefreshButtons();
        }

        private void GrantMatchRewardOnce()
        {
            if (rewardGrantedForMatch || coordinator == null || roster == null)
            {
                return;
            }

            // 通常起動（Boot→Home）ではHomeが永続Repositoryへ切り替えます。
            // テストが明示的に使うInMemory保存では端末プロフィールを変更しません。
            if (!SquadRepositoryProvider.UsesPersistentStorage)
            {
                return;
            }

            BattleRewardOutcome outcome;

            switch (coordinator.MatchState)
            {
                case BattleMatchState.PlayerWin:
                    outcome = BattleRewardOutcome.Win;
                    break;
                case BattleMatchState.Draw:
                    outcome = BattleRewardOutcome.Draw;
                    break;
                case BattleMatchState.CpuWin:
                    outcome = BattleRewardOutcome.Loss;
                    break;
                default:
                    return;
            }

            int reward = GameEconomy.RewardFor(outcome);
            PlayerProfile profile = PlayerProfileProvider.Get(roster);

            profile.RecordBattle(outcome, reward);
            PlayerProfileProvider.Save();
            GameFlowState.AddPendingReward(reward);

            grantedMatchReward = reward;
            rewardGrantedForMatch = true;
        }

        /// <summary>
        /// スコア文字とスコアピップを、表示スコアで同じフレームに作り直します。
        /// RoundLabel には触れません。引き分けではどちらも増えません。
        /// </summary>
        private void RefreshDisplayedScore()
        {
            if (scoreView != null)
            {
                scoreView.RefreshScore(DisplayedPlayerWins, DisplayedCpuWins);
            }

            if (scorePips != null)
            {
                scorePips.Refresh(DisplayedPlayerWins, DisplayedCpuWins);
            }
        }

        /// <summary>RoundLabel だけを、表示ラウンドで作り直します。スコアには触れません。</summary>
        private void RefreshDisplayedRound()
        {
            if (scoreView != null)
            {
                scoreView.RefreshRound(DisplayedRound, coordinator.MaxRounds);
            }
        }

        /// <summary>
        /// 留めていないときに見せるラウンド番号。試合中は次に戦うラウンド、
        /// 決着後は最後に戦ったラウンドです（存在しない次ラウンドの番号を出しません）。
        /// </summary>
        private int LogicalDisplayRound()
        {
            if (coordinator == null)
            {
                return 1;
            }

            BattleSession session = coordinator.Session;

            if (session != null && session.IsFinished)
            {
                return session.CompletedRounds < 1 ? 1 : session.CompletedRounds;
            }

            return coordinator.CurrentRound;
        }

        /// <summary>画面のスコアとラウンドを、いまの論理状態（ラウンド前）に留めます。</summary>
        private void HoldScoreDisplay()
        {
            heldPlayerWins = coordinator.PlayerWins;
            heldCpuWins = coordinator.CpuWins;
            holdScore = true;
            scoreRevealedThisRound = false;

            heldRound = LogicalDisplayRound();
            holdRound = true;
        }

        /// <summary>
        /// 表示ラウンドを論理状態へ追いつかせます。次ラウンドの準備が終わり、
        /// 入力を開く直前にだけ呼びます。何度呼んでも安全です。
        /// </summary>
        private void ReleaseRoundHold()
        {
            if (!holdRound)
            {
                return;
            }

            holdRound = false;

            if (isReady)
            {
                RefreshDisplayedRound();
            }
        }

        /// <summary>
        /// 画面のスコアを論理スコアへ追いつかせます。スコア文字とピップは同じフレームで変わります。
        /// 何度呼んでも安全です（留めていなければ何もしません）。
        /// </summary>
        private void ReleaseScoreHold()
        {
            if (!holdScore)
            {
                return;
            }

            holdScore = false;

            if (isReady)
            {
                scoreRevealedThisRound =
                    heldPlayerWins != coordinator.PlayerWins || heldCpuWins != coordinator.CpuWins;

                RefreshDisplayedScore();
            }
        }

        /// <summary>
        /// 出場中の2枠へ敗北表示を反映します。
        ///
        /// 適用するのは、勝敗を画面へ出した後（結果表示中と決着後）だけです。
        /// 選択中・解決中はどちらもグレーにしません。
        /// 引き分けでは<see cref="BattleSlotOutcome.Draw"/>になるため、両者とも変わりません。
        /// </summary>
        private void ApplyCombatantDefeat()
        {
            BattleUiState state = coordinator.State;
            BattleSlotOutcome revealed = coordinator.LastRevealedOutcome;

            if (playerCombatant != null)
            {
                playerCombatant.SetDefeated(
                    BattleDefeatPresentation.GreysPlayer(state, revealed));
            }

            if (cpuCombatant != null)
            {
                cpuCombatant.SetDefeated(
                    BattleDefeatPresentation.GreysCpu(state, revealed));
            }
        }

        private void RefreshSelection()
        {
            if (!isReady)
            {
                return;
            }

            // リングは未使用個体だけを持ちます。使用済みは履歴レーンへ移ります。
            if (playerWheel != null)
            {
                playerWheel.SetInteractable(
                    BattleWheelPhases.AllowsInput(WheelPhase, IsSettingsOpen));

                playerWheel.Refresh();
            }

            if (historyLane != null)
            {
                historyLane.Show(
                    history,
                    matchSource != null ? matchSource.PlayerSide : null,
                    palette,
                    text);
            }

            if (slideUpGuide != null)
            {
                bool canSlide =
                    WheelPhase == BattleWheelPhase.Selecting &&
                    !IsSettingsOpen &&
                    !ring.IsEmpty;

                // 通常時に出すのは控えめなシェブロンだけです。
                slideUpGuide.SetHintVisible(canSlide);
                slideUpGuide.SetFxEnabled(FxEnabled);

                // 発進レールは、上方向へロックされているあいだだけ出します。
                bool sliding = canSlide && playerWheel != null && playerWheel.IsSlidingUp;

                if (sliding)
                {
                    slideUpGuide.ShowRail(playerWheel.SlideProgress);
                }
                else
                {
                    slideUpGuide.HideRail();
                }
            }

            // 中央の個体を、上部の固定情報欄（戦闘表示）へプレビューします。
            // ここでは選出を確定しません。確定は上スライドの成立時だけです。
            if (playerCombatant != null &&
                coordinator.State == BattleUiState.Selecting &&
                !isDeploying)
            {
                BattleUnitCard focused = FindPlayerCard(ring.FocusedInstanceId);

                if (focused != null)
                {
                    playerCombatant.Show(focused);

                    // 選択前の予告。プレイヤー自身の直前の個体だけから、副作用なしで求めます。
                    playerCombatant.ShowLink(coordinator.PreviewPlayerLink(focused.InstanceId));
                    playerCombatant.ShowSkillPreview(coordinator.PreviewPlayerSkill(focused.InstanceId));
                }
                else
                {
                    playerCombatant.ShowEmpty();
                }
            }

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool settingsOpen = IsSettingsOpen;
            bool presenting = roundActive;

            if (rematchButton != null)
            {
                rematchButton.interactable = BattleInputGate.AllowsRematch(
                    coordinator.CanRematch, settingsOpen, presenting);

                rematchButton.gameObject.SetActive(
                    coordinator.State == BattleUiState.MatchFinished);
            }

            if (settingsView != null)
            {
                // 演出中は設定を開けません。途中で設定を変えられると、
                // 再生中の見た目と設定が食い違います。
                settingsView.SetSettingsInteractable(
                    BattleInputGate.AllowsSettings(coordinator.State, IsPresenting));
            }
        }

        private void ShowRoundBanner()
        {
            if (resultView == null || coordinator.LastResult == null)
            {
                return;
            }

            RoundResult result = coordinator.LastResult;

            resultView.ShowRound(
                result,
                FindPlayerCard(result.PlayerUnit.InstanceId),
                FindCpuCard(result.CpuUnit.InstanceId),
                coordinator.PlayerWins,
                coordinator.CpuWins);
        }

        // ---------------- 組み立て ----------------

        private IBattleMatchSource CreateMatchSource()
        {
            PlayerSideLoader.TryLoad(
                SquadRepositoryProvider.Shared,
                setId,
                roster,
                out BattleSideRoster playerSide,
                out BattleError _);

            // seedを指定しない場合だけ、起動ごとに違う対戦になります。
            IRandomSource random = new SystemRandomSource(
                cpuRandomSeed != 0 ? cpuRandomSeed : System.Environment.TickCount);

            matchSource = new BattleMatchSource(
                playerSide,
                new RosterCpuSideBuilder(roster, random),
                random);

            return matchSource;
        }

        private void BindViews()
        {
            if (playerCombatant != null)
            {
                playerCombatant.Bind(palette, text, battleText);
                playerCombatant.ShowEmpty();
            }

            if (cpuCombatant != null)
            {
                cpuCombatant.Bind(palette, text, battleText);
                cpuCombatant.ShowHidden();
            }

            if (attributeLinkView != null)
            {
                attributeLinkView.Bind(palette, battleText);
            }

            if (scoreView != null)
            {
                scoreView.Bind(battleText);
            }

            if (resultView != null)
            {
                resultView.Bind(battleText, text);
            }

            if (enemyStatus != null)
            {
                enemyStatus.Build(battleText);
            }

            if (settingsView != null)
            {
                settingsView.Bind(battleText);
            }

            RebuildWheel();
            ApplyFxLabel();
        }

        /// <summary>
        /// リングと履歴を作り直し、プレイヤー編成を流し込みます。
        /// REMATCHでは7体リングが元の順序で戻り、履歴7枠は空になります。
        /// </summary>
        private void RebuildWheel()
        {
            BattleSideRoster side = matchSource != null ? matchSource.PlayerSide : null;

            ring.Build(side != null ? side.Cards : null);
            history.Clear();

            if (playerWheel != null)
            {
                playerWheel.Bind(ring, palette, text);
                playerWheel.SetSide(side);
                playerWheel.Refresh();
            }

            if (historyLane != null)
            {
                historyLane.Clear();
                historyLane.Show(history, side, palette, text);
            }
        }

        private void AddListeners()
        {
            if (playerWheel != null)
            {
                playerWheel.DeployRequested += OnWheelDeployRequested;
                playerWheel.FocusChanged += OnWheelFocusChanged;
            }

            if (rematchButton != null)
            {
                rematchButton.onClick.AddListener(OnRematchClicked);
            }
        }

        private void RemoveListeners()
        {
            if (playerWheel != null)
            {
                playerWheel.DeployRequested -= OnWheelDeployRequested;
                playerWheel.FocusChanged -= OnWheelFocusChanged;
            }

            if (rematchButton != null)
            {
                rematchButton.onClick.RemoveListener(OnRematchClicked);
            }
        }

        private void ApplyStaticLabels()
        {
            SetText(screenTitleLabel, battleText.Battle);
            SetText(rematchLabel, battleText.Rematch);
            SetText(squadRequiredTitleLabel, battleText.SquadRequired);
            SetText(squadRequiredHintLabel, battleText.SquadRequiredHint);
        }

        /// <summary>FXの現在値を設定パネルへ反映します。</summary>
        private void ApplyFxLabel()
        {
            if (settingsView != null)
            {
                settingsView.SetFxState(FxEnabled);
            }
        }

        private BattleUnitCard FindPlayerCard(string instanceId)
        {
            return matchSource != null && matchSource.PlayerSide != null
                ? matchSource.PlayerSide.Find(instanceId)
                : null;
        }

        private BattleUnitCard FindCpuCard(string instanceId)
        {
            return matchSource != null && matchSource.CpuSide != null
                ? matchSource.CpuSide.Find(instanceId)
                : null;
        }

        /// <summary>勝者側の属性色で光らせます。引き分けは白です。</summary>
        private Color ResolveFlashColor(RoundWinner winner)
        {
            switch (winner)
            {
                case RoundWinner.Player:
                    return playerCombatant != null
                        ? playerCombatant.PrimaryColor
                        : Color.white;

                case RoundWinner.Cpu:
                    return cpuCombatant != null
                        ? cpuCombatant.PrimaryColor
                        : Color.white;

                default:
                    return Color.white;
            }
        }

        private bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleScreenController),
                ReferenceCheck.Of(nameof(roster), roster),
                ReferenceCheck.Of(nameof(palette), palette),
                ReferenceCheck.Of(nameof(text), text),
                ReferenceCheck.Of(nameof(battleText), battleText),
                ReferenceCheck.Of(nameof(playerCombatant), playerCombatant),
                ReferenceCheck.Of(nameof(cpuCombatant), cpuCombatant),
                ReferenceCheck.Of(nameof(playerWheel), playerWheel),
                ReferenceCheck.Of(nameof(historyLane), historyLane),
                ReferenceCheck.Of(nameof(deployTransition), deployTransition),
                ReferenceCheck.Of(nameof(slideUpGuide), slideUpGuide),
                ReferenceCheck.Of(nameof(coreGate), coreGate),
                ReferenceCheck.Of(nameof(scorePips), scorePips),
                ReferenceCheck.Of(nameof(enemyStatus), enemyStatus),
                ReferenceCheck.Of(nameof(scoreView), scoreView),
                ReferenceCheck.Of(nameof(resultView), resultView),
                ReferenceCheck.Of(nameof(fxPlayer), fxPlayer),
                ReferenceCheck.Of(nameof(settingsView), settingsView),
                ReferenceCheck.Of(nameof(battleRoot), battleRoot),
                ReferenceCheck.Of(nameof(squadRequiredRoot), squadRequiredRoot),
                ReferenceCheck.Of(nameof(rematchButton), rematchButton),
                ReferenceCheck.Of(nameof(screenTitleLabel), screenTitleLabel),
                ReferenceCheck.Of(nameof(rematchLabel), rematchLabel),
                ReferenceCheck.Of(
                    nameof(squadRequiredTitleLabel), squadRequiredTitleLabel),
                ReferenceCheck.Of(
                    nameof(squadRequiredHintLabel), squadRequiredHintLabel));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
