using System.Collections.Generic;

using CoreBeasts.Units;
using NUnit.Framework;

namespace CoreBeasts.Battle.Tests
{
    /// <summary>
    /// 敵の残存戦力の公開。
    ///
    /// 残っている属性構成は読めるが、次に出る個体は分からない――
    /// その線引きが守られていることを確かめます。
    /// </summary>
    public sealed class EnemyForceDisclosureTests
    {
        private const UnitAttribute R = UnitAttribute.Red;
        private const UnitAttribute G = UnitAttribute.Green;
        private const UnitAttribute B = UnitAttribute.Blue;

        private static BattleUnit One(string id, UnitAttribute a)
        {
            return new BattleUnit(id, a, 50, 50);
        }

        private static BattleUnit Two(string id, UnitAttribute a, UnitAttribute b)
        {
            return new BattleUnit(id, a, 30, b, 20, 50);
        }

        /// <summary>単色3・2色4の、分かりやすい7体。</summary>
        private static List<BattleUnit> Seven()
        {
            return new List<BattleUnit>
            {
                One("c0", R), One("c1", G), One("c2", B),
                Two("c3", R, B), Two("c4", R, G), Two("c5", G, B), Two("c6", R, B),
            };
        }

        // ---------------- 構成そのもの ----------------

        [Test]
        public void ADualCompositionIsTheSameWhateverTheOrder()
        {
            Assert.That(
                AttributeComposition.Dual(R, B),
                Is.EqualTo(AttributeComposition.Dual(B, R)),
                "登録順が違うだけで別構成になってはいけません。");
        }

        [Test]
        public void AThreeColourCompositionCannotBeBuilt()
        {
            // 型として2色までしか持てません。3色は作る口がありません。
            AttributeComposition dual = AttributeComposition.Dual(R, B);

            Assert.That(dual.Count, Is.EqualTo(2));
            Assert.That(dual.Contains(G), Is.False);

            AttributeComposition single = AttributeComposition.Single(R);

            Assert.That(single.Count, Is.EqualTo(1));
            Assert.That(single.IsDual, Is.False);
        }

        // ---------------- FullComposition ----------------

        [Test]
        public void TheInitialSevenAreShownWithTheirRealCompositions()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.FullComposition);

            Assert.That(readout.RemainingCount, Is.EqualTo(7));
            Assert.That(readout.UsedCount, Is.Zero);
            Assert.That(readout.Compositions.Count, Is.EqualTo(7));

