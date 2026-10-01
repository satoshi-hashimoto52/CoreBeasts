namespace CoreBeasts.Battle.UI
{
    /// <summary>リング操作のジェスチャー状態。</summary>
    public enum BattleWheelGesture
    {
        /// <summary>触れていません。</summary>
        Idle = 0,

        /// <summary>触れたが、まだ横回転か上スライドかを決めていません。</summary>
        Pending = 1,

        /// <summary>横回転として確定。指を離すまで上スライドへは変わりません。</summary>
        Rotating = 2,

        /// <summary>上スライドとして確定。指を離すまで回転へは変わりません。</summary>
        SlidingUp = 3,

        /// <summary>
        /// この操作では何もしないと確定。下方向や、中央以外からの上方向がここへ来ます。
        /// 指を離すまで他の状態へは変わりません。
        /// </summary>
        Rejected = 4,
    }

    /// <summary>
    /// 横スワイプと上スライドを取り違えないための方向ロック。
    ///
    /// Unityへ依存しません。座標はすべて pt 相当で受け取ります。
    ///
    /// 決め方:
    ///   1. 押した直後は<see cref="BattleWheelGesture.Pending"/>（方向未確定）
    ///   2. 12pt 動いた時点で1度だけ方向を決める
    ///   3. 横の勝ち   : abs(横) > abs(縦) * 1.0
    ///      上の勝ち   : 上向き距離 > abs(横) * 1.25
    ///      それ以外   : Rejected（下方向はここへ来ます）
    ///   4. 一度決めたら指を離すまで変わらない
    ///
    /// 中央以外の個体から上へ動かしても出撃しません（<see cref="Begin"/>で渡します）。
    /// </summary>
    public sealed class BattleWheelGestureStateMachine
    {
        /// <summary>方向を決めるまでに必要な移動量（pt）。</summary>
        public const float DirectionLockDistance = 12f;

        /// <summary>横が勝つための比率。</summary>
        public const float HorizontalBias = 1.0f;

        /// <summary>上が勝つための比率。</summary>
        public const float UpwardBias = 1.25f;

        /// <summary>出撃が成立する上向き距離（pt）。</summary>
        public const float CommitDistance = 72f;

        /// <summary>出撃が成立する上向き速度（pt/秒）。</summary>
        public const float CommitVelocity = 900f;

        /// <summary>1体回すのに必要な、カード間隔に対する累積移動の割合。</summary>
        public const float OneStepRatio = 0.35f;

        /// <summary>2体回すのに必要な割合。</summary>
        public const float TwoStepRatio = 1.35f;

        /// <summary>この速さ以上のフリックなら、距離が足りなくても1体回します（pt/秒）。</summary>
        public const float FlickVelocity = 320f;

        private float startX;
        private float startY;
        private float currentX;
        private float currentY;
        private bool fromCenter;
        private bool interactable;

        /// <summary>現在の状態。</summary>
        public BattleWheelGesture State { get; private set; } = BattleWheelGesture.Idle;

        /// <summary>押し始めからの横移動（右が正、pt）。</summary>
        public float HorizontalDistance => currentX - startX;

        /// <summary>押し始めからの上向き移動（上が正、pt）。負なら下向きです。</summary>
        public float UpwardDistance => currentY - startY;

        /// <summary>今この操作でリングを回してよいか。</summary>
        public bool IsRotating => State == BattleWheelGesture.Rotating;

        /// <summary>今この操作で出撃へ向かっているか。</summary>
        public bool IsSlidingUp => State == BattleWheelGesture.SlidingUp;

        /// <summary>
        /// 出撃の成立まで、あとどれくらいか（0〜1）。
        /// 成立直前に少し拡大するのは、この値を見て行います。
        /// </summary>
        public float SlideProgress
        {
            get
            {
                if (State != BattleWheelGesture.SlidingUp || CommitDistance <= 0f)
                {
                    return 0f;
                }

                float ratio = UpwardDistance / CommitDistance;

                if (ratio < 0f)
                {
                    return 0f;
                }

                return ratio > 1f ? 1f : ratio;
            }
        }

        /// <summary>
        /// 操作を始めます。
        /// <paramref name="isCenterItem"/> が false なら、上スライドは成立しません。
        /// <paramref name="allowInteraction"/> が false なら、何も受け付けません
        /// （設定パネル表示中・演出中・決着後）。
        /// </summary>
        public void Begin(
            float x, float y, bool isCenterItem, bool allowInteraction)
        {
            startX = x;
            startY = y;
            currentX = x;
            currentY = y;
            fromCenter = isCenterItem;
            interactable = allowInteraction;

            State = allowInteraction
                ? BattleWheelGesture.Pending
                : BattleWheelGesture.Rejected;
        }

        /// <summary>指を動かします。未確定なら、ここで方向が決まることがあります。</summary>
        public BattleWheelGesture Move(float x, float y)
        {
            if (State == BattleWheelGesture.Idle)
            {
                return State;
            }

            currentX = x;
            currentY = y;

            // 一度決めた方向は、指を離すまで変えません。
            if (State != BattleWheelGesture.Pending)
            {
                return State;
            }

            if (!interactable)
            {
                return State;
            }

            float horizontal = HorizontalDistance;
            float upward = UpwardDistance;

            float absHorizontal = horizontal < 0f ? -horizontal : horizontal;
            float absVertical = upward < 0f ? -upward : upward;

            float travel = absHorizontal > absVertical ? absHorizontal : absVertical;

            if (travel < DirectionLockDistance)
            {
                return State;
            }

            if (absHorizontal > absVertical * HorizontalBias)
            {
                State = BattleWheelGesture.Rotating;
                return State;
            }

            // 下方向は出撃にしません。中央以外からの上方向も受け付けません。
            if (upward > absHorizontal * UpwardBias && fromCenter)
            {
                State = BattleWheelGesture.SlidingUp;
                return State;
            }

            State = BattleWheelGesture.Rejected;

            return State;
        }

        /// <summary>
        /// 指を離したとき、出撃が成立したか。
        /// 上スライドとして確定していて、距離か速度のどちらかが足りていれば成立です。
        /// </summary>
        public bool ShouldDeploy(float upwardVelocity)
        {
            if (State != BattleWheelGesture.SlidingUp)
            {
                return false;
            }

            return UpwardDistance >= CommitDistance || upwardVelocity >= CommitVelocity;
        }

        /// <summary>
        /// 指を離したときの回転量。短いフリックで1体、強いフリックで最大2体まで。
        /// 何周も慣性で回ることはありません。
        /// </summary>
        public int ResolveRotationSteps(float horizontalVelocity, float itemSpacing)
        {
            if (itemSpacing <= 0f)
            {
                return 0;
            }

            // 判定は「押した位置からの累積移動」です。
            // 指を離す直前の1フレームぶんだけを見ると、ゆっくり動かした操作が
            // すべて 0 体になってしまいます。
            return ResolveSnapSteps(-HorizontalDistance / itemSpacing, horizontalVelocity);
        }

        /// <summary>
        /// 残り位相からスナップ量を決めます。
        ///
        /// Drag中にリングを回し続ける実装では、累積移動のうち
        /// すでに反映した回転ぶんを差し引いた「残り」だけをここへ渡します。
        /// <paramref name="residualRatio"/> はカード間隔に対する割合で、
        /// 正なら右方向の個体へ、負なら左方向の個体へ寄せます。
        /// </summary>
        public int ResolveSnapSteps(float residualRatio, float horizontalVelocity)
        {
            if (State != BattleWheelGesture.Rotating)
            {
                return 0;
            }

            float magnitude = residualRatio < 0f ? -residualRatio : residualRatio;
            int direction = residualRatio < 0f ? -1 : 1;

            int steps;

            if (magnitude >= TwoStepRatio)
            {
                steps = 2;
            }
            else if (magnitude >= OneStepRatio)
            {
                steps = 1;
            }
            else
            {
                steps = 0;
            }

            if (steps == 0)
            {
                float absVelocity =
                    horizontalVelocity < 0f ? -horizontalVelocity : horizontalVelocity;

                // 距離が足りなくても、十分速いフリックなら1体だけ回します。
                if (absVelocity >= FlickVelocity)
                {
                    steps = 1;
                    direction = horizontalVelocity < 0f ? 1 : -1;
                }
            }

            return steps * direction;
        }

        /// <summary>操作を終えます。</summary>
        public void End()
        {
            State = BattleWheelGesture.Idle;
            fromCenter = false;
            interactable = false;
        }

        /// <summary>操作を捨てます。画面離脱や設定パネルの表示で呼びます。</summary>
        public void Reset()
        {
            End();

            startX = 0f;
            startY = 0f;
            currentX = 0f;
            currentY = 0f;
        }
    }
}
