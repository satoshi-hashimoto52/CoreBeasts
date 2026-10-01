using UnityEngine;

namespace CoreBeasts.Shared.UI
{
    /// <summary>
    /// 船内背景のまとめ役。動かす担当はここだけです。
    ///
    /// 動きは低速・低負荷に限ります。
    ///   ・導管をゆっくり流れる微光
    ///   ・コアの低速パルス
    ///   ・透視グリッドの緩やかな位相移動
    ///
    /// 次のときは動かしません。静止しても絵として成立します。
    ///   ・FX OFF
    ///   ・画面が非表示（<c>OnDisable</c>で止まります）
    ///
    /// Coroutine も Tween も作らず、<c>Update</c> の中で値を進めるだけなので、
    /// 表示を切り替えても増殖しません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipBackdropView : MonoBehaviour
    {
        [Header("Animated parts")]
        [Tooltip("微光が流れる導管。")]
        [SerializeField] private EnergyConduitGraphic[] conduits;

        [Tooltip("位相が動く透視グリッド。")]
        [SerializeField] private DimensionalGridGraphic[] grids;

        [Tooltip("ゆっくり明滅する部分（コアなど）。")]
        [SerializeField] private ShipBackgroundGraphic[] pulsing;

        [Header("Speed")]
        [Tooltip("導管の流れる速さ（1秒あたりの周回数）。")]
        [SerializeField] [Range(0.01f, 0.5f)] private float flowSpeed = 0.08f;

        [Tooltip("グリッドの位相が進む速さ。")]
        [SerializeField] [Range(0.005f, 0.2f)] private float gridSpeed = 0.02f;

        [Tooltip("明滅の速さ。")]
        [SerializeField] [Range(0.05f, 1f)] private float pulseSpeed = 0.25f;

        [Tooltip("明滅の下限。ここより暗くはなりません。")]
        [SerializeField] [Range(0.3f, 1f)] private float pulseFloor = 0.72f;

        [Tooltip("静止しているときの明るさ。")]
        [SerializeField] [Range(0.3f, 1f)] private float restingGlow = 0.88f;

        private bool fxEnabled = true;
        private float elapsed;

        /// <summary>動いているか（確認・テスト用）。</summary>
        public bool IsAnimating => fxEnabled && isActiveAndEnabled;

        /// <summary>直近に適用した明るさ。</summary>
        public float CurrentGlow { get; private set; } = 1f;

        private void OnEnable()
        {
            // 表示に戻ったら、まず静止した姿へそろえます。
            ApplyResting();
        }

        private void OnDisable()
        {
            // 非表示のあいだは進めません。Update が呼ばれないので止まります。
            elapsed = 0f;
        }

        /// <summary>FX ON / OFF を受け取ります。OFF では静止させます。</summary>
        public void SetFxEnabled(bool enabled)
        {
            if (fxEnabled == enabled)
            {
                return;
            }

            fxEnabled = enabled;

            if (!fxEnabled)
            {
                ApplyResting();
            }
        }

        private void Update()
        {
            if (!fxEnabled)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            Flow(elapsed * flowSpeed);
            Phase(elapsed * gridSpeed);

            float wave = (Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

            Pulse(Mathf.Lerp(pulseFloor, 1f, wave));
        }

        /// <summary>止まっている姿。FX OFF でも絵として成立させます。</summary>
        private void ApplyResting()
        {
            elapsed = 0f;

            Flow(0f);
            Phase(0f);
            Pulse(restingGlow);
        }

        private void Flow(float value)
        {
            if (conduits == null)
            {
                return;
            }

            for (int i = 0; i < conduits.Length; i++)
            {
                if (conduits[i] != null)
                {
                    conduits[i].SetFlow(value);
                }
            }
        }

        private void Phase(float value)
        {
            if (grids == null)
            {
                return;
            }

            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null)
                {
                    grids[i].SetPhase(value);
                }
            }
        }

        private void Pulse(float value)
        {
            CurrentGlow = value;

            if (pulsing == null)
            {
                return;
            }

            for (int i = 0; i < pulsing.Length; i++)
            {
                if (pulsing[i] != null)
                {
                    pulsing[i].SetGlow(value);
                }
            }
        }
    }
}
