namespace TuTien.Model.Client
{
    public class TaskSelect : WorldAction
    {
        public short Id { get; set; }
        public long Revision { get; set; }
    }

    public class SkillSelect : TaskSelect
    {
        public int Slot { get; set; }
    }

    public class RealmAdvance : WorldAction
    {
        public long Revision { get; set; }
    }

    public class SkillUse : WorldAction
    {
        public int Slot { get; set; }
        public int TargetId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public string LifeId { get; set; } = string.Empty;
    }
}
