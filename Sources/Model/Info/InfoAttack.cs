namespace TuTien.Model.Info
{
    public class InfoAttack
    {
        public long Id { get; set; }
        public int AttackerId { get; set; }
        public int TargetId { get; set; }
        public string AttackerType { get; set; } = "player";
        public string TargetType { get; set; } = "player";
        public short SkillId { get; set; }
        public long Damage { get; set; }
        public bool IsCrit { get; set; }
        public bool IsDodge { get; set; }
        public long TargetHp { get; set; }
        public long TargetHpFull { get; set; }
        public long Time { get; set; }
    }
}
