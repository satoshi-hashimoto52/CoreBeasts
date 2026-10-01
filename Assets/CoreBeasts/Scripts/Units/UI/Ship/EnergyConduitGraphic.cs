using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 船体を走る細いエネルギー導管。
    ///
    /// 稼働している場所だけが光ります。常時全部を光らせると
    /// サイバー風のネオンになってしまうため、本数は絞ります。
    /// 流れる微光は<see cref="SetFlow"/>で位置だけ動かします。
    /// </summary>
    public sealed class EnergyConduitGraphic : ShipBackgroundGraphic
    {
        [Tooltip("導管の本数。")]
        [SerializeField] [Range(1, 6)] private int lines = 2;

        [Tooltip("縦に走らせるか。false で横向き。")]
        [SerializeField] private bool vertical = true;

        [Tooltip("導管の太さ。")]
        [SerializeField] [Range(1f, 10f)] private float thickness = 2f;

        [Tooltip("導管そのものの濃さ。")]
        [SerializeField] [Range(0f, 0.6f)] private float baseAlpha = 0.16f;

        [Tooltip("流れる微光の濃さ。")]
        [SerializeField] [Range(0f, 1f)] private float pulseAlpha = 0.5f;

        [Tooltip("微光の長さ（導管全体に対する割合）。")]
        [SerializeField] [Range(0.05f, 0.5f)] private float pulseLength = 0.18f;

        [Tooltip("青紫にするか。false で冷たいシアン。")]
        [SerializeField] private bool violet;

        [Tooltip("微光の位置（0〜1）。")]
        [SerializeField] [Range(0f, 1f)] private float flow;

        /// <summary>微光の位置をずらします。動かすかどうかは外が決めます。</summary>
        public void SetFlow(float value)
        {
            float wrapped = value - Mathf.Floor(value);

            if (Mathf.Approximately(flow, wrapped))
            {
                return;
            }

            flow = wrapped;
            SetVerticesDirty();
        }

        protected override void Build(VertexHelper vh, Rect area)
        {
            Color tint = violet ? ShipPalette.BlueViolet : ShipPalette.ColdCyan;
            Color bright = violet ? ShipPalette.BlueVioletBright : ShipPalette.ColdCyanBright;

            Color dim = ShipPalette.WithAlpha(tint, baseAlpha);
            Color lit = ShipPalette.WithAlpha(bright, pulseAlpha);
            Color fade = ShipPalette.WithAlpha(bright, 0f);

            for (int i = 0; i < lines; i++)
            {
                float t = (i + 1) / (float)(lines + 1);

                Vector2 from;
                Vector2 to;

                if (vertical)
                {
                    float x = Mathf.Lerp(area.xMin, area.xMax, t);
                    from = new Vector2(x, area.yMin);
                    to = new Vector2(x, area.yMax);
                }
                else
                {
                    float y = Mathf.Lerp(area.yMin, area.yMax, t);
                    from = new Vector2(area.xMin, y);
                    to = new Vector2(area.xMax, y);
                }

                // 導管そのもの。
                AddLine(vh, from, to, thickness, dim, dim);

                // 流れる微光。導管ごとに少しずらして、そろって光らないようにします。
                float offset = flow + i / (float)lines;
                offset -= Mathf.Floor(offset);

                float head = offset;
                float tail = offset - pulseLength;

                if (tail < 0f)
                {
                    tail = 0f;
                }

                if (head <= tail)
                {
                    continue;
                }

                AddLine(
                    vh,
                    Vector2.Lerp(from, to, tail),
                    Vector2.Lerp(from, to, head),
                    thickness,
                    fade,
                    lit);
            }
        }
    }
}
