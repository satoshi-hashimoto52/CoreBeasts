using System.Collections;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// バトル画面の表示コンポーネント。
    /// 確定済みの値を受け取って見た目へ写すだけであること、
    /// CPU側の枠へ個体が渡らないことを確かめます。
    /// </summary>
    public sealed class BattleUiViewTests
    {
        private TestBattleViews views;
        private TestBattleCards cards;
        private FakeBattleText text;
        private AttributePalette palette;
        private UiTextCatalog uiText;

        [SetUp]
        public void SetUp()
        {
            views = new TestBattleViews();
            cards = new TestBattleCards();
            text = new FakeBattleText();

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

        /// <summary>1ラウンド解決済みのセッションを作ります。</summary>
        private static BattleSession CreateSession(
            BattleSideRoster playerSide,
            BattleSideRoster cpuSide)
        {
            return new BattleSession(
                playerSide.Squad,
                cpuSide.Squad,
                new FirstAvailableSelector());
        }

        // ---------------- プレイヤートレイ ----------------

        [Test]
        public void Tray_BuildsSevenSlots()
        {
            BattleSquadTrayView tray = views.CreateTray(out _);
            tray.Build(new RecordingTrayListener(), uiText);

            Assert.That(tray.Slots.Count, Is.EqualTo(BattleSquad.UnitCount));

            for (int i = 0; i < tray.Slots.Count; i++)
            {
                Assert.That(tray.Slots[i].SlotIndex, Is.EqualTo(i));
                Assert.That(tray.Slots[i].HasRequiredReferences(), Is.True);
            }
        }

        [Test]
        public void Tray_ShowsUnusedSelectedAndUsedStates()
        {
            BattleSideRoster playerSide = cards.CreateSide("p_");
            BattleSideRoster cpuSide = cards.CreateSide("c_");
            BattleSession session = CreateSession(playerSide, cpuSide);

            BattleSquadTrayView tray = views.CreateTray(out _);
            tray.Build(new RecordingTrayListener(), uiText);
            tray.Show(playerSide, palette, uiText);

            // 1ラウンド解決し、先頭の個体を使用済みにします。
            string usedId = playerSide.Cards[0].InstanceId;
            session.SelectPlayerUnit(usedId);
            session.SelectCpuUnit();
            session.TryResolveRound(out RoundResult _, out BattleError _);

            string selectedId = playerSide.Cards[2].InstanceId;
            tray.RefreshStates(session, selectedId);

            Assert.That(tray.Slots[0].State, Is.EqualTo(BattleSlotState.Used));
            Assert.That(tray.Slots[1].State, Is.EqualTo(BattleSlotState.Available));
            Assert.That(tray.Slots[2].State, Is.EqualTo(BattleSlotState.Selected));
        }

        [Test]
        public void TraySlot_DoesNotReportTapsWhileUsed()
        {
            RecordingTrayListener listener = new RecordingTrayListener();

            BattleTraySlotView slot = views.CreateTraySlot();
            slot.Bind(0, listener, uiText);
            slot.Show(cards.CreateCards(1, "solo_")[0], palette, uiText);

            slot.SetState(BattleSlotState.Available);
            slot.OnPointerClick(null);

            Assert.That(listener.Taps, Is.EqualTo(new List<string> { "solo_0" }));

            slot.SetState(BattleSlotState.Used);
            slot.OnPointerClick(null);

            Assert.That(
                listener.Taps.Count,
                Is.EqualTo(1),
                "使用済みの枠はタップを通しません。");
        }

        [Test]
        public void TraySlot_IgnoresTapsOnAnEmptySlot()
        {
            RecordingTrayListener listener = new RecordingTrayListener();

            BattleTraySlotView slot = views.CreateTraySlot();
            slot.Bind(0, listener, uiText);
            slot.Show(null, palette, uiText);
            slot.OnPointerClick(null);

            Assert.That(listener.Taps, Is.Empty);
        }

        // ---------------- CPU側の非公開枠 ----------------

        [Test]
        public void EnemyStatus_BuildsSevenHiddenMarkers()
        {
            EnemySquadStatusView status = views.CreateEnemyStatus(out TMP_Text remaining);
            status.Build(text);

            Assert.That(status.Markers.Count, Is.EqualTo(BattleSquad.UnitCount));
            Assert.That(remaining.text, Is.EqualTo("LEFT 7 / 7"));

            for (int i = 0; i < status.Markers.Count; i++)
            {
                Assert.That(status.Markers[i].State, Is.EqualTo(EnemyMarkerState.Unused));
                Assert.That(status.Markers[i].HasRequiredReferences(), Is.True);
            }
        }

        [Test]
        public void EnemyStatus_ShowsUsedCountAndOnePendingSlot()
        {
            EnemySquadStatusView status = views.CreateEnemyStatus(out TMP_Text remaining);
            status.Build(text);

            status.Refresh(2, true);

            Assert.That(status.Markers[0].State, Is.EqualTo(EnemyMarkerState.Used));
            Assert.That(status.Markers[1].State, Is.EqualTo(EnemyMarkerState.Used));
            Assert.That(status.Markers[2].State, Is.EqualTo(EnemyMarkerState.Pending));
            Assert.That(status.Markers[3].State, Is.EqualTo(EnemyMarkerState.Unused));
            Assert.That(remaining.text, Is.EqualTo("LEFT 5 / 7"));
        }

        [Test]
        public void EnemyStatus_NeverAcceptsUnitData()
        {
            // Refresh の引数は数と真偽値だけです。個体を渡す口がありません。
            System.Reflection.MethodInfo refresh =
                typeof(EnemySquadStatusView).GetMethod(nameof(EnemySquadStatusView.Refresh));

            System.Reflection.ParameterInfo[] parameters = refresh.GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(bool)));
        }

        [Test]
        public void EnemyMarker_ShowsNoIdentifyingTextBeforeReveal()
        {
            EnemySquadStatusView status = views.CreateEnemyStatus(out _);
            status.Build(text);
            status.Refresh(0, true);

            BattleSideRoster cpuSide = cards.CreateSide("c_");

            for (int i = 0; i < status.Markers.Count; i++)
            {
                string label = status.Markers[i].GetComponentInChildren<TMP_Text>().text;

                for (int c = 0; c < cpuSide.Cards.Count; c++)
                {
                    Assert.That(
                        label,
                        Does.Not.Contain(cpuSide.Cards[c].Definition.DisplayName),
                        "CPU枠に個体名が出てはいけません。");
                }

                Assert.That(
                    label == "?" || label == "READY" || label == "USED",
                    Is.True,
                    "CPU枠に出せるのは状態を示す記号だけです。");
            }
        }

        // ---------------- 出場中の表示 ----------------

        [Test]
        public void Combatant_HidesEverythingBeforeReveal()
        {
            BattleCombatantView combatant =
                views.CreateCombatant(out TestBattleViews.CombatantParts parts);

            combatant.Bind(palette, uiText, text);
            combatant.ShowHidden();

            Assert.That(combatant.IsRevealed, Is.False);
            Assert.That(combatant.Current, Is.Null);
            Assert.That(parts.InfoRoot.activeSelf, Is.False);
            Assert.That(parts.HiddenRoot.activeSelf, Is.True);
            Assert.That(parts.HiddenLabel.text, Is.EqualTo("READY"));

            // ShowHidden は infoRoot を隠すだけで nameLabel へ書き込みません。
            // テスト側の TMP_Text も生成直後で text へ一度も代入していないため、
            // ここでの実測値は null です。「名前が表示されていない」という意味では
            // null と空文字は同義なので、どちらも許容します。
            // （Is.Empty は null を渡すと ArgumentException になります）
            Assert.That(
                string.IsNullOrEmpty(parts.Name.text),
                Is.True,
                "Reveal前の個体名ラベルには何も表示されていてはいけません。");
        }

        /// <summary>色タグを外した素の文字列。</summary>
        private static string StripRichText(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                value ?? string.Empty, "<.*?>", string.Empty);
        }

        [Test]
        public void Combatant_ShowsNameLevelAttributePowerCoreAndSkillOnReveal()
        {
            BattleCombatantView combatant =
                views.CreateCombatant(out TestBattleViews.CombatantParts parts);

            combatant.Bind(palette, uiText, text);

            CoreBeastDefinition definition =
                cards.CreateDefinition("volx", UnitAttribute.Red, 76);

            BattleUnitCard.TryCreate("p0", 18, definition, out BattleUnitCard card);

            combatant.Show(card);

            Assert.That(combatant.IsRevealed, Is.True);
            Assert.That(combatant.Current, Is.SameAs(card));
            Assert.That(parts.InfoRoot.activeSelf, Is.True);
            Assert.That(parts.HiddenRoot.activeSelf, Is.False);
            Assert.That(parts.Name.text, Is.EqualTo("VOLX"));
            Assert.That(parts.Level.text, Is.EqualTo("Lv.18"));
            // POWER / CORE の常設ラベルは廃止しました。
            // 数値だけを、それぞれの色で出します。
            Assert.That(
                StripRichText(parts.Power.text),
                Is.EqualTo("76"),
                "属性POWERは数値だけを出します。");

            Assert.That(
                parts.Power.text,
                Is.EqualTo(StatLinePresenter.Wrap(
                    "76", StatLinePresenter.ResolveColor(UnitAttribute.Red, palette))),
                "属性POWERがその属性色で出ていません。");

            Assert.That(parts.Power.text, Does.Not.Contain("POWER"));

            Assert.That(
                StripRichText(parts.Core.text),
                Is.EqualTo("38"),
                "CORE は数値だけを出します。");

            Assert.That(
                parts.Core.text,
                Is.EqualTo(StatLinePresenter.BuildCoreValue(38)),
                "CORE がシルバーグレーで出ていません。");

            Assert.That(parts.Core.text, Does.Not.Contain("CORE"));

            // 属性は名前だけ。記号との重複（R RED）を出しません。
            Assert.That(parts.Attribute.text, Is.EqualTo("RED"));
            Assert.That(parts.SkillName.text, Is.EqualTo("SKILL volx"));
            Assert.That(parts.SkillDescription.text, Does.Contain("volx"));
        }

        [Test]
        public void Combatant_ShowsBothAttributesForDualUnits()
        {
            BattleCombatantView combatant =
                views.CreateCombatant(out TestBattleViews.CombatantParts parts);

            combatant.Bind(palette, uiText, text);

            CoreBeastDefinition definition = cards.CreateDefinition(
                "dual", UnitAttribute.Red, 50, true, UnitAttribute.Blue);

            BattleUnitCard.TryCreate("p0", 1, definition, out BattleUnitCard card);

            combatant.Show(card);

            // 2色でも記号との重複（R/B RED / BLUE）を出しません。
            Assert.That(parts.Attribute.text, Is.EqualTo("RED / BLUE"));

            // 色別POWERは2つ並び、スラッシュは1つだけです。
            Assert.That(StripRichText(parts.Power.text), Is.EqualTo("50 / 50"));
        }

        [Test]
        public void Combatant_TextIsNeverMirrored()
        {
            BattleCombatantView combatant =
                views.CreateCombatant(out TestBattleViews.CombatantParts parts);

            // ミラーは立ち絵のRectTransformにだけ掛けます。
            parts.PortraitRoot.localScale = new Vector3(-1f, 1f, 1f);

            Assert.That(parts.Name.transform.lossyScale.x, Is.GreaterThan(0f));
            Assert.That(parts.Attribute.transform.lossyScale.x, Is.GreaterThan(0f));
        }

        // ---------------- スコア ----------------

        [Test]
        public void Score_ShowsRoundAndWins()
        {
            BattleScoreView view = views.CreateScoreView(
                out TMP_Text round, out TMP_Text score);

            view.Bind(text);
            view.Refresh(3, 7, 2, 1);

            Assert.That(round.text, Is.EqualTo("ROUND 3 / 7"));
            Assert.That(score.text, Is.EqualTo("PLAYER 2  -  1 CPU"));
            Assert.That(view.HasRequiredReferences(), Is.True);
        }

        [Test]
        public void Score_ClampsTheRoundNumberAfterTheLastRound()
        {
            BattleScoreView view = views.CreateScoreView(out TMP_Text round, out _);

            view.Bind(text);
            view.Refresh(8, 7, 4, 3);

            Assert.That(round.text, Is.EqualTo("ROUND 7 / 7"));
        }

        // ---------------- 結果表示 ----------------

        [Test]
        public void Result_ShowsDecisionWinnerMatchupAndScore()
        {
            BattleResultView view =
                views.CreateResultView(out TestBattleViews.ResultLabels labels);

            view.Bind(text, uiText);

            BattleSideRoster playerSide = cards.CreateSide("p_");
            BattleSideRoster cpuSide = cards.CreateSide("c_");

            RoundResult result = new RoundResult(
                1,
                playerSide.Cards[0].Unit,
                cpuSide.Cards[1].Unit,
                RoundWinner.Player,
                RoundDecision.AttributeAdvantage);

            view.ShowRound(result, playerSide.Cards[0], cpuSide.Cards[1], 1, 0);

            Assert.That(view.IsBannerVisible, Is.True);
            Assert.That(labels.Decision.text, Is.EqualTo("ATTRIBUTE WIN"));
            Assert.That(labels.Winner.text, Is.EqualTo("PLAYER WIN"));
            Assert.That(labels.BannerScore.text, Is.EqualTo("PLAYER 1  -  0 CPU"));

            Assert.That(labels.Matchup.text, Does.Contain("VS"));
            Assert.That(
                labels.Matchup.text,
                Does.Contain(playerSide.Cards[0].Definition.DisplayName));
            Assert.That(
                labels.Matchup.text,
                Does.Contain(cpuSide.Cards[1].Definition.DisplayName));
            Assert.That(
                labels.Matchup.text,
                Does.Contain(playerSide.Cards[0].Unit.PowerOf(
                    playerSide.Cards[0].Unit.PrimaryAttribute).ToString()));
            Assert.That(
                labels.Matchup.text,
                Does.Contain(cpuSide.Cards[1].Unit.PowerOf(
                    cpuSide.Cards[1].Unit.PrimaryAttribute).ToString()));
        }

        [Test]
        public void Result_ShowsDrawWithoutAWinner()
        {
            BattleResultView view =
                views.CreateResultView(out TestBattleViews.ResultLabels labels);

            view.Bind(text, uiText);

            BattleSideRoster playerSide = cards.CreateSide("p_");
            BattleSideRoster cpuSide = cards.CreateSide("c_");

            RoundResult result = new RoundResult(
                1,
                playerSide.Cards[0].Unit,
                cpuSide.Cards[0].Unit,
                RoundWinner.Draw,
                RoundDecision.PowerComparison);

            view.ShowRound(result, playerSide.Cards[0], cpuSide.Cards[0], 0, 0);

            Assert.That(labels.Decision.text, Is.EqualTo("DRAW"));
            Assert.That(labels.Winner.text, Is.EqualTo("DRAW"));
        }

        [Test]
        public void Result_BannerStartsTransparentSoItCanFadeIn()
        {
            BattleResultView view = views.CreateResultView(out _);
            view.Bind(text, uiText);

            BattleSideRoster playerSide = cards.CreateSide("p_");
            BattleSideRoster cpuSide = cards.CreateSide("c_");

            view.ShowRound(
                new RoundResult(
                    1,
                    playerSide.Cards[0].Unit,
                    cpuSide.Cards[0].Unit,
                    RoundWinner.Cpu,
                    RoundDecision.PowerComparison),
                playerSide.Cards[0],
                cpuSide.Cards[0],
                0,
                1);

            Assert.That(view.BannerGroup.alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Result_ShowsTheFinalOutcome()
        {
            BattleResultView view =
                views.CreateResultView(out TestBattleViews.ResultLabels labels);

            view.Bind(text, uiText);

            Assert.That(view.IsFinalVisible, Is.False);

            view.ShowFinal(BattleMatchState.CpuWin, 2, 4);

            Assert.That(view.IsFinalVisible, Is.True);
            Assert.That(labels.FinalTitle.text, Is.EqualTo("CPU WIN"));
            Assert.That(labels.FinalScore.text, Is.EqualTo("PLAYER 2  -  4 CPU"));

            view.HideAll();

            Assert.That(view.IsFinalVisible, Is.False);
            Assert.That(view.IsBannerVisible, Is.False);
        }

        [Test]
        public void Result_ShowsTheEarnedCoreCoinWithoutGrowingPerMatch()
        {
            BattleResultView view =
                views.CreateResultView(out TestBattleViews.ResultLabels labels);

            view.Bind(text, uiText);
            Transform rewardTransform = labels.FinalRoot.transform.Find("FinalRewardLabel");

            Assert.That(rewardTransform, Is.Not.Null);
            TMP_Text reward = rewardTransform.GetComponent<TMP_Text>();

            view.SetMatchReward(30);
            Assert.That(reward.text, Is.EqualTo("+30 CORE COIN"));

            view.SetMatchReward(10);
            Assert.That(reward.text, Is.EqualTo("+10 CORE COIN"));
            Assert.That(labels.FinalRoot.transform.childCount, Is.EqualTo(3));

            view.HideAll();
            Assert.That(reward.text, Is.Empty);
        }

        [Test]
        public void Result_IsFullyWired()
        {
            BattleResultView view = views.CreateResultView(out _);

            Assert.That(view.HasRequiredReferences(), Is.True);
        }

        // ---------------- 演出 ----------------

        [Test]
        public void Fx_IsOnByDefaultAndFullyWired()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);

            Assert.That(fx.FxEnabled, Is.True);
            Assert.That(fx.HasRequiredReferences(), Is.True);
        }

        [Test]
        public void Fx_Off_SkipsEveryWaitInTheClash()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);
            fx.FxEnabled = false;

            IEnumerator routine = fx.PlayClashRoutine(RoundWinner.Player, Color.white);

            Assert.That(
                routine.MoveNext(),
                Is.False,
                "FX OFF では1フレームも待ちません。");
        }

        [Test]
        public void Fx_On_YieldsWhilePlaying()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);
            fx.FxEnabled = true;

            IEnumerator routine = fx.PlayClashRoutine(RoundWinner.Player, Color.white);

            Assert.That(routine.MoveNext(), Is.True, "FX ON では演出が動きます。");
        }

        [Test]
        public void Fx_Off_HoldsTheBannerForAShorterTime()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);

            fx.FxEnabled = true;
            float withFx = fx.BannerHoldSeconds;

            fx.FxEnabled = false;

            Assert.That(fx.BannerHoldSeconds, Is.LessThan(withFx));
        }

        [Test]
        public void Fx_Off_StillLeavesTheWinnerAndLoserDistinguishable()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out TestBattleViews.FxParts parts);
            fx.FxEnabled = false;

            IEnumerator routine = fx.PlayClashRoutine(RoundWinner.Cpu, Color.white);

            while (routine.MoveNext())
            {
            }

            Assert.That(parts.CpuPortrait.localScale.y, Is.GreaterThan(1f));
            Assert.That(parts.PlayerGroup.alpha, Is.LessThan(1f));
            Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Fx_KeepsMirroredPortraitsMirrored()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out TestBattleViews.FxParts parts);
            fx.FxEnabled = false;

            // CPU側はシーンで左右反転しています。演出で向きが戻ってはいけません。
            parts.CpuPortrait.localScale = new Vector3(-1f, 1f, 1f);

            IEnumerator routine = fx.PlayClashRoutine(RoundWinner.Cpu, Color.white);

            while (routine.MoveNext())
            {
            }

            Assert.That(parts.CpuPortrait.localScale.x, Is.LessThan(0f));
        }

        [Test]
        public void Fx_ResetVisuals_RestoresPositionsAndBrightness()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out TestBattleViews.FxParts parts);

            Vector2 playerRest = parts.PlayerPortrait.anchoredPosition;
            Vector2 cpuRest = parts.CpuPortrait.anchoredPosition;

            fx.FxEnabled = false;

            IEnumerator routine = fx.PlayClashRoutine(RoundWinner.Player, Color.white);

            while (routine.MoveNext())
            {
            }

            fx.ResetVisuals();

            Assert.That(parts.PlayerPortrait.anchoredPosition, Is.EqualTo(playerRest));
            Assert.That(parts.CpuPortrait.anchoredPosition, Is.EqualTo(cpuRest));
            Assert.That(parts.PlayerGroup.alpha, Is.EqualTo(1f));
            Assert.That(parts.CpuGroup.alpha, Is.EqualTo(1f));
            Assert.That(parts.FlashGroup.alpha, Is.EqualTo(0f));
            Assert.That(parts.PlayerPortrait.localScale.y, Is.EqualTo(1f));
        }

        [Test]
        public void Fx_BannerRoutine_IsSafeWithoutACanvasGroup()
        {
            BattleFxPlayer fx = views.CreateFxPlayer(out _);

            IEnumerator routine = fx.ShowBannerRoutine(null);

            Assert.That(routine.MoveNext(), Is.False);
        }

        /// <summary>タップ通知を記録するだけのテスト用の受け手。</summary>
        private sealed class RecordingTrayListener : IBattleTrayListener
        {
            internal List<string> Taps { get; } = new List<string>();

            public void OnTraySlotTapped(string instanceId)
            {
                Taps.Add(instanceId);
            }
        }
    }
}
