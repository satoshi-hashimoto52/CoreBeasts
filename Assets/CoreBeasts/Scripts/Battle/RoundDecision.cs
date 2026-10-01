namespace CoreBeasts.Battle
{
    /// <summary>1ラウンドの決着理由。</summary>
    public enum RoundDecision
    {
        /// <summary>属性相性で決着しました。POWERとCOREは見ていません。</summary>
        AttributeAdvantage = 0,

        /// <summary>属性で優劣が付かず、POWER比較で決着しました。</summary>
        PowerComparison = 1,

        /// <summary>POWERも同値で、COREの比較で決着しました。</summary>
        CoreComparison = 2,

        /// <summary>属性・POWER・COREのすべてで差が付きませんでした。</summary>
        Draw = 3,
    }
}
