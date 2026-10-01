using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 履歴レーンは常に7枠。1回戦から空白に見えず、2回戦で1枠だけ孤立しません。
    ///
    /// 枠は最初にそろえて、進行につれて「点灯する枠が増える」だけにします。
    /// ノードが後から生えてレイアウトが動いて見えることがありません。
    /// </summary>
    public sealed class BattleHistoryLaneStateTests
    {
        private const string ScenePath = "Assets/CoreBeasts/Scenes/Battle.unity";

        private Scene scene;
        private BattleHistoryLaneView lane;

        [SetUp]
        public void SetUp()
        {
            // Additive だと、直前のテストが開いたシーンと同居して
            // Global Light 2D が2つになり、URP 2D が Error を出します。
            // NUnit は想定外の Error ログを失敗として扱うため、常に単独で開きます。
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assume.That(scene.IsValid(), Is.True, ScenePath + " を開けません。");

            List<BattleHistoryLaneView> found = new List<BattleHistoryLaneView>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<BattleHistoryLaneView>(true));
            }

            Assert.That(found.Count, Is.EqualTo(1), "履歴レーンは1個だけです。");

            lane = found[0];

            // Build() は入力を締め直します。保存されている値そのものを
            // 確かめられるよう、締め直す前の状態を控えておきます。
            CanvasGroup group = (CanvasGroup)GetField(lane, "canvasGroup");

            authoredBlocksRaycasts = group != null && group.blocksRaycasts;
            authoredInteractable = group != null && group.interactable;

            lane.Build();
        }

        private bool authoredBlocksRaycasts;
        private bool authoredInteractable;

        [TearDown]
        public void TearDown()
        {
            // 単独で開いているときは閉じられません（唯一のシーンは閉じられない）。
            // 次の SetUp が Single で開き直すので、ここでは残しておきます。
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static BattleHistoryModel HistoryOf(params BattleSlotOutcome[] outcomes)
        {
            BattleHistoryModel history = new BattleHistoryModel();

            for (int i = 0; i < outcomes.Length; i++)
            {
                history.Append("p" + i, i + 1, outcomes[i]);
            }

            return history;
        }

        private void Show(BattleHistoryModel history)
        {
            lane.Show(history, null, null, null);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, name + " が見つかりません。");

            return field.GetValue(target);
        }

        // ---------------- 常に7枠 ----------------

        [Test]
        public void TheLaneAlwaysHoldsSevenSlots()
        {
            Assert.That(
                lane.Slots.Count,
                Is.EqualTo(BattleHistoryModel.MaxEntries),
                "1回戦から7枠そろえます。");

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Assert.That(
                    lane.Slots[i].gameObject.activeSelf,
                    Is.True,
                    (i + 1) + "番目の枠が非表示です。空きも暗いソケットとして出します。");
            }
        }

        [Test]
        public void RoundOneShowsSevenUnusedSockets()
        {
            Show(HistoryOf());

            Assert.That(lane.Slots.Count, Is.EqualTo(7));
            Assert.That(lane.UsedCount, Is.EqualTo(0), "1回戦は使用済みゼロです。");
            Assert.That(lane.LitCount, Is.EqualTo(0), "1回戦は点灯ゼロです。");

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                BattleHistorySlotView slot = lane.Slots[i];

                Assert.That(slot.gameObject.activeSelf, Is.True);
                Assert.That(slot.IsUsed, Is.False);
                Assert.That(slot.IsLit, Is.False);
                Assert.That(
                    slot.IsBadgeVisible,
                    Is.False,
                    "未使用の枠に W/L/D を出してはいけません。");

                TMP_Text order = (TMP_Text)GetField(slot, "orderLabel");

                Assert.That(
                    string.IsNullOrEmpty(order.text),
                    Is.True,
                    "未使用の枠に編成番号を出してはいけません。");
            }
        }

        [Test]
        public void RoundTwoLightsExactlyOneSlotAndLeavesSixSockets()
        {
            Show(HistoryOf(BattleSlotOutcome.Win));

            Assert.That(lane.Slots.Count, Is.EqualTo(7), "枠の数は変わりません。");
            Assert.That(lane.UsedCount, Is.EqualTo(1));
            Assert.That(lane.LitCount, Is.EqualTo(1));

            Assert.That(lane.Slots[0].IsUsed, Is.True, "結果は左端から入ります。");
            Assert.That(lane.Slots[0].IsLit, Is.True);
            Assert.That(lane.Slots[0].IsBadgeVisible, Is.True);

            for (int i = 1; i < lane.Slots.Count; i++)
            {
                Assert.That(lane.Slots[i].gameObject.activeSelf, Is.True);
                Assert.That(lane.Slots[i].IsUsed, Is.False);
                Assert.That(lane.Slots[i].IsBadgeVisible, Is.False);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void OnlyThePublishedRoundsAreLit(int published)
        {
            BattleSlotOutcome[] outcomes = new BattleSlotOutcome[published];

            for (int i = 0; i < published; i++)
            {
                outcomes[i] = i % 3 == 0
                    ? BattleSlotOutcome.Win
                    : (i % 3 == 1 ? BattleSlotOutcome.Loss : BattleSlotOutcome.Draw);
            }

            Show(HistoryOf(outcomes));

            Assert.That(lane.Slots.Count, Is.EqualTo(7), "枠は常に7つです。");
            Assert.That(lane.LitCount, Is.EqualTo(published));
            Assert.That(lane.UsedCount, Is.EqualTo(published));

            for (int i = 0; i < 7; i++)
            {
                Assert.That(
                    lane.Slots[i].IsLit,
                    Is.EqualTo(i < published),
                    (i + 1) + "番目の点灯が公開済み数と合いません。");

                Assert.That(
                    lane.Slots[i].IsBadgeVisible,
                    Is.EqualTo(i < published),
                    (i + 1) + "番目のバッジ表示が公開済み数と合いません。");
            }
        }

        [Test]
        public void APendingRoundIsNotLitYet()
        {
            // 結果公開前は履歴へ入りません。入っていない枠は点灯しません。
            BattleHistoryModel history = HistoryOf(BattleSlotOutcome.Win);

            Assert.That(
                history.Append("p1", 2, BattleSlotOutcome.None),
                Is.False,
                "Pending（結果なし）を履歴へ入れてはいけません。");

            Show(history);

            Assert.That(lane.LitCount, Is.EqualTo(1), "Pending中は点灯しません。");
            Assert.That(lane.Slots[1].IsLit, Is.False);
            Assert.That(lane.Slots[1].IsBadgeVisible, Is.False);
        }

        [Test]
        public void RematchReturnsAllSevenSlotsToUnused()
        {
            Show(HistoryOf(
                BattleSlotOutcome.Win,
                BattleSlotOutcome.Loss,
                BattleSlotOutcome.Draw,
                BattleSlotOutcome.Win));

            Assert.That(lane.LitCount, Is.EqualTo(4));

            lane.Clear();

            Assert.That(lane.Slots.Count, Is.EqualTo(7), "REMATCHで枠を消しません。");
            Assert.That(lane.UsedCount, Is.EqualTo(0));
            Assert.That(lane.LitCount, Is.EqualTo(0));

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Assert.That(lane.Slots[i].gameObject.activeSelf, Is.True);
                Assert.That(lane.Slots[i].IsUsed, Is.False);
                Assert.That(lane.Slots[i].IsBadgeVisible, Is.False);
                Assert.That(lane.Slots[i].Outcome, Is.EqualTo(BattleSlotOutcome.None));
            }
        }

        // ---------------- 並びが動かない ----------------

        [Test]
        public void TheNodePositionsNeverMoveAsRoundsAreAdded()
        {
            Canvas.ForceUpdateCanvases();

            Vector3[] before = new Vector3[7];

            for (int i = 0; i < 7; i++)
            {
                before[i] = lane.Slots[i].transform.position;
            }

            Show(HistoryOf(
                BattleSlotOutcome.Win,
                BattleSlotOutcome.Loss,
                BattleSlotOutcome.Draw));

            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < 7; i++)
            {
                Assert.That(
                    lane.Slots[i].transform.position.x,
                    Is.EqualTo(before[i].x).Within(0.01f),
                    (i + 1) + "番目のノードが動きました。並びが動いて見えます。");
            }
        }

        [Test]
        public void TheNodesAreEvenlySpaced()
        {
            Canvas.ForceUpdateCanvases();

            List<float> gaps = new List<float>();

            for (int i = 1; i < lane.Slots.Count; i++)
            {
                gaps.Add(
                    lane.Slots[i].transform.position.x -
                    lane.Slots[i - 1].transform.position.x);
            }

            Assert.That(gaps.Count, Is.EqualTo(6));

            for (int i = 1; i < gaps.Count; i++)
            {
                Assert.That(
                    gaps[i],
                    Is.EqualTo(gaps[0]).Within(0.5f),
                    "ノードの間隔が均等ではありません。");
            }

            Assert.That(gaps[0], Is.GreaterThan(0f), "左から右へ並べます。");
        }

        // ---------------- 入力を奪わない ----------------

        [Test]
        public void TheLaneNeverTakesInput()
        {
            CanvasGroup group = (CanvasGroup)GetField(lane, "canvasGroup");

            Assert.That(group, Is.Not.Null);
            Assert.That(group.blocksRaycasts, Is.False, "履歴は入力を受けません。");
            Assert.That(
                group.interactable,
                Is.False,
                "HistoryLane の CanvasGroup.interactable が有効です。");

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Graphic[] graphics =
                    lane.Slots[i].GetComponentsInChildren<Graphic>(true);

                for (int g = 0; g < graphics.Length; g++)
                {
                    Assert.That(
                        graphics[g].raycastTarget,
                        Is.False,
                        graphics[g].name + " が入力を奪っています。");
                }
            }

            // 状態が変わっても同じであること。
            // とくに OutcomeBadge は未使用のあいだ非表示なので、
            // 公開してから確かめないと設定の誤りに気付けません。
            AssertNoInput("7枠未使用");

            Show(HistoryOf(BattleSlotOutcome.Win));
            AssertNoInput("Win公開後");

            Show(HistoryOf(BattleSlotOutcome.Loss));
            AssertNoInput("Loss公開後");

            Show(HistoryOf(BattleSlotOutcome.Draw));
            AssertNoInput("Draw公開後");

            Show(HistoryOf(
                BattleSlotOutcome.Win,
                BattleSlotOutcome.Loss,
                BattleSlotOutcome.Draw,
                BattleSlotOutcome.Win,
                BattleSlotOutcome.Loss,
                BattleSlotOutcome.Draw,
                BattleSlotOutcome.Win));
            AssertNoInput("7枠すべて公開後");

            lane.Clear();
            AssertNoInput("REMATCH後");
        }

        /// <summary>
        /// レーン配下のどこも入力を取らないこと。
        /// 実際に生成された枠を <c>GetComponentsInChildren</c> で全走査します。
        /// </summary>
        private void AssertNoInput(string because)
        {
            CanvasGroup laneGroup = (CanvasGroup)GetField(lane, "canvasGroup");

            Assert.That(
                laneGroup.blocksRaycasts,
                Is.False,
                because + ": HistoryLane が raycast を塞いでいます。");

            Assert.That(
                laneGroup.interactable,
                Is.False,
                because + ": HistoryLane が操作可能になっています。");

            Graphic[] all = lane.GetComponentsInChildren<Graphic>(true);

            Assert.That(all.Length, Is.GreaterThan(0), because + ": 走査対象がありません。");

            for (int i = 0; i < all.Length; i++)
            {
                Assert.That(
                    all[i].raycastTarget,
                    Is.False,
                    because + ": " + Path(all[i].transform) + " が入力を奪っています。");
            }

            CanvasGroup[] groups = lane.GetComponentsInChildren<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
            {
                Assert.That(
                    groups[i].blocksRaycasts,
                    Is.False,
                    because + ": " + Path(groups[i].transform) +
                    " の CanvasGroup が raycast を塞いでいます。");

                Assert.That(
                    groups[i].interactable,
                    Is.False,
                    because + ": " + Path(groups[i].transform) +
                    " の CanvasGroup が操作可能になっています。");
            }

            // 押せる部品そのものを置きません。
            Assert.That(
                lane.GetComponentsInChildren<Selectable>(true),
                Is.Empty,
                because + ": 履歴に Selectable（Button など）があります。");

            Assert.That(
                lane.GetComponentsInChildren<EventTrigger>(true),
                Is.Empty,
                because + ": 履歴に EventTrigger があります。");

            // 枠は消しません。7個そろったままです。
            Assert.That(
                lane.Slots.Count,
                Is.EqualTo(BattleHistoryModel.MaxEntries),
                because + ": 枠の数が変わりました。");

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Assert.That(
                    lane.Slots[i].gameObject.activeSelf,
                    Is.True,
                    because + ": 入力を防ぐために枠を消してはいけません。");
            }
        }

        private static string Path(Transform node)
        {
            string path = node.name;

            while (node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }

            return path;
        }

        [Test]
        public void TheLaneIsAuthoredWithInputAlreadyBlocked()
        {
            // Build() が締め直す前の、保存されている値そのものを見ます。
            // ここが緩いと、Awake の走らない場面で入力を取り得ます。
            Assert.That(
                authoredBlocksRaycasts,
                Is.False,
                "Battle.unity の HistoryLane に blocksRaycasts が保存されています。");

            Assert.That(
                authoredInteractable,
                Is.False,
                "Battle.unity の HistoryLane に interactable が保存されています。");
        }

        [Test]
        public void TheSlotPrefabIsAuthoredWithInputAlreadyBlocked()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CoreBeasts/Prefabs/BattleHistorySlot.prefab");

            Assert.That(asset, Is.Not.Null, "BattleHistorySlot.prefab が読めません。");

            Graphic[] graphics = asset.GetComponentsInChildren<Graphic>(true);

            Assert.That(graphics.Length, Is.GreaterThan(0));

            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(
                    graphics[i].raycastTarget,
                    Is.False,
                    "Prefab の " + graphics[i].name + " に raycastTarget が保存されています。");
            }

            CanvasGroup[] groups = asset.GetComponentsInChildren<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
            {
                Assert.That(
                    groups[i].blocksRaycasts,
                    Is.False,
                    "Prefab の " + groups[i].name +
                    " の CanvasGroup に blocksRaycasts が保存されています。");

                Assert.That(
                    groups[i].interactable,
                    Is.False,
                    "Prefab の " + groups[i].name +
                    " の CanvasGroup に interactable が保存されています。");
            }

            Assert.That(asset.GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(asset.GetComponentsInChildren<EventTrigger>(true), Is.Empty);
        }

        [Test]
        public void TheHistoryComponentsDoNotHandlePointerInput()
        {
            // 押せないことを、実装しているインターフェースの側からも見ます。
            Assert.That(
                lane,
                Is.Not.InstanceOf<IEventSystemHandler>(),
                "BattleHistoryLaneView が入力Handlerを実装しています。");

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Assert.That(
                    lane.Slots[i],
                    Is.Not.InstanceOf<IEventSystemHandler>(),
                    "BattleHistorySlotView が入力Handlerを実装しています。");
            }
        }

        [Test]
        public void TheSevenNodesFitInsideTheSafeArea()
        {
            Canvas.ForceUpdateCanvases();

            RectTransform safe = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = root.transform.Find("SafeArea");

                if (found != null)
                {
                    safe = (RectTransform)found;
                }
            }

            Assert.That(safe, Is.Not.Null, "SafeArea が見つかりません。");

            Vector3[] corners = new Vector3[4];
            safe.GetWorldCorners(corners);

            for (int i = 0; i < lane.Slots.Count; i++)
            {
                Vector3[] node = new Vector3[4];
                ((RectTransform)lane.Slots[i].transform).GetWorldCorners(node);

                Assert.That(
                    node[0].x,
                    Is.GreaterThanOrEqualTo(corners[0].x - 0.5f),
                    (i + 1) + "番目のノードが SafeArea の左へはみ出しています。");

                Assert.That(
                    node[2].x,
                    Is.LessThanOrEqualTo(corners[2].x + 0.5f),
                    (i + 1) + "番目のノードが SafeArea の右へはみ出しています。");
            }
        }
    }
}
