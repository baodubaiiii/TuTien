using System.Text.Json;

namespace TuTien.Model.Client
{
    public class TcpMessage
    {
        public int Version { get; set; }
        public ulong Sequence { get; set; }
        public string Type { get; set; }
        public JsonElement Data { get; set; }

        public TcpMessage()
        {
            Type = string.Empty;
        }
    }
}
