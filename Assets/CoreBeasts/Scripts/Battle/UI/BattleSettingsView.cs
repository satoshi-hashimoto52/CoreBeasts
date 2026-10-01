using System;

using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の設定パネル。右上の歯車から開き、低頻度の操作をまとめます。
    ///
    /// ヘッダーへ常時出していた HOME と FX ON/OFF は、ここへ入れました。
    /// 画面を離れる操作を常時 Primary で出さないためです。
    ///
    /// 進行や勝敗には一切触りません。開閉と、押されたことを知らせるだけです。
    /// 何を実行するかは<see cref="BattleScreenController"/>が決めます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSettingsView : MonoBehaviour
    {
        [Header("Roots")]
        [Tooltip("パネル全体。初期状態は非表示です。")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("パネルの枠。Safe Area の内側へ収めます。")]
        [SerializeField] private RectTransform panelBody;

        [Header("Buttons")]
        [Tooltip("ヘッダー右上の歯車。これだけがパネルの外に出ています。")]
        [SerializeField] private Button settingsButton;

        [Tooltip("パネルの外側。タップで閉じるだけの、背面をふさぐ板です。")]
        [SerializeField] private Button backdropButton;

        [SerializeField] private Button fxButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button closeButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text fxCaptionLabel;
        [SerializeField] private TMP_Text fxValueLabel;
        [SerializeField] private TMP_Text homeLabel;
        [SerializeField] private TMP_Text closeLabel;

        private IBattleTextSource battleText;
        private bool listenersAdded;

        /// <summary>FXの切り替えを求められた。</summary>
        public event Action FxToggleRequested;

        /// <summary>HOMEへ戻ることを求められた。</summary>
        public event Action HomeRequested;

        /// <summary>開閉が変わった。</summary>
        public event Action OpenStateChanged;

        /// <summary>パネルを出しているか。</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        /// <summary>
        /// 表示に使う文言を渡し、配線を済ませます。
        /// パネルは必ず閉じた状態から始めます。
        /// </summary>
        public void Bind(IBattleTextSource battleTextSource)
        {
            battleText = battleTextSource;

            AddListeners();
            ApplyStaticLabels();

            // 開いたままシーンへ入らないよう、通知せずに閉じ切ります。
            SetActive(panelRoot, false);
        }

        /// <summary>パネルを開きます。すでに開いていれば何もしません。</summary>
        public bool Open()
        {
            if (panelRoot == null || IsOpen)
            {
                return false;
            }

            SetActive(panelRoot, true);
            OpenStateChanged?.Invoke();

            return true;
        }

        /// <summary>パネルを閉じます。すでに閉じていれば何もしません。</summary>
        public bool Close()
        {
            if (panelRoot == null || !IsOpen)
            {
                return false;
            }

            SetActive(panelRoot, false);
            OpenStateChanged?.Invoke();

            return true;
        }

        /// <summary>歯車のタップ。開いているときは閉じます。</summary>
        public void OnSettingsClicked()
        {
            if (settingsButton != null && !settingsButton.interactable)
            {
                return;
            }

            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        /// <summary>CLOSE、またはパネル外のタップ。</summary>
        public void OnCloseClicked()
        {
            Close();
        }

        /// <summary>FX項目のタップ。切り替えるかどうかは受け手が決めます。</summary>
        public void OnFxClicked()
        {
            if (!IsOpen)
            {
                return;
            }

            FxToggleRequested?.Invoke();
        }

        /// <summary>HOME項目のタップ。遷移そのものは受け手と遷移用ボタンが行います。</summary>
        public void OnHomeClicked()
        {
            if (!IsOpen)
            {
                return;
            }

            HomeRequested?.Invoke();
        }

        /// <summary>FXの現在値を表示へ反映します。</summary>
        public void SetFxState(bool enabled)
        {
            if (fxValueLabel == null)
            {
                return;
            }

            fxValueLabel.text = battleText != null
                ? (enabled ? battleText.On : battleText.Off)
                : (enabled ? "ON" : "OFF");
        }

        /// <summary>歯車を押せるかどうか。演出中は押せなくします。</summary>
        public void SetSettingsInteractable(bool interactable)
        {
            if (settingsButton != null)
            {
                settingsButton.interactable = interactable;
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleSettingsView),
                ReferenceCheck.Of(nameof(panelRoot), panelRoot),
                ReferenceCheck.Of(nameof(panelBody), panelBody),
                ReferenceCheck.Of(nameof(settingsButton), settingsButton),
                ReferenceCheck.Of(nameof(backdropButton), backdropButton),
                ReferenceCheck.Of(nameof(fxButton), fxButton),
                ReferenceCheck.Of(nameof(homeButton), homeButton),
                ReferenceCheck.Of(nameof(closeButton), closeButton),
                ReferenceCheck.Of(nameof(titleLabel), titleLabel),
                ReferenceCheck.Of(nameof(fxCaptionLabel), fxCaptionLabel),
                ReferenceCheck.Of(nameof(fxValueLabel), fxValueLabel),
                ReferenceCheck.Of(nameof(homeLabel), homeLabel),
                ReferenceCheck.Of(nameof(closeLabel), closeLabel));
        }

        private void OnDisable()
        {
            // 開いたままシーンを離れないようにします。
            if (panelRoot != null && panelRoot.activeSelf)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            if (listenersAdded)
            {
                return;
            }

            listenersAdded = true;

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.AddListener(OnCloseClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (fxButton != null)
            {
                fxButton.onClick.AddListener(OnFxClicked);
            }

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(OnHomeClicked);
            }
        }

        private void RemoveListeners()
        {
            if (!listenersAdded)
            {
                return;
            }

            listenersAdded = false;

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsClicked);
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (fxButton != null)
            {
                fxButton.onClick.RemoveListener(OnFxClicked);
            }

            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(OnHomeClicked);
            }
        }

        private void ApplyStaticLabels()
        {
            if (battleText == null)
            {
                return;
            }

            SetText(titleLabel, battleText.Settings);
            SetText(fxCaptionLabel, battleText.Fx);
            SetText(homeLabel, battleText.Home);
            SetText(closeLabel, battleText.Close);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
