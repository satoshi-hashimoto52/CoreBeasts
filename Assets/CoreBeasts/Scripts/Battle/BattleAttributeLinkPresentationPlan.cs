namespace CoreBeasts.Battle
{
    /// <summary>
    /// ATTRIBUTE LINK の演出設計（Phase 4C）。Unityに依存しない純データです。
    ///
    /// <see cref="BattleRoundPresentationPlan"/>（Phase 1・2）とは独立したクラスで、
    /// Phase 1〜3 の時間・距離・振動・色には触れません。
    ///
    /// 時系列: DEPLOY の直後、既存の移動（0.32秒）と同時に始まり、約0.30秒で終わります。
    /// ラウンド全体の長さは増やさず、LINK のための待ち時間も入れません。
    /// FX OFF でも同じ時間だけ短い文字を出し、図形は出しません。
    /// </summary>
    public sealed class BattleAttributeLinkPresentationPlan
    {
        /// <summary>LINK 演出の時間（秒）。既存の移動 0.32秒に収まります。</summary>
        public const float CueDuration = 0.30f;

        /// <summary>FX OFF で文字だけを出す時間（秒）。移動中に収まり、ラウンド時間を増やしません。</summary>
        public const float TextOnlyDuration = 0.30f;

        /// <summary>立ち絵まわりのエネルギーが中心から離れてよい最大距離（参照解像度上のpx）。</summary>
        public const float MaxRadius = 170f;

        /// <summary>LINK 演出の Graphic が1フレームで超えない頂点数（双方の合計）。</summary>
        public const int VertexBudget = 200;

        /// <summary>LINK 演出は何も揺らしません。</summary>
        public const float ShakeAmplitude = 0f;

        private BattleAttributeLinkPresentationPlan(AttributeLinkResult player, AttributeLinkResult cpu, bool fxEnabled)
        {
            Player = player;
            Cpu = cpu;
            FxEnabled = fxEnabled;
        }

        /// <summary>プレイヤー側の LINK。</summary>
        public AttributeLinkResult Player { get; }

        /// <summary>CPU側の LINK。</summary>
        public AttributeLinkResult Cpu { get; }

        /// <summary>FX を再生するか。</summary>
        public bool FxEnabled { get; }

        /// <summary>プレイヤー側の演出を出すか（LINK 成立時だけ）。</summary>
        public bool ShowsPlayer => Player.IsActive;

        /// <summary>CPU側の演出を出すか（LINK 成立時だけ）。双方が成立すれば同時に出します。</summary>
        public bool ShowsCpu => Cpu.IsActive;

        /// <summary>どちらかの演出を出すか。</summary>
        public bool ShowsAny => ShowsPlayer || ShowsCpu;

        /// <summary>エネルギーの図形を出すか。FX OFF では出しません。</summary>
        public bool PlaysGraphics => FxEnabled && ShowsAny;

        /// <summary>演出の時間（秒）。何も出さないときは0です。</summary>
        public float Duration => !ShowsAny ? 0f : FxEnabled ? CueDuration : TextOnlyDuration;

        /// <summary>LINK の結果から演出を決めます。</summary>
        public static BattleAttributeLinkPresentationPlan Create(
            AttributeLinkResult player, AttributeLinkResult cpu, bool fxEnabled)
        {
            return new BattleAttributeLinkPresentationPlan(player, cpu, fxEnabled);
        }
    }
}
