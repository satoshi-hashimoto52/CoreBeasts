using System.Collections;

using CoreBeasts.Units;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// リング中央から戦闘エリアへの移動表示。
    ///
    /// 「消して、別の場所へ出す」ではなく「同じ個体が動いた」ように見せます。
    /// そのため、リング側の見た目をそのまま持った表示用ゴーストを1体だけ作り、
    /// リング位置から戦闘位置へ動かしてから捨てます。
    ///
    /// 二重表示を作らないため、ゴーストは常に1体までです。
    /// 中断（HOME・OnDisable）でも必ず捨てます。
    /// 新しい画像素材は足しません。リング項目と同じプレハブを流用します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleDeployTransitionView : MonoBehaviour
    {
        [Tooltip("ゴーストを置く最前面のコンテナ。")]
        [SerializeField] private RectTransform ghostRoot;

        [Tooltip("リング項目と同じ見た目を使います。")]
        [SerializeField] private BattleUnitWheelItemView ghostPrefab;

        [Tooltip("FX ON のときの移動時間（秒）。")]
        [SerializeField] [Range(0.25f, 0.4f)] private float fxSeconds = 0.32f;

        [Tooltip("FX OFF のときの移動時間（秒）。短くても移動は残します。")]
        [SerializeField] [Range(0.1f, 0.15f)] private float plainSeconds = 0.12f;

        [Tooltip("到着時の拡大率。戦闘表示の大きさへ寄せます。")]
        [SerializeField] [Range(0.6f, 2f)] private float arrivalScale = 1.35f;

        private BattleUnitWheelItemView ghost;

        /// <summary>今ゴーストを出しているか。</summary>
        public bool IsPlaying => ghost != null;

        /// <summary>
        /// リング位置から戦闘位置へ1体を動かします。
        /// 呼び出し側は、この間ずっと戦闘側の正式Viewを伏せておきます。
        /// </summary>
        public IEnumerator PlayRoutine(
            BattleRingSlot slot,
            BattleUnitCard card,
            RectTransform from,
            RectTransform to,
            AttributePalette palette,
            UiTextCatalog text,
            bool fxEnabled)
        {
            Cancel();

            if (ghostRoot == null || ghostPrefab == null || from == null || to == null)
            {
                yield break;
            }

            ghost = Instantiate(ghostPrefab, ghostRoot);
            ghost.name = "DeployGhost";
            ghost.Bind(slot, card, palette, text);
            ghost.SetCentre(false);

            RectTransform moving = ghost.Root;

            Vector2 start = LocalPointOf(from);
            Vector2 end = LocalPointOf(to);

            float duration = fxEnabled ? fxSeconds : plainSeconds;
            float startScale = BattleRingLayout.CenterScale;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

                // 立ち上がりを速く、着地を穏やかに。
                float eased = 1f - (1f - t) * (1f - t);

                moving.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);

                float scale = Mathf.LerpUnclamped(startScale, arrivalScale, eased);
                moving.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            moving.anchoredPosition = end;

            // 到着してから捨てます。呼び出し側がここで正式Viewを出します。
            Cancel();
        }

        /// <summary>ゴーストを必ず捨てます。何度呼んでも安全です。</summary>
        public void Cancel()
        {
            if (ghost == null)
            {
                return;
            }

            GameObject target = ghost.gameObject;
            ghost = null;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        private void OnDestroy()
        {
            Cancel();
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleDeployTransitionView),
                ReferenceCheck.Of(nameof(ghostRoot), ghostRoot),
                ReferenceCheck.Of(nameof(ghostPrefab), ghostPrefab));
        }

        /// <summary>対象の中心を、ゴースト置き場のローカル座標へ移します。</summary>
        private Vector2 LocalPointOf(RectTransform target)
        {
            Vector3 world = target.TransformPoint(target.rect.center);

            return ghostRoot.InverseTransformPoint(world);
        }
    }
}
