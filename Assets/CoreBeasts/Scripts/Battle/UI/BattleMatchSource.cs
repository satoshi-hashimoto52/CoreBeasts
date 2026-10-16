using System;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// プレイヤー編成（固定）とCPU編成（マッチごとに作り直し）を束ねて、
    /// 進行役へ1マッチぶんを渡します。
    ///
    /// 表示側は<see cref="PlayerSide"/> / <see cref="CpuSide"/>から立ち絵や名前を引きます。
    /// CPU側の中身は、解決済みの<see cref="RoundResult"/>が示す個体しか引かないため、
    /// ここに参照があること自体は非公開性を損ないません。
    /// </summary>
    public sealed class BattleMatchSource : IBattleMatchSource
    {
        private readonly ICpuSideBuilder cpuSideBuilder;
        private readonly IRandomSource cpuSelectionRandom;
        private readonly Func<IRandomSource, IBattleUnitSelector> cpuSelectorFactory;

        /// <param name="cpuSelectorFactory">
        /// CPU の選び方をマッチごとに作る関数。null なら無作為選出（<see cref="RandomUnitSelector"/>）です。
        /// 製品の画面は常に null で、PlayMode テストだけが展開を固定するために渡します。
        /// </param>
        public BattleMatchSource(
            BattleSideRoster playerSide,
            ICpuSideBuilder cpuSideBuilder,
            IRandomSource cpuSelectionRandom,
            Func<IRandomSource, IBattleUnitSelector> cpuSelectorFactory = null)
        {
            PlayerSide = playerSide;

            this.cpuSideBuilder = cpuSideBuilder
                ?? throw new ArgumentNullException(nameof(cpuSideBuilder));

            this.cpuSelectionRandom = cpuSelectionRandom
                ?? throw new ArgumentNullException(nameof(cpuSelectionRandom));

            this.cpuSelectorFactory = cpuSelectorFactory;
        }

        /// <summary>プレイヤー編成。マッチをまたいでも変わりません。</summary>
        public BattleSideRoster PlayerSide { get; }

        /// <summary>直近に作ったCPU編成。まだ作っていなければ null。</summary>
        public BattleSideRoster CpuSide { get; private set; }

        /// <summary>
        /// 1マッチぶんを用意します。
        /// プレイヤー編成が無い場合は、編成不足として失敗します。
        /// </summary>
        public bool TryCreateMatch(
            out BattleSquad playerSquad,
            out BattleSquad cpuSquad,
            out IBattleUnitSelector cpuSelector,
            out BattleError error)
        {
            playerSquad = null;
            cpuSquad = null;
            cpuSelector = null;

            if (PlayerSide == null)
            {
                error = BattleError.InvalidSquadSize;
                return false;
            }

            if (!cpuSideBuilder.TryBuild(out BattleSideRoster cpuSide, out error))
            {
                return false;
            }

            CpuSide = cpuSide;

            playerSquad = PlayerSide.Squad;
            cpuSquad = cpuSide.Squad;
            cpuSelector = cpuSelectorFactory != null
                ? cpuSelectorFactory(cpuSelectionRandom)
                : new RandomUnitSelector(cpuSelectionRandom);
            error = BattleError.None;

            return true;
        }
    }
}
