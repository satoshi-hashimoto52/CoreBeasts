using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 敗北表示の実際の画素が無彩色になることの回帰テスト（Phase 8）。
    ///
    /// 事前生成サムネイル（色付き）を下に敷いたままグレーを重ねると、属性色が透けて見えました。
    /// Battle の大きな立ち絵（実際の Battle シーンの <see cref="BattleCombatantView"/> の立ち絵）と、
    /// 履歴欄（BattleHistorySlot プレハブ）を、テスト用のカメラで RenderTexture へ描いて画素で確かめます。
    ///
    /// - 色空間: プロジェクトは Linear。sRGB の RenderTexture（R8G8B8A8_SRGB）へ描き、sRGB の 8bit で読みます。
    /// - 判定対象: 立ち絵の3層だけを描き（枠・バッジなど他の Graphic は描く間だけ消します）、背景は黒で塗ります。
    ///   RGB の最大値が <see cref="JudgedBrightness"/> 未満の画素（背景、線画の黒）は彩度が意味を持たないため除外します。
    ///   シルエットの縁（上下左右のどれかが除外された画素）も除外します。元画像のほぼ透明な画素（アルファ 2/255 程度）に
    ///   見えない色が残っており、縮小時の補間で縁の1画素へ混ざるためです（属性色ではなく、従来の表示でも同じです）。
    /// - 彩度: sRGB 8bit の max(R,G,B) − min(R,G,B)。
    /// - 許容誤差: 敗北中は判定対象の画素がすべて <see cref="GreyChromaLimit"/> 以下。重ねるグレー（#7E8490）そのものが
    ///   18/255 の青みを持つため、それにテクスチャの補間と 8bit の丸めの分 6/255 を足した値です。
    ///   通常表示へ戻したときは、敗北前と RGB 各チャンネル 1/255 以内で一致します（同じテクスチャ・同じ色のため）。
    /// </summary>
    public sealed class BattleDefeatGreyPixelTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string PalettePath = "Assets/CoreBeasts/Data/AttributePalette_Default.asset";
        private const string HistorySlotPath = "Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab";
        private const int Size = 512;
        private const int RenderLayer = 31;

        /// <summary>この明るさ（sRGB 8bit の RGB 最大値）以上の画素だけを判定します。</summary>
        private const int JudgedBrightness = 40;

        /// <summary>敗北中に許す彩度の上限（sRGB 8bit）。</summary>
        private const int GreyChromaLimit = 24;

        /// <summary>通常表示で、属性色が付いているとみなす画素の割合の下限（判定が属性色を見分けられることの前提）。</summary>
        private const float ColouredShareInNormal = 0.25f;

        /// <summary>判定する画素の最小数。これより少なければ立ち絵が描かれていないとみなします。</summary>
        private const int MinimumJudgedPixels = 1000;

        private ISquadRepository originalRepository;
        private CoreBeastRoster roster;
        private AttributePalette palette;
        private RenderTexture target;
        private Camera camera;
        private RectTransform frame;
        private readonly List<Object> created = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleCpuScript.Install(BattleCpuScript.MostVersatile);

            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

#if UNITY_EDITOR
            roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);
            palette = AssetDatabase.LoadAssetAtPath<AttributePalette>(PalettePath);
#endif
            Assert.That(roster, Is.Not.Null, RosterPath);
            Assert.That(palette, Is.Not.Null, PalettePath);

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            SquadRepositoryProvider.Shared.Save("1", new SquadSnapshot(ids));

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            CreateRig();
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

            BattleCpuScript.Uninstall();
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 大きな立ち絵 ----------------

        [UnityTest]
        public IEnumerator TheDefeatedCombatantPortraitIsAchromaticAndRestoresExactly()
        {
            BattleCombatantView combatant = Object.FindAnyObjectByType<BattleCombatantView>(FindObjectsInactive.Include);
            Assert.That(combatant, Is.Not.Null, "Battle シーンに BattleCombatantView がありません。");

            BeastThumbnailView original = (BeastThumbnailView)GetField(combatant, "thumbnail");
            Assert.That(original, Is.Not.Null, "BattleCombatantView.thumbnail が未設定です。");

            BeastThumbnailView view = Mount(original.gameObject);

            foreach (CoreBeastDefinition definition in Definitions())
            {
                view.Show(definition, palette);
                yield return null;

                Color32[] normal = Render(view);
                view.SetDefeated(true);
                Color32[] defeated = Render(view);
                view.SetDefeated(false);
                Color32[] restored = Render(view);

                AssertColouredWhenNormal(normal, "大きな立ち絵 " + definition.name);
                AssertAchromatic(defeated, "大きな立ち絵 " + definition.name);
                AssertSame(restored, normal, "大きな立ち絵 " + definition.name + " の通常表示への復帰");
            }
        }

        // ---------------- 履歴欄 ----------------

        [UnityTest]
        public IEnumerator OnlyALossInTheHistoryIsAchromaticAndWinOrDrawKeepTheNormalPortrait()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HistorySlotPath);
