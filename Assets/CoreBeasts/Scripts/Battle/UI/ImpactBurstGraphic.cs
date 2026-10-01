using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 決着エフェクト（Phase 2「属性別インパクト」）をコード生成メッシュで描く1枚のGraphic。
    ///
    /// Texture・Material・ShaderGraph・外部画像は使いません。<see cref="MaskableGraphic"/>が
    /// 既定で使う UI/Default へ頂点を流し込むだけです。
    ///
    /// シーンに1つだけ置き、全ラウンドで使い回します。実行中に GameObject・Material・List を
    /// 作りません。頂点は Unity が貸し出す<see cref="VertexHelper"/>へ直接書きます。
    /// 種類ごとの頂点数は固定で、<see cref="AttributeEffectProfile.MaxVertices"/>を超えません。
    ///
    /// 時間は持ちません。<see cref="BattleFxPlayer"/>が Time.unscaledDeltaTime で数えた進み具合を
    /// <see cref="SetProgress"/>で受け取るだけなので、Coroutine を別に走らせません。
    ///
    /// 形（衝突点からの距離はすべて<see cref="AttributeEffectProfile.MaxRadius"/>以内）:
    ///   Red   : 長さの揃わない12本の鋭い放射スパイク＋X字の斬撃2本。一気に伸びてすぐ消える
    ///   Blue  : 滑らかに広がる2重の円形衝撃波。外へ向かって薄くなる
    ///   Green : 螺旋に回りながら広がる葉10枚と、逆回りの粒子10個
    ///   Power : 大きな白金リングが内側へ押し潰れ、中心に金の塊が残る（外→内の動き）
    ///   Core  : 白金色のコアフラッシュ（円盤＋細いリング）だけ
    ///   Draw  : 薄い灰色の円形衝撃波と、45度ごとの対称な8本の筋
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ImpactBurstGraphic : MaskableGraphic
    {
        private const int RedSpikes = 12;
        private const int BlueSegments = 32;
        private const int GreenLeaves = 10;
        private const int PowerRingSegments = 32;
        private const int PowerCoreSegments = 16;
        private const int CoreDiscSegments = 20;
        private const int CoreRingSegments = 20;
        private const int DrawSegments = 24;
        private const int DrawRays = 8;

        /// <summary>REDのスパイクの長さ比。揃えないことで「火花」に見せます。</summary>
        private static readonly float[] RedSpikeLengths =
        {
            1f, 0.62f, 0.86f, 0.55f, 0.95f, 0.68f, 0.80f, 0.58f, 0.92f, 0.64f, 0.84f, 0.60f,
        };

        /// <summary>REDのスパイクの角度ずれ（度）。等間隔の放射にしないためです。</summary>
        private static readonly float[] RedSpikeJitter =
        {
            0f, 6f, -4f, 3f, -7f, 5f, -2f, 7f, -5f, 2f, -6f, 4f,
        };

        private BattleImpactKind kind = BattleImpactKind.None;
        private AttributeEffectProfile profile = AttributeEffectProfile.None;
        private float progress;
        private Vector2 centre;
        private Color innerColor = Color.white;
        private Color outerColor = Color.white;

        /// <summary>いま出している種類。出していなければ<see cref="BattleImpactKind.None"/>です。</summary>
        public BattleImpactKind Kind => kind;

        /// <summary>0〜1の進み具合。</summary>
        public float Progress => progress;

        /// <summary>このRectTransformの座標での衝突点。</summary>
        public Vector2 Centre => centre;

        /// <summary>何かを表示中か。</summary>
        public bool IsShowing => kind != BattleImpactKind.None;

        /// <summary>直近に作ったメッシュの頂点数。何も描いていなければ0です。</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>直近のメッシュで、衝突点から最も遠い頂点までの距離。</summary>
        public float LastMaxDistance { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            // 装飾です。入力を奪いません。
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            // 非表示になった時点で状態も捨てます。再表示で前の続きが出ません。
            ClearState();

            base.OnDisable();
        }

        /// <summary>
        /// 衝突点（ワールド座標）から<paramref name="impact"/>を始めます。
        /// 端末解像度や Canvas の scaleFactor は仮定せず、このRectTransformの座標へ直します。
        /// </summary>
        public void Begin(BattleImpactKind impact, Vector3 contactWorld)
        {
            AttributeEffectProfile next = AttributeEffectProfile.For(impact);

            if (!next.IsVisible)
            {
                Hide();
                return;
            }

            kind = next.Kind;
            profile = next;
            progress = 0f;

            Vector3 local = rectTransform.InverseTransformPoint(contactWorld);
            centre = new Vector2(local.x, local.y);

            innerColor = ToColor(profile.InnerColor);
            outerColor = ToColor(profile.OuterColor);

            SetVerticesDirty();
        }

        /// <summary>進み具合を更新します。1以上で消えます。</summary>
        public void SetProgress(float value)
        {
            if (kind == BattleImpactKind.None)
            {
                return;
            }

            if (value >= 1f)
            {
                Hide();
                return;
            }

            float clamped = Mathf.Clamp01(value);

            if (Mathf.Approximately(clamped, progress))
            {
                return;
            }

            progress = clamped;

            SetVerticesDirty();
        }

        /// <summary>即座に消します。何度呼んでも安全です。</summary>
        public void Hide()
        {
            bool wasShowing = kind != BattleImpactKind.None || LastVertexCount > 0;

            ClearState();

            if (wasShowing)
            {
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 今の状態でメッシュを作ります。<see cref="OnPopulateMesh"/>から呼ばれ、
        /// テストからも同じ経路で頂点を確かめられます。
        /// </summary>
        public void FillMesh(VertexHelper vh)
        {
            vh.Clear();

            LastVertexCount = 0;
            LastMaxDistance = 0f;

            if (kind == BattleImpactKind.None)
            {
                return;
            }

            float p = progress;
            float radius = profile.MaxRadius;

            switch (kind)
            {
                case BattleImpactKind.Red:
                    BuildRed(vh, p, radius);
                    break;

                case BattleImpactKind.Blue:
                    BuildBlue(vh, p, radius);
                    break;

                case BattleImpactKind.Green:
                    BuildGreen(vh, p, radius);
                    break;

                case BattleImpactKind.Power:
                    BuildPower(vh, p, radius);
                    break;

                case BattleImpactKind.Core:
                    BuildCore(vh, p, radius);
                    break;

                case BattleImpactKind.Draw:
                    BuildDraw(vh, p, radius);
                    break;
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private void ClearState()
        {
            kind = BattleImpactKind.None;
            profile = AttributeEffectProfile.None;
            progress = 0f;

            if (!IsActive())
            {
                // 無効中は再構築が走らないため、ここで空にしておきます。
                LastVertexCount = 0;
                LastMaxDistance = 0f;
            }
        }

        // ---------------- 種類ごとの形 ----------------

        /// <summary>RED: 一気に伸びる鋭いスパイクと X 字の斬撃。短く強く。</summary>
        private void BuildRed(VertexHelper vh, float p, float radius)
        {
            float grow = EaseOutQuart(Mathf.Clamp01(p / 0.4f));
            float alpha = 1f - SmoothStep(Mathf.Clamp01((p - 0.3f) / 0.7f));
            float baseHalfWidth = 8f * (1f - 0.5f * p);

            Color hot = WithAlpha(innerColor, alpha);
            Color tipColor = WithAlpha(outerColor, alpha * 0.9f);

            for (int i = 0; i < RedSpikes; i++)
            {
                float angle = (i * (360f / RedSpikes) + RedSpikeJitter[i]) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = new Vector2(-dir.y, dir.x);

                float length = radius * RedSpikeLengths[i] * grow;
                float start = length * 0.12f;

                AddTriangle(
                    vh,
                    dir * start + side * baseHalfWidth, hot,
                    dir * start - side * baseHalfWidth, hot,
                    dir * length, tipColor);
            }

            // X 字の斬撃。中心を通る細長いひし形を2本。
            float slashHalf = radius * 0.95f * grow;
            float slashWidth = 5f * (1f - 0.6f * p);
            Color slashColor = WithAlpha(Color.Lerp(innerColor, Color.white, 0.35f), alpha);

            AddSlash(vh, 30f, slashHalf, slashWidth, slashColor, tipColor);
            AddSlash(vh, 150f, slashHalf, slashWidth, slashColor, tipColor);
        }

        /// <summary>BLUE: 滑らかに広がる2重の円形衝撃波。</summary>
        private void BuildBlue(VertexHelper vh, float p, float radius)
        {
            float alpha = Mathf.Pow(1f - p, 1.4f);

            float outer1 = radius * EaseOutCubic(p);
            float thick1 = Mathf.Lerp(26f, 4f, p);

            AddRing(
                vh, outer1, thick1, BlueSegments,
                WithAlpha(innerColor, alpha), WithAlpha(outerColor, alpha * 0.8f));

            float p2 = Mathf.Clamp01((p - 0.12f) / 0.88f);
            float outer2 = radius * 0.72f * EaseOutCubic(p2);
            float thick2 = Mathf.Lerp(12f, 3f, p);

            AddRing(
                vh, outer2, thick2, BlueSegments,
                WithAlpha(outerColor, alpha * 0.9f), WithAlpha(innerColor, alpha * 0.6f));
        }

        /// <summary>GREEN: 螺旋に回りながら舞い広がる葉と、逆回りの粒子。</summary>
        private void BuildGreen(VertexHelper vh, float p, float radius)
        {
            const float leafHalfLength = 13f;
            const float leafHalfWidth = 5f;
            const float moteHalf = 4f;

            float spread = EaseOutCubic(p);
            float alpha = 1f - SmoothStep(Mathf.Clamp01((p - 0.35f) / 0.65f));

            Color leafTip = WithAlpha(outerColor, alpha);
            Color leafSide = WithAlpha(innerColor, alpha);
            Color mote = WithAlpha(innerColor, alpha * 0.85f);

            for (int i = 0; i < GreenLeaves; i++)
            {
                float reach = (i & 1) == 0 ? 0.92f : 0.66f;

                // 葉: 外へ出ながら反時計回りに回ります。
                float angle = (i * (360f / GreenLeaves) + 160f * spread) * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 leafCentre = radial * ((radius - leafHalfLength) * spread * reach);

                // 接線から少し外へ傾けて、螺旋の流れに見せます。
                float tilt = angle + (90f - 35f) * Mathf.Deg2Rad;
                Vector2 along = new Vector2(Mathf.Cos(tilt), Mathf.Sin(tilt));
                Vector2 across = new Vector2(-along.y, along.x);

                AddQuad(
                    vh,
                    leafCentre + along * leafHalfLength, leafTip,
                    leafCentre + across * leafHalfWidth, leafSide,
                    leafCentre - along * leafHalfLength, leafTip,
                    leafCentre - across * leafHalfWidth, leafSide);

                // 粒子: 葉と逆回りで、少し内側に。
                float moteAngle = (i * (360f / GreenLeaves) + 18f - 100f * spread) * Mathf.Deg2Rad;
                Vector2 moteCentre =
                    new Vector2(Mathf.Cos(moteAngle), Mathf.Sin(moteAngle)) *
                    ((radius - moteHalf * 1.5f) * spread * (reach > 0.8f ? 0.7f : 1f) * 0.8f);

                AddQuad(
                    vh,
                    moteCentre + new Vector2(-moteHalf, -moteHalf), mote,
                    moteCentre + new Vector2(-moteHalf, moteHalf), mote,
                    moteCentre + new Vector2(moteHalf, moteHalf), mote,
                    moteCentre + new Vector2(moteHalf, -moteHalf), mote);
            }
        }

        /// <summary>POWER: 大きな白金リングが内側へ押し潰れ、中心に金の塊が残る。</summary>
        private void BuildPower(VertexHelper vh, float p, float radius)
        {
            float squeeze = EaseInCubic(p);
            float outer = Mathf.Lerp(radius, radius * 0.28f, squeeze);
            float thick = Mathf.Min(Mathf.Lerp(6f, 30f, squeeze), outer);
            float ringAlpha = 1f - SmoothStep(Mathf.Clamp01((p - 0.6f) / 0.4f));

            // 外縁は白、内縁は金。押し込まれる向きが分かるようにします。
            AddRing(
                vh, outer, thick, PowerRingSegments,
                WithAlpha(outerColor, ringAlpha), WithAlpha(innerColor, ringAlpha));

            float coreT = Mathf.Clamp01((p - 0.45f) / 0.55f);
            float coreRadius = radius * 0.34f * EaseOutCubic(coreT);
            float coreAlpha = p < 0.45f ? 0f : 1f - coreT * coreT;

            AddDisc(
                vh, coreRadius, PowerCoreSegments,
                WithAlpha(innerColor, coreAlpha), WithAlpha(outerColor, coreAlpha * 0.7f));
        }

        /// <summary>CORE: 簡素な白金色のコアフラッシュだけ。</summary>
        private void BuildCore(VertexHelper vh, float p, float radius)
        {
            float grow = EaseOutCubic(p);
            float discAlpha = (1f - p) * (1f - p);
            float ringAlpha = 1f - p;

            AddDisc(
                vh, radius * 0.55f * grow, CoreDiscSegments,
                WithAlpha(innerColor, discAlpha), WithAlpha(outerColor, discAlpha * 0.5f));

            AddRing(
                vh, radius * grow, 6f, CoreRingSegments,
                WithAlpha(outerColor, ringAlpha), WithAlpha(innerColor, ringAlpha * 0.6f));
        }

        /// <summary>DRAW: 勝者色を持たない、上下左右対称の灰色の衝撃波。</summary>
        private void BuildDraw(VertexHelper vh, float p, float radius)
        {
            const float rayHalfWidth = 3f;

            float outer = radius * EaseOutCubic(p);
            float thick = Mathf.Lerp(16f, 3f, p);
            float alpha = 1f - SmoothStep(p);

            AddRing(
                vh, outer, thick, DrawSegments,
                WithAlpha(innerColor, alpha), WithAlpha(outerColor, alpha * 0.8f));

            Color rayColor = WithAlpha(outerColor, alpha * 0.9f);
            float from = outer * 0.35f;
            float to = outer * 0.85f;

            for (int i = 0; i < DrawRays; i++)
            {
                float angle = i * (360f / DrawRays) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = new Vector2(-dir.y, dir.x) * rayHalfWidth;

                AddQuad(
                    vh,
                    dir * from - side, rayColor,
                    dir * from + side, rayColor,
                    dir * to + side, rayColor,
                    dir * to - side, rayColor);
            }
        }

        // ---------------- 頂点の追加（割り当てなし） ----------------

        private void AddSlash(
            VertexHelper vh, float degrees, float halfLength, float halfWidth, Color middle, Color tip)
        {
            float angle = degrees * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-dir.y, dir.x);

            AddQuad(
                vh,
                dir * halfLength, tip,
                side * halfWidth, middle,
                -dir * halfLength, tip,
                -side * halfWidth, middle);
        }

        /// <summary>中心からの円環。頂点は 2×segments で、継ぎ目で重複させません。</summary>
        private void AddRing(
            VertexHelper vh, float outer, float thickness, int segments, Color innerEdge, Color outerEdge)
        {
            float inner = Mathf.Max(0f, outer - thickness);
            int start = vh.currentVertCount;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                AddVertex(vh, dir * inner, innerEdge);
                AddVertex(vh, dir * outer, outerEdge);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % segments) * 2;

                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        /// <summary>中心1点＋外周 segments 点の円盤。</summary>
        private void AddDisc(VertexHelper vh, float radius, int segments, Color middle, Color edge)
        {
            int start = vh.currentVertCount;

            AddVertex(vh, Vector2.zero, middle);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);

                AddVertex(vh, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, edge);
            }

            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
            }
        }

        private void AddTriangle(
            VertexHelper vh, Vector2 a, Color ca, Vector2 b, Color cb, Vector2 c, Color cc)
        {
            int start = vh.currentVertCount;

            AddVertex(vh, a, ca);
            AddVertex(vh, b, cb);
            AddVertex(vh, c, cc);

            vh.AddTriangle(start, start + 1, start + 2);
        }

        private void AddQuad(
            VertexHelper vh,
            Vector2 a, Color ca,
            Vector2 b, Color cb,
            Vector2 c, Color cc,
            Vector2 d, Color cd)
        {
            int start = vh.currentVertCount;

            AddVertex(vh, a, ca);
            AddVertex(vh, b, cb);
            AddVertex(vh, c, cc);
            AddVertex(vh, d, cd);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        /// <summary>衝突点からの相対位置で頂点を足し、最遠距離を控えます。</summary>
        private void AddVertex(VertexHelper vh, Vector2 offset, Color vertexColor)
        {
            float distance = offset.magnitude;

            if (distance > LastMaxDistance)
            {
                LastMaxDistance = distance;
            }

            vh.AddVert(
                new Vector3(centre.x + offset.x, centre.y + offset.y, 0f),
                vertexColor * color,
                Vector4.zero);
        }

        // ---------------- 補助 ----------------

        private static Color ToColor(uint rgb)
        {
            AttributeEffectProfile.Unpack(rgb, out float r, out float g, out float b);

            return new Color(r, g, b, 1f);
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseOutQuart(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u * u;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }
    }
}
