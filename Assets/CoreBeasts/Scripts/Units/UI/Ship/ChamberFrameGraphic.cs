using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 画面ごとの「部屋」を囲む多層フレーム。
    ///
    /// 大きな単色矩形で区切らず、細い枠を何重かに重ねて
    /// 「巨大な構造体の中の一区画」に見せます。
    /// 左右対称を基本にしつつ、上側の角だけを落として非対称を残します。
    /// </summary>
    public sealed class ChamberFrameGraphic : ShipBackgroundGraphic
    {
        [Tooltip("枠の重なり数。")]
        [SerializeField] [Range(1, 4)] private int layers = 2;

        [Tooltip("いちばん外の枠の太さ。")]
        [SerializeField] [Range(1f, 8f)] private float thickness = 2f;

        [Tooltip("枠どうしの間隔。")]
        [SerializeField] [Range(2f, 32f)] private float spacing = 10f;

        [Tooltip("角を落とす長さ。")]
        [SerializeField] [Range(0f, 80f)] private float corner = 28f;

        [Tooltip("いちばん外の枠の濃さ。内側ほど薄くなります。")]
        [SerializeField] [Range(0f, 1f)] private float outerAlpha = 0.55f;

        [Tooltip("四隅に付ける短い突起の長さ。0で無し。")]
        [SerializeField] [Range(0f, 60f)] private float bracket = 26f;

        [Tooltip("上側の角だけ落として非対称にするか。")]
        [SerializeField] private bool asymmetricTop = true;

        protected override void Build(VertexHelper vh, Rect area)
        {
            for (int layer = 0; layer < layers; layer++)
            {
                float inset = layer * spacing;

                Rect r = new Rect(
                    area.xMin + inset,
                    area.yMin + inset,
                    area.width - inset * 2f,
                    area.height - inset * 2f);

                if (r.width <= 0f || r.height <= 0f)
                {
                    break;
                }

                float alpha = outerAlpha * (1f - layer / (float)(layers + 1));

                Color line = ShipPalette.WithAlpha(
                    layer == 0 ? ShipPalette.StructuralLineLit : ShipPalette.StructuralLine,
                    alpha);

                float c = Mathf.Min(corner, Mathf.Min(r.width, r.height) * 0.35f);
                float top = asymmetricTop ? c : 0f;

                float t = Mathf.Max(thickness - layer * 0.5f, 0.75f);

                // 上辺（左上だけ角を落とす、わずかな非対称）。
                AddLine(vh, new Vector2(r.xMin + top, r.yMax),
                        new Vector2(r.xMax - c, r.yMax), t, line, line);

                AddLine(vh, new Vector2(r.xMax - c, r.yMax),
                        new Vector2(r.xMax, r.yMax - c), t, line, line);

                if (top > 0f)
                {
                    AddLine(vh, new Vector2(r.xMin, r.yMax - top),
                            new Vector2(r.xMin + top, r.yMax), t, line, line);
                }

                // 下辺と左右。
                AddLine(vh, new Vector2(r.xMin + c, r.yMin),
                        new Vector2(r.xMax - c, r.yMin), t, line, line);

                AddLine(vh, new Vector2(r.xMin, r.yMin + c),
                        new Vector2(r.xMin + c, r.yMin), t, line, line);

                AddLine(vh, new Vector2(r.xMax - c, r.yMin),
                        new Vector2(r.xMax, r.yMin + c), t, line, line);

                AddLine(vh, new Vector2(r.xMin, r.yMin + c),
                        new Vector2(r.xMin, r.yMax - top), t, line, line);

                AddLine(vh, new Vector2(r.xMax, r.yMin + c),
                        new Vector2(r.xMax, r.yMax - c), t, line, line);
            }

            if (bracket <= 0f)
            {
                return;
            }

            // 四隅の突起。装甲が留められている感じを足します。
            Color accent = ShipPalette.WithAlpha(ShipPalette.ColdCyan, outerAlpha * 0.5f);

            float b = Mathf.Min(bracket, Mathf.Min(area.width, area.height) * 0.2f);

            AddLine(vh, new Vector2(area.xMin, area.yMin + b),
                    new Vector2(area.xMin, area.yMin), thickness, accent, accent);

            AddLine(vh, new Vector2(area.xMin, area.yMin),
                    new Vector2(area.xMin + b, area.yMin), thickness, accent, accent);

            AddLine(vh, new Vector2(area.xMax, area.yMin + b),
                    new Vector2(area.xMax, area.yMin), thickness, accent, accent);

            AddLine(vh, new Vector2(area.xMax - b, area.yMin),
                    new Vector2(area.xMax, area.yMin), thickness, accent, accent);
        }
    }
}
