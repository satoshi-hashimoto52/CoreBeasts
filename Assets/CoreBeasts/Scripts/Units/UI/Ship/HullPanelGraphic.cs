using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 装甲板と継ぎ目。矩形をそのまま置くのではなく、
    /// 面・縁・継ぎ目の3つで「板が張ってある」ことを見せます。
    ///
    /// UIの背後へ敷くので、面はごく暗く、縁だけが弱く光ります。
    /// </summary>
    public sealed class HullPanelGraphic : ShipBackgroundGraphic
    {
        [Tooltip("板の面の色を、奥側か手前側かで選びます。")]
        [SerializeField] private bool raised;

        [Tooltip("縁の太さ。")]
        [SerializeField] [Range(1f, 8f)] private float edgeThickness = 2f;

        [Tooltip("継ぎ目の本数。0で無し。")]
        [SerializeField] [Range(0, 6)] private int seams = 2;

        [Tooltip("継ぎ目を横向きに引くか。false で縦向き。")]
        [SerializeField] private bool horizontalSeams = true;

        [Tooltip("角を落とす長さ。0で直角のままです。")]
        [SerializeField] [Range(0f, 48f)] private float corner = 18f;

        [Tooltip("面の濃さ。UIの背後では低く保ちます。")]
        [SerializeField] [Range(0f, 1f)] private float fillAlpha = 0.9f;

        protected override void Build(VertexHelper vh, Rect area)
        {
            Color face = raised ? ShipPalette.RaisedHull : ShipPalette.DeepHull;
            Color faceLit = raised ? ShipPalette.RaisedHullLit : ShipPalette.DeepHullLit;

            face = ShipPalette.WithAlpha(face, fillAlpha);
            faceLit = ShipPalette.WithAlpha(faceLit, fillAlpha);

            float c = Mathf.Min(corner, Mathf.Min(area.width, area.height) * 0.4f);

            // 面。上が少しだけ明るく、下へ沈みます。
            if (c <= 0f)
            {
                AddQuad(
                    vh,
                    new Vector2(area.xMin, area.yMin),
                    new Vector2(area.xMax, area.yMax),
                    face, face, faceLit, faceLit);
            }
            else
            {
                // 角を落とした八角形。装甲板らしい形にします。
                AddBevelled(vh, area, c, face, faceLit);
            }

            // 縁。構造線の色で細く回します。
            Color edge = ShipPalette.WithAlpha(ShipPalette.StructuralLine, 0.85f);

            AddLine(vh, new Vector2(area.xMin + c, area.yMax),
                    new Vector2(area.xMax - c, area.yMax), edgeThickness, edge, edge);

            AddLine(vh, new Vector2(area.xMin + c, area.yMin),
                    new Vector2(area.xMax - c, area.yMin), edgeThickness, edge, edge);

            AddLine(vh, new Vector2(area.xMin, area.yMin + c),
                    new Vector2(area.xMin, area.yMax - c), edgeThickness, edge, edge);

            AddLine(vh, new Vector2(area.xMax, area.yMin + c),
                    new Vector2(area.xMax, area.yMax - c), edgeThickness, edge, edge);

            if (c > 0f)
            {
                AddLine(vh, new Vector2(area.xMin, area.yMax - c),
                        new Vector2(area.xMin + c, area.yMax), edgeThickness, edge, edge);

                AddLine(vh, new Vector2(area.xMax - c, area.yMax),
                        new Vector2(area.xMax, area.yMax - c), edgeThickness, edge, edge);

                AddLine(vh, new Vector2(area.xMin, area.yMin + c),
                        new Vector2(area.xMin + c, area.yMin), edgeThickness, edge, edge);

                AddLine(vh, new Vector2(area.xMax - c, area.yMin),
                        new Vector2(area.xMax, area.yMin + c), edgeThickness, edge, edge);
            }

            // 継ぎ目。板が分かれていることだけを示す、ごく細い線です。
            Color seam = ShipPalette.WithAlpha(ShipPalette.StructuralLine, 0.35f);

            for (int i = 1; i <= seams; i++)
            {
                float t = i / (float)(seams + 1);

                if (horizontalSeams)
                {
                    float y = Mathf.Lerp(area.yMin, area.yMax, t);

                    AddLine(vh, new Vector2(area.xMin + c, y),
                            new Vector2(area.xMax - c, y), 1f, seam, seam);
                }
                else
                {
                    float x = Mathf.Lerp(area.xMin, area.xMax, t);

                    AddLine(vh, new Vector2(x, area.yMin + c),
                            new Vector2(x, area.yMax - c), 1f, seam, seam);
                }
            }
        }

        /// <summary>角を落とした面。</summary>
        private static void AddBevelled(
            VertexHelper vh, Rect area, float corner, Color bottom, Color top)
        {
            Vector2[] points =
            {
                new Vector2(area.xMin + corner, area.yMin),
                new Vector2(area.xMax - corner, area.yMin),
                new Vector2(area.xMax, area.yMin + corner),
                new Vector2(area.xMax, area.yMax - corner),
                new Vector2(area.xMax - corner, area.yMax),
                new Vector2(area.xMin + corner, area.yMax),
                new Vector2(area.xMin, area.yMax - corner),
                new Vector2(area.xMin, area.yMin + corner),
            };

            int centre = vh.currentVertCount;

            vh.AddVert(area.center, Color.Lerp(bottom, top, 0.5f), Vector2.zero);

            for (int i = 0; i < points.Length; i++)
            {
                float t = Mathf.InverseLerp(area.yMin, area.yMax, points[i].y);

                vh.AddVert(points[i], Color.Lerp(bottom, top, t), Vector2.zero);
            }

            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;

                vh.AddTriangle(centre, centre + 1 + i, centre + 1 + next);
            }
        }
    }
}
