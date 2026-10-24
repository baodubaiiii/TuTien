using TuTien.Application.Interfaces.Client;
using TuTien.Protocol;

namespace TuTien.Application.Main
{
    public partial class Controller : IMessageHandler
    {
        private readonly ISession_ME _client;
        private readonly ILogger<Controller> _logger;

        public Controller(ISession_ME client, ILogger<Controller> logger)
        {
            _client = client;
            _logger = logger;
        }

        public void OnConnectOK(ISession_ME client)
        {
            _logger.LogDebug("Session connected: {SessionId}", client.Id);
        }

        public void OnConnectionFail(ISession_ME client)
        {
            _logger.LogDebug("Session connection failed: {SessionId}", client.Id);
        }

        public void OnDisconnected(ISession_ME client)
        {
            _logger.LogDebug("Session disconnected: {SessionId}", client.Id);
        }

        public virtual Task OnMessage(ClientMessage message, CancellationToken cancellationToken)
        {
            if (message.PayloadCase == ClientMessage.PayloadOneofCase.Ping)
            {
                _client.SendMessage(Service.Pong(message.Ping.ClientTime));
                return Task.CompletedTask;
            }

            _logger.LogDebug("Gameplay handler is not implemented: {Payload}", message.PayloadCase);
            _client.SendMessage(Service.ServerMessage(message.Sequence, "Base server: chức năng chưa được triển khai."));
            return Task.CompletedTask;
        }
    }
}
