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
    ///
    /// Phase 8: 定義に事前生成サムネイル（<see cref="CoreBeastDefinition.BakedThumbnail"/>）があれば、
    /// base にそれを出し、primary / secondary は通常表示では出しません（色は事前生成で詳細表示と同じ式で付いています）。
    /// 敗北表示では、base をその種の無着色の元画像（06_preview_neutral）へ戻し、種のマスク（<see cref="CoreBeastArtSet"/>）で
    /// グレーを重ねます。従来と同じ重ね方なので、属性色は透けずに無彩色になります。
    /// 事前生成サムネイルが無い定義は、従来どおりプレハブの3層を重ねて描きます。
    /// 正方形の立ち絵が縦長・横長の枠でゆがまないよう、3層は枠の中央に正方形で置きます。
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
        private bool usesBakedThumbnail;
        private bool rememberedLegacy;
        private Texture legacyBase;
        private Texture legacyPrimary;
        private Texture legacySecondary;
        private Rect legacyBaseUv = new Rect(0f, 0f, 1f, 1f);
        private Texture bakedBase;
        private Rect bakedBaseUv = new Rect(0f, 0f, 1f, 1f);
        private Texture neutralBase;
        private Rect neutralBaseUv = new Rect(0f, 0f, 1f, 1f);

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

        /// <summary>事前生成サムネイルを出しているか（確認・テスト用）。</summary>
        public bool UsesBakedThumbnail => hasContent && usesBakedThumbnail;

        /// <summary>今 base レイヤーに出しているテクスチャ（確認・テスト用）。</summary>
        public Texture DisplayedTexture => baseLayer != null ? baseLayer.texture : null;

        /// <summary>primary / secondary を重ねて表示しているか（確認・テスト用）。</summary>
        public bool AreTintLayersVisible =>
            primaryLayer != null && primaryLayer.enabled && secondaryLayer != null && secondaryLayer.enabled;

        private void Awake()
        {
            RememberLegacyTextures();
            FitLayersToSquare();
        }

        private void OnRectTransformDimensionsChange()
        {
            FitLayersToSquare();
        }

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
            rememberedLegacy = false;
            RememberLegacyTextures();
            FitLayersToSquare();
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
            SelectTextures(definition);
            FitLayersToSquare();

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

                if (usesBakedThumbnail)
                {
                    SetTexture(baseLayer, bakedBase, bakedBaseUv);

                    // 色は事前生成で付いています。重ねると二重に着色されます。
                    SetLayer(primaryLayer, Color.white, false);
                    SetLayer(secondaryLayer, Color.white, false);

                    return;
                }

                SetLayer(primaryLayer, WithAlpha(colors.Primary, tintAlpha), true);
                SetLayer(secondaryLayer, WithAlpha(colors.Secondary, tintAlpha), true);

                return;
            }

            // 事前生成サムネイルは色付きのため、下に敷くと属性色が透けます。無着色の元画像へ戻します。
            if (usesBakedThumbnail)
            {
                SetTexture(baseLayer, neutralBase, neutralBaseUv);
            }

            // 属性色をニュートラルグレーへ置き換え、3層とも減彩します。
            SetLayer(baseLayer, WithAlpha(Color.white, defeatedAlpha), true);
            SetLayer(
                primaryLayer, WithAlpha(defeatedTint, tintAlpha * defeatedAlpha), true);
            SetLayer(
                secondaryLayer, WithAlpha(defeatedTint, tintAlpha * defeatedAlpha), true);
        }

        /// <summary>
        /// 事前生成サムネイルがあればそれと種のマスクを、無ければプレハブの3層（VOLX）を使います。
        /// </summary>
        private void SelectTextures(CoreBeastDefinition definition)
        {
            RememberLegacyTextures();

            Sprite baked = definition.BakedThumbnail;
            CoreBeastArtSet art = definition.Art;

            usesBakedThumbnail = baked != null && art != null && art.IsComplete;

            if (usesBakedThumbnail)
            {
                bakedBase = baked.texture;
                bakedBaseUv = SpriteUv(baked);
                neutralBase = art.BaseTexture;
                neutralBaseUv = SpriteUv(art.Preview);

                SetTexture(baseLayer, bakedBase, bakedBaseUv);
                SetTexture(primaryLayer, art.PrimaryMask, new Rect(0f, 0f, 1f, 1f));
                SetTexture(secondaryLayer, art.SecondaryMask, new Rect(0f, 0f, 1f, 1f));

                return;
            }

            SetTexture(baseLayer, legacyBase, legacyBaseUv);
            SetTexture(primaryLayer, legacyPrimary, new Rect(0f, 0f, 1f, 1f));
            SetTexture(secondaryLayer, legacySecondary, new Rect(0f, 0f, 1f, 1f));
        }

        private void RememberLegacyTextures()
        {
            if (rememberedLegacy)
            {
                return;
            }

            rememberedLegacy = true;
            legacyBase = baseLayer != null ? baseLayer.texture : null;
            legacyBaseUv = baseLayer != null ? baseLayer.uvRect : new Rect(0f, 0f, 1f, 1f);
            legacyPrimary = primaryLayer != null ? primaryLayer.texture : null;
            legacySecondary = secondaryLayer != null ? secondaryLayer.texture : null;
        }

        /// <summary>
        /// 3層を、それぞれの親（枠、またはカードの持ち上げ用の階層）の中央へ、短い辺に合わせた正方形で置きます。
        /// 立ち絵（正方形）が枠の形でゆがまないためです。枠や親の RectTransform そのものは動かしません。
        /// </summary>
        private void FitLayersToSquare()
        {
            FitLayer(baseLayer);
            FitLayer(primaryLayer);
            FitLayer(secondaryLayer);
        }

        private static void FitLayer(RawImage layer)
        {
            if (layer == null || !(layer.rectTransform.parent is RectTransform parent))
            {
                return;
            }

            float size = Mathf.Min(Mathf.Abs(parent.rect.width), Mathf.Abs(parent.rect.height));

            if (size <= 0f)
            {
                return;
            }

            RectTransform rect = layer.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
        }

        private static Rect SpriteUv(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;

            return new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
        }

        private static void SetTexture(RawImage layer, Texture texture, Rect uv)
        {
            if (layer == null)
            {
                return;
            }

            layer.texture = texture;
            layer.uvRect = uv;
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
