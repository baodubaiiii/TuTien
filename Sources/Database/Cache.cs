using TuTien.Model.Template;
using TuTien.Model.Info;

namespace TuTien.Database
{
    public class Cache
    {
        public Dictionary<short, ItemTemplate> ItemTemplates { get; set; }
        public Dictionary<int, ItemOptionTemplate> ItemOptionTemplates { get; set; }
        public Dictionary<short, SkillTemplate> SkillTemplates { get; set; }
        public Dictionary<short, NpcTemplate> NpcTemplates { get; set; }
        public Dictionary<short, BossTemplate> BossTemplates { get; set; }
        public Dictionary<short, TaskTemplate> TaskTemplates { get; set; }
        public Dictionary<int, RaceTemplate> RaceTemplates { get; set; }
        public Dictionary<int, ElementTemplate> ElementTemplates { get; set; }
        public Dictionary<int, ClassTemplate> ClassTemplates { get; set; }
        public Dictionary<int, AppearanceTemplate> AppearanceTemplates { get; set; }
        public Dictionary<int, MapTemplate> MapTemplates { get; set; }
        public Dictionary<int, RealmTemplate> RealmTemplates { get; set; }
        public Dictionary<int, MonsterTemplate> MonsterTemplates { get; set; } = new Dictionary<int, MonsterTemplate>();
        public InfoChar CharacterInfo { get; set; }

        public bool IsCreationReady
        {
            get
            {
                return RaceTemplates.Count == 5 && ElementTemplates.Count == 5 && ClassTemplates.Count == 5 && AppearanceTemplates.Count > 0;
            }
        }

        public Cache()
        {
            ItemTemplates = new Dictionary<short, ItemTemplate>();
            ItemOptionTemplates = new Dictionary<int, ItemOptionTemplate>();
            SkillTemplates = new Dictionary<short, SkillTemplate>();
            NpcTemplates = new Dictionary<short, NpcTemplate>();
            BossTemplates = new Dictionary<short, BossTemplate>();
            TaskTemplates = new Dictionary<short, TaskTemplate>();
            RaceTemplates = new Dictionary<int, RaceTemplate>();
            ElementTemplates = new Dictionary<int, ElementTemplate>();
            ClassTemplates = new Dictionary<int, ClassTemplate>();
            AppearanceTemplates = new Dictionary<int, AppearanceTemplate>();
            MapTemplates = new Dictionary<int, MapTemplate>();
            RealmTemplates = new Dictionary<int, RealmTemplate>();
            CharacterInfo = new InfoChar();
        }

        public void CheckTemplates()
        {
            foreach (var monster in MonsterTemplates.Values)
            {
                if (monster.Id < 1 || string.IsNullOrWhiteSpace(monster.Name) || monster.Hp < 1 || monster.Hp > 1000000000 ||
                    monster.Damage < 0 || monster.Damage > 1000000000 || monster.Defence < 0 || monster.Defence > 1000000000 ||
                    monster.Speed < 1 || monster.Speed > 600 || monster.AttackRange < 16 || monster.AggroRange < monster.AttackRange ||
                    monster.LeashRange < monster.AggroRange || monster.LeashRange > 2000 || monster.AttackCooldown < 200 ||
                    monster.RespawnSeconds < 1 || monster.RespawnSeconds > 86400 || monster.TuVi < 0 || monster.TuVi > 1000000 ||
                    !ItemTemplates.TryGetValue(monster.DropItemId, out var item) || monster.DropQuantity < 1 ||
                    monster.DropQuantity > item.MaxStack || monster.DropChance < 0 || monster.DropChance > 100)
                {
                    throw new InvalidDataException("Invalid monster template");
                }
            }
            foreach (var item in ItemTemplates.Values)
            {
                if (!RealmTemplates.ContainsKey(item.RealmId) || item.MaxStack < 1 || item.ClassId < 0 || item.ClassId > 5 ||
                    item.ElementId < 0 || item.ElementId > 5 || item.Options.Any(option => !ItemOptionTemplates.ContainsKey(option.Id)) ||
                    (item.DurationSeconds > 0 && (item.Type != 6 || item.EffectStatId < 1 || item.EffectStatId > 5 || item.DurationSeconds > 86400 || item.RestorePercent < 0 || item.RestorePercent > 100 || item.EffectParam < 0)))
                {
                    throw new InvalidDataException("Invalid item template reference");
                }
            }

            foreach (var map in MapTemplates.Values)
            {
                var ids = new HashSet<int>();
                foreach (var spawn in map.Mob)
                {
                    if (!ids.Add(spawn.Id) || spawn.Id < 1 || !MonsterTemplates.ContainsKey(spawn.TemplateId) ||
                        !float.IsFinite(spawn.X) || !float.IsFinite(spawn.Y) || spawn.X < 16 || spawn.Y < 16 ||
                        spawn.X > map.Width - 16 || spawn.Y > map.Height - 16 || map.Type == 0)
                    {
                        throw new InvalidDataException("Invalid monster placement");
                    }
                }
                foreach (var npc in map.ArrNpc)
                {
                    if (npc.Length < 4 || npc.Length > 5 || npc[0] < 0 || npc[0] > 1 || npc[3] < 0 || npc[3] > short.MaxValue ||
                        !NpcTemplates.ContainsKey((short)npc[3]) || npc[1] < 0 || npc[1] > map.Width || npc[2] < 0 || npc[2] > map.Height)
                    {
                        throw new InvalidDataException("Invalid map NPC placement");
                    }
                }
                foreach (var rectangle in map.MapCollisions)
                {
                    float x = rectangle.GetProperty("x").GetSingle();
                    float y = rectangle.GetProperty("y").GetSingle();
                    float width = rectangle.GetProperty("width").GetSingle();
                    float height = rectangle.GetProperty("height").GetSingle();
                    if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(width) || !float.IsFinite(height) ||
                        x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > map.Width || y + height > map.Height)
                    {
                        throw new InvalidDataException("Invalid map collision rectangle");
                    }
                }
                if (!RealmTemplates.ContainsKey(map.RealmId) || map.Width < 1 || map.Height < 1 ||
                    map.SpawnX < 0 || map.SpawnX > map.Width || map.SpawnY < 0 || map.SpawnY > map.Height ||
                    map.Waypoint.Any(point => !MapTemplates.ContainsKey(point.MapId) || point.X < 0 || point.X > map.Width || point.Y < 0 || point.Y > map.Height))
                {
                    throw new InvalidDataException("Invalid map template reference");
                }
            }

