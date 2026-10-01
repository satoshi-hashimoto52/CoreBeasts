using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>発進台のどの部分を描くか。</summary>
    public enum LaunchPedestalPart
    {
        /// <summary>内側の面。暗い半透明で、キャラクターの足元を受けます。</summary>
        Fill = 0,

        /// <summary>外周のリング。属性色で出します。</summary>
        Rim = 1,
    }

    /// <summary>
    /// 発進台（Launch Pedestal）をコード生成メッシュで描きます。
    ///
    /// 上から見た六角形を縦につぶした形で、カードのような長方形にはなりません。
    /// 外部PNGも専用Shaderも使わず、<see cref="MaskableGraphic"/>が既定で使う
    /// UI/Default系マテリアルへ頂点を流し込むだけです。
    ///
    /// 色は決めません。属性色は<see cref="CoreBeasts.Units.AttributeColorResolver"/>が
    /// 解決したものを<see cref="Apply"/>で受け取るだけです。
    /// 2属性は左半分が第一属性、右半分が第二属性になります。
    ///
    /// 頂点は必ず次の順で出します（<see cref="Sides"/>=6 のとき）。
    ///   Fill : 中心1点 + 外周6点 = 7頂点 / 6三角形
    ///   Rim  : 外周6点 + 内周6点 = 12頂点 / 12三角形
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaunchPedestalGraphic : MaskableGraphic
    {
        /// <summary>角の数。六角形にします。</summary>
        public const int Sides = 6;

        [SerializeField] private LaunchPedestalPart part = LaunchPedestalPart.Fill;

        [Tooltip("縦のつぶし具合。1で真円、小さいほど寝かせた楕円に見えます。")]
        [SerializeField] [Range(0.25f, 1f)] private float flatten = 0.42f;

        [Tooltip("リングの太さ（外半径に対する割合）。")]
        [SerializeField] [Range(0.08f, 0.4f)] private float rimWidth = 0.18f;

        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;

        [Tooltip("2属性なら左半分と右半分を塗り分けます。")]
        [SerializeField] private bool isDualAttribute;

        [Tooltip("この不透明度で出します。")]
        [SerializeField] [Range(0f, 1f)] private float opacity = 1f;

        /// <summary>描く部分。</summary>
        public LaunchPedestalPart Part
        {
            get => part;
            set { part = value; SetVerticesDirty(); }
        }

        /// <summary>第一属性の色。2属性なら左半分に出ます。</summary>
        public Color PrimaryColor => primaryColor;

        /// <summary>第二属性の色。2属性なら右半分に出ます。</summary>
        public Color SecondaryColor => secondaryColor;

        /// <summary>2属性として塗り分けるか。</summary>
        public bool IsDualAttribute => isDualAttribute;

        /// <summary>現在の不透明度。</summary>
        public float Opacity => opacity;

        /// <summary>単属性では第一属性色と同じになります。</summary>
        public Color EffectiveSecondaryColor =>
            isDualAttribute ? secondaryColor : primaryColor;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // 発進台は装飾です。入力は透明な入力面だけが受けます。
            raycastTarget = false;

            SyncGraphicColor();
        }

        /// <summary>解決済みの配色をそのまま受け取ります。</summary>
        public void Apply(Color primary, Color secondary, bool dual)
        {
            primaryColor = primary;
            secondaryColor = secondary;
            isDualAttribute = dual;

            SyncGraphicColor();
            SetVerticesDirty();
        }

        /// <summary>明るさを変えます。中央は明るく、外側は暗くします。</summary>
        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);

            SyncGraphicColor();
            SetVerticesDirty();
        }

        /// <summary>
        /// 頂点色は<see cref="OnPopulateMesh"/>で直接書くため、
        /// 既定の<see cref="Graphic.color"/>は描画に使われません。
        /// そのままだと Inspector も検査も「白」と報告してしまうので、
        /// 実際に出している色をここへ写して食い違いを無くします。
        /// </summary>
        private void SyncGraphicColor()
        {
            Color shown = primaryColor;
            shown.a *= opacity;

            if (base.color != shown)
            {
                base.color = shown;
            }
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

            float radius = Mathf.Min(r.width, r.height / Mathf.Max(flatten, 0.0001f)) * 0.5f;

            if (radius <= 0f)
            {
                return;
            }

            Vector2 centre = r.center;

            if (part == LaunchPedestalPart.Fill)
            {
                AddFill(vh, centre, radius);
            }
            else
            {
                AddRim(vh, centre, radius);
            }
        }

        /// <summary>角 <paramref name="index"/> の位置。上を頂点にせず、左右が広い向きにします。</summary>
        private Vector2 Corner(Vector2 centre, float radius, int index, float scale)
        {
            float angle = (index / (float)Sides) * Mathf.PI * 2f;

            return new Vector2(
                centre.x + Mathf.Cos(angle) * radius * scale,
                centre.y + Mathf.Sin(angle) * radius * scale * flatten);
        }

        /// <summary>左半分なら第一属性、右半分なら第二属性。</summary>
        private Color ColorAt(Vector2 centre, Vector2 point)
        {
            Color color = point.x <= centre.x ? primaryColor : EffectiveSecondaryColor;

            color.a *= opacity;

            return color;
        }

        private void AddFill(VertexHelper vh, Vector2 centre, float radius)
        {
            Color middle = primaryColor;
            middle.a *= opacity;

            vh.AddVert(centre, middle, Vector2.zero);

            for (int i = 0; i < Sides; i++)
            {
                Vector2 point = Corner(centre, radius, i, 1f);

                vh.AddVert(point, ColorAt(centre, point), Vector2.zero);
            }

            for (int i = 0; i < Sides; i++)
            {
                int next = i + 1 < Sides ? i + 2 : 1;

                vh.AddTriangle(0, i + 1, next);
            }
        }

        private void AddRim(VertexHelper vh, Vector2 centre, float radius)
        {
            float inner = 1f - rimWidth;

            for (int i = 0; i < Sides; i++)
            {
                Vector2 point = Corner(centre, radius, i, 1f);

                vh.AddVert(point, ColorAt(centre, point), Vector2.zero);
            }

            for (int i = 0; i < Sides; i++)
            {
                Vector2 point = Corner(centre, radius, i, inner);

                vh.AddVert(point, ColorAt(centre, point), Vector2.zero);
            }

            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;

                int outerA = i;
                int outerB = next;
                int innerA = Sides + i;
                int innerB = Sides + next;

                vh.AddTriangle(outerA, outerB, innerB);
                vh.AddTriangle(innerB, innerA, outerA);
            }
        }
    }
}
