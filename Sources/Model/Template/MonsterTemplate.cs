namespace TuTien.Model.Template
{
    public class MonsterTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long Hp { get; set; }
        public long Damage { get; set; }
        public long Defence { get; set; }
        public int Speed { get; set; }
        public int AttackRange { get; set; }
        public int AggroRange { get; set; }
        public int LeashRange { get; set; }
        public int AttackCooldown { get; set; }
        public int RespawnSeconds { get; set; }
        public long TuVi { get; set; }
        public short DropItemId { get; set; }
        public int DropQuantity { get; set; }
        public int DropChance { get; set; }
    }
}
