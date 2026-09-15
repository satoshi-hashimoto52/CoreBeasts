using UnityEngine.EventSystems;

namespace CoreBeasts.Units
{
    /// <summary>所持一覧カードからの通知先。</summary>
    public interface IBeastCardListener
    {
        /// <summary>短いタップ。選択と詳細表示のみを行います。</summary>
        void OnCardTapped(OwnedCoreBeast beast);

        /// <summary>長押ししてからの移動でドラッグを開始したとき。</summary>
        void OnCardDragBegin(OwnedCoreBeast beast, PointerEventData eventData);

        /// <summary>ドラッグ中の移動。</summary>
        void OnCardDragMove(PointerEventData eventData);

        /// <summary>ドラッグ終了。ドロップ成否に関わらず呼ばれます。</summary>
        void OnCardDragEnd(OwnedCoreBeast beast, PointerEventData eventData);
    }

    /// <summary>編成枠からの通知先。</summary>
    public interface ISquadSlotListener
    {
        /// <summary>いま一覧からのドラッグ中かどうか。</summary>
        bool IsDraggingBeast { get; }

        /// <summary>短いタップ。配置済みなら除外します。</summary>
        void OnSlotTapped(int slotIndex);

        /// <summary>枠の上で指を離したとき。</summary>
        void OnSlotDropped(int slotIndex);
    }
}
