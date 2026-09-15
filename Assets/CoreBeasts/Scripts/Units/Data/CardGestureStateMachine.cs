using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>カード操作のジェスチャー状態。</summary>
    public enum CardGestureState
    {
        Idle,
        Pending,
        Scrolling,
        SquadDragging,
    }

    /// <summary>状態機械が要求する処理。</summary>
    public enum CardGestureAction
    {
        None,
        BeginScroll,
        ContinueScroll,
        EndScroll,
        BeginSquadDrag,
        ContinueSquadDrag,
        EndSquadDrag,

        /// <summary>Pending中に長押しが成立した（まだ動いていない）。</summary>
        HoldSatisfied,

        /// <summary>長押し成立後、動かさずに離した。</summary>
        Cancelled,
    }

    /// <summary>
    /// 所持カードのジェスチャー判定。UnityのUIイベントに依存しないため、
    /// 時間と座標を注入して単体テストできます。
    ///
    /// 重要:
    /// OnBeginDragが発生した瞬間だけで判定しません。
    /// OnBeginDragはUnityの pixelDragThreshold(既定10px) を超えた時点で1度だけ飛ぶため、
    /// そこで長押しを判定すると、長押し前に必ず「スクロール」と誤判定されます。
    /// ここでは押下継続時間を<see cref="Tick"/>で監視し、
    /// 移動量が<see cref="moveThreshold"/>を超えた瞬間に初めて種別を決めます。
    /// </summary>
    public sealed class CardGestureStateMachine
    {
        /// <summary>ポインタ未使用を表す値。</summary>
        public const int NoPointer = int.MinValue;

        private readonly float holdSeconds;
        private readonly float moveThreshold;

        private Vector2 pressPosition;
        private float pressTime;

        public CardGestureStateMachine(float holdSeconds, float moveThreshold)
        {
            this.holdSeconds = Mathf.Max(0f, holdSeconds);
            this.moveThreshold = Mathf.Max(0f, moveThreshold);
        }

        /// <summary>現在の状態。</summary>
        public CardGestureState State { get; private set; } = CardGestureState.Idle;

        /// <summary>受け付けているポインタID。未使用なら<see cref="NoPointer"/>。</summary>
        public int ActivePointerId { get; private set; } = NoPointer;

        /// <summary>長押しが成立しているか。</summary>
        public bool HoldSatisfied { get; private set; }

        /// <summary>この押下でスクロールか編成ドラッグが確定したか（タップ判定用）。</summary>
        public bool GestureConsumed { get; private set; }

        /// <summary>押下からの経過時間。</summary>
        public float ElapsedSincePress(float time)
        {
            return State == CardGestureState.Idle ? 0f : time - pressTime;
        }

        /// <summary>押下位置からの移動距離。</summary>
        public float MovedSincePress(Vector2 position)
        {
            return State == CardGestureState.Idle
                ? 0f
                : (position - pressPosition).magnitude;
        }

        /// <summary>
        /// 指を置いた。別のポインタが操作中なら受け付けません
        /// （異なるpointerIdを混同しないため）。
        /// </summary>
        public bool PointerDown(int pointerId, Vector2 position, float time)
        {
            if (State != CardGestureState.Idle)
            {
                return false;
            }

            ActivePointerId = pointerId;
            pressPosition = position;
            pressTime = time;
            HoldSatisfied = false;
            GestureConsumed = false;
            State = CardGestureState.Pending;

            return true;
        }

        /// <summary>
        /// 押下継続時間を監視します。毎フレーム呼んでください。
        /// 長押し成立はここで決まり、OnBeginDragには依存しません。
        /// 成立した瞬間だけ<see cref="CardGestureAction.HoldSatisfied"/>を返します。
        /// </summary>
        public CardGestureAction Tick(float time)
        {
            if (State != CardGestureState.Pending || HoldSatisfied)
            {
                return CardGestureAction.None;
            }

            if (time - pressTime < holdSeconds)
            {
                return CardGestureAction.None;
            }

            HoldSatisfied = true;

            return CardGestureAction.HoldSatisfied;
        }

        /// <summary>
        /// 指が動いた。移動量がしきい値を超えた瞬間に種別を確定します。
        /// 一度Scrollingになったら、その押下の間はSquadDraggingへ移行しません。
        /// </summary>
        public CardGestureAction Move(int pointerId, Vector2 position, float time)
        {
            if (pointerId != ActivePointerId)
            {
                return CardGestureAction.None;
            }

            switch (State)
            {
                case CardGestureState.Pending:
                    Tick(time);

                    if ((position - pressPosition).magnitude < moveThreshold)
                    {
                        return CardGestureAction.None;
                    }

                    GestureConsumed = true;

                    if (HoldSatisfied)
                    {
                        State = CardGestureState.SquadDragging;
                        return CardGestureAction.BeginSquadDrag;
                    }

                    State = CardGestureState.Scrolling;
                    return CardGestureAction.BeginScroll;

                case CardGestureState.Scrolling:
                    return CardGestureAction.ContinueScroll;

                case CardGestureState.SquadDragging:
                    return CardGestureAction.ContinueSquadDrag;

                default:
                    return CardGestureAction.None;
            }
        }

        /// <summary>操作終了。必ずIdleへ戻します。</summary>
        public CardGestureAction End(int pointerId)
        {
            if (pointerId != ActivePointerId)
            {
                return CardGestureAction.None;
            }

            CardGestureState finished = State;
            bool wasHeld = HoldSatisfied;

            State = CardGestureState.Idle;
            ActivePointerId = NoPointer;
            HoldSatisfied = false;

            switch (finished)
            {
                case CardGestureState.Scrolling:
                    return CardGestureAction.EndScroll;

                case CardGestureState.SquadDragging:
                    return CardGestureAction.EndSquadDrag;

                case CardGestureState.Pending when wasHeld:
                    // 長押しは成立したが動かさずに離した
                    return CardGestureAction.Cancelled;

                default:
                    return CardGestureAction.None;
            }
        }

        /// <summary>強制的に初期状態へ戻します。</summary>
        public void Reset()
        {
            State = CardGestureState.Idle;
            ActivePointerId = NoPointer;
            HoldSatisfied = false;
            GestureConsumed = false;
        }
    }
}
