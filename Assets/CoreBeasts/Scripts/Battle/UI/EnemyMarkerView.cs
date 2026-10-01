using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// CPU側7枠のうちの1枠。カード裏面風の非公開表示です。
    ///
    /// 個体を受け取る口がありません。状態（未使用・選出中・使用済み）しか持たないため、
    /// この枠から相手の名前・属性・POWERが漏れることはありません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyMarkerView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text markLabel;

        [Header("Unused")]
        [SerializeField] private Color unusedBackground = new Color(0.16f, 0.15f, 0.21f, 1f);
        [SerializeField] private Color unusedFrame = new Color(0.36f, 0.34f, 0.46f, 1f);
        [SerializeField] private Color unusedMark = new Color(0.62f, 0.6f, 0.74f, 1f);

        [Header("Pending")]
        [Tooltip("選出済みであることだけを示します。どれを選んだかは示しません。")]
        [SerializeField] private Color pendingBackground = new Color(0.3f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color pendingFrame = new Color(0.95f, 0.45f, 0.4f, 1f);
        [SerializeField] private Color pendingMark = new Color(1f, 0.85f, 0.8f, 1f);

        [Header("Used")]
        [SerializeField] private Color usedBackground = new Color(0.08f, 0.08f, 0.1f, 1f);
        [SerializeField] private Color usedFrame = new Color(0.17f, 0.17f, 0.21f, 1f);
        [SerializeField] private Color usedMark = new Color(0.3f, 0.3f, 0.36f, 1f);

        private IBattleTextSource text;

        /// <summary>現在の状態。</summary>
        public EnemyMarkerState State { get; private set; } = EnemyMarkerState.Unused;

        /// <summary>表示文字列の供給元を渡します。</summary>
        public void Bind(IBattleTextSource battleText)
        {
            text = battleText;

            SetState(EnemyMarkerState.Unused);
        }

        /// <summary>状態を切り替えます。</summary>
        public void SetState(EnemyMarkerState state)
        {
            State = state;

            Color back = unusedBackground;
            Color edge = unusedFrame;
            Color mark = unusedMark;
            string label = text != null ? text.Hidden : string.Empty;

            switch (state)
            {
                case EnemyMarkerState.Pending:
                    back = pendingBackground;
                    edge = pendingFrame;
                    mark = pendingMark;
                    label = text != null ? text.Ready : string.Empty;
                    break;

                case EnemyMarkerState.Used:
                    back = usedBackground;
                    edge = usedFrame;
                    mark = usedMark;
                    label = text != null ? text.Used : string.Empty;
                    break;
            }

            if (background != null)
            {
                background.color = back;
            }

            if (frame != null)
            {
                frame.color = edge;
            }

            if (markLabel != null)
            {
                markLabel.color = mark;
                markLabel.text = label;
            }
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(EnemyMarkerView),
                ReferenceCheck.Of(nameof(background), background),
                ReferenceCheck.Of(nameof(frame), frame),
                ReferenceCheck.Of(nameof(markLabel), markLabel));
        }
    }
}
