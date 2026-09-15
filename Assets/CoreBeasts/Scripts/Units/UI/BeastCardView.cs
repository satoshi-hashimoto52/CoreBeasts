using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 所持一覧の1マス。名前・レベル・属性を、色だけに頼らず表示します。
    ///
    /// ジェスチャー判定は<see cref="CardGestureStateMachine"/>が持ち、
    /// Idle / Pending / Scrolling / SquadDragging を明示管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeastCardView : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        /// <summary>テスト用の時間差し替え。null なら Time.unscaledTime。</summary>
        public static Func<float> TimeProvider;

        [Header("Input thresholds")]
        [Tooltip("この時間だけ押し続けてから動かすと、編成ドラッグになります。")]
        [SerializeField] [Range(0.05f, 0.6f)] private float holdToDragSeconds = 0.2f;

        [Tooltip("この距離を超えた瞬間に、スクロールか編成ドラッグかを確定します。")]
        [SerializeField] [Min(1f)] private float dragStartDistance = 24f;

        [Header("Parts")]
        [Tooltip("長押し成立時の浮き上がり表示。")]
        [SerializeField] private CardLiftView liftView;

        [SerializeField] private Image background;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private BeastThumbnailView thumbnail;
        [SerializeField] private Image attributeChip;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;

        [Header("Selection appearance")]

        [Header("Squad membership")]
        [Tooltip("MY SQUADへ編成済みのときだけ出すチェックバッジ。")]
        [SerializeField] private GameObject squadBadge;
        [SerializeField] private Color normalBackground = new Color(0.16f, 0.18f, 0.22f, 1f);
        [SerializeField] private Color selectedBackground = new Color(0.29f, 0.34f, 0.42f, 1f);
        [SerializeField] private Color normalFrame = new Color(0.32f, 0.36f, 0.42f, 1f);
        [SerializeField] private Color selectedFrame = new Color(1f, 0.85f, 0.35f, 1f);

        private OwnedCoreBeast boundBeast;
        private IBeastCardListener listener;
        private ScrollRect parentScroll;
        private bool scrollWasEnabled = true;
        private bool scrollSuspended;

        private CardGestureStateMachine gesture;

        /// <summary>このマスが表す所持個体。</summary>
        public OwnedCoreBeast Beast => boundBeast;

        /// <summary>浮き上がり表示になっているか（確認・テスト用）。</summary>
        public bool IsDragReadyVisual => liftView != null && liftView.IsLifted;

        /// <summary>現在のジェスチャー状態。</summary>
        public CardGestureState CurrentState =>
            Gesture != null ? Gesture.State : CardGestureState.Idle;

        private CardGestureStateMachine Gesture =>
            gesture ??= new CardGestureStateMachine(
                holdToDragSeconds, dragStartDistance);

        /// <summary>
        /// 親のScrollRect。Prefabはシーン上のScrollRectを参照できないため実行時に解決します。
        /// Awakeの時点で親が決まっていない生成手順（Instantiate後にSetParentする等）でも
        /// 取りこぼさないよう、未解決なら都度解決します。
        /// </summary>
        private ScrollRect ParentScroll =>
            parentScroll != null
                ? parentScroll
                : parentScroll = GetComponentInParent<ScrollRect>();

        private static float Now =>
            TimeProvider != null ? TimeProvider() : Time.unscaledTime;

        private void Awake()
        {
            if (ParentScroll != null)
            {
                scrollWasEnabled = ParentScroll.enabled;
            }
        }

        private void Update()
        {
            // 長押し成立は押下継続時間で決まり、OnBeginDragには依存しません。
            // 成立した瞬間に、移動を待たず浮き上がり表示へ切り替えます。
            Apply(Gesture.Tick(Now), null);
        }

        private void OnDisable()
        {
            RestoreScroll();
            Gesture.Reset();

            if (liftView != null)
            {
                liftView.ResetImmediate();
            }
        }

        private void OnDestroy()
        {
            if (liftView != null)
            {
                liftView.ResetImmediate();
            }
        }

        /// <summary>1個体を割り当てて表示を作ります。</summary>
        public void Bind(
            OwnedCoreBeast beast,
            AttributePalette palette,
            UiTextCatalog text,
            IBeastCardListener cardListener)
        {
            boundBeast = beast;
            listener = cardListener;

            if (beast == null || !beast.IsValid || text == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            CoreBeastDefinition definition = beast.Definition;

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }

            if (levelLabel != null)
            {
                levelLabel.text = text.FormatLevel(beast.Level);
            }

            if (attributeLabel != null)
            {
                attributeLabel.text =
                    text.BuildAttributeSymbol(definition) + " " +
                    text.BuildAttributeLabel(definition);
            }

            if (attributeChip != null && palette != null)
            {
                attributeChip.color =
                    palette.GetColors(definition.PrimaryAttribute).PrimaryColor;
            }

            if (liftView != null)
            {
                if (palette != null)
                {
                    liftView.SetAttributeColor(
                        palette.GetColors(definition.PrimaryAttribute).PrimaryColor);
                }

                liftView.ResetImmediate();
            }

            if (thumbnail != null)
            {
                thumbnail.Show(definition, palette);
            }

            SetSelected(false);
            SetInSquad(false);
        }

        /// <summary>
        /// MY SQUADへ編成済みかどうかを右上のバッジで示します。
        /// 選択状態(枠線・背景・チェック)とは別の意味として共存させます。
        /// </summary>
        public void SetInSquad(bool inSquad)
        {
            if (squadBadge != null)
            {
                squadBadge.SetActive(inSquad);
            }
        }

        /// <summary>編成済みバッジが出ているか（確認・テスト用）。</summary>
        public bool IsMarkedInSquad =>
            squadBadge != null && squadBadge.activeSelf;

        /// <summary>
        /// 選択状態を、枠線と背景色だけで示します。
        /// 編成済みを示す右上のシアンのチェックとは意味が異なるため、
        /// 選択側にはチェック形状を使いません。
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            if (background != null)
            {
                background.color = isSelected ? selectedBackground : normalBackground;
            }

            if (selectionFrame != null)
            {
                selectionFrame.color = isSelected ? selectedFrame : normalFrame;
            }
        }

        // ---------------- ポインタイベント ----------------

        public void OnPointerDown(PointerEventData eventData)
        {
            Gesture.PointerDown(eventData.pointerId, eventData.position, Now);
            Log("OnPointerDown", eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            Log("OnInitializePotentialDrag", eventData);

            ScrollRect scroll = ParentScroll;

            if (scroll != null)
            {
                ExecuteEvents.Execute(
                    scroll.gameObject,
                    eventData,
                    ExecuteEvents.initializePotentialDrag);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // ここでは判定しません。移動量がしきい値を超えた時点でOnDragが決めます。
            Log("OnBeginDrag", eventData);
            Apply(Gesture.Move(eventData.pointerId, eventData.position, Now), eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Apply(Gesture.Move(eventData.pointerId, eventData.position, Now), eventData);
            Log("OnDrag", eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Apply(Gesture.End(eventData.pointerId), eventData);
            Log("OnEndDrag", eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // ドラッグを伴わない離しでも必ずIdleへ戻します。
            Apply(Gesture.End(eventData.pointerId), eventData);
            RestoreScroll();
            SetLift(CardLiftView.LiftState.Normal);
            Log("OnPointerUp", eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Log("OnPointerClick", eventData);

            if (Gesture.GestureConsumed || boundBeast == null)
            {
                return;
            }

            listener?.OnCardTapped(boundBeast);
        }

        // ---------------- 内部 ----------------

        private void Apply(CardGestureAction action, PointerEventData eventData)
        {
            switch (action)
            {
                case CardGestureAction.HoldSatisfied:
                    // 指を動かす前にここへ来ます。
                    SetLift(CardLiftView.LiftState.DragReady);
                    Log("HoldSatisfied", eventData);
                    break;

                case CardGestureAction.Cancelled:
                    SetLift(CardLiftView.LiftState.Normal);
                    break;

                case CardGestureAction.BeginScroll:
                    Forward(eventData, ExecuteEvents.beginDragHandler);
                    Forward(eventData, ExecuteEvents.dragHandler);
                    break;

                case CardGestureAction.ContinueScroll:
                    Forward(eventData, ExecuteEvents.dragHandler);
                    break;

                case CardGestureAction.EndScroll:
                    Forward(eventData, ExecuteEvents.endDragHandler);
                    SetLift(CardLiftView.LiftState.Normal);
                    break;

                case CardGestureAction.BeginSquadDrag:
                    SuspendScroll();
                    // 浮いたままドラッグ中表示へ引き継ぎます。
                    SetLift(CardLiftView.LiftState.Dragging);
                    listener?.OnCardDragBegin(boundBeast, eventData);
                    listener?.OnCardDragMove(eventData);
                    break;

                case CardGestureAction.ContinueSquadDrag:
                    listener?.OnCardDragMove(eventData);
                    break;

                case CardGestureAction.EndSquadDrag:
                    listener?.OnCardDragEnd(boundBeast, eventData);
                    RestoreScroll();
                    SetLift(CardLiftView.LiftState.Normal);
                    break;
            }
        }

        private void SetLift(CardLiftView.LiftState state)
        {
            if (liftView != null)
            {
                liftView.SetState(state);
            }
        }

        /// <summary>編成ドラッグ中はスクロールを止めます。</summary>
        private void SuspendScroll()
        {
            ScrollRect scroll = ParentScroll;

            if (scroll == null || scrollSuspended)
            {
                return;
            }

            scrollWasEnabled = scroll.enabled;
            scroll.StopMovement();
            scroll.enabled = false;
            scrollSuspended = true;
        }

        /// <summary>操作終了時に必ずスクロールを元へ戻します。</summary>
        private void RestoreScroll()
        {
            if (parentScroll == null || !scrollSuspended)
            {
                return;
            }

            parentScroll.enabled = scrollWasEnabled;
            scrollSuspended = false;
        }

        private void Forward<T>(
            PointerEventData eventData,
            ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
        {
            ScrollRect scroll = ParentScroll;

            if (scroll != null)
            {
                ExecuteEvents.Execute(scroll.gameObject, eventData, handler);
            }
        }

        private void Log(string eventName, PointerEventData eventData)
        {
            if (!GestureLog.Enabled)
            {
                return;
            }

            GestureLog.Write(
                "BeastCardView",
                eventName,
                eventData,
                Gesture.ElapsedSincePress(Now),
                eventData != null ? Gesture.MovedSincePress(eventData.position) : 0f,
                Gesture.State,
                boundBeast != null ? boundBeast.InstanceId : null,
                -1
            );
        }
    }
}
