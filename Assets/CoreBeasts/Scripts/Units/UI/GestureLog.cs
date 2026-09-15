using UnityEngine;
using UnityEngine.EventSystems;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 操作イベントの調査用ログ。
    /// UnitSetScreenのLog Gesture EventsをONにしたときだけ出力します。
    /// </summary>
    public static class GestureLog
    {
        /// <summary>出力するかどうか。</summary>
        public static bool Enabled { get; set; }

        public static void Write(
            string source,
            string eventName,
            PointerEventData eventData,
            float elapsedSeconds,
            float movedPixels,
            CardGestureState state,
            string instanceId,
            int slotIndex)
        {
            if (!Enabled)
            {
                return;
            }

            GameObject hit = eventData != null &&
                             eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject
                : null;

            Debug.Log(
                $"[Gesture] {eventName,-26} src={source} " +
                $"pointerId={(eventData != null ? eventData.pointerId : 0)} " +
                $"t={elapsedSeconds:F3}s move={movedPixels:F1}px " +
                $"state={state} instance={(string.IsNullOrEmpty(instanceId) ? "-" : instanceId)} " +
                $"slot={slotIndex} ray={(hit != null ? hit.name : "-")}"
            );
        }
    }
}
