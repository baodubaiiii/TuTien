namespace TuTien.Application.Interfaces.Monster
{
    public interface IMonsterHandler
    {
        Task Update(long timeServer, float delta);
        void Reset();
    }
}
