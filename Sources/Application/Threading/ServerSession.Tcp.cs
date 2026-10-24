using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Main;
using TuTien.Application.Manager;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;

namespace TuTien.Application.Threading
{
    public partial class ServerSession
    {
        private readonly TcpClient? _tcpClient;
        private readonly NetworkStream? _tcpStream;
        private readonly IClientManager? _tcpClients;
        private CancellationTokenSource? _tcpWorldStop;
        private Task _tcpSnapshots = Task.CompletedTask;

        public ServerSession(TcpClient client, ConfigManager config, ILoggerFactory loggerFactory, MapManager maps,
            LoginGame login, CharacterDB characters, Cache cache, IClientManager clients)
            : this(null, config, loggerFactory, maps)
        {
            _tcpClient = client;
            _tcpClients = clients;
            _tcpStream = client.GetStream();
            _controller = new Controller(this, loggerFactory.CreateLogger<Controller>(), maps, login, characters, cache, clients, config);
        }

        private async Task ReceiveTcpMessage(CancellationToken cancellationToken)
        {
            byte[] header = new byte[4];
            while (!cancellationToken.IsCancellationRequested)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await _tcpStream!.ReadExactlyAsync(header, timeout.Token);
                int length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length < 1 || length > _config.MaxMessageBytes)
                {
                    return;
                }

                byte[] body = new byte[length];
                await _tcpStream.ReadExactlyAsync(body, timeout.Token);
                long now = Environment.TickCount64;
                if (now - _rateWindow >= 1000)
                {
                    _rateWindow = now;
                    _receivedInWindow = 0;
                }

                var message = JsonSerializer.Deserialize<TcpMessage>(body, GameJson);
                if (++_receivedInWindow > 30 || message == null || message.Version != 1 ||
                    message.Sequence <= _clientSequence || string.IsNullOrEmpty(message.Type) || message.Type.Length > 40)
                {
                    return;
                }

                _clientSequence = message.Sequence;
                await _controller.OnMessage(message, cancellationToken);
            }
        }

        public void SendMessage(TcpMessage request, bool success, object? data = null, string message = "")
        {
            if (IsConnected())
            {
                SendPacket(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    type = "result",
                    version = 1,
                    requestSequence = request.Sequence,
                    command = request.Type,
                    success,
                    message,
                    data
                }, GameJson));
            }
        }

        public async Task<bool> JoinMap(int id, CancellationToken cancellationToken)
        {
            if (Player == null || _maps == null)
            {
                return false;
            }

            await OutMap();
            var character = await _maps.JoinMap(Player.Id, id, Id, cancellationToken);
            if (character == null)
            {
                return false;
            }

            _worldCharacterId = id;
            Player.Character = character;
            _tcpWorldStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            _tcpSnapshots = SendGameState(_tcpWorldStop.Token);
            return true;
        }

        public async Task OutMap()
        {
            await StopGameState();

            if (Player != null && _worldCharacterId > 0)
            {
                await _maps!.OutMap(Player.Id, _worldCharacterId, Id);
                _worldCharacterId = 0;
                Player.Character = null;
            }
        }

        private async Task StopGameState()
        {
            if (_tcpWorldStop != null)
            {
                _tcpWorldStop.Cancel();
                await _tcpSnapshots;
                _tcpWorldStop.Dispose();
                _tcpWorldStop = null;
                _tcpSnapshots = Task.CompletedTask;
            }

        }

        public async Task<TuTien.Model.Character.Character?> ChangeMap(string playToken, int mapId, int zoneId, bool revive,
            Action<string> onError, CancellationToken cancellationToken)
        {
            await StopGameState();
            try
            {
                var character = await _maps!.ChangeMap(Player!.Id, _worldCharacterId, Id, playToken, mapId, zoneId, revive, onError, cancellationToken);
                if (character != null)
                {
                    Player.Character = character;
                }
                else if (_tcpClients!.GetCharacter(_worldCharacterId) == null)
                {
                    Player.Character = null;
                    _worldCharacterId = 0;
                }
                return character;
            }
            finally
            {
                if (_worldCharacterId > 0 && IsConnected())
                {
                    _tcpWorldStop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    _tcpSnapshots = SendGameState(_tcpWorldStop.Token);
                }
            }
        }
    }
}
