using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Manager;
using TuTien.Database;
using TuTien.Model.Client;

namespace TuTien.Application.Threading
{
    public partial class ServerSession
    {
        private static readonly JsonSerializerOptions GameJson = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static async Task HandleGameClient(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest || !int.TryParse(context.Request.RouteValues["id"]?.ToString(), out int characterId))
            {
                context.Response.StatusCode = 400;
                return;
            }
            string origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = 403;
                return;
            }
            var maps = context.RequestServices.GetRequiredService<MapManager>();
            if (!maps.IsWorldReady)
            {
                context.Response.StatusCode = 503;
                return;
            }
            var clients = context.RequestServices.GetRequiredService<IClientManager>();
            var config = context.RequestServices.GetRequiredService<ConfigManager>();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var session = new ServerSession(socket, config, logger, maps, characterId)
            {
                Player = new TuTien.Model.Player { Id = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!) },
                IsLogin = true
            };
            if (!clients.Add(session))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "server_full", timeout.Token);
                }
                catch (Exception error) when (error is WebSocketException || error is OperationCanceledException)
                {
                    socket.Abort();
                }
                socket.Dispose();
                return;
            }
            try
            {
                await session.Start(context.RequestAborted);
            }
            finally
            {
                clients.RemoveSession(session);
            }
        }

        private async Task SendGameState(CancellationToken cancellationToken)
        {
            long version = -1;
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
                do
                {
                    byte[]? packet = await _maps!.GetFrame(Player!.Id, _worldCharacterId, Id, version, cancellationToken);
                    if (packet == null)
                    {
                        CloseMessage();
                        return;
                    }
                    using var json = JsonDocument.Parse(packet);
                    version = json.RootElement.GetProperty("stateVersion").GetInt64();
                    SendPacket(packet);
                }
                while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                _logger.LogWarning(error, "World stream closed: {SessionId}", Id);
                CloseMessage();
            }
        }

        private async Task ReceiveGameMessage(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[Math.Min(_config.MaxMessageBytes, 2048)];
            while (!cancellationToken.IsCancellationRequested)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                int length = 0;
                ValueWebSocketReceiveResult received;
                do
                {
                    if (length == buffer.Length)
                    {
                        return;
                    }
                    received = await _socket!.ReceiveAsync(buffer.AsMemory(length), timeout.Token);
                    if (received.MessageType != WebSocketMessageType.Binary)
                    {
                        return;
                    }
                    length += received.Count;
                }
                while (!received.EndOfMessage);

                long now = Environment.TickCount64;
                if (now - _rateWindow >= 1000)
                {
                    _rateWindow = now;
                    _receivedInWindow = 0;
                }
                if (++_receivedInWindow > 30)
                {
                    return;
                }
                GameInput? input;
                try
                {
                    input = JsonSerializer.Deserialize<GameInput>(buffer.AsSpan(0, length), GameJson);
                }
                catch (JsonException)
                {
                    return;
                }
                if (input == null || input.Version != 1 || input.Sequence <= _clientSequence || !float.IsFinite(input.X) || !float.IsFinite(input.Y))
                {
                    return;
                }
                _clientSequence = input.Sequence;
                switch (input.Type)
                {
                    case "move":
                        await _maps!.SetPosition(Player!.Id, _worldCharacterId, Id, input.X, input.Y, cancellationToken);
                        break;
                    case "ping":
                        break;
                    default:
                        return;
                }
            }
        }
    }
}
