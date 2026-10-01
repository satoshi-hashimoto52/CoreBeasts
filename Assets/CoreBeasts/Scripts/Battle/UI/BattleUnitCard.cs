using CoreBeasts.Units;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// 画面へ1体を出すために必要なものをまとめた不変データ。
    ///
    /// バトル中核が使う値（<see cref="BattleUnit"/>）と、
    /// 表示だけに使う値（表示名・レベル・立ち絵・スキル文言）を1組で持ちます。
    /// 中核側はこの型を知りません。変換は<see cref="TryCreate"/>だけで行います。
    /// </summary>
    public sealed class BattleUnitCard
    {
        private BattleUnitCard(
            string instanceId,
            int level,
            CoreBeastDefinition definition,
            BattleUnit unit)
        {
            InstanceId = instanceId;
            Level = level;
            Definition = definition;
            Unit = unit;
        }

        /// <summary>対戦中の同一性に使う内部ID。</summary>
        public string InstanceId { get; }

        /// <summary>表示用のレベル。</summary>
        public int Level { get; }

        /// <summary>表示に使う個体定義。</summary>
        public CoreBeastDefinition Definition { get; }

        /// <summary>バトル中核へ渡す値。</summary>
        public BattleUnit Unit { get; }

        /// <summary>
        /// 表示用データと中核用データを組み立てます。
        /// IDが空、または定義が無い場合は作れません。
        /// </summary>
        public static bool TryCreate(
            string instanceId,
            int level,
            CoreBeastDefinition definition,
            out BattleUnitCard card)
        {
            card = null;

            if (string.IsNullOrEmpty(instanceId) || definition == null)
            {
                return false;
            }

            BattleUnit unit = new BattleUnit(
                instanceId,
                definition.AttributePowers,
                definition.Core);

            card = new BattleUnitCard(
                instanceId,
                level < 1 ? 1 : level,
                definition,
                unit);

            return true;
        }

        /// <summary>所持個体から作ります。IDはそのまま引き継ぎます。</summary>
        public static bool TryCreate(OwnedCoreBeast owned, out BattleUnitCard card)
        {
            card = null;

            if (owned == null || !owned.IsValid)
            {
                return false;
            }

            return TryCreate(owned.InstanceId, owned.Level, owned.Definition, out card);
        }

        /// <summary>
        /// 所持個体から、別のIDを付けて作ります。
        /// プレイヤーとCPUが同じ所持データを使っても、IDが衝突しないようにするためのものです。
        /// </summary>
        public static bool TryCreate(
            OwnedCoreBeast owned,
            string instanceId,
            out BattleUnitCard card)
        {
            card = null;

            if (owned == null || !owned.IsValid)
            {
                return false;
            }

            return TryCreate(instanceId, owned.Level, owned.Definition, out card);
        }
    }
}
