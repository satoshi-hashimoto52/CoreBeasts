using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ユニークスキル発動の演出（Phase 5）。発動した側の文字を囲む「角かっこ」と、文字の上を走る光の帯を描く1枚の Graphic です。
    ///
    /// PLAYER と CPU を同じ Graphic で同時に描けます（片方だけでも描けます）。
    /// Texture・Material・ShaderGraph・外部画像は使わず、実行中に GameObject・Material・List を作りません。
    /// 時間は持たず、<see cref="SetProgress"/> で進み具合（0〜1）を受け取るだけです。
    /// 頂点数は1陣営あたり <see cref="VerticesPerSide"/>（角かっこ4つ×2本 + 光の帯1本、各4頂点）で固定です。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UniqueSkillCueGraphic : MaskableGraphic
    {
        /// <summary>1陣営あたりの四角形の数。</summary>
        public const int QuadsPerSide = 4 * 2 + 1;

        /// <summary>1陣営あたりの頂点数。</summary>
        public const int VerticesPerSide = QuadsPerSide * 4;

        [SerializeField] private Color accent = new Color(1f, 0.84f, 0.32f, 1f);

        [Tooltip("角かっこの線の太さ。")]
        [SerializeField] [Range(1f, 8f)] private float thickness = 4f;

        [Tooltip("角かっこの線の長さ（枠の短い辺に対する割合）。")]
        [SerializeField] [Range(0.1f, 0.5f)] private float cornerLength = 0.35f;

        private struct Side
        {
            internal bool Active;
            internal Rect Frame;
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

        /// <summary>0〜1の進み具合。</summary>
        public float Progress => progress;

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>直近のメッシュで、頂点が文字の枠から外へ出た最大距離。</summary>
        public float LastMaxSpread { get; private set; }

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

        /// <summary>描き始めます。枠はこの RectTransform の座標で受け取ります。</summary>
        public void Begin(bool showPlayer, Rect playerFrame, bool showCpu, Rect cpuFrame)
        {
            player = new Side { Active = showPlayer && playerFrame.width > 0f && playerFrame.height > 0f, Frame = playerFrame };
            cpu = new Side { Active = showCpu && cpuFrame.width > 0f && cpuFrame.height > 0f, Frame = cpuFrame };
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
            LastMaxSpread = 0f;

            if (player.Active)
            {
                BuildSide(vh, player.Frame, progress);
            }

            if (cpu.Active)
            {
                BuildSide(vh, cpu.Frame, progress);
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private void ClearState()
        {
            player = default;
            cpu = default;
            progress = 0f;

            if (!IsActive())
            {
                LastVertexCount = 0;
                LastMaxSpread = 0f;
            }
        }

        /// <summary>
        /// 角かっこは外側から文字へ寄りながら現れ、光の帯は左から右へ走ります。
        /// 外へ出る量は <see cref="BattleSkillPresentationPlan.MaxFrameSpread"/> までです。
        /// </summary>
        private void BuildSide(VertexHelper vh, Rect frame, float p)
        {
            float alpha = p < 0.2f ? p / 0.2f : p > 0.7f ? 1f - (p - 0.7f) / 0.3f : 1f;
            alpha = Mathf.Clamp01(alpha);

            float spread = BattleSkillPresentationPlan.MaxFrameSpread * (1f - EaseOutCubic(Mathf.Clamp01(p / 0.5f)));
            Rect r = new Rect(frame.xMin - spread, frame.yMin - spread, frame.width + spread * 2f, frame.height + spread * 2f);

            float t = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.25f);
            float len = Mathf.Min(r.width, r.height) * cornerLength;
            Color c = WithAlpha(accent, alpha);

            // 角かっこ（左下・右下・左上・右上）。それぞれ横線と縦線の2本です。
            Corner(vh, frame, new Vector2(r.xMin, r.yMin), 1f, 1f, len, t, c);
            Corner(vh, frame, new Vector2(r.xMax, r.yMin), -1f, 1f, len, t, c);
            Corner(vh, frame, new Vector2(r.xMin, r.yMax), 1f, -1f, len, t, c);
            Corner(vh, frame, new Vector2(r.xMax, r.yMax), -1f, -1f, len, t, c);

            // 光の帯: 文字の枠の内側を左から右へ走ります。
            float bandWidth = Mathf.Max(t * 2f, frame.width * 0.12f);
            float x = Mathf.Lerp(frame.xMin, frame.xMax - bandWidth, Mathf.Clamp01(p));
            Color band = WithAlpha(accent, alpha * 0.35f);

            AddQuad(vh, frame, new Vector2(x, frame.yMin), new Vector2(x + bandWidth, frame.yMax), band);
        }

        private void Corner(VertexHelper vh, Rect frame, Vector2 at, float sx, float sy, float len, float t, Color c)
        {
            // 横線
            AddQuad(vh, frame, at, at + new Vector2(sx * len, sy * t), c);

            // 縦線
            AddQuad(vh, frame, at, at + new Vector2(sx * t, sy * len), c);
        }

        private void AddQuad(VertexHelper vh, Rect frame, Vector2 a, Vector2 b, Color c)
        {
            float x0 = Mathf.Min(a.x, b.x);
            float x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y);
            float y1 = Mathf.Max(a.y, b.y);
            int start = vh.currentVertCount;
            Color tinted = c * color;

            AddVertex(vh, frame, new Vector2(x0, y0), tinted);
            AddVertex(vh, frame, new Vector2(x0, y1), tinted);
            AddVertex(vh, frame, new Vector2(x1, y1), tinted);
            AddVertex(vh, frame, new Vector2(x1, y0), tinted);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper vh, Rect frame, Vector2 position, Color c)
        {
            float dx = Mathf.Max(frame.xMin - position.x, position.x - frame.xMax, 0f);
            float dy = Mathf.Max(frame.yMin - position.y, position.y - frame.yMax, 0f);
            float spread = Mathf.Max(dx, dy);

            if (spread > LastMaxSpread)
            {
                LastMaxSpread = spread;
            }

            vh.AddVert(new Vector3(position.x, position.y, 0f), c, Vector4.zero);
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a *= Mathf.Clamp01(alpha);
            return c;
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }
    }
}
