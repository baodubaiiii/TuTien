using System.Net.WebSockets;
using System.Buffers.Binary;
using System.Threading.Channels;
using Google.Protobuf;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Main;
using TuTien.Database;
using TuTien.Model;
using TuTien.Protocol;
using TuTien.Application.Manager;

namespace TuTien.Application.Threading
{
    public partial class ServerSession : ISession_ME
    {
        private static int _nextId;
        private readonly WebSocket? _socket;
        private readonly ConfigManager _config;
        private readonly ILogger<ServerSession> _logger;
        private readonly Controller _controller;
        private readonly Channel<byte[]> _sendMessages;
        private readonly CancellationTokenSource _stop;
        private readonly TaskCompletionSource _completion;
        private ulong _clientSequence;
        private long _serverSequence;
        private int _queuedBytes;
        private readonly object _sendGate = new object();
        private long _rateWindow = Environment.TickCount64;
        private int _receivedInWindow;
        private readonly MapManager? _maps;
        private int _worldCharacterId;

        public int Id { get; }
        public Player? Player { get; set; }
        public bool IsLogin { get; set; }
        public Task Completion
        {
            get
            {
                return _completion.Task;
            }
        }

        public ServerSession(WebSocket? socket, ConfigManager config, ILoggerFactory loggerFactory, MapManager? maps = null, int worldCharacterId = 0)
        {
            Id = Interlocked.Increment(ref _nextId);
            _socket = socket;
            _config = config;
            _maps = maps;
            _worldCharacterId = worldCharacterId;
            _logger = loggerFactory.CreateLogger<ServerSession>();
            _controller = new Controller(this, loggerFactory.CreateLogger<Controller>());
            _stop = new CancellationTokenSource();
            _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _sendMessages = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(config.SendQueueCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public bool IsConnected()
        {
            return !_stop.IsCancellationRequested && (_tcpClient?.Connected ?? _socket?.State == WebSocketState.Open);
        }

        public async Task Start(CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            Task sender = SendMessage(linked.Token);
            Task snapshots = Task.CompletedTask;
            _controller.OnConnectOK(this);

            try
            {
                if (_tcpClient != null)
                {
                    await ReceiveTcpMessage(linked.Token);
                }
                else if (_worldCharacterId > 0 && _maps != null && Player != null)
                {
                    var character = await _maps.JoinMap(Player.Id, _worldCharacterId, Id, linked.Token);
                    if (character != null)
                    {
                        Player.Character = character;
                        snapshots = SendGameState(linked.Token);
                        await ReceiveGameMessage(linked.Token);
                    }
                }
                else
                {
                    await ReceiveMessage(linked.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
                _controller.OnConnectionFail(this);
            }
            catch (InvalidProtocolBufferException)
            {
                _logger.LogDebug("Malformed protobuf packet rejected: {SessionId}", Id);
            }
            catch (IOException)
            {
                _controller.OnConnectionFail(this);
            }
            catch (System.Text.Json.JsonException)
            {
                _logger.LogDebug("Invalid TCP JSON rejected: {SessionId}", Id);
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Session failed: {SessionId}", Id);
            }
            finally
            {
                CloseMessage();
                await sender;
                await snapshots;
                await _tcpSnapshots;
                _tcpWorldStop?.Dispose();
                if (_worldCharacterId > 0 && _maps != null && Player != null)
                {
                    try
                    {
                        await _maps.OutMap(Player.Id, _worldCharacterId, Id);
                        Player.Character = null;
                    }
                    catch (Exception error)
                    {
                        _logger.LogError(error, "Character leave/save failed: {CharacterId}", _worldCharacterId);
                    }
                }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                try
                {
                    if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived))
                    {
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", timeout.Token);
                    }
                }
                catch (Exception error) when (error is WebSocketException || error is OperationCanceledException)
                {
                    _socket?.Abort();
                }

                _socket?.Dispose();
                _tcpClient?.Dispose();
                _controller.OnDisconnected(this);
                _completion.TrySetResult();
            }
        }

        public void SendMessage(ServerMessage message)
        {
            if (!IsConnected())
            {
                return;
            }

            lock (_sendGate)
            {
                var packet = message.Clone();
                packet.ProtocolVersion = 1;
                packet.Sequence = (ulong)Interlocked.Increment(ref _serverSequence);
                byte[] bytes = packet.ToByteArray();
                SendPacket(bytes);
            }
        }

        private void SendPacket(byte[] bytes)
        {
            if (!IsConnected())
            {
                return;
            }
            int queued = Interlocked.Add(ref _queuedBytes, bytes.Length);
            int maxBytes = _worldCharacterId > 0 || _tcpClient != null ? 65536 : _config.MaxMessageBytes;
            if (bytes.Length > maxBytes || queued > _config.MaxQueuedBytes || !_sendMessages.Writer.TryWrite(bytes))
            {
                Interlocked.Add(ref _queuedBytes, -bytes.Length);
                if (!_stop.IsCancellationRequested)
                {
                    _logger.LogWarning("Slow session closed: {SessionId}", Id);
                }
                CloseMessage();
            }
        }

        private async Task SendMessage(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (byte[] bytes in _sendMessages.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        if (_tcpStream != null)
                        {
                            byte[] header = new byte[4];
                            BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
                            await _tcpStream.WriteAsync(header, cancellationToken);
                            await _tcpStream.WriteAsync(bytes, cancellationToken);
                        }
                        else
                        {
                            await _socket!.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
                        }
                    }
                    finally
                    {
                        Interlocked.Add(ref _queuedBytes, -bytes.Length);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (WebSocketException error)
            {
                _logger.LogDebug(error, "Send disconnected: {SessionId}", Id);
                CloseMessage();
            }
            catch (IOException)
            {
                CloseMessage();
            }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Send failed: {SessionId}", Id);
                CloseMessage();
            }
        }

        private async Task ReceiveMessage(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[_config.MaxMessageBytes];

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
                        _logger.LogDebug("Oversized packet rejected: {SessionId}", Id);
                        return;
                    }

                    received = await _socket!.ReceiveAsync(buffer.AsMemory(length), timeout.Token);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (received.MessageType != WebSocketMessageType.Binary)
                    {
                        _logger.LogDebug("Text packet rejected: {SessionId}", Id);
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

                if (++_receivedInWindow > 120)
                {
                    _logger.LogDebug("Packet rate limit exceeded: {SessionId}", Id);
                    return;
                }

                var message = ClientMessage.Parser.ParseFrom(buffer, 0, length);
                if (message.ProtocolVersion != 1 || message.Sequence <= _clientSequence ||
                    message.PayloadCase == ClientMessage.PayloadOneofCase.None)
                {
                    _logger.LogDebug("Invalid protocol or sequence: {SessionId}", Id);
                    return;
                }

                _clientSequence = message.Sequence;
                await _controller.OnMessage(message, cancellationToken);
            }
        }

        public void CloseMessage()
        {
            _stop.Cancel();
            _sendMessages.Writer.TryComplete();
        }
    }
}
