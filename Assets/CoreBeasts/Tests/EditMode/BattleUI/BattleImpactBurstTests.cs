using System;
using System.Collections;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// Phase 2「属性別インパクト」の表示。
    ///
    /// <see cref="ImpactBurstGraphic"/>が作る頂点を直接読み、種類ごとの形・色・頂点数・広がりを確かめます。
    /// <see cref="BattleFxPlayer"/>経由では、フレーム時間を 1/60 秒に固定して、
    /// エフェクトがヒットストップ明けにだけ出ること、Phase 1 の動きを1フレームも変えないこと、
    /// 中断・FX OFF で即座に消えることを確かめます。
    /// </summary>
    public sealed class BattleImpactBurstTests
    {
        private const float Frame = 1f / 60f;

        private static readonly BattleImpactKind[] VisibleKinds =
        {
            BattleImpactKind.Red,
            BattleImpactKind.Blue,
            BattleImpactKind.Green,
            BattleImpactKind.Power,
            BattleImpactKind.Core,
            BattleImpactKind.Draw,
        };

        private TestBattleViews views;

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
        }

        [TearDown]
        public void TearDown()
        {
            views.Cleanup();
        }

        // ---------------- Graphic 単体 ----------------

        [Test]
        public void EveryKindBuildsExactlyItsVertexBudgetInsideItsRadius()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            foreach (BattleImpactKind kind in VisibleKinds)
            {
                AttributeEffectProfile profile = AttributeEffectProfile.For(kind);

                graphic.Begin(kind, graphic.rectTransform.TransformPoint(new Vector3(30f, -20f, 0f)));

                for (float p = 0f; p < 1f; p += 0.05f)
                {
                    graphic.SetProgress(p);

                    List<UIVertex> vertices = Build(graphic);
                    string label = kind + " @" + p.ToString("0.00");

                    Assert.That(vertices.Count, Is.EqualTo(profile.MaxVertices), label);
                    Assert.That(vertices.Count, Is.LessThanOrEqualTo(AttributeEffectProfile.VertexBudget), label);
                    Assert.That(graphic.LastMaxDistance, Is.LessThanOrEqualTo(profile.MaxRadius + 0.01f), label);

                    foreach (UIVertex v in vertices)
                    {
                        Vector2 offset = (Vector2)v.position - graphic.Centre;

                        Assert.That(offset.magnitude, Is.LessThanOrEqualTo(profile.MaxRadius + 0.01f), label);
                    }
                }
            }
        }

        [Test]
        public void TheThreeAttributeImpactsHaveDifferentShapesAndColours()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            Dictionary<BattleImpactKind, float> hues = new Dictionary<BattleImpactKind, float>();
            HashSet<int> counts = new HashSet<int>();

            foreach (BattleImpactKind kind in new[] { BattleImpactKind.Red, BattleImpactKind.Blue, BattleImpactKind.Green })
            {
                graphic.Begin(kind, graphic.rectTransform.position);
                graphic.SetProgress(0.3f);

                List<UIVertex> vertices = Build(graphic);

                counts.Add(vertices.Count);
                hues[kind] = MeanHue(vertices);
            }

            Assert.That(counts.Count, Is.EqualTo(3), "RED / BLUE / GREEN は頂点構成が異なります。");
            Assert.That(hues[BattleImpactKind.Red], Is.InRange(0f, 30f));
            Assert.That(hues[BattleImpactKind.Blue], Is.InRange(180f, 230f));
            Assert.That(hues[BattleImpactKind.Green], Is.InRange(70f, 150f));
        }

        [Test]
        public void EveryShownVertexUsesOnlyItsOwnColourFamily()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            foreach (BattleImpactKind kind in VisibleKinds)
            {
                graphic.Begin(kind, graphic.rectTransform.position);

                for (float p = 0f; p < 1f; p += 0.1f)
                {
                    graphic.SetProgress(p);

                    foreach (UIVertex v in Build(graphic))
                    {
                        Color c = v.color;

                        if (c.a <= 0.01f)
                        {
                            continue;
                        }

                        Color.RGBToHSV(c, out float h, out float s, out float value);
                        float hue = h * 360f;
                        string label = kind + " @" + p.ToString("0.0") + " " + c;

                        switch (kind)
                        {
                            case BattleImpactKind.Red:
                                Assert.That(hue <= 30f || s < 0.15f, Is.True, label);
                                break;

                            case BattleImpactKind.Blue:
                                Assert.That(hue, Is.InRange(180f, 230f), label);
                                break;

                            case BattleImpactKind.Green:
                                Assert.That(hue, Is.InRange(70f, 150f), label);
                                break;

                            case BattleImpactKind.Power:
                            case BattleImpactKind.Core:
                                Assert.That(s < 0.15f || (hue >= 38f && hue <= 60f), Is.True, label + " は白〜金だけです。");
                                break;

                            case BattleImpactKind.Draw:
                                Assert.That(s, Is.LessThan(0.1f), label + " は勝者色を持ちません。");
                                break;
                        }
                    }
                }
            }
        }

        [Test]
        public void TheDrawImpactIsMirrorSymmetricOnBothAxes()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            graphic.Begin(BattleImpactKind.Draw, graphic.rectTransform.TransformPoint(new Vector3(-12f, 40f, 0f)));

            foreach (float p in new[] { 0.05f, 0.3f, 0.6f, 0.9f })
            {
                graphic.SetProgress(p);

                List<UIVertex> vertices = Build(graphic);

                AssertMirrored(vertices, graphic.Centre, new Vector2(-1f, 1f), "左右 @" + p);
                AssertMirrored(vertices, graphic.Centre, new Vector2(1f, -1f), "上下 @" + p);
            }
        }

        [Test]
        public void ThePowerImpactCompressesInwardsWhileBlueSpreadsOutwards()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            graphic.Begin(BattleImpactKind.Power, graphic.rectTransform.position);
            graphic.SetProgress(0.05f);
            Build(graphic);
            float powerEarly = graphic.LastMaxDistance;
            graphic.SetProgress(0.7f);
            Build(graphic);
            float powerLate = graphic.LastMaxDistance;

            graphic.Begin(BattleImpactKind.Blue, graphic.rectTransform.position);
            graphic.SetProgress(0.05f);
            Build(graphic);
            float blueEarly = graphic.LastMaxDistance;
            graphic.SetProgress(0.7f);
            Build(graphic);
            float blueLate = graphic.LastMaxDistance;

            Assert.That(powerLate, Is.LessThan(powerEarly), "POWER は外から内へ押し潰れます。");
            Assert.That(blueLate, Is.GreaterThan(blueEarly), "BLUE は内から外へ広がります。");
        }

        [Test]
        public void BeginPlacesTheBurstAtTheContactPointInLocalSpace()
        {
            GameObject parent = views.CreateObject("ScaledParent");
            parent.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            parent.transform.position = new Vector3(100f, -40f, 0f);

            ImpactBurstGraphic graphic = CreateGraphic(parent.transform);
            graphic.rectTransform.anchoredPosition = new Vector2(17f, 9f);

            Vector3 world = graphic.rectTransform.TransformPoint(new Vector3(-44f, 61f, 0f));

            graphic.Begin(BattleImpactKind.Core, world);

            Assert.That(graphic.Centre.x, Is.EqualTo(-44f).Within(0.01f));
            Assert.That(graphic.Centre.y, Is.EqualTo(61f).Within(0.01f));
        }

        [Test]
        public void HidingAndFinishingLeaveNothingDrawn()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            graphic.Begin(BattleImpactKind.Red, graphic.rectTransform.position);
            graphic.SetProgress(0.4f);
            Assert.That(Build(graphic).Count, Is.GreaterThan(0));

            graphic.SetProgress(1f);
            Assert.That(graphic.IsShowing, Is.False, "終わりまで進めたら消えます。");
            Assert.That(Build(graphic).Count, Is.EqualTo(0));

            graphic.Begin(BattleImpactKind.Green, graphic.rectTransform.position);
            graphic.Hide();
            Assert.That(graphic.Kind, Is.EqualTo(BattleImpactKind.None));
            Assert.That(Build(graphic).Count, Is.EqualTo(0));

            graphic.Hide();
            graphic.SetProgress(0.5f);
            Assert.That(graphic.IsShowing, Is.False, "消えた後の進み具合では出ません。");

            graphic.Begin(BattleImpactKind.None, graphic.rectTransform.position);
            Assert.That(graphic.IsShowing, Is.False);

            graphic.Begin((BattleImpactKind)77, graphic.rectTransform.position);
            Assert.That(graphic.IsShowing, Is.False, "未定義の種類は何も出しません。");
            Assert.That(Build(graphic).Count, Is.EqualTo(0));
        }

        [Test]
        public void DisablingTheGraphicClearsItImmediately()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            graphic.Begin(BattleImpactKind.Blue, graphic.rectTransform.position);
            graphic.SetProgress(0.2f);
            Build(graphic);

            graphic.gameObject.SetActive(false);

            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(graphic.LastVertexCount, Is.EqualTo(0));

            graphic.gameObject.SetActive(true);

            Assert.That(graphic.IsShowing, Is.False, "再表示で前の続きは出ません。");
            Assert.That(Build(graphic).Count, Is.EqualTo(0));
        }

        [Test]
        public void TheGraphicNeverTakesInput()
        {
            ImpactBurstGraphic graphic = CreateGraphic();

            Assert.That(graphic.raycastTarget, Is.False);
        }

        // ---------------- BattleFxPlayer 経由 ----------------

        [Test]
        public void TheImpactAppearsOnlyAfterTheHitStopAndLastsItsPlannedTime()
        {
            foreach (BattleImpactKind kind in VisibleKinds)
            {
                BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

                RoundWinner winner = kind == BattleImpactKind.Draw ? RoundWinner.Draw : RoundWinner.Player;
                Verdict(kind, out RoundDecision decision, out UnitAttribute? attribute);

                int shownFrames = 0;
                bool shownBeforeRelease = false;
                bool shownOnFirstReleaseFrame = false;
                bool sawRelease = false;

                PlayToEnd(fx.PlayClashRoutine(winner, decision, attribute, Color.white), () =>
                {
                    bool showing = parts.ImpactBurst.IsShowing;

                    if (fx.Step == BattleFxPlayer.ClashStep.Release && !sawRelease)
                    {
                        sawRelease = true;
                        shownOnFirstReleaseFrame = showing;
                    }

                    if (showing)
                    {
                        Assert.That(parts.ImpactBurst.Kind, Is.EqualTo(kind));
                        Assert.That(fx.Step, Is.EqualTo(BattleFxPlayer.ClashStep.Release), kind + " は着地までに終わります。");
                        shownFrames++;
                    }

                    if (showing && !sawRelease)
                    {
                        shownBeforeRelease = true;
                    }
                });

                float expected = AttributeEffectProfile.For(kind).Duration;

                Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(kind));
                Assert.That(shownBeforeRelease, Is.False, kind + " はヒットストップより前に出ません。");
                Assert.That(shownOnFirstReleaseFrame, Is.True, kind + " はフラッシュと同時に出ます。");
                Assert.That(shownFrames * Frame, Is.EqualTo(expected).Within(Frame * 1.5f), kind.ToString());
                Assert.That(parts.ImpactBurst.IsShowing, Is.False, kind + " は終了後に残りません。");
            }
        }

        [Test]
        public void TheImpactNeverChangesThePhase1Motion()
        {
            foreach (RoundWinner winner in new[] { RoundWinner.Player, RoundWinner.Cpu, RoundWinner.Draw })
            {
                BattleFxPlayer plain = CreateFx(out TestBattleViews.FxParts plainParts);
                List<string> expected = new List<string>();

                PlayToEnd(plain.PlayClashRoutine(winner, Color.white), () => expected.Add(Pose(plain, plainParts)));

                foreach (BattleImpactKind kind in VisibleKinds)
                {
                    if ((kind == BattleImpactKind.Draw) != (winner == RoundWinner.Draw))
                    {
                        continue;
                    }

                    Verdict(kind, out RoundDecision decision, out UnitAttribute? attribute);

                    BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);
                    List<string> actual = new List<string>();

                    PlayToEnd(fx.PlayClashRoutine(winner, decision, attribute, Color.white), () => actual.Add(Pose(fx, parts)));

                    Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(kind));
                    Assert.That(actual, Is.EqualTo(expected), winner + " / " + kind + " で Phase 1 の動きが変わりました。");
                }
            }
        }

        [Test]
        public void TheContactPointIsTheMidpointOfTheTwoPortraits()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            parts.PlayerPortrait.sizeDelta = new Vector2(300f, 360f);
            parts.CpuPortrait.sizeDelta = new Vector2(300f, 360f);
            parts.PlayerPortrait.pivot = new Vector2(1f, 0.5f);
            parts.CpuPortrait.pivot = new Vector2(0f, 0.5f);

            Vector3 expected = Vector3.zero;

            PlayToEnd(fx.PlayClashRoutine(RoundWinner.Cpu, RoundDecision.AttributeAdvantage, UnitAttribute.Blue, Color.white), () =>
            {
                if (fx.Step == BattleFxPlayer.ClashStep.ImpactHold)
                {
                    Vector3 player = parts.PlayerPortrait.TransformPoint(parts.PlayerPortrait.rect.center);
                    Vector3 cpu = parts.CpuPortrait.TransformPoint(parts.CpuPortrait.rect.center);

                    expected = (player + cpu) * 0.5f;
                }

                if (parts.ImpactBurst.IsShowing)
                {
                    Vector3 local = parts.ImpactBurst.rectTransform.InverseTransformPoint(expected);

                    Assert.That(parts.ImpactBurst.Centre.x, Is.EqualTo(local.x).Within(0.01f));
                    Assert.That(parts.ImpactBurst.Centre.y, Is.EqualTo(local.y).Within(0.01f));
                }
            });

            Assert.That((fx.LastContactWorld - expected).magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void FxOffNeverShowsAnImpact()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);
            fx.FxEnabled = false;

            PlayToEnd(fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.AttributeAdvantage, UnitAttribute.Red, Color.white), () =>
            {
                Assert.That(parts.ImpactBurst.IsShowing, Is.False);
            });

            Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(BattleImpactKind.None));
            Assert.That(parts.ImpactBurst.IsShowing, Is.False);
        }

        [Test]
        public void ThePhase1EntryNeverShowsAnImpact()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            PlayToEnd(fx.PlayClashRoutine(RoundWinner.Player, Color.white), () =>
            {
                Assert.That(parts.ImpactBurst.IsShowing, Is.False);
            });

            Assert.That(fx.LastPlan.ImpactKind, Is.EqualTo(BattleImpactKind.None));
        }

        [Test]
        public void ResettingOrDisablingMidImpactHidesItImmediately()
        {
            foreach (bool disable in new[] { false, true })
            {
                BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);
                Stack<IEnumerator> stack = new Stack<IEnumerator>();

                stack.Push(fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.PowerComparison, UnitAttribute.Red, Color.white));

                int guard = 0;

                while (!parts.ImpactBurst.IsShowing)
                {
                    Assert.That(StepFrame(stack), Is.True);
                    Assert.That(++guard, Is.LessThan(600));
                }

                if (disable)
                {
                    fx.gameObject.SetActive(false);
                }
                else
                {
                    fx.ResetVisuals();
                }

                Assert.That(parts.ImpactBurst.IsShowing, Is.False, disable ? "OnDisable" : "ResetVisuals");
                Assert.That(parts.ImpactBurst.LastVertexCount, Is.EqualTo(0));

                // 古いルーチンを進めても、もう出てきません。
                while (StepFrame(stack))
                {
                    Assert.That(parts.ImpactBurst.IsShowing, Is.False);
                    Assert.That(++guard, Is.LessThan(1200));
                }

                if (disable)
                {
                    fx.gameObject.SetActive(true);
                    Assert.That(parts.ImpactBurst.IsShowing, Is.False);
                }
            }
        }

        [Test]
        public void TenClashesCreateNoObjectsOrComponents()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            int transforms = fx.GetComponentsInChildren<Transform>(true).Length;
            int components = fx.GetComponentsInChildren<Component>(true).Length;
            int graphics = fx.GetComponentsInChildren<Graphic>(true).Length;

            for (int i = 0; i < 10; i++)
            {
                BattleImpactKind kind = VisibleKinds[i % VisibleKinds.Length];
                RoundWinner winner = kind == BattleImpactKind.Draw ? RoundWinner.Draw : (i % 2 == 0 ? RoundWinner.Player : RoundWinner.Cpu);

                Verdict(kind, out RoundDecision decision, out UnitAttribute? attribute);

                PlayToEnd(fx.PlayClashRoutine(winner, decision, attribute, Color.white), null);

                Assert.That(parts.ImpactBurst.IsShowing, Is.False, "clash " + i);
            }

            Assert.That(fx.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
            Assert.That(fx.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(components));
            Assert.That(fx.GetComponentsInChildren<Graphic>(true).Length, Is.EqualTo(graphics));
        }

        [Test]
        public void OnlyAttributeWinsKeepTheWinnerColouredFlash()
        {
            Color winnerColour = new Color(0.9f, 0.1f, 0.1f, 1f);

            foreach (BattleImpactKind kind in VisibleKinds)
            {
                BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);
                Image flash = parts.FlashGroup.GetComponent<Image>();

                RoundWinner winner = kind == BattleImpactKind.Draw ? RoundWinner.Draw : RoundWinner.Cpu;
                Verdict(kind, out RoundDecision decision, out UnitAttribute? attribute);

                PlayToEnd(fx.PlayClashRoutine(winner, decision, attribute, winnerColour), null);

                AttributeEffectProfile profile = AttributeEffectProfile.For(kind);
                Color.RGBToHSV(flash.color, out float h, out float s, out _);

                if (profile.UsesAttributeColor)
                {
                    Assert.That(flash.color, Is.EqualTo(winnerColour), kind + " は勝者の属性色で光ります。");
                }
                else
                {
                    Assert.That(
                        s < 0.15f || (h * 360f >= 38f && h * 360f <= 60f),
                        Is.True,
                        kind + " のフラッシュ " + flash.color + " に属性色が出ています。");
                }
            }
        }

        [Test]
        public void TheFxPlayerIsCompleteOnlyWithTheImpactGraphicWired()
        {
            BattleFxPlayer fx = CreateFx(out TestBattleViews.FxParts parts);

            Assert.That(fx.HasRequiredReferences(), Is.True);
            Assert.That(fx.ImpactBurst, Is.SameAs(parts.ImpactBurst));
        }

        // ---------------- 補助 ----------------

        private ImpactBurstGraphic CreateGraphic(Transform parent = null)
        {
            GameObject go = views.CreateObject("ImpactBurst", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000f, 900f);

            return go.AddComponent<ImpactBurstGraphic>();
        }

        private BattleFxPlayer CreateFx(out TestBattleViews.FxParts parts)
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out parts);

            TestBattleViews.SetField(fx, "deltaTimeSource", (Func<float>)(() => Frame));

            parts.PlayerPortrait.anchoredPosition = new Vector2(-18f, 0f);
            parts.CpuPortrait.anchoredPosition = new Vector2(18f, 0f);
            parts.CpuPortrait.localScale = new Vector3(-1f, 1f, 1f);
            parts.ImpactBurst.rectTransform.sizeDelta = new Vector2(1000f, 900f);

            fx.FxEnabled = true;

            return fx;
        }

        private static void Verdict(BattleImpactKind kind, out RoundDecision decision, out UnitAttribute? attribute)
        {
            attribute = null;

            switch (kind)
            {
                case BattleImpactKind.Red:
                    decision = RoundDecision.AttributeAdvantage;
                    attribute = UnitAttribute.Red;
                    return;

                case BattleImpactKind.Blue:
                    decision = RoundDecision.AttributeAdvantage;
                    attribute = UnitAttribute.Blue;
                    return;

                case BattleImpactKind.Green:
                    decision = RoundDecision.AttributeAdvantage;
                    attribute = UnitAttribute.Green;
                    return;

                case BattleImpactKind.Power:
                    decision = RoundDecision.PowerComparison;
                    attribute = UnitAttribute.Blue;
                    return;

                case BattleImpactKind.Core:
                    decision = RoundDecision.CoreComparison;
                    return;

                default:
                    decision = RoundDecision.Draw;
                    return;
            }
        }

        private static string Pose(BattleFxPlayer fx, TestBattleViews.FxParts parts)
        {
            return fx.Step + " " +
                   parts.PlayerPortrait.anchoredPosition.ToString("F4") + " " +
                   parts.CpuPortrait.anchoredPosition.ToString("F4") + " " +
                   parts.PlayerPortrait.localScale.ToString("F4") + " " +
                   parts.CpuPortrait.localScale.ToString("F4") + " " +
                   parts.PlayerGroup.alpha.ToString("F4") + " " +
                   parts.CpuGroup.alpha.ToString("F4") + " " +
                   parts.FlashGroup.alpha.ToString("F4") + " " +
                   fx.ShakeOffset.ToString("F4");
        }

        private static void PlayToEnd(IEnumerator routine, Action onFrame)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            int guard = 0;

            while (StepFrame(stack))
            {
                onFrame?.Invoke();

                Assert.That(++guard, Is.LessThan(600), "演出が終わりません。");
            }
        }

        /// <summary>入れ子の IEnumerator も Unity と同じ順で辿り、1フレーム進めます。</summary>
        private static bool StepFrame(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();

                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                return true;
            }

            return false;
        }

        private static List<UIVertex> Build(ImpactBurstGraphic graphic)
        {
            List<UIVertex> vertices = new List<UIVertex>();

            using (VertexHelper vh = new VertexHelper())
            {
                graphic.FillMesh(vh);

                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    UIVertex v = new UIVertex();
                    vh.PopulateUIVertex(ref v, i);
                    vertices.Add(v);
                }
            }

            return vertices;
        }

        private static float MeanHue(List<UIVertex> vertices)
        {
            float x = 0f;
            float y = 0f;

            foreach (UIVertex v in vertices)
            {
                Color c = v.color;

                if (c.a <= 0.01f)
                {
                    continue;
                }

                Color.RGBToHSV(c, out float h, out float s, out _);

                x += Mathf.Cos(h * Mathf.PI * 2f) * s;
                y += Mathf.Sin(h * Mathf.PI * 2f) * s;
            }

            float hue = Mathf.Atan2(y, x) * Mathf.Rad2Deg;

            return hue < 0f ? hue + 360f : hue;
        }

        private static void AssertMirrored(List<UIVertex> vertices, Vector2 centre, Vector2 mirror, string label)
        {
            foreach (UIVertex v in vertices)
            {
                Vector2 offset = (Vector2)v.position - centre;
                Vector2 target = Vector2.Scale(offset, mirror);
                Color32 colour = v.color;

                bool found = false;

                foreach (UIVertex w in vertices)
                {
                    Vector2 other = (Vector2)w.position - centre;
                    Color32 otherColour = w.color;

                    if ((other - target).sqrMagnitude < 0.01f * 0.01f &&
                        otherColour.r == colour.r &&
                        otherColour.g == colour.g &&
                        otherColour.b == colour.b &&
                        otherColour.a == colour.a)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, label + ": " + offset + " の鏡像がありません。");
            }
        }
    }
}
