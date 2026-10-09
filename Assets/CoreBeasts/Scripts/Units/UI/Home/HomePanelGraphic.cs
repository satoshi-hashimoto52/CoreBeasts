using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.HomeUI
{
    /// <summary>
    /// Home 系画面のパネル・ボタンの下地（Phase 7）。角を斜めに落とした面と、細い縁の線、角の小さな刻みを描く1枚の Graphic です。
    ///
    /// 暗い機械施設の意匠に合わせ、色は呼び出し側が渡します（構造色・白金色）。Texture・Material は使わず、
    /// 頂点数は <see cref="VertexCount"/> で固定です。入力は受けません（ボタンの判定は別の透明な Graphic が持ちます）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomePanelGraphic : MaskableGraphic
    {
        /// <summary>面（八角形）9頂点 + 縁8辺×4 + 角の刻み4×4。</summary>
        public const int VertexCount = 9 + 8 * 4 + 4 * 4;

        private Color fill = new Color(0.05f, 0.08f, 0.13f, 0.86f);
        private Color edge = new Color(0.26f, 0.44f, 0.62f, 0.9f);
        private Color accent = new Color(0.36f, 0.86f, 0.96f, 1f);
        private float cornerCut = 18f;
        private float edgeThickness = 2f;

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        public Color Fill => fill;

        public Color Edge => edge;

        public Color Accent => accent;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>色と形を決めます。</summary>
        public void Configure(Color fillColor, Color edgeColor, Color accentColor, float corner = 18f, float thickness = 2f)
        {
            fill = fillColor;
            edge = edgeColor;
            accent = accentColor;
            cornerCut = Mathf.Max(0f, corner);
            edgeThickness = Mathf.Max(1f, thickness);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;

            if (r.width <= 0f || r.height <= 0f)
            {
                LastVertexCount = 0;
                return;
            }

            float c = Mathf.Min(cornerCut, Mathf.Min(r.width, r.height) * 0.3f);

            Vector2[] o =
            {
                new Vector2(r.xMin + c, r.yMin), new Vector2(r.xMax - c, r.yMin),
                new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax, r.yMax - c),
                new Vector2(r.xMax - c, r.yMax), new Vector2(r.xMin + c, r.yMax),
                new Vector2(r.xMin, r.yMax - c), new Vector2(r.xMin, r.yMin + c),
            };

            // 面
            Color f = fill * color;
            int center = vh.currentVertCount;
            vh.AddVert(new Vector3(r.center.x, r.center.y), f, Vector4.zero);

            for (int i = 0; i < o.Length; i++)
            {
                vh.AddVert(o[i], f, Vector4.zero);
            }

            for (int i = 0; i < o.Length; i++)
            {
                vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % o.Length);
            }

            // 縁の線
            Color e = edge * color;

            for (int i = 0; i < o.Length; i++)
            {
                Line(vh, o[i], o[(i + 1) % o.Length], edgeThickness, e);
            }

            // 角の刻み（白金・青の差し色）
            Color a = accent * color;
            float tick = Mathf.Min(28f, r.width * 0.12f);

            Line(vh, new Vector2(r.xMin + c, r.yMax - 1f), new Vector2(r.xMin + c + tick, r.yMax - 1f), edgeThickness + 1f, a);
            Line(vh, new Vector2(r.xMax - c - tick, r.yMax - 1f), new Vector2(r.xMax - c, r.yMax - 1f), edgeThickness + 1f, a);
            Line(vh, new Vector2(r.xMin + c, r.yMin + 1f), new Vector2(r.xMin + c + tick, r.yMin + 1f), edgeThickness + 1f, a);
            Line(vh, new Vector2(r.xMax - c - tick, r.yMin + 1f), new Vector2(r.xMax - c, r.yMin + 1f), edgeThickness + 1f, a);

            LastVertexCount = vh.currentVertCount;
        }

        private static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color c)
        {
            Vector2 d = to - from;
            float length = d.magnitude;

            if (length <= 0.0001f)
            {
                d = Vector2.right;
            }
            else
            {
                d /= length;
            }

            Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);
            int start = vh.currentVertCount;

            vh.AddVert(from - n, c, Vector4.zero);
            vh.AddVert(from + n, c, Vector4.zero);
            vh.AddVert(to + n, c, Vector4.zero);
            vh.AddVert(to - n, c, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
