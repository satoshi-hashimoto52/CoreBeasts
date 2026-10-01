using System;
using System.Collections.Generic;

using CoreBeasts.Units;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 循環リングの配置・回転・入力。
    ///
    /// 何を出すかは<see cref="BattleUnitRingModel"/>と<see cref="BattleRingPresentation"/>、
    /// どう見せるかは<see cref="BattleRingLayout"/>、
    /// 横と上の取り違えは<see cref="BattleWheelGestureStateMachine"/>が決めます。
    /// ここはUnity側（RectTransform・ポインター）との橋渡しだけです。
    ///
    /// 回しただけでは出撃しません。中央が変わったら
    /// <see cref="FocusChanged"/>で知らせるだけで、選出の確定は行いません。
    /// 上スライドが成立したときだけ<see cref="DeployRequested"/>を出します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleUnitWheelView : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private BattleUnitWheelItemView itemPrefab;

        [Tooltip("隣り合う個体の間隔（pt相当）。")]
        [SerializeField] private float itemSpacingPoints = 92f;

        [Tooltip("1ptあたりのCanvas単位。端末ごとの見た目をそろえます。")]
        [SerializeField] private float pointsToUnits = 2.43f;

        [Tooltip("上スライド中に中央を持ち上げる最大量（pt相当）。")]
        [SerializeField] private float maxLiftPoints = 26f;

        [Tooltip("成立直前に足す拡大。")]
        [SerializeField] [Range(1f, 1.2f)] private float commitScaleBoost = 1.06f;

        [Tooltip("スナップの戻り速度。大きいほど速く収まります。")]
        [SerializeField] [Range(4f, 30f)] private float snapSpeed = 14f;

        private readonly List<BattleUnitWheelItemView> items =
            new List<BattleUnitWheelItemView>();

        private readonly BattleWheelGestureStateMachine gesture =
            new BattleWheelGestureStateMachine();

        private BattleUnitRingModel model;
        private BattleSideRoster side;
        private AttributePalette palette;
        private UiTextCatalog text;

        /// <summary>
        /// いま画面へ出している位相（カード間隔単位）。
        /// 0 ならモデルの中央がちょうど真ん中に居ます。
        /// </summary>
        private float phase;

        /// <summary>
        /// Drag中にすでにリングへ反映した回転量。
        /// 累積移動からこれを差し引いた残りが<see cref="phase"/>です。
        /// これがあるので、指を離さずに何周でも回せます。
        /// </summary>
        private float rebased;

        private bool interactable;

        /// <summary>押した瞬間のローカル座標（pt）。累積移動はここからの差で測ります。</summary>
        private Vector2 startPoint;

        private Vector2 lastPoint;
        private float lastMoveTime;
        private Vector2 velocity;

        /// <summary>この操作で回転／出撃をすでに確定したか（二重確定よけ）。</summary>
        private bool resolved;

        /// <summary>中央が変わったときに発火します。選出は確定しません。</summary>
        public event Action<string> FocusChanged;

        /// <summary>上スライドが成立したときに発火します。</summary>
        public event Action<string> DeployRequested;

        /// <summary>出撃の持ち上げ具合（0〜1）。ガイドの強調へ使います。</summary>
        public float SlideProgress => gesture.SlideProgress;

        /// <summary>今この瞬間、中央を上へ動かしているか。</summary>
        public bool IsSlidingUp => gesture.IsSlidingUp;

        /// <summary>生成済みの表示枠。テストから確かめるために公開しています。</summary>
        public IReadOnlyList<BattleUnitWheelItemView> Items => items;

        /// <summary>いま画面へ出している位相（確認・テスト用）。</summary>
        public float Phase => phase;

        /// <summary>Drag中にリングへ反映した回転量（確認・テスト用）。</summary>
        public float RebasedSteps => rebased;

        /// <summary>1ptあたりのCanvas単位。</summary>
        public float PointsToUnits => pointsToUnits;

        /// <summary>個体間の間隔（pt相当）。</summary>
        public float ItemSpacingPoints => itemSpacingPoints;

        /// <summary>表示に使うデータを渡します。</summary>
        public void Bind(
            BattleUnitRingModel ringModel,
            AttributePalette attributePalette,
            UiTextCatalog uiText)
        {
            model = ringModel;
            palette = attributePalette;
            text = uiText;
        }

        /// <summary>編成の見た目データを差し替えます。</summary>
        public void SetSide(BattleSideRoster playerSide)
        {
            side = playerSide;
        }

        /// <summary>今リング中央にある個体。</summary>
        public string FocusedInstanceId =>
            model != null ? model.FocusedInstanceId : null;

        /// <summary>入力を受け付けるかどうかを切り替えます。</summary>
        public void SetInteractable(bool value)
        {
            interactable = value;

            if (!value)
            {
                gesture.Reset();
                phase = 0f;
                rebased = 0f;
            }
        }

        /// <summary>リング全体の表示を作り直します。</summary>
        public void Refresh()
        {
            if (model == null || content == null)
            {
                return;
            }

            bool empty = model.IsEmpty;

            if (content.gameObject.activeSelf == empty)
            {
                content.gameObject.SetActive(!empty);
            }

            if (empty)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].SetVisible(false);
                }

                return;
            }

            // 連続位相から、いま中央に近い順で出す個体を決めます。
            // 端という概念が無いので、左へ出た個体はその場で右側の距離を持ちます。
            IReadOnlyList<BattleRingPhaseSlot> visible =
                BattleRingPhase.Resolve(model, phase, BattleRingPhase.MaxVisible);

            EnsureCapacity(visible.Count);

            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetVisible(i < visible.Count);
            }

            // 遠いものから先に描き、中央を必ず最前面にします。
            List<BattleRingPhaseSlot> ordered =
                new List<BattleRingPhaseSlot>(visible);

            ordered.Sort((a, b) =>
                BattleRingPhase.DrawOrderOf(a.Distance)
                    .CompareTo(BattleRingPhase.DrawOrderOf(b.Distance)));

            for (int i = 0; i < ordered.Count; i++)
            {
                BattleRingPhaseSlot entry = ordered[i];
                BattleUnitWheelItemView item = items[i];

                item.Bind(entry.Slot, FindCard(entry.Slot.InstanceId), palette, text);
                item.Root.SetSiblingIndex(i);

                ApplyPlacement(item, entry);
            }
        }

        private void ApplyPlacement(
            BattleUnitWheelItemView item, BattleRingPhaseSlot entry)
        {
            float distance = entry.Distance;

            item.ApplySample(
                BattleRingLayout.Evaluate(distance),
                distance,
                BattleRingLayout.HorizontalOffset(distance, itemSpacingPoints),
                pointsToUnits);

            bool isCentre = (distance < 0f ? -distance : distance) < 0.5f;

            item.SetCentre(isCentre);

            if (isCentre && gesture.IsSlidingUp)
            {
                float progress = gesture.SlideProgress;

                item.AddLift(
                    maxLiftPoints * progress,
                    Mathf.Lerp(1f, commitScaleBoost, progress),
                    pointsToUnits);
            }
        }

        private void EnsureCapacity(int count)
        {
            while (items.Count < count)
            {
                BattleUnitWheelItemView item = Instantiate(itemPrefab, content);
                item.name = "WheelItem_" + (items.Count + 1);

                items.Add(item);
            }
        }

        private BattleUnitCard FindCard(string instanceId)
        {
            return side != null ? side.Find(instanceId) : null;
        }

        private void Update()
        {
            if (gesture.IsRotating || Mathf.Approximately(phase, 0f))
            {
                return;
            }

            // 指を離したあとは、いまの位相から中央へ滑らかに収めます。
            // 一気に 0 へ戻さないので、瞬間移動しません。
            phase = Mathf.MoveTowards(
                phase, 0f, Time.unscaledDeltaTime * snapSpeed);

            Refresh();
        }

        private void OnDisable()
        {
            gesture.Reset();

            phase = 0f;
            rebased = 0f;
        }

        // ---------------- 入力 ----------------

        /// <summary>
        /// 画面座標を、リングのローカル座標（pt相当）へ直します。
        ///
        /// 画面ピクセルのまま測ると、端末の解像度と Canvas.scaleFactor のぶんだけ
        /// 操作量が変わってしまいます。Canvas のローカル単位へ落としてから
        /// pt へ直すので、iPhone 12 mini でも 16 Pro でも同じ指の動きになります。
        /// </summary>
        private bool TryPoint(PointerEventData eventData, out Vector2 point)
        {
            point = Vector2.zero;

            RectTransform area = content != null
                ? content
                : transform as RectTransform;

            if (area == null || eventData == null || pointsToUnits <= 0f)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area, eventData.position, eventData.pressEventCamera,
                    out Vector2 local))
            {
                return false;
            }

            point = local / pointsToUnits;

            return true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            resolved = false;
            rebased = 0f;
            velocity = Vector2.zero;
            lastMoveTime = Time.unscaledTime;

            if (!TryPoint(eventData, out Vector2 point))
            {
                gesture.Begin(0f, 0f, false, false);
                return;
            }

            startPoint = point;
            lastPoint = point;

            // 押した場所が中央かどうかで、上スライドの可否が決まります。
            gesture.Begin(
                point.x,
                point.y,
                IsOnCentre(point),
                interactable && model != null && !model.IsEmpty);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 開始座標は上書きしません。累積移動の基準が動いてしまうためです。
            Track(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!TryPoint(eventData, out Vector2 point))
            {
                return;
            }

            Track(eventData, point);

            // 押した位置からの累積移動を渡します。1フレームぶんの差分ではありません。
            gesture.Move(point.x, point.y);

            if (gesture.IsRotating && model != null && model.CanRotate)
            {
                // 指が右へ動けば、カードも右へ動きます。
                AdvancePhase(gesture.HorizontalDistance / itemSpacingPoints);
            }

            Refresh();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Track(eventData);

            Resolve();
        }

        /// <summary>
        /// 指を離したとき。ドラッグにならなかったタップでは<see cref="OnEndDrag"/>が
        /// 呼ばれないため、ここで後始末します。
        /// すでに確定済みなら何もしません（回転や出撃を二度起こさないため）。
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (resolved)
            {
                return;
            }

            Resolve();
        }

        /// <summary>
        /// 回転または出撃を、1回の操作につき1度だけ確定します。
        /// </summary>
        private void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;

            float upwardVelocity = velocity.y;
            float horizontalVelocity = velocity.x;

            if (gesture.ShouldDeploy(upwardVelocity))
            {
                string focused = model != null ? model.FocusedInstanceId : null;

                gesture.End();
                phase = 0f;
                rebased = 0f;
                Refresh();

                if (!string.IsNullOrEmpty(focused))
                {
                    DeployRequested?.Invoke(focused);
                }

                return;
            }

            // 残っている位相ぶんだけをスナップの対象にします。
            // Drag中にすでに回したぶんは二度数えません。
            bool rotatedWhileDragging = !Mathf.Approximately(rebased, 0f);

            int steps = gesture.ResolveSnapSteps(-phase, horizontalVelocity);

            gesture.End();

            rebased = 0f;

            if (steps != 0 && model != null && model.Rotate(steps))
            {
                // 中央が steps ぶん進んだので、位相も同じだけ付け替えます。
                // いま指の下にあるカードはその場に留まったまま、新しい中央へ収まります。
                phase += steps;

                Refresh();

                FocusChanged?.Invoke(model.FocusedInstanceId);

                return;
            }

            Refresh();

            if (rotatedWhileDragging && model != null)
            {
                // Drag中に中央が動いたなら、指を離した時点の中央を知らせます。
                FocusChanged?.Invoke(model.FocusedInstanceId);
            }
        }

        /// <summary>
        /// 累積移動から位相を更新し、1枚ぶんを越えるたびにリングを回します。
        ///
        /// ここで回すので、指を離さずに何周でも循環できます。
        /// 回したぶんは<see cref="rebased"/>へ控え、累積移動から差し引くため、
        /// 同じ移動を二度反映することはありません。
        /// </summary>
        private void AdvancePhase(float totalOffset)
        {
            phase = totalOffset - rebased;

            // 右へ1枚ぶん動いたら、左隣の個体が中央になります。
            while (phase >= 1f && model.Rotate(-1))
            {
                rebased += 1f;
                phase = totalOffset - rebased;
            }

            while (phase <= -1f && model.Rotate(1))
            {
                rebased -= 1f;
                phase = totalOffset - rebased;
            }
        }

        private void Track(PointerEventData eventData)
        {
            if (TryPoint(eventData, out Vector2 point))
            {
                Track(eventData, point);
            }
        }

        /// <summary>速度もローカル座標（pt/秒）で測ります。</summary>
        private void Track(PointerEventData eventData, Vector2 point)
        {
            float now = Time.unscaledTime;
            float delta = now - lastMoveTime;

            if (delta > 0.0001f)
            {
                velocity = (point - lastPoint) / delta;
            }

            lastPoint = point;
            lastMoveTime = now;
        }

        /// <summary>
        /// 押した位置が中央の個体の上かどうか。
        /// 中央以外から上へ動かしても出撃しないのは、この判定のためです。
        /// </summary>
        private bool IsOnCentre(Vector2 point)
        {
            if (model == null || model.IsEmpty)
            {
                return false;
            }

            float half = itemSpacingPoints * 0.5f;
            float distance = point.x < 0f ? -point.x : point.x;

            return distance <= half;
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleUnitWheelView),
                ReferenceCheck.Of(nameof(content), content),
                ReferenceCheck.Of(nameof(itemPrefab), itemPrefab));
        }
    }
}