            foreach (var npc in NpcTemplates.Values)
            {
                if (!RealmTemplates.ContainsKey(npc.RealmId))
                {
                    throw new InvalidDataException("Invalid NPC template reference");
                }
            }

            foreach (var skill in SkillTemplates.Values)
            {
                if (skill.IsImplemented && (!NpcTemplates.ContainsKey(skill.TeacherNpcId) || skill.SkillDataTemplates.Any(level =>
                    !RealmTemplates.ContainsKey(level.RealmId) || level.TuViCost < 0 || level.TuViCost > 1000000000 ||
                    level.ManaUse < 1 || level.CoolDown < 200 || level.CoolDown > 60000 || level.Dx < 30 || level.Dx > 600 ||
                    level.Damage < 1 || level.Damage > 1000 || level.MaxFight != 1)))
                {
                    throw new InvalidDataException("Invalid implemented skill data");
                }
                if (!RealmTemplates.ContainsKey(skill.RealmId) || skill.ClassId < 1 || skill.ClassId > 5 ||
                    skill.SkillDataTemplates.Count != skill.MaxPoint ||
                    skill.SkillDataTemplates.Any(level => level.SkillId != skill.Id || level.Point < 1 || level.Point > skill.MaxPoint) ||
                    skill.SkillDataTemplates.Select(level => level.Point).Distinct().Count() != skill.MaxPoint)
                {
                    throw new InvalidDataException("Invalid skill template reference");
                }
            }
            foreach (var task in TaskTemplates.Values)
            {
                if (task.Id < 1 || !NpcTemplates.ContainsKey(task.NpcId) || task.PreviousId == task.Id ||
                    (task.PreviousId != 0 && !TaskTemplates.ContainsKey(task.PreviousId)) ||
                    task.RequiredCount < 1 || task.RequiredCount > 1000 || task.RewardTuVi < 0 || task.RewardTuVi > 1000000 ||
                    task.RewardPoints < 0 || task.RewardPoints > 10000 || task.Reward == null || task.Reward.Count > 40 ||
                    task.Reward.Any(item => item == null || !ItemTemplates.ContainsKey(item.Id) || item.Quantity < 1 || item.Quantity > 1000000 ||
                        item.Options == null || item.Options.Count > 64 || item.Options.Any(option => option == null || !ItemOptionTemplates.ContainsKey(option.Id)) ||
                        item.Options.Select(option => (option.Id, option.Type)).Distinct().Count() != item.Options.Count) ||
                    (task.Event != "talk" && task.Event != "kill" && task.Event != "learn" && task.Event != "cast") ||
                    (task.Event == "talk" && (task.TargetId < 1 || task.TargetId > short.MaxValue || !NpcTemplates.ContainsKey((short)task.TargetId))) ||
                    ((task.Event == "learn" || task.Event == "cast") && task.TargetId != 0 &&
                        (task.TargetId < 1 || task.TargetId > short.MaxValue || !SkillTemplates.TryGetValue((short)task.TargetId, out var skill) || !skill.IsImplemented)) ||
                    (task.Event == "kill" && !MonsterTemplates.ContainsKey(task.TargetId)))
                {
                    throw new InvalidDataException("Invalid task template");
                }
                var visited = new HashSet<short>();
                short current = task.Id;
                while (current != 0)
                {
                    if (!visited.Add(current) || !TaskTemplates.TryGetValue(current, out var parent))
                    {
                        throw new InvalidDataException("Task prerequisite cycle");
                    }
                    current = parent.PreviousId;
                }
            }
            foreach (var realm in RealmTemplates.Values.Where(value => value.IsImplemented))
            {
                if (realm.TuViRequired < 0 || realm.TuViRequired > 1000000000 || realm.PointReward < 0 || realm.PointReward > 10000 ||
                    (realm.RequiredTaskId != 0 && !TaskTemplates.ContainsKey(realm.RequiredTaskId)) ||
                    (realm.NextRealmId != 0 && (!RealmTemplates.TryGetValue(realm.NextRealmId, out var next) || !next.IsImplemented || next.Rank <= realm.Rank)))
                {
                    throw new InvalidDataException("Invalid realm progression");
                }
            }

        }
    }
}
