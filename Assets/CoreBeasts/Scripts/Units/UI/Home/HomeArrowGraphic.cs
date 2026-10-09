using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.HomeUI
{
    /// <summary>
    /// 右向きの矢印（Phase 7）。「100 → 130」「COPIES 1 → 2」の矢印を、フォントに無い記号を使わずに描きます。
    /// 軸1本と矢じり2本の3つの四角形（12頂点）だけです。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeArrowGraphic : MaskableGraphic
    {
        public const int VertexCount = 12;

        [SerializeField] [Range(1f, 10f)] private float thickness = 4f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
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
                return;
            }

            float y = r.center.y;
            float head = Mathf.Min(r.height * 0.45f, r.width * 0.4f);
            Vector2 tip = new Vector2(r.xMax - thickness * 0.5f, y);

            Line(vh, new Vector2(r.xMin, y), tip, color);
            Line(vh, tip, tip + new Vector2(-head, head), color);
            Line(vh, tip, tip + new Vector2(-head, -head), color);
        }

        private void Line(VertexHelper vh, Vector2 from, Vector2 to, Color c)
        {
            Vector2 d = (to - from).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * (thickness * 0.5f);
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
