using NUnit.Framework;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 横スワイプと上スライドの方向ロック。座標はすべて pt 相当です。
    /// Unityへ依存しないため、そのまま実行できます。
    /// </summary>
    public sealed class BattleWheelGestureTests
    {
        private BattleWheelGestureStateMachine gesture;

        [SetUp]
        public void SetUp()
        {
            gesture = new BattleWheelGestureStateMachine();
        }

        /// <summary>中央個体を、操作できる状態で押します。</summary>
        private void PressCentre()
        {
            gesture.Begin(0f, 0f, true, true);
        }

        // ---------------- 方向未確定 ----------------

        [Test]
        public void TouchingAloneDecidesNothing()
        {
            PressCentre();

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Pending));

            gesture.Move(4f, 3f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.Pending),
                "12pt 動くまでは方向を決めません。");
        }

        [Test]
        public void TheDirectionLocksOnceTheThresholdIsPassed()
        {
            PressCentre();

            gesture.Move(11f, 0f);
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Pending));

            gesture.Move(13f, 0f);
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rotating));
        }

        // ---------------- 横 ----------------

        [Test]
        public void ASidewaysDragRotatesTheRing()
        {
            PressCentre();

            gesture.Move(-60f, 6f);

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rotating));
            Assert.That(gesture.IsRotating, Is.True);
            Assert.That(gesture.IsSlidingUp, Is.False);
        }

        [Test]
        public void ASidewaysDragNeverDeploysEvenIfItLaterGoesUp()
        {
            PressCentre();

            gesture.Move(-60f, 0f);
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rotating));

            // そのまま大きく上へ動かしても、方向は変わりません。
            gesture.Move(-60f, 300f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.Rotating),
                "一度横と決めたら、指を離すまで上スライドにはなりません。");

            Assert.That(
                gesture.ShouldDeploy(5000f),
                Is.False,
                "横スワイプ中に誤って出撃してはいけません。");
        }

        // ---------------- 上 ----------------

        [Test]
        public void AnUpwardDragBecomesADeployCandidate()
        {
            PressCentre();

            gesture.Move(4f, 40f);

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.SlidingUp));
            Assert.That(gesture.IsSlidingUp, Is.True);
            Assert.That(gesture.IsRotating, Is.False);
        }

        [Test]
        public void AnUpwardDragNeverRotatesEvenIfItLaterGoesSideways()
        {
            PressCentre();

            gesture.Move(0f, 40f);
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.SlidingUp));

            gesture.Move(400f, 40f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.SlidingUp),
                "上スライド中にリングが回ってはいけません。");

            Assert.That(gesture.ResolveRotationSteps(3000f, 100f), Is.EqualTo(0));
        }

        [Test]
        public void ADiagonalDragDoesNotDeploy()
        {
            PressCentre();

            // 上へ 40、横へ 36。上が横の 1.25 倍に届かないため出撃候補になりません。
            gesture.Move(36f, 40f);

            Assert.That(
                gesture.State,
                Is.Not.EqualTo(BattleWheelGesture.SlidingUp),
                "斜め入力で誤って出撃してはいけません。");
        }

        [Test]
        public void ADownwardDragIsNeverADeploy()
        {
            PressCentre();

            gesture.Move(0f, -120f);

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rejected));
            Assert.That(gesture.ShouldDeploy(5000f), Is.False);
        }

        [Test]
        public void OnlyTheCentreUnitCanBeDeployed()
        {
            gesture.Begin(0f, 0f, false, true);

            gesture.Move(0f, 200f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.Rejected),
                "中央以外から上へ動かしても出撃しません。");

            Assert.That(gesture.ShouldDeploy(5000f), Is.False);
        }

        [Test]
        public void ANonCentreUnitCanStillRotateTheRing()
        {
            gesture.Begin(0f, 0f, false, true);

            gesture.Move(-80f, 0f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.Rotating),
                "中央以外でも横回転はできます。");
        }

        // ---------------- 成立と不成立 ----------------

        [Test]
        public void AShortSlideDoesNotCommit()
        {
            PressCentre();

            gesture.Move(0f, 60f);

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.SlidingUp));
            Assert.That(
                gesture.ShouldDeploy(0f),
                Is.False,
                "閾値未満ではキャンセルします。");
        }

        [Test]
        public void ALongEnoughSlideCommits()
        {
            PressCentre();

            gesture.Move(0f, BattleWheelGestureStateMachine.CommitDistance);

            Assert.That(gesture.ShouldDeploy(0f), Is.True);
        }

        [Test]
        public void AFastFlickUpCommitsEvenWhenShort()
        {
            PressCentre();

            gesture.Move(0f, 50f);

            Assert.That(gesture.ShouldDeploy(0f), Is.False);
            Assert.That(
                gesture.ShouldDeploy(BattleWheelGestureStateMachine.CommitVelocity),
                Is.True,
                "十分な上向き速度でも成立します。");
        }

        [Test]
        public void TheSlideProgressGrowsTowardsTheCommitPoint()
        {
            PressCentre();

            gesture.Move(0f, 20f);
            float early = gesture.SlideProgress;

            gesture.Move(0f, 80f);
            float late = gesture.SlideProgress;

            Assert.That(early, Is.GreaterThan(0f));
            Assert.That(late, Is.GreaterThan(early), "成立に近づくほど進みます。");
            Assert.That(late, Is.LessThanOrEqualTo(1f));

            gesture.Move(0f, 500f);
            Assert.That(gesture.SlideProgress, Is.EqualTo(1f), "1を超えません。");
        }

        [Test]
        public void ReleasingEndsTheGestureSoTheNextOneStartsFresh()
        {
            PressCentre();
            gesture.Move(-80f, 0f);
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rotating));

            gesture.End();
            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Idle));

            PressCentre();
            gesture.Move(0f, 200f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.SlidingUp),
                "次の操作では方向を決め直せます。");
        }

        // ---------------- 回転量 ----------------

        [Test]
        public void ASlowDragPastTheOneStepRatioTurnsOneUnit()
        {
            PressCentre();

            // カード間隔の 35% を超えれば、速度が無くても1体進みます。
            gesture.Move(-100f * BattleWheelGestureStateMachine.OneStepRatio - 1f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(0f, 100f),
                Is.EqualTo(1),
                "累積移動が間隔の35%を超えたら1体進みます。");
        }

        [Test]
        public void JustUnderTheOneStepRatioSnapsBack()
        {
            PressCentre();

            gesture.Move(-100f * BattleWheelGestureStateMachine.OneStepRatio + 1f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(0f, 100f),
                Is.EqualTo(0),
                "35%に届かなければ元の中央へ戻します。");
        }

        [Test]
        public void PastTheTwoStepRatioTurnsTwoUnits()
        {
            PressCentre();

            gesture.Move(-100f * BattleWheelGestureStateMachine.TwoStepRatio - 1f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(0f, 100f),
                Is.EqualTo(2),
                "累積移動が間隔の135%を超えたら2体進みます。");
        }

        [Test]
        public void AShortFlickTurnsExactlyOneUnit()
        {
            PressCentre();
            gesture.Move(-20f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(-400f, 100f),
                Is.EqualTo(1),
                "短いフリックで1体回ります。");
        }

        [Test]
        public void AStrongFlickNeverTurnsMoreThanTwo()
        {
            PressCentre();
            gesture.Move(-900f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(-9000f, 100f),
                Is.EqualTo(2),
                "強いフリックでも最大2体です。何周も回りません。");

            PressCentre();
            gesture.Move(900f, 0f);

            Assert.That(gesture.ResolveRotationSteps(9000f, 100f), Is.EqualTo(-2));
        }

        [Test]
        public void ATinyDragWithoutSpeedDoesNotRotate()
        {
            PressCentre();
            gesture.Move(-14f, 0f);

            Assert.That(
                gesture.ResolveRotationSteps(0f, 100f),
                Is.EqualTo(0),
                "指を離した位置が中央に近ければ、そのまま戻ります。");
        }

        // ---------------- 入力を止める ----------------

        [Test]
        public void NothingHappensWhileInputIsBlocked()
        {
            gesture.Begin(0f, 0f, true, false);

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Rejected));

            gesture.Move(0f, 400f);

            Assert.That(
                gesture.State,
                Is.EqualTo(BattleWheelGesture.Rejected),
                "設定パネル表示中・演出中・決着後は受け付けません。");

            Assert.That(gesture.ShouldDeploy(9000f), Is.False);
            Assert.That(gesture.ResolveRotationSteps(9000f, 100f), Is.EqualTo(0));
        }

        [Test]
        public void ResetThrowsTheGestureAway()
        {
            PressCentre();
            gesture.Move(0f, 120f);

            gesture.Reset();

            Assert.That(gesture.State, Is.EqualTo(BattleWheelGesture.Idle));
            Assert.That(gesture.UpwardDistance, Is.EqualTo(0f));
            Assert.That(gesture.ShouldDeploy(9000f), Is.False);
        }

        // ---------------- 段階ごとの入力可否 ----------------

        [Test]
        public void OnlySelectingAcceptsRingInput()
        {
            Assert.That(
                BattleWheelPhases.AllowsInput(BattleWheelPhase.Selecting, false),
                Is.True);

            BattleWheelPhase[] blocked =
            {
                BattleWheelPhase.Idle,
                BattleWheelPhase.Deploying,
                BattleWheelPhase.Resolving,
                BattleWheelPhase.ShowingResult,
                BattleWheelPhase.Archiving,
                BattleWheelPhase.Finished,
            };

            for (int i = 0; i < blocked.Length; i++)
            {
                Assert.That(
                    BattleWheelPhases.AllowsInput(blocked[i], false),
                    Is.False,
                    blocked[i] + " では操作させません。");
            }

            Assert.That(
                BattleWheelPhases.AllowsInput(BattleWheelPhase.Selecting, true),
                Is.False,
                "設定パネル表示中は操作させません。");
        }

        [Test]
        public void MovingAndArchivingOverrideTheFlowState()
        {
            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.Selecting, true, false),
                Is.EqualTo(BattleWheelPhase.Deploying));

            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.ShowingResult, false, true),
                Is.EqualTo(BattleWheelPhase.Archiving));

            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.Selecting, false, false),
                Is.EqualTo(BattleWheelPhase.Selecting));

            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.Resolving, false, false),
                Is.EqualTo(BattleWheelPhase.Resolving));

            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.MatchFinished, false, false),
                Is.EqualTo(BattleWheelPhase.Finished));

            Assert.That(
                BattleWheelPhases.Resolve(BattleUiState.SquadRequired, false, false),
                Is.EqualTo(BattleWheelPhase.Idle));
        }
    }
}
