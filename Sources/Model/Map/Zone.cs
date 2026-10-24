using TuTien.Application.Handlers.Map;
using TuTien.Application.Interfaces.Character;
using TuTien.Application.Interfaces.Map;
using TuTien.Application.Interfaces.Zone;
using TuTien.Model.Character;
using TuTien.Model.Info;
using TuTien.Model.Monster;
using TuTien.Model.Item;

namespace TuTien.Model.Map
{
    public class Zone : IZone
    {
        private readonly Dictionary<int, ICharacter> _characters;

        public int Id { get; set; }
        public IMap? Map { get; set; }
        public IReadOnlyDictionary<int, ICharacter> Characters
        {
            get
            {
                return _characters;
            }
        }

        public IZoneHandler ZoneHandler { get; set; }
        public List<Npc> Npcs { get; }
        public List<Boss> Bosses { get; }
        public Queue<InfoAttack> Attacks { get; }
        public Dictionary<int, MonsterMap> MonsterMaps { get; } = new Dictionary<int, MonsterMap>();
        public Dictionary<string, ItemMap> ItemMaps { get; } = new Dictionary<string, ItemMap>();
        public HashSet<int> Reservations { get; } = new HashSet<int>();

        public Zone()
        {
            _characters = new Dictionary<int, ICharacter>();
            Npcs = new List<Npc>();
            Bosses = new List<Boss>();
            Attacks = new Queue<InfoAttack>();
            ZoneHandler = new ZoneNormalHandler(this);
        }

        public bool JoinZone(ICharacter character)
        {
            return _characters.TryAdd(character.Id, character);
        }

        public bool OutZone(int id)
        {
            return _characters.Remove(id);
        }
    }
}
