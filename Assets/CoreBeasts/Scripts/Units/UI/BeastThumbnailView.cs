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
    ///
    /// 敗北表示（<see cref="SetDefeated"/>）では primary / secondary を
    /// ニュートラルグレーへ置き換え、3層とも不透明度を下げます。
    /// base の色は変えないため、線画とシルエットは読めるまま残ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeastThumbnailView : MonoBehaviour
    {
        [SerializeField] private RawImage baseLayer;
        [SerializeField] private RawImage primaryLayer;
        [SerializeField] private RawImage secondaryLayer;

        [Tooltip("マスク着色の不透明度。下の陰影と線画を残すため1未満にします。")]
        [SerializeField] [Range(0.3f, 1f)] private float tintAlpha = 0.78f;

        [Header("Defeated")]
        [Tooltip("敗北時に属性レイヤーを置き換えるニュートラルグレー。")]
        [SerializeField] private Color defeatedTint = DefeatTint.Neutral;

        [Tooltip("敗北時のキャラクター不透明度。読めなくならない範囲まで下げます。")]
        [SerializeField] [Range(0.4f, 1f)] private float defeatedAlpha =
            DefeatTint.CharacterAlpha;

        private AttributeColorResolver.Colors colors;
        private bool hasContent;

        /// <summary>直近に適用した配色（確認・テスト用）。敗北表示でも書き換えません。</summary>
        public AttributeColorResolver.Colors LastColors { get; private set; }

        /// <summary>敗北表示になっているか。</summary>
        public bool IsDefeated { get; private set; }

        /// <summary>今 base レイヤーへ出している色（確認・テスト用）。</summary>
        public Color BaseLayerColor => baseLayer != null ? baseLayer.color : Color.clear;

        /// <summary>今 primary レイヤーへ出している色（確認・テスト用）。</summary>
        public Color PrimaryLayerColor =>
            primaryLayer != null ? primaryLayer.color : Color.clear;

        /// <summary>今 secondary レイヤーへ出している色（確認・テスト用）。</summary>
        public Color SecondaryLayerColor =>
            secondaryLayer != null ? secondaryLayer.color : Color.clear;

        /// <summary>着色済みの縮小立ち絵を表示します。敗北表示は解除されます。</summary>
        /// <summary>
        /// 実行時に組み立てた3層を渡します（Home の獲得画面のように、プレハブを使わず起動時に一度だけ組み立てる場合）。
        /// 既存のプレハブ・シーンはシリアライズされた参照をそのまま使うため、この呼び出しは不要です。
        /// </summary>
        public void Bind(RawImage baseImage, RawImage primaryImage, RawImage secondaryImage)
        {
            baseLayer = baseImage;
            primaryLayer = primaryImage;
            secondaryLayer = secondaryImage;
            Clear();
        }

        public void Show(CoreBeastDefinition definition, AttributePalette palette)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            colors = AttributeColorResolver.Resolve(palette, definition);
            LastColors = colors;

            hasContent = true;

            // 中身を入れ替えたら、前の個体の敗北表示は持ち越しません。
            IsDefeated = false;

            ApplyLayers();
        }

        /// <summary>
        /// 敗北表示を切り替えます。表示中の個体はそのままで、色だけを変えます。
        /// 中身が無いときは何も起きません。
        /// </summary>
        public void SetDefeated(bool defeated)
        {
            if (IsDefeated == defeated)
            {
                return;
            }

            IsDefeated = defeated;

            if (hasContent)
            {
                ApplyLayers();
            }
        }

        /// <summary>空き枠などで非表示にします。</summary>
        public void Clear()
        {
            hasContent = false;
            IsDefeated = false;

            SetLayer(baseLayer, Color.white, false);
            SetLayer(primaryLayer, Color.white, false);
            SetLayer(secondaryLayer, Color.white, false);
        }

        /// <summary>
        /// 3層の色を今の状態から作り直します。
        /// 通常表示と敗北表示の違いはここだけに閉じています。
        /// </summary>
        private void ApplyLayers()
        {
            if (!hasContent)
            {
                return;
            }

            if (!IsDefeated)
            {
                SetLayer(baseLayer, Color.white, true);
                SetLayer(primaryLayer, WithAlpha(colors.Primary, tintAlpha), true);
                SetLayer(secondaryLayer, WithAlpha(colors.Secondary, tintAlpha), true);

                return;
            }

            // 属性色をニュートラルグレーへ置き換え、3層とも減彩します。
            SetLayer(baseLayer, WithAlpha(Color.white, defeatedAlpha), true);
            SetLayer(
                primaryLayer, WithAlpha(defeatedTint, tintAlpha * defeatedAlpha), true);
            SetLayer(
                secondaryLayer, WithAlpha(defeatedTint, tintAlpha * defeatedAlpha), true);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
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
