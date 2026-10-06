namespace CoreBeasts.Battle
{
    /// <summary>
    /// ユニークスキル発動の演出設計（Phase 5）。Unity に依存しない純データです。
    ///
    /// Phase 1〜4 の演出設計（<see cref="BattleRoundPresentationPlan"/>・<see cref="BattleAttributeLinkPresentationPlan"/> など）
    /// とは独立したクラスで、それらの時間・距離・振動・色・頂点数には触れません。
    ///
    /// 時系列: DEPLOY の直後（双方の選出が確定し、ラウンドが解決した後）、既存の移動（0.32秒）と同時に始まり、
    /// 約0.30秒で終わります。接触（Impact）より前に終わり、ラウンド全体の長さは増やしません。
    /// FX OFF でも同じ時間だけ文字を出し、図形は出しません。発動の有無と数値は FX と無関係です。
    /// </summary>
    public sealed class BattleSkillPresentationPlan
    {
        /// <summary>スキル演出の時間（秒）。既存の移動 0.32秒に収まります。</summary>
        public const float CueDuration = 0.30f;

        /// <summary>FX OFF で文字だけを出す時間（秒）。</summary>
        public const float TextOnlyDuration = 0.30f;

        /// <summary>スキル演出の Graphic が1フレームで超えない頂点数（双方の合計）。</summary>
        public const int VertexBudget = 96;

        /// <summary>文字の枠から外へ広がってよい最大距離（参照解像度上のpx）。</summary>
        public const float MaxFrameSpread = 12f;

        /// <summary>スキル演出は何も揺らしません。</summary>
        public const float ShakeAmplitude = 0f;

        private BattleSkillPresentationPlan(UniqueSkillActivation player, UniqueSkillActivation cpu, bool fxEnabled)
        {
            Player = player;
            Cpu = cpu;
            FxEnabled = fxEnabled;
        }

        /// <summary>プレイヤー側のスキル。</summary>
        public UniqueSkillActivation Player { get; }

        /// <summary>CPU側のスキル。</summary>
        public UniqueSkillActivation Cpu { get; }

        /// <summary>FX を再生するか。</summary>
        public bool FxEnabled { get; }

        /// <summary>プレイヤー側を出すか（発動したときだけ）。</summary>
        public bool ShowsPlayer => Player.Activated;

        /// <summary>CPU側を出すか（発動したときだけ）。双方が発動すれば同時に出します。</summary>
        public bool ShowsCpu => Cpu.Activated;

        /// <summary>どちらかを出すか。</summary>
        public bool ShowsAny => ShowsPlayer || ShowsCpu;

        /// <summary>枠の図形を出すか。FX OFF では出しません。</summary>
        public bool PlaysGraphics => FxEnabled && ShowsAny;

        /// <summary>演出の時間（秒）。何も出さないときは0です。</summary>
        public float Duration => !ShowsAny ? 0f : FxEnabled ? CueDuration : TextOnlyDuration;

        /// <summary>スキルの結果から演出を決めます。</summary>
        public static BattleSkillPresentationPlan Create(UniqueSkillActivation player, UniqueSkillActivation cpu, bool fxEnabled)
        {
            return new BattleSkillPresentationPlan(player, cpu, fxEnabled);
        }
    }
}
