using System.Collections;
using System.Collections.Generic;

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
    /// Phase 2「属性別インパクト」を、実際の Battle シーンで確かめます。
    ///
    /// 形と色は、表示中の<see cref="ImpactBurstGraphic"/>と同じ状態から
    /// <see cref="ImpactBurstGraphic.FillMesh"/>で頂点を読み直して確かめます。
    /// 実ラウンドは循環リングの上スライド成立と同じ入口から入れます。
    /// </summary>
    public sealed class BattleAttributeImpactPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const int WarmUpFrames = 5;
        private const float RoundSecondsLimit = 10f;

        /// <summary>エフェクトが覆ってはいけない HUD。</summary>
        private static readonly string[] HudNames =
        {
            "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton",
            "HistoryLane", "PlayerWheel",
        };

        /// <summary>エフェクト中も動いてはいけないもの（位置で確かめます）。</summary>
        private static readonly string[] StaticNames =
        {
            "SafeArea", "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton",
            "BattleRoot", "PlayerWheel", "HistoryLane", "FxFlash", "ResultView", "RematchButton",
            "FinalHomeButton", "FxImpact",
        };

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleFxPlayer fx;
        private ImpactBurstGraphic burst;
        private RectTransform effectRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            SaveTestSquad();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            controller = Object.FindAnyObjectByType<BattleScreenController>();
            fx = Object.FindAnyObjectByType<BattleFxPlayer>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True, "BattleScreenController が参照不足で無効化されました。");
            Assert.That(fx, Is.Not.Null);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "編成が読み込めていません。");

            burst = fx.ImpactBurst;
            Assert.That(burst, Is.Not.Null);
            effectRoot = (RectTransform)burst.transform.parent;

            Assert.That(burst.IsShowing, Is.False, "開始時は何も出していません。");
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 種類ごとの見え方 ----------------

        [UnityTest]
        public IEnumerator RedBlueAndGreenAttributeWinsShowDifferentImpacts()
        {
            Dictionary<BattleImpactKind, Record> seen = new Dictionary<BattleImpactKind, Record>();

            foreach (UnitAttribute colour in new[] { UnitAttribute.Red, UnitAttribute.Blue, UnitAttribute.Green })
            {
                Record record = new Record();

                yield return PlayDirect(RoundWinner.Player, RoundDecision.AttributeAdvantage, colour, record);

                BattleImpactKind expected =
                    colour == UnitAttribute.Red ? BattleImpactKind.Red :
                    colour == UnitAttribute.Blue ? BattleImpactKind.Blue :
                    BattleImpactKind.Green;

                Assert.That(record.Kinds, Is.EquivalentTo(new[] { expected }), colour.ToString());
                Assert.That(record.MaxVertices, Is.EqualTo(AttributeEffectProfile.For(expected).MaxVertices), colour.ToString());

                seen[expected] = record;
            }

            Assert.That(seen[BattleImpactKind.Red].MaxVertices, Is.Not.EqualTo(seen[BattleImpactKind.Blue].MaxVertices));
            Assert.That(seen[BattleImpactKind.Blue].MaxVertices, Is.Not.EqualTo(seen[BattleImpactKind.Green].MaxVertices));
            Assert.That(seen[BattleImpactKind.Red].MaxVertices, Is.Not.EqualTo(seen[BattleImpactKind.Green].MaxVertices));

            Assert.That(seen[BattleImpactKind.Red].MeanHue, Is.InRange(0f, 30f));
            Assert.That(seen[BattleImpactKind.Blue].MeanHue, Is.InRange(180f, 230f));
            Assert.That(seen[BattleImpactKind.Green].MeanHue, Is.InRange(70f, 150f));
        }

        [UnityTest]
        public IEnumerator APowerWinShowsNoAttributeColourAnywhere()
        {
            foreach (RoundWinner winner in new[] { RoundWinner.Player, RoundWinner.Cpu })
            {
                foreach (UnitAttribute colour in new[] { UnitAttribute.Red, UnitAttribute.Blue, UnitAttribute.Green })
                {
                    Record record = new Record();

                    yield return PlayDirect(
                        winner,
                        RoundDecision.PowerComparison,
                        colour,
                        record,
                        flashColour: new Color(0.9f, 0.1f, 0.1f, 1f));

                    string label = winner + " / " + colour;

                    Assert.That(record.Kinds, Is.EquivalentTo(new[] { BattleImpactKind.Power }), label);
                    Assert.That(record.AttributeColouredVertices, Is.EqualTo(0), label + " のエフェクトに属性色があります。");
                    Assert.That(record.AttributeColouredFlash, Is.False, label + " のフラッシュに属性色があります。");
                }
            }
        }

        [UnityTest]
        public IEnumerator ACoreWinShowsOnlyTheWhiteGoldCoreFlash()
        {
            Record record = new Record();

            yield return PlayDirect(RoundWinner.Cpu, RoundDecision.CoreComparison, null, record);

            Assert.That(record.Kinds, Is.EquivalentTo(new[] { BattleImpactKind.Core }));
            Assert.That(record.AttributeColouredVertices, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ADrawIsSymmetricAndTreatsBothSidesTheSame()
        {
            Record record = new Record { CheckSymmetry = true };

            yield return PlayDirect(RoundWinner.Draw, RoundDecision.Draw, null, record);

            Assert.That(record.Kinds, Is.EquivalentTo(new[] { BattleImpactKind.Draw }));
            Assert.That(record.AttributeColouredVertices, Is.EqualTo(0), "引き分けは勝者色を出しません。");
            Assert.That(record.SymmetryFailures, Is.EqualTo(0), "引き分けのエフェクトが左右非対称です。");

            // 衝突点は、ヒットストップ時の2体の立ち絵の中心から等距離です。
            Assert.That(record.ContactToPlayer, Is.GreaterThan(0f));
            Assert.That(record.ContactToPlayer, Is.EqualTo(record.ContactToCpu).Within(0.01f), "双方から等距離です。");
        }

        // ---------------- HUD を覆わない・動かさない ----------------

        /// <summary>縦持ちの実機（実ピクセルと安全余白pt、@3x）。</summary>
        private static readonly PhoneLayout[] Phones =
        {
            new PhoneLayout("iPhone 12 mini", 1080f, 2340f, 50f, 34f),
            new PhoneLayout("iPhone 16 Pro", 1206f, 2622f, 62f, 34f),
        };

        [UnityTest]
        public IEnumerator EveryImpactStaysInsideTheCombatAreaOnPortraitPhones([ValueSource(nameof(Phones))] PhoneLayout phone)
        {
            // バッチ実行の Game View は横長のため、縦持ちのレイアウトが成り立ちません。
            // ルート Canvas を実機の縦画面と同じ大きさ（Canvas単位）へ置き換えてから確かめます。
            yield return UsePortraitPhone(phone);

            Dictionary<string, Vector3> positions = CapturePositions();

            Assert.That(effectRoot.GetComponent<RectMask2D>(), Is.Not.Null);

            foreach (string name in HudNames)
            {
                Assert.That(
                    WorldRect(effectRoot).Overlaps(WorldRect((RectTransform)Find(name))),
                    Is.False,
                    "エフェクト領域が " + name + " に重なっています。");
            }

            BattleImpactKind[] kinds =
            {
                BattleImpactKind.Red, BattleImpactKind.Blue, BattleImpactKind.Green,
                BattleImpactKind.Power, BattleImpactKind.Core, BattleImpactKind.Draw,
            };

            foreach (BattleImpactKind kind in kinds)
            {
                Verdict(kind, out RoundWinner winner, out RoundDecision decision, out UnitAttribute? colour);

                Record record = new Record { OnFrame = () => AssertPositionsUnchanged(positions) };

                yield return PlayDirect(winner, decision, colour, record);

                Assert.That(record.Kinds, Is.EquivalentTo(new[] { kind }));
                Assert.That(record.OutsideRoot, Is.EqualTo(0), kind + " が戦闘表示領域の外へ出ています。" + record.FirstOutside);
                Assert.That(record.OverHud, Is.EqualTo(0), kind + " が HUD に重なっています。" + record.FirstOverHud);
                Assert.That(record.Clipped, Is.True, kind + " が切り抜きの対象になっていません。");
            }

            AssertPositionsUnchanged(positions);
        }

        // ---------------- 実ラウンド ----------------

        [UnityTest]
        public IEnumerator TenRealRoundsShowTheRuleDecidedImpactAndLeaveNothingBehind()
        {
            int burstCount = Object.FindObjectsByType<ImpactBurstGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int canvasCount = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int rootTransforms = effectRoot.GetComponentsInChildren<Transform>(true).Length;
            int rootComponents = effectRoot.GetComponentsInChildren<Component>(true).Length;
            int rootGraphics = effectRoot.GetComponentsInChildren<Graphic>(true).Length;

            List<string> observed = new List<string>();
            CanvasGroup banner = Find("Banner").GetComponent<CanvasGroup>();

            for (int round = 0; round < 10; round++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "round " + round);

                HashSet<BattleImpactKind> shown = new HashSet<BattleImpactKind>();

                controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

                float startedAt = Time.realtimeSinceStartup;

                while (controller.IsRoundInProgress)
                {
                    if (burst.IsShowing)
                    {
                        shown.Add(burst.Kind);

                        Assert.That(
                            banner.gameObject.activeInHierarchy && banner.alpha > 0f,
                            Is.False,
                            "結果バナーとエフェクトは同時に出ません。");
                    }

                    yield return null;

                    Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
                }

                RoundResult result = controller.Coordinator.LastResult;
                BattleImpactKind expected = BattleRoundPresentationPlan.ResolveImpactKind(
                    result.Winner, result.Decision, DecidingAttributeLookup.Of(result), true);

                string label = "round " + (round + 1) + " " + result.Winner + "/" + result.Decision;

                Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(expected), label);
                Assert.That(expected, Is.Not.EqualTo(BattleImpactKind.None), label + " は実際の組み合わせでは必ず決まります。");
                Assert.That(shown, Is.EquivalentTo(new[] { expected }), label);

                yield return null;

                Assert.That(burst.IsShowing, Is.False, label + " の後に残っています。");
                Assert.That(burst.LastVertexCount, Is.EqualTo(0), label + " の後に頂点が残っています。");

                observed.Add(expected.ToString());
            }

            Assert.That(Object.FindObjectsByType<ImpactBurstGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(burstCount));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(canvasCount));
            Assert.That(effectRoot.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(rootTransforms));
            Assert.That(effectRoot.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(rootComponents));
            Assert.That(effectRoot.GetComponentsInChildren<Graphic>(true).Length, Is.EqualTo(rootGraphics));

            TestContext.WriteLine("observed impacts: " + string.Join(", ", observed));
        }

        [UnityTest]
        public IEnumerator TenDirectImpactsCreateNoObjectsOrGraphicsAnywhereInTheScene()
        {
            int objects = CountScene<Transform>();
            int graphics = CountScene<Graphic>();
            int components = CountScene<Component>();

            BattleImpactKind[] kinds =
            {
                BattleImpactKind.Red, BattleImpactKind.Blue, BattleImpactKind.Green,
                BattleImpactKind.Power, BattleImpactKind.Core, BattleImpactKind.Draw,
            };

            for (int i = 0; i < 10; i++)
            {
                Verdict(kinds[i % kinds.Length], out RoundWinner winner, out RoundDecision decision, out UnitAttribute? colour);

                yield return PlayDirect(winner, decision, colour, new Record());

                yield return null;

                Assert.That(burst.IsShowing, Is.False, "impact " + i);
                Assert.That(burst.LastVertexCount, Is.EqualTo(0), "impact " + i);
            }

            Assert.That(CountScene<Transform>(), Is.EqualTo(objects), "GameObject が増えました。");
            Assert.That(CountScene<Graphic>(), Is.EqualTo(graphics), "Graphic が増えました。");
            Assert.That(CountScene<Component>(), Is.EqualTo(components), "Component が増えました。");
        }

        [UnityTest]
        public IEnumerator DisablingTheScreenMidImpactHidesItImmediately()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForImpact();

            controller.gameObject.SetActive(false);

            Assert.That(burst.IsShowing, Is.False, "OnDisable の直後に消えます。");
            Assert.That(fx.IsPlaying, Is.False);

            yield return null;

            Assert.That(burst.LastVertexCount, Is.EqualTo(0), "次のフレームには頂点も残りません。");

            controller.gameObject.SetActive(true);
            yield return null;

            Assert.That(burst.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingTheEffectRootMidImpactHidesItImmediately()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForImpact();

            effectRoot.gameObject.SetActive(false);

            Assert.That(burst.IsShowing, Is.False);
            Assert.That(burst.LastVertexCount, Is.EqualTo(0));

            yield return WaitRoundEnd();

            effectRoot.gameObject.SetActive(true);
            yield return null;

            Assert.That(burst.IsShowing, Is.False, "再表示で前の続きは出ません。");
            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(1), "ラウンドは正常に終わります。");
        }

        [UnityTest]
        public IEnumerator LeavingTheSceneMidImpactLeavesNothingRunning()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForImpact();

            Scene battle = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("BattleAttributeImpactTests_Empty");

            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(battle);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.That(burst == null, Is.True, "エフェクトが破棄されていません。");
            Assert.That(Object.FindAnyObjectByType<ImpactBurstGraphic>(), Is.Null);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FxOffShowsNoImpactAndTheRoundProgressesNormally()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                Assert.That(burst.IsShowing, Is.False, "FX OFF では出しません。");
                Assert.That(burst.LastVertexCount, Is.EqualTo(0));

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(BattleImpactKind.None));
            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(controller.Ring.Count, Is.EqualTo(BattleSquad.UnitCount - 1));
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.True);
        }

        // ---------------- 補助 ----------------

        /// <summary>1回の再生で観測したこと。</summary>
        private sealed class Record
        {
            internal readonly HashSet<BattleImpactKind> Kinds = new HashSet<BattleImpactKind>();
            internal int MaxVertices;
            internal float MeanHue;
            internal int AttributeColouredVertices;
            internal bool AttributeColouredFlash;
            internal int OutsideRoot;
            internal string FirstOutside;
            internal int OverHud;
            internal string FirstOverHud;
            internal bool Clipped;
            internal bool CheckSymmetry;
            internal int SymmetryFailures;
            internal float ContactToPlayer;
            internal float ContactToCpu;
            internal System.Action OnFrame;

            internal float HueX;
            internal float HueY;
        }

        private IEnumerator PlayDirect(
            RoundWinner winner,
            RoundDecision decision,
            UnitAttribute? colour,
            Record record,
            Color? flashColour = null)
        {
            Image flash = Find("FxFlash").GetComponent<Image>();

            fx.StartCoroutine(fx.PlayClashRoutine(winner, decision, colour, flashColour ?? Color.white));

            float startedAt = Time.realtimeSinceStartup;

            yield return null;

            while (fx.IsPlaying)
            {
                record.OnFrame?.Invoke();

                if (fx.Step == BattleFxPlayer.ClashStep.ImpactHold)
                {
                    // ヒットストップ中の立ち絵の中心から、衝突点までの距離。
                    record.ContactToPlayer = Vector3.Distance(fx.LastContactWorld, CentreOf(fx.PlayerShakeTarget));
                    record.ContactToCpu = Vector3.Distance(fx.LastContactWorld, CentreOf(fx.CpuShakeTarget));
                }

                if (fx.Step == BattleFxPlayer.ClashStep.Release)
                {
                    Color.RGBToHSV(flash.color, out float fh, out float fs, out _);

                    if (IsAttributeHue(fh * 360f, fs))
                    {
                        record.AttributeColouredFlash = true;
                    }
                }

                if (burst.IsShowing)
                {
                    Observe(record);
                }

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            record.OnFrame?.Invoke();

            float hue = Mathf.Atan2(record.HueY, record.HueX) * Mathf.Rad2Deg;
            record.MeanHue = hue < 0f ? hue + 360f : hue;

            Assert.That(record.Kinds.Count, Is.GreaterThan(0), winner + "/" + decision + "/" + colour + " でエフェクトが出ませんでした。");
            Assert.That(burst.IsShowing, Is.False, "再生の終わりに残っています。");

            fx.ResetVisuals();
        }

        private void Observe(Record record)
        {
            record.Kinds.Add(burst.Kind);
            record.Clipped |= burst.canvasRenderer.hasRectClipping;

            List<UIVertex> vertices = new List<UIVertex>();

            using (VertexHelper vh = new VertexHelper())
            {
                burst.FillMesh(vh);

                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    UIVertex v = new UIVertex();
                    vh.PopulateUIVertex(ref v, i);
                    vertices.Add(v);
                }
            }

            record.MaxVertices = Mathf.Max(record.MaxVertices, vertices.Count);

            Assert.That(vertices.Count, Is.LessThanOrEqualTo(AttributeEffectProfile.VertexBudget));

            Rect rootRect = WorldRect(effectRoot);
            List<Rect> hud = new List<Rect>();

            foreach (string name in HudNames)
            {
                hud.Add(WorldRect((RectTransform)Find(name)));
            }

            foreach (UIVertex v in vertices)
            {
                Color c = v.color;

                if (c.a > 0.01f)
                {
                    Color.RGBToHSV(c, out float h, out float s, out _);

                    record.HueX += Mathf.Cos(h * Mathf.PI * 2f) * s;
                    record.HueY += Mathf.Sin(h * Mathf.PI * 2f) * s;

                    if (IsAttributeHue(h * 360f, s))
                    {
                        record.AttributeColouredVertices++;
                    }
                }

                Vector3 world = burst.rectTransform.TransformPoint(v.position);

                if (!rootRect.Contains(world))
                {
                    if (record.OutsideRoot == 0)
                    {
                        record.FirstOutside =
                            "vertex " + world + " / area " + rootRect +
                            " / contact " + fx.LastContactWorld +
                            " / player " + CentreOf(fx.PlayerShakeTarget) +
                            " / cpu " + CentreOf(fx.CpuShakeTarget);
                    }

                    record.OutsideRoot++;
                }

                for (int i = 0; i < hud.Count; i++)
                {
                    if (hud[i].Contains(world))
                    {
                        if (record.OverHud == 0)
                        {
                            record.FirstOverHud = " vertex " + world + " / " + HudNames[i] + " " + hud[i];
                        }

                        record.OverHud++;
                    }
                }
            }

            if (record.CheckSymmetry)
            {
                foreach (UIVertex v in vertices)
                {
                    Vector2 offset = (Vector2)v.position - burst.Centre;

                    if (!HasVertexAt(vertices, burst.Centre + new Vector2(-offset.x, offset.y), v.color))
                    {
                        record.SymmetryFailures++;
                    }
                }

                // エフェクトの中心は、ヒットストップ時に求めた衝突点のままです（振動で動きません）。
                Vector3 centreWorld = burst.rectTransform.TransformPoint(burst.Centre);

                Assert.That(Vector3.Distance(centreWorld, fx.LastContactWorld), Is.LessThan(0.01f), "中心は衝突点です。");
            }
        }

        private static Vector3 CentreOf(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        private static bool HasVertexAt(List<UIVertex> vertices, Vector2 position, Color32 colour)
        {
            foreach (UIVertex w in vertices)
            {
                Color32 c = w.color;

                if (((Vector2)w.position - position).sqrMagnitude < 0.01f * 0.01f &&
                    c.r == colour.r && c.g == colour.g && c.b == colour.b && c.a == colour.a)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>赤・緑・青の属性色とみなす色相か。白〜金（38〜60度）と無彩色は含みません。</summary>
        private static bool IsAttributeHue(float hue, float saturation)
        {
            if (saturation < 0.15f)
            {
                return false;
            }

            return hue < 38f || hue > 60f;
        }

        private static void Verdict(
            BattleImpactKind kind, out RoundWinner winner, out RoundDecision decision, out UnitAttribute? colour)
        {
            winner = RoundWinner.Player;
            colour = null;

            switch (kind)
            {
                case BattleImpactKind.Red:
                    decision = RoundDecision.AttributeAdvantage;
                    colour = UnitAttribute.Red;
                    return;

                case BattleImpactKind.Blue:
                    decision = RoundDecision.AttributeAdvantage;
                    colour = UnitAttribute.Blue;
                    winner = RoundWinner.Cpu;
                    return;

                case BattleImpactKind.Green:
                    decision = RoundDecision.AttributeAdvantage;
                    colour = UnitAttribute.Green;
                    return;

                case BattleImpactKind.Power:
                    decision = RoundDecision.PowerComparison;
                    colour = UnitAttribute.Red;
                    winner = RoundWinner.Cpu;
                    return;

                case BattleImpactKind.Core:
                    decision = RoundDecision.CoreComparison;
                    return;

                default:
                    decision = RoundDecision.Draw;
                    winner = RoundWinner.Draw;
                    return;
            }
        }

        /// <summary>縦持ちの実機1台ぶんの画面。</summary>
        public readonly struct PhoneLayout
        {
            public PhoneLayout(string name, float widthPixels, float heightPixels, float topInsetPoints, float bottomInsetPoints)
            {
                Name = name;
                WidthPixels = widthPixels;
                HeightPixels = heightPixels;
                TopInsetPoints = topInsetPoints;
                BottomInsetPoints = bottomInsetPoints;
            }

            public string Name { get; }
            public float WidthPixels { get; }
            public float HeightPixels { get; }
            public float TopInsetPoints { get; }
            public float BottomInsetPoints { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        /// <summary>
        /// ルート Canvas を、実機で CanvasScaler が作るのと同じ Canvas 単位の大きさにします。
        /// 倍率は参照解像度と Match から計算し、SafeArea には実機の安全余白を当てます。
        /// </summary>
        private IEnumerator UsePortraitPhone(PhoneLayout phone)
        {
            Canvas canvas = Find("Canvas").GetComponent<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

            float logWidth = Mathf.Log(phone.WidthPixels / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(phone.HeightPixels / scaler.referenceResolution.y, 2f);
            float pixelsPerUnit = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));

            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(phone.WidthPixels / pixelsPerUnit, phone.HeightPixels / pixelsPerUnit);

            // SafeAreaController は画面が変わったときだけ動くため、ここで当てた余白は上書きされません。
            RectTransform safeArea = (RectTransform)Find("SafeArea");
            safeArea.anchorMin = new Vector2(0f, phone.BottomInsetPoints * 3f / phone.HeightPixels);
            safeArea.anchorMax = new Vector2(1f, 1f - phone.TopInsetPoints * 3f / phone.HeightPixels);

            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(WorldRect(safeArea).height, Is.GreaterThan(WorldRect(safeArea).width), phone + " が縦画面になっていません。");
            Assert.That(WorldRect((RectTransform)Find("CpuCombatant")).yMin, Is.GreaterThan(WorldRect((RectTransform)Find("PlayerCombatant")).yMax), "ENEMY 段が PLAYER 段の上にあります。");
        }

        private IEnumerator WaitForImpact()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (!burst.IsShowing)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "エフェクトの段まで進みません。");
            }
        }

        private IEnumerator WaitRoundEnd()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }
        }

        private static int CountScene<T>() where T : Component
        {
            int count = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private Dictionary<string, Vector3> CapturePositions()
        {
            Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();

            foreach (string name in StaticNames)
            {
                positions[name] = Find(name).position;
            }

            return positions;
        }

        private void AssertPositionsUnchanged(Dictionary<string, Vector3> positions)
        {
            foreach (KeyValuePair<string, Vector3> entry in positions)
            {
                Assert.That(Find(entry.Key).position, Is.EqualTo(entry.Value), entry.Key + " が動きました。");
            }
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
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

        private static void SaveTestSquad()
        {
#if UNITY_EDITOR
            CoreBeastRoster roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);

            Assert.That(roster, Is.Not.Null, RosterPath + " を読めません。");
            Assert.That(roster.Owned.Count, Is.GreaterThanOrEqualTo(SquadFormation.SlotCount));

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            SquadRepositoryProvider.Shared.Save(SetId, new SquadSnapshot(ids));
#else
            Assert.Fail("エディタ上でのみ実行します（テスト用ロスターをAssetDatabaseから読むため）。");
#endif
        }
    }
}
