using System.Text.Json;
using TuTien.Database;
using TuTien.Model.Template;
using Xunit;

namespace TuTien.Tests
{
    public class TemplateTests
    {
        private static Cache Load()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "TuTien.Server.csproj")))
            {
                root = root.Parent;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "content", "source", "base", "catalog.json")));
            var data = document.RootElement;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return new Cache
            {
                ItemTemplates = data.GetProperty("items").Deserialize<List<ItemTemplate>>(options)!.ToDictionary(value => value.Id),
                ItemOptionTemplates = data.GetProperty("itemOptions").Deserialize<List<ItemOptionTemplate>>(options)!.ToDictionary(value => value.Id),
                NpcTemplates = data.GetProperty("npcs").Deserialize<List<NpcTemplate>>(options)!.ToDictionary(value => value.Id),
                SkillTemplates = data.GetProperty("skills").Deserialize<List<SkillTemplate>>(options)!.ToDictionary(value => value.Id),
                TaskTemplates = data.GetProperty("tasks").Deserialize<List<TaskTemplate>>(options)!.ToDictionary(value => value.Id),
                MapTemplates = data.GetProperty("maps").Deserialize<List<MapTemplate>>(options)!.ToDictionary(value => value.Id),
                MonsterTemplates = data.TryGetProperty("monsters", out var monsters) ? monsters.Deserialize<List<MonsterTemplate>>(options)!.ToDictionary(value => value.Id) : new(),
                RealmTemplates = data.GetProperty("realms").Deserialize<List<RealmTemplate>>(options)!.ToDictionary(value => value.Id),
            };
        }

        [Fact]
        public void CatalogContainsCompleteLinkedTemplateLists()
        {
            var cache = Load();
            cache.CheckTemplates();
            Assert.Equal(143, cache.ItemTemplates.Count);
            Assert.Equal(5, cache.ItemOptionTemplates.Count);
            Assert.Equal(16, cache.MapTemplates.Count);
            Assert.Equal(16, cache.NpcTemplates.Count);
            Assert.Equal(50, cache.SkillTemplates.Count);
            Assert.Equal(40, cache.RealmTemplates.Count);
            Assert.Equal(16, cache.MapTemplates.Values.Sum(map => map.ArrNpc.Length));
        }

        [Fact]
        public void MapNpcArrayRejectsUnknownTemplatesAndInvalidShape()
        {
            var cache = Load();
            cache.MapTemplates[1].ArrNpc = new[] { new[] { 1, 500, 600, 9999, 0 } };
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
            cache.MapTemplates[1].ArrNpc = new[] { new[] { 1, 500 } };
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
        }

        [Fact]
        public void TaskRewardJsonRejectsInvalidAmountsAndItemReferences()
        {
            var cache = Load();
            var template = cache.TaskTemplates[1];
            template.RewardTuVi = -1;
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
            template.RewardTuVi = 40;
            var reward = template.Reward[0];
            reward.Id = 9999;
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
            reward.Id = 3010;
            reward.Quantity = 0;
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
            template.Reward.Clear();
            cache.CheckTemplates();
        }

        [Fact]
        public void RealmTreeMatchesRequestedStages()
        {
            var cache = Load();
            int[] sizes = { 1, 10, 4, 9, 4, 4, 4, 4 };
            for (int group = 1; group <= sizes.Length; group++)
            {
                var stages = cache.RealmTemplates.Values.Where(value => value.GroupId == group).OrderBy(value => value.Rank).ToArray();
                Assert.Equal(sizes[group - 1], stages.Length);
                if (group == 2)
                {
                    Assert.All(stages, value => Assert.Equal(3, value.SkillSlots));
                }
            }

            Assert.Equal("Phàm Nhân", cache.RealmTemplates[1].Name);
            Assert.Equal(Enumerable.Range(1, 40), cache.RealmTemplates.Values.OrderBy(value => value.Rank).Select(value => value.Rank));
        }

        [Fact]
        public void EveryClassHasStarterWeaponAndTenSkillDefinitions()
        {
            var cache = Load();
            for (int classId = 1; classId <= 5; classId++)
            {
                Assert.Contains(cache.ItemTemplates.Values, item => item.Type == 0 && item.ClassId == classId && item.RealmId == 1);
                Assert.Equal(10, cache.SkillTemplates.Values.Count(skill => skill.ClassId == classId));
            }
        }

        [Fact]
        public void BrokenReferencesAreRejectedBeforePublishing()
        {
            var cache = Load();
            cache.ItemTemplates.Values.First().Options.Add(new TuTien.Model.Option.OptionItem { Id = 999 });
            Assert.Throws<InvalidDataException>(() => cache.CheckTemplates());
        }

        [Fact]
        public void MapLinksAreConnectedAndBidirectional()
        {
            var cache = Load();
            var visited = new HashSet<int>();
            var pending = new Queue<int>();
            pending.Enqueue(1);
            while (pending.TryDequeue(out int id))
            {
                if (!visited.Add(id))
                {
                    continue;
                }

                foreach (var link in cache.MapTemplates[id].Waypoint)
                {
                    Assert.Contains(cache.MapTemplates[link.MapId].Waypoint, value => value.MapId == id);
                    pending.Enqueue(link.MapId);
                }
            }

            Assert.Equal(16, visited.Count);
        }
    }
}
