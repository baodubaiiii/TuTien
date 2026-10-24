namespace TuTien.Model.ModelBase
{
    public class TaskBase
    {
        public short Id { get; set; }
        public List<string> SubNames { get; set; }
        public List<short> Counts { get; set; }
        public List<string> ContentInfo { get; set; }
        public long[] Reward { get; set; }

        public TaskBase()
        {
            SubNames = new List<string>();
            Counts = new List<short>();
            ContentInfo = new List<string>();
            Reward = Array.Empty<long>();
        }
    }
}
