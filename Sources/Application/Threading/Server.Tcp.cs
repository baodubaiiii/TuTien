using System.Net;
using System.Net.Sockets;

namespace TuTien.Application.Threading
{
    public partial class Server
    {
        private TcpListener? _listener;
        private Task _acceptClients = Task.CompletedTask;
        public bool IsTcpRunning { get; private set; }

        private void StartTcp()
        {
            if (!_config.TcpEnabled || _services == null || IsTcpRunning)
            {
                return;
            }

            _listener = new TcpListener(IPAddress.Parse(_config.TcpHost), _config.TcpPort);
            _listener.Start(Math.Min(_config.MaxClients, 512));
            IsTcpRunning = true;
            _acceptClients = HandleAcceptClient();
            _logger.LogInformation("TCP listening: {Host}:{Port}", _config.TcpHost, _config.TcpPort);
        }

        private async Task HandleAcceptClient()
        {
            try
            {
                while (!_stop.IsCancellationRequested && IsTcpRunning)
                {
                    var client = await _listener!.AcceptTcpClientAsync(_stop.Token);
                    client.NoDelay = true;
                    var session = ActivatorUtilities.CreateInstance<ServerSession>(_services!, client);
                    if (!_clients.Add(session))
                    {
                        client.Dispose();
                        continue;
                    }

                    _ = HandleClient(session);
                }
            }
            catch (Exception error) when (!IsTcpRunning && (error is SocketException || error is ObjectDisposedException))
            {
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                _logger.LogError(error, "TCP listener failed");
                _lifetime?.StopApplication();
            }
            finally
            {
                IsTcpRunning = false;
            }
        }

        private async Task HandleClient(ServerSession session)
        {
            try
            {
                await session.Start(_stop.Token);
            }
            catch (Exception error)
            {
                _logger.LogError(error, "TCP session failed: {SessionId}", session.Id);
            }
            finally
            {
                _clients.RemoveSession(session);
            }
        }
    }
}
