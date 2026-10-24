namespace TuTien.Model.Template
{
    public class RealmTemplate
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Name { get; set; }
        public string Stage { get; set; }
        public int Rank { get; set; }
        public string Description { get; set; }
        public int SkillSlots { get; set; }
        public bool IsImplemented { get; set; }
        public int NextRealmId { get; set; }
        public long TuViRequired { get; set; }
        public int PointReward { get; set; }
        public bool RequiresTrial { get; set; }
        public short RequiredTaskId { get; set; }

        public RealmTemplate()
        {
            Name = string.Empty;
            Stage = string.Empty;
            Description = string.Empty;
        }
    }
}
