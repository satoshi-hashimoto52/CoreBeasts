using System.Collections;

using CoreBeasts.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 実際の Battle シーンの FxFlash が、どの決着でも最大不透明度
    /// <see cref="BattleFxPlayer.FlashPeakAlpha"/>（従来 1.0 → 0.8）を超えないことを確かめます。
    /// 決着エフェクトはフラッシュと同じ瞬間に出ていることも確かめます。
    /// </summary>
    public sealed class BattleFlashPeakPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float PreviousPeakAlpha = 1f;
        private const float SecondsLimit = 10f;

        private ISquadRepository originalRepository;
        private BattleFxPlayer fx;
        private CanvasGroup flash;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            SaveTestSquad();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            fx = Object.FindAnyObjectByType<BattleFxPlayer>();

            Assert.That(fx, Is.Not.Null);

            flash = GameObject.Find("FxFlash").GetComponent<CanvasGroup>();

            Assert.That(flash, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        [UnityTest]
        public IEnumerator TheFlashStaysBelowItsPeakForEveryDecision()
        {
            BattleImpactKind[] kinds =
            {
                BattleImpactKind.Red, BattleImpactKind.Blue, BattleImpactKind.Green,
                BattleImpactKind.Power, BattleImpactKind.Core, BattleImpactKind.Draw,
            };

            foreach (BattleImpactKind kind in kinds)
            {
                float max = 0f;
                bool impactWhileLit = false;
                float litSeconds = 0f;

                fx.StartCoroutine(Play(kind));

                yield return null;

                float startedAt = Time.realtimeSinceStartup;

                while (fx.IsPlaying)
                {
                    max = Mathf.Max(max, flash.alpha);

                    if (flash.alpha > 0f)
                    {
                        litSeconds += Time.unscaledDeltaTime;
                        impactWhileLit |= fx.ImpactBurst.IsShowing && fx.ImpactBurst.Kind == kind;
                    }

                    yield return null;

                    Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(SecondsLimit));
                }

                Assert.That(max, Is.GreaterThan(0f), kind + " でフラッシュが出ませんでした。");
                Assert.That(max, Is.LessThanOrEqualTo(BattleFxPlayer.FlashPeakAlpha + 0.0001f), kind.ToString());
                Assert.That(max, Is.LessThan(PreviousPeakAlpha), kind + " は従来より弱くなります。");
                Assert.That(impactWhileLit, Is.True, kind + " の決着エフェクトはフラッシュと同時に出ます。");
                Assert.That(flash.alpha, Is.EqualTo(0f), kind + " の終了後に残っています。");

                TestContext.WriteLine(kind + ": max flash alpha " + max.ToString("0.000") + ", lit " + litSeconds.ToString("0.000") + "s");

                fx.ResetVisuals();
            }
        }

        private IEnumerator Play(BattleImpactKind kind)
        {
            switch (kind)
            {
                case BattleImpactKind.Red:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.AttributeAdvantage, UnitAttribute.Red, Color.red);

                case BattleImpactKind.Blue:
                    return fx.PlayClashRoutine(RoundWinner.Cpu, RoundDecision.AttributeAdvantage, UnitAttribute.Blue, Color.blue);

                case BattleImpactKind.Green:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.AttributeAdvantage, UnitAttribute.Green, Color.green);

                case BattleImpactKind.Power:
                    return fx.PlayClashRoutine(RoundWinner.Cpu, RoundDecision.PowerComparison, UnitAttribute.Red, Color.red);

                case BattleImpactKind.Core:
                    return fx.PlayClashRoutine(RoundWinner.Player, RoundDecision.CoreComparison, null, Color.green);

                default:
                    return fx.PlayClashRoutine(RoundWinner.Draw, RoundDecision.Draw, null, Color.white);
            }
        }

        private static void SaveTestSquad()
        {
#if UNITY_EDITOR
            CoreBeastRoster roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);

            Assert.That(roster, Is.Not.Null, RosterPath + " を読めません。");

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
