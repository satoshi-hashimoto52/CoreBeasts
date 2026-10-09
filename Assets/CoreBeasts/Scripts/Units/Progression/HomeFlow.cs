using System;

using CoreBeasts.Units;

namespace CoreBeasts.Progression
{
    /// <summary>Home シーン上の画面（Phase 7）。主要な画面は常に1つだけが表示されます。</summary>
    public enum HomePage
    {
        Home = 0,
        Reward = 1,
        Gacha = 2,
        Summoning = 3,
        Acquisition = 4,
        Collection = 5,
    }

    /// <summary>
    /// Home シーンの画面遷移（Phase 7）。表示中の画面と、遷移中の入力の閉じ方だけを持つ純粋な状態です。
    ///
    /// 決まった遷移だけを許します:
    ///   Home → Gacha / Collection、Reward → Gacha / Home、Gacha → Summoning / Home、
    ///   Summoning → Acquisition（演出の完了時だけ）、Acquisition → Collection / Home、Collection → Home。
    /// 演出中（Summoning）は入力を閉じ、Acquisition へ移るまで他の操作を受け付けません。
    /// </summary>
    public sealed class HomeFlow
    {
        public HomeFlow(HomePage initial = HomePage.Home)
        {
            Current = initial;
        }

        /// <summary>いま表示している画面。</summary>
        public HomePage Current { get; private set; }

        /// <summary>入力を閉じているか（ガチャ演出の間）。</summary>
        public bool IsInputLocked => Current == HomePage.Summoning;

        /// <summary>画面が変わったとき（前, 後）。</summary>
        public event Action<HomePage, HomePage> Changed;

        /// <summary>利用者の操作による遷移。許されない遷移や入力を閉じている間は何もしません。</summary>
        public bool TryGo(HomePage next)
        {
            if (IsInputLocked || !IsAllowed(Current, next) || next == HomePage.Acquisition)
            {
                return false;
            }

            Set(next);
            return true;
        }

        /// <summary>ガチャ演出の完了による Acquisition への遷移。演出中のときだけ通ります。</summary>
        public bool CompleteSummon()
        {
            if (Current != HomePage.Summoning)
            {
                return false;
            }

            Set(HomePage.Acquisition);
            return true;
        }

        /// <summary>中断（OnDisable・HOME・シーン遷移）。演出中なら入力を開け、Home へ戻します。</summary>
        public void Abort()
        {
            if (Current == HomePage.Summoning)
            {
                Set(HomePage.Home);
            }
        }

        /// <summary>
        /// シーンを読み込んだときの最初の画面を置きます。Home か Reward だけを受け付けます
        /// （Acquisition などを再読み込みで勝手に再表示しないため）。
        /// </summary>
        public bool Begin(HomePage page)
        {
            if (page != HomePage.Home && page != HomePage.Reward)
            {
                return false;
            }

            if (Current != page)
            {
                Set(page);
            }

            return true;
        }

        /// <summary>シーンを読み込んだときの最初の画面。未確認の報酬があるときだけ Reward です。</summary>
        public static HomePage InitialPage(bool hasPendingReward)
        {
            return hasPendingReward ? HomePage.Reward : HomePage.Home;
        }

        public static bool IsAllowed(HomePage from, HomePage to)
        {
            if (from == to)
            {
                return false;
            }

            switch (from)
            {
                case HomePage.Home:
                    return to == HomePage.Gacha || to == HomePage.Collection;
                case HomePage.Reward:
                    return to == HomePage.Gacha || to == HomePage.Home;
                case HomePage.Gacha:
                    return to == HomePage.Summoning || to == HomePage.Home;
                case HomePage.Summoning:
                    return to == HomePage.Acquisition;
                case HomePage.Acquisition:
                    return to == HomePage.Collection || to == HomePage.Home;
                case HomePage.Collection:
                    return to == HomePage.Home;
                default:
                    return false;
            }
        }

        private void Set(HomePage next)
        {
            HomePage previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
        }
    }

    /// <summary>
    /// ガチャの1回ぶん（Phase 7）。結果は開始時に一度だけ確定・保存し、演出の途中で変えません。
    /// 演出が終わるまで次の開始を受け付けないので、ボタンを連打しても1回ぶんしか消費しません。
    /// </summary>
    public sealed class GachaSummonSession
    {
        private readonly GachaService service;
        private readonly Action save;

        public GachaSummonSession(GachaService service, Action save)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.save = save;
        }

        /// <summary>演出中か（結果は確定済み）。</summary>
        public bool IsBusy { get; private set; }

        /// <summary>直近に確定した結果。</summary>
        public GachaResult Result { get; private set; }

