using TuTien.Protocol;

namespace TuTien.Application.Interfaces.Client
{
    public interface IMessageHandler
    {
        void OnConnectOK(ISession_ME client);
        void OnConnectionFail(ISession_ME client);
        void OnDisconnected(ISession_ME client);
        Task OnMessage(ClientMessage message, CancellationToken cancellationToken);
    }
}
