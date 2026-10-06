using System;
using System.Collections;

using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ユニークスキル発動の演出（Phase 5）。DEPLOY の直後、既存の移動と同時に約0.30秒だけ出します。
    ///
    /// - 双方の選出が確定し、ラウンドが解決した後にだけ呼ばれます（CPU の情報はこの時点で初めて出ます）
    /// - PLAYER と CPU で同じ演出で、双方が発動すれば同時に出します
    /// - 文字は立ち絵の「中央側の縁」の外（PLAYER は立ち絵の上端の上、CPU は下端の下）へ置き、
    ///   立ち絵の中心付近に出る ATTRIBUTE LINK の文字・図形と重ねません
    /// - FX OFF では図形を出さず、同じ時間だけ文字を出します
    ///
    /// 立ち絵の位置は読むだけで動かしません。描くのは戦闘表示領域（RectMask2D）の中だけで、HUD は揺らしません。
    /// 進行と勝敗には触れません。ラウンドの処理はこの演出を待ちません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSkillCueView : MonoBehaviour
    {
        [SerializeField] private UniqueSkillCueGraphic graphic;

        [Header("Labels")]
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private CanvasGroup playerLabelGroup;
        [SerializeField] private TMP_Text cpuLabel;
        [SerializeField] private CanvasGroup cpuLabelGroup;

        [Header("Portraits (read only)")]
        [SerializeField] private RectTransform playerPortrait;
        [SerializeField] private RectTransform cpuPortrait;

        [Tooltip("立ち絵の中央側の縁から、文字の中心までの距離（参照解像度上のpx）。")]
        [SerializeField] [Range(0f, 120f)] private float edgeOffset = 46f;

        [Tooltip("文字の枠の余白（角かっこが文字に重ならないように）。")]
        [SerializeField] [Range(0f, 24f)] private float framePadding = 8f;

        private readonly Vector3[] corners = new Vector3[4];

        private int playToken;
        private Coroutine running;

        private bool hasLabelRest;
        private Vector2 playerLabelRest;
        private Vector2 cpuLabelRest;

        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>直近に再生した演出の設計。</summary>
        public BattleSkillPresentationPlan LastPlan { get; private set; }

        /// <summary>描いている Graphic。</summary>
        public UniqueSkillCueGraphic Graphic => graphic;

        /// <summary>プレイヤー側の文字。</summary>
        public TMP_Text PlayerLabel => playerLabel;

        /// <summary>CPU側の文字。</summary>
        public TMP_Text CpuLabel => cpuLabel;

        /// <summary>プレイヤー側の文字の不透明度。</summary>
        public float PlayerLabelAlpha => playerLabelGroup != null ? playerLabelGroup.alpha : 0f;

        /// <summary>CPU側の文字の不透明度。</summary>
        public float CpuLabelAlpha => cpuLabelGroup != null ? cpuLabelGroup.alpha : 0f;

        private void Awake()
        {
            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        /// <summary>
        /// 演出を始めます（待ちません）。<paramref name="playerText"/> / <paramref name="cpuText"/>は表示する文字
        /// （文言は <see cref="IBattleTextSource.FormatSkillCue"/> で作ります）。発動しない側の文字は使いません。
        /// </summary>
        public void Play(BattleSkillPresentationPlan plan, string playerText, string cpuText)
        {
            ResetVisuals();

            LastPlan = plan;

            if (plan == null || !plan.ShowsAny || !isActiveAndEnabled)
            {
                return;
            }

            running = StartCoroutine(PlayRoutine(plan, playerText, cpuText));
        }

        /// <summary>演出のルーチン。テストからは直接進められます。</summary>
        public IEnumerator PlayRoutine(BattleSkillPresentationPlan plan, string playerText, string cpuText)
        {
            int token = ++playToken;

            if (plan == null || !plan.ShowsAny)
            {
                yield break;
            }

            CacheLabelRest();

            IsPlaying = true;

            Rect playerFrame = SetLabel(playerLabel, playerLabelGroup, plan.ShowsPlayer ? playerText : null, playerPortrait, true, playerLabelRest);
            Rect cpuFrame = SetLabel(cpuLabel, cpuLabelGroup, plan.ShowsCpu ? cpuText : null, cpuPortrait, false, cpuLabelRest);

            if (plan.PlaysGraphics && graphic != null)
            {
                graphic.Begin(plan.ShowsPlayer && playerFrame.width > 0f, playerFrame, plan.ShowsCpu && cpuFrame.width > 0f, cpuFrame);
            }

            float duration = plan.Duration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (token != playToken)
                {
                    yield break;
                }

                elapsed += deltaTimeSource();

                float p = Mathf.Clamp01(elapsed / duration);
                float alpha = plan.FxEnabled
                    ? Mathf.Clamp01(p / 0.15f) * (1f - Mathf.Clamp01((p - 0.75f) / 0.25f))
                    : 1f;

                SetAlpha(playerLabelGroup, plan.ShowsPlayer ? alpha : 0f);
                SetAlpha(cpuLabelGroup, plan.ShowsCpu ? alpha : 0f);

                if (plan.PlaysGraphics && graphic != null)
                {
                    graphic.SetProgress(p);
                }

                if (elapsed >= duration)
                {
                    break;
                }

                yield return null;
            }

            if (token == playToken)
            {
                Clear();
                running = null;
            }
        }

        /// <summary>表示を消し、再生中のルーチンを止めます。何度呼んでも安全です。</summary>
        public void ResetVisuals()
        {
            playToken++;

            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            Clear();
        }

        /// <summary>未設定のSerializeFieldがあれば報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleSkillCueView),
                ReferenceCheck.Of(nameof(graphic), graphic),
                ReferenceCheck.Of(nameof(playerLabel), playerLabel),
                ReferenceCheck.Of(nameof(playerLabelGroup), playerLabelGroup),
                ReferenceCheck.Of(nameof(cpuLabel), cpuLabel),
                ReferenceCheck.Of(nameof(cpuLabelGroup), cpuLabelGroup),
                ReferenceCheck.Of(nameof(playerPortrait), playerPortrait),
                ReferenceCheck.Of(nameof(cpuPortrait), cpuPortrait));
        }

        private void Clear()
        {
            IsPlaying = false;

            if (graphic != null)
            {
                graphic.Hide();
            }

            ClearLabel(playerLabel, playerLabelGroup, playerLabelRest);
            ClearLabel(cpuLabel, cpuLabelGroup, cpuLabelRest);
        }

        private void ClearLabel(TMP_Text label, CanvasGroup group, Vector2 rest)
        {
            if (label != null)
            {
                label.text = string.Empty;

                if (hasLabelRest)
                {
                    label.rectTransform.anchoredPosition = rest;
                }
            }

            SetAlpha(group, 0f);
        }

        private void CacheLabelRest()
        {
            if (hasLabelRest)
            {
                return;
            }

            playerLabelRest = playerLabel != null ? playerLabel.rectTransform.anchoredPosition : Vector2.zero;
            cpuLabelRest = cpuLabel != null ? cpuLabel.rectTransform.anchoredPosition : Vector2.zero;
            hasLabelRest = true;
        }

        /// <summary>
        /// 文字を置き、角かっこの枠（Graphic の座標）を返します。出さない側は空の枠です。
        /// 文字の RectTransform だけを動かし、立ち絵は動かしません。
        /// </summary>
        private Rect SetLabel(TMP_Text label, CanvasGroup group, string value, RectTransform portrait, bool towardTop, Vector2 rest)
        {
            if (label == null)
            {
                return Rect.zero;
            }

            if (string.IsNullOrEmpty(value) || portrait == null)
            {
                label.text = string.Empty;
                label.rectTransform.anchoredPosition = rest;
                SetAlpha(group, 0f);
                return Rect.zero;
            }

            label.text = value;
            SetAlpha(group, 0f);

            RectTransform parent = label.rectTransform.parent as RectTransform;

            if (parent == null)
            {
                return Rect.zero;
            }

            // 立ち絵の中央側の縁（左右反転していても、見えている範囲の上端・下端）。
            portrait.GetWorldCorners(corners);

            float top = Mathf.Max(Mathf.Max(corners[0].y, corners[1].y), Mathf.Max(corners[2].y, corners[3].y));
            float bottom = Mathf.Min(Mathf.Min(corners[0].y, corners[1].y), Mathf.Min(corners[2].y, corners[3].y));
            float centreX = (corners[0].x + corners[2].x) * 0.5f;

            Vector3 edge = parent.InverseTransformPoint(new Vector3(centreX, towardTop ? top : bottom, corners[0].z));
            Vector2 centre = new Vector2(edge.x, edge.y + (towardTop ? edgeOffset : -edgeOffset));

            // 実際に描かれる文字の大きさ（ラベルの枠を上限）。角かっこはこれに余白を足して囲みます。
            Vector2 preferred = label.GetPreferredValues(value);
            Rect bounds = label.rectTransform.rect;
            float width = Mathf.Min(preferred.x, bounds.width) + framePadding * 2f;
            float height = Mathf.Min(preferred.y, bounds.height) + framePadding * 2f;

            // 立ち絵が端に寄っていても、文字と枠が戦闘表示領域（RectMask2D）で切れないよう横だけ内側へ寄せます。
            Rect area = parent.rect;
            float half = width * 0.5f + BattleSkillPresentationPlan.MaxFrameSpread;

            centre.x = area.width > half * 2f
                ? Mathf.Clamp(centre.x, area.xMin + half, area.xMax - half)
                : area.center.x;

            label.rectTransform.anchoredPosition = centre - PivotShift(label.rectTransform, parent);

            if (graphic == null)
            {
                return Rect.zero;
            }

            Vector3 world = label.rectTransform.TransformPoint(bounds.center);
            Vector3 local = graphic.rectTransform.InverseTransformPoint(world);

            return new Rect(local.x - width * 0.5f, local.y - height * 0.5f, width, height);
        }

        /// <summary>anchoredPosition はアンカー基準なので、親の中で揃えたい点とのずれを差し引きます。</summary>
        private static Vector2 PivotShift(RectTransform rect, RectTransform parent)
        {
            Vector2 anchor = rect.anchorMin + Vector2.Scale(rect.anchorMax - rect.anchorMin, rect.pivot);
            Rect p = parent.rect;

            return new Vector2(p.xMin + p.width * anchor.x, p.yMin + p.height * anchor.y);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }
    }
}
