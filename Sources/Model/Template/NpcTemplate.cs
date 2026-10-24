namespace TuTien.Model.Template
{
    public class NpcTemplate
    {
        public short Id { get; set; }
        public string Name { get; set; }
        public int Head { get; set; }
        public int Body { get; set; }
        public int Leg { get; set; }
        public int Avatar { get; set; }
        public string[][] Menu { get; set; }
        public List<string> Chats { get; set; }
        public int RealmId { get; set; }
        public string Role { get; set; }
        public string AssetKey { get; set; }

        public NpcTemplate()
        {
            Name = string.Empty;
            Role = string.Empty;
            AssetKey = string.Empty;
            Menu = Array.Empty<string[]>();
            Chats = new List<string>();
        }
    }
}
