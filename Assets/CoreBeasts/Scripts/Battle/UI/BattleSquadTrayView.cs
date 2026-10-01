using System.Collections.Generic;

using CoreBeasts.Units;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// プレイヤー編成7枠の表示と入力。横並びで7枠を同時に出します。
    ///
    /// 使用済みの判定は<see cref="BattleSession"/>へ問い合わせるだけで、
    /// ここでは持ちません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSquadTrayView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private BattleTraySlotView slotPrefab;

        private readonly List<BattleTraySlotView> slots =
            new List<BattleTraySlotView>();

        /// <summary>生成済みの枠。テストから状態を確かめるために公開しています。</summary>
        public IReadOnlyList<BattleTraySlotView> Slots => slots;

        /// <summary>7枠を生成します。</summary>
        public void Build(IBattleTrayListener listener, UiTextCatalog text)
        {
            Clear();

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(BattleSquadTrayView),
                    ReferenceCheck.Of(nameof(content), content),
                    ReferenceCheck.Of(nameof(slotPrefab), slotPrefab),
                    ReferenceCheck.Of(nameof(listener), listener)))
            {
                return;
            }

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                BattleTraySlotView slot = Instantiate(slotPrefab, content);
                slot.name = "TraySlot_" + (i + 1);
                slot.Bind(i, listener, text);

                slots.Add(slot);
            }
        }

        /// <summary>編成の中身を枠へ流し込みます。状態はまだ変えません。</summary>
        public void Show(
            BattleSideRoster side,
            AttributePalette palette,
            UiTextCatalog text)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                BattleUnitCard card =
                    side != null && i < side.Cards.Count ? side.Cards[i] : null;

                slots[i].Show(card, palette, text);
            }
        }

        /// <summary>
        /// 未使用・選択中・使用済みと、戦闘済みの結果を反映します。
        ///
        /// 使用済みかどうかはセッションが唯一の情報源です。
        /// 勝敗も同じで、ここでは決めません。
        /// <paramref name="outcomes"/>が null なら、バッジはすべて非表示になります。
        /// <paramref name="revealedOutcomes"/>が null なら、敗北のグレー化も行いません。
        /// </summary>
        public void RefreshStates(
            BattleSession session,
            string selectedInstanceId,
            IBattleSlotOutcomeSource outcomes = null,
            IBattleSlotOutcomeSource revealedOutcomes = null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                BattleTraySlotView slot = slots[i];
                BattleUnitCard card = slot.Card;

                if (card == null)
                {
                    slot.SetState(BattleSlotState.Available);
                    slot.SetOutcome(BattleSlotOutcome.None);
                    slot.SetDefeated(false);
                    continue;
                }

                bool used = session != null && session.IsPlayerUnitUsed(card.InstanceId);

                slot.SetState(
                    BattleSlotStates.Resolve(card.InstanceId, selectedInstanceId, used));

                // 個体IDで引くため、枠の並びが変わっても取り違えません。
                slot.SetOutcome(
                    outcomes != null
                        ? outcomes.GetOutcome(card.InstanceId)
                        : BattleSlotOutcome.None);

                // 敗北のグレーはバッジより一段早い控えを見ます。
                // 個体IDで引くため、同じDefinitionの別個体へは波及しません。
                slot.SetDefeated(
                    revealedOutcomes != null &&
                    BattleDefeatPresentation.GreysTraySlot(
                        revealedOutcomes.GetOutcome(card.InstanceId)));
            }
        }

        /// <summary>生成済みの枠を破棄します。</summary>
        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    DestroyView(slots[i].gameObject);
                }
            }

            slots.Clear();
        }

        /// <summary>
        /// 再生成時に古い表示が残らないよう破棄します。
        /// Destroy はEditModeで遅延するため、実行中かどうかで使い分けます
        /// （既存の RosterGridView / SquadBarView と同じ方式）。
        /// </summary>
        private static void DestroyView(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
