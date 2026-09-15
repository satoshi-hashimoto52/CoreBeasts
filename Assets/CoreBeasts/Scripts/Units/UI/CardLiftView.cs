using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 長押し成立（ドラッグ可能）を、浮き上がり・拡大・影・発光で示します。
    ///
    /// GridLayoutGroupが管理するカードのルートは動かさず、
    /// 表示専用の子(VisualRoot)のlocalPositionとlocalScaleだけを補間します。
    /// 補間は基準値からの割合で計算するため、連続操作でも累積しません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardLiftView : MonoBehaviour
    {
        /// <summary>表示状態。</summary>
        public enum LiftState
        {
            Normal,
            DragReady,
            Dragging,
        }

        [Header("Parts")]
        [Tooltip("アニメーション対象。カードのルートではなく表示専用の子を指定します。")]
        [SerializeField] private RectTransform visualRoot;

        [SerializeField] private CanvasGroup visualGroup;
        [SerializeField] private GameObject dragShadow;
        [SerializeField] private GameObject dragGlow;
        [SerializeField] private Image dragGlowImage;

        [Tooltip("前面表示に使うCanvas。常に有効のままにし、sortingだけを切り替えます。")]
        [SerializeField] private Canvas frontCanvas;

        [SerializeField] private int frontSortingOrder = 10;

        [Header("Motion")]
        [Tooltip("浮き上がる距離(Canvas units)。iPhone 16 Proで約9.9pt。")]
        [SerializeField] [Range(8f, 40f)] private float liftDistance = 24f;

        [Tooltip("拡大率。")]
        [SerializeField] [Range(1.01f, 1.15f)] private float liftScale = 1.06f;

        [Tooltip("変化にかける時間(秒)。ループしません。")]
        [SerializeField] [Range(0.05f, 0.2f)] private float transitionSeconds = 0.1f;

        [Tooltip("ドラッグ中に元カードを少し沈ませる不透明度。")]
        [SerializeField] [Range(0.4f, 1f)] private float draggingAlpha = 0.72f;

        [Tooltip("発光の不透明度。")]
        [SerializeField] [Range(0.1f, 0.8f)] private float glowAlpha = 0.45f;

        private Vector3 restPosition;
        private Vector3 restScale;
        private int restSortingOrder;
        private bool captured;

        private float progress;
        private float targetProgress;

        /// <summary>現在の表示状態。</summary>
        public LiftState State { get; private set; } = LiftState.Normal;

        /// <summary>浮き上がり表示になっているか（テスト・確認用）。</summary>
        public bool IsLifted => State != LiftState.Normal;

        /// <summary>補間の進捗(0=通常, 1=浮き上がり)。</summary>
        public float LiftProgress => progress;

        private void Awake()
        {
            Capture();
            ResetImmediate();
        }

        private void OnDisable()
        {
            ResetImmediate();
        }

        private void OnDestroy()
        {
            ResetImmediate();
        }

        private void Update()
        {
            if (Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            float step = transitionSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / transitionSeconds;

            progress = Mathf.MoveTowards(progress, targetProgress, step);

            ApplyProgress();
        }

        /// <summary>発光色を属性色に合わせます。</summary>
        public void SetAttributeColor(Color color)
        {
            if (dragGlowImage == null)
            {
                return;
            }

            color.a = glowAlpha;
            dragGlowImage.color = color;
        }

        /// <summary>表示状態を切り替えます。</summary>
        public void SetState(LiftState state)
        {
            State = state;

            switch (state)
            {
                case LiftState.Normal:
                    targetProgress = 0f;
                    SetDecorationsActive(false);
                    SetAlpha(1f);
                    break;

                case LiftState.DragReady:
                    targetProgress = 1f;
                    SetDecorationsActive(true);
                    SetAlpha(1f);
                    break;

                case LiftState.Dragging:
                    targetProgress = 1f;
                    SetDecorationsActive(true);
                    SetAlpha(draggingAlpha);
                    break;
            }

            // 目標が同じでも、装飾の表示切り替えは即時反映します。
            ApplyProgress();
        }

        /// <summary>補間を待たずに通常表示へ戻します。表示が残らないようにします。</summary>
        public void ResetImmediate()
        {
            Capture();

            State = LiftState.Normal;
            progress = 0f;
            targetProgress = 0f;

            SetDecorationsActive(false);
            SetAlpha(1f);
            ApplyProgress();
        }

        private void Capture()
        {
            if (captured || visualRoot == null)
            {
                return;
            }

            restPosition = visualRoot.localPosition;
            restScale = visualRoot.localScale;

            if (frontCanvas != null)
            {
                restSortingOrder = frontCanvas.sortingOrder;
            }

            captured = true;
        }

        /// <summary>基準値からの割合で毎回計算するため、値が累積しません。</summary>
        private void ApplyProgress()
        {
            if (visualRoot == null)
            {
                return;
            }

            Capture();

            visualRoot.localPosition =
                restPosition + new Vector3(0f, liftDistance * progress, 0f);

            visualRoot.localScale =
                restScale * Mathf.LerpUnclamped(1f, liftScale, progress);
        }

        private void SetDecorationsActive(bool active)
        {
            if (dragShadow != null)
            {
                dragShadow.SetActive(active);
            }

            if (dragGlow != null)
            {
                dragGlow.SetActive(active);
            }

            SetFrontMost(active);
        }

        /// <summary>
        /// 浮いている間だけ前面へ出します。
        ///
        /// Canvasは常に有効のままにします。
        /// ネストしたCanvasを無効化すると、その配下のGraphicが一切描画されなくなり、
        /// カードそのものが消えてしまうためです。
        /// 前面化の解除は overrideSorting を戻すことで行います。
        /// </summary>
        private void SetFrontMost(bool front)
        {
            if (frontCanvas == null)
            {
                return;
            }

            Capture();

            // 無効化しない。描画は常に生かしたままにします。
            frontCanvas.enabled = true;

            frontCanvas.overrideSorting = front;
            frontCanvas.sortingOrder = front ? frontSortingOrder : restSortingOrder;
        }

        private void SetAlpha(float alpha)
        {
            if (visualGroup != null)
            {
                visualGroup.alpha = alpha;
            }
        }
    }
}
