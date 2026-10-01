namespace CoreBeasts.Battle
{
    /// <summary>
    /// 決着エフェクト1種類ぶんの設計値（Phase 2「属性別インパクト」）。
    ///
    /// Unityに依存しない純データです。色は 0xRRGGBB で持ち、描画側が Color へ直します。
    /// 時間・広がり・頂点数の上限をここで固定し、描画側はこれを超えないように作ります。
    ///
    /// 時間はすべて<see cref="BattleRoundPresentationPlan.ReleaseDuration"/>以内です。
    /// エフェクトはノックバックと着地のあいだに始まって終わり、Phase 1 の時系列を延ばしません。
    /// </summary>
    public sealed class AttributeEffectProfile
    {
        /// <summary>どの種類でも超えない頂点数。1回に1つしか出さないため、これが同時表示の上限です。</summary>
        public const int VertexBudget = 128;

        /// <summary>どの種類でも超えない広がり（衝突点からの距離、参照解像度上のpx）。</summary>
        public const float MaxRadiusLimit = 180f;

        public static readonly AttributeEffectProfile None =
            new AttributeEffectProfile(BattleImpactKind.None, 0f, 0f, 0, 0x000000, 0x000000, false, false);

        public static readonly AttributeEffectProfile Red =
            new AttributeEffectProfile(BattleImpactKind.Red, 0.24f, 150f, 44, 0xFF8A3D, 0xFF2A1A, true, false);

        public static readonly AttributeEffectProfile Blue =
            new AttributeEffectProfile(BattleImpactKind.Blue, 0.30f, 170f, 128, 0x6FE3FF, 0x2F7BFF, true, true);

        public static readonly AttributeEffectProfile Green =
            new AttributeEffectProfile(BattleImpactKind.Green, 0.30f, 150f, 80, 0xB4F050, 0x2FC85A, true, false);

        public static readonly AttributeEffectProfile Power =
            new AttributeEffectProfile(BattleImpactKind.Power, 0.26f, 140f, 81, 0xFFFFFF, 0xFFD24D, false, true);

        public static readonly AttributeEffectProfile Core =
            new AttributeEffectProfile(BattleImpactKind.Core, 0.22f, 110f, 61, 0xFFF8E6, 0xFFDC78, false, true);

        public static readonly AttributeEffectProfile Draw =
            new AttributeEffectProfile(BattleImpactKind.Draw, 0.28f, 150f, 80, 0xF2F2F2, 0xBFC4CC, false, true);

        private AttributeEffectProfile(
            BattleImpactKind kind,
            float duration,
            float maxRadius,
            int maxVertices,
            uint innerColor,
            uint outerColor,
            bool usesAttributeColor,
            bool isSymmetric)
        {
            Kind = kind;
            Duration = duration;
            MaxRadius = maxRadius;
            MaxVertices = maxVertices;
            InnerColor = innerColor;
            OuterColor = outerColor;
            UsesAttributeColor = usesAttributeColor;
            IsSymmetric = isSymmetric;
        }

        public BattleImpactKind Kind { get; }

        /// <summary>表示時間（秒）。</summary>
        public float Duration { get; }

        /// <summary>頂点が衝突点から離れてよい最大距離（参照解像度上のpx）。</summary>
        public float MaxRadius { get; }

        /// <summary>1フレームで出す頂点数の上限。</summary>
        public int MaxVertices { get; }

        /// <summary>衝突点側の色（0xRRGGBB）。</summary>
        public uint InnerColor { get; }

        /// <summary>外側の色（0xRRGGBB）。</summary>
        public uint OuterColor { get; }

        /// <summary>属性色（赤・青・緑）を使うか。POWER・CORE・DRAW は使いません。</summary>
        public bool UsesAttributeColor { get; }

        /// <summary>上下・左右とも鏡像対称な形か。</summary>
        public bool IsSymmetric { get; }

        /// <summary>何かを描くか。</summary>
        public bool IsVisible => Kind != BattleImpactKind.None;

        /// <summary>種類ごとの設計値。範囲外の値は<see cref="None"/>へ落とします。</summary>
        public static AttributeEffectProfile For(BattleImpactKind kind)
        {
            switch (kind)
            {
                case BattleImpactKind.Red:
                    return Red;

                case BattleImpactKind.Blue:
                    return Blue;

                case BattleImpactKind.Green:
                    return Green;

                case BattleImpactKind.Power:
                    return Power;

                case BattleImpactKind.Core:
                    return Core;

                case BattleImpactKind.Draw:
                    return Draw;

                default:
                    return None;
            }
        }

        /// <summary>None を除く全種類。テストと確認用です。</summary>
        public static AttributeEffectProfile[] Visible()
        {
            return new[] { Red, Blue, Green, Power, Core, Draw };
        }

        /// <summary>0xRRGGBB を 0〜1 の成分へ分けます。</summary>
        public static void Unpack(uint rgb, out float r, out float g, out float b)
        {
            r = ((rgb >> 16) & 0xFF) / 255f;
            g = ((rgb >> 8) & 0xFF) / 255f;
            b = (rgb & 0xFF) / 255f;
        }

        public override string ToString()
        {
            return Kind + " (" + Duration.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s)";
        }
    }
}
