namespace TuTien.Application.Interfaces.Client
{
    public interface IClientManager
    {
        bool Add(ISession_ME session);
        ISession_ME? GetSession(int id);
        bool Add(TuTien.Model.Character.Character character);
        TuTien.Model.Character.Character? GetCharacter(int id);
        void RemoveCharacter(TuTien.Model.Character.Character character);
        ISession_ME[] GetAllSession();
        void RemoveSession(ISession_ME session);
        Task Clear();
    }
}
