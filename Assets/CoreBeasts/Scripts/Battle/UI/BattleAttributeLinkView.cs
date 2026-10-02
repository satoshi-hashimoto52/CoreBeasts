using System;
using System.Collections;

using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ATTRIBUTE LINK の演出（Phase 4C）。DEPLOY の直後、既存の移動と同時に約0.30秒だけ出します。
    ///
    /// - PLAYER と CPU で同じ演出で、双方が LINK すれば同時に出します
    /// - 立ち絵のまわりへ属性エネルギー（<see cref="AttributeLinkGraphic"/>）と
    ///   「ATTRIBUTE LINK x2  POWER +3」の文字を出します
    /// - FX OFF では図形を出さず、同じ時間だけ短い文字を出します
    ///
    /// 立ち絵の位置は読むだけで動かしません（Phase 1 の PortraitTransform を奪いません）。
    /// 描くのは FxImpact（RectMask2D 付きの戦闘表示領域）の中だけで、HUD は揺らしません。
    /// 進行と勝敗には触れません。ラウンドの処理はこの演出を待ちません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleAttributeLinkView : MonoBehaviour
    {
        [SerializeField] private AttributeLinkGraphic graphic;

        [Header("Labels")]
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private CanvasGroup playerLabelGroup;
        [SerializeField] private TMP_Text cpuLabel;
        [SerializeField] private CanvasGroup cpuLabelGroup;

        [Header("Portraits (read only)")]
        [SerializeField] private RectTransform playerPortrait;
        [SerializeField] private RectTransform cpuPortrait;

        [Tooltip("文字を立ち絵の中心から戦闘領域の中央側へ寄せる量（半径に対する割合）。")]
        [SerializeField] [Range(0f, 1.2f)] private float labelOffset = 0.75f;

        /// <summary>共有した属性を並べる順（色を決めつけず、共有した色をすべて使います）。</summary>
        private static readonly UnitAttribute[] ColourOrder = { UnitAttribute.Red, UnitAttribute.Green, UnitAttribute.Blue };

        private AttributePalette palette;
        private IBattleTextSource text;

        private int playToken;
        private Coroutine running;

        private bool hasLabelRest;
        private Vector2 playerLabelRest;
        private Vector2 cpuLabelRest;

        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>直近に再生した演出の設計。</summary>
        public BattleAttributeLinkPresentationPlan LastPlan { get; private set; }

        /// <summary>描いている Graphic。</summary>
        public AttributeLinkGraphic Graphic => graphic;

        /// <summary>プレイヤー側の文字。</summary>
        public TMP_Text PlayerLabel => playerLabel;

        /// <summary>CPU側の文字。</summary>
        public TMP_Text CpuLabel => cpuLabel;

        /// <summary>プレイヤー側の文字の不透明度。</summary>
        public float PlayerLabelAlpha => playerLabelGroup != null ? playerLabelGroup.alpha : 0f;

        /// <summary>CPU側の文字の不透明度。</summary>
        public float CpuLabelAlpha => cpuLabelGroup != null ? cpuLabelGroup.alpha : 0f;

        /// <summary>表示に使う配色と文言を渡します。</summary>
        public void Bind(AttributePalette attributePalette, IBattleTextSource battleText)
        {
            palette = attributePalette;
            text = battleText;
        }

        private void Awake()
        {
            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        /// <summary>
        /// 演出を始めます（待ちません）。何も出さない計画や、無効な状態なら何もしません。
        /// 再生中なら前の演出を止めてから始めます。
        /// </summary>
        public void Play(BattleAttributeLinkPresentationPlan plan)
        {
            ResetVisuals();

            LastPlan = plan;

            if (plan == null || !plan.ShowsAny || !isActiveAndEnabled)
            {
                return;
            }

            running = StartCoroutine(PlayRoutine(plan));
        }

        /// <summary>
        /// 演出のルーチン。テストからは直接進められます（<see cref="Play"/>は同じものを Coroutine で回します）。
        /// </summary>
        public IEnumerator PlayRoutine(BattleAttributeLinkPresentationPlan plan)
        {
            int token = ++playToken;

            if (plan == null || !plan.ShowsAny)
            {
                yield break;
            }

            CacheLabelRest();

            IsPlaying = true;

            float radiusPlayer = RadiusOf(playerPortrait);
            float radiusCpu = RadiusOf(cpuPortrait);

            SetLabel(playerLabel, playerLabelGroup, plan.ShowsPlayer ? plan.Player : AttributeLinkResult.None, playerPortrait, radiusPlayer, playerLabelRest, 1f);
            SetLabel(cpuLabel, cpuLabelGroup, plan.ShowsCpu ? plan.Cpu : AttributeLinkResult.None, cpuPortrait, radiusCpu, cpuLabelRest, -1f);

            if (plan.PlaysGraphics && graphic != null)
            {
                ColoursOf(plan.Player, out Color p0, out Color p1, out bool pDual);
                ColoursOf(plan.Cpu, out Color c0, out Color c1, out bool cDual);

                graphic.Begin(
                    plan.ShowsPlayer && playerPortrait != null, CentreOf(playerPortrait), radiusPlayer, p0, p1, pDual,
                    plan.ShowsCpu && cpuPortrait != null, CentreOf(cpuPortrait), radiusCpu, c0, c1, cDual);
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
                nameof(BattleAttributeLinkView),
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
        /// 文字を立ち絵の中心から、戦闘領域の中央側（PLAYER は上、CPU は下）へ寄せて置きます。
        /// 文字の RectTransform だけを動かし、立ち絵は動かしません。
        /// </summary>
        private void SetLabel(TMP_Text label, CanvasGroup group, AttributeLinkResult link, RectTransform portrait, float radius, Vector2 rest, float towardCentre)
        {
            if (label == null)
            {
                return;
            }

            if (!link.IsActive || text == null)
            {
                label.text = string.Empty;
                label.rectTransform.anchoredPosition = rest;
                SetAlpha(group, 0f);
                return;
            }

            label.text = text.FormatLinkCue(link.ChainCount, link.BonusPower);

            if (portrait != null)
            {
                RectTransform parent = label.rectTransform.parent as RectTransform;

                if (parent != null)
                {
                    Vector3 local = parent.InverseTransformPoint(CentreOf(portrait));
                    label.rectTransform.anchoredPosition =
                        new Vector2(local.x, local.y + towardCentre * radius * labelOffset) -
                        PivotShift(label.rectTransform, parent);
                }
            }

            SetAlpha(group, 0f);
        }

        /// <summary>anchoredPosition はアンカー基準なので、親中心とのずれを差し引きます。</summary>
        private static Vector2 PivotShift(RectTransform rect, RectTransform parent)
        {
            Vector2 anchor = rect.anchorMin + Vector2.Scale(rect.anchorMax - rect.anchorMin, rect.pivot);
            Rect p = parent.rect;

            return new Vector2(p.xMin + p.width * anchor.x, p.yMin + p.height * anchor.y);
        }

        private void ColoursOf(AttributeLinkResult link, out Color first, out Color second, out bool dual)
        {
            first = Color.white;
            second = Color.white;
            dual = false;

            int found = 0;

            for (int i = 0; i < ColourOrder.Length; i++)
            {
                if (!link.Shares(ColourOrder[i]))
                {
                    continue;
                }

                Color c = palette != null ? palette.GetColors(ColourOrder[i]).PrimaryColor : Color.white;

                if (found == 0)
                {
                    first = c;
                    second = c;
                }
                else
                {
                    second = c;
                    dual = true;
                }

                found++;
            }
        }

        private static Vector3 CentreOf(RectTransform rect)
        {
            return rect != null ? rect.TransformPoint(rect.rect.center) : Vector3.zero;
        }

        /// <summary>立ち絵の小さい辺の半分（この Graphic の単位）。上限は計画の最大半径です。</summary>
        private float RadiusOf(RectTransform portrait)
        {
            if (portrait == null || graphic == null)
            {
                return 0f;
            }

            Rect r = portrait.rect;
            Vector3 centre = portrait.TransformPoint(r.center);
            Vector3 edge = portrait.TransformPoint(r.center + new Vector2(Mathf.Min(r.width, r.height) * 0.5f, 0f));

            float local = (graphic.rectTransform.InverseTransformPoint(edge) - graphic.rectTransform.InverseTransformPoint(centre)).magnitude;

            return Mathf.Min(local, BattleAttributeLinkPresentationPlan.MaxRadius);
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
