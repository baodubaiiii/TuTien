namespace TuTien.Model.Info
{
    public class InfoBreakthrough
    {
        public bool Active { get; set; }
        public int TargetRealmId { get; set; }
        public long ReservedTuVi { get; set; }
        public long NextStrikeTime { get; set; }
        public int StrikeCount { get; set; }
        public string Result { get; set; } = string.Empty;
        public long LostTuVi { get; set; }
        public long LastDamage { get; set; }
        public int[] DamagePercent { get; set; } = Array.Empty<int>();
        public int StrikeInterval { get; set; }
        public int LossMin { get; set; }
        public int LossMax { get; set; }
    }
}
