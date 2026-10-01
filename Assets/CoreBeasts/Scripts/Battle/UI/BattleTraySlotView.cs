using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// プレイヤー編成トレイの1枠。タップで選択するだけで、ドラッグ操作はありません。
    ///
    /// 立ち絵の着色は既存の<see cref="BeastThumbnailView"/>へ委ねます。
    /// 使用済みかどうかの判定は持たず、<see cref="SetState"/>で渡された状態を写すだけです。
    /// 勝敗も同じで、<see cref="SetOutcome"/>で渡された結果を写すだけです。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleTraySlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform liftRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private BeastThumbnailView thumbnail;
        [SerializeField] private Image attributeChip;
        [SerializeField] private TMP_Text orderLabel;
        [SerializeField] private TMP_Text attributeLabel;
        [SerializeField] private TMP_Text usedLabel;

        [Header("Outcome badge")]
        [Tooltip("カード右上の勝敗バッジ。未戦闘のあいだは丸ごと隠します。")]
        [SerializeField] private GameObject outcomeBadgeRoot;

        [Tooltip("使用済みの暗転から外すため、バッジ専用のCanvasGroupを持たせます。")]
        [SerializeField] private CanvasGroup outcomeBadgeGroup;

        [SerializeField] private Image outcomeBadgeImage;
        [SerializeField] private TMP_Text outcomeBadgeLabel;

        [SerializeField] private Color winBadge = new Color(0.259f, 0.78f, 0.478f, 1f);
        [SerializeField] private Color lossBadge = new Color(0.882f, 0.357f, 0.392f, 1f);
        [SerializeField] private Color drawBadge = new Color(0.604f, 0.643f, 0.71f, 1f);

        [Tooltip("W / L / D の文字色。色だけでなく文字でも結果が分かるようにします。")]
        [SerializeField] private Color badgeTextColor = Color.white;

        [Header("Available")]
        [SerializeField] private Color availableBackground = new Color(0.14f, 0.16f, 0.2f, 1f);
        [SerializeField] private Color availableFrame = new Color(0.32f, 0.36f, 0.42f, 1f);

        [Header("Selected")]
        [Tooltip("選択中はアンバー系の枠で強調し、少し浮かせます。")]
        [SerializeField] private Color selectedBackground = new Color(0.26f, 0.22f, 0.12f, 1f);
        [SerializeField] private Color selectedFrame = new Color(1f, 0.78f, 0.28f, 1f);
        [SerializeField] [Range(0f, 40f)] private float selectedLift = 16f;

        [Header("Used")]
        [SerializeField] private Color usedBackground = new Color(0.08f, 0.09f, 0.11f, 1f);
        [SerializeField] private Color usedFrame = new Color(0.18f, 0.2f, 0.24f, 1f);
        [SerializeField] [Range(0.1f, 1f)] private float usedAlpha = 0.38f;

        [Header("Defeated")]
        [Tooltip("敗れた個体へ足す細いグレー枠。使用済みの暗転とは別概念です。")]
        [SerializeField] private Color defeatedFrame = DefeatTint.Frame;

        private int slotIndex = -1;
        private IBattleTrayListener listener;
        private Vector2 restPosition;
        private bool hasRestPosition;

        /// <summary>0始まりの枠番号。</summary>
        public int SlotIndex => slotIndex;

        /// <summary>この枠が示している個体。空なら null。</summary>
        public BattleUnitCard Card { get; private set; }

        /// <summary>現在の見た目の状態。</summary>
        public BattleSlotState State { get; private set; } = BattleSlotState.Available;

        /// <summary>現在出している戦闘結果。未戦闘なら None。</summary>
        public BattleSlotOutcome Outcome { get; private set; } = BattleSlotOutcome.None;

        /// <summary>
        /// 敗れた個体としてグレー化しているか。
        /// <see cref="BattleSlotState.Used"/>の暗転とは別概念です。
        /// 使用済みでも勝っていればグレーにはなりません。
        /// </summary>
        public bool IsDefeated { get; private set; }

        /// <summary>枠番号と通知先を設定します。</summary>
        public void Bind(int index, IBattleTrayListener trayListener, UiTextCatalog text)
        {
            slotIndex = index;
            listener = trayListener;

            CacheRestPosition();

            if (orderLabel != null && text != null)
            {
                orderLabel.text = text.FormatSlotOrder(index + 1);
            }

            BringBadgeToFront();

            SetState(BattleSlotState.Available);
            SetOutcome(BattleSlotOutcome.None);
        }

        /// <summary>枠の中身を更新します。null なら空き枠表示。</summary>
        public void Show(
            BattleUnitCard card,
            AttributePalette palette,
            UiTextCatalog text)
        {
            Card = card;

            // 中身を入れ替えるときは、前の個体の結果を必ず消します。
            // 画面を出入りしても古いバッジや灰色が残らないのは、ここが起点です。
            SetOutcome(BattleSlotOutcome.None);
            SetDefeated(false);

            bool hasCard = card != null && card.Definition != null;

            if (thumbnail != null)
            {
                if (hasCard)
                {
                    thumbnail.Show(card.Definition, palette);
                }
                else
                {
                    thumbnail.Clear();
                }
            }

            if (attributeChip != null)
            {
                attributeChip.enabled = hasCard;

                if (hasCard && palette != null)
                {
                    attributeChip.color = palette
                        .GetColors(card.Definition.PrimaryAttribute)
                        .PrimaryColor;
                }
            }

            if (attributeLabel != null)
            {
                attributeLabel.gameObject.SetActive(hasCard);

                if (hasCard && text != null)
                {
                    attributeLabel.text = text.BuildAttributeSymbol(card.Definition);
                }
            }
        }

        /// <summary>見た目の状態を切り替えます。</summary>
        public void SetState(BattleSlotState state)
        {
            State = state;

            ApplyVisuals();
        }

        /// <summary>
        /// 敗北表示を切り替えます。勝敗はここでは決めず、
        /// 公開済みの結果を写すだけです。
        ///
        /// 使用済みの暗転（<see cref="BattleSlotState.Used"/>）とは独立して持ちます。
        /// 使用済みでも勝っていればグレーにはならず、
        /// 敗れていれば未使用のうちからグレーになります。
        /// </summary>
        public void SetDefeated(bool defeated)
        {
            IsDefeated = defeated;

            ApplyVisuals();
        }

        /// <summary>
        /// 状態と敗北表示をまとめて描き直します。
        /// 2つの概念が同じ場所で合流するのはここだけです。
        /// </summary>
        private void ApplyVisuals()
        {
            BattleSlotState state = State;

            Color back = availableBackground;
            Color edge = availableFrame;
            float alpha = 1f;
            float lift = 0f;

            switch (state)
            {
                case BattleSlotState.Selected:
                    back = selectedBackground;
                    edge = selectedFrame;
                    lift = selectedLift;
                    break;

                case BattleSlotState.Used:
                    back = usedBackground;
                    edge = usedFrame;
                    alpha = usedAlpha;
                    break;
            }

            if (background != null)
            {
                background.color = back;
            }

            // 敗北は状態色の上から重ねます。細いグレー枠を足すことで、
            // 隣のW / D個体と並んでも色差が残ります。
            if (IsDefeated)
            {
                edge = defeatedFrame;
            }

            if (frame != null)
            {
                frame.color = edge;
            }

            // 立ち絵は属性色からニュートラルグレーへ置き換えます。
            // バッジは専用CanvasGroupで親の影響を無視するため、Lの赤は残ります。
            if (thumbnail != null)
            {
                thumbnail.SetDefeated(IsDefeated);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            if (usedLabel != null)
            {
                usedLabel.gameObject.SetActive(state == BattleSlotState.Used);
            }

            ApplyLift(lift);
        }

        /// <summary>
        /// 戦闘済みの結果を切り替えます。
        /// None ならバッジを隠し、それ以外は色と文字を入れ替えて出します。
        ///
        /// バッジは<see cref="BattleSlotState.Used"/>の暗転から外します。
        /// カード全体のCanvasGroupは触らず、バッジ側のCanvasGroupで
        /// 親の影響を無視させるため、暗転しても文字は読める明るさのままです。
        /// </summary>
        public void SetOutcome(BattleSlotOutcome outcome)
        {
            Outcome = outcome;

            bool show = outcome != BattleSlotOutcome.None;

            if (outcomeBadgeGroup != null)
            {
                // 使用済みカードの alpha 低下を、バッジだけ受け取らないようにします。
                outcomeBadgeGroup.ignoreParentGroups = true;
                outcomeBadgeGroup.alpha = 1f;

                // バッジはタップを奪いません。判定は従来どおりカード本体が受けます。
                outcomeBadgeGroup.blocksRaycasts = false;
            }

            if (outcomeBadgeRoot != null)
            {
                outcomeBadgeRoot.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (outcomeBadgeImage != null)
            {
                outcomeBadgeImage.color = ResolveBadgeColor(outcome);
            }

            if (outcomeBadgeLabel != null)
            {
                outcomeBadgeLabel.text = BattleSlotOutcomes.Symbol(outcome);
                outcomeBadgeLabel.color = badgeTextColor;
            }
        }

        /// <summary>
        /// 使用済みは再選択できません。空き枠と、通知先が無い場合も何もしません。
        /// 実際に受け付けるかどうかは、通知を受けた側が最終判断します。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (listener == null || Card == null || State == BattleSlotState.Used)
            {
                return;
            }

            listener.OnTraySlotTapped(Card.InstanceId);
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleTraySlotView),
                ReferenceCheck.Of(nameof(liftRoot), liftRoot),
                ReferenceCheck.Of(nameof(canvasGroup), canvasGroup),
                ReferenceCheck.Of(nameof(background), background),
                ReferenceCheck.Of(nameof(frame), frame),
                ReferenceCheck.Of(nameof(thumbnail), thumbnail),
                ReferenceCheck.Of(nameof(attributeChip), attributeChip),
                ReferenceCheck.Of(nameof(orderLabel), orderLabel),
                ReferenceCheck.Of(nameof(attributeLabel), attributeLabel),
                ReferenceCheck.Of(nameof(usedLabel), usedLabel),
                ReferenceCheck.Of(nameof(outcomeBadgeRoot), outcomeBadgeRoot),
                ReferenceCheck.Of(nameof(outcomeBadgeGroup), outcomeBadgeGroup),
                ReferenceCheck.Of(nameof(outcomeBadgeImage), outcomeBadgeImage),
                ReferenceCheck.Of(nameof(outcomeBadgeLabel), outcomeBadgeLabel));
        }

        /// <summary>結果ごとのバッジ色。未戦闘は使いません。</summary>
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

        /// <summary>
        /// バッジを見た目の根（Lift）の最前面へ置きます。
        /// 立ち絵や属性バーより後に描かれるため、重なっても隠れません。
        /// </summary>
        private void BringBadgeToFront()
        {
            if (outcomeBadgeRoot != null)
            {
                outcomeBadgeRoot.transform.SetAsLastSibling();
            }
        }

        private void CacheRestPosition()
        {
            if (hasRestPosition || liftRoot == null)
            {
                return;
            }

            restPosition = liftRoot.anchoredPosition;
            hasRestPosition = true;
        }

        private void ApplyLift(float lift)
        {
            if (liftRoot == null)
            {
                return;
            }

            CacheRestPosition();

            liftRoot.anchoredPosition = new Vector2(
                restPosition.x,
                restPosition.y + lift);
        }
    }
}
