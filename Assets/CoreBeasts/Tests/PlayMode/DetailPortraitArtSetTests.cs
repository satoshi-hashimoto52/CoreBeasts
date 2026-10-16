using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 詳細立ち絵が、定義の <see cref="CoreBeastArtSet"/>（6レイヤー素材）で描かれることを、
    /// 実際の UnitSet シーンの PortraitCamera → RenderTexture の画素で確かめます（Phase 8）。
    ///
    /// VOLX の素材は Material の既定と同じなので、素材を渡しても渡さなくても画素は1つも変わりません。
    /// 比較は RenderTexture（R8G8B8A8_SRGB）を 8bit で読んだ値どうしで、許容誤差は 0 です。
    /// </summary>
    public sealed class DetailPortraitArtSetTests
    {
        private const string SceneName = "UnitSet";
        private const string VolxArtPath = "Assets/CoreBeasts/Data/Art/AS_Volx.asset";

        private static readonly int BaseTextureId = Shader.PropertyToID("_Base_Texture");
        private static readonly int PrimaryMaskId = Shader.PropertyToID("_Primary_Mask");

        private ISquadRepository originalRepository;
        private BeastDetailPanel detailPanel;
        private CoreBeastView portraitView;
        private SpriteRenderer portraitRenderer;
        private Camera portraitCamera;
        private CoreBeastArtSet volx;
        private readonly List<Object> created = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

#if UNITY_EDITOR
            volx = AssetDatabase.LoadAssetAtPath<CoreBeastArtSet>(VolxArtPath);
#endif
            Assert.That(volx, Is.Not.Null, VolxArtPath + " を読めません（エディタ上でのみ実行します）。");

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }

            detailPanel = Object.FindAnyObjectByType<BeastDetailPanel>();
            portraitView = Object.FindAnyObjectByType<CoreBeastView>();
            PortraitRenderTarget target = Object.FindAnyObjectByType<PortraitRenderTarget>();

            Assert.That(detailPanel, Is.Not.Null);
            Assert.That(portraitView, Is.Not.Null);
            Assert.That(target, Is.Not.Null);

            portraitRenderer = (SpriteRenderer)GetField(portraitView, "spriteRenderer");
            portraitCamera = (Camera)GetField(target, "portraitCamera");

            Assert.That(portraitRenderer, Is.Not.Null);
            Assert.That(portraitCamera, Is.Not.Null);
            Assert.That(portraitCamera.targetTexture, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.Destroy(o);
                }
            }

            created.Clear();
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        [UnityTest]
        public IEnumerator TheVolxArtSetRendersExactlyTheMaterialDefaultPortrait()
        {
            (UnitAttribute, UnitAttribute?)[] cases =
            {
                (UnitAttribute.Red, null),
                (UnitAttribute.Green, null),
                (UnitAttribute.Blue, null),
                (UnitAttribute.Red, UnitAttribute.Blue),
            };

            foreach ((UnitAttribute primary, UnitAttribute? secondary) in cases)
            {
                string label = primary + (secondary.HasValue ? "/" + secondary.Value : string.Empty);

                portraitView.SetArt(null);
                SetAttributes(primary, secondary);
                Color32[] byDefault = Render();

                portraitView.SetArt(volx);
                SetAttributes(primary, secondary);
                Color32[] byArtSet = Render();

                Assert.That(byArtSet.Length, Is.EqualTo(byDefault.Length));

                int different = 0;

                for (int i = 0; i < byDefault.Length; i++)
                {
                    if (!byDefault[i].Equals(byArtSet[i]))
                    {
                        different++;
                    }
                }

                Assert.That(different, Is.EqualTo(0), label + ": VOLX の素材で描いた詳細立ち絵が、既定の描画と違います。");
                Assert.That(CountForeground(byArtSet), Is.GreaterThan(1000), label + ": 立ち絵が描かれていません。");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ShowingAUnitBindsItsArtSetToTheDetailPortrait()
        {
            OwnedCoreBeast beast = FirstOwnedWithArt();

            detailPanel.Show(beast);
            yield return null;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            portraitRenderer.GetPropertyBlock(block);

            Assert.That(portraitView.Art, Is.SameAs(beast.Definition.Art));
            Assert.That(portraitRenderer.sprite, Is.SameAs(beast.Definition.Art.Preview));
            Assert.That(block.GetTexture(BaseTextureId), Is.SameAs(beast.Definition.Art.BaseTexture));
            Assert.That(block.GetTexture(PrimaryMaskId), Is.SameAs(beast.Definition.Art.PrimaryMask));
        }

        [UnityTest]
        public IEnumerator AnotherArtSetChangesThePixelsAndClearingItRestoresTheDefault()
        {
            portraitView.SetArt(null);
            SetAttributes(UnitAttribute.Red, null);
            Sprite defaultSprite = portraitRenderer.sprite;
            Color32[] before = Render();

            // 元の絵だけを黒く塗った別の種（形は VOLX と同じ）。マスクは VOLX のものを使います。
            CoreBeastArtSet other = CreateArtSet(BlackBase(volx.Preview));

            portraitView.SetArt(other);
            Color32[] withOther = Render();

            Assert.That(portraitRenderer.sprite, Is.SameAs(other.Preview));
            Assert.That(CountDifferent(before, withOther), Is.GreaterThan(1000), "別の種の素材が描画に反映されていません。");

            portraitView.SetArt(null);
            Color32[] after = Render();

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            portraitRenderer.GetPropertyBlock(block);

            Assert.That(portraitRenderer.sprite, Is.SameAs(defaultSprite), "既定の Sprite へ戻します。");
            Assert.That(block.HasTexture(BaseTextureId), Is.False, "素材の上書きを残しません。");
            Assert.That(CountDifferent(before, after), Is.EqualTo(0), "素材を外すと元の描画へ戻ります。");

            yield return null;
        }

        // ---------------- 道具 ----------------

        private void SetAttributes(UnitAttribute primary, UnitAttribute? secondary)
        {
            if (secondary.HasValue)
            {
                portraitView.SetAttributes(primary, secondary.Value);
            }
            else
            {
                portraitView.SetAttribute(primary);
            }
        }

        private Color32[] Render()
        {
            portraitCamera.Render();

            RenderTexture target = portraitCamera.targetTexture;
            Texture2D readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                readback.Apply(false);

                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        private static int CountForeground(Color32[] pixels)
        {
            Color32 background = pixels[0];
            int count = 0;

            foreach (Color32 p in pixels)
            {
                if (p.r != background.r || p.g != background.g || p.b != background.b)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDifferent(Color32[] a, Color32[] b)
        {
            int count = 0;

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private OwnedCoreBeast FirstOwnedWithArt()
        {
            UnitSetScreen screen = Object.FindAnyObjectByType<UnitSetScreen>();
            CoreBeastRoster roster = (CoreBeastRoster)GetField(screen, "roster");

            foreach (OwnedCoreBeast beast in roster.Owned)
            {
                if (beast.IsValid && beast.Definition.Art != null)
                {
                    return beast;
                }
            }

            Assert.Fail("素材を持つ個体が編成データにありません。");
            return null;
        }

        /// <summary>元の絵と同じ大きさ・同じアルファで、色だけ黒い Sprite。</summary>
        private Sprite BlackBase(Sprite source)
        {
            Texture2D texture = new Texture2D(source.texture.width, source.texture.height, TextureFormat.RGBA32, false);
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source.texture, rt);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }

            Color32[] pixels = texture.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, pixels[i].a);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            created.Add(texture);

            Sprite sprite = Sprite.Create(texture, source.rect, new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height), source.pixelsPerUnit);
            created.Add(sprite);

            return sprite;
        }

        private CoreBeastArtSet CreateArtSet(Sprite preview)
        {
            CoreBeastArtSet art = ScriptableObject.CreateInstance<CoreBeastArtSet>();
            created.Add(art);

            SetField(art, "preview", preview);
            SetField(art, "primaryMask", volx.PrimaryMask);
            SetField(art, "secondaryMask", volx.SecondaryMask);
            SetField(art, "emissionMask", volx.EmissionMask);

            Assert.That(art.IsComplete, Is.True);
            return art;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            field.SetValue(target, value);
        }
    }
}
