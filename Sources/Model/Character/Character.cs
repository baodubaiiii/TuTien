using TuTien.Application.Handlers.Character;
using TuTien.Model.ModelBase;
using TuTien.Model.Info;
using System.Text.Json.Serialization;
using System.Text.Json;
using GameSkill = TuTien.Model.SkillCharacter.SkillCharacter;
using TuTien.Model.Task;

namespace TuTien.Model.Character
{
    public class Character : CharacterBase
    {
        public int PlayerId { get; set; }
        public int Slot { get; set; }
        public int ClassId { get; set; }
        public int Gender { get; set; }
        public int AppearanceId { get; set; }
        public int RaceId { get; set; }
        public int ElementId { get; set; }
        public bool TalentSeen { get; set; }
        public int RealmId { get; set; }
        public int MapId { get; set; }
        public InfoGame InfoGame { get; set; }
        public long GameRevision { get; set; }
        [JsonIgnore]
        public long StateVersion { get; set; }
        [JsonIgnore]
        public int PlaySessionId { get; set; }
        [JsonIgnore]
        public int ZoneId { get; set; }
        [JsonIgnore]
        public float TargetX { get; set; }
        [JsonIgnore]
        public float TargetY { get; set; }
        [JsonIgnore]
        public long LastPositionSave { get; set; }
        [JsonIgnore]
        public bool PositionDirty { get; set; }
        [JsonIgnore]
        public int BagCapacity { get; set; }
        [JsonIgnore]
        public float MoveSpeed { get; set; }
        [JsonIgnore]
        public string PlayToken { get; set; }
        [JsonIgnore]
        public float AttackRange { get; set; }
        [JsonIgnore]
        public int AttackCooldown { get; set; }
        [JsonIgnore]
        public bool IsChangingMap { get; set; }

        public Character()
        {
            RealmId = 1;
            MapId = 1;
            InfoGame = new InfoGame();
            PlayToken = string.Empty;
            CharacterHandler = new CharacterHandler(this);
        }

        public Character Clone()
        {
            var copy = (Character)MemberwiseClone();
            copy.InfoChar = InfoChar.Clone();
            copy.InfoGame = InfoGame.Clone();
            copy.Skills = JsonSerializer.Deserialize<List<GameSkill>>(JsonSerializer.Serialize(Skills))!;
            copy.InfoTask = JsonSerializer.Deserialize<TaskInfo>(JsonSerializer.Serialize(InfoTask))!;
            copy.ItemBag = ItemBag.Select(item => item.Clone()).ToList();
            copy.ItemBody = ItemBody.Select(item => item.Clone()).ToList();
            copy.ItemBox = ItemBox.Select(item => item.Clone()).ToList();
            copy.Position = new Position { X = Position.X, Y = Position.Y };
            copy.CharacterHandler = new CharacterHandler(copy);
            return copy;
        }
    }
}
