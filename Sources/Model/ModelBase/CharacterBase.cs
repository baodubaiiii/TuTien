using TuTien.Application.Interfaces.Character;
using TuTien.Application.Interfaces.Zone;
using GameClan = TuTien.Model.Clan.Clan;
using TuTien.Model.Info;
using TuTien.Model.Task;
using GameItem = TuTien.Model.Item.Item;
using GameSkill = TuTien.Model.SkillCharacter.SkillCharacter;

namespace TuTien.Model.ModelBase
{
    public class CharacterBase : ICharacter
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Position Position { get; set; }
        public InfoChar InfoChar { get; set; }
        public IZone? Zone { get; set; }
        public ICharacterHandler? CharacterHandler { get; set; }
        public GameClan? Clan { get; set; }
        public Player? Player { get; set; }
        public List<GameItem> ItemBody { get; set; }
        public List<GameItem> ItemBag { get; set; }
        public List<GameItem> ItemBox { get; set; }
        public List<GameSkill> Skills { get; set; }
        public TaskInfo InfoTask { get; set; }

        public CharacterBase()
        {
            Name = string.Empty;
            Position = new Position();
            InfoChar = new InfoChar();
            ItemBody = new List<GameItem>();
            ItemBag = new List<GameItem>();
            ItemBox = new List<GameItem>();
            Skills = new List<GameSkill>();
            InfoTask = new TaskInfo();
        }
    }
}
