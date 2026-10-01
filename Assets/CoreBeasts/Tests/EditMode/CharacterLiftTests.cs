using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// 長押しからドラッグ中にかけての持ち上げは、カード全体ではなく
    /// カード内のキャラクター画像だけを対象にします。
    ///
    /// 実物の BeastCard.prefab / DragGhost.prefab を読んで、
    /// 「何が動く対象として配線されているか」を確かめます。
    /// </summary>
    public sealed class CharacterLiftTests
    {
        private const string CardPrefabPath = "Assets/CoreBeasts/Prefabs/BeastCard.prefab";
        private const string GhostPrefabPath = "Assets/CoreBeasts/Prefabs/DragGhost.prefab";

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        /// <summary>CardLiftView.Update を直接呼び、補間を進めます。</summary>
        private static void PumpLift(CardLiftView lift)
        {
            lift.GetType()
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(lift, null);
        }

        private GameObject SpawnCard()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            Assert.That(asset, Is.Not.Null, CardPrefabPath + " が読めません。");

            GameObject card = Object.Instantiate(asset);
            spawned.Add(card);

            return card;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---------------- 持ち上げの対象 ----------------

        [Test]
        public void TheLiftTargetsTheCharacterAndNotTheWholeCard()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            BeastThumbnailView thumbnail =
                card.GetComponentInChildren<BeastThumbnailView>(true);

            RectTransform thumbRect = thumbnail.GetComponent<RectTransform>();

            // 持ち上げるのは、機械獣のピクセルだけを入れた専用の根です。
            // サムネイル自体には属性色のグロウもぶら下がるため、そこは動かしません。
            Assert.That(
                visual.IsChildOf(thumbRect),
                Is.True,
                "持ち上げる対象がキャラクターの中にありません。");

            Assert.That(
                visual,
                Is.Not.SameAs(thumbRect),
                "サムネイルごと動かすと、属性色のグロウまで一緒に浮きます。");

            Assert.That(
                visual.gameObject,
                Is.Not.SameAs(card),
                "カード本体のRectTransformを動かしてはいけません。");
        }

        [Test]
        public void AllThreeCharacterLayersMoveTogether()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            BeastThumbnailView thumbnail =
                card.GetComponentInChildren<BeastThumbnailView>(true);

            string[] layers = { "baseLayer", "primaryLayer", "secondaryLayer" };

            for (int i = 0; i < layers.Length; i++)
            {
                RawImage layer = (RawImage)GetField(thumbnail, layers[i]);

                Assert.That(layer, Is.Not.Null, layers[i] + " が未設定です。");

                Assert.That(
                    layer.transform.IsChildOf(visual),
                    Is.True,
                    layers[i] + " が持ち上げ対象の外にあります。3層は一体で動かします。");
            }
        }

        /// <summary>world corners を取り出します。実際に描かれる四隅で比べます。</summary>
        private static Vector3[] Corners(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return corners;
        }

        /// <summary>ホバーで動いてはいけない部分。属性色の面と背景を含みます。</summary>
        private static readonly string[] FixedBackgroundParts =
        {
            "Frame", "Background", "AttributeSurface",
            "NameLabel", "LevelLabel", "SquadBadge",
        };

        [Test]
        public void EveryBackgroundGraphicIsPixelIdenticalAcrossHover()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();

            Canvas.ForceUpdateCanvases();

            // ホバー前の world corners・色・位置を控えます。
            Vector3[][] corners = new Vector3[FixedBackgroundParts.Length][];
            Color[] colors = new Color[FixedBackgroundParts.Length];
            Vector3[] positions = new Vector3[FixedBackgroundParts.Length];
            Vector3[] scales = new Vector3[FixedBackgroundParts.Length];

            for (int i = 0; i < FixedBackgroundParts.Length; i++)
            {
                Transform node = FindDeep(card.transform, FixedBackgroundParts[i]);

                Assert.That(node, Is.Not.Null, FixedBackgroundParts[i] + " が見つかりません。");

                RectTransform rect = (RectTransform)node;

                corners[i] = Corners(rect);
                positions[i] = rect.position;
                scales[i] = rect.lossyScale;

                Graphic graphic = node.GetComponent<Graphic>();
                colors[i] = graphic != null ? graphic.color : Color.clear;
            }

            RectTransform liftRoot = (RectTransform)GetField(lift, "visualRoot");
            float liftedFrom = liftRoot.localPosition.y;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);
            Canvas.ForceUpdateCanvases();

            // キャラクターが実際に持ち上がっていること。
            // ここが動いていなければ、以下の「不変」は何も証明しません。
            Assert.That(
                liftRoot.localPosition.y,
                Is.GreaterThan(liftedFrom),
                "キャラクターが持ち上がっていません。検査が成立しません。");

            for (int i = 0; i < FixedBackgroundParts.Length; i++)
            {
                string name = FixedBackgroundParts[i];

                RectTransform rect =
                    (RectTransform)FindDeep(card.transform, name);

                Vector3[] now = Corners(rect);

                for (int c = 0; c < 4; c++)
                {
                    Assert.That(
                        now[c],
                        Is.EqualTo(corners[i][c]),
                        name + " の world corner[" + c + "] がホバーで動いています。");
                }

                Assert.That(rect.position, Is.EqualTo(positions[i]), name + " の位置");
                Assert.That(rect.lossyScale, Is.EqualTo(scales[i]), name + " の大きさ");

                Graphic graphic = rect.GetComponent<Graphic>();

                if (graphic != null)
                {
                    Assert.That(graphic.color, Is.EqualTo(colors[i]), name + " の色");
                }
            }

            // 戻したあとも完全に一致します。
            lift.ResetImmediate();
            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < FixedBackgroundParts.Length; i++)
            {
                Vector3[] now =
                    Corners((RectTransform)FindDeep(card.transform, FixedBackgroundParts[i]));

                for (int c = 0; c < 4; c++)
                {
                    Assert.That(
                        now[c],
                        Is.EqualTo(corners[i][c]),
                        FixedBackgroundParts[i] + " が PointerExit 後に戻っていません。");
                }
            }
        }

        [Test]
        public void TheHoverLiftRootHoldsOnlyThePortraitPixels()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Assert.That(
                visual.name,
                Is.EqualTo("HoverLiftRoot"),
                "持ち上げ専用の根が使われていません。");

            // 属性色の面・背景・カード表面が、構造としてぶら下がっていないこと。
            string[] forbidden =
            {
                "AttributeSurface", "Background", "Frame",
                "DragGlow", "DragShadow", "NameLabel", "LevelLabel", "SquadBadge",
            };

            for (int i = 0; i < forbidden.Length; i++)
            {
                Assert.That(
                    FindDeep(visual, forbidden[i]),
                    Is.Null,
                    forbidden[i] + " が持ち上げ対象の中にあります。");
            }

            // 中身は機械獣の3層だけです。
            Graphic[] inside = visual.GetComponentsInChildren<Graphic>(true);

            Assert.That(inside.Length, Is.EqualTo(3), "Portrait の3層だけを入れます。");

            for (int i = 0; i < inside.Length; i++)
            {
                Assert.That(
                    inside[i],
                    Is.InstanceOf<RawImage>(),
                    inside[i].name + " は Portrait の層ではありません。");
            }
        }

        [Test]
        public void ThePortraitTexturesAreTransparentAroundTheCreature()
        {
            GameObject card = SpawnCard();

            BeastThumbnailView thumbnail =
                card.GetComponentInChildren<BeastThumbnailView>(true);

            string[] layers = { "baseLayer", "primaryLayer", "secondaryLayer" };

            for (int i = 0; i < layers.Length; i++)
            {
                RawImage layer = (RawImage)GetField(thumbnail, layers[i]);

                Assert.That(layer, Is.Not.Null, layers[i] + " が未設定です。");
                Assert.That(layer.texture, Is.Not.Null, layers[i] + " のテクスチャがありません。");

                string path = AssetDatabase.GetAssetPath(layer.texture);

                Assert.That(
                    string.IsNullOrEmpty(path),
                    Is.False,
                    layers[i] + " が Asset のテクスチャを指していません（RenderTexture かもしれません）: "
                        + layer.texture.name);

                TextureImporter importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;

                Assert.That(
                    importer,
                    Is.Not.Null,
                    layers[i] + " のインポート設定が読めません: " + path);

                // 元画像がアルファを持っていること。
                // ここが false だと、機械獣の周囲が不透明になり、
                // 色を掛けた瞬間にベタ塗りの矩形板になります。
                Assert.That(
                    importer.DoesSourceTextureHaveAlpha(),
                    Is.True,
                    layers[i] + " の元画像にアルファがありません: " + path);

                // アルファを入力画像から取ること（生成や不透明化をしない）。
                Assert.That(
                    importer.alphaSource,
                    Is.EqualTo(TextureImporterAlphaSource.FromInput),
                    layers[i] + " がアルファを入力から取っていません: " + path);
            }
        }

        [Test]
        public void ThePortraitLayersShareTheSameSizeAsTheLiftRoot()
        {
            // 3層が同じ矩形でなければ、持ち上げたときに層がずれて
            // 縁が板のように見えます。
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            BeastThumbnailView thumbnail =
                card.GetComponentInChildren<BeastThumbnailView>(true);

            string[] layers = { "baseLayer", "primaryLayer", "secondaryLayer" };

            Canvas.ForceUpdateCanvases();

            Vector3[] expected = Corners(visual);

            for (int i = 0; i < layers.Length; i++)
            {
                RawImage layer = (RawImage)GetField(thumbnail, layers[i]);

                Vector3[] now = Corners(layer.rectTransform);

                for (int c = 0; c < 4; c++)
                {
                    Assert.That(
                        now[c],
                        Is.EqualTo(expected[c]),
                        layers[i] + " が持ち上げ対象と同じ矩形ではありません。");
                }
            }
        }

        [Test]
        public void HoveringNeverLiftsTheAttributeColouredBackground()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();

            // ホバー前の、動かしてはいけない部分の姿を控えます。
            string[] chrome = { "Frame", "Background", "AttributeSurface" };

            Vector3[] positions = new Vector3[chrome.Length];
            Vector3[] scales = new Vector3[chrome.Length];

            for (int i = 0; i < chrome.Length; i++)
            {
                Transform t = FindDeep(card.transform, chrome[i]);

                Assert.That(t, Is.Not.Null, chrome[i] + " が見つかりません。");

                positions[i] = t.localPosition;
                scales[i] = t.localScale;
            }

            RectTransform liftRoot = (RectTransform)GetField(lift, "visualRoot");
            float before = liftRoot.localPosition.y;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                liftRoot.localPosition.y,
                Is.GreaterThan(before),
                "キャラクターが持ち上がっていません。検査が成立しません。");

            // 属性色の面も枠も背景も、位置と大きさが変わらないこと。
            for (int i = 0; i < chrome.Length; i++)
            {
                Transform t = FindDeep(card.transform, chrome[i]);

                Assert.That(
                    t.localPosition,
                    Is.EqualTo(positions[i]),
                    chrome[i] + " がホバーで動いています。");

                Assert.That(
                    t.localScale,
                    Is.EqualTo(scales[i]),
                    chrome[i] + " がホバーで拡大しています。");
            }

            // 属性色の矩形グロウは、ホバーでは出しません。
            Assert.That(
                lift.IsAttributeGlowVisible,
                Is.False,
                "ホバーで属性色の矩形が浮き上がっています。");
        }

        [Test]
        public void HoveringMovesAndScalesOnlyTheCharacter()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Vector3 restPosition = visual.localPosition;
            Vector3 restScale = visual.localScale;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(
                visual.localPosition.y,
                Is.GreaterThan(restPosition.y),
                "キャラクターが上がっていません。");

            // 拡大は控えめに保ちます。
            float ratio = visual.localScale.x / restScale.x;

            Assert.That(
                ratio,
                Is.InRange(1.0f, 1.12f),
                "キャラクターの拡大が大きすぎます: " + ratio);
        }

        [Test]
        public void LeavingTheCardRestoresTheCharacterExactly()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Vector3 restPosition = visual.localPosition;
            Vector3 restScale = visual.localScale;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);
            lift.ResetImmediate();

            Assert.That(visual.localPosition, Is.EqualTo(restPosition), "位置が戻りません。");
            Assert.That(visual.localScale, Is.EqualTo(restScale), "大きさが戻りません。");
            Assert.That(lift.IsAttributeGlowVisible, Is.False);
        }

        [Test]
        public void HoveringTheSameCardRepeatedlyNeverAccumulates()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Vector3 restPosition = visual.localPosition;
            Vector3 restScale = visual.localScale;

            Vector3 liftedPosition = Vector3.zero;
            Vector3 liftedScale = Vector3.zero;

            for (int i = 0; i < 5; i++)
            {
                lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

                if (i == 0)
                {
                    liftedPosition = visual.localPosition;
                    liftedScale = visual.localScale;
                }
                else
                {
                    Assert.That(
                        visual.localPosition,
                        Is.EqualTo(liftedPosition),
                        (i + 1) + "回目のホバーで位置が積み上がっています。");

                    Assert.That(
                        visual.localScale,
                        Is.EqualTo(liftedScale),
                        (i + 1) + "回目のホバーで大きさが積み上がっています。");
                }

                lift.ResetImmediate();

                Assert.That(visual.localPosition, Is.EqualTo(restPosition));
                Assert.That(visual.localScale, Is.EqualTo(restScale));
            }
        }

        [Test]
        public void TheTapAreaNeverMovesWithTheHover()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();

            RectTransform root = (RectTransform)card.transform;

            Vector3 before = root.localPosition;
            Vector2 sizeBefore = root.sizeDelta;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            Assert.That(root.localPosition, Is.EqualTo(before), "タップ領域が動いています。");
            Assert.That(root.sizeDelta, Is.EqualTo(sizeBefore), "タップ領域が変わっています。");
        }

        [Test]
        public void TheLabelsAndBadgeNeverMoveWithTheHover()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();

            string[] fixedParts = { "NameLabel", "LevelLabel", "SquadBadge" };

            Vector3[] positions = new Vector3[fixedParts.Length];

            for (int i = 0; i < fixedParts.Length; i++)
            {
                positions[i] = FindDeep(card.transform, fixedParts[i]).localPosition;
            }

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            for (int i = 0; i < fixedParts.Length; i++)
            {
                Assert.That(
                    FindDeep(card.transform, fixedParts[i]).localPosition,
                    Is.EqualTo(positions[i]),
                    fixedParts[i] + " がホバーで動いています。");
            }
        }

        [Test]
        public void TheCardChromeStaysOutsideTheLiftedPart()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            string[] stayPut =
            {
                "Frame", "Background", "AttributeSurface", "NameLabel",
                "LevelLabel", "SquadBadge",
            };

            for (int i = 0; i < stayPut.Length; i++)
            {
                Transform part = FindDeep(card.transform, stayPut[i]);

                Assert.That(part, Is.Not.Null, stayPut[i] + " が見つかりません。");

                Assert.That(
                    part.IsChildOf(visual),
                    Is.False,
                    stayPut[i] + " まで一緒に浮いています。カード本体は元位置に残します。");
            }
        }

        [Test]
        public void TheSelectionFrameStaysWithTheCard()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            BeastCardView view = card.GetComponent<BeastCardView>();
            Image selection = (Image)GetField(view, "selectionFrame");

            Assert.That(selection, Is.Not.Null);

            Assert.That(
                selection.transform.IsChildOf(visual),
                Is.False,
                "選択枠はカード位置に残します。");
        }

        [Test]
        public void TheTapAreaRemainsTheWholeCard()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Image raycastTarget = card.GetComponent<Image>();

            Assert.That(
                raycastTarget,
                Is.Not.Null,
                "カード全体で入力を受けるImageが必要です。");

            Assert.That(raycastTarget.raycastTarget, Is.True);

            Assert.That(
                raycastTarget.transform,
                Is.Not.SameAs(visual),
                "タップ判定はカード全体のまま維持します。");
        }

        [Test]
        public void TheGlowAndShadowStayWithTheCard()
        {
            // 以前は「影と発光はキャラクターに追従する」契約でした。
            // DragGlow は属性色のベタ矩形なので、追従させると
            // 色の付いた板が一緒に浮き上がってしまいます。
            // 現在の正式仕様は「カード側に固定」です。
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            GameObject shadow = (GameObject)GetField(lift, "dragShadow");
            GameObject glow = (GameObject)GetField(lift, "dragGlow");

            Assert.That(shadow, Is.Not.Null);
            Assert.That(glow, Is.Not.Null);

            // 持ち上げる根の子孫ではないこと。
            Assert.That(
                shadow.transform.IsChildOf(visual),
                Is.False,
                "影が持ち上げ対象の中にあります。");

            Assert.That(
                glow.transform.IsChildOf(visual),
                Is.False,
                "属性色の発光が持ち上げ対象の中にあります。");

            // ホバー前後で world corners・位置・大きさ・色が完全一致すること。
            GameObject[] parts = { shadow, glow };

            Vector3[][] corners = new Vector3[parts.Length][];
            Vector3[] positions = new Vector3[parts.Length];
            Vector3[] scales = new Vector3[parts.Length];
            Color[] colors = new Color[parts.Length];

            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < parts.Length; i++)
            {
                RectTransform rect = (RectTransform)parts[i].transform;

                corners[i] = Corners(rect);
                positions[i] = rect.position;
                scales[i] = rect.lossyScale;

                Graphic graphic = parts[i].GetComponent<Graphic>();
                colors[i] = graphic != null ? graphic.color : Color.clear;
            }

            Vector3 beforeLift = visual.localPosition;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);
            Canvas.ForceUpdateCanvases();

            // キャラクターは実際に動いていること（動かないまま一致しても意味がありません）。
            Assert.That(
                visual.localPosition.y,
                Is.GreaterThan(beforeLift.y),
                "キャラクターが持ち上がっていません。検査が成立しません。");

            for (int i = 0; i < parts.Length; i++)
            {
                RectTransform rect = (RectTransform)parts[i].transform;

                Vector3[] now = Corners(rect);

                for (int c = 0; c < 4; c++)
                {
                    Assert.That(
                        now[c],
                        Is.EqualTo(corners[i][c]),
                        parts[i].name + " の world corner[" + c + "] が動いています。");
                }

                Assert.That(rect.position, Is.EqualTo(positions[i]), parts[i].name + " の位置");
                Assert.That(rect.lossyScale, Is.EqualTo(scales[i]), parts[i].name + " の大きさ");

                Graphic graphic = parts[i].GetComponent<Graphic>();

                if (graphic != null)
                {
                    Assert.That(graphic.color, Is.EqualTo(colors[i]), parts[i].name + " の色");
                }
            }
        }

        [Test]
        public void TheLiftAmountStaysWithinTheAgreedRange()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();

            float scale = (float)GetField(lift, "liftScale");

            Assert.That(
                scale,
                Is.InRange(1.08f, 1.14f),
                "持ち上げの拡大率が目安の範囲外です。");
        }

        // ---------------- 実際に動かす ----------------

        [Test]
        public void WhileLiftedOnlyTheCharacterHasMoved()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");
            RectTransform root = card.GetComponent<RectTransform>();

            Transform name = FindDeep(card.transform, "NameLabel");
            Transform badge = FindDeep(card.transform, "SquadBadge");
            Transform surface = FindDeep(card.transform, "AttributeSurface");
            Transform level = FindDeep(card.transform, "LevelLabel");

            Vector3 rootBefore = root.localPosition;
            Vector3 visualBefore = visual.localPosition;
            Vector3 nameBefore = name.localPosition;
            Vector3 badgeBefore = badge.localPosition;
            Vector3 surfaceBefore = surface.localPosition;
            Vector3 levelBefore = level.localPosition;

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            for (int i = 0; i < 30; i++)
            {
                PumpLift(lift);
            }

            Assert.That(
                visual.localPosition.y,
                Is.GreaterThan(visualBefore.y),
                "キャラクターが持ち上がっていません。");

            Assert.That(
                visual.localScale.x,
                Is.GreaterThan(1f),
                "キャラクターが拡大していません。");

            Assert.That(
                root.localPosition,
                Is.EqualTo(rootBefore),
                "カード本体のRectTransformが動いています。");

            Assert.That(name.localPosition, Is.EqualTo(nameBefore), "名前が動いています。");
            Assert.That(level.localPosition, Is.EqualTo(levelBefore), "レベルが動いています。");
            Assert.That(
                surface.localPosition,
                Is.EqualTo(surfaceBefore),
                "属性フレームと背景が動いています。");
            Assert.That(badge.localPosition, Is.EqualTo(badgeBefore), "編成済みチェックが動いています。");
        }

        [Test]
        public void CancellingTheLiftRestoresTheCharacterExactly()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Vector3 position = visual.localPosition;
            Vector3 scale = visual.localScale;

            GameObject shadow = (GameObject)GetField(lift, "dragShadow");
            GameObject glow = (GameObject)GetField(lift, "dragGlow");

            lift.SetStateImmediate(CardLiftView.LiftState.DragReady);

            for (int i = 0; i < 30; i++)
            {
                PumpLift(lift);
            }

            lift.SetState(CardLiftView.LiftState.Normal);

            for (int i = 0; i < 30; i++)
            {
                PumpLift(lift);
            }

            Assert.That(visual.localPosition, Is.EqualTo(position), "位置が戻っていません。");
            Assert.That(visual.localScale, Is.EqualTo(scale), "拡大が戻っていません。");
            Assert.That(shadow.activeSelf, Is.False, "影が残っています。");
            Assert.That(glow.activeSelf, Is.False, "発光が残っています。");
        }

        [Test]
        public void DisablingTheCardRestoresTheCharacterImmediately()
        {
            GameObject card = SpawnCard();

            CardLiftView lift = card.GetComponent<CardLiftView>();
            RectTransform visual = (RectTransform)GetField(lift, "visualRoot");

            Vector3 position = visual.localPosition;
            Vector3 scale = visual.localScale;

            lift.SetState(CardLiftView.LiftState.Dragging);

            for (int i = 0; i < 30; i++)
            {
                PumpLift(lift);
            }

            lift.ResetImmediate();

            Assert.That(visual.localPosition, Is.EqualTo(position));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            Assert.That(lift.IsLifted, Is.False);
        }

        // ---------------- ドラッグゴースト ----------------

        [Test]
        public void TheDragGhostShowsTheCharacterOnly()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath);

            Assert.That(asset, Is.Not.Null, GhostPrefabPath + " が読めません。");

            GameObject ghost = Object.Instantiate(asset);
            spawned.Add(ghost);

            Assert.That(
                ghost.GetComponentsInChildren<TMP_Text>(true),
                Is.Empty,
                "ゴーストに名前・レベル・属性記号は載せません。");

            Image[] plates = ghost.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < plates.Length; i++)
            {
                Assert.That(
                    plates[i].enabled,
                    Is.False,
                    plates[i].name + " : ゴーストに背景カードや属性チップは載せません。");
            }

            BeastThumbnailView thumbnail =
                ghost.GetComponentInChildren<BeastThumbnailView>(true);

            Assert.That(
                thumbnail,
                Is.Not.Null,
                "ゴーストはキャラクター画像そのものを出します。");

            string[] layers = { "baseLayer", "primaryLayer", "secondaryLayer" };

            for (int i = 0; i < layers.Length; i++)
            {
                Assert.That(
                    (RawImage)GetField(thumbnail, layers[i]),
                    Is.Not.Null,
                    layers[i] + " が未設定です。属性着色は維持します。");
            }
        }

        [Test]
        public void TheDragGhostStaysReadableAndOffsetFromTheFinger()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath);
            GameObject ghost = Object.Instantiate(asset);
            spawned.Add(ghost);

            CanvasGroup group = ghost.GetComponent<CanvasGroup>();

            Assert.That(group, Is.Not.Null);

            Assert.That(
                group.alpha,
                Is.GreaterThanOrEqualTo(0.85f),
                "半透明にしすぎると、移動対象の属性と個体が読めません。");

            Assert.That(
                group.blocksRaycasts,
                Is.False,
                "ゴーストがドロップ先の判定を奪ってはいけません。");
        }
    }
}
