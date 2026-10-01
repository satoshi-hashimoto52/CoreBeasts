using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 奥へ収束する透視線。巨大な構造体の内側にいる奥行きを出します。
    ///
    /// 全面に格子を敷き詰めず、消失点へ向かう線と、
    /// 距離を示す横線を数本だけ引きます。
    /// </summary>
    public sealed class DimensionalGridGraphic : ShipBackgroundGraphic
    {
        [Tooltip("消失点（0〜1の画面比）。ここへ線が集まります。")]
        [SerializeField] private Vector2 vanishingPoint = new Vector2(0.5f, 0.56f);

        [Tooltip("放射する線の本数。左右対称に引きます。")]
        [SerializeField] [Range(4, 24)] private int rays = 12;

        [Tooltip("距離を示す横線の本数。")]
        [SerializeField] [Range(0, 8)] private int rings = 4;

        [Tooltip("線の太さ。")]
        [SerializeField] [Range(0.5f, 4f)] private float thickness = 1.5f;

        [Tooltip("いちばん手前での濃さ。奥へ向かって消えます。")]
        [SerializeField] [Range(0f, 0.6f)] private float nearAlpha = 0.22f;

        [Tooltip("位相。ゆっくり動かすと空間が流れて見えます。")]
        [SerializeField] [Range(0f, 1f)] private float phase;

        /// <summary>位相をずらします。動かすかどうかは外が決めます。</summary>
        public void SetPhase(float value)
        {
            float wrapped = value - Mathf.Floor(value);

            if (Mathf.Approximately(phase, wrapped))
            {
                return;
            }

            phase = wrapped;
            SetVerticesDirty();
        }

        protected override void Build(VertexHelper vh, Rect area)
        {
            Vector2 centre = new Vector2(
                Mathf.Lerp(area.xMin, area.xMax, vanishingPoint.x),
                Mathf.Lerp(area.yMin, area.yMax, vanishingPoint.y));

            Color near = ShipPalette.WithAlpha(ShipPalette.StructuralLine, nearAlpha);
            Color far = ShipPalette.WithAlpha(ShipPalette.StructuralLine, 0f);

            // 消失点から画面の外周へ向かって引きます。
            float reach = Mathf.Max(area.width, area.height);

            for (int i = 0; i < rays; i++)
            {
                float angle = (i / (float)rays) * Mathf.PI * 2f;

                Vector2 outer = new Vector2(
                    centre.x + Mathf.Cos(angle) * reach,
                    centre.y + Mathf.Sin(angle) * reach);

                AddLine(vh, centre, outer, thickness, far, near);
            }

            // 距離を示す横線。位相ぶんだけ手前へ流れます。
            for (int i = 0; i < rings; i++)
            {
                float t = (i + phase) / rings;

                if (t <= 0f || t >= 1f)
                {
                    continue;
                }

                // 手前ほど間隔が広がるよう、二乗で配ります。
                float spread = t * t;

                float halfWidth = area.width * 0.5f * spread;
                float y = Mathf.Lerp(centre.y, area.yMin, spread);

                Color color = ShipPalette.WithAlpha(
                    ShipPalette.StructuralLine, nearAlpha * spread);

                AddLine(
                    vh,
                    new Vector2(centre.x - halfWidth, y),
                    new Vector2(centre.x + halfWidth, y),
                    thickness,
                    color,
                    color);
            }
        }
    }
}
