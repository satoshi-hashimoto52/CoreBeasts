namespace CoreBeasts.Progression
{
    public sealed class InMemoryPlayerProfileRepository : IPlayerProfileRepository
    {
        private PlayerProfile stored;

        public bool TryLoad(out PlayerProfile profile)
        {
            profile = stored;
            return profile != null;
        }

        public void Save(PlayerProfile profile)
        {
            stored = profile;
        }
    }
}
