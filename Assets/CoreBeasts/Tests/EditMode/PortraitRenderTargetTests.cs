using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    public sealed class PortraitRenderTargetTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
                host = null;
            }
        }

        [Test]
        public void CreatePortraitTexture_HasDepthStencilFormat()
        {
            RenderTexture texture =
                PortraitRenderTarget.CreatePortraitTexture(128, 160);

            Assert.That(texture, Is.Not.Null, "RenderTextureを作成できませんでした。");

            try
            {
                Assert.That(
                    texture.depthStencilFormat,
                    Is.Not.EqualTo(GraphicsFormat.None),
                    "URPのRender Graphはデプス付きの描画先を要求します。"
                );

                Assert.That(
                    texture.IsCreated(),
                    Is.True,
                    "RenderTexture.Create()が成功している必要があります。"
                );
            }
            finally
            {
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ReleaseTarget_IsSafeWhenCalledRepeatedly()
        {
            host = new GameObject("PortraitRig");
            host.SetActive(false);

            Camera camera = host.AddComponent<Camera>();

            GameObject imageObject = new GameObject("Portrait", typeof(RectTransform));
            imageObject.transform.SetParent(host.transform, false);
            RawImage rawImage = imageObject.AddComponent<RawImage>();

            PortraitRenderTarget target = host.AddComponent<PortraitRenderTarget>();
            SetField(target, "portraitCamera", camera);
            SetField(target, "targetImage", rawImage);
            SetField(target, "width", 128);
            SetField(target, "height", 160);

            host.SetActive(true);

            // EditModeではUnityがOnEnableを配送しないため、実機と同じ順序で呼びます。
            EditModeLifecycle.Enable(target);

            Assert.That(target.Texture, Is.Not.Null, "有効化時に生成されるべきです。");
            Assert.That(camera.targetTexture, Is.SameAs(target.Texture));
            Assert.That(rawImage.texture, Is.SameAs(target.Texture));

            Assert.DoesNotThrow(() => target.ReleaseTarget());
            Assert.DoesNotThrow(() => target.ReleaseTarget());
            Assert.DoesNotThrow(() => target.ReleaseTarget());

            Assert.That(target.Texture, Is.Null);
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(rawImage.texture, Is.Null);
        }

        [Test]
        public void EnablingTwice_DoesNotCreateASecondTexture()
        {
            host = new GameObject("PortraitRig");
            host.SetActive(false);

            Camera camera = host.AddComponent<Camera>();

            GameObject imageObject = new GameObject("Portrait", typeof(RectTransform));
            imageObject.transform.SetParent(host.transform, false);
            RawImage rawImage = imageObject.AddComponent<RawImage>();

            PortraitRenderTarget target = host.AddComponent<PortraitRenderTarget>();
            SetField(target, "portraitCamera", camera);
            SetField(target, "targetImage", rawImage);

            host.SetActive(true);
            EditModeLifecycle.Enable(target);

            RenderTexture first = target.Texture;

            target.enabled = false;
            EditModeLifecycle.Disable(target);

            target.enabled = true;
            EditModeLifecycle.Enable(target);

            Assert.That(target.Texture, Is.Not.Null);
            Assert.That(
                target.Texture.IsCreated(),
                Is.True,
                "再有効化後も使える状態である必要があります。"
            );

            // 無効化で破棄しているため、古いインスタンスは残りません。
            Assert.That(first == null || !ReferenceEquals(first, target.Texture),
                Is.True);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.That(field, Is.Not.Null, name + " が見つかりません。");
            field.SetValue(target, value);
        }
    }
}
