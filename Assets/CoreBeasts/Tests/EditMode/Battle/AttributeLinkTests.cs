using System;
using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// Phase 4A「ATTRIBUTE LINK」の規則。
    ///
    /// 純粋関数 <see cref="AttributeLink"/> の判定と、<see cref="BattleSession"/> を通した実ラウンドでの
    /// チェーン・ボーナス・勝敗への反映を固定します。
    /// </summary>
    public sealed class AttributeLinkTests
    {
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute G = UnitAttribute.Green;
        private const UnitAttribute B = UnitAttribute.Blue;

        private static BattleUnit One(string id, UnitAttribute a, int power = 50, int core = 0)
        {
            return new BattleUnit(id, a, power, core);
        }

        private static BattleUnit Two(string id, UnitAttribute a, int pa, UnitAttribute b, int pb, int core = 0)
        {
            return new BattleUnit(id, a, pa, b, pb, core);
        }

        // ---------------- 純粋関数 ----------------

        [Test]
        public void TheFirstUnitIsChainOneWithNoBonus()
        {
            AttributeLinkResult link = AttributeLink.Evaluate(null, One("a", R), 1);

            Assert.That(link.ChainCount, Is.EqualTo(1));
            Assert.That(link.BonusPower, Is.EqualTo(0));
            Assert.That(link.IsActive, Is.False);
            Assert.That(link.SharedAttributeMask, Is.EqualTo(0));
            Assert.That(link, Is.EqualTo(AttributeLinkResult.None));
        }

        [Test]
        public void ASharedSingleColourLinksAndADifferentOneDoesNot()
        {
            AttributeLinkResult same = AttributeLink.Evaluate(One("a", R), One("b", R), 1);
            AttributeLinkResult other = AttributeLink.Evaluate(One("a", R), One("b", G), 1);

            Assert.That(same.ChainCount, Is.EqualTo(2));
            Assert.That(same.BonusPower, Is.EqualTo(3));
            Assert.That(same.IsActive, Is.True);
            Assert.That(same.Shares(R), Is.True);
            Assert.That(same.SharedAttributeCount, Is.EqualTo(1));

            Assert.That(other, Is.EqualTo(AttributeLinkResult.None), "共有属性が無ければチェーン1です。");
        }

        [Test]
        public void ADualUnitLinksWhenEitherColourIsShared()
        {
            AttributeLinkResult viaSecond = AttributeLink.Evaluate(Two("a", R, 40, B, 40), One("b", B), 1);
            AttributeLinkResult viaFirst = AttributeLink.Evaluate(One("a", G), Two("b", G, 40, R, 40), 1);

            Assert.That(viaSecond.ChainCount, Is.EqualTo(2));
            Assert.That(viaSecond.Shares(B), Is.True);
            Assert.That(viaSecond.Shares(R), Is.False);
            Assert.That(viaFirst.ChainCount, Is.EqualTo(2));
            Assert.That(viaFirst.Shares(G), Is.True);
        }

        [Test]
        public void TwoSharedColoursStillAddOnlyOneLink()
        {
            AttributeLinkResult link = AttributeLink.Evaluate(Two("a", R, 40, B, 40), Two("b", R, 30, B, 30), 2);

            Assert.That(link.ChainCount, Is.EqualTo(3), "2色とも一致しても加算は1回です。");
            Assert.That(link.SharedAttributeCount, Is.EqualTo(2));
            Assert.That(link.Shares(R) && link.Shares(B), Is.True);
            Assert.That(link.BonusPower, Is.EqualTo(6));
        }

        [Test]
        public void RedBlueAndBlueRedAreTheSamePair()
        {
            AttributeLinkResult forward = AttributeLink.Evaluate(Two("a", R, 40, B, 40), Two("b", B, 40, R, 40), 1);
            AttributeLinkResult same = AttributeLink.Evaluate(Two("a", R, 40, B, 40), Two("b", R, 40, B, 40), 1);

            Assert.That(forward, Is.EqualTo(same), "並び順は関係ありません。");
            Assert.That(AttributeLink.MaskOf(Two("x", R, 1, B, 1)), Is.EqualTo(AttributeLink.MaskOf(Two("y", B, 1, R, 1))));
        }

        [Test]
        public void TheChainGrowsAndTheBonusCapsAtSix()
        {
            int chain = 1;
            BattleUnit previous = One("u0", R);
            int[] bonuses = new int[6];

            for (int i = 0; i < bonuses.Length; i++)
            {
                BattleUnit current = One("u" + (i + 1), R);
                AttributeLinkResult link = AttributeLink.Evaluate(previous, current, chain);

                Assert.That(link.ChainCount, Is.EqualTo(chain + 1));
                bonuses[i] = link.BonusPower;

                chain = link.ChainCount;
                previous = current;
            }

            Assert.That(bonuses, Is.EqualTo(new[] { 3, 6, 6, 6, 6, 6 }), "x2 で +3、x3 以上は +6 が上限です。");
            Assert.That(chain, Is.EqualTo(7), "実チェーン数は4以上も保持します。");
            Assert.That(AttributeLink.BonusFor(1), Is.EqualTo(0));
            Assert.That(AttributeLink.BonusFor(2), Is.EqualTo(3));
            Assert.That(AttributeLink.BonusFor(3), Is.EqualTo(6));
            Assert.That(AttributeLink.BonusFor(99), Is.EqualTo(6));
        }

        [Test]
        public void ABreakReturnsTheChainToOne()
        {
            AttributeLinkResult broken = AttributeLink.Evaluate(One("a", R), One("b", G), 5);

            Assert.That(broken.ChainCount, Is.EqualTo(1));
            Assert.That(broken.BonusPower, Is.EqualTo(0));
        }

        [Test]
        public void ApplyAddsTheBonusToEveryColourWithoutTouchingTheOriginal()
        {
            BattleUnit original = Two("a", R, 54, B, 30, core: 7);
            AttributeLinkResult link = new AttributeLinkResult(2, 3, AttributeLink.BitOf(R));

            BattleUnit effective = AttributeLink.Apply(original, link);

            Assert.That(effective.PowerOf(R), Is.EqualTo(57), "54 + 3");
            Assert.That(effective.PowerOf(B), Is.EqualTo(33), "30 + 3");
            Assert.That(effective.Core, Is.EqualTo(7), "COREは変えません。");
            Assert.That(effective.InstanceId, Is.EqualTo("a"));
            Assert.That(original.PowerOf(R), Is.EqualTo(54), "元の個体のPOWERは書き換えません。");
            Assert.That(original.PowerOf(B), Is.EqualTo(30));
            Assert.That(AttributeLink.Apply(original, AttributeLinkResult.None), Is.SameAs(original), "ボーナス0なら元の個体のままです。");
        }

        [Test]
        public void OutOfRangeAttributesAreNeverTreatedAsShared()
        {
            Assert.That(AttributeLink.BitOf((UnitAttribute)99), Is.EqualTo(0));
            Assert.That(AttributeLink.BitOf((UnitAttribute)(-1)), Is.EqualTo(0));
            Assert.That(AttributeLink.Evaluate(One("a", (UnitAttribute)99), One("b", (UnitAttribute)99), 1).IsActive, Is.False);
            Assert.Throws<ArgumentNullException>(() => AttributeLink.Evaluate(One("a", R), null, 1));
        }

        // ---------------- 実ラウンド（BattleSession） ----------------

        [Test]
        public void ChainsAreTrackedPerSideAcrossRealRoundsIncludingDraws()
        {
            // PLAYER: R R R G R  /  CPU: R R G G G
            BattleSession session = Session(
                new[] { One("p0", R), One("p1", R), One("p2", R), One("p3", G), One("p4", R), One("p5", G), One("p6", B) },
                new[] { One("c0", R), One("c1", R), One("c2", G), One("c3", G), One("c4", G), One("c5", B), One("c6", B) },
                "c0", "c1", "c2", "c3", "c4");

            int[] playerChains = new int[5];
            int[] cpuChains = new int[5];
            string[] picks = { "p0", "p1", "p2", "p3", "p4" };

            for (int i = 0; i < picks.Length; i++)
            {
                RoundResult r = Play(session, picks[i]);

                playerChains[i] = r.PlayerLink.ChainCount;
                cpuChains[i] = r.CpuLink.ChainCount;

                if (session.IsFinished)
                {
                    break;
                }
            }

            Assert.That(playerChains, Is.EqualTo(new[] { 1, 2, 3, 1, 1 }), "PLAYER は自分の直前の個体だけで数えます。");
            Assert.That(cpuChains, Is.EqualTo(new[] { 1, 2, 1, 2, 3 }), "CPU も同じ規則で、別々に数えます。");

            // 1ラウンド目 R50 vs R50 は引き分け。それでも2ラウンド目はチェーン2です。
            Assert.That(session.History[0].Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(session.History[1].PlayerLink.ChainCount, Is.EqualTo(2), "引き分けを挟んでも継続します。");
        }

        [Test]
        public void AttributeWinsAreNeverOverturnedByLink()
        {
            // CPU は R→R→R でチェーン3（+6）。PLAYER は G→B→G で LINK なし、3ラウンド目に R へ属性勝ちされる G を出す。
            BattleSession session = Session(
                new[] { One("p0", G, 10), One("p1", B, 10), One("p2", G, 10), One("p3", G), One("p4", G), One("p5", G), One("p6", G) },
                new[] { One("c0", R, 1), One("c1", R, 1), One("c2", R, 1), One("c3", R), One("c4", R), One("c5", R), One("c6", R) },
                "c0", "c1", "c2");

            Play(session, "p0");
            Play(session, "p1");
            RoundResult third = Play(session, "p2");

            Assert.That(third.CpuLink.BonusPower, Is.EqualTo(6));
            Assert.That(third.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(third.Winner, Is.EqualTo(RoundWinner.Cpu), "R は G に属性勝ち。POWER は見ません。");
            Assert.That(third.DecidingAttribute, Is.EqualTo(R));

            // 逆向き: LINK +6 の側が属性で負ける。
            BattleSession reverse = Session(
                new[] { One("p0", B, 1), One("p1", B, 1), One("p2", B, 1), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", G, 99), One("c1", R, 99), One("c2", G, 99), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1", "c2");

            Play(reverse, "p0");
            Play(reverse, "p1");
            RoundResult r = Play(reverse, "p2");

            Assert.That(r.PlayerLink.BonusPower, Is.EqualTo(6));
            Assert.That(r.Winner, Is.EqualTo(RoundWinner.Cpu), "G は B に属性勝ち。PLAYER の LINK +6 では覆りません。");
        }

        [Test]
        public void LinkCanOverturnAPowerComparison()
        {
            // 2ラウンド目: PLAYER R48 (+3 → 51) vs CPU R50（CPU は G→R で LINK なし）
            BattleSession session = Session(
                new[] { One("p0", R), One("p1", R, 48), One("p2", B), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", G, 1), One("c1", R, 50), One("c2", G), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1");

            Play(session, "p0");
            RoundResult r = Play(session, "p1");

            Assert.That(r.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(r.Winner, Is.EqualTo(RoundWinner.Player), "LINK なしなら 48 < 50 で負けていました。");
            Assert.That(r.PlayerUnit.PowerOf(R), Is.EqualTo(48), "RoundResult の PlayerUnit は元のPOWERのままです。");
            Assert.That(r.PlayerEffectiveUnit.PowerOf(R), Is.EqualTo(51));
            Assert.That(r.CpuEffectiveUnit.PowerOf(R), Is.EqualTo(50));
            Assert.That(r.DecidingAttribute, Is.EqualTo(R), "POWER比較で比べた色です。");
            Assert.That(session.PlayerSquad.Find("p1").PowerOf(R), Is.EqualTo(48), "編成の個体も書き換えません。");
        }

        [Test]
        public void LinkCanTieThePowerAndSendTheRoundToCore()
        {
            // 2ラウンド目: PLAYER R47+3=50（CORE 9）vs CPU R50（CORE 1）
            BattleSession session = Session(
                new[] { One("p0", R), One("p1", R, 47, core: 9), One("p2", B), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", G), One("c1", R, 50, core: 1), One("c2", G), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1");

            Play(session, "p0");
            RoundResult r = Play(session, "p1");

            Assert.That(r.Decision, Is.EqualTo(RoundDecision.CoreComparison), "LINK で POWER が並び、CORE 比較へ進みます。");
            Assert.That(r.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(r.DecidingAttribute, Is.Null);
        }

        [Test]
        public void LinkCanCreateOrRemoveADraw()
        {
            // 作る: PLAYER R47+3=50 vs CPU R50、CORE も同じ → 引き分け（LINK なしなら CPU 勝ち）
            BattleSession makes = Session(
                new[] { One("p0", R), One("p1", R, 47), One("p2", B), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", G), One("c1", R, 50), One("c2", G), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1");

            Play(makes, "p0");
            RoundResult made = Play(makes, "p1");

            Assert.That(made.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(made.Decision, Is.EqualTo(RoundDecision.Draw));

            // 消す: PLAYER R50+3=53 vs CPU R50 → PLAYER 勝ち（LINK なしなら引き分け）
            BattleSession removes = Session(
                new[] { One("p0", R), One("p1", R, 50), One("p2", B), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", G), One("c1", R, 50), One("c2", G), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1");

            Play(removes, "p0");
            RoundResult removed = Play(removes, "p1");

            Assert.That(removed.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(removed.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void PlayerAndCpuAreTreatedSymmetrically()
        {
            BattleUnit[] a = { One("a0", R), One("a1", R, 48), One("a2", B), One("a3", B), One("a4", B), One("a5", B), One("a6", B) };
            BattleUnit[] b = { One("b0", G, 1), One("b1", R, 50), One("b2", G), One("b3", G), One("b4", G), One("b5", G), One("b6", G) };

            BattleSession forward = Session(a, b, "b0", "b1");
            Play(forward, "a0");
            RoundResult f = Play(forward, "a1");

            BattleSession mirrored = Session(b, a, "a0", "a1");
            Play(mirrored, "b0");
            RoundResult m = Play(mirrored, "b1");

            Assert.That(f.PlayerLink, Is.EqualTo(m.CpuLink));
            Assert.That(f.CpuLink, Is.EqualTo(m.PlayerLink));
            Assert.That(f.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(m.Winner, Is.EqualTo(RoundWinner.Cpu), "左右を入れ替えると勝者も入れ替わります。");
            Assert.That(f.Decision, Is.EqualTo(m.Decision));
        }

        [Test]
        public void RejectedOrFailedResolutionsNeverAdvanceTheChain()
        {
            BattleSession session = Session(
                new[] { One("p0", R), One("p1", R), One("p2", R), One("p3", R), One("p4", R), One("p5", R), One("p6", R) },
                new[] { One("c0", R), One("c1", R), One("c2", R), One("c3", R), One("c4", R), One("c5", R), One("c6", R) },
                "c0", "c1", "c2");

            Play(session, "p0");

            // 入力拒否: 未選出のまま解決、使用済みの選出
            Assert.That(session.TryResolveRound(out _, out BattleError incomplete), Is.False);
            Assert.That(incomplete, Is.EqualTo(BattleError.SelectionIncomplete));
            Assert.That(session.SelectPlayerUnit("p0").Success, Is.False);
            Assert.That(session.History.Count, Is.EqualTo(1));

            RoundResult second = Play(session, "p1");

            Assert.That(second.PlayerLink.ChainCount, Is.EqualTo(2), "拒否された操作はチェーンを進めません。");

            // 判定が例外を投げる個体（重複色の2属性）は、編成の時点で受け付けません。
            // そのため TryResolveRound の中で判定が例外を投げる経路はなく、
            // LINK の計算も判定も状態を変える前に済ませています。
            List<BattleUnit> invalid = new List<BattleUnit>
            {
                One("p0", R), Two("p1", R, 40, R, 40), One("p2", R), One("p3", R), One("p4", R), One("p5", R), One("p6", R),
            };

            Assert.That(BattleSquad.TryCreate(invalid, out _, out BattleError rejected), Is.False);
            Assert.That(rejected, Is.Not.EqualTo(BattleError.None));

            Assert.That(session.History.Count, Is.EqualTo(2));
            Assert.That(session.History[1].PlayerLink.ChainCount, Is.EqualTo(2));
        }

        [Test]
        public void PreviewIsSideEffectFreeAndOnlyCoversThePlayer()
        {
            BattleSession session = Session(
                new[] { One("p0", R), Two("p1", G, 40, R, 40), One("p2", G), One("p3", B), One("p4", B), One("p5", B), One("p6", B) },
                new[] { One("c0", R), One("c1", R), One("c2", R), One("c3", R), One("c4", R), One("c5", R), One("c6", R) },
                "c0", "c1", "c2");

            Assert.That(session.PreviewPlayerLink("p0"), Is.EqualTo(AttributeLinkResult.None), "最初の個体はチェーン1です。");

            Play(session, "p0");

            Assert.That(session.PreviewPlayerLink("p1").BonusPower, Is.EqualTo(3), "G/R は直前の R とつながります。");
            Assert.That(session.PreviewPlayerLink("p2").IsActive, Is.False, "G は R とつながりません。");
            Assert.That(session.PreviewPlayerLink("p0"), Is.EqualTo(AttributeLinkResult.None), "使用済みは予告しません。");
            Assert.That(session.PreviewPlayerLink("c1"), Is.EqualTo(AttributeLinkResult.None), "相手の個体は予告しません。");
            Assert.That(session.PreviewPlayerLink("nope"), Is.EqualTo(AttributeLinkResult.None));

            // 予告は状態を変えません。
            Assert.That(session.History.Count, Is.EqualTo(1));
            Assert.That(session.HasPlayerSelected, Is.False);

            RoundResult second = Play(session, "p1");

            Assert.That(second.PlayerLink, Is.EqualTo(new AttributeLinkResult(2, 3, AttributeLink.BitOf(R))), "予告どおりに解決されます。");
        }

        [Test]
        public void ANewSessionStartsEveryChainAgain()
        {
            BattleUnit[] players = { One("p0", R), One("p1", R), One("p2", R), One("p3", R), One("p4", R), One("p5", R), One("p6", R) };
            BattleUnit[] cpus = { One("c0", R), One("c1", R), One("c2", R), One("c3", R), One("c4", R), One("c5", R), One("c6", R) };

            BattleSession first = Session(players, cpus, "c0", "c1", "c2");
            Play(first, "p0");
            Assert.That(Play(first, "p1").PlayerLink.ChainCount, Is.EqualTo(2));

            // 再戦は新しい BattleSession です（BattleFlowCoordinator.Rematch → Begin）。
            BattleSession rematch = Session(players, cpus, "c0", "c1", "c2");
            RoundResult opening = Play(rematch, "p1");

            Assert.That(opening.PlayerLink, Is.EqualTo(AttributeLinkResult.None));
            Assert.That(opening.CpuLink, Is.EqualTo(AttributeLinkResult.None));
        }

        [Test]
        public void TheRoundResultRecordsLinksAndTheDecidingAttribute()
        {
            BattleSession session = Session(
                new[] { Two("p0", R, 40, B, 40), Two("p1", B, 30, R, 30), One("p2", G), One("p3", G), One("p4", G), One("p5", G), One("p6", G) },
                new[] { One("c0", G), One("c1", G), One("c2", G), One("c3", G), One("c4", G), One("c5", G), One("c6", G) },
                "c0", "c1");

            RoundResult first = Play(session, "p0");
            RoundResult second = Play(session, "p1");

            Assert.That(first.PlayerLink, Is.EqualTo(AttributeLinkResult.None));
            Assert.That(first.DecidingAttribute, Is.EqualTo(R), "R/B は G に R で属性勝ち。");
            Assert.That(DecidingAttributeLookup.Of(first), Is.EqualTo(first.DecidingAttribute), "画面はこの値をそのまま読みます。");

            Assert.That(second.PlayerLink.ChainCount, Is.EqualTo(2));
            Assert.That(second.PlayerLink.SharedAttributeCount, Is.EqualTo(2), "R/B と B/R は2色とも共有。");
            Assert.That(second.CpuLink.ChainCount, Is.EqualTo(2), "CPU も G→G で x2。");
            Assert.That(second.PlayerEffectiveUnit.PowerOf(B), Is.EqualTo(33));
            Assert.That(second.PlayerEffectiveUnit.PowerOf(R), Is.EqualTo(33));
            Assert.That(second.CpuEffectiveUnit.PowerOf(G), Is.EqualTo(53));
            Assert.That(second.PlayerUnit.PowerOf(B), Is.EqualTo(30), "PlayerUnit は元の個体です。");
        }

        // ---------------- 補助 ----------------

        private static BattleSession Session(BattleUnit[] players, BattleUnit[] cpus, params string[] cpuOrder)
        {
            return new BattleSession(
                TestBattleUnits.CreateSquad(players),
                TestBattleUnits.CreateSquad(cpus),
                Scripted(cpuOrder));
        }

        /// <summary>CPU に、決めた順番で個体を出させます（候補の中から ID で選ぶだけで、相手の手は見ません）。</summary>
        private static IBattleUnitSelector Scripted(params string[] order)
        {
            int next = 0;

            return new RecordingUnitSelector(candidates =>
            {
                string id = next < order.Length ? order[next++] : candidates[0].InstanceId;

                foreach (BattleUnit unit in candidates)
                {
                    if (unit.InstanceId == id)
                    {
                        return unit;
                    }
                }

                return candidates[0];
            });
        }

        private static RoundResult Play(BattleSession session, string playerId)
        {
            Assert.That(session.SelectCpuUnit().Success, Is.True, "CPU の選出");
            Assert.That(session.SelectPlayerUnit(playerId).Success, Is.True, "PLAYER の選出 " + playerId);
            Assert.That(session.TryResolveRound(out RoundResult result, out BattleError error), Is.True, error.ToString());

            return result;
        }
    }
}
