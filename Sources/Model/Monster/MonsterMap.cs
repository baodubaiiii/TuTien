using TuTien.Model.ModelBase;
using TuTien.Model.Template;
using GameZone = TuTien.Model.Map.Zone;
using TuTien.Application.Interfaces.Monster;

namespace TuTien.Model.Monster
{
    public class MonsterMap : MonsterBase
    {
        public string LifeId { get; set; } = Guid.NewGuid().ToString("N");
        public MonsterTemplate Template { get; set; } = new MonsterTemplate();
        public GameZone Zone { get; set; } = null!;
        public float HomeX { get; set; }
        public float HomeY { get; set; }
        public int TargetId { get; set; }
        public bool IsReturning { get; set; }
        public long LastAttackTime { get; set; }
        public long RespawnTime { get; set; }
        public IMonsterHandler MonsterHandler { get; set; } = null!;
    }
}
