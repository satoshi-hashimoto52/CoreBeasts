namespace CoreBeasts.Battle.UI
{
    /// <summary>中央からの距離1つぶんの見え方。</summary>
    public readonly struct BattleRingSample
    {
        /// <summary>拡大率。</summary>
        public readonly float Scale;

        /// <summary>持ち上げ量（pt相当）。弧を作るのはこの値です。</summary>
        public readonly float OffsetY;

        /// <summary>不透明度。</summary>
        public readonly float Alpha;

        /// <summary>中央方向への傾き（度）。左側は正、右側は負。</summary>
        public readonly float Tilt;

        public BattleRingSample(float scale, float offsetY, float alpha, float tilt)
        {
            Scale = scale;
            OffsetY = offsetY;
            Alpha = alpha;
            Tilt = tilt;
        }
    }

    /// <summary>
    /// Mac Dock風の連続変化。中央からの距離だけで見え方を決めます。
    ///
    /// 距離は整数に限りません。スワイプ中は小数の距離を渡すため、
    /// 中央だけが突然拡大することがなく、指の動きに連れて滑らかに変わります。
    ///
    /// 節（距離0・1・2・3）の値を線形につなぐだけなので、
    /// Unityへ依存せずそのままテストできます。
    /// </summary>
    public static class BattleRingLayout
    {
        /// <summary>中央。</summary>
        public const float CenterScale = 1.20f;
        public const float CenterOffsetY = 24f;
        public const float CenterAlpha = 1f;

        /// <summary>左右隣接。</summary>
        public const float AdjacentScale = 0.92f;
        public const float AdjacentOffsetY = 10f;
        public const float AdjacentAlpha = 0.90f;
        public const float AdjacentTilt = 6f;

        /// <summary>左右外側。</summary>
        public const float OuterScale = 0.78f;
        public const float OuterOffsetY = 0f;
        public const float OuterAlpha = 0.68f;
        public const float OuterTilt = 12f;

        /// <summary>奥側。</summary>
        public const float BackScale = 0.66f;
        public const float BackOffsetY = 0f;
        public const float BackAlpha = 0.45f;

        private static readonly float[] Distances = { 0f, 1f, 2f, 3f };
        private static readonly float[] Scales =
            { CenterScale, AdjacentScale, OuterScale, BackScale };
        private static readonly float[] OffsetsY =
            { CenterOffsetY, AdjacentOffsetY, OuterOffsetY, BackOffsetY };
        private static readonly float[] Alphas =
            { CenterAlpha, AdjacentAlpha, OuterAlpha, BackAlpha };
        private static readonly float[] Tilts =
            { 0f, AdjacentTilt, OuterTilt, OuterTilt };

        /// <summary>
        /// 中央からの符号つき距離から見え方を作ります。
        /// 左（負）と右（正）で傾きの向きだけが反転し、他は対称です。
        /// </summary>
        public static BattleRingSample Evaluate(float signedDistance)
        {
            float distance = signedDistance < 0f ? -signedDistance : signedDistance;

            float scale = Interpolate(Scales, distance);
            float offsetY = Interpolate(OffsetsY, distance);
            float alpha = Interpolate(Alphas, distance);
            float tilt = Interpolate(Tilts, distance);

            // 中央へ向かって傾けます。右側は左へ、左側は右へ。
            if (signedDistance > 0f)
            {
                tilt = -tilt;
            }

            return new BattleRingSample(scale, offsetY, alpha, tilt);
        }

        /// <summary>
        /// 奥側の見え方。距離では表せないため、区分から直接引きます。
        /// </summary>
        public static BattleRingSample BackSample()
        {
            return new BattleRingSample(BackScale, BackOffsetY, BackAlpha, 0f);
        }

        /// <summary>
        /// 中央からの距離に対する横位置（pt相当）。
        /// 外側ほど間隔を詰め、弧に沿って見えるようにします。
        /// </summary>
        public static float HorizontalOffset(float signedDistance, float spacing)
        {
            float distance = signedDistance < 0f ? -signedDistance : signedDistance;

            // 1体目までは等間隔、そこから先は 0.82 倍ずつ詰めます。
            float magnitude = distance <= 1f
                ? distance * spacing
                : spacing + (distance - 1f) * spacing * 0.82f;

            return signedDistance < 0f ? -magnitude : magnitude;
        }

        private static float Interpolate(float[] values, float distance)
        {
            if (distance <= Distances[0])
            {
                return values[0];
            }

            for (int i = 1; i < Distances.Length; i++)
            {
                if (distance <= Distances[i])
                {
                    float span = Distances[i] - Distances[i - 1];
                    float t = span <= 0f ? 0f : (distance - Distances[i - 1]) / span;

                    return values[i - 1] + (values[i] - values[i - 1]) * t;
                }
            }

            return values[values.Length - 1];
        }
    }
}
