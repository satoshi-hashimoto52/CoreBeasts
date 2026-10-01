using CoreBeasts.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 発進の案内。中央発進台の上へ出します。
    ///
    /// 通常時に出すのは、低alphaのシアンの二重シェブロンだけです。
    /// 白い中心線も、常時表示の SLIDE UP 文字も持ちません。
    /// キャラクターへ文字を重ねないため、文字そのものを使いません。
    ///
    /// 上方向へ入力がロックされたあいだだけ、発進台から戦場へ向かう
    /// 一時的なエネルギーレールを出します。横回転中・入力不可・PointerUp後は出しません。
    ///
    /// 判定は一切しません。進み具合を受け取って見た目を変えるだけです。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSlideUpGuideView : MonoBehaviour
    {
        [Header("Idle hint")]
        [Tooltip("通常時に出す二重シェブロンの根。")]
        [SerializeField] private GameObject chevronRoot;

        [SerializeField] private CanvasGroup chevronGroup;

        [Tooltip("軽い上下パルスを掛ける対象。")]
        [SerializeField] private RectTransform chevron;

        [Header("Launch rail")]
        [Tooltip("上方向ロック中だけ出す、戦場へ向かうエネルギーレール。")]
        [SerializeField] private GameObject railRoot;

        [SerializeField] private RectTransform rail;
        [SerializeField] private Image railImage;

        [Header("Look")]
        [Tooltip("通常時のシェブロンの濃さ。控えめにします。")]
        [SerializeField] [Range(0.1f, 1f)] private float idleAlpha = 0.42f;

        [Tooltip("パルスの振れ幅（Canvas単位）。")]
        [SerializeField] private float pulseAmplitude = 5f;

        [Tooltip("パルスの速さ。")]
        [SerializeField] private float pulseSpeed = 2.2f;

        [Tooltip("レールが伸びきったときの高さ（Canvas単位）。")]
        [SerializeField] private float railFullHeight = 260f;

        [Tooltip("待機側の色。シアン。")]
        [SerializeField] private Color railIdleColor = new Color(0.34f, 0.82f, 0.94f, 0f);

        [Tooltip("成立側の色。アンバー。")]
        [SerializeField] private Color railCommitColor = new Color(1f, 0.74f, 0.32f, 0.85f);

        private Vector2 chevronRest;
        private bool captured;
        private bool fxEnabled = true;

        /// <summary>通常時の案内を出しているか。</summary>
        public bool IsHintVisible => chevronRoot != null && chevronRoot.activeSelf;

        /// <summary>発進レールを出しているか。</summary>
        public bool IsRailVisible => railRoot != null && railRoot.activeSelf;

        /// <summary>直近に受け取った進み具合（確認・テスト用）。</summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            Capture();

            SetHintVisible(false);
            HideRail();
        }

        private void OnDisable()
        {
            // 中断しても案内が出たままにはしません。
            HideRail();
        }

        private void Update()
        {
            if (!fxEnabled || chevron == null || !IsHintVisible)
            {
                return;
            }

            Capture();

            // 操作を促す、ごく軽い上下パルスです。
            float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;

            chevron.anchoredPosition =
                new Vector2(chevronRest.x, chevronRest.y + wave);
        }

        /// <summary>FX OFF ではパルスを止め、静止表示にします。</summary>
        public void SetFxEnabled(bool enabled)
        {
            fxEnabled = enabled;

            if (!enabled && chevron != null)
            {
                Capture();
                chevron.anchoredPosition = chevronRest;
            }
        }

        /// <summary>通常時の案内（シェブロン）を出すかどうか。</summary>
        public void SetHintVisible(bool visible)
        {
            if (chevronRoot != null && chevronRoot.activeSelf != visible)
            {
                chevronRoot.SetActive(visible);
            }

            if (chevronGroup != null)
            {
                chevronGroup.alpha = idleAlpha;
            }

            if (!visible && chevron != null)
            {
                Capture();
                chevron.anchoredPosition = chevronRest;
            }
        }

        /// <summary>
        /// 上方向ロック中の進み具合（0〜1）を反映します。
        /// 伸びる長さと濃さが進み、成立点でアンバーへ変わります。
        /// </summary>
        public void ShowRail(float progress)
        {
            Progress = Mathf.Clamp01(progress);

            if (railRoot != null && !railRoot.activeSelf)
            {
                railRoot.SetActive(true);
            }

            if (rail != null)
            {
                Vector2 size = rail.sizeDelta;
                rail.sizeDelta = new Vector2(size.x, railFullHeight * Progress);
            }

            if (railImage != null)
            {
                // シアン → アンバー。成立点で一番強くなります。
                railImage.color = Color.Lerp(railIdleColor, railCommitColor, Progress);
            }
        }

        /// <summary>
        /// レールを必ず消します。PointerUp・キャンセル・横回転・入力不可で呼びます。
        /// 何度呼んでも安全です。
        /// </summary>
        public void HideRail()
        {
            Progress = 0f;

            if (railRoot != null && railRoot.activeSelf)
            {
                railRoot.SetActive(false);
            }

            if (rail != null)
            {
                Vector2 size = rail.sizeDelta;
                rail.sizeDelta = new Vector2(size.x, 0f);
            }

            if (railImage != null)
            {
                railImage.color = railIdleColor;
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleSlideUpGuideView),
                ReferenceCheck.Of(nameof(chevronRoot), chevronRoot),
                ReferenceCheck.Of(nameof(chevronGroup), chevronGroup),
                ReferenceCheck.Of(nameof(chevron), chevron),
                ReferenceCheck.Of(nameof(railRoot), railRoot),
                ReferenceCheck.Of(nameof(rail), rail),
                ReferenceCheck.Of(nameof(railImage), railImage));
        }

        private void Capture()
        {
            if (captured || chevron == null)
            {
                return;
            }

            chevronRest = chevron.anchoredPosition;
            captured = true;
        }
    }
}