            // 単色3・2色4。嘘の情報は出しません。
            Assert.That(readout.DualCount, Is.EqualTo(4));
            Assert.That(readout.RedHolders, Is.EqualTo(4));   // c0, c3, c4, c6
            Assert.That(readout.GreenHolders, Is.EqualTo(3)); // c1, c4, c5
            Assert.That(readout.BlueHolders, Is.EqualTo(4));  // c2, c3, c5, c6
        }

        [Test]
        public void TheCompositionsAreACanonicalPoolNotTheSquadOrder()
        {
            List<BattleUnit> squad = Seven();

            EnemyForceReadout first = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.FullComposition);

            // 編成順を入れ替えても、見える並びは変わりません。
            squad.Reverse();

            EnemyForceReadout second = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.FullComposition);

            Assert.That(
                second.Compositions,
                Is.EqualTo(first.Compositions),
                "並びから編成順が読めてしまいます。");

            // 正規化順に並んでいること（単色が先、次に色の順）。
            for (int i = 1; i < first.Compositions.Count; i++)
            {
                Assert.That(
                    first.Compositions[i - 1].CompareTo(first.Compositions[i]),
                    Is.LessThanOrEqualTo(0),
                    "並べ替えが効いていません。");
            }
        }

        [Test]
        public void RemovingAUnitRemovesExactlyOneComposition()
        {
            List<BattleUnit> squad = Seven();

            EnemyForceReadout before = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.FullComposition);

            // Red/Blue を1体使いました。
            squad.RemoveAll(u => u.InstanceId == "c3");

            EnemyForceReadout after = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.FullComposition);

            Assert.That(after.RemainingCount, Is.EqualTo(6));
            Assert.That(after.UsedCount, Is.EqualTo(1));
            Assert.That(after.Compositions.Count, Is.EqualTo(6));

            Assert.That(
                after.DualCount,
                Is.EqualTo(before.DualCount - 1),
                "2色が1つだけ減ります。");

            Assert.That(after.RedHolders, Is.EqualTo(before.RedHolders - 1));
            Assert.That(after.BlueHolders, Is.EqualTo(before.BlueHolders - 1));
            Assert.That(after.GreenHolders, Is.EqualTo(before.GreenHolders));
        }

        // ---------------- AggregateCounts ----------------

        [Test]
        public void AggregateCountsHideEachCompositionButKeepTheTotals()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.AggregateCounts);

            Assert.That(readout.ShowsEachComposition, Is.False);
            Assert.That(readout.Compositions, Is.Empty, "構成を見せてはいけません。");

            Assert.That(readout.ShowsAttributeCounts, Is.True);
            Assert.That(readout.RedHolders, Is.EqualTo(4));
            Assert.That(readout.GreenHolders, Is.EqualTo(3));
            Assert.That(readout.BlueHolders, Is.EqualTo(4));
            Assert.That(readout.DualCount, Is.EqualTo(4));
            Assert.That(readout.RemainingCount, Is.EqualTo(7));
        }

        // ---------------- Masked ----------------

        [Test]
        public void MaskedShowsOnlyHowManyAreLeft()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.Masked);

            Assert.That(readout.RemainingCount, Is.EqualTo(7));

            Assert.That(readout.Compositions, Is.Empty);
            Assert.That(readout.ShowsEachComposition, Is.False);
            Assert.That(readout.ShowsAttributeCounts, Is.False);

            // 色の手がかりを一切残しません。
            Assert.That(readout.RedHolders, Is.Zero);
            Assert.That(readout.GreenHolders, Is.Zero);
            Assert.That(readout.BlueHolders, Is.Zero);
            Assert.That(readout.DualCount, Is.Zero);

            Assert.That(readout.HoldersOf(R), Is.Zero);
            Assert.That(readout.HoldersOf(G), Is.Zero);
            Assert.That(readout.HoldersOf(B), Is.Zero);
        }

        [Test]
        public void TheThreePoliciesGiveStrictlyDecreasingInformation()
        {
            List<BattleUnit> squad = Seven();

            EnemyForceReadout full = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.FullComposition);

            EnemyForceReadout aggregate = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.AggregateCounts);

            EnemyForceReadout masked = EnemyForceModel.Build(
                squad, 7, EnemyForceDisclosure.Masked);

            Assert.That(full.Compositions.Count, Is.GreaterThan(aggregate.Compositions.Count));
            Assert.That(
                aggregate.Compositions.Count,
                Is.GreaterThanOrEqualTo(masked.Compositions.Count));

            Assert.That(aggregate.RedHolders, Is.GreaterThan(masked.RedHolders));

            // 残数だけはどの方針でも同じです。嘘は出しません。
            Assert.That(aggregate.RemainingCount, Is.EqualTo(full.RemainingCount));
            Assert.That(masked.RemainingCount, Is.EqualTo(full.RemainingCount));
        }

        // ---------------- 将来の解析スキル用の口 ----------------

        [Test]
        public void AnAnalysisOverrideCanRevealOneCompositionWithoutOpeningTheRest()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.Masked, revealCompositions: 1);

            Assert.That(readout.Compositions.Count, Is.EqualTo(1));

            // それ以外は隠れたままです。
            Assert.That(readout.RedHolders, Is.Zero);
            Assert.That(readout.ShowsEachComposition, Is.False);
        }

        [Test]
        public void AnAnalysisOverrideCanRevealOneAttributeCount()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.Masked,
                revealAttributeCounts: new[] { R });

            Assert.That(readout.RedHolders, Is.EqualTo(4));
            Assert.That(readout.GreenHolders, Is.Zero, "指定していない色は隠れたままです。");
            Assert.That(readout.BlueHolders, Is.Zero);
        }

        // ---------------- 情報が漏れないこと ----------------

        [Test]
        public void NothingAboutIdentityOrStatsIsExposed()
        {
            EnemyForceReadout readout = EnemyForceModel.Build(
                Seven(), 7, EnemyForceDisclosure.FullComposition);

            // 公開されるのは構成と数だけです。
            // 個体ID・POWER・CORE・レベル・スキルを持つ口がありません。
            System.Reflection.PropertyInfo[] properties =
                typeof(EnemyForceReadout).GetProperties();

            for (int i = 0; i < properties.Length; i++)
            {
                string name = properties[i].Name.ToLowerInvariant();

                Assert.That(name, Does.Not.Contain("instance"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("power"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("core"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("level"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("skill"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("next"), properties[i].Name);
                Assert.That(name, Does.Not.Contain("pending"), properties[i].Name);
            }

            Assert.That(readout.Compositions.Count, Is.EqualTo(7));
        }
    }
}
