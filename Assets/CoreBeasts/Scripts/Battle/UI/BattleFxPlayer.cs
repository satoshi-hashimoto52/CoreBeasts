using System;
using System.Collections;

using CoreBeasts.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 最小限の演出。既存のUnity機能（Coroutine / RectTransform / CanvasGroup）だけで作り、
    /// 画像も外部ライブラリも追加しません。
    ///
    /// 進行管理は持ちません。勝敗はすでに確定した値として受け取り、見た目を動かすだけです。
    /// 将来、本格的な演出へ差し替えるときも、ここだけを入れ替えれば済みます。
    ///
    /// 時間配分は<see cref="BattleRoundPresentationPlan"/>が決めます。
    /// lift → 加速しながら approach → 接触位置でヒットストップ → フラッシュ →
    /// 敗者だけノックバック（勝者は小さく反動）→ 着地 → 初期位置へ戻る、の順です。
    ///
    /// 振動は2体の立ち絵だけへ同じ加算オフセットとして掛けます。
    /// SafeArea・Canvas・ヘッダー・スコア・ボタンは動かしません。
    /// 立ち絵の位置は毎フレーム「基準位置＋演出オフセット＋振動オフセット」で
    /// 書き直すため、何ラウンド続けても誤差が積み上がりません。
    ///
    /// Phase 2 の決着エフェクト（<see cref="ImpactBurstGraphic"/>）は、ヒットストップ明けの
    /// フラッシュと同時に衝突点から始まり、ノックバックと着地のあいだに終わります。
    /// 衝突点は2体の立ち絵の RectTransform から実行時に求めます。
    /// エフェクトは立ち絵とは別の RectTransform だけを更新するため、Phase 1 の位置計算と競合しません。
    ///
    /// FX OFF のときは動きと待ちを省き、最終状態だけを即座に作ります。決着エフェクトも出しません。
    /// どちらの設定でもゲーム結果は変わりません（この型はセッションへ触れません）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleFxPlayer : MonoBehaviour
    {
        /// <summary>接触演出のどの段にいるか。確認とテスト用で、進行には使いません。</summary>
        public enum ClashStep
        {
            Idle = 0,
            Lift = 1,
            Approach = 2,
            ImpactHold = 3,
            Release = 4,
            Return = 5,
        }

        /// <summary>
        /// 接触フラッシュの最大不透明度。従来の 1.0 から 0.8 へ下げ、
        /// 直後に出る決着エフェクト（<see cref="ImpactBurstGraphic"/>）の形を覆わないようにします。
        /// 時間（flashSeconds）と色分岐は変えません。
        /// </summary>
        public const float FlashPeakAlpha = 0.8f;

        [Header("Targets")]
        [SerializeField] private RectTransform playerPortrait;
        [SerializeField] private RectTransform cpuPortrait;
        [SerializeField] private CanvasGroup playerGroup;
        [SerializeField] private CanvasGroup cpuGroup;

        [Header("Flash")]
        [Tooltip("接触時の短いフラッシュ。白または属性色で光らせます。")]
        [SerializeField] private Image flashImage;
        [SerializeField] private CanvasGroup flashGroup;

        [Header("Impact")]
        [Tooltip("決着エフェクト。戦闘表示領域だけを覆う専用RectTransformの中に1つだけ置きます。")]
        [SerializeField] private ImpactBurstGraphic impactBurst;

        [Header("Motion")]
        [Tooltip("選出直後に立ち絵を少し浮かせる量。")]
        [SerializeField] [Range(0f, 60f)] private float liftOffset = 14f;

        [Tooltip("接触へ向けてプレイヤー側が動く量。")]
        [SerializeField] private Vector2 playerApproach = new Vector2(0f, 92f);

        [Tooltip("接触へ向けてCPU側が動く量。")]
        [SerializeField] private Vector2 cpuApproach = new Vector2(0f, -92f);

        [Tooltip("勝者を少し大きくする倍率。")]
        [SerializeField] [Range(1f, 1.4f)] private float winnerScale = 1.12f;

        [Tooltip("引き分けで双方を少し大きくする倍率。")]
        [SerializeField] [Range(1f, 1.4f)] private float drawScale = 1.04f;

        [Tooltip("敗者を一瞬暗くする不透明度。")]
        [SerializeField] [Range(0.2f, 1f)] private float loserAlpha = 0.5f;

        [Header("Durations (seconds)")]
        [Tooltip("接触フラッシュと結果バナーのフェード時間。")]
        [SerializeField] [Range(0f, 0.4f)] private float flashSeconds = 0.12f;

        [Tooltip("FX ON のときの結果バナー表示時間。")]
        [SerializeField] [Range(0.1f, 1.2f)] private float fxBannerSeconds = 0.35f;

        [Tooltip("FX OFF のときの結果バナー表示時間。動かさず、ほぼ即時に切り替えます。")]
        [SerializeField] [Range(0f, 0.6f)] private float plainBannerSeconds = 0.2f;

        private Vector2 playerRest;
        private Vector2 cpuRest;
        private Vector3 playerRestScale = Vector3.one;
        private Vector3 cpuRestScale = Vector3.one;
        private float playerRestAlpha = 1f;
        private float cpuRestAlpha = 1f;
        private bool hasRestPose;

        /// <summary>
        /// 再生中の演出を識別する番号。<see cref="ResetVisuals"/>や新しい再生で進み、
        /// 古いルーチンはそこで書き込みをやめます。二重再生で位置を奪い合いません。
        /// </summary>
        private int playToken;

        private Vector2 shakeOffset;

        /// <summary>
        /// 経過時間の取得元。既定は Time.unscaledDeltaTime で、Time.timeScale の影響を受けません。
        /// テストではフレーム時間を固定して、決まった手数で最後まで進められます。
        /// </summary>
        private Func<float> deltaTimeSource = () => Time.unscaledDeltaTime;

        /// <summary>演出を再生するか。既定はONです。</summary>
        public bool FxEnabled { get; set; } = true;

        /// <summary>振動の乱数seed。テストでは固定値を注入できます。</summary>
        public int ShakeSeed { get; set; } = BattleRoundPresentationPlan.DefaultShakeSeed;

        /// <summary>接触演出を再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>今どの段を再生しているか。再生していなければ<see cref="ClashStep.Idle"/>です。</summary>
        public ClashStep Step { get; private set; }

        /// <summary>今フレームで立ち絵へ足している振動オフセット。</summary>
        public Vector2 ShakeOffset => shakeOffset;

        /// <summary>直近の再生で使った演出設計。未再生なら null です。</summary>
        public BattleRoundPresentationPlan LastPlan { get; private set; }

        /// <summary>振動させる対象。2体の立ち絵だけです。</summary>
        public RectTransform PlayerShakeTarget => playerPortrait;

        /// <summary>振動させる対象。2体の立ち絵だけです。</summary>
        public RectTransform CpuShakeTarget => cpuPortrait;

        /// <summary>決着エフェクトを描くGraphic。</summary>
        public ImpactBurstGraphic ImpactBurst => impactBurst;

        /// <summary>直近の接触で求めた衝突点（ワールド座標）。</summary>
        public Vector3 LastContactWorld { get; private set; }

        /// <summary>結果バナーを出しておく時間。FX設定で変わります。</summary>
        public float BannerHoldSeconds =>
            FxEnabled ? fxBannerSeconds : plainBannerSeconds;

        private void Awake()
        {
            CacheRestPose();
            ResetVisuals();
        }

        private void OnDisable()
        {
            // シーン離脱や非表示化で、動かしかけの見た目を残しません。
            ResetVisuals();
        }

        /// <summary>
        /// 1ラウンドぶんの接触演出。勝敗はすでに確定した値として受け取ります。
        /// 決着理由を渡さないため、決着エフェクトは出しません（Phase 1 と同じ見た目です）。
        /// FX OFF のときは1フレームも待たず、最終状態だけを作って終わります。
        /// </summary>
        public IEnumerator PlayClashRoutine(RoundWinner winner, Color flashColor)
        {
            return PlayClashRoutine(BattleRoundPresentationPlan.Create(winner, ShakeSeed), flashColor);
        }

        /// <summary>
        /// 決着理由と勝因の属性まで含めた接触演出。
        /// 勝敗・決着理由は確定済みの値を写すだけで、ここで計算し直しません。
        /// </summary>
        public IEnumerator PlayClashRoutine(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute,
            Color flashColor)
        {
            return PlayClashRoutine(
                BattleRoundPresentationPlan.Create(winner, decision, decidingAttribute, FxEnabled, ShakeSeed),
                flashColor);
        }

        private IEnumerator PlayClashRoutine(BattleRoundPresentationPlan plan, Color flashColor)
        {
            int token = ++playToken;
            RoundWinner winner = plan.Winner;

            CacheRestPose();
            HideImpact();

            LastPlan = plan;

            if (!FxEnabled)
            {
                IsPlaying = false;
                Step = ClashStep.Idle;
                ApplyVerdictInstantly(winner);
                yield break;
            }

            IsPlaying = true;
            ApplyRestPose();

            Vector2 playerForward = DirectionOf(playerApproach, Vector2.up);
            Vector2 cpuForward = DirectionOf(cpuApproach, Vector2.down);

            Vector2 playerLift = playerRest + new Vector2(0f, liftOffset);
            Vector2 cpuLift = cpuRest + new Vector2(0f, -liftOffset);

            Vector2 playerContact = playerLift + playerApproach;
            Vector2 cpuContact = cpuLift + cpuApproach;

            Vector2 playerKnock = playerContact - playerForward * plan.PlayerDisplacement;
            Vector2 cpuKnock = cpuContact - cpuForward * plan.CpuDisplacement;

            Vector2 playerLand =
                playerContact - playerForward * plan.LandedDisplacement(RoundWinner.Player);
            Vector2 cpuLand =
                cpuContact - cpuForward * plan.LandedDisplacement(RoundWinner.Cpu);

            // 1. lift: 少し浮かせて「出る」合図を作ります。
            Step = ClashStep.Lift;

            yield return TimedRoutine(token, plan.LiftDuration, elapsed =>
            {
                float t = SmoothStep(Normalize(elapsed, plan.LiftDuration));

                SetPositions(
                    Vector2.Lerp(playerRest, playerLift, t),
                    Vector2.Lerp(cpuRest, cpuLift, t));
            });

            if (!IsCurrent(token))
            {
                yield break;
            }

            // 2. approach: 加速しながら接触へ。接触の瞬間がいちばん速くなります。
            Step = ClashStep.Approach;

            yield return TimedRoutine(token, plan.ApproachDuration, elapsed =>
            {
                float t = Normalize(elapsed, plan.ApproachDuration);
                float eased = t * t * t;

                SetPositions(
                    Vector2.Lerp(playerLift, playerContact, eased),
                    Vector2.Lerp(cpuLift, cpuContact, eased));
            });

            if (!IsCurrent(token))
            {
                yield break;
            }

            // 3. impact hold: 接触位置で完全に止めます（Time.timeScaleは使いません）。
            Step = ClashStep.ImpactHold;
            SetPositions(playerContact, cpuContact);

            LastContactWorld = ContactPointWorld();

            yield return TimedRoutine(token, plan.ImpactHoldDuration, null);

            if (!IsCurrent(token))
            {
                yield break;
            }

            // 4. フラッシュ → ノックバック → 着地。振動もここから始めます。
            Step = ClashStep.Release;

            if (flashImage != null)
            {
                Color shown = FlashColorFor(plan, flashColor);

                flashImage.color = new Color(shown.r, shown.g, shown.b, 1f);
            }

            SetFlashAlpha(FlashPeakAlpha);

            // 決着エフェクトはフラッシュと同時に衝突点から始めます。
            if (plan.ImpactEnabled && impactBurst != null)
            {
                impactBurst.Begin(plan.ImpactKind, LastContactWorld);
            }

            ResolveVerdict(
                winner,
                out float playerScale,
                out float cpuScale,
                out float playerAlpha,
                out float cpuAlpha);

            yield return TimedRoutine(token, plan.ReleaseDuration, elapsed =>
            {
                float knock = Normalize(elapsed, plan.KnockbackDuration);
                float knockEased = 1f - (1f - knock) * (1f - knock) * (1f - knock);
                float land = SmoothStep(
                    Normalize(elapsed - plan.KnockbackDuration, plan.LandingDuration));

                plan.SampleShake(elapsed, out float shakeX, out float shakeY);
                shakeOffset = new Vector2(shakeX, shakeY);

                SetPositions(
                    Vector2.Lerp(
                        Vector2.Lerp(playerContact, playerKnock, knockEased), playerLand, land),
                    Vector2.Lerp(
                        Vector2.Lerp(cpuContact, cpuKnock, knockEased), cpuLand, land));

                SetScale(playerPortrait, playerRestScale, Mathf.Lerp(1f, playerScale, knockEased));
                SetScale(cpuPortrait, cpuRestScale, Mathf.Lerp(1f, cpuScale, knockEased));
                SetAlpha(playerGroup, Mathf.Lerp(playerRestAlpha, playerAlpha, knockEased));
                SetAlpha(cpuGroup, Mathf.Lerp(cpuRestAlpha, cpuAlpha, knockEased));

                SetFlashAlpha(flashSeconds <= 0f ? 0f : FlashPeakAlpha * (1f - Normalize(elapsed, flashSeconds)));

                if (plan.ImpactEnabled && impactBurst != null)
                {
                    impactBurst.SetProgress(
                        plan.ImpactDuration <= 0f ? 1f : elapsed / plan.ImpactDuration);
                }
            });

            if (!IsCurrent(token))
            {
                yield break;
            }

            shakeOffset = Vector2.zero;
            SetFlashAlpha(0f);
            HideImpact();
            SetPositions(playerLand, cpuLand);

            // 5. return: 初期位置へ。勝敗の大きさと明暗は結果表示まで残します。
            Step = ClashStep.Return;

            yield return TimedRoutine(token, plan.ReturnDuration, elapsed =>
            {
                float t = SmoothStep(Normalize(elapsed, plan.ReturnDuration));

                SetPositions(
                    Vector2.Lerp(playerLand, playerRest, t),
                    Vector2.Lerp(cpuLand, cpuRest, t));
            });

            if (!IsCurrent(token))
            {
                yield break;
            }

            SetPositions(playerRest, cpuRest);
            IsPlaying = false;
            Step = ClashStep.Idle;
        }

        /// <summary>
        /// 結果バナーを出しておくルーチン。
        /// FX ON ではフェードで出し、FX OFF では動かさずに表示だけします。
        /// </summary>
        public IEnumerator ShowBannerRoutine(CanvasGroup banner)
        {
            if (banner == null)
            {
                yield break;
            }

            if (!FxEnabled)
            {
                banner.alpha = 1f;

                yield return WaitRoutine(plainBannerSeconds);

                yield break;
            }

            yield return FadeRoutine(banner, 0f, 1f, flashSeconds);

            yield return WaitRoutine(fxBannerSeconds);

            yield return FadeRoutine(banner, 1f, 0f, flashSeconds);
        }

        /// <summary>
        /// 立ち絵の位置・大きさ・明暗・振動とフラッシュを初期状態へ戻します。
        /// 再生中のルーチンはここで書き込みをやめます。何度呼んでも安全です。
        /// </summary>
        public void ResetVisuals()
        {
            playToken++;
            IsPlaying = false;
            Step = ClashStep.Idle;

            CacheRestPose();
            ApplyRestPose();
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleFxPlayer),
                ReferenceCheck.Of(nameof(playerPortrait), playerPortrait),
                ReferenceCheck.Of(nameof(cpuPortrait), cpuPortrait),
                ReferenceCheck.Of(nameof(playerGroup), playerGroup),
                ReferenceCheck.Of(nameof(cpuGroup), cpuGroup),
                ReferenceCheck.Of(nameof(flashImage), flashImage),
                ReferenceCheck.Of(nameof(flashGroup), flashGroup),
                ReferenceCheck.Of(nameof(impactBurst), impactBurst));
        }

        /// <summary>
        /// 2体の立ち絵の中心（RectTransform.rect の中心）をワールド座標で求め、その中点を返します。
        /// 端末解像度・Canvas の scaleFactor・左右反転はすべて Transform 側で解決されます。
        /// </summary>
        private Vector3 ContactPointWorld()
        {
            if (playerPortrait == null || cpuPortrait == null)
            {
                return transform.position;
            }

            Vector3 player = playerPortrait.TransformPoint(playerPortrait.rect.center);
            Vector3 cpu = cpuPortrait.TransformPoint(cpuPortrait.rect.center);

            return (player + cpu) * 0.5f;
        }

        /// <summary>
        /// POWER・CORE・DRAW の決着では、フラッシュにも属性色（勝者色）を使いません。
        /// 属性勝ちと、決着エフェクトを出さない場合は、受け取った色のままです。
        /// </summary>
        private static Color FlashColorFor(BattleRoundPresentationPlan plan, Color flashColor)
        {
            AttributeEffectProfile profile = plan.ImpactProfile;

            if (!profile.IsVisible || profile.UsesAttributeColor)
            {
                return flashColor;
            }

            AttributeEffectProfile.Unpack(profile.InnerColor, out float r, out float g, out float b);

            return new Color(r, g, b, 1f);
        }

        private void HideImpact()
        {
            if (impactBurst != null)
            {
                impactBurst.Hide();
            }
        }

        /// <summary>FX OFF でも、勝者と敗者の差は最終状態として残します。決着エフェクトは出しません。</summary>
        private void ApplyVerdictInstantly(RoundWinner winner)
        {
            SetFlashAlpha(0f);
            HideImpact();

            shakeOffset = Vector2.zero;
            SetPositions(playerRest, cpuRest);

            ResolveVerdict(
                winner,
                out float playerScale,
                out float cpuScale,
                out float playerAlpha,
                out float cpuAlpha);

            SetScale(playerPortrait, playerRestScale, playerScale);
            SetScale(cpuPortrait, cpuRestScale, cpuScale);
            SetAlpha(playerGroup, playerAlpha);
            SetAlpha(cpuGroup, cpuAlpha);
        }

        private void ResolveVerdict(
            RoundWinner winner,
            out float playerScale,
            out float cpuScale,
            out float playerAlpha,
            out float cpuAlpha)
        {
            switch (winner)
            {
                case RoundWinner.Player:
                    playerScale = winnerScale;
                    cpuScale = 1f;
                    playerAlpha = playerRestAlpha;
                    cpuAlpha = loserAlpha;
                    return;

                case RoundWinner.Cpu:
                    playerScale = 1f;
                    cpuScale = winnerScale;
                    playerAlpha = loserAlpha;
                    cpuAlpha = cpuRestAlpha;
                    return;

                default:
                    playerScale = drawScale;
                    cpuScale = drawScale;
                    playerAlpha = playerRestAlpha;
                    cpuAlpha = cpuRestAlpha;
                    return;
            }
        }

        /// <summary>
        /// <paramref name="seconds"/>秒ぶん、経過時間を<paramref name="apply"/>へ渡します。
        /// 別の再生や<see cref="ResetVisuals"/>で番号が変わったら、その場で書き込みをやめます。
        /// </summary>
        private IEnumerator TimedRoutine(int token, float seconds, Action<float> apply)
        {
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                if (!IsCurrent(token))
                {
                    yield break;
                }

                elapsed += deltaTimeSource();

                apply?.Invoke(Mathf.Min(elapsed, seconds));

                yield return null;
            }

            if (IsCurrent(token))
            {
                apply?.Invoke(seconds);
            }
        }

        private bool IsCurrent(int token)
        {
            return token == playToken;
        }

        private static IEnumerator FadeRoutine(
            CanvasGroup group,
            float from,
            float to,
            float seconds)
        {
            float elapsed = 0f;

            group.alpha = from;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(seconds <= 0f ? 1f : elapsed / seconds);

                group.alpha = Mathf.Lerp(from, to, t);

                yield return null;
            }

            group.alpha = to;
        }

        private static IEnumerator WaitRoutine(float seconds)
        {
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;

                yield return null;
            }
        }

        /// <summary>
        /// 基準姿勢（位置・大きさ・明暗）を一度だけ控えます。
        /// 以後の再生も復元も同じ基準を使うため、連続ラウンドで誤差が積み上がりません。
        /// </summary>
        private void CacheRestPose()
        {
            if (hasRestPose)
            {
                return;
            }

            if (playerPortrait == null || cpuPortrait == null)
            {
                return;
            }

            playerRest = playerPortrait.anchoredPosition;
            cpuRest = cpuPortrait.anchoredPosition;
            playerRestScale = playerPortrait.localScale;
            cpuRestScale = cpuPortrait.localScale;
            playerRestAlpha = playerGroup != null ? playerGroup.alpha : 1f;
            cpuRestAlpha = cpuGroup != null ? cpuGroup.alpha : 1f;
            hasRestPose = true;
        }

        private void ApplyRestPose()
        {
            shakeOffset = Vector2.zero;

            if (hasRestPose)
            {
                SetPositions(playerRest, cpuRest);
            }

            SetScale(playerPortrait, playerRestScale, 1f);
            SetScale(cpuPortrait, cpuRestScale, 1f);
            SetAlpha(playerGroup, playerRestAlpha);
            SetAlpha(cpuGroup, cpuRestAlpha);
            SetFlashAlpha(0f);
            HideImpact();
        }

        /// <summary>演出上の位置へ、今の振動オフセットを足して書き込みます。加算はしません。</summary>
        private void SetPositions(Vector2 player, Vector2 cpu)
        {
            if (playerPortrait != null)
            {
                playerPortrait.anchoredPosition = player + shakeOffset;
            }

            if (cpuPortrait != null)
            {
                cpuPortrait.anchoredPosition = cpu + shakeOffset;
            }
        }

        private void SetFlashAlpha(float alpha)
        {
            if (flashGroup != null)
            {
                flashGroup.alpha = alpha;
            }
        }

        private static float Normalize(float elapsed, float seconds)
        {
            return seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static Vector2 DirectionOf(Vector2 approach, Vector2 fallback)
        {
            return approach.sqrMagnitude > 0f ? approach.normalized : fallback;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        /// <summary>基準の大きさ（左右反転を含む）に倍率を掛けます。反転は崩しません。</summary>
        private static void SetScale(RectTransform target, Vector3 restScale, float scale)
        {
            if (target != null)
            {
                target.localScale = new Vector3(
                    restScale.x * scale,
                    restScale.y * scale,
                    restScale.z);
            }
        }
    }
}
