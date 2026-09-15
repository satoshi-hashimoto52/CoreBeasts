namespace CoreBeasts.Battle
{
    /// <summary>
    /// 乱数の供給元。UnityEngine.Randomへ直接依存させないための境界です。
    /// テストでは決まった値を返す実装へ差し替えて、選出結果を固定できます。
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// 0以上<paramref name="exclusiveMax"/>未満の整数を返します。
        /// <paramref name="exclusiveMax"/>は1以上で呼ばれます。
        /// </summary>
        int NextInt(int exclusiveMax);
    }
}
