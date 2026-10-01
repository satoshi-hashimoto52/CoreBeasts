using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 自軍トレイの勝敗バッジ。
    ///
    /// 勝敗はここでは決めません。<see cref="RoundResult.Winner"/>を言い換えるだけであること、
    /// 個体IDで正しい枠へ届くこと、暗転しても読める明るさのままであることを確かめます。
    /// </summary>
    public sealed class BattleOutcomeBadgeTests
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
        private static BattleFlowCoordinator CreatePlayerWins(
            out FakeBattleMatchSource source)
        {
            source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Green, 50),
                new FirstAvailableSelector());

            return new BattleFlowCoordinator(source);
        }

        /// <summary>CPUが必ず属性勝ちする組み合わせ（Red が Green に勝つ）。</summary>
        private static BattleFlowCoordinator CreateCpuWins(
            out FakeBattleMatchSource source)
        {
            source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Green, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector());

            return new BattleFlowCoordinator(source);
        }

        /// <summary>毎ラウンド必ず引き分けになる組み合わせ。</summary>
        private static BattleFlowCoordinator CreateAlwaysDraw(
            out FakeBattleMatchSource source)
        {
            source = new FakeBattleMatchSource(
                TestBattleSquads.Uniform("p", UnitAttribute.Red, 50),
                TestBattleSquads.Uniform("c", UnitAttribute.Red, 50),
                new FirstAvailableSelector());

            return new BattleFlowCoordinator(source);
        }

        /// <summary>
        /// 指定の個体で1ラウンドを最後まで進めます。
        /// 実機と同じ順序にするため、演出と結果表示の完了を表す
        /// <see cref="BattleFlowCoordinator.PublishPendingOutcome"/>まで通します。
        /// </summary>
        private static void PlayRound(
            BattleFlowCoordinator coordinator,
            string playerInstanceId)
        {
            coordinator.SelectPlayerUnit(playerInstanceId);
            coordinator.Deploy();
            coordinator.CompleteResolve();
            coordinator.PublishPendingOutcome();
            coordinator.AdvanceToNextRound();
        }

        /// <summary>まだ使っていない先頭の個体で1ラウンド進めます。</summary>
        private static void PlayRound(BattleFlowCoordinator coordinator)
        {
            PlayRound(
                coordinator,
                coordinator.Session.PlayerAvailableUnits[0].InstanceId);
        }

        /// <summary>
        /// 進行役の編成と同じIDでトレイを組み立てます。
        /// 中核側は "p0"〜"p6"、表示側も同じIDになります。
        /// </summary>
        private BattleSquadTrayView BuildTrayFor(
            BattleFlowCoordinator coordinator,
            out BattleSideRoster playerSide)
        {
            playerSide = cards.CreateSide("p");

            BattleSquadTrayView tray = views.CreateTray(out _);
            tray.Build(new SilentTrayListener(), uiText);
            tray.Show(playerSide, palette, uiText);

            return tray;
        }

        private static void Refresh(
            BattleSquadTrayView tray,
            BattleFlowCoordinator coordinator)
        {
            tray.RefreshStates(
                coordinator.Session,
                coordinator.SelectedPlayerInstanceId,
                coordinator.Outcomes);
        }

        // ---------------- 結果の控え（純粋部分） ----------------

        [Test]
        public void Ledger_ReportsNoneBeforeAnyRound()
        {
            BattleOutcomeLedger ledger = new BattleOutcomeLedger();

            Assert.That(ledger.Count, Is.EqualTo(0));
            Assert.That(
                ledger.GetOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.None),
                "未戦闘の個体に結果はありません。");

            Assert.That(ledger.GetOutcome(null), Is.EqualTo(BattleSlotOutcome.None));
            Assert.That(ledger.GetOutcome(string.Empty), Is.EqualTo(BattleSlotOutcome.None));
        }

        [TestCase(RoundWinner.Player, BattleSlotOutcome.Win)]
        [TestCase(RoundWinner.Cpu, BattleSlotOutcome.Loss)]
        [TestCase(RoundWinner.Draw, BattleSlotOutcome.Draw)]
        public void Ledger_TranslatesTheRoundWinnerWithoutRejudging(
            RoundWinner winner,
            BattleSlotOutcome expected)
        {
            BattleOutcomeLedger ledger = new BattleOutcomeLedger();

            // 属性もPOWERも同じ組にして、勝者だけを外から与えます。
            // 再計算しているなら、この結果にはなりません。
            RoundResult result = new RoundResult(
                1,
                new BattleUnit("p0", UnitAttribute.Red, 50),
                new BattleUnit("c0", UnitAttribute.Red, 50),
                winner,
                RoundDecision.PowerComparison);

            Assert.That(ledger.Record(result), Is.True);
            Assert.That(ledger.GetOutcome("p0"), Is.EqualTo(expected));
        }

        [Test]
        public void Ledger_KeysOnThePlayerUnitOnly()
        {
            BattleOutcomeLedger ledger = new BattleOutcomeLedger();

            ledger.Record(new RoundResult(
                1,
                new BattleUnit("p3", UnitAttribute.Red, 50),
                new BattleUnit("c3", UnitAttribute.Green, 50),
                RoundWinner.Player,
                RoundDecision.AttributeAdvantage));

            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.That(ledger.GetOutcome("p3"), Is.EqualTo(BattleSlotOutcome.Win));
            Assert.That(
                ledger.GetOutcome("c3"),
                Is.EqualTo(BattleSlotOutcome.None),
                "CPU側にはバッジを付けません。");
        }

        [Test]
        public void Symbols_AreAlwaysShownAsLetters()
        {
            Assert.That(BattleSlotOutcomes.Symbol(BattleSlotOutcome.Win), Is.EqualTo("W"));
            Assert.That(BattleSlotOutcomes.Symbol(BattleSlotOutcome.Loss), Is.EqualTo("L"));
            Assert.That(BattleSlotOutcomes.Symbol(BattleSlotOutcome.Draw), Is.EqualTo("D"));
            Assert.That(
                BattleSlotOutcomes.Symbol(BattleSlotOutcome.None),
                Is.Empty,
                "未戦闘は文字も出しません。");
        }

        // ---------------- 進行役からの控え ----------------

        [Test]
        public void Coordinator_RecordsWinForTheDeployedUnitOnly()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            PlayRound(coordinator, "p2");

            Assert.That(
                coordinator.GetPlayerOutcome("p2"),
                Is.EqualTo(BattleSlotOutcome.Win));

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                if (i == 2)
                {
                    continue;
                }

                Assert.That(
                    coordinator.GetPlayerOutcome("p" + i),
                    Is.EqualTo(BattleSlotOutcome.None),
                    "出していない個体のバッジを書き換えてはいけません。");
            }
        }

        [Test]
        public void Coordinator_RecordsLossWhenTheCpuWins()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins(out _);
            coordinator.Begin();

            PlayRound(coordinator, "p0");

            Assert.That(coordinator.LastResult.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Loss));
            Assert.That(
                coordinator.GetPlayerOutcome("p1"),
                Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void Coordinator_RecordsDrawWhenTheRoundIsDrawn()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            PlayRound(coordinator, "p0");

            Assert.That(coordinator.LastResult.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Draw));
        }

        [Test]
        public void Coordinator_ShowsNothingWhileASelectionIsStillUnresolved()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            coordinator.SelectPlayerUnit("p4");

            Assert.That(coordinator.HasSelection, Is.True);
            Assert.That(
                coordinator.GetPlayerOutcome("p4"),
                Is.EqualTo(BattleSlotOutcome.None),
                "選択しただけでは結果になりません。");
        }

        [Test]
        public void Coordinator_KeepsEarlierOutcomesInLaterRounds()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            PlayRound(coordinator, "p0");
            PlayRound(coordinator, "p1");

            Assert.That(coordinator.CurrentRound, Is.EqualTo(3));
            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Win),
                "前のラウンドのバッジは消えません。");
            Assert.That(
                coordinator.GetPlayerOutcome("p1"),
                Is.EqualTo(BattleSlotOutcome.Win));
        }

        [Test]
        public void Coordinator_GivesEverySevenUnitAnOutcome()
        {
            // 4勝でマッチが決まると7ラウンド戦えないため、全ラウンド引き分けにします。
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(coordinator.State, Is.EqualTo(BattleUiState.MatchFinished));

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    coordinator.GetPlayerOutcome("p" + i),
                    Is.Not.EqualTo(BattleSlotOutcome.None),
                    "7ラウンド終えたら、7体すべてに結果が付きます。");
            }
        }

        [Test]
        public void Rematch_ClearsEveryOutcome()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                PlayRound(coordinator);
            }

            Assert.That(
                coordinator.GetPlayerOutcome("p0"),
                Is.EqualTo(BattleSlotOutcome.Draw),
                "消える前に、確かにバッジが付いていることを確かめます。");

            Assert.That(coordinator.Rematch(), Is.True);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                Assert.That(
                    coordinator.GetPlayerOutcome("p" + i),
                    Is.EqualTo(BattleSlotOutcome.None),
                    "REMATCHでバッジはすべて消えます。");
            }
        }

        // ---------------- 枠の見た目 ----------------

        [Test]
        public void Slot_HidesTheBadgeUntilTheUnitHasFought()
        {
            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(cards.CreateCards(1, "solo_")[0], palette, uiText);

            Assert.That(slot.Outcome, Is.EqualTo(BattleSlotOutcome.None));
            Assert.That(parts.BadgeRoot.activeSelf, Is.False);

            // 選択中でも、解決していないうちはバッジを出しません。
            slot.SetState(BattleSlotState.Selected);

            Assert.That(parts.BadgeRoot.activeSelf, Is.False);
        }

        [Test]
        public void Slot_ShowsADistinctLetterAndColorForEachOutcome()
        {
            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(cards.CreateCards(1, "solo_")[0], palette, uiText);

            slot.SetOutcome(BattleSlotOutcome.Win);

            Assert.That(parts.BadgeRoot.activeSelf, Is.True);
            Assert.That(parts.BadgeLabel.text, Is.EqualTo("W"));
            Assert.That(parts.BadgeLabel.color, Is.EqualTo(Color.white));

            Color win = parts.BadgeImage.color;

            slot.SetOutcome(BattleSlotOutcome.Loss);

            Assert.That(parts.BadgeLabel.text, Is.EqualTo("L"));

            Color loss = parts.BadgeImage.color;

            slot.SetOutcome(BattleSlotOutcome.Draw);

            Assert.That(parts.BadgeLabel.text, Is.EqualTo("D"));

            Color draw = parts.BadgeImage.color;

            Assert.That(win, Is.Not.EqualTo(loss));
            Assert.That(loss, Is.Not.EqualTo(draw));
            Assert.That(win, Is.Not.EqualTo(draw));

            // 勝ちは緑寄り、負けは赤寄り、引き分けは無彩色寄りです。
            Assert.That(win.g, Is.GreaterThan(win.r));
            Assert.That(loss.r, Is.GreaterThan(loss.g));
            Assert.That(Mathf.Abs(draw.r - draw.b), Is.LessThan(0.2f));

            slot.SetOutcome(BattleSlotOutcome.None);

            Assert.That(parts.BadgeRoot.activeSelf, Is.False);
        }

        [Test]
        public void Slot_KeepsTheBadgeBrightWhileTheUsedCardIsDimmed()
        {
            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(cards.CreateCards(1, "solo_")[0], palette, uiText);

            slot.SetOutcome(BattleSlotOutcome.Win);
            slot.SetState(BattleSlotState.Used);

            Assert.That(
                parts.CardGroup.alpha,
                Is.LessThan(1f),
                "使用済みカード本体は、これまでどおり暗転します。");

            Assert.That(
                parts.BadgeGroup.ignoreParentGroups,
                Is.True,
                "バッジはカードのCanvasGroupの影響を受けません。");

            Assert.That(parts.BadgeGroup.alpha, Is.EqualTo(1f));
            Assert.That(parts.BadgeRoot.activeSelf, Is.True);
        }

        [Test]
        public void Slot_PutsTheBadgeInFrontOfTheCardVisuals()
        {
            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            // 組み立て時点ではバッジが先頭の子です。Bindで最前面へ移します。
            Assert.That(parts.BadgeRoot.transform.GetSiblingIndex(), Is.EqualTo(0));

            slot.Bind(0, new SilentTrayListener(), uiText);

            Assert.That(
                parts.BadgeRoot.transform.parent,
                Is.SameAs(parts.LiftRoot),
                "バッジは見た目の根の下に置きます。");

            Assert.That(
                parts.BadgeRoot.transform.GetSiblingIndex(),
                Is.EqualTo(parts.LiftRoot.childCount - 1),
                "バッジは見た目の根の最前面です。");
        }

        [Test]
        public void Slot_KeepsTheSelectionFrameUsedLabelAndAttributeChip()
        {
            BattleTraySlotView slot =
                views.CreateTraySlot(out TestBattleViews.TraySlotParts parts);

            slot.Bind(0, new SilentTrayListener(), uiText);
            slot.Show(cards.CreateCards(1, "solo_")[0], palette, uiText);

            slot.SetState(BattleSlotState.Selected);

            Color selectedFrame = parts.Frame.color;
            Color chip = parts.AttributeChip.color;
            string order = parts.OrderLabel.text;

            slot.SetOutcome(BattleSlotOutcome.Win);

            Assert.That(parts.Frame.color, Is.EqualTo(selectedFrame), "選択枠は変わりません。");
            Assert.That(parts.AttributeChip.enabled, Is.True, "属性バーは残ります。");
            Assert.That(parts.AttributeChip.color, Is.EqualTo(chip));
            Assert.That(parts.OrderLabel.text, Is.EqualTo(order), "カード番号は残ります。");

            slot.SetState(BattleSlotState.Used);

            Assert.That(parts.UsedLabel.gameObject.activeSelf, Is.True, "USED表示は残ります。");

            // アンバーの選択枠と、緑の勝利バッジは別の色です。
            Assert.That(parts.BadgeImage.color, Is.Not.EqualTo(selectedFrame));
        }

        // ---------------- トレイ全体 ----------------

        [Test]
        public void Tray_ShowsTheBadgeOnlyForTheUnitThatFought()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTrayFor(coordinator, out BattleSideRoster side);

            Refresh(tray, coordinator);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].Outcome,
                    Is.EqualTo(BattleSlotOutcome.None),
                    "1ラウンドも終わっていないので、バッジは1つも出ません。");
            }

            PlayRound(coordinator, side.Cards[3].InstanceId);
            Refresh(tray, coordinator);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].Outcome,
                    Is.EqualTo(i == 3 ? BattleSlotOutcome.Win : BattleSlotOutcome.None),
                    "出した1体だけにバッジが付きます。");
            }
        }

        [Test]
        public void Tray_MatchesOutcomesByInstanceIdNotBySlotOrder()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            BattleSideRoster side = cards.CreateSide("p");

            // 並びを逆にしても、IDで引くので取り違えません。
            List<BattleUnitCard> reversed = new List<BattleUnitCard>(side.Cards);
            reversed.Reverse();

            BattleSideRoster.TryCreate(
                reversed, out BattleSideRoster flipped, out BattleError _);

            BattleSquadTrayView tray = views.CreateTray(out _);
            tray.Build(new SilentTrayListener(), uiText);
            tray.Show(flipped, palette, uiText);

            PlayRound(coordinator, "p0");
            Refresh(tray, coordinator);

            int last = tray.Slots.Count - 1;

            Assert.That(tray.Slots[last].Card.InstanceId, Is.EqualTo("p0"));
            Assert.That(tray.Slots[last].Outcome, Is.EqualTo(BattleSlotOutcome.Win));
            Assert.That(tray.Slots[0].Card.InstanceId, Is.EqualTo("p6"));
            Assert.That(tray.Slots[0].Outcome, Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void Tray_KeepsOutcomesInLaterRounds()
        {
            BattleFlowCoordinator coordinator = CreateCpuWins(out _);
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTrayFor(coordinator, out BattleSideRoster side);

            PlayRound(coordinator, side.Cards[0].InstanceId);
            Refresh(tray, coordinator);

            PlayRound(coordinator, side.Cards[1].InstanceId);
            Refresh(tray, coordinator);

            Assert.That(tray.Slots[0].Outcome, Is.EqualTo(BattleSlotOutcome.Loss));
            Assert.That(tray.Slots[1].Outcome, Is.EqualTo(BattleSlotOutcome.Loss));
            Assert.That(tray.Slots[0].State, Is.EqualTo(BattleSlotState.Used));
            Assert.That(tray.Slots[2].Outcome, Is.EqualTo(BattleSlotOutcome.None));
        }

        [Test]
        public void Tray_ShowsAllSevenOutcomesWhenTheMatchEnds()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTrayFor(coordinator, out _);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                PlayRound(coordinator);
            }

            Refresh(tray, coordinator);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].Outcome,
                    Is.EqualTo(BattleSlotOutcome.Draw),
                    "7ラウンド終了後は、7体すべてに結果が出ます。");
            }
        }

        [Test]
        public void Tray_ClearsEveryBadgeOnRematch()
        {
            BattleFlowCoordinator coordinator = CreateAlwaysDraw(out _);
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTrayFor(coordinator, out _);

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                PlayRound(coordinator);
            }

            Refresh(tray, coordinator);
            coordinator.Rematch();
            Refresh(tray, coordinator);

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(
                    tray.Slots[i].Outcome,
                    Is.EqualTo(BattleSlotOutcome.None),
                    "REMATCHのあとに古い結果が残ってはいけません。");

                Assert.That(
                    tray.Slots[i].State,
                    Is.EqualTo(BattleSlotState.Available));
            }
        }

        /// <summary>タップを受け流すだけのテスト用の受け手。</summary>
        private sealed class SilentTrayListener : IBattleTrayListener
        {
            public void OnTraySlotTapped(string instanceId)
            {
            }
        }

        [Test]
        public void Tray_ShowsNoBadgeWithoutAnOutcomeSource()
        {
            BattleFlowCoordinator coordinator = CreatePlayerWins(out _);
            coordinator.Begin();

            BattleSquadTrayView tray = BuildTrayFor(coordinator, out BattleSideRoster side);

            PlayRound(coordinator, side.Cards[0].InstanceId);

            // 結果の出どころを渡さない既存の呼び出しでは、バッジは出ません。
            tray.RefreshStates(coordinator.Session, null);

            Assert.That(tray.Slots[0].Outcome, Is.EqualTo(BattleSlotOutcome.None));
            Assert.That(
                tray.Slots[0].State,
                Is.EqualTo(BattleSlotState.Used),
                "使用済みの表示は従来どおりです。");
        }
    }
}
