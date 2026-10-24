using System.Text.Json;
using TuTien.Model.Effect;

namespace TuTien.Model.Info
{
    public class InfoGame
    {
        public int Version { get; set; }
        public int PointBudget { get; set; }
        public int[] Points { get; set; }
        public long Hp { get; set; }
        public long Mp { get; set; }
        public long LastAttackTime { get; set; }
        public long TuVi { get; set; }
        public long DeathTime { get; set; }
        public long LastMapChangeTime { get; set; }
        public short[] SkillSlots { get; set; } = new short[3];
        public InfoBreakthrough Breakthrough { get; set; } = new InfoBreakthrough();
        public List<ItemEffect> Effects { get; set; }

        public InfoGame()
        {
            Points = new int[5];
            Effects = new List<ItemEffect>();
        }

        public InfoGame Clone()
        {
            return JsonSerializer.Deserialize<InfoGame>(JsonSerializer.Serialize(this))!;
        }
    }
}
