using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.HomeUI;
using CoreBeasts.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// Phase 7（Home・報酬・ガチャ・獲得画面）の純粋な表示モデル・遷移・演出設計と、演出の部品。
    /// </summary>
    public sealed class HomeScreenTests
    {
        private const float Frame = 1f / 60f;

        private sealed class FixedRandom : IGachaRandomSource
        {
            private readonly int value;

            internal FixedRandom(int selected)
            {
                value = selected;
            }

            public int Next(int maxExclusive)
            {
                return value < maxExclusive ? value : maxExclusive - 1;
            }
        }

        private CoreBeastRoster roster;
        private readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            roster = TestRosterFactory.Create(8);
            PlayerProfileProvider.SetRepository(new InMemoryPlayerProfileRepository());
            GameFlowState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            created.Clear();
            TestRosterFactory.Destroy(roster);
            PlayerProfileProvider.Reset();
            GameFlowState.Reset();
            PresentationSettings.FxEnabled = true;
        }

        // ---------------- 経済値 ----------------

        [Test]
        public void TheAgreedEconomyIsUnchanged()
        {
            Assert.That(GameEconomy.StartingCoins, Is.EqualTo(100));
            Assert.That(GameEconomy.GachaCost, Is.EqualTo(100));
            Assert.That(GameEconomy.WinReward, Is.EqualTo(30));
            Assert.That(GameEconomy.DrawReward, Is.EqualTo(20));
            Assert.That(GameEconomy.LossReward, Is.EqualTo(10));
            Assert.That(GameEconomy.StarterBeastCount, Is.EqualTo(7));
        }

        // ---------------- Home ----------------

        [Test]
        public void TheHomeSummaryShowsCoinsRecordSquadAndOwnedCount()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.RecordBattle(BattleRewardOutcome.Win, GameEconomy.WinReward);
            profile.RecordBattle(BattleRewardOutcome.Draw, GameEconomy.DrawReward);
            profile.RecordBattle(BattleRewardOutcome.Loss, GameEconomy.LossReward);

            HomeSummary summary = HomeSummary.Build(profile, roster, "1");

            Assert.That(summary.Coins, Is.EqualTo(160));
            Assert.That(summary.CoinText, Is.EqualTo("160"));
            Assert.That(summary.RecordText, Is.EqualTo("W 1  D 1  L 1"));
            Assert.That(summary.SquadText, Is.EqualTo("SET 1"));
            Assert.That(summary.OwnedText, Is.EqualTo("OWNED 7 / 8"));
        }

        // ---------------- 報酬 ----------------

        [Test]
        public void TheRewardShowsTheBalanceBeforeAndAfterWithoutAddingAgain()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.RecordBattle(BattleRewardOutcome.Win, GameEconomy.WinReward);
            GameFlowState.AddPendingReward(BattleRewardOutcome.Win, GameEconomy.WinReward);

            RewardSummary summary = GameFlowState.ConsumePendingSummary(profile.Coins);

            Assert.That(summary.OutcomeText, Is.EqualTo("WIN"));
            Assert.That(summary.EarnedText, Is.EqualTo("+30 CORE COIN"));
            Assert.That(summary.BalanceBefore, Is.EqualTo(100));
            Assert.That(summary.BalanceAfter, Is.EqualTo(130));
            Assert.That(summary.CanAffordGacha, Is.True);
            Assert.That(summary.GachaReasonText, Is.Empty);
            Assert.That(profile.Coins, Is.EqualTo(130), "表示しても加算し直しません。");
        }

        [Test]
        public void TheUnconfirmedRewardIsConsumedExactlyOnce()
        {
            GameFlowState.AddPendingReward(BattleRewardOutcome.Draw, GameEconomy.DrawReward);

            RewardSummary first = GameFlowState.ConsumePendingSummary(120);
            RewardSummary second = GameFlowState.ConsumePendingSummary(120);

            Assert.That(first.HasReward, Is.True);
            Assert.That(first.OutcomeText, Is.EqualTo("DRAW"));
            Assert.That(second.HasReward, Is.False, "未確認の報酬は一度だけ表示します。");
            Assert.That(GameFlowState.HasPendingReward, Is.False);
        }

        [Test]
        public void SeveralUnconfirmedBattlesShowTheTotalAndTheCount()
        {
            GameFlowState.AddPendingReward(BattleRewardOutcome.Win, GameEconomy.WinReward);
            GameFlowState.AddPendingReward(BattleRewardOutcome.Loss, GameEconomy.LossReward);
            GameFlowState.AddPendingReward(BattleRewardOutcome.Draw, GameEconomy.DrawReward);

            RewardSummary summary = GameFlowState.ConsumePendingSummary(160);

            Assert.That(summary.TotalReward, Is.EqualTo(60));
            Assert.That(summary.Battles, Is.EqualTo(3));
            Assert.That(summary.OutcomeText, Is.EqualTo("3 BATTLES"));
            Assert.That(summary.BreakdownText, Is.EqualTo("W 1  D 1  L 1"));
            Assert.That(summary.BalanceBefore, Is.EqualTo(100));
        }

        [Test]
        public void TheRewardExplainsWhyTheGachaIsLocked()
        {
            GameFlowState.AddPendingReward(BattleRewardOutcome.Loss, GameEconomy.LossReward);

            RewardSummary summary = GameFlowState.ConsumePendingSummary(60);

            Assert.That(summary.OutcomeText, Is.EqualTo("LOSS"));
            Assert.That(summary.CanAffordGacha, Is.False);
            Assert.That(summary.GachaShortfall, Is.EqualTo(40));
            Assert.That(summary.GachaReasonText, Is.EqualTo("NEED 40 MORE CORE COIN"));
        }

        // ---------------- ガチャ ----------------

        [Test]
        public void TheGachaSummaryGuaranteesNewBeastsUntilEverythingIsOwned()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            GachaSummary before = GachaSummary.Build(profile, roster);

            Assert.That(before.CatalogCount, Is.EqualTo(8));
            Assert.That(before.UnownedCount, Is.EqualTo(1));
            Assert.That(before.PoolStatusText, Is.EqualTo("NEW GUARANTEED"));
            Assert.That(before.CostText, Is.EqualTo("COST 100"));
            Assert.That(before.ActivateText, Is.EqualTo("ACTIVATE  100"));
            Assert.That(before.CanActivate, Is.True);

            profile.Acquire(roster.Owned[7].InstanceId);
            GachaSummary after = GachaSummary.Build(profile, roster);

            Assert.That(after.UnownedCount, Is.EqualTo(0));
            Assert.That(after.PoolStatusText, Is.EqualTo("DUPLICATES AVAILABLE"));
        }

        [Test]
        public void ShortOfCoinsTheGachaCannotStartAndNothingChanges()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            Assert.That(profile.TrySpendCoins(60), Is.True);

            GachaSummary summary = GachaSummary.Build(profile, roster);

            Assert.That(summary.CanActivate, Is.False);
            Assert.That(summary.ReasonText, Is.EqualTo("NEED 60 MORE CORE COIN"));

            int saves = 0;
            GachaSummonSession session = new GachaSummonSession(new GachaService(new FixedRandom(0)), () => saves++);

            Assert.That(session.TryBegin(profile, roster, out GachaResult result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(profile.Coins, Is.EqualTo(40));
            Assert.That(profile.Beasts.Count, Is.EqualTo(7));
            Assert.That(saves, Is.EqualTo(0));
            Assert.That(session.IsBusy, Is.False);
        }

        [Test]
        public void TheResultIsDecidedAndSavedOnceAndRepeatedPressesAreIgnored()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.AddCoins(500);

            int saves = 0;
            GachaSummonSession session = new GachaSummonSession(new GachaService(new FixedRandom(0)), () => saves++);

            Assert.That(session.TryBegin(profile, roster, out GachaResult first), Is.True);
            int coinsAfterFirst = profile.Coins;

            for (int i = 0; i < 5; i++)
            {
                Assert.That(session.TryBegin(profile, roster, out GachaResult again), Is.False, "演出中は次を始めません。");
                Assert.That(again, Is.Null);
            }

            Assert.That(profile.Coins, Is.EqualTo(coinsAfterFirst), "連打しても1回ぶんしか消費しません。");
            Assert.That(coinsAfterFirst, Is.EqualTo(500));
            Assert.That(saves, Is.EqualTo(1), "結果は開始時に一度だけ保存します。");
            Assert.That(session.Result, Is.SameAs(first), "演出の途中で結果を変えません。");
            Assert.That(first.IsNew, Is.True, "未所持が残る間は新規です。");
            Assert.That(first.Beast.InstanceId, Is.EqualTo(roster.Owned[7].InstanceId));

            session.Finish();

            Assert.That(session.TryBegin(profile, roster, out GachaResult second), Is.True);
            Assert.That(second.IsNew, Is.False, "全取得後は重複です。");
            Assert.That(profile.Coins, Is.EqualTo(400));
        }

        // ---------------- 獲得 ----------------

        [Test]
        public void TheAcquisitionShowsNewOrDuplicateWithCopiesAndKeepsTheStats()
        {
            PlayerProfile profile = PlayerProfileProvider.Get(roster);
            profile.AddCoins(100);
            GachaService service = new GachaService(new FixedRandom(0));

            Assert.That(service.TryPull(profile, roster, out GachaResult newResult), Is.True);
            AcquisitionSummary fresh = AcquisitionSummary.Build(newResult, profile, roster);

            Assert.That(fresh.TitleText, Is.EqualTo("NEW CORE BEAST"));
            Assert.That(fresh.CopiesBefore, Is.EqualTo(0));
            Assert.That(fresh.CopiesAfter, Is.EqualTo(1));
            Assert.That(fresh.ShowsUnitSetLink, Is.True, "新しい個体だけ編成への導線を出します。");
            Assert.That(fresh.OwnedText, Is.EqualTo("OWNED 8 / 8"));
            Assert.That(fresh.Level, Is.EqualTo(8));
            Assert.That(fresh.LevelText, Is.EqualTo("LV 8"));

            CoreBeastDefinition definition = fresh.Definition;
            int power = definition.PowerOf(definition.PrimaryAttribute);
            int core = definition.Core;

            Assert.That(service.TryPull(profile, roster, out GachaResult dupResult), Is.True);
            AcquisitionSummary duplicate = AcquisitionSummary.Build(dupResult, profile, roster);

            Assert.That(duplicate.TitleText, Is.EqualTo("DUPLICATE"));
            Assert.That(duplicate.CopiesBefore, Is.EqualTo(duplicate.CopiesAfter - 1));
            Assert.That(duplicate.CopiesAfter, Is.GreaterThanOrEqualTo(2));
            Assert.That(duplicate.ShowsUnitSetLink, Is.False);
            Assert.That(definition.PowerOf(definition.PrimaryAttribute), Is.EqualTo(power), "重複で能力値は変えません。");
            Assert.That(definition.Core, Is.EqualTo(core));
            Assert.That(AcquisitionSummary.Build(null, profile, roster).IsValid, Is.False);
        }

        // ---------------- 遷移 ----------------

        [Test]
        public void TheFlowAllowsOnlyTheAgreedTransitions()
        {
            Assert.That(HomeFlow.InitialPage(false), Is.EqualTo(HomePage.Home));
            Assert.That(HomeFlow.InitialPage(true), Is.EqualTo(HomePage.Reward));

            HomeFlow flow = new HomeFlow();

            Assert.That(flow.TryGo(HomePage.Acquisition), Is.False, "Acquisition へはガチャの完了だけで移ります。");
            Assert.That(flow.TryGo(HomePage.Reward), Is.False, "Reward はシーン読み込み時だけです。");
            Assert.That(flow.TryGo(HomePage.Gacha), Is.True);
            Assert.That(flow.TryGo(HomePage.Collection), Is.False);
            Assert.That(flow.TryGo(HomePage.Summoning), Is.True);
            Assert.That(flow.IsInputLocked, Is.True);
            Assert.That(flow.TryGo(HomePage.Home), Is.False, "演出中は戻れません。");
            Assert.That(flow.CompleteSummon(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(HomePage.Acquisition));
            Assert.That(flow.IsInputLocked, Is.False);
            Assert.That(flow.CompleteSummon(), Is.False);
            Assert.That(flow.TryGo(HomePage.Collection), Is.True);
            Assert.That(flow.TryGo(HomePage.Home), Is.True);

            Assert.That(flow.Begin(HomePage.Acquisition), Is.False, "再読み込みで Acquisition を勝手に出しません。");
            Assert.That(flow.Begin(HomePage.Reward), Is.True);
            Assert.That(flow.TryGo(HomePage.Gacha), Is.True, "Reward から Gacha へ進めます。");
        }

        [Test]
        public void AbortingDuringTheSummonReturnsHomeAndOpensInput()
        {
            HomeFlow flow = new HomeFlow();
            flow.TryGo(HomePage.Gacha);
            flow.TryGo(HomePage.Summoning);

            flow.Abort();

            Assert.That(flow.Current, Is.EqualTo(HomePage.Home));
            Assert.That(flow.IsInputLocked, Is.False);
            Assert.That(flow.CompleteSummon(), Is.False, "中断した演出の完了で Acquisition へ移りません。");
        }

        // ---------------- 演出設計 ----------------

        [Test]
        public void TheSummonPlanIsShortAndNeverFlashesFullyWhite()
        {
            GachaSummonPlan fx = GachaSummonPlan.Create(true);
            GachaSummonPlan plain = GachaSummonPlan.Create(false);

            Assert.That(fx.Duration, Is.InRange(0.9f, 1.4f));
            Assert.That(plain.Duration, Is.LessThan(fx.Duration), "FX OFF は短縮します。");
            Assert.That(plain.PlaysGraphics, Is.False);
            Assert.That(GachaSummonPlan.MaxFlashAlpha, Is.LessThanOrEqualTo(0.8f));

            GachaSummonPhase last = GachaSummonPhase.Idle;
            List<GachaSummonPhase> order = new List<GachaSummonPhase> { last };

            for (float t = 0f; t <= fx.Duration + 0.05f; t += 0.01f)
            {
                GachaSummonPhase phase = fx.PhaseAt(t);

                if (phase != last)
                {
                    order.Add(phase);
                    last = phase;
                }

                Assert.That(fx.FlashAlpha(t), Is.LessThanOrEqualTo(GachaSummonPlan.MaxFlashAlpha + 0.0001f));
                Assert.That(plain.FlashAlpha(t), Is.EqualTo(0f));
            }

            Assert.That(order, Is.EqualTo(new[]
            {
                GachaSummonPhase.Idle, GachaSummonPhase.Compress, GachaSummonPhase.Ring, GachaSummonPhase.Flash, GachaSummonPhase.Done,
            }), "コア待機 → 圧縮 → リング展開 → 白金フラッシュ の順です。");
            Assert.That(plain.PhaseAt(0.1f), Is.EqualTo(GachaSummonPhase.Plain));
        }

        // ---------------- 演出の部品 ----------------

        [Test]
        public void TheSummonGraphicStaysInsideItsRectAndBudget()
        {
            GachaSummonGraphic graphic = NewObject("SummonGraphic").AddComponent<GachaSummonGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(800f, 520f);

            Assert.That(GachaSummonGraphic.MaxVertices, Is.LessThanOrEqualTo(GachaSummonPlan.VertexBudget));

            foreach (GachaSummonPhase phase in new[] { GachaSummonPhase.Idle, GachaSummonPhase.Compress, GachaSummonPhase.Ring, GachaSummonPhase.Flash })
            {
                for (float p = 0f; p <= 1f; p += 0.1f)
                {
                    graphic.Show(phase, p, phase == GachaSummonPhase.Flash ? 0.8f : 0f);

                    int count = Build(graphic.FillMesh);

                    Assert.That(count, Is.LessThanOrEqualTo(GachaSummonPlan.VertexBudget), phase.ToString());
                    Assert.That(graphic.LastMaxDistance, Is.LessThanOrEqualTo(260f + 0.01f), "描画は矩形の短い辺の内側だけです。");
                }
            }

            graphic.Show(GachaSummonPhase.Flash, 0.2f, 1f);
            Assert.That(graphic.FlashAlpha, Is.LessThanOrEqualTo(0.8f), "フラッシュは0.8を超えません。");

            graphic.Hide();
            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(Build(graphic.FillMesh), Is.EqualTo(0));
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void TheSummonViewCompletesOnceAndFxOffDrawsNoGraphics([Values(true, false)] bool fx)
        {
            GachaSummonView view = NewView(out GachaSummonGraphic graphic, out TMP_Text label);
            int completions = 0;
            int frames = 0;
            bool drew = false;

            Run(view.PlayRoutine(GachaSummonPlan.Create(fx), () => completions++), () =>
            {
                frames++;
                drew |= graphic.IsShowing;
                Assert.That(view.IsPlaying, Is.True);
                Assert.That(label.text, Is.EqualTo("SUMMONING"));
            });

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(drew, Is.EqualTo(fx), "FX OFF では図形を出しません。");
            Assert.That(frames * Frame, Is.EqualTo(GachaSummonPlan.Create(fx).Duration).Within(Frame * 2f));
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(label.text, Is.Empty);
        }

        [Test]
        public void ResettingMidSummonClearsEverythingWithoutCompleting()
        {
            GachaSummonView view = NewView(out GachaSummonGraphic graphic, out TMP_Text label);
            int completions = 0;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(view.PlayRoutine(GachaSummonPlan.Create(true), () => completions++));

            for (int i = 0; i < 20; i++)
            {
                StepFrame(stack);
            }

            Assert.That(view.IsPlaying, Is.True);
            Assert.That(graphic.IsShowing, Is.True);

            view.ResetVisuals();

            Assert.That(view.IsPlaying, Is.False);
            Assert.That(graphic.IsShowing, Is.False);
            Assert.That(label.text, Is.Empty);
            Assert.That(view.LabelAlpha, Is.EqualTo(0f));

            while (StepFrame(stack))
            {
                Assert.That(graphic.IsShowing, Is.False, "止めたルーチンはもう描きません。");
            }

            Assert.That(completions, Is.EqualTo(0), "中断した演出は完了を通知しません。");
        }

        [Test]
        public void ThePanelAndArrowGraphicsHaveFixedVertexCountsAndTakeNoInput()
        {
            HomePanelGraphic panel = NewObject("Panel").AddComponent<HomePanelGraphic>();
            panel.rectTransform.sizeDelta = new Vector2(600f, 200f);
            panel.Configure(Color.black, Color.blue, Color.white);

            Assert.That(Build(vh => Populate(panel, vh)), Is.EqualTo(HomePanelGraphic.VertexCount));
            Assert.That(panel.raycastTarget, Is.False);

            HomeArrowGraphic arrow = NewObject("Arrow").AddComponent<HomeArrowGraphic>();
            arrow.rectTransform.sizeDelta = new Vector2(80f, 40f);

            Assert.That(Build(vh => Populate(arrow, vh)), Is.EqualTo(HomeArrowGraphic.VertexCount));
            Assert.That(arrow.raycastTarget, Is.False);
        }

        // ---------------- 補助 ----------------

        private GameObject NewObject(string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            created.Add(go);
            return go;
        }

        private GachaSummonView NewView(out GachaSummonGraphic graphic, out TMP_Text label)
        {
            GameObject root = NewObject("Gacha");
            GameObject g = NewObject("Graphic");
            g.transform.SetParent(root.transform, false);
            graphic = g.AddComponent<GachaSummonGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(800f, 520f);

            GameObject l = NewObject("Label");
            l.transform.SetParent(root.transform, false);
            label = l.AddComponent<TextMeshProUGUI>();
            CanvasGroup group = l.AddComponent<CanvasGroup>();

            GachaSummonView view = root.AddComponent<GachaSummonView>();
            view.Bind(graphic, label, group);

            FieldInfo dt = typeof(GachaSummonView).GetField("deltaTimeSource", BindingFlags.NonPublic | BindingFlags.Instance);
            dt.SetValue(view, (Func<float>)(() => Frame));

            return view;
        }

        private static void Populate(Graphic graphic, VertexHelper vh)
        {
            MethodInfo populate = graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
            populate.Invoke(graphic, new object[] { vh });
        }

        private static int Build(Action<VertexHelper> fill)
        {
            using (VertexHelper vh = new VertexHelper())
            {
                fill(vh);
                return vh.currentVertCount;
            }
        }

        private static void Run(IEnumerator routine, Action onFrame)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            int guard = 0;

            while (StepFrame(stack))
            {
                onFrame?.Invoke();
                Assert.That(++guard, Is.LessThan(600));
            }
        }

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
    }
}
