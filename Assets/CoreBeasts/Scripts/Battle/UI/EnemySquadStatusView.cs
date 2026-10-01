using System.Collections.Generic;

using CoreBeasts.Units;
using TMPro;
using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// CPU側7枠の非公開表示。
    ///
    /// 受け取るのは「使用済み数」と「選出済みかどうか」だけです。
    /// 個体を渡す口が無いため、解決前に相手の編成が漏れることはありません。
    /// 使用済み数と残数は読み取れます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySquadStatusView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private EnemyMarkerView markerPrefab;
        [SerializeField] private TMP_Text remainingLabel;

        private readonly List<EnemyMarkerView> markers = new List<EnemyMarkerView>();
        private IBattleTextSource text;

        /// <summary>生成済みの枠。テストから状態を確かめるために公開しています。</summary>
        public IReadOnlyList<EnemyMarkerView> Markers => markers;

        /// <summary>7枠を生成します。</summary>
        public void Build(IBattleTextSource battleText)
        {
            Clear();

            text = battleText;

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(EnemySquadStatusView),
                    ReferenceCheck.Of(nameof(content), content),
                    ReferenceCheck.Of(nameof(markerPrefab), markerPrefab),
                    ReferenceCheck.Of(nameof(remainingLabel), remainingLabel)))
            {
                return;
            }

            for (int i = 0; i < BattleSquad.UnitCount; i++)
            {
                EnemyMarkerView marker = Instantiate(markerPrefab, content);
                marker.name = "EnemyMarker_" + (i + 1);
                marker.Bind(battleText);

                markers.Add(marker);
            }

            Refresh(0, false);
        }

        /// <summary>
        /// 使用済み数と選出済みかどうかだけで枠を更新します。
        /// </summary>
        public void Refresh(int usedCount, bool hasPendingSelection)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                markers[i].SetState(
                    EnemyMarkerStates.Resolve(i, usedCount, hasPendingSelection));
            }

            if (remainingLabel != null && text != null)
            {
                int total = BattleSquad.UnitCount;
                int remaining = total - usedCount;

                remainingLabel.text = text.FormatRemaining(
                    remaining < 0 ? 0 : remaining,
                    total);
            }
        }

        /// <summary>生成済みの枠を破棄します。</summary>
        public void Clear()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    DestroyView(markers[i].gameObject);
                }
            }

            markers.Clear();
        }

        private static void DestroyView(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
