using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 編成画面の進行役。各Viewとデータの受け渡しだけを行い、
    /// 表示処理・編成データ・操作ルール・保存処理はそれぞれ別クラスへ分離しています。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitSetScreen : MonoBehaviour,
        IBeastCardListener,
        ISquadSlotListener
    {
        [Header("Data")]
        [SerializeField] private CoreBeastRoster roster;
        [SerializeField] private AttributePalette palette;
        [SerializeField] private UiTextCatalog text;

        [SerializeField]
        [Tooltip("編成セットの内部ID。表示名はUiTextCatalogが作ります。")]
        private string setId = "1";

        [Header("Views")]
        [SerializeField] private BeastDetailPanel detailPanel;
        [SerializeField] private RosterGridView rosterGrid;
        [SerializeField] private SquadBarView squadBar;
        [SerializeField] private ToastLabel toast;
        [SerializeField] private DragGhostPresenter dragGhost;

        [Header("Static labels")]
        [SerializeField] private TMP_Text homeButtonLabel;
        [SerializeField] private TMP_Text screenTitleLabel;
        [SerializeField] private TMP_Text setNameLabel;
        [SerializeField] private TMP_Text rosterHeadingLabel;
        [SerializeField] private TMP_Text squadHeadingLabel;
        [SerializeField] private TMP_Text saveButtonLabel;

        [Header("Set status chip")]
        [Tooltip(
            "MY SQUAD見出しの右に出す SET 表示。切り替えは未実装のため操作にはしません。" +
            "将来セット切替を入れるときは、この根へButtonを足せば済みます。")]
        [SerializeField] private RectTransform setChip;

        [Header("Bottom action dock")]
        [SerializeField] private Button saveButton;

        [Header("Diagnostics")]
        [Tooltip("ONにすると操作イベントの流れをConsoleへ出力します（調査用）。")]
        [SerializeField] private bool logGestureEvents;

        private readonly SquadFormation formation = new SquadFormation();
        private SquadEditor editor;
        private ISquadRepository repository;
        private OwnedCoreBeast draggingBeast;
        private bool dropHandled;

        /// <summary>ドラッグ1回ごとに増える通し番号。</summary>
        private int dragSessionId;

        /// <summary>終了処理を済ませたドラッグの通し番号。</summary>
        private int finishedDragSessionId;

        private readonly List<RaycastResult> raycastResults =
            new List<RaycastResult>();

        /// <summary>編成枠側から参照される、ドラッグ中かどうか。</summary>
        public bool IsDraggingBeast => draggingBeast != null;

        private void Awake()
        {
            // 保存先はアプリ内で共有します。ここで自前に作ると、
            // 保存した編成をBattle画面から読めません。
            repository = SquadRepositoryProvider.Shared;
            editor = new SquadEditor(formation);

            GestureLog.Enabled = logGestureEvents;

            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            ApplyStaticLabels();

            formation.Changed += HandleFormationChanged;

            rosterGrid.Build(roster, palette, text, this);
            squadBar.Build(text, this);

            if (repository.TryLoad(setId, out SquadSnapshot saved))
            {
                formation.Restore(saved, roster);
            }

            if (saveButton != null)
            {
                saveButton.onClick.AddListener(HandleSaveClicked);
            }

            HandleFormationChanged();
            ShowInitialDetail();
        }

        private void OnDestroy()
        {
            formation.Changed -= HandleFormationChanged;

            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(HandleSaveClicked);
            }
        }

        // ---------------- 所持一覧カード ----------------

        /// <summary>短いタップ。選択と詳細表示のみで、編成は変更しません。</summary>
        public void OnCardTapped(OwnedCoreBeast beast)
        {
            editor.Select(beast);

            detailPanel.Show(beast);
            rosterGrid.SetSelected(beast);
        }

        public void OnCardDragBegin(OwnedCoreBeast beast, PointerEventData eventData)
        {
            if (beast == null || !beast.IsValid)
            {
                return;
            }

            // 開始1回につき通し番号を1つ進めます。終了処理はこの番号で1回に限ります。
            dragSessionId++;

            draggingBeast = beast;
            dropHandled = false;

            // 7枠を控えめに候補表示し、同じ個体が入っている枠は入れ替えと分かる表示にします。
            squadBar.SetDragTarget(true, formation.IndexOf(beast));

            if (dragGhost != null)
            {
                dragGhost.Show(beast, palette, text, eventData);
            }
        }

        public void OnCardDragMove(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.Move(eventData);
            }
        }

        /// <summary>
        /// ドラッグ終了。ドロップ成否に関わらず呼ばれます。
        ///
        /// 呼び出し元は1回の操作で複数あります。
        /// Unity は離した瞬間に OnPointerUp → OnDrop → OnEndDrag の順で配送し、
        /// さらに一覧の作り直しで BeastCardView.OnDisable からも届きます。
        /// そのため通し番号で「開始1回に対して終了処理1回」に限ります。
        ///
        /// 順序が重要です。
        /// 表示(Ghost)を消すことと、Drop判定に要る情報を捨てることは別物です。
        /// 判定材料（対象個体と離した位置）を先に退避してから表示を消します。
        /// </summary>
        public void OnCardDragEnd(OwnedCoreBeast beast, PointerEventData eventData)
        {
            if (dragSessionId == finishedDragSessionId)
            {
                // 2回目以降。表示の後始末だけ行い、編成には触れません。
                if (dragGhost != null)
                {
                    dragGhost.Hide();
                }

                return;
            }

            finishedDragSessionId = dragSessionId;

            // 1. Drop判定に必要な情報を退避します。表示を消す前に確保します。
            OwnedCoreBeast dropped = draggingBeast;
            PointerEventData releasedAt = eventData;
            bool placedAlready = dropHandled;

            // 2. ドラッグ表示だけを即時に消します。判定材料は消しません。
            if (dragGhost != null)
            {
                dragGhost.Hide();
            }

            // 3. 退避した eventData で Drop 先を判定します。
            // 4. 有効な SquadSlot なら登録します。
            //    SquadSlotView.OnDrop が飛ばない場合の保険です。
            //    eventData が無いのは中断（カード破棄など）で、その場合は配置しません。
            if (!placedAlready && releasedAt != null && dropped != null)
            {
                TryDropByRaycast(releasedAt, dropped);
            }

            // 5. 最後にドラッグ状態をクリアします。
            draggingBeast = null;
            dropHandled = false;

            if (squadBar != null)
            {
                squadBar.ClearHighlights();
            }

            // 元カードの見た目を、選択状態に合わせて戻します。
            if (rosterGrid != null && editor != null)
            {
                rosterGrid.SetSelected(editor.Selected);
            }
        }

        /// <summary>
        /// 指の位置をRaycastAllし、最初に見つかったSquadSlotViewへ配置します。
        /// 枠が見つからなければ編成は変更しません。
        /// </summary>
        private bool TryDropByRaycast(PointerEventData eventData, OwnedCoreBeast beast)
        {
            if (eventData == null || beast == null || EventSystem.current == null)
            {
                return false;
            }

            raycastResults.Clear();

            PointerEventData probe = new PointerEventData(EventSystem.current)
            {
                position = eventData.position,
            };

            EventSystem.current.RaycastAll(probe, raycastResults);

            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hit = raycastResults[i].gameObject;

                if (hit == null)
                {
                    continue;
                }

                SquadSlotView slot = hit.GetComponentInParent<SquadSlotView>();

                if (slot == null)
                {
                    continue;
                }

                GestureLog.Write(
                    "UnitSetScreen", "RaycastAll:slot found", eventData, 0f, 0f,
                    CardGestureState.SquadDragging,
                    beast.InstanceId,
                    slot.SlotIndex);

                return ApplyDrop(slot.SlotIndex, beast);
            }

            GestureLog.Write(
                "UnitSetScreen", "RaycastAll:no slot", eventData, 0f, 0f,
                CardGestureState.SquadDragging, beast.InstanceId, -1);

            return false;
        }

        // ---------------- 編成枠 ----------------

        /// <summary>配置済み枠のタップで部隊から除外します。空き枠は何もしません。</summary>
        public void OnSlotTapped(int slotIndex)
        {
            GestureLog.Write(
                "SquadSlotView", "OnSlotTapped", null, 0f, 0f,
                CardGestureState.Idle, null, slotIndex);

            if (!editor.RemoveAt(slotIndex))
            {
                return;
            }

            rosterGrid.SetSelected(editor.Selected);

            if (toast != null && text != null)
            {
                toast.Show(text.RemovedFromSquad);
            }
        }

        /// <summary>ドラッグした個体を枠へ落としたとき。</summary>
        public void OnSlotDropped(int slotIndex)
        {
            GestureLog.Write(
                "SquadSlotView", "OnDrop", null, 0f, 0f,
                CardGestureState.SquadDragging,
                draggingBeast != null ? draggingBeast.InstanceId : null, slotIndex);

            ApplyDrop(slotIndex, draggingBeast);
        }

        /// <summary>
        /// 指定の個体を枠へ適用します。
        /// 対象は引数で受け取ります。draggingBeast を読むと、
        /// 終了処理の途中で消えている場合に配置できなくなるためです。
        /// </summary>
        private bool ApplyDrop(int slotIndex, OwnedCoreBeast beast)
        {
            if (beast == null)
            {
                return false;
            }

            bool changed = editor.DropOnSlot(slotIndex, beast);

            if (changed)
            {
                dropHandled = true;

                squadBar.FlashSlot(slotIndex);

                // 配置した個体を選択状態にし、上部詳細も更新します。
                editor.Select(beast);
                detailPanel.Show(beast);
            }

            GestureLog.Write(
                "UnitSetScreen", changed ? "Assign:changed" : "Assign:no change",
                null, 0f, 0f, CardGestureState.SquadDragging,
                beast.InstanceId, slotIndex);

            return changed;
        }

        // ---------------- 内部 ----------------

        private bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(UnitSetScreen),
                ReferenceCheck.Of(nameof(roster), roster),
                ReferenceCheck.Of(nameof(palette), palette),
                ReferenceCheck.Of(nameof(text), text),
                ReferenceCheck.Of(nameof(detailPanel), detailPanel),
                ReferenceCheck.Of(nameof(rosterGrid), rosterGrid),
                ReferenceCheck.Of(nameof(squadBar), squadBar),
                ReferenceCheck.Of(nameof(toast), toast),
                ReferenceCheck.Of(nameof(dragGhost), dragGhost),
                ReferenceCheck.Of(nameof(setChip), setChip),
                ReferenceCheck.Of(nameof(saveButton), saveButton));
        }

        private void ApplyStaticLabels()
        {
            SetText(homeButtonLabel, text.Home);
            SetText(screenTitleLabel, text.UnitSet);
            SetText(setNameLabel, text.FormatSetName(setId));
            SetText(rosterHeadingLabel, text.CoreBeasts);
            SetText(squadHeadingLabel, text.MySquad);
            SetText(saveButtonLabel, text.SaveSet);
        }

        /// <summary>
        /// 初回表示。まだ何も選ばれていない状態のまま、
        /// 詳細欄だけ先頭の所持個体で埋めます。
        /// </summary>
        private void ShowInitialDetail()
        {
            for (int i = 0; i < roster.Owned.Count; i++)
            {
                OwnedCoreBeast candidate = roster.Owned[i];

                if (candidate != null && candidate.IsValid)
                {
                    detailPanel.Show(candidate);
                    return;
                }
            }

            detailPanel.ShowEmpty();
        }

        private void HandleFormationChanged()
        {
            squadBar.Refresh(formation, palette, text);

            // 配置・移動・除外・復元のいずれでも編成済みマークを一致させます。
            rosterGrid.RefreshSquadMarks(formation);
        }

        private void HandleSaveClicked()
        {
            // UI処理と保持処理を分離しているため、保存先の差し替えはここだけで済みます。
            repository.Save(setId, formation.CreateSnapshot());

            if (toast != null)
            {
                toast.Show(text.FormatSaved(text.FormatSetName(setId)));
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