        /// <summary>
        /// 1回ぶんを開始します。抽選・消費・獲得・保存をここで一度だけ行います。
        /// 演出中・残高不足・カタログが空のときは何もせず false を返します（コインも所持も変えません）。
        /// </summary>
        public bool TryBegin(PlayerProfile profile, CoreBeastRoster catalog, out GachaResult result)
        {
            result = null;

            if (IsBusy)
            {
                return false;
            }

            if (!service.TryPull(profile, catalog, out result))
            {
                result = null;
                return false;
            }

            save?.Invoke();

            Result = result;
            IsBusy = true;
            return true;
        }

        /// <summary>演出の完了（または中断）。結果はそのまま残し、次の開始を受け付けます。</summary>
        public void Finish()
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// ガチャ演出の設計（Phase 7）。Unity に依存しない純データです。
    ///
    /// FX ON: コア待機 → 圧縮 → リング展開 → 白金フラッシュ（合計 <see cref="FxDuration"/> 秒）。
    /// FX OFF: 短い文字とフェードだけ（<see cref="PlainDuration"/> 秒）。
    /// 結果は演出の前に確定済みで、演出は見た目だけです。
    /// </summary>
    public sealed class GachaSummonPlan
    {
        public const float IdleSeconds = 0.25f;
        public const float CompressSeconds = 0.25f;
        public const float RingSeconds = 0.40f;
        public const float FlashSeconds = 0.30f;

        /// <summary>FX ON の合計（1.2秒）。</summary>
        public const float FxDuration = IdleSeconds + CompressSeconds + RingSeconds + FlashSeconds;

        /// <summary>FX OFF の合計。</summary>
        public const float PlainDuration = 0.45f;

        /// <summary>フラッシュの最大不透明度。画面全体を真っ白にしません。</summary>
        public const float MaxFlashAlpha = 0.8f;

        /// <summary>演出の Graphic が1フレームで超えない頂点数。</summary>
        public const int VertexBudget = 240;

        private GachaSummonPlan(bool fxEnabled)
        {
            FxEnabled = fxEnabled;
        }

        public bool FxEnabled { get; }

        /// <summary>演出の長さ（秒）。</summary>
        public float Duration => FxEnabled ? FxDuration : PlainDuration;

        /// <summary>図形を出すか。</summary>
        public bool PlaysGraphics => FxEnabled;

        public static GachaSummonPlan Create(bool fxEnabled)
        {
            return new GachaSummonPlan(fxEnabled);
        }

        /// <summary>経過時間から段階を求めます（FX OFF は文字だけの1段階）。</summary>
        public GachaSummonPhase PhaseAt(float elapsed)
        {
            if (elapsed >= Duration)
            {
                return GachaSummonPhase.Done;
            }

            if (!FxEnabled)
            {
                return GachaSummonPhase.Plain;
            }

            if (elapsed < IdleSeconds)
            {
                return GachaSummonPhase.Idle;
            }

            if (elapsed < IdleSeconds + CompressSeconds)
            {
                return GachaSummonPhase.Compress;
            }

            if (elapsed < IdleSeconds + CompressSeconds + RingSeconds)
            {
                return GachaSummonPhase.Ring;
            }

            return GachaSummonPhase.Flash;
        }

        /// <summary>段階の中での進み具合（0〜1）。</summary>
        public float PhaseProgress(float elapsed)
        {
            switch (PhaseAt(elapsed))
            {
                case GachaSummonPhase.Idle:
                    return Clamp01(elapsed / IdleSeconds);
                case GachaSummonPhase.Compress:
                    return Clamp01((elapsed - IdleSeconds) / CompressSeconds);
                case GachaSummonPhase.Ring:
                    return Clamp01((elapsed - IdleSeconds - CompressSeconds) / RingSeconds);
                case GachaSummonPhase.Flash:
                    return Clamp01((elapsed - IdleSeconds - CompressSeconds - RingSeconds) / FlashSeconds);
                case GachaSummonPhase.Plain:
                    return Clamp01(elapsed / PlainDuration);
                default:
                    return 1f;
            }
        }

        /// <summary>白金フラッシュの不透明度（最大 <see cref="MaxFlashAlpha"/>）。</summary>
        public float FlashAlpha(float elapsed)
        {
            if (!FxEnabled || PhaseAt(elapsed) != GachaSummonPhase.Flash)
            {
                return 0f;
            }

            float p = PhaseProgress(elapsed);

            // 立ち上がりを速く、消えるのをゆっくりにします。
            float shape = p < 0.3f ? p / 0.3f : 1f - (p - 0.3f) / 0.7f;

            return MaxFlashAlpha * Clamp01(shape);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }

    /// <summary>ガチャ演出の段階。</summary>
    public enum GachaSummonPhase
    {
        Idle = 0,
        Compress = 1,
        Ring = 2,
        Flash = 3,
        Plain = 4,
        Done = 5,
    }

    /// <summary>
    /// 画面演出の設定（Phase 7）。保存キーを増やさないため、アプリを起動している間だけ保ちます。
    /// </summary>
    public static class PresentationSettings
    {
        public static bool FxEnabled { get; set; } = true;
    }
}
