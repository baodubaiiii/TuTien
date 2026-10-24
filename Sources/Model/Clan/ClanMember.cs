namespace TuTien.Model.Clan
{
    public class ClanMember
    {
        public int CharacterId { get; set; }
        public string Name { get; set; }
        public int Role { get; set; }

        public ClanMember()
        {
            Name = string.Empty;
        }
    }
}
