using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 立ち絵用カメラの描画先RenderTextureを実行時に作り、UIのRawImageへつなぎます。
    /// 既存のCoreBeastView(SpriteRenderer)をそのままUIへ出すための橋渡しです。
    ///
    /// URPのRender Graphは、カメラのtargetTextureにデプスバッファを要求します。
    /// デプス無し(GraphicsFormat.None)で作るとレンダーパスを構築できず、
    /// 毎フレーム描画エラーになるため、必ずdepthStencilFormatを指定します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitRenderTarget : MonoBehaviour
    {
        /// <summary>要求するデプスビット数。</summary>
        public const int MinimumDepthBits = 24;

        /// <summary>要求するステンシルビット数。</summary>
        public const int MinimumStencilBits = 8;

        [SerializeField] private Camera portraitCamera;
        [SerializeField] private RawImage targetImage;

        [SerializeField] [Min(64)] private int width = 512;
        [SerializeField] [Min(64)] private int height = 640;

        [SerializeField]
        [Tooltip("実行時に生成した立ち絵オブジェクトがあれば、破棄対象として設定します。")]
        private GameObject runtimePortraitRoot;

        private RenderTexture renderTexture;
        private bool hasLoggedMissingReference;

        /// <summary>現在割り当てているRenderTexture。未生成なら null。</summary>
        public RenderTexture Texture => renderTexture;

        /// <summary>
        /// URPで使えるデプス付きRenderTextureを作ります。
        /// 生成に失敗した場合は null を返します。
        /// </summary>
        public static RenderTexture CreatePortraitTexture(int width, int height)
        {
            GraphicsFormat depthStencilFormat = ResolveDepthStencilFormat();

            if (depthStencilFormat == GraphicsFormat.None)
            {
                Debug.LogError(
                    "[PortraitRenderTarget] 利用可能なDepth Stencil Formatが" +
                    "見つからないため、立ち絵用RenderTextureを作成できません。"
                );

                return null;
            }

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                GraphicsFormat.R8G8B8A8_SRGB,
                depthStencilFormat)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = "RT_BeastPortrait",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            if (!texture.Create())
            {
                Debug.LogError(
                    "[PortraitRenderTarget] RenderTexture.Create()に失敗しました。"
                );

                DestroyTexture(texture);
                return null;
            }

            return texture;
        }

        /// <summary>
        /// デプス付きの描画先として使える形式を、優先順に探します。
        /// </summary>
        private static GraphicsFormat ResolveDepthStencilFormat()
        {
            GraphicsFormat[] candidates =
            {
                GraphicsFormatUtility.GetDepthStencilFormat(
                    MinimumDepthBits, MinimumStencilBits),
                GraphicsFormatUtility.GetDepthStencilFormat(MinimumDepthBits, 0),
                GraphicsFormatUtility.GetDepthStencilFormat(16, 0),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                GraphicsFormat candidate = candidates[i];

                if (candidate != GraphicsFormat.None &&
                    SystemInfo.IsFormatSupported(
                        candidate, GraphicsFormatUsage.Render))
                {
                    return candidate;
                }
            }

            return GraphicsFormat.None;
        }

        /// <summary>
        /// 描画先を破棄します。何度呼んでも安全です。
        /// 順序: Camera.targetTexture → RawImage.texture →
        /// Release() → Destroy() → 実行時生成オブジェクト。
        /// </summary>
        public void ReleaseTarget()
        {
            if (portraitCamera != null)
            {
                portraitCamera.targetTexture = null;
            }

            if (targetImage != null)
            {
                targetImage.texture = null;
            }

            if (renderTexture != null)
            {
                DestroyTexture(renderTexture);
                renderTexture = null;
            }

            if (runtimePortraitRoot != null)
            {
                DestroyUnityObject(runtimePortraitRoot);
                runtimePortraitRoot = null;
            }
        }

        private void OnEnable()
        {
            CreateTarget();
        }

        private void OnDisable()
        {
            ReleaseTarget();
        }

        private void OnDestroy()
        {
            ReleaseTarget();
        }

        /// <summary>
        /// 描画先を用意します。既に生成済みなら作り直しません。
        /// </summary>
        private void CreateTarget()
        {
            if (portraitCamera == null || targetImage == null)
            {
                if (!hasLoggedMissingReference)
                {
                    hasLoggedMissingReference = true;

                    ReferenceCheck.Validate(
                        this,
                        nameof(PortraitRenderTarget),
                        ReferenceCheck.Of(nameof(portraitCamera), portraitCamera),
                        ReferenceCheck.Of(nameof(targetImage), targetImage));
                }

                return;
            }

            hasLoggedMissingReference = false;

            if (renderTexture != null && renderTexture.IsCreated())
            {
                Bind();
                return;
            }

            renderTexture = CreatePortraitTexture(width, height);

            if (renderTexture == null)
            {
                return;
            }

            Bind();
        }

        private void Bind()
        {
            portraitCamera.targetTexture = renderTexture;
            targetImage.texture = renderTexture;
        }

        private static void DestroyTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            DestroyUnityObject(texture);
        }

        private static void DestroyUnityObject(Object target)
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

        private void LogMissingReferenceOnce()
        {
            if (hasLoggedMissingReference)
            {
                return;
            }

            hasLoggedMissingReference = true;

            Debug.LogError(
                $"[PortraitRenderTarget] GameObject「{name}」の" +
                "CameraまたはRawImageが未設定のため、立ち絵を表示できません。",
                this
            );
        }
    }
}
