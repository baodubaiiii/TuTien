namespace TuTien.Model.Client
{
    public class GameInput
    {
        public int Version { get; set; }
        public ulong Sequence { get; set; }
        public string Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public GameInput()
        {
            Type = string.Empty;
        }
    }
}
