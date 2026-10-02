using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 戦績履歴レーンの LINK マーカー（Phase 4B）。ATTRIBUTE LINK が成立したラウンドの枠にだけ、
    /// 2つの輪が重なった小さな印を描きます。
    ///
    /// 枠（BattleHistorySlot プレハブ）には手を加えず、レーンを覆う1枚の Graphic だけで描きます。
    /// 文字を使わないため、フォントの収録状況に依存しません。
    /// 実行中に GameObject・Material・List を作りません（枠数ぶんの配列を最初に1回だけ持ちます）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleHistoryLinkMarkerGraphic : MaskableGraphic
    {
        /// <summary>輪1つの分割数。</summary>
        public const int RingSegments = 12;

        /// <summary>マーカー1つの頂点数（輪2つ）。</summary>
        public const int VerticesPerMarker = RingSegments * 2 * 2;

        [SerializeField] private Color markerColor = new Color(0.72f, 0.94f, 1f, 1f);

        [Tooltip("輪の半径（このRectTransformの単位）。")]
        [SerializeField] [Range(3f, 20f)] private float ringRadius = 8f;

        [Tooltip("輪の太さ。")]
        [SerializeField] [Range(1f, 6f)] private float ringThickness = 2.5f;

        [Tooltip("枠の左上から内側へ寄せる量。")]
        [SerializeField] private Vector2 inset = new Vector2(18f, 18f);

        private readonly RectTransform[] slots = new RectTransform[BattleHistoryModel.MaxEntries];
        private readonly bool[] linked = new bool[BattleHistoryModel.MaxEntries];
        private readonly Vector3[] lastWorld = new Vector3[BattleHistoryModel.MaxEntries];

        private int count;

        /// <summary>マーカーを出している枠の数。</summary>
        public int MarkerCount
        {
            get
            {
                int shown = 0;

                for (int i = 0; i < count; i++)
                {
                    if (linked[i] && slots[i] != null)
                    {
                        shown++;
                    }
                }

                return shown;
            }
        }

        /// <summary>その枠にマーカーを出しているか（確認・テスト用）。</summary>
        public bool IsMarked(int index)
        {
            return index >= 0 && index < count && linked[index] && slots[index] != null;
        }

        /// <summary>直近に作ったメッシュの頂点数。</summary>
        public int LastVertexCount { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>
        /// 枠と LINK 成立の有無を渡します。<paramref name="slotTransforms"/>と<paramref name="history"/>の
        /// 並びは同じです（履歴の i 番目が枠の i 番目）。
        /// </summary>
        public void SetMarkers(IReadOnlyList<RectTransform> slotTransforms, BattleHistoryModel history)
        {
            count = 0;

            int n = slotTransforms != null ? Mathf.Min(slotTransforms.Count, slots.Length) : 0;

            for (int i = 0; i < slots.Length; i++)
            {
                bool has = i < n;

                slots[i] = has ? slotTransforms[i] : null;
                linked[i] = has && history != null && i < history.Count && history.Entries[i].Linked;
            }

            count = n;
            CaptureWorld();
            SetVerticesDirty();
        }

        /// <summary>マーカーをすべて消します。</summary>
        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = null;
                linked[i] = false;
            }

            count = 0;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // レイアウトで枠が動いたときだけ作り直します（毎フレームは作りません）。
            for (int i = 0; i < count; i++)
            {
                if (linked[i] && slots[i] != null && slots[i].position != lastWorld[i])
                {
                    CaptureWorld();
                    SetVerticesDirty();
                    return;
                }
            }
        }

        private void CaptureWorld()
        {
            for (int i = 0; i < count; i++)
            {
                lastWorld[i] = slots[i] != null ? slots[i].position : Vector3.zero;
            }
        }

        /// <summary>今の状態でメッシュを作ります。テストからも同じ経路で頂点を確かめられます。</summary>
        public void FillMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVertexCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (!linked[i] || slots[i] == null)
                {
                    continue;
                }

                RectTransform slot = slots[i];
                Rect r = slot.rect;
                Vector3 corner = slot.TransformPoint(new Vector3(r.xMin + inset.x, r.yMax - inset.y, 0f));
                Vector3 local = rectTransform.InverseTransformPoint(corner);
                Vector2 centre = new Vector2(local.x, local.y);

                // 2つの輪を少し重ねて「つながり」を示します。
                AddRing(vh, centre + new Vector2(-ringRadius * 0.55f, 0f));
                AddRing(vh, centre + new Vector2(ringRadius * 0.55f, 0f));
            }

            LastVertexCount = vh.currentVertCount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            FillMesh(vh);
        }

        private void AddRing(VertexHelper vh, Vector2 centre)
        {
            float outer = ringRadius;
            float inner = Mathf.Max(0f, ringRadius - ringThickness);
            int start = vh.currentVertCount;
            Color c = markerColor * color;

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * (Mathf.PI * 2f / RingSegments);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                vh.AddVert(new Vector3(centre.x + dir.x * inner, centre.y + dir.y * inner, 0f), c, Vector4.zero);
                vh.AddVert(new Vector3(centre.x + dir.x * outer, centre.y + dir.y * outer, 0f), c, Vector4.zero);
            }

            for (int i = 0; i < RingSegments; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % RingSegments) * 2;

                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
