using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 所持カードの属性を、外周フレームと薄い背景だけで示す描画。
    ///
    /// 単属性はカード全面が同じ色、2属性は左下から右上へ走る対角線で
    /// 「左上＝第一属性 / 右下＝第二属性」に分割します。
    ///
    /// 外部PNGもShaderも追加しません。<see cref="MaskableGraphic"/>が既定で使う
    /// UI/Default系のマテリアルへ、コード生成のメッシュを流し込むだけです。
    ///
    /// 色は<see cref="AttributeColorResolver"/>から受け取るだけで、
    /// ここでは属性から色を決めません（色定義を二重に持たないため）。
    ///
    /// 頂点は必ず次の順で出します。テストが頂点位置と色をそのまま確かめられます。
    ///   [0..2]   内側の塗り・左上三角（第一属性）
    ///   [3..5]   内側の塗り・右下三角（第二属性）
    ///   [6..9]   フレーム上辺（第一属性）
    ///   [10..13] フレーム左辺（第一属性）
    ///   [14..17] フレーム右辺（第二属性）
    ///   [18..21] フレーム下辺（第二属性）
    /// 対角線はカードの左下隅と右上隅をちょうど通るため、
    /// 上辺・左辺が線の左上側、右辺・下辺が右下側に丸ごと入ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiagonalAttributeCardGraphic : MaskableGraphic
    {
        /// <summary>塗りの三角形ぶんの頂点数。</summary>
        public const int FillVertexCount = 6;

        /// <summary>フレーム4辺ぶんの頂点数。</summary>
        public const int BorderVertexCount = 16;

        /// <summary>1回の描画で出す頂点数。</summary>
        public const int TotalVertexCount = FillVertexCount + BorderVertexCount;

        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;

        [Tooltip("2属性なら対角線で2色へ分けます。単属性は全面が第一属性色です。")]
        [SerializeField] private bool isDualAttribute;

        [Tooltip("薄く敷く背景の不透明度。立ち絵・名前・レベルの可読性を落とさない範囲。")]
        [SerializeField] [Range(0.12f, 0.18f)] private float backgroundAlpha = 0.15f;

        [Tooltip("外周フレームの不透明度。")]
        [SerializeField] [Range(0.9f, 1f)] private float borderAlpha = 0.95f;

        [Tooltip("外周フレームの太さ（Canvas単位）。")]
        [SerializeField] [Range(4f, 6f)] private float borderWidth = 5f;

        /// <summary>第一属性の色。2属性なら左上側に出ます。</summary>
        public Color PrimaryColor
        {
            get => primaryColor;
            set { primaryColor = value; SetVerticesDirty(); }
        }

        /// <summary>第二属性の色。2属性なら右下側に出ます。</summary>
        public Color SecondaryColor
        {
            get => secondaryColor;
            set { secondaryColor = value; SetVerticesDirty(); }
        }

        /// <summary>2属性として対角分割するか。</summary>
        public bool IsDualAttribute
        {
            get => isDualAttribute;
            set { isDualAttribute = value; SetVerticesDirty(); }
        }

        /// <summary>薄い背景の不透明度。</summary>
        public float BackgroundAlpha
        {
            get => backgroundAlpha;
            set { backgroundAlpha = Mathf.Clamp(value, 0.12f, 0.18f); SetVerticesDirty(); }
        }

        /// <summary>外周フレームの不透明度。</summary>
        public float BorderAlpha
        {
            get => borderAlpha;
            set { borderAlpha = Mathf.Clamp(value, 0.9f, 1f); SetVerticesDirty(); }
        }

        /// <summary>外周フレームの太さ（Canvas単位）。</summary>
        public float BorderWidth
        {
            get => borderWidth;
            set { borderWidth = Mathf.Clamp(value, 4f, 6f); SetVerticesDirty(); }
        }

        /// <summary>右下側へ実際に出す色。単属性では第一属性色と同じです。</summary>
        public Color EffectiveSecondaryColor =>
            isDualAttribute ? secondaryColor : primaryColor;

        protected override void Awake()
        {
            base.Awake();

            // 属性表示はタップを奪いません。判定は従来どおりカード本体が受けます。
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // 実行時に足された場合でも、入力を奪わない状態から始めます。
            raycastTarget = false;
        }

        /// <summary>
        /// 解決済みの配色をそのまま受け取ります。
        /// 属性から色を決めるのは<see cref="AttributeColorResolver"/>だけです。
        /// </summary>
        public void Apply(Color primary, Color secondary, bool dual)
        {
            primaryColor = primary;
            secondaryColor = secondary;
            isDualAttribute = dual;

            SetVerticesDirty();
        }

        /// <summary>カードの大きさが変わったら描き直します。</summary>
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;

            float b = Mathf.Min(
                borderWidth,
                Mathf.Min(r.width, r.height) * 0.5f);

            Color fillFirst = WithAlpha(primaryColor, backgroundAlpha);
            Color fillSecond = WithAlpha(EffectiveSecondaryColor, backgroundAlpha);
            Color edgeFirst = WithAlpha(primaryColor, borderAlpha);
            Color edgeSecond = WithAlpha(EffectiveSecondaryColor, borderAlpha);

            // 外周
            Vector2 obl = new Vector2(r.xMin, r.yMin);
            Vector2 otl = new Vector2(r.xMin, r.yMax);
            Vector2 otr = new Vector2(r.xMax, r.yMax);
            Vector2 obr = new Vector2(r.xMax, r.yMin);

            // フレームの内側
            Vector2 ibl = new Vector2(r.xMin + b, r.yMin + b);
            Vector2 itl = new Vector2(r.xMin + b, r.yMax - b);
            Vector2 itr = new Vector2(r.xMax - b, r.yMax - b);
            Vector2 ibr = new Vector2(r.xMax - b, r.yMin + b);

            // 薄い背景。内側の矩形を左下→右上の対角線で2枚へ割ります。
            AddTriangle(vh, ibl, itl, itr, fillFirst);
            AddTriangle(vh, ibl, itr, ibr, fillSecond);

            // 外周フレーム。対角線が左下隅と右上隅を通るため、
            // 上辺・左辺が左上側、右辺・下辺が右下側へ丸ごと入ります。
            AddQuad(vh, otl, otr, itr, itl, edgeFirst);
            AddQuad(vh, obl, otl, itl, ibl, edgeFirst);
            AddQuad(vh, otr, obr, ibr, itr, edgeSecond);
            AddQuad(vh, obr, obl, ibl, ibr, edgeSecond);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void AddTriangle(
            VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vh.currentVertCount;

            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);

            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddQuad(
            VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;

            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
