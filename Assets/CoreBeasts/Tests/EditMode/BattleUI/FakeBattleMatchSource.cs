namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 決まった編成と選択器を返す<see cref="IBattleMatchSource"/>。
    /// 失敗も再現できるため、編成不足の経路も試せます。
    /// </summary>
    internal sealed class FakeBattleMatchSource : IBattleMatchSource
    {
        private readonly BattleSquad playerSquad;
        private readonly BattleSquad cpuSquad;
        private readonly IBattleUnitSelector cpuSelector;

        internal FakeBattleMatchSource(
            BattleSquad playerSquad,
            BattleSquad cpuSquad,
            IBattleUnitSelector cpuSelector)
        {
            this.playerSquad = playerSquad;
            this.cpuSquad = cpuSquad;
            this.cpuSelector = cpuSelector;
        }

        /// <summary>false にすると、編成を用意できない状況を再現します。</summary>
        internal bool CanCreate { get; set; } = true;

        /// <summary>用意を求められた回数。REMATCHの再生成を確かめるために使います。</summary>
        internal int CreateCount { get; private set; }

        public bool TryCreateMatch(
            out BattleSquad playerSquadResult,
            out BattleSquad cpuSquadResult,
            out IBattleUnitSelector cpuSelectorResult,
            out BattleError error)
        {
            CreateCount++;

            if (!CanCreate)
            {
                playerSquadResult = null;
                cpuSquadResult = null;
                cpuSelectorResult = null;
                error = BattleError.InvalidSquadSize;

                return false;
            }

            playerSquadResult = playerSquad;
            cpuSquadResult = cpuSquad;
            cpuSelectorResult = cpuSelector;
            error = BattleError.None;

            return true;
        }
    }
}
