using System;

using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>自分から見た直前のラウンドの結果（Phase 5）。初戦は <see cref="None"/>。</summary>
    public enum PreviousRoundResult
    {
        None = 0,
        Won = 1,
        Lost = 2,
        Draw = 3,
    }

    /// <summary>ユニークスキルが発動した、またはしなかった理由。</summary>
    public enum UniqueSkillReason
    {
        /// <summary>スキルを持たない。</summary>
        NoSkill = 0,

        /// <summary>初戦のため、直前の個体・結果が無い。</summary>
        FirstRound = 1,

        /// <summary>CRIMSON BITE: 直前の個体が RED を含む。</summary>
        PreviousUnitHadRed = 2,

        /// <summary>CRIMSON BITE: 直前の個体が RED を含まない。</summary>
        PreviousUnitHadNoRed = 3,

        /// <summary>TIDAL HOWL: 無条件。</summary>
        Unconditional = 4,

        /// <summary>VERDANT FANG: 直前のラウンドで負けた。</summary>
        LostPreviousRound = 5,

        /// <summary>VERDANT FANG: 直前のラウンドで勝った。</summary>
        WonPreviousRound = 6,

        /// <summary>VERDANT FANG: 直前のラウンドが引き分け。</summary>
        DrewPreviousRound = 7,

        /// <summary>STORM BITE: 相手が RED か BLUE を含む。</summary>
        OpponentHasRedOrBlue = 8,

        /// <summary>STORM BITE: 相手が RED も BLUE も含まない。</summary>
        OpponentHasNoRedOrBlue = 9,
    }

    /// <summary>選択前予告の状態。</summary>
    public enum UniqueSkillPreviewState
    {
        /// <summary>予告しない（スキルなし・発動しない）。</summary>
        None = 0,

        /// <summary>自分の履歴だけで発動が決まる（CRIMSON BITE・VERDANT FANG・TIDAL HOWL）。</summary>
        Activates = 1,

        /// <summary>発動するかが相手の選出で決まる（STORM BITE）。発動を予告しません。</summary>
        DependsOnOpponent = 2,
    }

    /// <summary>1陣営1ラウンドぶんのユニークスキルの結果。不変の値です。</summary>
    public readonly struct UniqueSkillActivation : IEquatable<UniqueSkillActivation>
    {
        public UniqueSkillActivation(UniqueSkillKind kind, bool activated, UniqueSkillReason reason, int selfBonus, int opponentPenalty)
        {
            Kind = kind;
            Activated = activated;
            Reason = reason;
            SelfBonus = activated ? selfBonus : 0;
            OpponentPenalty = activated ? opponentPenalty : 0;
        }

        /// <summary>スキルなし。</summary>
        public static UniqueSkillActivation None => new UniqueSkillActivation(UniqueSkillKind.None, false, UniqueSkillReason.NoSkill, 0, 0);

        /// <summary>持っているスキル（発動しなくても入ります）。</summary>
        public UniqueSkillKind Kind { get; }

        /// <summary>発動したか。</summary>
        public bool Activated { get; }

        /// <summary>発動した、またはしなかった理由。</summary>
        public UniqueSkillReason Reason { get; }

        /// <summary>自分の全属性POWERへ足す値（発動しなければ0）。</summary>
        public int SelfBonus { get; }

        /// <summary>相手の全属性POWERから引く値（発動しなければ0）。</summary>
        public int OpponentPenalty { get; }

        public bool Equals(UniqueSkillActivation other)
        {
            return Kind == other.Kind && Activated == other.Activated && Reason == other.Reason &&
                   SelfBonus == other.SelfBonus && OpponentPenalty == other.OpponentPenalty;
        }

        public override bool Equals(object obj)
        {
            return obj is UniqueSkillActivation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((((int)Kind * 31 + (Activated ? 1 : 0)) * 31 + (int)Reason) * 31 + SelfBonus) * 31 + OpponentPenalty;
            }
        }

        public override string ToString()
        {
            return Kind + (Activated ? " ON " : " off ") + Reason + " +" + SelfBonus + " / -" + OpponentPenalty;
        }
    }

    /// <summary>選択前予告。自分の履歴だけから作ります（相手の情報は使いません）。</summary>
    public readonly struct UniqueSkillPreview
    {
        public UniqueSkillPreview(UniqueSkillKind kind, UniqueSkillPreviewState state, int selfBonus, int opponentPenalty)
        {
            Kind = kind;
            State = state;
            SelfBonus = state == UniqueSkillPreviewState.Activates ? selfBonus : 0;
            OpponentPenalty = state == UniqueSkillPreviewState.Activates ? opponentPenalty : 0;
        }

        /// <summary>予告なし。</summary>
        public static UniqueSkillPreview None => new UniqueSkillPreview(UniqueSkillKind.None, UniqueSkillPreviewState.None, 0, 0);

        public UniqueSkillKind Kind { get; }

        public UniqueSkillPreviewState State { get; }

        /// <summary>発動が決まっているときだけ、自分へ足す値。</summary>
        public int SelfBonus { get; }

        /// <summary>発動が決まっているときだけ、相手から引く値。</summary>
        public int OpponentPenalty { get; }

        /// <summary>発動を予告するか。</summary>
        public bool Activates => State == UniqueSkillPreviewState.Activates;
    }

    /// <summary>
    /// ユニークスキル（Phase 5）の条件判定と数値。Unity に依存しない純粋な関数です。
    ///
    /// PLAYER と CPU に同じ関数・同じ条件・同じ数値で使います。ルールは <see cref="UniqueSkillKind"/> だけで分岐し、
    /// スキル名などの表示文字列は見ません。<see cref="BattleRules"/> は変えず、ここで求めた加算・減算を
    /// 一時的な個体へ反映してから判定へ渡します（<see cref="BattleRoundEvaluator"/>）。
    /// </summary>
    public static class UniqueSkill
    {
        /// <summary>CRIMSON BITE の加算。</summary>
        public const int CrimsonBiteBonus = 4;

        /// <summary>TIDAL HOWL が相手から引く値。</summary>
        public const int TidalHowlPenalty = 4;

        /// <summary>VERDANT FANG の加算。</summary>
        public const int VerdantFangBonus = 6;

        /// <summary>STORM BITE の加算。</summary>
        public const int StormBiteBonus = 3;

        /// <summary>スキル・LINK を反映した後の各属性POWERの下限。</summary>
        public const int MinimumPower = 1;

        /// <summary>
        /// 1陣営ぶんのスキルを判定します。
        /// <paramref name="previousUnit"/>は自分が直前に出した個体（初戦は null）、
        /// <paramref name="previousResult"/>は自分から見た直前のラウンドの結果です。
        /// <paramref name="opponent"/>は今回の相手の個体（元の個体）です。
        /// </summary>
        public static UniqueSkillActivation Evaluate(
            BattleUnit self, BattleUnit previousUnit, PreviousRoundResult previousResult, BattleUnit opponent)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (opponent == null)
            {
                throw new ArgumentNullException(nameof(opponent));
            }

            switch (self.Skill)
            {
                case UniqueSkillKind.CrimsonBite:
                    return CrimsonBite(previousUnit);

                case UniqueSkillKind.TidalHowl:
                    return new UniqueSkillActivation(UniqueSkillKind.TidalHowl, true, UniqueSkillReason.Unconditional, 0, TidalHowlPenalty);

                case UniqueSkillKind.VerdantFang:
                    return VerdantFang(previousResult);

                case UniqueSkillKind.StormBite:
                    bool hit = opponent.HasAttribute(UnitAttribute.Red) || opponent.HasAttribute(UnitAttribute.Blue);

                    return new UniqueSkillActivation(
                        UniqueSkillKind.StormBite,
                        hit,
                        hit ? UniqueSkillReason.OpponentHasRedOrBlue : UniqueSkillReason.OpponentHasNoRedOrBlue,
                        StormBiteBonus,
                        0);

                default:
                    return UniqueSkillActivation.None;
            }
        }

        /// <summary>
        /// 選択前予告。自分の直前の個体・結果だけから作り、相手の情報も状態も使いません（副作用なし）。
        /// STORM BITE は相手の選出が要るため、発動を予告せず <see cref="UniqueSkillPreviewState.DependsOnOpponent"/> を返します。
        /// </summary>
        public static UniqueSkillPreview Preview(BattleUnit self, BattleUnit previousUnit, PreviousRoundResult previousResult)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            UniqueSkillActivation own;

            switch (self.Skill)
            {
                case UniqueSkillKind.CrimsonBite:
                    own = CrimsonBite(previousUnit);
                    break;

                case UniqueSkillKind.TidalHowl:
                    return new UniqueSkillPreview(UniqueSkillKind.TidalHowl, UniqueSkillPreviewState.Activates, 0, TidalHowlPenalty);

                case UniqueSkillKind.VerdantFang:
                    own = VerdantFang(previousResult);
                    break;

                case UniqueSkillKind.StormBite:
                    return new UniqueSkillPreview(UniqueSkillKind.StormBite, UniqueSkillPreviewState.DependsOnOpponent, 0, 0);

                default:
                    return UniqueSkillPreview.None;
            }

            return own.Activated
                ? new UniqueSkillPreview(own.Kind, UniqueSkillPreviewState.Activates, own.SelfBonus, own.OpponentPenalty)
                : new UniqueSkillPreview(own.Kind, UniqueSkillPreviewState.None, 0, 0);
        }

        /// <summary>
        /// 加算・減算を反映した一時的な個体を作ります。各属性POWERを
        /// 「基礎 + LINK + 自分のスキル - 相手のスキル」とし、最後に下限 <see cref="MinimumPower"/> へ揃えます。
        /// 元の個体は書き換えません。CORE とスキルの種類はそのまま引き継ぎます。
        /// </summary>
        public static BattleUnit ApplyModifiers(BattleUnit unit, int linkBonus, int selfBonus, int opponentPenalty)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            int delta = linkBonus + selfBonus - opponentPenalty;

            if (delta == 0)
            {
                return unit;
            }

            AttributePower[] changed = new AttributePower[unit.AttributePowers.Count];

            for (int i = 0; i < changed.Length; i++)
            {
                AttributePower original = unit.AttributePowers[i];
                int value = original.Power + delta;

                changed[i] = original.WithPower(value < MinimumPower ? MinimumPower : value);
            }

            return new BattleUnit(unit.InstanceId, changed, unit.Core, unit.Skill);
        }

        private static UniqueSkillActivation CrimsonBite(BattleUnit previousUnit)
        {
            if (previousUnit == null)
            {
                return new UniqueSkillActivation(UniqueSkillKind.CrimsonBite, false, UniqueSkillReason.FirstRound, CrimsonBiteBonus, 0);
            }

            bool hit = previousUnit.HasAttribute(UnitAttribute.Red);

            return new UniqueSkillActivation(
                UniqueSkillKind.CrimsonBite,
                hit,
                hit ? UniqueSkillReason.PreviousUnitHadRed : UniqueSkillReason.PreviousUnitHadNoRed,
                CrimsonBiteBonus,
                0);
        }

        private static UniqueSkillActivation VerdantFang(PreviousRoundResult previousResult)
        {
            switch (previousResult)
            {
                case PreviousRoundResult.Lost:
                    return new UniqueSkillActivation(UniqueSkillKind.VerdantFang, true, UniqueSkillReason.LostPreviousRound, VerdantFangBonus, 0);

                case PreviousRoundResult.Won:
                    return new UniqueSkillActivation(UniqueSkillKind.VerdantFang, false, UniqueSkillReason.WonPreviousRound, VerdantFangBonus, 0);

                case PreviousRoundResult.Draw:
                    return new UniqueSkillActivation(UniqueSkillKind.VerdantFang, false, UniqueSkillReason.DrewPreviousRound, VerdantFangBonus, 0);

                default:
                    return new UniqueSkillActivation(UniqueSkillKind.VerdantFang, false, UniqueSkillReason.FirstRound, VerdantFangBonus, 0);
            }
        }
    }
}
