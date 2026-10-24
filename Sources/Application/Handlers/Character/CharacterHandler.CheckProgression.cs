using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Character
{
    public partial class CharacterHandler
    {
        private void CheckProgression(GameCharacter character)
        {
            if (character.InfoGame.TuVi < 0 || character.InfoGame.SkillSlots == null || character.InfoGame.SkillSlots.Length != 3 ||
                character.Skills.Count > _cache!.SkillTemplates.Count || character.Skills.Select(value => value.TemplateId).Distinct().Count() != character.Skills.Count ||
                character.Skills.Any(skill => !_cache.SkillTemplates.TryGetValue(skill.TemplateId, out var template) || !template.IsImplemented ||
                    template.ClassId != character.ClassId || skill.Level < 1 || skill.Level > template.MaxPoint || skill.LastTimeUse < 0) ||
                character.InfoGame.SkillSlots.Any(id => id != 0 && !character.Skills.Any(skill => skill.TemplateId == id)) ||
                character.InfoGame.SkillSlots.Where(id => id != 0).Distinct().Count() != character.InfoGame.SkillSlots.Count(id => id != 0))
            {
                throw new InvalidDataException("Invalid stored skills");
            }
            if (character.InfoTask.Completed == null || character.InfoTask.Completed.Count > _cache.TaskTemplates.Count ||
                character.InfoTask.Completed.Distinct().Count() != character.InfoTask.Completed.Count ||
                character.InfoTask.Completed.Any(id => !_cache.TaskTemplates.ContainsKey(id)))
            {
                throw new InvalidDataException("Invalid stored task history");
            }
            var task = character.InfoTask.MainTask;
            if (task != null && (!_cache.TaskTemplates.TryGetValue(task.Id, out var data) || task.Count < 0 ||
                task.Count > data.RequiredCount || task.Max != data.RequiredCount || character.InfoTask.Completed.Contains(task.Id)))
            {
                throw new InvalidDataException("Invalid active task");
            }
            var trial = character.InfoGame.Breakthrough;
            if (trial == null || (trial.Active && (!_cache.RealmTemplates.TryGetValue(trial.TargetRealmId, out var realm) ||
                !realm.IsImplemented || _cache.RealmTemplates[character.RealmId].NextRealmId != realm.Id ||
                trial.ReservedTuVi < 0 || trial.ReservedTuVi > 1000000000 || trial.NextStrikeTime < 1 ||
                trial.DamagePercent == null || trial.DamagePercent.Length < 1 || trial.DamagePercent.Length > 36 ||
                trial.DamagePercent.Any(value => value < 1 || value > 100) || trial.StrikeCount < 0 || trial.StrikeCount >= trial.DamagePercent.Length ||
                trial.StrikeInterval < 200 || trial.StrikeInterval > 30000 || trial.LossMin < 0 || trial.LossMax > 100 || trial.LossMax < trial.LossMin)))
            {
                throw new InvalidDataException("Invalid breakthrough data");
            }
        }
    }
}
