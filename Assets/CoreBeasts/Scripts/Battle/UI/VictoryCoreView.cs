using System;
using System.Collections;

using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 勝利コア獲得（Phase 3）。ラウンド勝者側で新しく点灯したピップ1個の上へ、
    /// 白金色の点灯 → 軽い拡大 → 定位置への収束を重ねて描きます。
    ///
    /// ピップ自体（<see cref="BattleScorePipsView"/>の Image）は動かさず、色も変えません。
    /// 描くのはヘッダーを覆う専用 RectTransform 内の<see cref="CoreBreakGraphic"/>1枚だけです。
    /// 勝敗は決めず、<see cref="BattleMatchPresentationPlan"/>が選んだピップを光らせるだけです。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VictoryCoreView : MonoBehaviour
    {
        [SerializeField] private BattleScorePipsView scorePips;
        [SerializeField] private CoreBreakGraphic graphic;

        private int playToken;

        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>直近に光らせた側。</summary>
        public RoundWinner LastSide { get; private set; } = RoundWinner.Draw;

        /// <summary>直近に光らせたピップ番号（0始まり）。未再生なら -1。</summary>
        public int LastPipIndex { get; private set; } = -1;

        /// <summary>直近に光らせたピップ。</summary>
        public RectTransform LastPip { get; private set; }

        /// <summary>描いている Graphic。</summary>
        public CoreBreakGraphic Graphic => graphic;

        private void Awake()
        {
            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        /// <summary>
        /// 新しく点灯したピップを光らせます。対象が無ければ何もせず終わります。
        ///
        /// <paramref name="onConverged"/>は、光がピップの定位置へ収束しきったフレームで1回だけ呼びます。
        /// 画面はこの瞬間に表示スコアを更新します。中断されたときは呼びません。
        /// </summary>
        public IEnumerator PlayRoutine(RoundWinner side, int pipIndex, float duration, Action onConverged = null)
        {
            int token = ++playToken;

            RectTransform pip = scorePips != null ? scorePips.PipTransform(side, pipIndex) : null;

            LastSide = side;
            LastPipIndex = pipIndex;
            LastPip = pip;

            if (pip == null || graphic == null || duration <= 0f)
            {
                yield break;
            }

            Rect rect = pip.rect;
            Vector3 centre = pip.TransformPoint(rect.center);
            Vector3 edge = pip.TransformPoint(rect.center + new Vector2(Mathf.Min(rect.width, rect.height) * 0.5f, 0f));

            IsPlaying = true;
            graphic.Begin(CoreBreakGraphic.Mode.VictoryCore, centre, edge);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (token != playToken)
                {
                    yield break;
                }

                elapsed += deltaTimeSource();
                graphic.SetProgress(elapsed / duration);

                // 収束しきったフレームでは待たずに抜け、同じフレームで知らせます。
                if (elapsed >= duration)
                {
                    break;
                }

                yield return null;
            }

            if (token == playToken)
            {
                graphic.Hide();
                IsPlaying = false;

                onConverged?.Invoke();
            }
        }

        /// <summary>表示を消し、再生中のルーチンを止めます。何度呼んでも安全です。</summary>
        public void ResetVisuals()
        {
            playToken++;
            IsPlaying = false;

            if (graphic != null)
            {
                graphic.Hide();
            }
        }

        /// <summary>未設定のSerializeFieldがあれば報告します。</summary>
        public bool HasRequiredReferences()
        {
            return CoreBeasts.Units.ReferenceCheck.Validate(
                this,
                nameof(VictoryCoreView),
                CoreBeasts.Units.ReferenceCheck.Of(nameof(scorePips), scorePips),
                CoreBeasts.Units.ReferenceCheck.Of(nameof(graphic), graphic));
        }
    }
}
