namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// プレイヤー編成トレイのタップ通知先。
    /// 枠は「どれが押されたか」だけを伝え、受け付けるかどうかは判断しません。
    /// </summary>
    public interface IBattleTrayListener
    {
        /// <summary>未使用の枠がタップされました。</summary>
        void OnTraySlotTapped(string instanceId);
    }
}
