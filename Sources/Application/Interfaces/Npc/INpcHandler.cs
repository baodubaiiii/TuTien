namespace TuTien.Application.Interfaces.Npc
{
    public interface INpcHandler
    {
        void Chat();
        void Chat(string text);
        void Update(long timeServer);
    }
}
