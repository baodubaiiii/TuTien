namespace TuTien.Model.Item
{
    public class ItemMap
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public int PlayerId { get; set; }
        public int MapId { get; set; }
        public int ZoneId { get; set; }
        public short ItemId { get; set; }
        public int Quantity { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public long OwnerTime { get; set; }
        public long LeftTime { get; set; }
    }
}
