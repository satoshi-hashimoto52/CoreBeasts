using CoreBeasts.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// ヘッダーの勝利ピップ。テキストスコアと同じ値を、小さなコアランプでも示します。
    ///
    /// 勝敗は決めません。<see cref="BattleSession"/>が数えた勝利数を写すだけです。
    /// 引き分けでは、どちらのピップも増えません。
    /// 装飾なので入力は取りません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleScorePipsView : MonoBehaviour
    {
        [Tooltip("PLAYER側のピップ。左から順に点灯します。")]
        [SerializeField] private Image[] playerPips;

        [Tooltip("CPU側のピップ。")]
        [SerializeField] private Image[] cpuPips;

        [SerializeField] private Color idle = new Color(0.16f, 0.2f, 0.27f, 1f);
        [SerializeField] private Color playerLit = new Color(0.34f, 0.82f, 0.94f, 1f);
        [SerializeField] private Color cpuLit = new Color(0.9f, 0.32f, 0.46f, 1f);

        /// <summary>直近に反映したPLAYERの勝利数（確認・テスト用）。</summary>
        public int PlayerWins { get; private set; }

        /// <summary>直近に反映したCPUの勝利数（確認・テスト用）。</summary>
        public int CpuWins { get; private set; }

        /// <summary>片側のピップ数。</summary>
        public int PipsPerSide => playerPips != null ? playerPips.Length : 0;

        private void Awake()
        {
            BlockInput();
            Refresh(0, 0);
        }

        /// <summary>勝利数を反映します。</summary>
        public void Refresh(int playerWins, int cpuWins)
        {
            PlayerWins = playerWins;
            CpuWins = cpuWins;

            Apply(playerPips, playerWins, playerLit);
            Apply(cpuPips, cpuWins, cpuLit);
        }

        /// <summary>指定側のピップが点灯しているか（確認・テスト用）。</summary>
        public bool IsPlayerPipLit(int index)
        {
            return playerPips != null &&
                   index >= 0 &&
                   index < playerPips.Length &&
                   playerPips[index] != null &&
                   playerPips[index].color == playerLit;
        }

        /// <summary>
        /// 指定側のピップ（0始まり）。範囲外なら null。
        /// 勝利コア獲得の演出が位置を読むためだけに使い、ピップ自体は動かしません。
        /// </summary>
        public RectTransform PipTransform(RoundWinner side, int index)
        {
            Image[] pips = side == RoundWinner.Player ? playerPips : side == RoundWinner.Cpu ? cpuPips : null;

            if (pips == null || index < 0 || index >= pips.Length || pips[index] == null)
            {
                return null;
            }

            return pips[index].rectTransform;
        }

        /// <summary>未設定のSerializeFieldがあれば報告します。</summary>
        public bool HasRequiredReferences()
        {
            bool ok = playerPips != null && playerPips.Length > 0 &&
                      cpuPips != null && cpuPips.Length > 0;

            if (!ok)
            {
                Debug.LogWarning(
                    nameof(BattleScorePipsView) + ": ピップが未設定です。", this);
            }

            return ok;
        }

        private void Apply(Image[] pips, int wins, Color lit)
        {
            if (pips == null)
            {
                return;
            }

            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null)
                {
                    continue;
                }

                pips[i].color = i < wins ? lit : idle;
            }
        }

        private void BlockInput()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
