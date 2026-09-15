using System.Collections.Generic;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 所持Core Beastのグリッド表示。カードの生成と選択状態の反映だけを担当します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RosterGridView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private BeastCardView cardPrefab;

        private readonly List<BeastCardView> cards = new List<BeastCardView>();

        /// <summary>ロースターからカードを作り直します。</summary>
        public void Build(
            CoreBeastRoster roster,
            AttributePalette palette,
            UiTextCatalog text,
            IBeastCardListener listener)
        {
            Clear();

            if (!ReferenceCheck.Validate(
                    this,
                    nameof(RosterGridView),
                    ReferenceCheck.Of(nameof(content), content),
                    ReferenceCheck.Of(nameof(cardPrefab), cardPrefab),
                    ReferenceCheck.Of(nameof(roster), roster),
                    ReferenceCheck.Of(nameof(palette), palette),
                    ReferenceCheck.Of(nameof(text), text),
                    ReferenceCheck.Of(nameof(listener), listener)))
            {
                return;
            }

            IReadOnlyList<OwnedCoreBeast> owned = roster.Owned;

            for (int i = 0; i < owned.Count; i++)
            {
                OwnedCoreBeast beast = owned[i];

                if (beast == null || !beast.IsValid)
                {
                    continue;
                }

                BeastCardView card = Instantiate(cardPrefab, content);
                card.name = $"Card_{beast.InstanceId}";
                card.Bind(beast, palette, text, listener);

                cards.Add(card);
            }
        }

        /// <summary>
        /// 編成済みマークを編成データと一致させます。
        /// 所持個体の内部ID(InstanceId)で判定するため、
        /// 同じ種類・同じ属性でも別個体にはマークが付きません。
        /// </summary>
        public void RefreshSquadMarks(SquadFormation formation)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                OwnedCoreBeast beast = cards[i].Beast;

                bool inSquad = formation != null &&
                               beast != null &&
                               formation.IndexOfInstance(beast.InstanceId) >= 0;

                cards[i].SetInSquad(inSquad);
            }
        }

        /// <summary>選択中の個体だけを選択状態にします。</summary>
        public void SetSelected(OwnedCoreBeast selected)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].SetSelected(ReferenceEquals(cards[i].Beast, selected));
            }
        }

        private void Clear()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    DestroyView(cards[i].gameObject);
                }
            }

            cards.Clear();
        }

        /// <summary>
        /// 再生成時に古い表示が残らないよう破棄します。
        /// Destroy はEditModeで遅延するため、実行中かどうかで使い分けます
        /// （DragGhostPresenter / PortraitRenderTarget と同じ方式）。
        /// </summary>
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
