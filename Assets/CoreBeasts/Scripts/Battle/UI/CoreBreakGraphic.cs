using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// Phase 3 の試合演出（勝利コア・FINAL CORE・CORE BREAK）をコード生成メッシュで描く Graphic。
    ///
    /// Texture・Material・ShaderGraph・外部画像は使いません。UI/Default へ頂点を流すだけです。
    /// シーンに置いた1枚を使い回し、実行中に GameObject・Material・List を作りません。
    /// 時間は持たず、<see cref="SetProgress"/>で進み具合（0〜1）を受け取るだけです。
    ///
    /// 頂点数はモードごとに固定です（<see cref="VerticesOf"/>）。
    ///   VictoryCore : 円盤16分割(17) + リング16分割(32)                 =  49
    ///   FinalCore   : 外リング40分割(80) + 内リング24分割(48)            = 128
    ///   CoreBreak   : コア円盤20分割(21) + 圧縮リング32分割(64)
    ///                 + 亀裂14本(56) + 白金フラッシュ円盤16分割(17)      = 158
    /// どれも<see cref="BattleMatchPresentationPlan.VertexBudget"/>（160）以下です。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CoreBreakGraphic : MaskableGraphic
    {
        /// <summary>描く演出。</summary>
        public enum Mode
        {
            None = 0,
            VictoryCore = 1,
            FinalCore = 2,
            CoreBreak = 3,
        }

        private const int VictoryDiscSegments = 16;
        private const int VictoryRingSegments = 16;
        private const int FinalOuterSegments = 40;
        private const int FinalInnerSegments = 24;
        private const int BreakCoreSegments = 20;
        private const int BreakRingSegments = 32;
        private const int BreakCracks = 14;
        private const int BreakFlashSegments = 16;

        /// <summary>白金色（中心側）。</summary>
        public static readonly Color WhiteGold = new Color(1f, 0.973f, 0.902f, 1f);

        /// <summary>金色（外側）。</summary>
        public static readonly Color Gold = new Color(1f, 0.824f, 0.302f, 1f);

        /// <summary>亀裂の角度ずれ（度）。まっすぐな放射にしないためです。</summary>
        private static readonly float[] CrackJitter =
        {
            7f, -5f, 9f, -8f, 4f, -10f, 6f, -4f, 10f, -7f, 5f, -9f, 8f, -6f,
        };

        /// <summary>亀裂の長さ比。</summary>
        private static readonly float[] CrackLengths =
        {
            1f, 0.72f, 0.9f, 0.64f, 0.98f, 0.78f, 0.86f, 0.68f, 0.95f, 0.74f, 0.88f, 0.66f, 0.92f, 0.8f,
        };

        private Mode mode = Mode.None;
        private float progress;
        private float baseRadius;
        private Vector2 centre;

        /// <summary>いま描いているモード。</summary>
        public Mode Current => mode;

        /// <summary>何かを表示中か。</summary>
        public bool IsShowing => mode != Mode.None;

        /// <summary>0〜1の進み具合。</summary>
        public float Progress => progress;

        /// <summary>このRectTransformの座標での中心。</summary>
        public Vector2 Centre => centre;

        /// <summary>勝利コアの基準半径（ピップの半分の大きさ）。</summary>
        public float BaseRadius => baseRadius;

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>直近のメッシュで、中心から最も遠い頂点までの距離。</summary>
        public float LastMaxDistance { get; private set; }

        /// <summary>直近のメッシュで最も高かった不透明度。</summary>
        public float LastMaxAlpha { get; private set; }

        /// <summary>モードごとの頂点数。</summary>
        public static int VerticesOf(Mode target)
        {
            switch (target)
            {
                case Mode.VictoryCore:
                    return (VictoryDiscSegments + 1) + VictoryRingSegments * 2;

                case Mode.FinalCore:
                    return FinalOuterSegments * 2 + FinalInnerSegments * 2;

                case Mode.CoreBreak:
                    return (BreakCoreSegments + 1) + BreakRingSegments * 2 + BreakCracks * 4 + (BreakFlashSegments + 1);

                default:
                    return 0;
            }
        }

        /// <summary>モードごとの、中心から頂点までの最大距離。</summary>
        public float MaxRadiusOf(Mode target)
        {
            switch (target)
            {
                case Mode.VictoryCore:
                    return baseRadius * BattleMatchPresentationPlan.VictoryCorePeakScale;

                case Mode.FinalCore:
                    return BattleMatchPresentationPlan.FinalCoreMaxRadius;

                case Mode.CoreBreak:
                    return BattleMatchPresentationPlan.CoreBreakMaxRadius;

                default:
                    return 0f;
            }
        }

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
        /// ワールド座標の中心から描き始めます。<paramref name="radiusWorldPoint"/>を渡すと、
        /// 中心からその点までの距離を基準半径にします（勝利コアでピップの大きさへ合わせるため）。
        /// </summary>
        public void Begin(Mode target, Vector3 centreWorld, Vector3? radiusWorldPoint = null)
        {
            if (target == Mode.None || VerticesOf(target) == 0)
            {
                Hide();
                return;
            }

            mode = target;
            progress = 0f;

            Vector3 local = rectTransform.InverseTransformPoint(centreWorld);
            centre = new Vector2(local.x, local.y);

            if (radiusWorldPoint.HasValue)
            {
                Vector3 edge = rectTransform.InverseTransformPoint(radiusWorldPoint.Value);
                baseRadius = Vector2.Distance(centre, new Vector2(edge.x, edge.y));
            }
            else
            {
                baseRadius = 0f;
            }

            SetVerticesDirty();
        }

        /// <summary>進み具合を更新します。1以上で消えます。</summary>
        public void SetProgress(float value)
        {
            if (mode == Mode.None)
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
            bool wasShowing = mode != Mode.None || LastVertexCount > 0;

            ClearState();

            if (wasShowing)
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
            LastMaxAlpha = 0f;

            switch (mode)
            {
                case Mode.VictoryCore:
                    BuildVictoryCore(vh, progress);
                    break;

                case Mode.FinalCore:
                    BuildFinalCore(vh, progress);
                    break;

                case Mode.CoreBreak:
                    BuildCoreBreak(vh, progress);
                    break;

                default:
                    return;
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private void ClearState()
        {
            mode = Mode.None;
            progress = 0f;
            baseRadius = 0f;

            if (!IsActive())
            {
                LastVertexCount = 0;
                LastMaxDistance = 0f;
                LastMaxAlpha = 0f;
            }
        }

        // ---------------- 勝利コア ----------------

        /// <summary>白金色に点灯 → 軽く拡大 → ピップの大きさへ収束しながら消えます。</summary>
        private void BuildVictoryCore(VertexHelper vh, float p)
        {
            float total =
                BattleMatchPresentationPlan.VictoryCoreLightDuration +
                BattleMatchPresentationPlan.VictoryCoreGrowDuration +
                BattleMatchPresentationPlan.VictoryCoreSettleDuration;

            float lightEnd = BattleMatchPresentationPlan.VictoryCoreLightDuration / total;
            float growEnd = lightEnd + BattleMatchPresentationPlan.VictoryCoreGrowDuration / total;

            float peak = BattleMatchPresentationPlan.VictoryCorePeakScale;
            float scale;
            float alpha;

            if (p < lightEnd)
            {
                scale = 1f;
                alpha = p / lightEnd;
            }
            else if (p < growEnd)
            {
                scale = Mathf.Lerp(1f, peak, EaseOutCubic((p - lightEnd) / (growEnd - lightEnd)));
                alpha = 1f;
            }
            else
            {
                float t = SmoothStep((p - growEnd) / (1f - growEnd));

                scale = Mathf.Lerp(peak, 1f, t);
                alpha = 1f - t;
            }

            float radius = baseRadius * scale;

            AddDisc(vh, radius * 0.8f, VictoryDiscSegments, WithAlpha(WhiteGold, alpha), WithAlpha(Gold, alpha * 0.8f));
            AddRing(vh, radius, radius * 0.18f, VictoryRingSegments, WithAlpha(Gold, alpha), WithAlpha(WhiteGold, alpha * 0.6f));
        }

        // ---------------- FINAL CORE ----------------

        /// <summary>白金色のコアリングが開き、2回明滅して消えます。</summary>
        private void BuildFinalCore(VertexHelper vh, float p)
        {
            float radius = BattleMatchPresentationPlan.FinalCoreMaxRadius;
            float open = EaseOutCubic(Mathf.Clamp01(p / 0.3f));

            // 2回の明滅。最後の2割で消えます。
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(p * Mathf.PI * BattleMatchPresentationPlan.FinalCorePulses));
            float fade = 1f - SmoothStep(Mathf.Clamp01((p - 0.8f) / 0.2f));
            float alpha = pulse * fade;

            float outer = radius * open;

            AddRing(vh, outer, 10f, FinalOuterSegments, WithAlpha(Gold, alpha), WithAlpha(WhiteGold, alpha));
            AddRing(vh, outer * 0.62f, 5f, FinalInnerSegments, WithAlpha(WhiteGold, alpha * 0.8f), WithAlpha(Gold, alpha * 0.6f));
        }

        // ---------------- CORE BREAK ----------------

        /// <summary>中心コアが圧縮 → 亀裂状の放射線 → 白金フラッシュ → 消滅。</summary>
        private void BuildCoreBreak(VertexHelper vh, float p)
        {
            float total = BattleMatchPresentationPlan.FullCoreBreakDuration;
            float compressEnd = BattleMatchPresentationPlan.CoreBreakCompressDuration / total;
            float crackEnd = compressEnd + BattleMatchPresentationPlan.CoreBreakCrackDuration / total;
            float flashEnd = crackEnd + BattleMatchPresentationPlan.CoreBreakFlashDuration / total;

            float maxRadius = BattleMatchPresentationPlan.CoreBreakMaxRadius;

            // 1. 圧縮リング: 外から中心へ押し込まれ、亀裂が走ると消えます。
            float squeeze = EaseInCubic(Mathf.Clamp01(p / compressEnd));
            float ringOuter = Mathf.Lerp(120f, 28f, squeeze);
            float ringAlpha = p < compressEnd ? 1f : 1f - Mathf.Clamp01((p - compressEnd) / (crackEnd - compressEnd));

            AddRing(vh, ringOuter, Mathf.Lerp(8f, 14f, squeeze), BreakRingSegments,
                WithAlpha(Gold, ringAlpha), WithAlpha(WhiteGold, ringAlpha));

            // 2. 中心コア: 圧縮で小さく明るくなり、フラッシュの後に消えます。
            float coreRadius = p < compressEnd
                ? Mathf.Lerp(56f, 20f, squeeze)
                : p < flashEnd
                    ? 20f
                    : Mathf.Lerp(20f, 0f, Mathf.Clamp01((p - flashEnd) / 0.1f));

            float coreAlpha = p < flashEnd ? 1f : 1f - Mathf.Clamp01((p - flashEnd) / (1f - flashEnd));

            AddDisc(vh, coreRadius, BreakCoreSegments, WithAlpha(WhiteGold, coreAlpha), WithAlpha(Gold, coreAlpha));

            // 3. 亀裂: 圧縮が終わった瞬間から外へ走り、消滅の段で薄れます。
            float crack = EaseOutCubic(Mathf.Clamp01((p - compressEnd) / (crackEnd - compressEnd)));
            float crackAlpha = p < compressEnd ? 0f : p < flashEnd ? 1f : 1f - Mathf.Clamp01((p - flashEnd) / (1f - flashEnd));
            float halfWidth = Mathf.Lerp(4f, 1.5f, crack);

            for (int i = 0; i < BreakCracks; i++)
            {
                float angle = i * (360f / BreakCracks) * Mathf.Deg2Rad;
                float bent = angle + CrackJitter[i] * Mathf.Deg2Rad;

                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 outerDir = new Vector2(Mathf.Cos(bent), Mathf.Sin(bent));

                float length = (maxRadius - halfWidth) * CrackLengths[i] * crack;
                float start = Mathf.Min(coreRadius, length);

                AddQuad(
                    vh,
                    dir * start - side * halfWidth, WithAlpha(WhiteGold, crackAlpha),
                    dir * start + side * halfWidth, WithAlpha(WhiteGold, crackAlpha),
                    outerDir * length + side * (halfWidth * 0.3f), WithAlpha(Gold, crackAlpha * 0.8f),
                    outerDir * length - side * (halfWidth * 0.3f), WithAlpha(Gold, crackAlpha * 0.8f));
            }

            // 4. 白金フラッシュ: 亀裂が走り切った直後に最大、消滅の段で引きます。
            float flashAlpha;

            if (p < crackEnd)
            {
                flashAlpha = 0f;
            }
            else if (p < flashEnd)
            {
                flashAlpha = BattleMatchPresentationPlan.CoreBreakFlashPeakAlpha * Mathf.Clamp01((p - crackEnd) / (flashEnd - crackEnd));
            }
            else
            {
                flashAlpha = BattleMatchPresentationPlan.CoreBreakFlashPeakAlpha * (1f - SmoothStep(Mathf.Clamp01((p - flashEnd) / (1f - flashEnd))));
            }

            AddDisc(vh, maxRadius, BreakFlashSegments, WithAlpha(WhiteGold, flashAlpha), WithAlpha(Gold, 0f));
        }

        // ---------------- 頂点の追加（割り当てなし） ----------------

        private void AddRing(VertexHelper vh, float outer, float thickness, int segments, Color innerEdge, Color outerEdge)
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

        private void AddVertex(VertexHelper vh, Vector2 offset, Color vertexColor)
        {
            float distance = offset.magnitude;

            if (distance > LastMaxDistance)
            {
                LastMaxDistance = distance;
            }

            Color shown = vertexColor * color;

            if (shown.a > LastMaxAlpha)
            {
                LastMaxAlpha = shown.a;
            }

            vh.AddVert(new Vector3(centre.x + offset.x, centre.y + offset.y, 0f), shown, Vector4.zero);
        }

        // ---------------- 補助 ----------------

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }
    }
}
