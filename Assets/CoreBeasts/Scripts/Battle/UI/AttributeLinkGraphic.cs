using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ATTRIBUTE LINK の演出（Phase 4C）。立ち絵のまわりに属性エネルギーを描く1枚の Graphic です。
    ///
    /// PLAYER と CPU の両方を同じ Graphic で同時に描けます（片方だけでも描けます）。
    /// 共有した属性が1色ならその色、2色なら弧ごとに2色を交互に使い、1色へ決めつけません。
    ///
    /// Texture・Material・ShaderGraph・外部画像は使いません。実行中に GameObject・Material・List を作らず、
    /// 時間は持たずに <see cref="SetProgress"/> で進み具合（0〜1）を受け取るだけです。
    /// 頂点数は1陣営あたり <see cref="VerticesPerSide"/>（弧12本×4 + 火花8個×4）で固定です。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttributeLinkGraphic : MaskableGraphic
    {
        /// <summary>立ち絵を囲む弧の数。</summary>
        public const int Arcs = 12;

        /// <summary>立ち上る火花の数。</summary>
        public const int Sparks = 8;

        /// <summary>1陣営あたりの頂点数。</summary>
        public const int VerticesPerSide = Arcs * 4 + Sparks * 4;

        private struct Side
        {
            internal bool Active;
            internal Vector2 Centre;
            internal float Radius;
            internal Color First;
            internal Color Second;
            internal bool Dual;
            internal float Spin;
        }

        private Side player;
        private Side cpu;
        private float progress;

        /// <summary>何かを表示中か。</summary>
        public bool IsShowing => player.Active || cpu.Active;

        /// <summary>プレイヤー側を描いているか。</summary>
        public bool IsShowingPlayer => player.Active;

        /// <summary>CPU側を描いているか。</summary>
        public bool IsShowingCpu => cpu.Active;

        /// <summary>このRectTransformの座標でのプレイヤー側の中心。</summary>
        public Vector2 PlayerCentre => player.Centre;

        /// <summary>このRectTransformの座標でのCPU側の中心。</summary>
        public Vector2 CpuCentre => cpu.Centre;

        /// <summary>0〜1の進み具合。</summary>
        public float Progress => progress;

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>直近のメッシュで、各陣営の中心から最も遠い頂点までの距離。</summary>
        public float LastMaxDistance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            ClearState();
            base.OnDisable();
        }

        /// <summary>
        /// 描き始めます。中心と半径はワールド座標で受け取り、このRectTransformの座標へ直します。
        /// 2色目が無いときは <paramref name="playerSecond"/> に1色目と同じ色を渡し、<paramref name="playerDual"/>を false にします。
        /// </summary>
        public void Begin(
            bool showPlayer, Vector3 playerCentreWorld, float playerRadius, Color playerFirst, Color playerSecond, bool playerDual,
            bool showCpu, Vector3 cpuCentreWorld, float cpuRadius, Color cpuFirst, Color cpuSecond, bool cpuDual)
        {
            player = Make(showPlayer, playerCentreWorld, playerRadius, playerFirst, playerSecond, playerDual, -1f);
            cpu = Make(showCpu, cpuCentreWorld, cpuRadius, cpuFirst, cpuSecond, cpuDual, 1f);
            progress = 0f;

            SetVerticesDirty();
        }

        /// <summary>進み具合を更新します。1以上で消えます。</summary>
        public void SetProgress(float value)
        {
            if (!IsShowing)
            {
                return;
            }

            if (value >= 1f)
            {
                Hide();
                return;
            }

            progress = Mathf.Clamp01(value);
            SetVerticesDirty();
        }

        /// <summary>即座に消します。何度呼んでも安全です。</summary>
        public void Hide()
        {
            bool was = IsShowing || LastVertexCount > 0;

            ClearState();

            if (was)
            {
                SetVerticesDirty();
            }
        }

        /// <summary>今の状態でメッシュを作ります。テストからも同じ経路で頂点を確かめられます。</summary>
        public void FillMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVertexCount = 0;
            LastMaxDistance = 0f;

            if (player.Active)
            {
                BuildSide(vh, player, progress);
            }

            if (cpu.Active)
            {
                BuildSide(vh, cpu, progress);
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private Side Make(bool show, Vector3 centreWorld, float radius, Color first, Color second, bool dual, float spin)
        {
            if (!show || radius <= 0f)
            {
                return default;
            }

            Vector3 local = rectTransform.InverseTransformPoint(centreWorld);

            return new Side
            {
                Active = true,
                Centre = new Vector2(local.x, local.y),
                Radius = Mathf.Min(radius, BattleAttributeLinkPresentationPlan.MaxRadius),
                First = first,
                Second = dual ? second : first,
                Dual = dual,
                Spin = spin,
            };
        }

        private void ClearState()
        {
            player = default;
            cpu = default;
            progress = 0f;

            if (!IsActive())
            {
                LastVertexCount = 0;
                LastMaxDistance = 0f;
            }
        }

        /// <summary>立ち絵を囲んで回る弧と、立ち上る火花。外へ少し広がりながら現れて消えます。</summary>
        private void BuildSide(VertexHelper vh, Side side, float p)
        {
            float alpha = p < 0.2f ? p / 0.2f : p > 0.65f ? 1f - (p - 0.65f) / 0.35f : 1f;
            alpha = Mathf.Clamp01(alpha);

            float grow = 0.85f + 0.15f * EaseOutCubic(p);
            float outer = side.Radius * grow;
            float inner = outer * 0.86f;
            float rotation = side.Spin * p * 60f * Mathf.Deg2Rad;

            for (int i = 0; i < Arcs; i++)
            {
                float a0 = i * (Mathf.PI * 2f / Arcs) + rotation;
                float a1 = a0 + (Mathf.PI * 2f / Arcs) * 0.66f;

                // 2色共有なら弧ごとに交互、1色ならその色だけです。
                Color c = WithAlpha((side.Dual && (i & 1) == 1) ? side.Second : side.First, alpha);

                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));

                AddQuad(vh, side.Centre, d0 * inner, d0 * outer, d1 * outer, d1 * inner, c);
            }

            // 火花: 立ち絵のまわりから少しずつ外へ立ち上ります。
            float sparkHalf = Mathf.Min(6f, side.Radius * 0.05f);

            for (int j = 0; j < Sparks; j++)
            {
                float angle = j * (Mathf.PI * 2f / Sparks) + Mathf.PI / Sparks - rotation;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 at = dir * (side.Radius * (0.55f + 0.3f * p));

                Color c = WithAlpha((side.Dual && (j & 1) == 0) ? side.Second : side.First, alpha * 0.9f);

                AddQuad(
                    vh,
                    side.Centre,
                    at + new Vector2(0f, sparkHalf * 1.6f),
                    at + new Vector2(sparkHalf, 0f),
                    at + new Vector2(0f, -sparkHalf * 1.6f),
                    at + new Vector2(-sparkHalf, 0f),
                    c);
            }
        }

        private void AddQuad(VertexHelper vh, Vector2 centre, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color vertexColor)
        {
            int start = vh.currentVertCount;

            AddVertex(vh, centre, a, vertexColor);
            AddVertex(vh, centre, b, vertexColor);
            AddVertex(vh, centre, c, vertexColor);
            AddVertex(vh, centre, d, vertexColor);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper vh, Vector2 centre, Vector2 offset, Color vertexColor)
        {
            float distance = offset.magnitude;

            if (distance > LastMaxDistance)
            {
                LastMaxDistance = distance;
            }

            vh.AddVert(new Vector3(centre.x + offset.x, centre.y + offset.y, 0f), vertexColor * color, Vector4.zero);
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }
    }
}
