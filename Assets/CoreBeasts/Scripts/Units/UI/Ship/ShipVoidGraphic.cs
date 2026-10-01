using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// いちばん奥の虚空。上下のグラデーションと、ごく弱い星雲だけを描きます。
    ///
    /// 明るい星空にはしません。船内から見える「外」はほとんど暗く、
    /// 手前の構造が読めるだけの明度差があれば足ります。
    /// </summary>
    public sealed class ShipVoidGraphic : ShipBackgroundGraphic
    {
        [Tooltip("縦のグラデーションの段数。増やすほど滑らかですが頂点も増えます。")]
        [SerializeField] [Range(2, 12)] private int bands = 6;

        [Tooltip("星雲のにじみの強さ。0で無し。")]
        [SerializeField] [Range(0f, 0.3f)] private float nebula = 0.1f;

        [Tooltip("星雲の中心（0〜1の画面比）。")]
        [SerializeField] private Vector2 nebulaCenter = new Vector2(0.5f, 0.62f);

        protected override void Build(VertexHelper vh, Rect area)
        {
            // 上から下へ、深い黒紺が沈んでいきます。
            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands;
                float t1 = (i + 1) / (float)bands;

                Color c0 = Color.Lerp(ShipPalette.BaseVoidBottom, ShipPalette.BaseVoidTop, t0);
                Color c1 = Color.Lerp(ShipPalette.BaseVoidBottom, ShipPalette.BaseVoidTop, t1);

                AddQuad(
                    vh,
                    new Vector2(area.xMin, Mathf.Lerp(area.yMin, area.yMax, t0)),
                    new Vector2(area.xMax, Mathf.Lerp(area.yMin, area.yMax, t1)),
                    c0, c0, c1, c1);
            }

            if (nebula <= 0f)
            {
                return;
            }

            // 星雲は、中心だけがほのかに青紫へ寄る一枚の板で表します。
            // 粒を大量に置くと細かすぎるノイズになります。
            Vector2 centre = new Vector2(
                Mathf.Lerp(area.xMin, area.xMax, nebulaCenter.x),
                Mathf.Lerp(area.yMin, area.yMax, nebulaCenter.y));

            float radius = Mathf.Min(area.width, area.height) * 0.45f;

            Color core = ShipPalette.WithAlpha(ShipPalette.BlueViolet, nebula);
            Color edge = ShipPalette.WithAlpha(ShipPalette.BlueViolet, 0f);

            const int Sides = 12;
            int centreIndex = vh.currentVertCount;

            vh.AddVert(centre, core, Vector2.zero);

            for (int i = 0; i <= Sides; i++)
            {
                float angle = i / (float)Sides * Mathf.PI * 2f;

                vh.AddVert(
                    new Vector3(
                        centre.x + Mathf.Cos(angle) * radius,
                        centre.y + Mathf.Sin(angle) * radius * 0.7f),
                    edge,
                    Vector2.zero);
            }

            for (int i = 0; i < Sides; i++)
            {
                vh.AddTriangle(centreIndex, centreIndex + 1 + i, centreIndex + 2 + i);
            }
        }
    }
}
