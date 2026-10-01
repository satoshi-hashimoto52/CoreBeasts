using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 勝敗バッジを「いつ」出すか。
    ///
    /// DEPLOYを押した瞬間に W/L/D が出ると、演出を見る前に勝敗が分かってしまいます。
    /// そのため解決済みの結果（Pending）と、表示してよい結果（Presented）を
    /// <see cref="BattleFlowCoordinator"/>の中で分けています。
    ///
    /// 勝敗そのものは一切再計算しません。確定済みの<see cref="RoundResult.Winner"/>を
    /// いつトレイへ渡すかだけを確かめます。
    /// </summary>
    public sealed class BattleOutcomeRevealTests
    {
        private TestBattleViews views;
        private TestBattleCards cards;
        private AttributePalette palette;
        private UiTextCatalog uiText;

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
            cards = new TestBattleCards();

            palette = ScriptableObject.CreateInstance<AttributePalette>();
            uiText = ScriptableObject.CreateInstance<UiTextCatalog>();
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

        /// <summary>プレイヤーが必ず属性勝ちする組み合わせ（Red が Green に勝つ）。</summary>
        private static BattleFlowCoordinator CreatePlayerWins()
        {
            return new BattleFlowCoordinator(new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
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

        /// <summary>トレイを組み立てます。IDは進行役の編成と同じ "p0"〜"p6" です。</summary>
        private BattleSquadTrayView BuildTray()
        {
            BattleSquadTrayView tray = views.CreateTray(out _);

            tray.Build(new SilentTrayListener(), uiText);
            tray.Show(cards.CreateSide("p"), palette, uiText);

            return tray;
        }

        /// <summary>
        /// 実機の<c>RefreshAll</c>と同じ呼び方です。
        /// 渡すのは常に<see cref="BattleFlowCoordinator.Outcomes"/>（公開済み）だけで、
        /// 未公開の結果を渡す口はありません。
        /// </summary>
        private static void RefreshAll(
            BattleSquadTrayView tray,
            BattleFlowCoordinator coordinator)
        {
            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes);
        }

        private static BattleTraySlotView SlotOf(
            BattleSquadTrayView tray,
            string instanceId)
        {
            for (int i = 0; i < tray.Slots.Count; i++)
            {
                if (tray.Slots[i].Card != null &&
                    tray.Slots[i].Card.InstanceId == instanceId)
                {
                    return tray.Slots[i];
                }
            }

            Assert.Fail(instanceId + " の枠が見つかりません。");
            return null;
        }

        /// <summary>タップを受け流すだけのテスト用の受け手。</summary>
        private sealed class SilentTrayListener : IBattleTrayListener
        {
            public void OnTraySlotTapped(string instanceId)
            {
            }
        }

        // ---------------- DEPLOY直後 ----------------

        [Test]
        public void Deploy_KeepsThisRoundsOutcomeUnpublished()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            coordinator.SelectPlayerUnit("p0");

            Assert.That(coordinator.Deploy(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));

            Assert.That(
                coordinator.LastResult.Winner,
                Is.EqualTo(RoundWinner.Player),
                "勝敗そのものはDEPLOYで確定します。");

            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.None),
                "DEPLOY直後に今回分のバッジを出してはいけません。");

            Assert.That(coordinator.HasPendingOutcome, Is.True);
            Assert.That(
                coordinator.PendingOutcome,
                Is.EqualTo(BattleSlotOutcome.Win),
                "結果は控えてあります。まだ見せないだけです。");
            Assert.That(coordinator.PendingOutcomeInstanceId, Is.EqualTo("p0"));
        }

        [Test]
        public void Deploy_DoesNotHandThisRoundsOutcomeToTheTray()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();

            RefreshAll(tray, coordinator);

            Assert.That(
                SlotOf(tray, "p0").Outcome,
                Is.EqualTo(BattleSlotOutcome.None),
                "DEPLOY直後のRefreshAllでバッジが出てはいけません。");
        }

        // ---------------- 演出中・バナー中 ----------------

        [Test]
        public void WhileFxIsPlaying_TheOutcomeStaysHidden()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();

            // Resolving = 接触・フラッシュ・勝敗強調の再生中。
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Resolving));

            RefreshAll(tray, coordinator);

            Assert.That(SlotOf(tray, "p0").Outcome, Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void WhileTheResultBannerIsUp_TheOutcomeStaysHidden()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();

            Assert.That(coordinator.CompleteResolve(), Is.True);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.ShowingResult));

            RefreshAll(tray, coordinator);

            Assert.That(
                SlotOf(tray, "p0").Outcome,
                Is.EqualTo(BattleSlotOutcome.None),
                "結果バナーを出しているあいだも、バッジはまだ出しません。");

            Assert.That(coordinator.HasPendingOutcome, Is.True);
        }

        // ---------------- 演出完了後 ----------------

        [Test]
        public void AfterThePresentation_TheOutcomeIsPublished()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            coordinator.SelectPlayerUnit("p3");
            coordinator.Deploy();
            coordinator.CompleteResolve();

            Assert.That(coordinator.PublishPendingOutcome(), Is.True);
            Assert.That(coordinator.HasPendingOutcome, Is.False);
            Assert.That(
                coordinator.PendingOutcome,
                Is.EqualTo(BattleSlotOutcome.None));

            RefreshAll(tray, coordinator);

            Assert.That(
                SlotOf(tray, "p3").Outcome,
                Is.EqualTo(BattleSlotOutcome.Win),
                "演出と結果表示が終わったら、初めてバッジを出します。");

            // 公開は次ラウンドへ移る前です。
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.ShowingResult));
        }

        [TestCase(RoundWinner.Player, BattleSlotOutcome.Win)]
        [TestCase(RoundWinner.Cpu, BattleSlotOutcome.Loss)]
        [TestCase(RoundWinner.Draw, BattleSlotOutcome.Draw)]
        public void PublishedOutcome_IsOnlyARewordingOfTheWinner(
            RoundWinner winner,
            BattleSlotOutcome expected)
        {
            // 公開そのものは判定を伴いません。確定済みの勝者を言い換えるだけです。
            Assert.That(BattleSlotOutcomes.FromWinner(winner), Is.EqualTo(expected));
        }

        [Test]
        public void PublishingTwice_DoesNotRevealTwice()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();
            coordinator.CompleteResolve();

            Assert.That(coordinator.PublishPendingOutcome(), Is.True);
            Assert.That(
                coordinator.PublishPendingOutcome(),
                Is.False,
                "未公開の結果が無ければ、二度目は何もしません。");

            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Win));
        }

        [Test]
        public void PublishWithoutADeployedRound_DoesNothing()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            Assert.That(coordinator.HasPendingOutcome, Is.False);
            Assert.That(coordinator.PublishPendingOutcome(), Is.False);
        }

        // ---------------- 過去ラウンド ----------------

        [Test]
        public void EarlierBadgesStayVisibleWhileTheNextRoundIsResolving()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            // 1ラウンド目を最後まで進めます。
            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();
            coordinator.CompleteResolve();
            coordinator.PublishPendingOutcome();
            coordinator.AdvanceToNextRound();

            RefreshAll(tray, coordinator);

            Assert.That(SlotOf(tray, "p0").Outcome, Is.EqualTo(BattleSlotOutcome.Win));

            // 2ラウンド目の演出中。
            coordinator.SelectPlayerUnit("p1");
            coordinator.Deploy();

            RefreshAll(tray, coordinator);

            Assert.That(
                SlotOf(tray, "p0").Outcome,
                Is.EqualTo(BattleSlotOutcome.Win),
                "過去ラウンドのバッジは、演出中も消えません。");

            Assert.That(
                SlotOf(tray, "p1").Outcome,
                Is.EqualTo(BattleSlotOutcome.None),
                "今回分だけがまだ未公開です。");
        }

        // ---------------- FX OFF ----------------

        [Test]
        public void FxOff_StillHasAResultDisplayBeforeTheBadgeAppears()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);

            fx.FxEnabled = false;

            Assert.That(
                fx.BannerHoldSeconds,
                Is.GreaterThan(0f),
                "FX OFF でも、結果表示の時間は0にしません。");

            fx.FxEnabled = true;

            Assert.That(
                fx.BannerHoldSeconds,
                Is.GreaterThan(0f));
        }

        [Test]
        public void FxSettingCannotChangeWhenTheBadgeAppears()
        {
            // 公開は演出側ではなく進行役の明示的な処理で起こります。
            // そのため FX ON / OFF では「待ち時間」しか変わりません。
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();

            // 演出の有無に関わらず、CompleteResolve だけでは公開されません。
            coordinator.CompleteResolve();

            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.None));

            Assert.That(coordinator.PublishPendingOutcome(), Is.True);
            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Win));
        }

        // ---------------- REMATCH ----------------

        [Test]
        public void Rematch_ClearsBothPendingAndPublishedOutcomes()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            // 6ラウンドは最後まで進め、7ラウンド目だけ未公開のまま決着させます。
            // （最終ラウンドは公開せずに MatchFinished へ入れるため、
            //   公開済みと未公開が同時に残った状態を作れます）
            for (int i = 0; i < BattleSquad.UnitCount - 1; i++)
            {
                coordinator.SelectPlayerUnit("p" + i);
                coordinator.Deploy();
                coordinator.CompleteResolve();
                coordinator.PublishPendingOutcome();
                coordinator.AdvanceToNextRound();
            }

            coordinator.SelectPlayerUnit("p6");
            coordinator.Deploy();
            coordinator.CompleteResolve();
            coordinator.AdvanceToNextRound();

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));
            Assert.That(
                coordinator.HasPendingOutcome,
                Is.True,
                "最終ラウンドを未公開のまま残した状態を作ります。");
            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Draw),
                "消える前に、確かに公開済みのバッジがあることを確かめます。");

            Assert.That(coordinator.Rematch(), Is.True);

            Assert.That(
                coordinator.HasPendingOutcome,
                Is.False,
                "REMATCHで未公開の結果も消えます。");

            Assert.That(
                coordinator.PendingOutcomeInstanceId,
                Is.Null,
                "未公開の対象個体も残しません。");

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    coordinator.GetPlayerOutcome("p" + i),
                    Is.EqualTo(BattleSlotOutcome.None),
                    "REMATCHで公開済みの結果も消えます。");
            }

            RefreshAll(tray, coordinator);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(tray.Slots[i].Outcome, Is.EqualTo(BattleSlotOutcome.None));
                Assert.That(tray.Slots[i].State, Is.EqualTo(BattleSlotState.Available));
            }
        }

        // ---------------- 演出中断 ----------------

        [Test]
        public void AbortedPresentation_LeavesNoHalfShownRound()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTray();

            coordinator.SelectPlayerUnit("p2");
            coordinator.Deploy();

            // OnDisable / HOME遷移 の経路。
            Assert.That(coordinator.AbortPresentation(), Is.True);

            Assert.That(
                coordinator.HasPendingOutcome,
                Is.False,
                "出したのに結果が分からない個体を残しません。");

            Assert.That(
                coordinator.GetPlayerOutcome("p2"),
                Is.EqualTo(BattleSlotOutcome.Win),
                "中断でも、解決済みラウンドの結果は確定させます。");

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            RefreshAll(tray, coordinator);

            Assert.That(SlotOf(tray, "p2").Outcome, Is.EqualTo(BattleSlotOutcome.Win));
            Assert.That(SlotOf(tray, "p2").State, Is.EqualTo(BattleSlotState.Used));
        }

        [Test]
        public void AbortWhileSelecting_ChangesNothing()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            Assert.That(coordinator.AbortPresentation(), Is.False);
            Assert.That(coordinator.HasPendingOutcome, Is.False);
            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.Selecting));
        }

        // ---------------- 渡す口の形 ----------------

        [Test]
        public void TheTrayOnlyEverSeesThePublishedLedger()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins();
            coordinator.Begin();

            Assert.That(
                coordinator.Outcomes,
                Is.SameAs(coordinator.PresentedOutcomes),
                "トレイへ渡す口は公開済みの控えだけです。");

            coordinator.SelectPlayerUnit("p0");
            coordinator.Deploy();

            Assert.That(
                coordinator.Outcomes.GetOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.None),
                "未公開の結果は、この口からは引けません。");
        }
    }
}
