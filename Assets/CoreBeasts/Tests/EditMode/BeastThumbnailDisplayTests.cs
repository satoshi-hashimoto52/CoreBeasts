using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 縮小立ち絵（<see cref="BeastThumbnailView"/>）が実際に何を表示するかを、実プレハブで確かめます（Phase 8）。
    ///
    /// - 事前生成サムネイルを持つ定義は、その絵を出し、属性色のレイヤーを重ねない
    /// - 個体の定義ごとに違う絵（属性違いの VOLX）を出す
    /// - 敗北表示は、その種の無着色の元画像へ戻し、種のマスクでニュートラルグレーを重ねる（従来と同じ見え方）
    /// - 事前生成サムネイルが無い定義は、従来どおりプレハブの3層で描く（後方互換）
    /// - 正方形の立ち絵は、縦長・横長の枠でもゆがまない
    /// </summary>
    public sealed class BeastThumbnailDisplayTests
    {
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string PalettePath = "Assets/CoreBeasts/Data/AttributePalette_Default.asset";

        private static readonly string[] PrefabPaths =
        {
            "Assets/CoreBeasts/Prefabs/BeastCard.prefab",
            "Assets/CoreBeasts/Prefabs/SquadSlot.prefab",
            "Assets/CoreBeasts/Prefabs/DragGhost.prefab",
            "Assets/CoreBeasts/Prefabs/BattleTraySlot.prefab",
            "Assets/CoreBeasts/Prefabs/BattleWheelItem.prefab",
            "Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab",
        };

        private CoreBeastRoster roster;
        private AttributePalette palette;
        private GameObject canvas;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);
            palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);

            Assert.That(roster, Is.Not.Null, RosterPath);
            Assert.That(palette, Is.Not.Null, PalettePath);

            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);

            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
        }

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryThumbnailPrefabShowsTheBakedThumbnailWithoutExtraTint(string prefabPath)
        {
            BeastThumbnailView view = Spawn(prefabPath);

            foreach (OwnedCoreBeast beast in roster.Owned)
            {
                CoreBeastDefinition definition = beast.Definition;

                view.Show(definition, palette);

                Assert.That(view.UsesBakedThumbnail, Is.True, definition.name);
                Assert.That(view.DisplayedTexture, Is.SameAs(definition.BakedThumbnail.texture), definition.name + " の絵ではありません。");
                Assert.That(view.AreTintLayersVisible, Is.False, "事前生成サムネイルへ属性色を重ねると二重に着色されます。");
                Assert.That(Layer(view, "baseLayer").enabled, Is.True);
                Assert.That(Layer(view, "baseLayer").color, Is.EqualTo(Color.white));
            }
        }

        [Test]
        public void EachDefinitionShowsItsOwnArtAndTheSameDefinitionSharesIt()
        {
            BeastThumbnailView view = Spawn(PrefabPaths[0]);
            Dictionary<CoreBeastDefinition, Texture> shown = new Dictionary<CoreBeastDefinition, Texture>();

            foreach (OwnedCoreBeast beast in roster.Owned)
            {
                view.Show(beast.Definition, palette);

                if (shown.TryGetValue(beast.Definition, out Texture before))
                {
                    Assert.That(view.DisplayedTexture, Is.SameAs(before), "同じ定義は同じ絵です。");
                }

                shown[beast.Definition] = view.DisplayedTexture;
            }

            Assert.That(shown.Count, Is.GreaterThanOrEqualTo(4), "属性違いの4定義があります。");
            Assert.That(new HashSet<Texture>(shown.Values).Count, Is.EqualTo(shown.Count), "定義ごとに違う絵を出します。");
        }

        [Test]
        public void DefeatOverlaysNeutralGreyThroughTheSpeciesMasks()
        {
            BeastThumbnailView view = Spawn("Assets/CoreBeasts/Prefabs/BattleTraySlot.prefab");
            CoreBeastDefinition definition = roster.Owned[0].Definition;

            view.Show(definition, palette);
            view.SetDefeated(true);

            RawImage primary = Layer(view, "primaryLayer");
            RawImage secondary = Layer(view, "secondaryLayer");

            Assert.That(view.AreTintLayersVisible, Is.True, "敗北表示ではグレーを重ねます。");
            Assert.That(primary.texture, Is.SameAs(definition.Art.PrimaryMask), "その種のマスクで重ねます。");
            Assert.That(secondary.texture, Is.SameAs(definition.Art.SecondaryMask));
            Assert.That(Mathf.Max(Mathf.Abs(primary.color.r - primary.color.g), Mathf.Abs(primary.color.g - primary.color.b)), Is.LessThan(0.12f), "ニュートラルグレー");
            Assert.That(view.BaseLayerColor.a, Is.InRange(0.55f, 0.70f), "キャラクターの不透明度を下げます。");
            // 色付きの事前生成サムネイルを下に敷くと属性色が透けるため、同じ種の無着色の元画像へ戻します。
            Assert.That(view.DisplayedTexture, Is.SameAs(definition.Art.BaseTexture), "絵は同じ種の無着色の元画像です。");

            view.SetDefeated(false);

            Assert.That(view.AreTintLayersVisible, Is.False, "敗北表示を解除すると重ねたグレーを外します。");
            Assert.That(view.BaseLayerColor.a, Is.EqualTo(1f));
            Assert.That(view.DisplayedTexture, Is.SameAs(definition.BakedThumbnail.texture), "敗北表示を解除すると同じ個体の事前生成サムネイルへ戻します。");
        }

        [Test]
        public void DefinitionsWithoutABakedThumbnailKeepTheLegacyThreeLayers()
        {
            BeastThumbnailView view = Spawn(PrefabPaths[0]);
            Texture legacyBase = Layer(view, "baseLayer").texture;
            Texture legacyPrimary = Layer(view, "primaryLayer").texture;

            CoreBeastRoster legacy = TestRosterFactory.Create(1);
            created.Add(legacy);
            created.Add(legacy.Owned[0].Definition);

            // いったん事前生成サムネイルを出してから、無い定義へ切り替えます（テクスチャを戻せること）。
            view.Show(roster.Owned[0].Definition, palette);
            view.Show(legacy.Owned[0].Definition, palette);

            AttributeColorResolver.Colors expected = AttributeColorResolver.Resolve(palette, legacy.Owned[0].Definition);

            Assert.That(view.UsesBakedThumbnail, Is.False);
            Assert.That(view.DisplayedTexture, Is.SameAs(legacyBase), "プレハブの既定の絵へ戻します。");
            Assert.That(Layer(view, "primaryLayer").texture, Is.SameAs(legacyPrimary));
            Assert.That(view.AreTintLayersVisible, Is.True, "従来どおり3層を重ねます。");
            Assert.That(view.PrimaryLayerColor.r, Is.EqualTo(expected.Primary.r), "従来どおり一次色を重ねます。");
            Assert.That(view.PrimaryLayerColor.g, Is.EqualTo(expected.Primary.g));
            Assert.That(view.PrimaryLayerColor.b, Is.EqualTo(expected.Primary.b));
        }

        [TestCase("Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab")]
        [TestCase("Assets/CoreBeasts/Prefabs/BattleWheelItem.prefab")]
        [TestCase("Assets/CoreBeasts/Prefabs/BeastCard.prefab")]
        public void LayersStaySquareAndCentredInANonSquareFrame(string prefabPath)
        {
            BeastThumbnailView view = Spawn(prefabPath);

            view.Show(roster.Owned[0].Definition, palette);

            foreach (string name in new[] { "baseLayer", "primaryLayer", "secondaryLayer" })
            {
                RectTransform layer = Layer(view, name).rectTransform;
                RectTransform parent = (RectTransform)layer.parent;
                float side = Mathf.Min(parent.rect.width, parent.rect.height);

                Assert.That(layer.rect.width, Is.EqualTo(side).Within(0.01f), prefabPath + " " + name + " の幅");
                Assert.That(layer.rect.height, Is.EqualTo(side).Within(0.01f), prefabPath + " " + name + " の高さ");
                Assert.That(layer.rect.center + (Vector2)layer.localPosition, Is.EqualTo(parent.rect.center).Using(new Vector2Comparer(0.01f)),
                    prefabPath + " " + name + " は枠の中央です。");
            }
        }

        // ---------------- 道具 ----------------

        private BeastThumbnailView Spawn(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, prefabPath);

            GameObject instance = Object.Instantiate(prefab, canvas.transform);
            BeastThumbnailView view = instance.GetComponentInChildren<BeastThumbnailView>(true);

            Assert.That(view, Is.Not.Null, prefabPath + " に BeastThumbnailView がありません。");
            Canvas.ForceUpdateCanvases();

            return view;
        }

        private static RawImage Layer(BeastThumbnailView view, string field)
        {
            FieldInfo info = typeof(BeastThumbnailView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(info, Is.Not.Null, field);
            RawImage layer = (RawImage)info.GetValue(view);
            Assert.That(layer, Is.Not.Null, field + " が未設定です。");

            return layer;
        }

        private sealed class Vector2Comparer : IEqualityComparer<Vector2>
        {
            private readonly float tolerance;

            internal Vector2Comparer(float tolerance)
            {
                this.tolerance = tolerance;
            }

            public bool Equals(Vector2 a, Vector2 b)
            {
                return Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;
            }

            public int GetHashCode(Vector2 v)
            {
                return 0;
            }
        }
    }
}
