namespace TuTien.Model.Client
{
    public class WorldAction
    {
        public string PlayToken { get; set; } = string.Empty;
    }

    public class MapChange : WorldAction
    {
        public int MapId { get; set; }
        public int ZoneId { get; set; }
    }

    public class MonsterAttack : WorldAction
    {
        public int TargetId { get; set; }
        public string LifeId { get; set; } = string.Empty;
    }

    public class ItemMapPick : WorldAction
    {
        public string Id { get; set; } = string.Empty;
    }

    public class NpcSelect : WorldAction
    {
        public int Id { get; set; }
    }
}
