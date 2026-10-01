using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ラウンド番号と勝利数の表示。数値を写すだけで、勝敗の判定はしません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roundLabel;
        [SerializeField] private TMP_Text scoreLabel;

        private IBattleTextSource text;

        /// <summary>表示文字列の供給元を渡します。</summary>
        public void Bind(IBattleTextSource battleText)
        {
            text = battleText;
        }

        /// <summary>ラウンドと勝利数を更新します。</summary>
        public void Refresh(int round, int maxRounds, int playerWins, int cpuWins)
        {
            RefreshRound(round, maxRounds);
            RefreshScore(playerWins, cpuWins);
        }

        /// <summary>ラウンド番号だけを更新します。1未満と上限超えは範囲内へ収めます。</summary>
        public void RefreshRound(int round, int maxRounds)
        {
            if (text == null)
            {
                return;
            }

            int shown = round < 1 ? 1 : (round > maxRounds ? maxRounds : round);

            SetText(roundLabel, text.FormatRound(shown, maxRounds));
        }

        /// <summary>勝利数だけを更新します。</summary>
        public void RefreshScore(int playerWins, int cpuWins)
        {
            if (text == null)
            {
                return;
            }

            SetText(scoreLabel, text.FormatScore(playerWins, cpuWins));
        }

        /// <summary>未設定のSerializeFieldがあれば、フィールド名ごとに報告します。</summary>
        public bool HasRequiredReferences()
        {
            return ReferenceCheck.Validate(
                this,
                nameof(BattleScoreView),
                ReferenceCheck.Of(nameof(roundLabel), roundLabel),
                ReferenceCheck.Of(nameof(scoreLabel), scoreLabel));
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
