using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;
#endif

namespace CoreBeasts.Units
{
    /// <summary>
    /// Core Beast 1種ぶんの立ち絵素材（6レイヤー方式の正本）。属性違いの定義どうしで共有します。
    ///
    /// 6レイヤーは同じフォルダに次の名前で置きます（2048 x 2048、全レイヤーのピクセル位置一致、上下左右 8% 以上の余白）:
    ///   01_lineart / 02_shading / 03_mask_primary / 04_mask_secondary / 05_mask_emission / 06_preview_neutral
    ///
    /// 実行時に使うのは 06（元の絵）と 03〜05（マスク）の4枚だけです。01 / 02 は制作用の正本で、
    /// ここから参照しないのでビルドにもメモリにも乗りません（検査テストはフォルダの規約で確かめます）。
    /// マスクはアルファチャンネルだけを使います（<c>CoreBeastTint.shadergraph</c> と同じ）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "AS_NewBeast",
        menuName = "CoreBeasts/Core Beast Art Set"
    )]
    public sealed class CoreBeastArtSet : ScriptableObject
    {
        /// <summary>6レイヤーの元画像の一辺（px）。</summary>
        public const int SourceSize = 2048;

        /// <summary>上下左右に空ける余白の下限（一辺に対する割合）。</summary>
        public const float MinMargin = 0.08f;

        /// <summary>事前生成するサムネイルの一辺（px）。</summary>
        public const int ThumbnailSize = 512;

        public const string LineartName = "01_lineart";
        public const string ShadingName = "02_shading";
        public const string PrimaryMaskName = "03_mask_primary";
        public const string SecondaryMaskName = "04_mask_secondary";
        public const string EmissionMaskName = "05_mask_emission";
        public const string PreviewName = "06_preview_neutral";

        [SerializeField]
        [Tooltip("06_preview_neutral（Sprite）。詳細表示の SpriteRenderer とシェーダーの _Base_Texture に使います。")]
        private Sprite preview;

        [SerializeField]
        [Tooltip("03_mask_primary。アルファが一次属性色の範囲です。")]
        private Texture2D primaryMask;

        [SerializeField]
        [Tooltip("04_mask_secondary。アルファが二次属性色の範囲です。")]
        private Texture2D secondaryMask;

        [SerializeField]
        [Tooltip("05_mask_emission。アルファが発光の範囲です。")]
        private Texture2D emissionMask;

        public Sprite Preview => preview;

        public Texture2D BaseTexture => preview != null ? preview.texture : null;

        public Texture2D PrimaryMask => primaryMask;

        public Texture2D SecondaryMask => secondaryMask;

        public Texture2D EmissionMask => emissionMask;

        /// <summary>実行時に使う4レイヤーがすべて設定されているか。</summary>
        public bool IsComplete =>
            preview != null && primaryMask != null && secondaryMask != null && emissionMask != null;

#if UNITY_EDITOR
        /// <summary>
        /// 実行時に使う4レイヤーの元 PNG の中身から作る識別子（エディタ専用）。
        /// 取り込み設定（iOS の圧縮など）を変えても変わらず、絵を描き直したときだけ変わります。
        /// 事前生成サムネイルが古くなっていないかの判定に使います。
        /// </summary>
        public string EditorLayerIdentity()
        {
            StringBuilder identity = new StringBuilder();

            foreach (Object layer in new Object[] { BaseTexture, primaryMask, secondaryMask, emissionMask })
            {
                string path = layer != null ? AssetDatabase.GetAssetPath(layer) : string.Empty;

                identity.Append(Path.GetFileName(path)).Append('=');

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using (SHA256 sha = SHA256.Create())
                    {
                        foreach (byte b in sha.ComputeHash(File.ReadAllBytes(path)))
                        {
                            identity.Append(b.ToString("x2"));
                        }
                    }
                }

                identity.Append(';');
            }

            return identity.ToString();
        }
#endif
    }
}
