using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

namespace CoreBeasts.Units
{
    /// <summary>
    /// ドラッグ表示の生成・追従・破棄を一箇所で管理します。
    /// 操作終了時に必ず破棄するため、表示が残りません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragGhostPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Canvas最前面のコンテナ。ここへドラッグ表示を作ります。")]
        private RectTransform ghostRoot;

        [SerializeField] private DragGhostView ghostPrefab;

        [SerializeField]
        [Tooltip("座標変換に使うCanvas。Overlayならカメラ不要です。")]
        private Canvas canvas;

        [SerializeField]
        [Tooltip("指で隠れないよう、指の位置からずらす量（Canvas units）。")]
        private Vector2 ghostOffset = new Vector2(0f, 96f);

        private DragGhostView current;

        /// <summary>
        /// この Presenter が作った Ghost だけを覚えます。
        /// 後始末の対象をここに限ることで、ghostRoot へ置かれた
        /// 他のUI（ドロップ先や入力対象）を巻き込んで消しません。
        /// </summary>
        private readonly List<DragGhostView> created = new List<DragGhostView>();

        /// <summary>ドラッグ表示を出しているか。</summary>
        public bool IsShowing => current != null;

        /// <summary>
        /// いま画面に出ているドラッグ表示の数。正常時は 0 か 1 です。
        /// <see cref="ghostRoot"/> を直接数えるため、
        /// 取りこぼして残った表示もここに現れます。
        /// </summary>
        public int VisibleGhostCount
        {
            get
            {
                if (ghostRoot == null)
                {
                    return 0;
                }

                int count = 0;

                for (int i = 0; i < ghostRoot.childCount; i++)
                {
                    Transform child = ghostRoot.GetChild(i);

                    if (child.gameObject.activeInHierarchy
                        && child.GetComponent<DragGhostView>() != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>ドラッグ表示を作り、指の位置へ置きます。</summary>
        public void Show(
            OwnedCoreBeast beast,
            AttributePalette palette,
            UiTextCatalog text,
            PointerEventData eventData)
        {
            Hide();

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(DragGhostPresenter),
                    ReferenceCheck.Of(nameof(ghostRoot), ghostRoot),
                    ReferenceCheck.Of(nameof(ghostPrefab), ghostPrefab)))
            {
                return;
            }

            // Hide が届かなかった経路で残った表示があれば、ここで必ず片付けます。
            SweepStrayGhosts();

            current = Instantiate(ghostPrefab, ghostRoot);
            current.name = "DragGhost";

            created.Add(current);
            current.Bind(beast, palette, text);

            Move(eventData);
        }

        /// <summary>指の位置へ追従させます。</summary>
        public void Move(PointerEventData eventData)
        {
            if (current == null || ghostRoot == null || eventData == null)
            {
                return;
            }

            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    ghostRoot, eventData.position, eventCamera, out Vector2 local))
            {
                current.MoveTo(local + ghostOffset);
            }
        }

        /// <summary>ドラッグ表示を破棄します。何度呼んでも安全です。</summary>
        public void Hide()
        {
            current = null;

            // 自分が作ったものだけを片付けます。
            // 取りこぼしが1つでも残ると、機械獣が二重に見えます。
            SweepStrayGhosts();
        }

        /// <summary>
        /// この Presenter が作った Ghost のうち、まだ残っているものを片付けます。
        ///
        /// カードがドラッグ中に破棄されるなどして Hide が届かなかった場合、
        /// 表示だけが画面へ残ります。現象としては
        /// 「一覧の下へ機械獣がもう1体、大きく出たまま」になります。
        ///
        /// 対象は<see cref="created"/>に限ります。
        /// ghostRoot 配下を無条件に消すと、そこへ置かれた他のUIまで壊します。
        /// </summary>
        private void SweepStrayGhosts()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                DragGhostView ghost = created[i];

                created.RemoveAt(i);

                if (ghost != null)
                {
                    Dispose(ghost.gameObject);
                }
            }
        }

        /// <summary>
        /// Destroy は次のフレームまで遅れるため、先に表示を止めてから破棄します。
        /// そうしないと、消したはずの表示がそのフレームのあいだ描かれ続けます。
        /// </summary>
        private static void Dispose(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}
