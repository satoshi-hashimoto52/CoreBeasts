using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 戦績履歴レーンの1枠。使い終わった個体を小さく出すだけの表示です。
    ///
    /// リング側の<see cref="BattleUnitWheelItemView"/>とは責務を分けています。
    /// あちらは「まだ出せる個体」、こちらは「もう出し終えた結果」です。
    /// 選択も使用済み判定も持たず、タップもドラッグも受け付けません。
    ///
    /// 勝敗は決めません。<see cref="BattleHistoryEntry.Outcome"/>を写すだけです。
    ///
    /// 見た目は小さな CORE NODE（六角ソケット）です。長方形カードではありません。
    /// 未使用は暗い輪郭だけ、結果が公開された枠だけ外周が結果色で点灯します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHistorySlotView : MonoBehaviour
    {
        [SerializeField] private BeastThumbnailView thumbnail;
        [SerializeField] private TMP_Text orderLabel;
        [SerializeField] private Image badgeImage;
        [SerializeField] private TMP_Text badgeLabel;

        [Tooltip("結果色で点灯するノード外周。")]
        [SerializeField] private LaunchPedestalGraphic nodeRing;

        [Tooltip("未使用ノードの暗い輪郭色。")]
        [SerializeField] private Color idleRing = new Color(0.2f, 0.26f, 0.34f, 1f);

        [SerializeField] private Color winBadge = new Color(0.259f, 0.78f, 0.478f, 1f);
        [SerializeField] private Color lossBadge = new Color(0.882f, 0.357f, 0.392f, 1f);
        [SerializeField] private Color drawBadge = new Color(0.604f, 0.643f, 0.71f, 1f);
        [SerializeField] private Color badgeTextColor = Color.white;

        /// <summary>この枠が示している個体ID。</summary>
        public string InstanceId { get; private set; }

        /// <summary>この枠が出している結果。</summary>
        public BattleSlotOutcome Outcome { get; private set; } = BattleSlotOutcome.None;

        /// <summary>この枠が使用済みか。未使用なら暗いソケットだけを出します。</summary>
        public bool IsUsed => !string.IsNullOrEmpty(InstanceId);

        /// <summary>結果色で点灯しているか。未使用とPending中は点灯しません。</summary>
        public bool IsLit => Outcome != BattleSlotOutcome.None;

        /// <summary>W/L/Dバッジを出しているか（確認・テスト用）。</summary>
        public bool IsBadgeVisible =>
            badgeImage != null && badgeImage.gameObject.activeSelf;

        /// <summary>敗北としてグレー化しているか。</summary>
        public bool IsDefeated => Outcome == BattleSlotOutcome.Loss;

        /// <summary>結果色で点灯するノード外周（確認・テスト用）。</summary>
        public LaunchPedestalGraphic NodeRing => nodeRing;

        /// <summary>1戦ぶんを流し込みます。</summary>
        public void Show(
            BattleHistoryEntry entry,
            BattleUnitCard card,
            AttributePalette palette,
            UiTextCatalog text)
        {
            InstanceId = entry.InstanceId;
            Outcome = entry.Outcome;

            LockInput();

            bool hasCard = card != null && card.Definition != null;

            if (orderLabel != null && text != null)
            {
                orderLabel.text = text.FormatSlotOrder(entry.SquadNumber);
            }

            if (thumbnail != null)
            {
                if (hasCard)
                {
                    thumbnail.Show(card.Definition, palette);

                    // 敗北だけ既存のグレー化を使います。勝ち・引き分けは通常色のままです。
                    thumbnail.SetDefeated(entry.Outcome == BattleSlotOutcome.Loss);
                }
                else
                {
                    thumbnail.Clear();
                }
            }

            Color result = ResolveBadgeColor(entry.Outcome);

            // 結果が公開された枠だけバッジを出します。Pending（None）では出しません。
            bool showBadge = entry.Outcome != BattleSlotOutcome.None;

            if (badgeImage != null)
            {
                badgeImage.color = result;
                SetActive(badgeImage.gameObject, showBadge);
            }

            // 結果が入った枠だけ、ノード外周を結果色で点灯させます。
            if (nodeRing != null)
            {
                bool lit = entry.Outcome != BattleSlotOutcome.None;

                nodeRing.Apply(lit ? result : idleRing, lit ? result : idleRing, false);
                nodeRing.SetOpacity(lit ? 1f : 0.55f);
            }

            if (badgeLabel != null)
            {
                badgeLabel.text = BattleSlotOutcomes.Symbol(entry.Outcome);
                badgeLabel.color = badgeTextColor;
            }
        }

        /// <summary>
        /// 未使用の枠にします。暗い輪郭と暗い内部だけを残し、
        /// キャラクターも編成番号も W/L/D も出しません。
        ///
        /// レーンは最初から7枠を並べます。ここで「まだ使っていない」姿を作るので、
        /// ラウンドが進んでもノードが後から生えず、並びが動いて見えません。
        /// </summary>
        public void ShowEmpty()
        {
            InstanceId = null;
            Outcome = BattleSlotOutcome.None;

            LockInput();

            if (orderLabel != null)
            {
                orderLabel.text = string.Empty;
            }

            if (thumbnail != null)
            {
                thumbnail.Clear();
            }

            if (badgeImage != null)
            {
                badgeImage.color = idleRing;
                SetActive(badgeImage.gameObject, false);
            }

            if (badgeLabel != null)
            {
                badgeLabel.text = string.Empty;
            }

            if (nodeRing != null)
            {
                nodeRing.Apply(idleRing, idleRing, false);
                nodeRing.SetOpacity(0.55f);
            }
        }

        /// <summary>
        /// この枠が入力を取らない状態へ締め直します。
        ///
        /// 履歴は見るだけの表示です。タップもドラッグも受け付けません。
        /// <see cref="Show"/>で <c>OutcomeBadge</c> を表示へ戻すため、
        /// 隠れているあいだは気付けない設定が、公開と同時に効いてしまいます。
        /// 状態が変わるたびにここで締め直せば、どの状態でも同じになります。
        ///
        /// 枠そのものは消しません。7個の固定ソケットは常に出したままです。
        /// </summary>
        public void LockInput()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].raycastTarget)
                {
                    graphics[i].raycastTarget = false;
                }
            }

            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].blocksRaycasts = false;
                groups[i].interactable = false;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleHistorySlotView),
                ReferenceCheck.Of(nameof(thumbnail), thumbnail),
                ReferenceCheck.Of(nameof(orderLabel), orderLabel),
                ReferenceCheck.Of(nameof(badgeImage), badgeImage),
                ReferenceCheck.Of(nameof(badgeLabel), badgeLabel),
                ReferenceCheck.Of(nameof(nodeRing), nodeRing));
        }

        private Color ResolveBadgeColor(BattleSlotOutcome outcome)
        {
            switch (outcome)
            {
                case BattleSlotOutcome.Win:
                    return winBadge;

                case BattleSlotOutcome.Loss:
                    return lossBadge;

                default:
                    return drawBadge;
            }
        }
    }
}
