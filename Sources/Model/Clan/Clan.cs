namespace TuTien.Model.Clan
{
    public class Clan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<ClanMember> Members { get; set; }

        public Clan()
        {
            Name = string.Empty;
            Members = new List<ClanMember>();
        }
    }
}
