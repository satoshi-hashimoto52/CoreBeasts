using System;
using System.Collections;

using CoreBeasts.Progression;
using TMPro;
using UnityEngine;

namespace CoreBeasts.HomeUI
{
    /// <summary>
    /// ガチャ演出の再生（Phase 7）。結果は開始前に確定済みで、ここは見た目だけを進めます。
    ///
    /// - 時間は <see cref="Time.unscaledDeltaTime"/>（timeScale を使いません）
    /// - 再生中に呼ばれても多重に起動せず、前の演出を止めてから始めます
    /// - OnDisable・HOME・シーン遷移（<see cref="ResetVisuals"/>）で図形・文字・状態を完全に戻し、完了の通知もしません
    /// - 実行中に GameObject・Component・Material を作りません
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GachaSummonView : MonoBehaviour
    {
        [SerializeField] private GachaSummonGraphic graphic;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup labelGroup;

        private int token;
        private Coroutine running;
        private Action completed;
        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>直近の演出設計。</summary>
        public GachaSummonPlan LastPlan { get; private set; }

        public GachaSummonGraphic Graphic => graphic;

        public TMP_Text Label => label;

        public float LabelAlpha => labelGroup != null ? labelGroup.alpha : 0f;

        /// <summary>実行時に組み立てた部品を渡します（Home の画面は起動時に一度だけ組み立てます）。</summary>
        public void Bind(GachaSummonGraphic summonGraphic, TMP_Text summonLabel, CanvasGroup summonLabelGroup)
        {
            graphic = summonGraphic;
            label = summonLabel;
            labelGroup = summonLabelGroup;
            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        /// <summary>演出を始めます。終わったら <paramref name="onCompleted"/> を一度だけ呼びます。</summary>
        public void Play(GachaSummonPlan plan, Action onCompleted)
        {
            ResetVisuals();

            LastPlan = plan;

            if (plan == null || !isActiveAndEnabled)
            {
                onCompleted?.Invoke();
                return;
            }

            running = StartCoroutine(PlayRoutine(plan, onCompleted));
        }

        /// <summary>演出のルーチン。テストからは直接進められます。終わったら <paramref name="onCompleted"/> を一度だけ呼びます。</summary>
        public IEnumerator PlayRoutine(GachaSummonPlan plan, Action onCompleted)
        {
            int current = ++token;

            completed = onCompleted;

            if (plan == null)
            {
                yield break;
            }

            IsPlaying = true;

            if (label != null)
            {
                label.text = HomeText.Summoning;
            }

            float elapsed = 0f;

            while (true)
            {
                if (current != token)
                {
                    yield break;
                }

                GachaSummonPhase phase = plan.PhaseAt(elapsed);

                if (phase == GachaSummonPhase.Done)
                {
                    break;
                }

                if (graphic != null && plan.PlaysGraphics)
                {
                    graphic.Show(phase, plan.PhaseProgress(elapsed), plan.FlashAlpha(elapsed));
                }

                if (labelGroup != null)
                {
                    float p = elapsed / plan.Duration;
                    labelGroup.alpha = Mathf.Clamp01(p / 0.2f) * (1f - Mathf.Clamp01((p - 0.8f) / 0.2f));
                }

                yield return null;

                elapsed += deltaTimeSource();
            }

            if (current != token)
            {
                yield break;
            }

            Action done = completed;

            Clear();
            running = null;
            completed = null;

            done?.Invoke();
        }

        /// <summary>図形・文字・状態を戻し、再生中のルーチンを止めます。完了は通知しません。何度呼んでも安全です。</summary>
        public void ResetVisuals()
        {
            token++;

            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            completed = null;
            Clear();
        }

        private void Clear()
        {
            IsPlaying = false;

            if (graphic != null)
            {
                graphic.Hide();
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            if (labelGroup != null)
            {
                labelGroup.alpha = 0f;
            }
        }
    }
}
