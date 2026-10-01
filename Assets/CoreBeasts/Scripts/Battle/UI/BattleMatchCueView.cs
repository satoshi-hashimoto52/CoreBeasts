using System;
using System.Collections;

using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 試合の節目の表示（Phase 3）。FINAL CORE と CORE BREAK を、中央の戦闘表示領域に出します。
    ///
    /// FX ON  : <see cref="CoreBreakGraphic"/>のリング・亀裂・フラッシュと文字。
    /// FX OFF : 状態を伝える短い文字だけ。リング・亀裂・フラッシュは出しません。
    ///
    /// 描くのは戦闘表示領域だけを覆う専用 RectTransform の中だけで、
    /// Header・Score・HistoryLane・PlayerWheel・ボタンは動かしません。振動もしません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleMatchCueView : MonoBehaviour
    {
        /// <summary>FINAL CORE の表示文言。</summary>
        public const string FinalCoreText = "FINAL CORE";

        /// <summary>CORE BREAK の表示文言。</summary>
        public const string CoreBreakText = "CORE BREAK";

        [SerializeField] private CoreBreakGraphic graphic;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup labelGroup;

        private int playToken;

        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>いま出している節目。出していなければ <see cref="MatchCueKind.None"/>。</summary>
        public MatchCueKind Current { get; private set; } = MatchCueKind.None;

        /// <summary>この画面で FINAL CORE を出した回数（確認・テスト用）。</summary>
        public int FinalCorePlays { get; private set; }

        /// <summary>この画面で CORE BREAK を出した回数（確認・テスト用）。</summary>
        public int CoreBreakPlays { get; private set; }

        /// <summary>描いている Graphic。</summary>
        public CoreBreakGraphic Graphic => graphic;

        /// <summary>文字ラベル。</summary>
        public TMP_Text Label => label;

        /// <summary>文字の不透明度。</summary>
        public float LabelAlpha => labelGroup != null ? labelGroup.alpha : 0f;

        private void Awake()
        {
            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        /// <summary>
        /// 節目を1回出します。<paramref name="fxEnabled"/>が false なら文字だけです。
        /// </summary>
        public IEnumerator PlayRoutine(MatchCueKind kind, bool fxEnabled, float duration)
        {
            int token = ++playToken;

            if (kind == MatchCueKind.None || duration <= 0f)
            {
                yield break;
            }

            Current = kind;
            IsPlaying = true;

            if (kind == MatchCueKind.FinalCore)
            {
                FinalCorePlays++;
            }
            else
            {
                CoreBreakPlays++;
            }

            if (label != null)
            {
                label.text = kind == MatchCueKind.FinalCore ? FinalCoreText : CoreBreakText;
            }

            SetLabelAlpha(fxEnabled ? 0f : 1f);

            if (fxEnabled && graphic != null)
            {
                RectTransform area = (RectTransform)transform;

                graphic.Begin(
                    kind == MatchCueKind.FinalCore ? CoreBreakGraphic.Mode.FinalCore : CoreBreakGraphic.Mode.CoreBreak,
                    area.TransformPoint(area.rect.center));
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (token != playToken)
                {
                    yield break;
                }

                elapsed += deltaTimeSource();

                float p = Mathf.Clamp01(elapsed / duration);

                if (fxEnabled)
                {
                    // CORE BREAK の文字はフラッシュと同じころに立ち上げ、最後に引きます。
                    float rise = kind == MatchCueKind.CoreBreak ? 0.45f : 0.1f;

                    SetLabelAlpha(Mathf.Clamp01((p - rise) / 0.12f) * (1f - Mathf.Clamp01((p - 0.82f) / 0.18f)));

                    if (graphic != null)
                    {
                        graphic.SetProgress(p);
                    }
                }

                yield return null;
            }

            if (token == playToken)
            {
                Clear();
            }
        }

        /// <summary>表示を消し、再生中のルーチンを止めます。何度呼んでも安全です。</summary>
        public void ResetVisuals()
        {
            playToken++;
            Clear();
        }

        /// <summary>試合を作り直したときの記録の初期化（確認・テスト用の回数だけ）。</summary>
        public void ResetCounters()
        {
            FinalCorePlays = 0;
            CoreBreakPlays = 0;
        }

        /// <summary>未設定のSerializeFieldがあれば報告します。</summary>
        public bool HasRequiredReferences()
        {
            return CoreBeasts.Units.ReferenceCheck.Validate(
                this,
                nameof(BattleMatchCueView),
                CoreBeasts.Units.ReferenceCheck.Of(nameof(graphic), graphic),
                CoreBeasts.Units.ReferenceCheck.Of(nameof(label), label),
                CoreBeasts.Units.ReferenceCheck.Of(nameof(labelGroup), labelGroup));
        }

        private void Clear()
        {
            IsPlaying = false;
            Current = MatchCueKind.None;

            if (graphic != null)
            {
                graphic.Hide();
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            SetLabelAlpha(0f);
        }

        private void SetLabelAlpha(float alpha)
        {
            if (labelGroup != null)
            {
                labelGroup.alpha = alpha;
            }
        }
    }
}
