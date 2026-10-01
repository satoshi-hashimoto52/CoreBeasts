using CoreBeasts.Units;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 画面中央のコアゲート。ENEMY STAGE と PLAYER STAGE の境目に置き、
    /// 戦闘の焦点を中央へ集めます。
    ///
    /// 同心の薄い円弧だけで作ります。外部素材も専用Shaderも使いません。
    /// 純白は使わず、通常はシアン寄りの低い光量です。
    /// 入力は取りません（装飾）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleCoreGateView : MonoBehaviour
    {
        [SerializeField] private LaunchPedestalGraphic outerRing;
        [SerializeField] private LaunchPedestalGraphic innerRing;
        [SerializeField] private LaunchPedestalGraphic core;

        [Tooltip("待機中の光量。")]
        [SerializeField] [Range(0f, 1f)] private float idleIntensity = 0.35f;

        [Tooltip("対戦公開・接触時の光量。")]
        [SerializeField] [Range(0f, 1f)] private float activeIntensity = 0.85f;

        [SerializeField] private Color idleColor = new Color(0.28f, 0.6f, 0.78f, 1f);
        [SerializeField] private Color playerColor = new Color(0.34f, 0.82f, 0.94f, 1f);
        [SerializeField] private Color cpuColor = new Color(0.9f, 0.32f, 0.46f, 1f);
        [SerializeField] private Color drawColor = new Color(0.72f, 0.76f, 0.84f, 1f);

        /// <summary>直近に適用した光量（確認・テスト用）。</summary>
        public float Intensity { get; private set; }

        private void Awake()
        {
            SetIdle();
        }

        /// <summary>通常時。控えめに灯します。</summary>
        public void SetIdle()
        {
            Apply(idleColor, idleIntensity);
        }

        /// <summary>
        /// 結果に合わせて光量と色を変えます。
        /// 勝敗は決めません。公開済みの結果を受け取るだけです。
        /// </summary>
        public void SetOutcome(BattleSlotOutcome outcome)
        {
            switch (outcome)
            {
                case BattleSlotOutcome.Win:
                    Apply(playerColor, activeIntensity);
                    break;

                case BattleSlotOutcome.Loss:
                    Apply(cpuColor, activeIntensity);
                    break;

                case BattleSlotOutcome.Draw:
                    Apply(drawColor, activeIntensity);
                    break;

                default:
                    SetIdle();
                    break;
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleCoreGateView),
                ReferenceCheck.Of(nameof(outerRing), outerRing),
                ReferenceCheck.Of(nameof(innerRing), innerRing),
                ReferenceCheck.Of(nameof(core), core));
        }

        private void Apply(Color color, float intensity)
        {
            Intensity = intensity;

            ApplyTo(outerRing, color, intensity * 0.6f);
            ApplyTo(innerRing, color, intensity * 0.85f);
            ApplyTo(core, color, intensity);
        }

        private static void ApplyTo(
            LaunchPedestalGraphic graphic, Color color, float intensity)
        {
            if (graphic == null)
            {
                return;
            }

            graphic.Apply(color, color, false);
            graphic.SetOpacity(intensity);
        }
    }
}
