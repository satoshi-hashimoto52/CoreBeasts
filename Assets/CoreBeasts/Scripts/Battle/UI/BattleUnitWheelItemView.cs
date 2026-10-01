using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// リング上の未使用ユニット1体ぶんの表示。
    ///
    /// 出すのはキャラクター・発進台・編成番号・レベルだけです。
    /// POWER / CORE / スキル・個体名は載せません。
    /// 文字をキャラクターへ重ねないため、名前は戦闘表示側が受け持ちます。
    ///
    /// 大きな長方形カードは持ちません。足元の発進台
    /// （<see cref="LaunchPedestalGraphic"/>）が属性色を示します。
    ///
    /// 勝敗も使用済みも持ちません。リングに居る＝未使用です。
    /// 配置の値は<see cref="BattleRingLayout"/>が決め、ここは受け取って適用するだけです。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleUnitWheelItemView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private BeastThumbnailView thumbnail;

        [Header("Launch pedestal")]
        [Tooltip("発進台の内側の面。暗い半透明でキャラクターの足元を受けます。")]
        [SerializeField] private LaunchPedestalGraphic pedestalFill;

        [Tooltip("発進台の外周。属性色で出します。")]
        [SerializeField] private LaunchPedestalGraphic attributeRim;

        [Tooltip("中央のときだけ灯す発光。")]
        [SerializeField] private LaunchPedestalGraphic activeGlow;

        [Header("Minimal state")]
        [Tooltip("元の編成番号1〜7。使用済みが抜けても変わりません。")]
        [SerializeField] private TMP_Text orderLabel;

        [SerializeField] private TMP_Text levelLabel;

        [Tooltip("中央であることを示す小さな印。")]
        [SerializeField] private GameObject selectedIndicator;

        /// <summary>この枠が示している個体ID。空なら未使用の表示枠です。</summary>
        public string InstanceId { get; private set; }

        /// <summary>元の編成番号。</summary>
        public int SquadNumber { get; private set; }

        /// <summary>直近に適用した拡大率（確認・テスト用）。</summary>
        public float AppliedScale { get; private set; } = 1f;

        /// <summary>直近に適用した不透明度（確認・テスト用）。</summary>
        public float AppliedAlpha { get; private set; } = 1f;

        /// <summary>直近に適用した中央からの距離（確認・テスト用）。</summary>
        public float AppliedDistance { get; private set; }

        /// <summary>動かす対象。出撃の持ち上げもここへ掛けます。</summary>
        public RectTransform Root => root != null ? root : (RectTransform)transform;

        /// <summary>立ち絵。出撃ゴーストの見た目を合わせるために使います。</summary>
        public BeastThumbnailView Thumbnail => thumbnail;

        /// <summary>中央として強調しているか（確認・テスト用）。</summary>
        public bool IsCentre { get; private set; }

        /// <summary>発進台の外周（確認・テスト用）。</summary>
        public LaunchPedestalGraphic AttributeRim => attributeRim;

        /// <summary>1体ぶんの中身を流し込みます。</summary>
        public void Bind(
            BattleRingSlot slot,
            BattleUnitCard card,
            AttributePalette palette,
            UiTextCatalog text)
        {
            InstanceId = slot.InstanceId;
            SquadNumber = slot.SquadNumber;

            bool hasCard = card != null && card.Definition != null;

            if (orderLabel != null && text != null)
            {
                orderLabel.text = text.FormatSlotOrder(slot.SquadNumber);
            }

            if (levelLabel != null && text != null && hasCard)
            {
                levelLabel.text = text.FormatLevel(card.Level);
            }

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

            if (hasCard)
            {
                // 属性色の実体は AttributePalette だけが持ちます。ここでは受け取るだけです。
                AttributeColorResolver.ResolveCardColors(
                    palette,
                    card.Definition,
                    out Color primary,
                    out Color secondary,
                    out bool isDual);

                if (attributeRim != null)
                {
                    attributeRim.Apply(primary, secondary, isDual);
                }

                if (activeGlow != null)
                {
                    activeGlow.Apply(primary, secondary, isDual);
                }
            }
        }

        /// <summary>
        /// 中央からの距離に応じた見え方を適用します。
        /// 距離は小数でも構いません（スワイプ中の連続変化のためです）。
        /// </summary>
        public void ApplySample(
            BattleRingSample sample,
            float signedDistance,
            float horizontalPoints,
            float pointsToUnits)
        {
            AppliedScale = sample.Scale;
            AppliedAlpha = sample.Alpha;
            AppliedDistance = signedDistance;

            // 中央ほど明るく、外側ほど暗くします。
            if (pedestalFill != null)
            {
                pedestalFill.SetOpacity(sample.Alpha);
            }

            if (attributeRim != null)
            {
                attributeRim.SetOpacity(sample.Alpha);
            }

            RectTransform target = Root;

            target.anchoredPosition = new Vector2(
                horizontalPoints * pointsToUnits,
                sample.OffsetY * pointsToUnits);

            target.localScale = new Vector3(sample.Scale, sample.Scale, 1f);

            // キャラクターは立たせたままにします。円弧へ沿わせるのは位置だけです。
            target.localRotation = Quaternion.identity;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = sample.Alpha;
            }
        }

        /// <summary>出撃の持ち上げぶんを、通常の配置へ足します。</summary>
        public void AddLift(float liftPoints, float scaleBoost, float pointsToUnits)
        {
            RectTransform target = Root;

            Vector2 position = target.anchoredPosition;
            target.anchoredPosition =
                new Vector2(position.x, position.y + liftPoints * pointsToUnits);

            float scale = AppliedScale * scaleBoost;
            target.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 中央かどうかを反映します。中央だけ発光と印を出します。
        /// 文字は増やしません（キャラクターへ重ねないためです）。
        /// </summary>
        public void SetCentre(bool centre)
        {
            IsCentre = centre;

            if (activeGlow != null &&
                activeGlow.gameObject.activeSelf != centre)
            {
                activeGlow.gameObject.SetActive(centre);
            }

            if (selectedIndicator != null &&
                selectedIndicator.activeSelf != centre)
            {
                selectedIndicator.SetActive(centre);
            }
        }

        /// <summary>この枠を出すかどうか。リング裏側は隠します。</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleUnitWheelItemView),
                ReferenceCheck.Of(nameof(root), root),
                ReferenceCheck.Of(nameof(canvasGroup), canvasGroup),
                ReferenceCheck.Of(nameof(thumbnail), thumbnail),
                ReferenceCheck.Of(nameof(pedestalFill), pedestalFill),
                ReferenceCheck.Of(nameof(attributeRim), attributeRim),
                ReferenceCheck.Of(nameof(activeGlow), activeGlow),
                ReferenceCheck.Of(nameof(orderLabel), orderLabel),
                ReferenceCheck.Of(nameof(levelLabel), levelLabel),
                ReferenceCheck.Of(nameof(selectedIndicator), selectedIndicator));
        }
    }
}
