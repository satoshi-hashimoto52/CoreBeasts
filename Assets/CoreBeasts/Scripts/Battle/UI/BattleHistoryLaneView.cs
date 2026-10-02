using System.Collections.Generic;

using CoreBeasts.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 使い終わった個体を、ラウンド順に左から並べる戦績履歴レーン。
    ///
    /// リングとは別物です。ここへ移った個体がリングへ戻ることはありません。
    /// 何を出すかは<see cref="BattleHistoryModel"/>が持ち、ここは写すだけです。
    ///
    /// 操作は一切受け付けません。レーン全体のレイキャストを切ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHistoryLaneView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private BattleHistorySlotView slotPrefab;

        [Tooltip("レーン全体の入力を止めるためのCanvasGroup。")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("ATTRIBUTE LINK が成立したラウンドの枠に出す小さなマーカー（未設定なら出しません）。")]
        [SerializeField] private BattleHistoryLinkMarkerGraphic linkMarkers;

        private readonly List<BattleHistorySlotView> slots =
            new List<BattleHistorySlotView>();

        /// <summary>マーカーへ渡す枠の位置。最初に1回だけ用意し、毎回作り直しません。</summary>
        private readonly RectTransform[] slotTransforms = new RectTransform[BattleHistoryModel.MaxEntries];

        /// <summary>LINK マーカー（確認・テスト用）。</summary>
        public BattleHistoryLinkMarkerGraphic LinkMarkers => linkMarkers;

        /// <summary>生成済みの枠。テストから確かめるために公開しています。</summary>
        public IReadOnlyList<BattleHistorySlotView> Slots => slots;

        /// <summary>使用済み（結果が入っている）枠の数。</summary>
        public int UsedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].IsUsed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>結果色で点灯している枠の数。使用済み数とは別に数えます。</summary>
        public int LitCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].IsLit)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            // 1回戦から7枠そろえます。途中でノードが生えて並びが動かないようにします。
            Build();
            BlockInput();
        }

        /// <summary>
        /// 7枠を作って、すべて未使用の姿にします。
        /// 何度呼んでも枠は増えません。
        /// </summary>
        public void Build()
        {
            EnsureCapacity(BattleHistoryModel.MaxEntries);

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (!slots[i].gameObject.activeSelf)
                {
                    slots[i].gameObject.SetActive(true);
                }

                slots[i].ShowEmpty();
            }

            BlockInput();
        }

        /// <summary>履歴の中身を流し込みます。</summary>
        public void Show(
            BattleHistoryModel history,
            BattleSideRoster side,
            AttributePalette palette,
            UiTextCatalog text)
        {
            BlockInput();

            if (content == null || slotPrefab == null || history == null)
            {
                return;
            }

            // 枠は常に7つ。空きは隠さず、暗いソケットのまま置いておきます。
            EnsureCapacity(BattleHistoryModel.MaxEntries);

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (!slots[i].gameObject.activeSelf)
                {
                    slots[i].gameObject.SetActive(true);
                }

                if (i >= history.Count)
                {
                    slots[i].ShowEmpty();
                    continue;
                }

                BattleHistoryEntry entry = history.Entries[i];

                slots[i].Show(
                    entry,
                    side != null ? side.Find(entry.InstanceId) : null,
                    palette,
                    text);
            }

            RefreshLinkMarkers(history);
        }

        /// <summary>LINK が成立したラウンドの枠だけに小さなマーカーを出します（PLAYER 側だけ）。</summary>
        private void RefreshLinkMarkers(BattleHistoryModel history)
        {
            if (linkMarkers == null)
            {
                return;
            }

            for (int i = 0; i < slotTransforms.Length; i++)
            {
                slotTransforms[i] = i < slots.Count && slots[i] != null ? (RectTransform)slots[i].transform : null;
            }

            linkMarkers.SetMarkers(slotTransforms, history);
        }

        /// <summary>
        /// 7枠とも未使用へ戻します。REMATCHとマッチの作り直しで呼びます。
        /// 枠そのものは消しません。レーンの見た目は常に7枠のままです。
        /// </summary>
        public void Clear()
        {
            Build();

            if (linkMarkers != null)
            {
                linkMarkers.Clear();
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleHistoryLaneView),
                ReferenceCheck.Of(nameof(content), content),
                ReferenceCheck.Of(nameof(slotPrefab), slotPrefab),
                ReferenceCheck.Of(nameof(canvasGroup), canvasGroup));
        }

        /// <summary>履歴はタップもドラッグも受け付けません。</summary>
        private void BlockInput()
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                DisableRaycasts(slots[i]);
            }
        }

        private static void DisableRaycasts(BattleHistorySlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.LockInput();
        }

        private void EnsureCapacity(int count)
        {
            while (slots.Count < count && slots.Count < BattleHistoryModel.MaxEntries)
            {
                BattleHistorySlotView slot = Instantiate(slotPrefab, content);
                slot.name = "HistorySlot_" + (slots.Count + 1);

                slots.Add(slot);

                DisableRaycasts(slot);
            }
        }
    }
}
