using CoreBeasts.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.HomeUI
{
    /// <summary>
    /// ガチャ演出の図形（Phase 7）。コア・圧縮・リング展開・粒子・白金フラッシュを1枚の Graphic で描きます。
    ///
    /// 時間は持たず、<see cref="Show"/> で段階と進み具合を受け取るだけです。描くのはこの RectTransform の中だけで、
    /// フラッシュも中央の円盤（最大 <see cref="GachaSummonPlan.MaxFlashAlpha"/>）なので画面全体を白くしません。
    /// Texture・Material は使わず、頂点数は <see cref="GachaSummonPlan.VertexBudget"/> 以下で固定です。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GachaSummonGraphic : MaskableGraphic
    {
        /// <summary>円の分割数。</summary>
        public const int Segments = 24;

        /// <summary>粒子の数。</summary>
        public const int Sparks = 12;

        /// <summary>コア（中心+外周）+ リング2本 + 粒子 + フラッシュ（中心+外周）。</summary>
        public const int MaxVertices = (Segments + 1) + 2 * (Segments * 2) + Sparks * 4 + (Segments + 1);

        private static readonly Color CoreColor = new Color(0.36f, 0.86f, 0.96f, 1f);
        private static readonly Color RingColor = new Color(0.44f, 0.72f, 1f, 1f);
        private static readonly Color Platinum = new Color(1f, 0.93f, 0.74f, 1f);

        private GachaSummonPhase phase = GachaSummonPhase.Done;
        private float progress;
        private float flashAlpha;

        /// <summary>何かを描いているか。</summary>
        public bool IsShowing => phase != GachaSummonPhase.Done && phase != GachaSummonPhase.Plain;

        public GachaSummonPhase Phase => phase;

        public float FlashAlpha => flashAlpha;

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>直近のメッシュで、中心から最も遠い頂点までの距離。</summary>
        public float LastMaxDistance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            phase = GachaSummonPhase.Done;
            progress = 0f;
            flashAlpha = 0f;
            base.OnDisable();
        }

        /// <summary>段階・段階内の進み具合・フラッシュの不透明度を受け取ります。</summary>
        public void Show(GachaSummonPhase value, float phaseProgress, float flash)
        {
            phase = value;
            progress = Mathf.Clamp01(phaseProgress);
            flashAlpha = Mathf.Clamp(flash, 0f, GachaSummonPlan.MaxFlashAlpha);
            SetVerticesDirty();
        }

        /// <summary>即座に消します。</summary>
        public void Hide()
        {
            Show(GachaSummonPhase.Done, 0f, 0f);
        }

        /// <summary>今の状態でメッシュを作ります。テストからも同じ経路で頂点を確かめられます。</summary>
        public void FillMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVertexCount = 0;
            LastMaxDistance = 0f;

            if (!IsShowing)
            {
                return;
            }

            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float max = Mathf.Min(r.width, r.height) * 0.5f;

            if (max <= 0f)
            {
                return;
            }

            float coreRadius;
            float coreAlpha;

            switch (phase)
            {
                case GachaSummonPhase.Idle:
                    // 待機: 小さく脈打つ。
                    coreRadius = max * (0.22f + 0.02f * Mathf.Sin(progress * Mathf.PI * 2f));
                    coreAlpha = 0.55f + 0.25f * progress;
                    break;
                case GachaSummonPhase.Compress:
                    // 圧縮: 縮みながら明るくなる。
                    coreRadius = max * Mathf.Lerp(0.22f, 0.08f, progress);
                    coreAlpha = Mathf.Lerp(0.8f, 1f, progress);
                    break;
                case GachaSummonPhase.Ring:
                    coreRadius = max * Mathf.Lerp(0.08f, 0.14f, progress);
                    coreAlpha = 1f;
                    break;
                default:
                    coreRadius = max * 0.14f * (1f - progress);
                    coreAlpha = 1f - progress;
                    break;
            }

            Disc(vh, c, coreRadius, WithAlpha(CoreColor, coreAlpha));

            if (phase == GachaSummonPhase.Ring || phase == GachaSummonPhase.Flash)
            {
                float t = phase == GachaSummonPhase.Ring ? progress : 1f;
                float fade = phase == GachaSummonPhase.Flash ? 1f - progress : 1f;

                Ring(vh, c, max * Mathf.Lerp(0.15f, 0.92f, EaseOut(t)), 5f, WithAlpha(RingColor, 0.85f * fade));
                Ring(vh, c, max * Mathf.Lerp(0.1f, 0.62f, EaseOut(t)), 3f, WithAlpha(Platinum, 0.7f * fade));

                for (int i = 0; i < Sparks; i++)
                {
                    float angle = i * Mathf.PI * 2f / Sparks + t * 0.6f;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float distance = max * Mathf.Lerp(0.2f, 0.95f, EaseOut(t));
                    float size = Mathf.Max(3f, max * 0.025f);

                    Quad(vh, c + dir * distance, size, WithAlpha(i % 2 == 0 ? Platinum : RingColor, 0.9f * fade));
                }
            }

            if (flashAlpha > 0f)
            {
                Disc(vh, c, max * 0.95f, WithAlpha(Platinum, flashAlpha));
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private void Disc(VertexHelper vh, Vector2 c, float radius, Color col)
        {
            int start = vh.currentVertCount;

            AddVertex(vh, c, c, col);

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                AddVertex(vh, c, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, WithAlpha(col, col.a * 0.6f));
            }

            for (int i = 0; i < Segments; i++)
            {
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
            }
        }

        private void Ring(VertexHelper vh, Vector2 c, float radius, float width, Color col)
        {
            int start = vh.currentVertCount;

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                AddVertex(vh, c, c + d * (radius - width), col);
                AddVertex(vh, c, c + d * radius, col);
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + i * 2;
                int b = start + (i + 1) % Segments * 2;

                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        private void Quad(VertexHelper vh, Vector2 at, float half, Color col)
        {
            int start = vh.currentVertCount;
            Vector2 c = rectTransform.rect.center;

            AddVertex(vh, c, at + new Vector2(0f, half), col);
            AddVertex(vh, c, at + new Vector2(half, 0f), col);
            AddVertex(vh, c, at + new Vector2(0f, -half), col);
            AddVertex(vh, c, at + new Vector2(-half, 0f), col);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper vh, Vector2 centre, Vector2 position, Color col)
        {
            float distance = (position - centre).magnitude;

            if (distance > LastMaxDistance)
            {
                LastMaxDistance = distance;
            }

            vh.AddVert(position, col * color, Vector4.zero);
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private static float EaseOut(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }
    }
}
