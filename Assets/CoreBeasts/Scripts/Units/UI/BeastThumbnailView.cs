using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 一覧カード・編成枠・ドラッグゴーストで使う縮小立ち絵。
    ///
    /// スプライト全体を単色で塗らず、既存のマスクPNGを重ねて着色します。
    ///   base      : 06_preview_neutral（線画・陰影・発光を含む合成済み）
    ///   primary   : 03_mask_primary   を一次色で着色
    ///   secondary : 04_mask_secondary を二次色で着色
    /// 色は<see cref="AttributeColorResolver"/>を通すため、上部詳細と食い違いません。
    /// マスクはTexture2Dのまま使うので、PNGもインポート設定も変更しません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeastThumbnailView : MonoBehaviour
    {
        [SerializeField] private RawImage baseLayer;
        [SerializeField] private RawImage primaryLayer;
        [SerializeField] private RawImage secondaryLayer;

        [Tooltip("マスク着色の不透明度。下の陰影と線画を残すため1未満にします。")]
        [SerializeField] [Range(0.3f, 1f)] private float tintAlpha = 0.78f;

        /// <summary>直近に適用した配色（確認・テスト用）。</summary>
        public AttributeColorResolver.Colors LastColors { get; private set; }

        /// <summary>着色済みの縮小立ち絵を表示します。</summary>
        public void Show(CoreBeastDefinition definition, AttributePalette palette)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            AttributeColorResolver.Colors colors =
                AttributeColorResolver.Resolve(palette, definition);

            LastColors = colors;

            SetLayer(baseLayer, Color.white, true);
            SetLayer(primaryLayer, WithTintAlpha(colors.Primary), true);
            SetLayer(secondaryLayer, WithTintAlpha(colors.Secondary), true);
        }

        /// <summary>空き枠などで非表示にします。</summary>
        public void Clear()
        {
            SetLayer(baseLayer, Color.white, false);
            SetLayer(primaryLayer, Color.white, false);
            SetLayer(secondaryLayer, Color.white, false);
        }

        private Color WithTintAlpha(Color color)
        {
            color.a = tintAlpha;
            return color;
        }

        private static void SetLayer(RawImage layer, Color color, bool visible)
        {
            if (layer == null)
            {
                return;
            }

            layer.color = color;
            layer.enabled = visible;
        }
    }
}
