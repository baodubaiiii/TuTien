using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TuTien.Client
{
    public class Session_ME : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private CancellationTokenSource stop;
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<long, TaskCompletionSource<JObject>> pending = new Dictionary<long, TaskCompletionSource<JObject>>();
        private long sequence;

        public Action<JObject> OnMessage;

        public bool IsConnected
        {
            get
            {
                return client != null && stream != null && client.Connected && stop != null && !stop.IsCancellationRequested;
            }
        }

        public async Task Connect(string host, int port)
        {
            Dispose();
            sequence = 0;
            stop = new CancellationTokenSource();
            client = new TcpClient { NoDelay = true };
            Task connection = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connection, Task.Delay(10000)).ConfigureAwait(false) != connection)
            {
                Dispose();
                throw new IOException("Không kết nối được máy chủ.");
            }
            await connection.ConfigureAwait(false);
            stream = client.GetStream();
            var input = stream;
            var token = stop.Token;
            _ = ReceiveMessage(input, token);
        }

        public async Task<JObject> SendMessage(string type, object data = null, bool expectResponse = true)
        {
            if (!IsConnected)
            {
                throw new IOException("Chưa kết nối máy chủ.");
            }
            TaskCompletionSource<JObject> completion = null;
            long id = 0;
            await sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                id = ++sequence;
                if (expectResponse)
                {
                    completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (pending)
                    {
                        pending.Add(id, completion);
                    }
                }
                var packet = new JObject { ["version"] = 1, ["sequence"] = id, ["type"] = type };
                if (data != null)
                {
                    packet["data"] = JToken.FromObject(data);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(packet.ToString(Formatting.None));
                if (bytes.Length > 16384)
                {
                    throw new IOException("Dữ liệu gửi quá lớn.");
                }
                byte[] header = { (byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length };
                await stream.WriteAsync(header, 0, header.Length, stop.Token).ConfigureAwait(false);
                await stream.WriteAsync(bytes, 0, bytes.Length, stop.Token).ConfigureAwait(false);
            }
            catch
            {
                lock (pending)
                {
                    pending.Remove(id);
                }
                throw;
            }
            finally
            {
                sendGate.Release();
            }
            if (!expectResponse)
            {
                return null;
            }
            try
            {
                if (await Task.WhenAny(completion.Task, Task.Delay(10000, stop.Token)).ConfigureAwait(false) != completion.Task)
                {
                    throw new IOException("Máy chủ phản hồi quá lâu.");
                }
                JObject result = await completion.Task.ConfigureAwait(false);
                if (result.Value<bool>("success") != true)
                {
                    throw new InvalidOperationException(result.Value<string>("message") ?? "Thao tác không thành công.");
                }
                return result;
            }
            finally
            {
                lock (pending)
                {
                    pending.Remove(id);
                }
            }
        }

        private async Task ReceiveMessage(NetworkStream input, CancellationToken token)
        {
            try
            {
                byte[] header = new byte[4];
                while (!token.IsCancellationRequested)
                {
                    await Read(input, header, token).ConfigureAwait(false);
                    int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                    if (length < 1 || length > 65536)
                    {
                        throw new IOException("Packet máy chủ không hợp lệ.");
                    }
                    byte[] bytes = new byte[length];
                    await Read(input, bytes, token).ConfigureAwait(false);
                    var message = JObject.Parse(Encoding.UTF8.GetString(bytes));
                    if (message.Value<int>("version") != 1)
                    {
                        throw new IOException("Giao thức không tương thích.");
                    }
                    if (message.Value<string>("type") == "result")
                    {
                        TaskCompletionSource<JObject> response;
                        lock (pending)
                        {
                            pending.TryGetValue(message.Value<long>("requestSequence"), out response);
                        }
                        if (response != null)
                        {
                            response.TrySetResult(message);
                        }
                    }
                    else
                    {
                        OnMessage?.Invoke(message);
                    }
                }
            }
            catch (Exception)
            {
                if (!token.IsCancellationRequested)
                {
                    OnMessage?.Invoke(new JObject { ["type"] = "disconnected" });
                    Dispose();
                }
            }
        }

        private static async Task Read(NetworkStream input, byte[] bytes, CancellationToken token)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = await input.ReadAsync(bytes, offset, bytes.Length - offset, token).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += count;
            }
        }

        public void Dispose()
        {
            stop?.Cancel();
            client?.Dispose();
            client = null;
            stream = null;
            lock (pending)
            {
                foreach (var request in pending.Values)
                {
                    request.TrySetException(new IOException("Mất kết nối máy chủ."));
                }
                pending.Clear();
            }
        }
    }
}