#endif
            Assert.That(prefab, Is.Not.Null, HistorySlotPath);

            BattleHistorySlotView slot = Mount(prefab).GetComponentInParent<BattleHistorySlotView>();
            Assert.That(slot, Is.Not.Null, HistorySlotPath + " に BattleHistorySlotView がありません。");

            BeastThumbnailView view = (BeastThumbnailView)GetField(slot, "thumbnail");
            Assert.That(view, Is.Not.Null, "BattleHistorySlotView.thumbnail が未設定です。");

            HashSet<CoreBeastDefinition> seen = new HashSet<CoreBeastDefinition>();

            foreach (OwnedCoreBeast owned in roster.Owned)
            {
                if (!seen.Add(owned.Definition))
                {
                    continue;
                }

                Assert.That(BattleUnitCard.TryCreate(owned, out BattleUnitCard card), Is.True, owned.InstanceId);

                string label = "履歴欄 " + owned.Definition.name;

                slot.Show(new BattleHistoryEntry(owned.InstanceId, 1, BattleSlotOutcome.Win), card, palette, null);
                yield return null;
                Color32[] win = Render(view);

                slot.Show(new BattleHistoryEntry(owned.InstanceId, 1, BattleSlotOutcome.Draw), card, palette, null);
                yield return null;
                Color32[] draw = Render(view);

                slot.Show(new BattleHistoryEntry(owned.InstanceId, 1, BattleSlotOutcome.Loss), card, palette, null);
                yield return null;
                Color32[] loss = Render(view);

                slot.Show(new BattleHistoryEntry(owned.InstanceId, 1, BattleSlotOutcome.Win), card, palette, null);
                yield return null;
                Color32[] winAgain = Render(view);

                AssertColouredWhenNormal(win, label + "（勝ち）");
                AssertSame(draw, win, label + " の引き分けは勝ちと同じ通常の立ち絵");
                AssertAchromatic(loss, label + "（負け）");
                AssertSame(winAgain, win, label + " の負けから勝ちへの復帰");
            }
        }

        // ---------------- 判定 ----------------

        private static void AssertAchromatic(Color32[] pixels, string label)
        {
            int judged = 0;
            int over = 0;
            int worst = 0;

            foreach (Color32 p in Judged(pixels))
            {
                judged++;
                int chroma = Chroma(p);
                worst = Mathf.Max(worst, chroma);

                if (chroma > GreyChromaLimit)
                {
                    over++;
                }
            }

            TestContext.WriteLine(label + " 敗北: 判定画素 " + judged + "、最大彩度 " + worst + "/255、上限超え " + over);

            Assert.That(judged, Is.GreaterThanOrEqualTo(MinimumJudgedPixels), label + " の立ち絵が描かれていません。");
            Assert.That(over, Is.EqualTo(0),
                label + " の敗北表示に属性色が残っています（最大彩度 " + worst + "/255、上限 " + GreyChromaLimit + "/255、超えた画素 " + over + " / " + judged + "）。");
        }

        private static void AssertColouredWhenNormal(Color32[] pixels, string label)
        {
            int judged = 0;
            int coloured = 0;

            foreach (Color32 p in Judged(pixels))
            {
                judged++;

                if (Chroma(p) > GreyChromaLimit)
                {
                    coloured++;
                }
            }

            Assert.That(judged, Is.GreaterThanOrEqualTo(MinimumJudgedPixels), label + " の立ち絵が描かれていません。");

            float share = coloured / (float)judged;
            TestContext.WriteLine(label + " 通常: 判定画素 " + judged + "、属性色の画素 " + (share * 100f).ToString("0.0") + "%");

            Assert.That(share, Is.GreaterThanOrEqualTo(ColouredShareInNormal),
                label + " の通常表示に属性色が見えません（判定が色を見分けられる前提が崩れています）。");
        }

        private static void AssertSame(Color32[] actual, Color32[] expected, string label)
        {
            int worst = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                worst = Mathf.Max(worst, Mathf.Abs(actual[i].r - expected[i].r));
                worst = Mathf.Max(worst, Mathf.Abs(actual[i].g - expected[i].g));
                worst = Mathf.Max(worst, Mathf.Abs(actual[i].b - expected[i].b));
            }

            Assert.That(worst, Is.LessThanOrEqualTo(1), label + "（RGB の最大差 " + worst + "/255）");
        }

        /// <summary>判定対象の画素（十分に明るく、シルエットの縁でない画素）。</summary>
        private static IEnumerable<Color32> Judged(Color32[] pixels)
        {
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int i = y * Size + x;

                    if (Bright(pixels[i]) && Bright(pixels[i - 1]) && Bright(pixels[i + 1]) &&
                        Bright(pixels[i - Size]) && Bright(pixels[i + Size]))
                    {
                        yield return pixels[i];
                    }
                }
            }
        }

        private static bool Bright(Color32 p)
        {
            return Mathf.Max(p.r, Mathf.Max(p.g, p.b)) >= JudgedBrightness;
        }

        private static int Chroma(Color32 p)
        {
            return Mathf.Max(p.r, Mathf.Max(p.g, p.b)) - Mathf.Min(p.r, Mathf.Min(p.g, p.b));
        }

        // ---------------- 描画 ----------------

        private void CreateRig()
        {
            target = new RenderTexture(new RenderTextureDescriptor(Size, Size, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = true,
            });
            target.Create();
            created.Add(target);

            GameObject cameraObject = new GameObject("DefeatPixelCamera");
            created.Add(cameraObject);
            cameraObject.layer = RenderLayer;
            cameraObject.transform.position = new Vector3(5000f, 5000f, -10f);

            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << RenderLayer;
            camera.targetTexture = target;

            GameObject canvasObject = new GameObject("DefeatPixelCanvas", typeof(RectTransform), typeof(Canvas));
            created.Add(canvasObject);
            canvasObject.layer = RenderLayer;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            GameObject frameObject = new GameObject("Frame", typeof(RectTransform));
            frameObject.layer = RenderLayer;
            frame = (RectTransform)frameObject.transform;
            frame.SetParent(canvasObject.transform, false);
            Stretch(frame);
        }

        /// <summary>立ち絵（またはプレハブ）を描画用の枠いっぱいに置き、その中の <see cref="BeastThumbnailView"/> を返します。</summary>
        private BeastThumbnailView Mount(GameObject source)
        {
            GameObject copy = Object.Instantiate(source, frame, false);
            created.Add(copy);

            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = RenderLayer;
            }

            copy.SetActive(true);
            Stretch((RectTransform)copy.transform);

            BeastThumbnailView view = copy.GetComponentInChildren<BeastThumbnailView>(true);
            Assert.That(view, Is.Not.Null, source.name + " に BeastThumbnailView がありません。");

            foreach (string name in new[] { "baseLayer", "primaryLayer", "secondaryLayer" })
            {
                RawImage layer = Layer(view, name);
                Assert.That(layer.transform.IsChildOf(copy.transform), Is.True, name + " が立ち絵の外にあります（複製で描けません）。");
            }

            // 枠やバッジに比べて立ち絵が小さいプレハブもあるため、立ち絵が描画範囲いっぱいになるよう全体を拡大します
            // （画素の数を確保するためで、色の判定には影響しません）。
            Canvas.ForceUpdateCanvases();
            RectTransform root = (RectTransform)copy.transform;
            RectTransform baseRect = Layer(view, "baseLayer").rectTransform;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(frame, baseRect);
            float scale = 0.95f * Mathf.Min(frame.rect.width / bounds.size.x, frame.rect.height / bounds.size.y);

            root.localScale = Vector3.one * scale;
            root.anchoredPosition = -(Vector2)bounds.center * scale;

            Canvas.ForceUpdateCanvases();
            return view;
        }

        /// <summary>立ち絵の3層だけを描きます（枠・バッジ・リングは描く間だけ消します）。</summary>
        private Color32[] Render(BeastThumbnailView view)
        {
            HashSet<Graphic> layers = new HashSet<Graphic>
            {
                Layer(view, "baseLayer"),
                Layer(view, "primaryLayer"),
                Layer(view, "secondaryLayer"),
            };

            List<Graphic> hidden = new List<Graphic>();

            foreach (Graphic graphic in frame.GetComponentsInChildren<Graphic>(true))
            {
                if (!layers.Contains(graphic) && graphic.enabled)
                {
                    graphic.enabled = false;
                    hidden.Add(graphic);
                }
            }

            // 透明度を下げる CanvasGroup は実際の表示の一部なのでそのまま描きます。
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Color32[] pixels = Read(target);

            foreach (Graphic graphic in hidden)
            {
                graphic.enabled = true;
            }

            return pixels;
        }

        private IEnumerable<CoreBeastDefinition> Definitions()
        {
            HashSet<CoreBeastDefinition> seen = new HashSet<CoreBeastDefinition>();

            foreach (OwnedCoreBeast owned in roster.Owned)
            {
                if (seen.Add(owned.Definition))
                {
                    yield return owned.Definition;
                }
            }
        }

        private static Color32[] Read(RenderTexture texture)
        {
            Texture2D readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readback.Apply(false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static RawImage Layer(BeastThumbnailView view, string field)
        {
            RawImage layer = (RawImage)GetField(view, field);
            Assert.That(layer, Is.Not.Null, field + " が未設定です。");
            return layer;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return field.GetValue(target);
        }
    }
}
