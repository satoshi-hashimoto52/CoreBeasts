using System;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 1ラウンドぶんの接触演出の設計値（Phase 1「戦闘の手応え」＋ Phase 2「属性別インパクト」）。
    ///
    /// Unityに依存しない純データです。勝敗は確定済みの<see cref="RoundWinner"/>を受け取り、
    /// 「どちらが下がるか」「何秒止めるか」「どれだけ揺らすか」だけを決めます。
    /// ゲーム結果には一切触れません。
    ///
    /// 時系列（FX ON）:
    /// lift → approach（加速）→ impact hold（ヒットストップ）→
    /// knockback（フラッシュと振動はここから始まる）→ landing → return。
    ///
    /// Time.timeScale は使いません。ヒットストップも明示的な待ち時間として表します。
    ///
    /// Phase 2 では、決着理由と勝因の属性から<see cref="ImpactKind"/>を決めます。
    /// 決着エフェクトはフラッシュと同時に始まり、<see cref="ReleaseDuration"/>以内に終わります。
    /// Phase 1 の時間・距離・振動はどの種類でも変えません。
    /// FINAL CORE、CORE BREAK はここへ持ち込みません。
    /// </summary>
    public sealed class BattleRoundPresentationPlan
    {
        /// <summary>ヒットストップの下限（秒）。</summary>
        public const float MinImpactHoldDuration = 0.04f;

        /// <summary>ヒットストップの上限（秒）。</summary>
        public const float MaxImpactHoldDuration = 0.08f;

        /// <summary>ヒットストップの標準値（秒）。</summary>
        public const float StandardImpactHoldDuration = 0.06f;

        /// <summary>振動振幅の上限（参照解像度上のpx）。</summary>
        public const float MaxShakeAmplitude = 6f;

        /// <summary>立ち絵を浮かせる時間（秒）。</summary>
        public const float StandardLiftDuration = 0.10f;

        /// <summary>接触まで加速しながら進む時間（秒）。</summary>
        public const float StandardApproachDuration = 0.22f;

        /// <summary>敗者が押し戻される時間（秒）。</summary>
        public const float StandardKnockbackDuration = 0.20f;

        /// <summary>勝者（引き分けでは双方）が反動から着地する時間（秒）。</summary>
        public const float StandardLandingDuration = 0.14f;

        /// <summary>初期位置へ戻る時間（秒）。</summary>
        public const float StandardReturnDuration = 0.18f;

        /// <summary>振動の時間（秒）。</summary>
        public const float StandardShakeDuration = 0.12f;

        /// <summary>振動の振幅（参照解像度上のpx）。</summary>
        public const float StandardShakeAmplitude = 5f;

        /// <summary>敗者が接触位置から押し戻される距離（px）。</summary>
        public const float StandardKnockbackDistance = 40f;

        /// <summary>勝者が接触位置から小さく跳ね返る距離（px）。</summary>
        public const float StandardWinnerRecoilDistance = 10f;

        /// <summary>引き分けで双方が跳ね返る距離（px）。双方同じ値です。</summary>
        public const float StandardDrawRecoilDistance = 18f;

        /// <summary>
        /// 着地後に残る反動の割合。0なら接触位置へ戻り、1なら跳ね返った位置のままです。
        /// </summary>
        public const float LandingRecoilRemain = 0.5f;

        /// <summary>振動の向きを切り替える頻度（Hz）。フレームレートに依存させないためです。</summary>
        public const float ShakeFrequency = 40f;

        /// <summary>振動の既定seed。テストや再現確認で同じ揺れになります。</summary>
        public const int DefaultShakeSeed = 0x5EED;

        private BattleRoundPresentationPlan(RoundWinner winner, int shakeSeed, BattleImpactKind impactKind)
        {
            Winner = winner;
            ShakeSeed = shakeSeed;
            ImpactKind = impactKind;

            PlayerKnockback = winner == RoundWinner.Cpu;
            CpuKnockback = winner == RoundWinner.Player;

            PlayerDisplacement = DisplacementOf(winner, RoundWinner.Player);
            CpuDisplacement = DisplacementOf(winner, RoundWinner.Cpu);
        }

        /// <summary>勝者。引き分けなら<see cref="RoundWinner.Draw"/>です。</summary>
        public RoundWinner Winner { get; }

        /// <summary>引き分けか。引き分けでは片方だけを敗者扱いしません。</summary>
        public bool IsDraw => Winner == RoundWinner.Draw;

        public float LiftDuration => StandardLiftDuration;

        public float ApproachDuration => StandardApproachDuration;

        /// <summary>接触位置で完全に止める時間。Time.timeScaleではなく待ち時間です。</summary>
        public float ImpactHoldDuration => StandardImpactHoldDuration;

        public float KnockbackDuration => StandardKnockbackDuration;

        public float LandingDuration => StandardLandingDuration;

        public float ReturnDuration => StandardReturnDuration;

        public float ShakeDuration => StandardShakeDuration;

        public float ShakeAmplitude => StandardShakeAmplitude;

        /// <summary>振動の乱数seed。同じseedなら同じ揺れになります。</summary>
        public int ShakeSeed { get; }

        /// <summary>振動するか。Phase 1では常に有効です（FX OFFは再生側で短縮経路へ回ります）。</summary>
        public bool ShakeEnabled => true;

        /// <summary>プレイヤー側が敗者として押し戻されるか。</summary>
        public bool PlayerKnockback { get; }

        /// <summary>CPU側が敗者として押し戻されるか。</summary>
        public bool CpuKnockback { get; }

        /// <summary>どちらかにノックバックがあるか。引き分けでは false です。</summary>
        public bool KnockbackEnabled => PlayerKnockback || CpuKnockback;

        /// <summary>プレイヤー側が接触位置から後ろへ下がる距離（px）。</summary>
        public float PlayerDisplacement { get; }

        /// <summary>CPU側が接触位置から後ろへ下がる距離（px）。</summary>
        public float CpuDisplacement { get; }

        /// <summary>lift から return までの合計（秒）。バナーとフェードは含みません。</summary>
        public float ClashDuration =>
            LiftDuration +
            ApproachDuration +
            ImpactHoldDuration +
            KnockbackDuration +
            LandingDuration +
            ReturnDuration;

        /// <summary>ヒットストップ明けから着地完了までの時間（秒）。</summary>
        public float ReleaseDuration => KnockbackDuration + LandingDuration;

        /// <summary>接触の瞬間に出す決着エフェクトの種類。出さないときは<see cref="BattleImpactKind.None"/>です。</summary>
        public BattleImpactKind ImpactKind { get; }

        /// <summary>決着エフェクトを出すか。</summary>
        public bool ImpactEnabled => ImpactKind != BattleImpactKind.None;

        /// <summary>決着エフェクトの設計値（色・時間・広がり・頂点数）。</summary>
        public AttributeEffectProfile ImpactProfile => AttributeEffectProfile.For(ImpactKind);

        /// <summary>決着エフェクトの表示時間（秒）。出さないときは0です。</summary>
        public float ImpactDuration => ImpactProfile.Duration;

        /// <summary>標準の演出設計を作ります。決着理由を渡さないため、決着エフェクトは出しません。</summary>
        public static BattleRoundPresentationPlan Create(RoundWinner winner)
        {
            return new BattleRoundPresentationPlan(winner, DefaultShakeSeed, BattleImpactKind.None);
        }

        /// <summary>振動seedを指定して作ります。テストと再現確認用です。決着エフェクトは出しません。</summary>
        public static BattleRoundPresentationPlan Create(RoundWinner winner, int shakeSeed)
        {
            return new BattleRoundPresentationPlan(winner, shakeSeed, BattleImpactKind.None);
        }

        /// <summary>
        /// 決着理由と勝因の属性まで含めて作ります。
        /// <paramref name="fxEnabled"/>が false なら決着エフェクトは<see cref="BattleImpactKind.None"/>です。
        /// </summary>
        public static BattleRoundPresentationPlan Create(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute,
            bool fxEnabled)
        {
            return Create(winner, decision, decidingAttribute, fxEnabled, DefaultShakeSeed);
        }

        /// <summary>決着理由・勝因の属性・振動seedを指定して作ります。</summary>
        public static BattleRoundPresentationPlan Create(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute,
            bool fxEnabled,
            int shakeSeed)
        {
            return new BattleRoundPresentationPlan(
                winner,
                shakeSeed,
                ResolveImpactKind(winner, decision, decidingAttribute, fxEnabled));
        }

        /// <summary>
        /// 決着理由と勝因の属性から、決着エフェクトの種類を決めます。例外は投げません。
        ///
        ///   FX OFF                               → None
        ///   勝者が Draw                          → Draw（決着理由に関係なく、勝者色を出さない）
        ///   AttributeAdvantage + RED/BLUE/GREEN  → Red / Blue / Green
        ///   AttributeAdvantage + 属性不明・範囲外 → None（色を推測しない）
        ///   PowerComparison                      → Power（属性があっても属性色は出さない）
        ///   CoreComparison                       → Core
        ///   Draw（勝者ありの矛盾した組み合わせ）  → None
        ///   範囲外の RoundDecision / RoundWinner  → None
        /// </summary>
        public static BattleImpactKind ResolveImpactKind(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? decidingAttribute,
            bool fxEnabled)
        {
            if (!fxEnabled)
            {
                return BattleImpactKind.None;
            }

            if (winner == RoundWinner.Draw)
            {
                return BattleImpactKind.Draw;
            }

            if (winner != RoundWinner.Player && winner != RoundWinner.Cpu)
            {
                return BattleImpactKind.None;
            }

            switch (decision)
            {
                case RoundDecision.AttributeAdvantage:
                    return ImpactOf(decidingAttribute);

                case RoundDecision.PowerComparison:
                    return BattleImpactKind.Power;

                case RoundDecision.CoreComparison:
                    return BattleImpactKind.Core;

                default:
                    return BattleImpactKind.None;
            }
        }

        private static BattleImpactKind ImpactOf(UnitAttribute? attribute)
        {
            if (!attribute.HasValue)
            {
                return BattleImpactKind.None;
            }

            switch (attribute.Value)
            {
                case UnitAttribute.Red:
                    return BattleImpactKind.Red;

                case UnitAttribute.Blue:
                    return BattleImpactKind.Blue;

                case UnitAttribute.Green:
                    return BattleImpactKind.Green;

                default:
                    return BattleImpactKind.None;
            }
        }

        /// <summary>
        /// ヒットストップ明けから<paramref name="elapsed"/>秒後の振動オフセット。
        ///
        /// 同じseedと時刻なら必ず同じ値を返します。振幅は線形に減衰し、
        /// <see cref="ShakeDuration"/>以降と開始前は必ず0です。
        /// 大きさが<see cref="ShakeAmplitude"/>を超えることはありません。
        /// </summary>
        public void SampleShake(float elapsed, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            if (!ShakeEnabled ||
                ShakeDuration <= 0f ||
                elapsed < 0f ||
                elapsed >= ShakeDuration)
            {
                return;
            }

            float decay = 1f - elapsed / ShakeDuration;
            int step = (int)Math.Floor(elapsed * ShakeFrequency);
            // 半円の中で向きを選び、段ごとに反対側へ振ります。同じ向きへ流れて見えないためです。
            double angle = Hash01(ShakeSeed, step) * Math.PI + ((step & 1) == 1 ? Math.PI : 0.0);
            float magnitude = ShakeAmplitude * decay;

            x = (float)(Math.Cos(angle) * magnitude);
            y = (float)(Math.Sin(angle) * magnitude);
        }

        /// <summary>
        /// 着地後に残る後退距離（px）。敗者は押し戻された位置のまま、
        /// 勝者と引き分けの双方は反動の一部だけを残して着地します。
        /// </summary>
        public float LandedDisplacement(RoundWinner side)
        {
            bool knockedBack = side == RoundWinner.Player ? PlayerKnockback : CpuKnockback;
            float displacement = side == RoundWinner.Player ? PlayerDisplacement : CpuDisplacement;

            return knockedBack ? displacement : displacement * LandingRecoilRemain;
        }

        private static float DisplacementOf(RoundWinner winner, RoundWinner side)
        {
            if (winner == RoundWinner.Draw)
            {
                return StandardDrawRecoilDistance;
            }

            return winner == side ? StandardWinnerRecoilDistance : StandardKnockbackDistance;
        }

        /// <summary>seedと段番号から0以上1未満の値を作ります。System.Randomの実装差に依存しません。</summary>
        private static double Hash01(int seed, int step)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)step * 0x85EBCA77u;

                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;

                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }
    }
}
