using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 船内の背景を描くGraphicに共通する約束。
    ///
    /// 背景は見せるだけのものです。タップもドラッグも受け取りません。
    /// 画像素材は増やさず、頂点色だけで組み立てます。
    ///
    /// 明るさは<see cref="SetGlow"/>で受け取ります。
    /// 色を変えるだけなのでメッシュを作り直さず、毎フレーム呼んでも
    /// GCを起こしません。動かすかどうかは<see cref="ShipBackdropView"/>が決めます。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ShipBackgroundGraphic : MaskableGraphic
    {
        [Tooltip("いちばん明るいときの強さ。1で素の色、0で消えます。")]
        [SerializeField] [Range(0f, 1f)] private float glow = 1f;

        /// <summary>現在の明るさ。</summary>
        public float Glow => glow;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // 背景が入力を奪わないことは、どの経路から来ても守ります。
            raycastTarget = false;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            // 画面サイズが変わっても破綻しないよう、作り直します。
            SetVerticesDirty();
        }

        /// <summary>
        /// 明るさを変えます。メッシュは作り直さず、CanvasRendererの色だけを触ります。
        /// </summary>
        public void SetGlow(float value)
        {
            float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);

            if (Mathf.Approximately(glow, clamped))
            {
                return;
            }

            glow = clamped;

            // 頂点色は OnPopulateMesh が書きます。全体の濃さは canvasRenderer で掛けます。
            if (canvasRenderer != null)
            {
                canvasRenderer.SetAlpha(glow);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;

            if (r.width <= 0f || r.height <= 0f)
            {
                return;
            }

            Build(vh, r);
        }

        /// <summary>この背景の形を作ります。</summary>
        protected abstract void Build(VertexHelper vh, Rect area);

        /// <summary>四隅の色を指定して1枚の板を足します。</summary>
        protected static void AddQuad(
            VertexHelper vh,
            Vector2 min,
            Vector2 max,
            Color bottomLeft,
            Color bottomRight,
            Color topRight,
            Color topLeft)
        {
            int index = vh.currentVertCount;

            vh.AddVert(new Vector3(min.x, min.y), bottomLeft, Vector2.zero);
            vh.AddVert(new Vector3(max.x, min.y), bottomRight, Vector2.zero);
            vh.AddVert(new Vector3(max.x, max.y), topRight, Vector2.zero);
            vh.AddVert(new Vector3(min.x, max.y), topLeft, Vector2.zero);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        /// <summary>単色の板。</summary>
        protected static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color color)
        {
            AddQuad(vh, min, max, color, color, color, color);
        }

        /// <summary>太さのある線分。両端の色を変えられます。</summary>
        protected static void AddLine(
            VertexHelper vh,
            Vector2 from,
            Vector2 to,
            float thickness,
            Color fromColor,
            Color toColor)
        {
            Vector2 direction = to - from;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized
                * (thickness * 0.5f);

            int index = vh.currentVertCount;

            vh.AddVert(from - normal, fromColor, Vector2.zero);
            vh.AddVert(from + normal, fromColor, Vector2.zero);
            vh.AddVert(to + normal, toColor, Vector2.zero);
            vh.AddVert(to - normal, toColor, Vector2.zero);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
