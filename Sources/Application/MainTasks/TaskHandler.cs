using TuTien.Application.Handlers.Item;
using TuTien.Application.Handlers.Npc;
using TuTien.Database;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.MainTasks
{
    public class TaskHandler
    {
        private readonly Cache _cache;
        private readonly ConfigManager _config;

        public TaskHandler(Cache cache, ConfigManager config)
        {
            _cache = cache;
            _config = config;
        }

        public string? AcceptTask(GameCharacter character, short id)
        {
            if (!_cache.TaskTemplates.TryGetValue(id, out var template) || !NpcHandler.CanInteract(character, template.NpcId, _config.InteractionRange))
            {
                return "Hãy đến gần NPC giao nhiệm vụ.";
            }
            if (character.InfoTask.MainTask != null || character.InfoTask.Completed.Contains(id) ||
                (template.PreviousId != 0 && !character.InfoTask.Completed.Contains(template.PreviousId)))
            {
                return "Nhiệm vụ chưa mở, đã nhận hoặc đã hoàn thành.";
            }
            character.InfoTask.MainTask = new TuTien.Model.Task.Task
            {
                Id = id,
                Max = template.RequiredCount,
                Count = (short)(template.Event == "learn" && character.Skills.Count > 0 ? 1 : 0)
            };
            return null;
        }

        public string? RewardDoneTask(GameCharacter character, short id)
        {
            var task = character.InfoTask.MainTask;
            if (!_cache.TaskTemplates.TryGetValue(id, out var template) || task == null || task.Id != id ||
                task.Count < template.RequiredCount || character.InfoTask.Completed.Contains(id))
            {
                return "Nhiệm vụ chưa hoàn thành hoặc đã nhận thưởng.";
            }
            if (!NpcHandler.CanInteract(character, template.NpcId, _config.InteractionRange))
            {
                return "Hãy trở lại NPC để trả nhiệm vụ.";
            }
            if (character.InfoGame.TuVi > long.MaxValue - template.RewardTuVi || character.InfoGame.PointBudget > 1000000 - template.RewardPoints)
            {
                return "Không thể nhận thêm tài nguyên.";
            }
            var draft = character.Clone();
            var items = new ItemHandler(_cache);
            foreach (var reward in template.Reward)
            {
                if (!items.AddItemToBag(draft, reward, out string error))
                {
                    return error;
                }
            }
            character.ItemBag = draft.ItemBag;
            character.InfoGame.TuVi += template.RewardTuVi;
            character.InfoGame.PointBudget += template.RewardPoints;
            character.InfoTask.Completed.Add(id);
            character.InfoTask.MainTask = null;
            return null;
        }

        public bool UpdateCount(GameCharacter character, string type, int targetId)
        {
            var task = character.InfoTask.MainTask;
            if (task == null || !_cache.TaskTemplates.TryGetValue(task.Id, out var template) || template.Event != type ||
                (template.TargetId != 0 && template.TargetId != targetId) || task.Count >= template.RequiredCount)
            {
                return false;
            }
            task.Count++;
            return true;
        }
    }
}
