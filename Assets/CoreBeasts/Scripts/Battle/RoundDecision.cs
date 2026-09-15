namespace CoreBeasts.Battle
{
    /// <summary>1ラウンドの決着理由。</summary>
    public enum RoundDecision
    {
        /// <summary>属性相性で決着しました。</summary>
        AttributeAdvantage = 0,

        /// <summary>
        /// 属性相性で優劣が付かず、POWER比較で決着しました。
        /// POWERも同値だった引き分けもこの理由になります。
        /// </summary>
        PowerComparison = 1,
    }
}
