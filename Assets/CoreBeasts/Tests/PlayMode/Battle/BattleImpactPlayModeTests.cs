using System.Collections;
using System.Collections.Generic;

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
    /// Phase 1「戦闘の手応え」を、実際の Battle シーンで確かめます。
    ///
    /// 編成は共有の保存先へテスト用に7体を入れてからシーンを読み込みます。
    /// DEPLOYは循環リングの上スライド成立と同じ入口
    /// （<see cref="BattleScreenController.OnWheelDeployRequested"/>）から入れます。
    /// </summary>
    public sealed class BattleImpactPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const int WarmUpFrames = 5;
        /// <summary>1ラウンドを待つ上限（実時間秒）。バッチモードはフレームが極端に速いため、フレーム数では数えません。</summary>
        private const float RoundSecondsLimit = 10f;

        private static readonly string[] HudNames =
        {
            "SafeArea", "Header", "ScoreLabel", "RoundLabel", "ScorePips", "SettingsButton",
            "BattleRoot", "PlayerWheel", "HistoryLane", "FxFlash", "ResultView", "RematchButton",
        };

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleFxPlayer fx;
        private RectTransform playerPortrait;
        private RectTransform cpuPortrait;
        private CanvasGroup playerGroup;
        private CanvasGroup cpuGroup;
        private CanvasGroup flashGroup;
        private Vector2 playerRest;
        private Vector2 cpuRest;
        private Vector3 playerRestScale;
        private Vector3 cpuRestScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            SaveTestSquad();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            controller = Object.FindAnyObjectByType<BattleScreenController>();
            fx = Object.FindAnyObjectByType<BattleFxPlayer>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True, "BattleScreenController が参照不足で無効化されました。");
            Assert.That(fx, Is.Not.Null);
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "編成が読み込めていません。");

            fx.ShakeSeed = BattleRoundPresentationPlan.DefaultShakeSeed;

            playerPortrait = fx.PlayerShakeTarget;
            cpuPortrait = fx.CpuShakeTarget;
            playerGroup = playerPortrait.GetComponent<CanvasGroup>();
            cpuGroup = cpuPortrait.GetComponent<CanvasGroup>();
            flashGroup = Find("FxFlash").GetComponent<CanvasGroup>();

            playerRest = playerPortrait.anchoredPosition;
            cpuRest = cpuPortrait.anchoredPosition;
            playerRestScale = playerPortrait.localScale;
            cpuRestScale = cpuPortrait.localScale;
        }

        [TearDown]
        public void TearDown()
        {
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- 入力ゲート ----------------

        [UnityTest]
        public IEnumerator MashingDeployAdvancesExactlyOneRound()
        {
            int before = controller.Coordinator.Session.CompletedRounds;

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            Assert.That(controller.IsRoundInProgress, Is.True);

            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                // 毎フレーム連打します。演出中は1回も通りません。
                controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);
                controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

                Assert.That(controller.Coordinator.Session.CompletedRounds, Is.LessThanOrEqualTo(before + 1));

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }

            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(before + 1));
            Assert.That(controller.Ring.Count, Is.EqualTo(BattleSquad.UnitCount - 1));
            Assert.That(controller.History.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InputStaysClosedThroughTheFadeOutAndReopensAfterwards()
        {
            bool fxBefore = controller.FxEnabled;
            int before = controller.Coordinator.Session.CompletedRounds;
            bool sawArchiving = false;
            bool sawPublishedWhileClosed = false;

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                if (controller.WheelPhase == BattleWheelPhase.Archiving)
                {
                    sawArchiving = true;
                }

                if (controller.History.Count == 0 &&
                    controller.Coordinator.Outcomes.GetOutcome(
                        controller.Coordinator.LastResult.PlayerUnit.InstanceId) != BattleSlotOutcome.None)
                {
                    // 結果公開の後、フェードとアーカイブの間。旧実装はここで入力が開いていました。
                    sawPublishedWhileClosed = true;
                }

                controller.OnFxToggleClicked();
                controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

                Assert.That(controller.FxEnabled, Is.EqualTo(fxBefore), "演出中にFXが切り替わりました。");
                Assert.That(controller.IsPresenting, Is.True);

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            Assert.That(sawArchiving, Is.True, "フェード中の段階を観測できませんでした。");
            Assert.That(sawPublishedWhileClosed, Is.True, "結果公開後のフェード中を観測できませんでした。");
            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(before + 1));

            // 終わった後は開きます。
            Assert.That(controller.IsPresenting, Is.False);
            Assert.That(controller.WheelPhase, Is.EqualTo(BattleWheelPhase.Selecting));

            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.Not.EqualTo(fxBefore), "演出後はFXを切り替えられます。");
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.EqualTo(fxBefore));

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);
            Assert.That(controller.IsRoundInProgress, Is.True, "演出後は次のDEPLOYが通ります。");

            yield return WaitRoundEnd();
        }

        // ---------------- 復元 ----------------

        [UnityTest]
        public IEnumerator TenRoundsInARowReturnThePortraitsToTheirInitialPose()
        {
            HashSet<RoundWinner> winners = new HashSet<RoundWinner>();

            for (int round = 0; round < 10; round++)
            {
                if (controller.Coordinator.State == BattleUiState.MatchFinished)
                {
                    controller.OnRematchClicked();
                    yield return null;
                }

                Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting), "round " + round);

                controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

                yield return WaitRoundEnd();

                winners.Add(fx.LastPlan.Winner);

                string label = "round " + (round + 1) + " (" + fx.LastPlan.Winner + ")";

                Assert.That(playerPortrait.anchoredPosition, Is.EqualTo(playerRest), label);
                Assert.That(cpuPortrait.anchoredPosition, Is.EqualTo(cpuRest), label);
                Assert.That(playerPortrait.localScale, Is.EqualTo(playerRestScale), label);
                Assert.That(cpuPortrait.localScale, Is.EqualTo(cpuRestScale), label);
                Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero), label);
                Assert.That(flashGroup.alpha, Is.EqualTo(0f), label);

                // 決着後は既存の片付けフェードで伏せたままです。明暗の復元は選択中に確かめます。
                if (controller.Coordinator.State == BattleUiState.Selecting)
                {
                    Assert.That(playerGroup.alpha, Is.EqualTo(1f), label);
                    Assert.That(cpuGroup.alpha, Is.EqualTo(1f), label);
                }
            }

            TestContext.WriteLine("observed winners: " + string.Join(", ", winners));
        }

        [UnityTest]
        public IEnumerator DisablingTheScreenMidImpactRestoresEverything()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForShake();

            controller.gameObject.SetActive(false);

            AssertPortraitsAtRest("直後");
            Assert.That(controller.IsRoundInProgress, Is.False);
            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(controller.History.Count, Is.EqualTo(1), "中断したラウンドも履歴に残ります。");
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            AssertPortraitsAtRest("30フレーム後");

            controller.gameObject.SetActive(true);
            yield return null;

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);
            Assert.That(controller.IsRoundInProgress, Is.True, "再開後は次のラウンドを始められます。");

            yield return WaitRoundEnd();

            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(2));
            AssertPortraitsAtRest("再開後のラウンド");
        }

        [UnityTest]
        public IEnumerator DisablingTheFxMidImpactRestoresThePoseAndTheRoundStillFinishes()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForShake();

            fx.gameObject.SetActive(false);

            AssertPortraitsAtRest("FX無効化の直後");

            yield return WaitRoundEnd();

            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(controller.History.Count, Is.EqualTo(1));

            fx.gameObject.SetActive(true);
            yield return null;

            AssertPortraitsAtRest("FX再有効化の後");
        }

        [UnityTest]
        public IEnumerator LeavingTheSceneMidImpactLeavesNothingRunning()
        {
            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            yield return WaitForShake();

            Scene battle = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("BattleImpactTests_Empty");

            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(battle);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.That(controller == null, Is.True, "コントローラが破棄されていません。");
            Assert.That(fx == null, Is.True, "演出プレイヤーが破棄されていません。");
            Assert.That(Object.FindAnyObjectByType<BattleScreenController>(), Is.Null);

            // 破棄済みの対象へ書き込む Coroutine が残っていれば、ここまでに例外が出ます。
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------- 勝敗ごとの見え方と振動対象 ----------------

        [UnityTest]
        public IEnumerator APlayerWinPushesBackOnlyTheCpuAndShakesOnlyThePortraits()
        {
            yield return PlayDirectClash(RoundWinner.Player, playerMax: 10f, cpuMax: 40f);
        }

        [UnityTest]
        public IEnumerator ACpuWinPushesBackOnlyThePlayerAndShakesOnlyThePortraits()
        {
            yield return PlayDirectClash(RoundWinner.Cpu, playerMax: 40f, cpuMax: 10f);
        }

        [UnityTest]
        public IEnumerator ADrawRecoilsBothSidesEquallyAndShakesOnlyThePortraits()
        {
            yield return PlayDirectClash(RoundWinner.Draw, playerMax: 18f, cpuMax: 18f);
        }

        [UnityTest]
        public IEnumerator TheHudNeverMovesDuringARealRound()
        {
            Dictionary<string, Vector3> hud = CaptureHud();

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            bool portraitMoved = false;
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                AssertHudUnchanged(hud);

                portraitMoved |= playerPortrait.anchoredPosition != playerRest;

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            AssertHudUnchanged(hud);
            Assert.That(portraitMoved, Is.True, "振動・突進の対象は実際に動きます。");
        }

        // ---------------- FX OFF ----------------

        [UnityTest]
        public IEnumerator FxOffTakesTheShortPathWithTheSameProgress()
        {
            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.False);

            controller.OnWheelDeployRequested(controller.Ring.FocusedInstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                Assert.That(fx.Step, Is.EqualTo(BattleFxPlayer.ClashStep.Idle));
                Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero));
                Assert.That(playerPortrait.anchoredPosition, Is.EqualTo(playerRest), "FX OFF では動かしません。");
                Assert.That(cpuPortrait.anchoredPosition, Is.EqualTo(cpuRest));

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            Assert.That(controller.Coordinator.Session.CompletedRounds, Is.EqualTo(1));
            Assert.That(controller.Ring.Count, Is.EqualTo(BattleSquad.UnitCount - 1));
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.Coordinator.State, Is.EqualTo(BattleUiState.Selecting));
            AssertPortraitsAtRest("FX OFF のラウンド後");

            controller.OnFxToggleClicked();
            Assert.That(controller.FxEnabled, Is.True);
        }

        // ---------------- 補助 ----------------

        private IEnumerator PlayDirectClash(RoundWinner winner, float playerMax, float cpuMax)
        {
            Dictionary<string, Vector3> hud = CaptureHud();

            Vector2 playerForward = Vector2.up;
            Vector2 cpuForward = Vector2.down;

            float playerRetreat = float.MinValue;
            float cpuRetreat = float.MinValue;
            bool shook = false;
            Vector2 playerContact = Vector2.zero;
            Vector2 cpuContact = Vector2.zero;

            fx.StartCoroutine(fx.PlayClashRoutine(winner, Color.white));

            yield return null;

            float startedAt = Time.realtimeSinceStartup;

            while (fx.IsPlaying)
            {
                AssertHudUnchanged(hud);

                if (fx.Step == BattleFxPlayer.ClashStep.ImpactHold)
                {
                    playerContact = playerPortrait.anchoredPosition;
                    cpuContact = cpuPortrait.anchoredPosition;
                }

                if (fx.Step == BattleFxPlayer.ClashStep.Release)
                {
                    Vector2 shake = fx.ShakeOffset;

                    shook |= shake != Vector2.zero;

                    Assert.That(shake.magnitude, Is.LessThanOrEqualTo(6f));

                    playerRetreat = Mathf.Max(
                        playerRetreat,
                        Vector2.Dot(playerContact - (playerPortrait.anchoredPosition - shake), playerForward));

                    cpuRetreat = Mathf.Max(
                        cpuRetreat,
                        Vector2.Dot(cpuContact - (cpuPortrait.anchoredPosition - shake), cpuForward));
                }

                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit));
            }

            AssertHudUnchanged(hud);

            Assert.That(shook, Is.True, "立ち絵は実際に振動します。");

            // 実フレーム時間は揺らぐため、最大値だけを見ます（ノックバック完了の瞬間に一致）。
            Assert.That(playerRetreat, Is.EqualTo(playerMax).Within(0.5f), "player");
            Assert.That(cpuRetreat, Is.EqualTo(cpuMax).Within(0.5f), "cpu");

            Assert.That(playerPortrait.anchoredPosition, Is.EqualTo(playerRest));
            Assert.That(cpuPortrait.anchoredPosition, Is.EqualTo(cpuRest));

            fx.ResetVisuals();

            AssertPortraitsAtRest(winner.ToString());
        }

        private IEnumerator WaitForShake()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (!(fx.Step == BattleFxPlayer.ClashStep.Release && fx.ShakeOffset != Vector2.zero))
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "振動の段まで進みません。");
            }

            Assert.That(playerPortrait.anchoredPosition, Is.Not.EqualTo(playerRest));
        }

        private IEnumerator WaitRoundEnd()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }
        }

        private void AssertPortraitsAtRest(string label)
        {
            Assert.That(playerPortrait.anchoredPosition, Is.EqualTo(playerRest), label);
            Assert.That(cpuPortrait.anchoredPosition, Is.EqualTo(cpuRest), label);
            Assert.That(playerPortrait.localScale, Is.EqualTo(playerRestScale), label);
            Assert.That(cpuPortrait.localScale, Is.EqualTo(cpuRestScale), label);
            Assert.That(playerGroup.alpha, Is.EqualTo(1f), label);
            Assert.That(cpuGroup.alpha, Is.EqualTo(1f), label);
            Assert.That(flashGroup.alpha, Is.EqualTo(0f), label);
            Assert.That(fx.ShakeOffset, Is.EqualTo(Vector2.zero), label);
            Assert.That(fx.IsPlaying, Is.False, label);
        }

        private Dictionary<string, Vector3> CaptureHud()
        {
            Dictionary<string, Vector3> hud = new Dictionary<string, Vector3>();

            foreach (string name in HudNames)
            {
                hud[name] = Find(name).position;
            }

            return hud;
        }

        private void AssertHudUnchanged(Dictionary<string, Vector3> hud)
        {
            foreach (KeyValuePair<string, Vector3> entry in hud)
            {
                Assert.That(Find(entry.Key).position, Is.EqualTo(entry.Value), entry.Key + " が動きました。");
            }
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SaveTestSquad()
        {
#if UNITY_EDITOR
            CoreBeastRoster roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);

            Assert.That(roster, Is.Not.Null, RosterPath + " を読めません。");
            Assert.That(roster.Owned.Count, Is.GreaterThanOrEqualTo(SquadFormation.SlotCount));

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            SquadRepositoryProvider.Shared.Save(SetId, new SquadSnapshot(ids));
#else
            Assert.Ignore("エディタ上でのみ実行します（テスト用ロスターをAssetDatabaseから読むため）。");
#endif
        }
    }
}
