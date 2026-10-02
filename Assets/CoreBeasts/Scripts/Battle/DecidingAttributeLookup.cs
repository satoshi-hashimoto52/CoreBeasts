using CoreBeasts.Units;

namespace CoreBeasts.Battle
{
    /// <summary>
    /// 解決済みラウンドの「勝因になった属性」を読みます。
    ///
    /// 正本は <see cref="RoundResult.DecidingAttribute"/> です。ATTRIBUTE LINK を反映した個体での
    /// 判定結果がそのまま記録されているため、ここで元の個体を判定し直すことはしません
    /// （LINK を無視した再判定は、LINK で勝敗が変わったラウンドで食い違うためです）。
    /// 既存の呼び出しとの互換のために残している薄いラッパーです。
    /// </summary>
    public static class DecidingAttributeLookup
    {
        /// <summary>勝因の属性。分からなければ null です。例外は投げません。</summary>
        public static UnitAttribute? Of(RoundResult result)
        {
            return result != null ? result.DecidingAttribute : null;
        }
    }
}
