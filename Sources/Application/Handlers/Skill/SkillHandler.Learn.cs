using TuTien.Application.Handlers.Npc;
using TuTien.Application.MainTasks;
using TuTien.Database;
using TuTien.Model.Info;
using TuTien.Model.SkillCharacter;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Skill
{
    public partial class SkillHandler
    {
        public static int GetSkillSlots(GameCharacter character, Cache cache)
        {
            return Math.Clamp(cache.RealmTemplates.GetValueOrDefault(character.RealmId)?.SkillSlots ?? 0, 1, 3);
        }

        public string? LearnSkill(GameCharacter character, short id)
        {
            if (_cache == null || _config == null || !_cache.SkillTemplates.TryGetValue(id, out var template) ||
                !template.IsImplemented || template.ClassId != character.ClassId)
            {
                return "Kỹ năng chưa triển khai hoặc không thuộc pháp khí hiện tại.";
            }
            if (!NpcHandler.CanInteract(character, template.TeacherNpcId, _config.InteractionRange))
            {
                return "Hãy đến gần NPC truyền công.";
            }
            var skill = character.Skills.Find(value => value.TemplateId == id);
            int level = (skill?.Level ?? 0) + 1;
            var data = template.SkillDataTemplates.Find(value => value.Point == level);
            if (data == null || _cache.RealmTemplates[character.RealmId].Rank < _cache.RealmTemplates[data.RealmId].Rank || character.InfoGame.TuVi < data.TuViCost)
            {
                return "Kỹ năng đã tối đa, cảnh giới chưa đủ hoặc thiếu tu vi.";
            }
            character.InfoGame.TuVi -= data.TuViCost;
            if (skill == null)
            {
                skill = new SkillCharacter { Id = id, TemplateId = id };
                character.Skills.Add(skill);
                for (int index = 0; index < GetSkillSlots(character, _cache); index++)
                {
                    if (character.InfoGame.SkillSlots[index] == 0)
                    {
                        character.InfoGame.SkillSlots[index] = id;
                        break;
                    }
                }
            }
            skill.Level = level;
            new TaskHandler(_cache, _config).UpdateCount(character, "learn", id);
            return null;
        }

        public string? AssignSkill(GameCharacter character, short id, int slot)
        {
            if (_cache == null || slot < 0 || slot >= GetSkillSlots(character, _cache) || character.InfoGame.Breakthrough.Active ||
                (id != 0 && !character.Skills.Any(value => value.TemplateId == id)))
            {
                return "Ô kỹ năng chưa mở hoặc kỹ năng chưa học.";
            }
            for (int index = 0; index < 3; index++)
            {
                if (character.InfoGame.SkillSlots[index] == id)
                {
                    character.InfoGame.SkillSlots[index] = 0;
                }
            }
            character.InfoGame.SkillSlots[slot] = id;
            return null;
        }

        private InfoChar? PrepareAttack(GameCharacter source, int slot, long now, out float range, out short skillId, out string error)
        {
            range = _config!.AttackRange;
            skillId = 0;
            error = string.Empty;
            if (source.InfoGame.Breakthrough.Active)
            {
                error = "Không thể chiến đấu khi đang độ kiếp.";
                return null;
            }
            var info = source.InfoChar.Clone();
            if (slot == -1)
            {
                return info;
            }
            if (slot < 0 || slot >= GetSkillSlots(source, _cache!))
            {
                error = "Ô kỹ năng chưa mở.";
                return null;
            }
            skillId = source.InfoGame.SkillSlots[slot];
            short id = skillId;
            var skill = source.Skills.Find(value => value.TemplateId == id);
            if (skill == null || !_cache!.SkillTemplates.TryGetValue(skillId, out var template) || !template.IsImplemented || template.ClassId != source.ClassId)
            {
                error = "Ô này chưa có kỹ năng hợp lệ.";
                return null;
            }
            var data = template.SkillDataTemplates.Find(value => value.Point == skill.Level);
            if (data == null || _cache.RealmTemplates[source.RealmId].Rank < _cache.RealmTemplates[data.RealmId].Rank ||
                source.InfoGame.Mp < data.ManaUse || now - skill.LastTimeUse < data.CoolDown)
            {
                error = "Thiếu MP, cảnh giới chưa đủ hoặc kỹ năng chưa hồi.";
                return null;
            }
            source.InfoGame.Mp -= data.ManaUse;
            skill.LastTimeUse = now;
            range = data.Dx;
            info.DamageFull = (long)Math.Min(long.MaxValue, decimal.Floor((decimal)info.DamageFull * data.Damage / 100));
            return info;
        }
    }
}
