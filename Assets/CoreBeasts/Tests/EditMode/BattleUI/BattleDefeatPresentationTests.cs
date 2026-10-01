using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 敗北表現。使用済みの暗転とは別概念として、敗れた側だけをグレー化します。
    ///
    /// 勝敗はここでは決めません。<see cref="RoundResult.Winner"/>を言い換えた
    /// 公開済みの結果だけを見ていること、公開前には出ないこと、
    /// 個体IDで正しい相手にだけ届くことを確かめます。
    /// </summary>
    public sealed class BattleDefeatPresentationTests
    {
        private TestBattleViews views;
        private TestBattleCards cards;
        private AttributePalette palette;
        private UiTextCatalog uiText;
        private FakeBattleText battleText;

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
            cards = new TestBattleCards();

            palette = ScriptableObject.CreateInstance<AttributePalette>();
            uiText = ScriptableObject.CreateInstance<UiTextCatalog>();
            battleText = new FakeBattleText();
        }

        [TearDown]
        public void TearDown()
        {
            views.Cleanup();
            cards.Cleanup();

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(uiText);
        }

        // ---------------- 素材 ----------------

        /// <summary>PLAYERが必ず属性勝ちする組み合わせ（Red が Green に勝つ）。</summary>
        private static BattleFlowCoordinator CreatePlayerWins()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
                new FirstAvailableSelector()));
        }

        /// <summary>CPUが必ず属性勝ちする組み合わせ。</summary>
        private static BattleFlowCoordinator CreateCpuWins()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Green, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector()));
        }

        /// <summary>毎ラウンド必ず引き分けになる組み合わせ。</summary>
        private static BattleFlowCoordinator CreateAlwaysDraw()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector()));
        }

        private sealed class SilentTrayListener : IBattleTrayListener
        {
            public void OnTraySlotTapped(string instanceId)
            {
            }
        }

        /// <summary>DEPLOYし、対戦演出まで終わった直後（結果を画面へ出す瞬間）で止めます。</summary>
        private static void DeployAndReveal(
            BattleFlowCoordinator coordinator, string playerInstanceId = null)
        {
            coordinator.SelectPlayerUnit(
                playerInstanceId ??
                coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            coordinator.Deploy();
            coordinator.CompleteResolve();
            coordinator.RevealRoundOutcome();
        }

        /// <summary>1ラウンドを最後（次ラウンド開始）まで通します。</summary>
        private static void PlayWholeRound(
            BattleFlowCoordinator coordinator, string playerInstanceId = null)
        {
            DeployAndReveal(coordinator, playerInstanceId);

            coordinator.PublishPendingOutcome();
            coordinator.AdvanceToNextRound();
        }

        /// <summary>進行役と同じIDのトレイを組み立てます。</summary>
        private BattleSquadTrayView BuildTray(BattleSideRoster side)
        {
            BattleSquadTrayView tray = views.CreateTray(out _);

            tray.Build(new SilentTrayListener(), uiText);
            tray.Show(side, palette, uiText);

            return tray;
        }

        /// <summary>進行役の状態を、コントローラと同じ規則で出場表示へ写します。</summary>
        private static void ApplyToCombatants(
            BattleFlowCoordinator coordinator,
            BattleCombatantView player,
            BattleCombatantView cpu)
        {
            BattleUiState state = coordinator.State;
            BattleSlotOutcome revealed = coordinator.LastRevealedOutcome;

            player.SetDefeated(BattleDefeatPresentation.GreysPlayer(state, revealed));
            cpu.SetDefeated(BattleDefeatPresentation.GreysCpu(state, revealed));
        }

        private BattleCombatantView Combatant(BattleUnitCard card)
        {
            BattleCombatantView view = views.CreateCombatant(out _);

            view.Bind(palette, uiText, battleText);
            view.Show(card);

            return view;
        }

        // ---------------- 出場中の表示 ----------------

        [Test]
        public void WhenTheCpuWins_ThePlayerCombatantBecomesDefeated()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");

            BattleCombatantView player = Combatant(side.Cards[0]);
            BattleCombatantView cpu = Combatant(side.Cards[1]);

            DeployAndReveal(coordinator);
            ApplyToCombatants(coordinator, player, cpu);

            Assert.That(player.IsDefeated, Is.True, "敗れたPLAYER側がグレーになりません。");
            Assert.That(cpu.IsDefeated, Is.False, "勝者の色を変えてはいけません。");
        }

        [Test]
        public void WhenThePlayerWins_OnlyTheCpuCombatantBecomesDefeated()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");

            BattleCombatantView player = Combatant(side.Cards[0]);
            BattleCombatantView cpu = Combatant(side.Cards[1]);

            DeployAndReveal(coordinator);
            ApplyToCombatants(coordinator, player, cpu);

            Assert.That(cpu.IsDefeated, Is.True, "敗れたCPU側がグレーになりません。");
            Assert.That(player.IsDefeated, Is.False, "勝者の色を変えてはいけません。");
        }

        [Test]
        public void OnADraw_NeitherCombatantBecomesDefeated()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");

            BattleCombatantView player = Combatant(side.Cards[0]);
            BattleCombatantView cpu = Combatant(side.Cards[1]);

            DeployAndReveal(coordinator);

            Assert.That(
                coordinator.LastRevealedOutcome,
                Is.EqualTo(BattleSlotOutcome.Draw));

            ApplyToCombatants(coordinator, player, cpu);

            Assert.That(player.IsDefeated, Is.False);
            Assert.That(cpu.IsDefeated, Is.False);
        }

        [Test]
        public void BeforeTheResultIsRevealed_NoCombatantIsGreyed()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");

            BattleCombatantView player = Combatant(side.Cards[0]);
            BattleCombatantView cpu = Combatant(side.Cards[1]);

            // 選択しただけ。DEPLOY前。
            coordinator.SelectPlayerUnit(
                coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            ApplyToCombatants(coordinator, player, cpu);

            Assert.That(player.IsDefeated, Is.False, "DEPLOY前に敗北表現が出ています。");

            // DEPLOY直後。まだ対戦演出の最中で、勝敗は画面に出ていません。
            coordinator.Deploy();

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));

            ApplyToCombatants(coordinator, player, cpu);

            Assert.That(
                player.IsDefeated,
                Is.False,
                "対戦演出の最中に敗北表現を先出ししてはいけません。");

            Assert.That(cpu.IsDefeated, Is.False);
        }

        [Test]
        public void TheRevealedDefeatClearsWhenTheNextRoundStarts()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            PlayWholeRound(coordinator);

            Assert.That(
                coordinator.State,
                Is.EqualTo(BattleUiState.Selecting),
                "次ラウンドが始まっていません。");

            Assert.That(
                coordinator.LastRevealedOutcome,
                Is.EqualTo(BattleSlotOutcome.None),
                "出場中の2枠は作り直されるため、前ラウンドの結果は持ち越しません。");
        }

        // ---------------- 自軍トレイ ----------------

        [Test]
        public void OnlyTheInstanceThatLostIsGreyedInTheTray()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");
            BattleSquadTrayView tray = BuildTray(side);

            string lost = coordinator.Session.PlayerAvailableUnits[0].InstanceId;

            PlayWholeRound(coordinator, lost);

            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes,
                coordinator.RevealedOutcomes);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                BattleTraySlotView slot = tray.Slots[i];
                bool isTheOneThatLost = slot.Card.InstanceId == lost;

                Assert.That(
                    slot.IsDefeated,
                    Is.EqualTo(isTheOneThatLost),
                    slot.Card.InstanceId + " の敗北表示が一致しません。");
            }
        }

        [Test]
        public void AnotherInstanceOfTheSameDefinitionIsNotGreyed()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            // 7枠すべてが同じ Definition を共有する編成を組みます。
            CoreBeastDefinition shared =
                cards.CreateDefinition("twin", UnitAttribute.Green, 50);

            List<BattleUnitCard> twins = new List<BattleUnitCard>();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                BattleUnitCard.TryCreate("p" + i, 1, shared, out BattleUnitCard card);
                twins.Add(card);
            }

            BattleSideRoster.TryCreate(twins, out BattleSideRoster side, out _);

            BattleSquadTrayView tray = BuildTray(side);

            string lost = "p0";

            PlayWholeRound(coordinator, lost);

            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes,
                coordinator.RevealedOutcomes);

            Assert.That(tray.Slots[0].Card.Definition, Is.SameAs(shared));
            Assert.That(tray.Slots[1].Card.Definition, Is.SameAs(shared));

            Assert.That(tray.Slots[0].IsDefeated, Is.True);
            Assert.That(
                tray.Slots[1].IsDefeated,
                Is.False,
                "同じDefinitionの別個体まで巻き添えでグレーになっています。");
        }

        [Test]
        public void TheTrayKeepsPastDefeatsThroughLaterRounds()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");
            BattleSquadTrayView tray = BuildTray(side);

            string first = coordinator.Session.PlayerAvailableUnits[0].InstanceId;
            PlayWholeRound(coordinator, first);

            string second = coordinator.Session.PlayerAvailableUnits[0].InstanceId;
            PlayWholeRound(coordinator, second);

            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes,
                coordinator.RevealedOutcomes);

            Assert.That(first, Is.Not.EqualTo(second));

            Assert.That(
                tray.Slots[0].IsDefeated,
                Is.True,
                "次のラウンドへ進むと過去の敗北表示が消えています。");

            Assert.That(tray.Slots[1].IsDefeated, Is.True);
        }

        [Test]
        public void BeforeTheResultIsRevealed_NoTraySlotIsGreyed()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");
            BattleSquadTrayView tray = BuildTray(side);

            string chosen = coordinator.Session.PlayerAvailableUnits[0].InstanceId;

            coordinator.SelectPlayerUnit(chosen);
            coordinator.Deploy();

            // 解決済みだが、まだ画面へ出していません。
            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes,
                coordinator.RevealedOutcomes);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].IsDefeated,
                    Is.False,
                    "結果の公開前に敗北表現が出ています。");
            }
        }

        [Test]
        public void RematchClearsEveryDefeatedSlot()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins();
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");
            BattleSquadTrayView tray = BuildTray(side);

            // 決着まで進めます。
            while (coordinator.State != BattleUiState.MatchFinished)
            {
                PlayWholeRound(coordinator);
            }

            tray.RefreshStates(
                coordinator.Session, null,
                coordinator.Outcomes, coordinator.RevealedOutcomes);

            Assert.That(tray.Slots[0].IsDefeated, Is.True, "前提が崩れています。");

            Assert.That(coordinator.Rematch(), Is.True);

            tray.Show(side, palette, uiText);
            tray.RefreshStates(
                coordinator.Session, null,
                coordinator.Outcomes, coordinator.RevealedOutcomes);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].IsDefeated,
                    Is.False,
                    "REMATCHで敗北表現が解除されていません。");
            }
        }

        // ---------------- 見た目 ----------------

        [Test]
        public void DefeatReplacesTheAttributeLayersWithNeutralGrey()
        {
            BattleSideRoster side = cards.CreateSide("p");

            BattleCombatantView player = Combatant(side.Cards[0]);

            BeastThumbnailView thumbnail =
                player.GetComponentInChildren<BeastThumbnailView>(true);

            Color primaryBefore = thumbnail.PrimaryLayerColor;

            player.SetDefeated(true);

            Color primary = thumbnail.PrimaryLayerColor;
            Color secondary = thumbnail.SecondaryLayerColor;

            Assert.That(
                primary,
                Is.Not.EqualTo(primaryBefore),
                "属性色のままでは、使用済みの暗転と見分けがつきません。");

            // ニュートラルグレー: R/G/B がほぼ揃っていること。
            Assert.That(
                Mathf.Max(Mathf.Abs(primary.r - primary.g), Mathf.Abs(primary.g - primary.b)),
                Is.LessThan(0.12f),
                "敗北色が中間グレーになっていません。");

            Assert.That(primary.r, Is.InRange(115f / 255f, 138f / 255f));
            Assert.That(primary.g, Is.InRange(121f / 255f, 144f / 255f));
            Assert.That(primary.b, Is.InRange(133f / 255f, 155f / 255f));

            Assert.That(secondary.r, Is.EqualTo(primary.r));

            // 線画とシルエットが読める明るさを残すこと。
            Assert.That(
                thumbnail.BaseLayerColor.a,
                Is.InRange(0.55f, 0.70f),
                "キャラクターのalphaが規定の範囲外です。");

            Assert.That(
                thumbnail.BaseLayerColor.r,
                Is.EqualTo(1f),
                "base レイヤーの色を変えると線画が読めなくなります。");
        }

        [Test]
        public void DefeatIsNotJustACanvasGroupFade()
        {
            BattleSideRoster side = cards.CreateSide("p");
            BattleCombatantView player = Combatant(side.Cards[0]);

            float before = player.PortraitGroup.alpha;

            player.SetDefeated(true);

            Assert.That(
                player.PortraitGroup.alpha,
                Is.EqualTo(before),
                "CanvasGroupの一括減光だけに頼ると、情報パネルまで読みにくくなります。");
        }

        [Test]
        public void TheLossBadgeKeepsItsOwnColourOnADefeatedSlot()
        {
            BattleSideRoster side = cards.CreateSide("p");

            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(side.Cards[0], palette, uiText);

            slot.SetOutcome(BattleSlotOutcome.Loss);

            Color badgeBefore = parts.BadgeImage.color;

            slot.SetDefeated(true);

            Assert.That(slot.IsDefeated, Is.True);

            Assert.That(
                parts.BadgeImage.color,
                Is.EqualTo(badgeBefore),
                "L バッジの赤までグレー化してはいけません。");

            Assert.That(
                parts.BadgeGroup.ignoreParentGroups,
                Is.True,
                "バッジは親の減光を受けない設定のままにします。");

            Assert.That(parts.BadgeGroup.alpha, Is.EqualTo(1f));
        }

        [Test]
        public void DefeatAndUsedDimmingStayIndependent()
        {
            BattleSideRoster side = cards.CreateSide("p");

            BattleTraySlotView slot = views.CreateTraySlot(out _);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(side.Cards[0], palette, uiText);

            // 使用済みでも、勝っていればグレーにはなりません。
            slot.SetState(BattleSlotState.Used);

            Assert.That(slot.State, Is.EqualTo(BattleSlotState.Used));
            Assert.That(slot.IsDefeated, Is.False);

            // 敗北を足しても、使用済みの状態は保たれます。
            slot.SetDefeated(true);

            Assert.That(slot.State, Is.EqualTo(BattleSlotState.Used));
            Assert.That(slot.IsDefeated, Is.True);

            // 未使用のまま敗北だけを解くこともできます。
            slot.SetDefeated(false);

            Assert.That(slot.IsDefeated, Is.False);
            Assert.That(slot.State, Is.EqualTo(BattleSlotState.Used));
        }

        // ---------------- 規則そのもの ----------------

        [Test]
        public void TheRuleNeverGreysAnyoneBeforeTheResultIsOnScreen()
        {
            BattleUiState[] tooEarly =
            {
                BattleUiState.Loading,
                BattleUiState.SquadRequired,
                BattleUiState.Selecting,
                BattleUiState.Resolving,
            };

            for (int i = 0; i < tooEarly.Length; i++)
            {
                Assert.That(
                    BattleDefeatPresentation.GreysPlayer(
                        tooEarly[i], BattleSlotOutcome.Loss),
                    Is.False,
                    tooEarly[i] + " で敗北表現を先出ししています。");

                Assert.That(
                    BattleDefeatPresentation.GreysCpu(
                        tooEarly[i], BattleSlotOutcome.Win),
                    Is.False,
                    tooEarly[i] + " で敗北表現を先出ししています。");
            }
        }
    }
}
