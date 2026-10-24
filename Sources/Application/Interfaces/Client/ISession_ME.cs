using TuTien.Model;
using TuTien.Protocol;

namespace TuTien.Application.Interfaces.Client
{
    public interface ISession_ME
    {
        int Id { get; }
        Player? Player { get; set; }
        bool IsLogin { get; set; }
        Task Completion { get; }
        bool IsConnected();
        void SendMessage(ServerMessage message);
        void CloseMessage();
    }
}
