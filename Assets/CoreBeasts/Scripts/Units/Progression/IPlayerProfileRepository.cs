namespace CoreBeasts.Progression
{
    public interface IPlayerProfileRepository
    {
        bool TryLoad(out PlayerProfile profile);
        void Save(PlayerProfile profile);
    }
}
