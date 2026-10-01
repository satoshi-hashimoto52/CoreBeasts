using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 色別POWERでの勝敗判定を、仕様の表どおりに総当たりで確かめます。
    ///
    /// 色は1〜2色、三すくみは Red &gt; Green &gt; Blue &gt; Red。
    /// 判定は<see cref="BattleRules.ResolveRound"/>だけが持ちます。
    /// </summary>
    public sealed class BattleColourPowerMatrixTests
    {
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute G = UnitAttribute.Green;
        private const UnitAttribute B = UnitAttribute.Blue;

        private static BattleUnit One(
            string id, UnitAttribute a, int power, int core = 50)
        {
            return new BattleUnit(id, a, power, core);
        }

        private static BattleUnit Two(
            string id, UnitAttribute a, int pa, UnitAttribute b, int pb, int core = 50)
        {
            return new BattleUnit(id, a, pa, b, pb, core);
        }

        // ---------------- 三すくみの向き ----------------

        [Test]
        public void TheAttributeCycleIsRedBeatsGreenBeatsBlueBeatsRed()
        {
            Assert.That(BattleRules.Beats(R, G), Is.True, "赤 > 緑");
            Assert.That(BattleRules.Beats(G, B), Is.True, "緑 > 青");
            Assert.That(BattleRules.Beats(B, R), Is.True, "青 > 赤");

            Assert.That(BattleRules.Beats(G, R), Is.False);
            Assert.That(BattleRules.Beats(B, G), Is.False);
            Assert.That(BattleRules.Beats(R, B), Is.False);

            Assert.That(BattleRules.Beats(R, R), Is.False, "同色では勝ちません。");
        }

        // ---------------- 単色 VS 単色 ----------------

        [TestCase(0, 1)]   // Red  vs Green -> Red
        [TestCase(1, 2)]   // Green vs Blue -> Green
        [TestCase(2, 0)]   // Blue vs Red   -> Blue
        public void DifferentSingleColoursAreDecidedByTheCycle(int winner, int loser)
        {
            UnitAttribute win = (UnitAttribute)winner;
            UnitAttribute lose = (UnitAttribute)loser;

            // POWERで負けていても属性勝ちが優先します。
            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", win, 1), One("c", lose, 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(win));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TheSameSingleColourIsDecidedByThatColoursPower(int colour)
        {
            UnitAttribute a = (UnitAttribute)colour;

            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", a, 60), One("c", a, 59));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(a));
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(60));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(59));
        }

        // ---------------- 2色 VS 単色：共通色あり ----------------

        [Test]
        public void RedBlueVersusRed_IsADualAttributeWin()
        {
            // 共通色 Red。余剰色 Blue は Red に勝つので 2色側の属性勝ち。
            // POWER も CORE も使いません。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 1, B, 1, core: 1),
                One("c", R, 999, core: 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(B));
            Assert.That(outcome.SharedAttribute, Is.EqualTo(R));
        }

        [Test]
        public void RedGreenVersusRed_FallsBackToTheSharedColoursPower()
        {
            // 共通色 Red。余剰色 Green は Red に負けます。
            // それでも単色側の属性勝ちにはせず、共通色 Red の POWER で比べます。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 40, G, 30),
                One("c", R, 35));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(R));
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(40));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(35));
        }

        [Test]
        public void TheSurplusColourLosingDoesNotMeanTheDualSideLoses()
        {
            // 余剰色が負けても即敗北ではありません。共通色のPOWERで上回れば勝ちます。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 80, G, 10),
                One("c", R, 20));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void RedBlueVersusBlue_FallsBackToTheSharedBluePower()
        {
            // 共通色 Blue。余剰色 Red は Blue に勝てないので Blue の POWER 勝負。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 99, B, 10),
                One("c", B, 58));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(B));
        }

        // ---------------- 2色 VS 単色：相手に勝てる色を持つ ----------------

        [Test]
        public void RedBlueVersusGreen_WinsByTheFavourableColour()
        {
            // Red は Green に勝ちます。Blue が Green に負けていても相殺しません。
            // 2色は相手へ有利な色を選んで戦えるものとして扱います。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 40, B, 20),
                One("c", G, 999, core: 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(R));
            Assert.That(outcome.SharedAttribute, Is.Null, "共通色はありません。");

            // POWERもCOREも見ていないこと。
            Assert.That(outcome.PlayerComparedValue, Is.Zero);
            Assert.That(outcome.CpuComparedValue, Is.Zero);
        }

        [Test]
        public void GreenVersusRedBlue_IsTheSameResultFromTheOtherSide()
        {
            // 左右を入れ替えても 2色側が勝ちます。先行側優先はありません。
            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", G, 999, core: 999),
                Two("c", R, 40, B, 20));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(R));
        }

        // 2色構成 × 単色の全9通り。属性値は Red=0 / Green=1 / Blue=2。
        // 勝てる色を持てば属性勝ち、無ければ必ず共通色があるのでPOWER勝負です。
        //
        //          dual1, dual2, single, 属性勝ちか, 決め手の色
        [TestCase(0, 2, 0, true, 2)]    // Red/Blue   vs Red   -> Blue で属性勝ち
        [TestCase(0, 2, 1, true, 0)]    // Red/Blue   vs Green -> Red   で属性勝ち
        [TestCase(0, 2, 2, false, 2)]   // Red/Blue   vs Blue  -> 共通 Blue の POWER
        [TestCase(0, 1, 0, false, 0)]   // Red/Green  vs Red   -> 共通 Red の POWER
        [TestCase(0, 1, 1, true, 0)]    // Red/Green  vs Green -> Red   で属性勝ち
        [TestCase(0, 1, 2, true, 1)]    // Red/Green  vs Blue  -> Green で属性勝ち
        [TestCase(1, 2, 0, true, 2)]    // Green/Blue vs Red   -> Blue  で属性勝ち
        [TestCase(1, 2, 1, false, 1)]   // Green/Blue vs Green -> 共通 Green の POWER
        [TestCase(1, 2, 2, true, 1)]    // Green/Blue vs Blue  -> Green で属性勝ち
        public void EveryDualVersusSingleFollowsTheTable(
            int first, int second, int single, bool attributeWin, int decidingColour)
        {
            UnitAttribute a = (UnitAttribute)first;
            UnitAttribute b = (UnitAttribute)second;
            UnitAttribute c = (UnitAttribute)single;
            UnitAttribute deciding = (UnitAttribute)decidingColour;

            string label = a + "/" + b + " vs " + c;

            // 属性相性を最上位に置くので、有利側のPOWERを 1、不利側を 999 にしても
            // 属性で決まるケースでは結果が変わりません。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", a, 1, b, 1, core: 1),
                One("c", c, 999, core: 999));

            if (attributeWin)
            {
                Assert.That(
                    outcome.Winner, Is.EqualTo(RoundWinner.Player), label);

                Assert.That(
                    outcome.Decision,
                    Is.EqualTo(RoundDecision.AttributeAdvantage),
                    label);

                Assert.That(
                    outcome.DecidingAttribute, Is.EqualTo(deciding), label);

                Assert.That(outcome.PlayerComparedValue, Is.Zero, label);
                Assert.That(outcome.CpuComparedValue, Is.Zero, label);

                // COREを入れ替えても属性決着は動きません。
                RoundOutcome swappedCore = BattleRules.ResolveRound(
                    Two("p", a, 1, b, 1, core: 999),
                    One("c", c, 999, core: 1));

                Assert.That(
                    swappedCore.Winner, Is.EqualTo(RoundWinner.Player),
                    label + "（CORE入れ替え）");

                Assert.That(
                    swappedCore.Decision,
                    Is.EqualTo(RoundDecision.AttributeAdvantage),
                    label + "（CORE入れ替え）");
            }
            else
            {
                // 勝てる色が無いので、共通色のPOWERだけで比べます。
                Assert.That(
                    outcome.Decision,
                    Is.EqualTo(RoundDecision.PowerComparison),
                    label);

                Assert.That(
                    outcome.DecidingAttribute, Is.EqualTo(deciding), label);

                Assert.That(
                    outcome.SharedAttribute, Is.EqualTo(deciding), label);

                Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu), label);

                // 比較に使われたのが「共通色のPOWER」であることを値で確かめます。
                // 2色側は共通色 deciding のPOWERが 1、単色側は 999。
                Assert.That(
                    outcome.PlayerComparedValue, Is.EqualTo(1),
                    label + ": 共通色以外のPOWERを見ています。");

                Assert.That(
                    outcome.CpuComparedValue, Is.EqualTo(999), label);

                // もう片方の色だけを大きくしても、結果は変わりません。
                UnitAttribute other = a == deciding ? b : a;

                RoundOutcome boostedOther = BattleRules.ResolveRound(
                    Two("p", deciding, 1, other, 999, core: 1),
                    One("c", c, 999, core: 999));

                Assert.That(
                    boostedOther.Winner, Is.EqualTo(RoundWinner.Cpu),
                    label + ": 共通色以外のPOWERが結果へ influence しています。");

                Assert.That(
                    boostedOther.PlayerComparedValue, Is.EqualTo(1),
                    label + ": 共通色以外のPOWERを比較へ使っています。");
            }

            // 左右を入れ替えると勝者が反転します。
            RoundOutcome mirrored = BattleRules.ResolveRound(
                One("p", c, 999, core: 999),
                Two("c", a, 1, b, 1, core: 1));

            RoundWinner expected = outcome.Winner == RoundWinner.Player
                ? RoundWinner.Cpu
                : (outcome.Winner == RoundWinner.Cpu
                    ? RoundWinner.Player
                    : RoundWinner.Draw);

            Assert.That(mirrored.Winner, Is.EqualTo(expected), label + "（左右入れ替え）");
            Assert.That(mirrored.Decision, Is.EqualTo(outcome.Decision), label);
            Assert.That(
                mirrored.DecidingAttribute, Is.EqualTo(outcome.DecidingAttribute), label);
        }

        [Test]
        public void RedBlueVersusBlue_ComparesOnlyTheBluePower()
        {
            // RED < BLUE、BLUE = BLUE。勝てる色が無いので共通 BLUE のPOWERだけを比べます。
            // Red 側をいくら上げても結果は変わりません。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 999, B, 21, core: 66),
                One("c", B, 58, core: 84));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(B));
            Assert.That(outcome.SharedAttribute, Is.EqualTo(B));

            // 比べたのは Blue の 21 と 58。Red の 999 ではありません。
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(21));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(58));
        }

        [Test]
        public void RedGreenVersusRed_ComparesOnlyTheRedPower()
        {
            // GREEN < RED、RED = RED。共通 RED のPOWERだけを比べます。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 80, G, 999, core: 10),
                One("c", R, 76, core: 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(R));

            // 比べたのは Red の 80 と 76。Green の 999 ではありません。
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(80));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(76));
        }

        [Test]
        public void RedBlueAlwaysBeatsSingleGreenWhateverThePowerAndCore()
        {
            // 実Playで負けていた組み合わせ。RED > GREEN があるので必ず 2色側が勝ちます。
            int[][] cases =
            {
                new[] { 1, 1, 1, 999, 999 },
                new[] { 40, 21, 66, 64, 71 },
                new[] { 999, 999, 999, 1, 1 },
            };

            for (int i = 0; i < cases.Length; i++)
            {
                int[] v = cases[i];

                RoundOutcome outcome = BattleRules.ResolveRound(
                    Two("p", R, v[0], B, v[1], core: v[2]),
                    One("c", G, v[3], core: v[4]));

                Assert.That(
                    outcome.Winner,
                    Is.EqualTo(RoundWinner.Player),
                    "Red/Blue が単色Green へ負けてはいけません。");

                Assert.That(
                    outcome.Decision,
                    Is.EqualTo(RoundDecision.AttributeAdvantage));

                Assert.That(outcome.DecidingAttribute, Is.EqualTo(R));
                Assert.That(outcome.SharedAttribute, Is.Null);
            }
        }

        [Test]
        public void TheLosingColourNeverCancelsTheWinningOne()
        {
            // Red/Blue vs Green。Blue は Green に負けますが、
            // Red が Green に勝つので相殺せず 2色側の勝ちです。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 1, B, 1, core: 1),
                One("c", G, 999, core: 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
        }

        // ---------------- 2色 VS 2色 ----------------

        [Test]
        public void TheSameColourPairComparesAverages()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 40, B, 21),
                Two("c", R, 30, B, 30));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(30));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(30));
        }

        [Test]
        public void TheSameColourSetIsRecognisedWhateverTheOrder()
        {
            // 登録順が違っても同じ色集合なら同一構成です。
            RoundOutcome ordered = BattleRules.ResolveRound(
                Two("p", R, 40, B, 20),
                Two("c", R, 10, B, 10));

            RoundOutcome reversed = BattleRules.ResolveRound(
                Two("p", R, 40, B, 20),
                Two("c", B, 10, R, 10));

            Assert.That(reversed.Winner, Is.EqualTo(ordered.Winner));
            Assert.That(reversed.Decision, Is.EqualTo(ordered.Decision));
            Assert.That(
                reversed.Decision,
                Is.EqualTo(RoundDecision.PowerComparison),
                "同じ色集合なら属性では決まりません。");
        }

        [Test]
        public void DifferentColourPairsAreDecidedByTheSurplusColours()
        {
            // Red/Blue vs Red/Green : 共通色 Red を除くと Blue VS Green。
            // Green が Blue に勝つので Red/Green 側の属性勝ち。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 99, B, 99),
                Two("c", R, 1, G, 1));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Cpu));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.SharedAttribute, Is.EqualTo(R));
            Assert.That(outcome.DecidingAttribute, Is.EqualTo(G));
        }

        // ---------------- POWER → CORE → DRAW ----------------

        [Test]
        public void EqualPowerFallsThroughToCore()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", R, 50, core: 84),
                One("c", R, 50, core: 62));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.CoreComparison));
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(84));
            Assert.That(outcome.CpuComparedValue, Is.EqualTo(62));
        }

        [Test]
        public void EqualPowerAndEqualCoreIsADraw()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", R, 50, core: 70),
                One("c", R, 50, core: 70));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.Draw));
            Assert.That(outcome.IsDraw, Is.True);
        }

        [Test]
        public void TheAverageComparisonAlsoFallsThroughToCore()
        {
            // 平均で比べるのは、同じ色集合の 2色 VS 2色 だけになりました。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 40, B, 20, core: 90),
                Two("c", R, 30, B, 30, core: 10));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.CoreComparison));
        }

        [Test]
        public void TheAverageComparisonIsExactAndNeverRounds()
        {
            // 同一構成の 2色 VS 2色。平均 (41+20)/2 = 30.5 と (30+30)/2 = 30。
            // 切り捨てた 30 で比べると引き分けになってしまいます。
            // 合計のまま比べるので、丸めで勝敗が変わりません。
            RoundOutcome outcome = BattleRules.ResolveRound(
                Two("p", R, 41, B, 20),
                Two("c", R, 30, B, 30));

            Assert.That(
                outcome.Winner,
                Is.EqualTo(RoundWinner.Player),
                "平均の丸めで勝敗が変わってはいけません。");

            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.PowerComparison));
        }

        [Test]
        public void AnAttributeWinNeverLooksAtPowerOrCore()
        {
            RoundOutcome outcome = BattleRules.ResolveRound(
                One("p", R, 1, core: 1),
                One("c", G, 999, core: 999));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.Decision, Is.EqualTo(RoundDecision.AttributeAdvantage));
            Assert.That(outcome.PlayerComparedValue, Is.Zero, "POWERを見ていません。");
            Assert.That(outcome.CpuComparedValue, Is.Zero);
        }

        // ---------------- 全6構成の総当たりと対称性 ----------------

        private static List<BattleUnit> AllSixLoadouts(string prefix, int core)
        {
            return new List<BattleUnit>
            {
                One(prefix + "R", R, 50, core),
                One(prefix + "G", G, 50, core),
                One(prefix + "B", B, 50, core),
                Two(prefix + "RG", R, 30, G, 20, core),
                Two(prefix + "RB", R, 30, B, 20, core),
                Two(prefix + "GB", G, 30, B, 20, core),
            };
        }

        [Test]
        public void EverySixBySixCombinationIsDecidedWithoutThrowing()
        {
            List<BattleUnit> mine = AllSixLoadouts("p", 50);
            List<BattleUnit> theirs = AllSixLoadouts("c", 50);

            for (int i = 0; i < mine.Count; i++)
            {
                for (int j = 0; j < theirs.Count; j++)
                {
                    RoundOutcome outcome = BattleRules.ResolveRound(mine[i], theirs[j]);

                    Assert.That(
                        outcome.Decision,
                        Is.Not.EqualTo(RoundDecision.Draw).Or.EqualTo(RoundDecision.Draw),
                        "判定が返りません。");

                    if (outcome.Winner == RoundWinner.Draw)
                    {
                        Assert.That(
                            outcome.Decision,
                            Is.EqualTo(RoundDecision.Draw),
                            mine[i] + " vs " + theirs[j] +
                            " : 引き分けの理由が Draw ではありません。");
                    }
                    else
                    {
                        Assert.That(
                            outcome.Decision,
                            Is.Not.EqualTo(RoundDecision.Draw),
                            mine[i] + " vs " + theirs[j] +
                            " : 勝者がいるのに理由が Draw です。");
                    }
                }
            }
        }

        [Test]
        public void SwappingTheSidesAlwaysSwapsTheWinner()
        {
            // POWERとCOREを散らして、あらゆる決着理由を通します。
            List<BattleUnit> mine = AllSixLoadouts("p", 61);
            List<BattleUnit> theirs = AllSixLoadouts("c", 44);

            for (int i = 0; i < mine.Count; i++)
            {
                for (int j = 0; j < theirs.Count; j++)
                {
                    RoundOutcome forward = BattleRules.ResolveRound(mine[i], theirs[j]);
                    RoundOutcome backward = BattleRules.ResolveRound(theirs[j], mine[i]);

                    RoundWinner expected = forward.Winner == RoundWinner.Player
                        ? RoundWinner.Cpu
                        : (forward.Winner == RoundWinner.Cpu
                            ? RoundWinner.Player
                            : RoundWinner.Draw);

                    Assert.That(
                        backward.Winner,
                        Is.EqualTo(expected),
                        mine[i] + " vs " + theirs[j] + " で左右対称になっていません。");

                    Assert.That(
                        backward.Decision,
                        Is.EqualTo(forward.Decision),
                        mine[i] + " vs " + theirs[j] + " で決着理由が変わりました。");
                }
            }
        }

        [Test]
        public void TheSameInputAlwaysGivesTheSameResult()
        {
            List<BattleUnit> mine = AllSixLoadouts("p", 55);
            List<BattleUnit> theirs = AllSixLoadouts("c", 55);

            for (int i = 0; i < mine.Count; i++)
            {
                for (int j = 0; j < theirs.Count; j++)
                {
                    RoundOutcome first = BattleRules.ResolveRound(mine[i], theirs[j]);
                    RoundOutcome second = BattleRules.ResolveRound(mine[i], theirs[j]);

                    Assert.That(second.Winner, Is.EqualTo(first.Winner));
                    Assert.That(second.Decision, Is.EqualTo(first.Decision));
                }
            }
        }

        // ---------------- 補正後の値で判定すること ----------------

        [Test]
        public void ScalingPowerAffectsEveryColourAndTheJudgementUsesTheResult()
        {
            // 色を指定しない「POWERを上げる」効果は、持っている色すべてへ同じように効きます。
            BattleUnit boosted = Two("p", R, 20, B, 10).WithScaledPower(2f);

            Assert.That(boosted.PowerOf(R), Is.EqualTo(40));
            Assert.That(boosted.PowerOf(B), Is.EqualTo(20));

            // 補正後の 40/20（平均30）で判定します。補正前なら平均15で負けています。
            // 平均で比べるのは同一構成どうしなので、相手も Red/Blue にします。
            RoundOutcome outcome = BattleRules.ResolveRound(
                boosted, Two("c", R, 25, B, 25));

            Assert.That(outcome.Winner, Is.EqualTo(RoundWinner.Player));
            Assert.That(outcome.PlayerComparedValue, Is.EqualTo(30));
        }

        [Test]
        public void ScalingASingleColourOnlyTouchesThatColour()
        {
            BattleUnit unit = Two("p", R, 20, B, 10).WithPowerOf(B, 55);

            Assert.That(unit.PowerOf(R), Is.EqualTo(20));
            Assert.That(unit.PowerOf(B), Is.EqualTo(55));
        }
    }
}
