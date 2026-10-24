using GameItem = TuTien.Model.Item.Item;

namespace TuTien.Model.Template
{
    public class TaskTemplate
    {
        public short Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public short NpcId { get; set; }
        public short PreviousId { get; set; }
        public string Event { get; set; } = string.Empty;
        public int TargetId { get; set; }
        public int RequiredCount { get; set; }
        public long RewardTuVi { get; set; }
        public int RewardPoints { get; set; }
        public List<GameItem> Reward { get; set; } = new List<GameItem>();

        public TaskTemplate()
        {
            Name = string.Empty;
        }
    }
}
