using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.MainTasks;
using TuTien.Model.Map;
using TuTien.Model.Option;
using System.Text.Json;
using Xunit;
using GameCharacter = TuTien.Model.Character.Character;
using GameItem = TuTien.Model.Item.Item;

namespace TuTien.Tests
{
    public class ProgressionTests
    {
        private static void Prepare(WorldTests.World world)
        {
            foreach (short id in new short[] { 101, 102 })
            {
                world.Zone.Npcs.Add(new Npc { Id = id, Status = 1, Zone = world.Zone, X = world.Hero.Position.X, Y = world.Hero.Position.Y });
            }
        }

        private static async Task<GameCharacter?> Change(WorldTests.World world, Func<GameCharacter, string?> action, bool allowTrial = false)
        {
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            var pending = handler.Handle(world.Hero.PlayToken, world.Hero.GameRevision, world.Characters, action, _ => { }, CancellationToken.None, allowTrial);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return await pending;
        }

        private static async Task ReadyTrial(WorldTests.World world)
        {
            Prepare(world);
            world.Hero.InfoGame.TuVi = 500;
            world.Hero.InfoTask.Completed.AddRange(new short[] { 1, 2, 3, 4 });
            world.Hero.InfoGame.Hp = world.Hero.InfoChar.HpFull;
            Assert.True(await world.Characters.SaveGame(world.Hero));
        }

        [Fact]
        public async Task TaskChainChecksNpcCountsServerEventsAndClaimsOnce()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            var tasks = new TaskHandler(world.Cache, world.Config);
            Assert.Null(await Change(world, hero => tasks.AcceptTask(hero, 2)));
            world.Zone.Npcs[0].X += 1000;
            Assert.Null(await Change(world, hero => tasks.AcceptTask(hero, 1)));
            world.Zone.Npcs[0].X -= 1000;
            Assert.NotNull(await Change(world, hero => tasks.AcceptTask(hero, 1)));
            Assert.False(tasks.UpdateCount(world.Hero.Clone(), "kill", 102));
            Assert.NotNull(await Change(world, hero =>
            {
                Assert.False(tasks.UpdateCount(hero, "talk", 101));
                Assert.True(tasks.UpdateCount(hero, "talk", 102));
                Assert.False(tasks.UpdateCount(hero, "talk", 102));
                return null;
            }));
            long revision = world.Hero.GameRevision;
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            var first = handler.Handle(world.Hero.PlayToken, revision, world.Characters, hero => tasks.RewardDoneTask(hero, 1), _ => { }, CancellationToken.None);
            var second = handler.Handle(world.Hero.PlayToken, revision, world.Characters, hero => tasks.RewardDoneTask(hero, 1), _ => { }, CancellationToken.None);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.NotNull(await first);
            Assert.Null(await second);
            Assert.Equal(40, world.Hero.InfoGame.TuVi);
            var stored = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Equal(new short[] { 1 }, stored.InfoTask.Completed);
            Assert.Null(stored.InfoTask.MainTask);
            Assert.NotNull(await Change(world, hero => tasks.AcceptTask(hero, 2)));
            await world.Kill();
            Assert.Equal(1, world.Hero.InfoTask.MainTask!.Count);
            Assert.Equal(1, (await world.Characters.Load(world.Hero.Id))!.InfoTask.MainTask!.Count);
        }

        [Fact]
        public async Task FullBagAndStaleDatabaseCannotPartiallyClaimRewards()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            var tasks = new TaskHandler(world.Cache, world.Config);
            Assert.NotNull(await Change(world, hero => tasks.AcceptTask(hero, 1)));
            world.Hero.InfoTask.MainTask!.Count = 1;
            world.Hero.ItemBag.Clear();
            for (int index = 0; index < 40; index++)
            {
                var item = new ItemHandler(world.Cache).GetItemDefault(1101)!;
                item.IndexUI = index;
                world.Hero.ItemBag.Add(item);
            }
            Assert.True(await world.Characters.SaveGame(world.Hero));
            Assert.Null(await Change(world, hero => tasks.RewardDoneTask(hero, 1)));
            Assert.Empty(world.Hero.InfoTask.Completed);
            Assert.Equal(0, world.Hero.InfoGame.TuVi);
            world.Hero.ItemBag.RemoveAt(39);
            Assert.True(await world.Characters.SaveGame(world.Hero));
            Assert.True(await world.Characters.SaveGame(world.Hero.Clone()));
            Assert.Null(await Change(world, hero => tasks.RewardDoneTask(hero, 1)));
            var stored = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Empty(stored.InfoTask.Completed);
            Assert.Equal(0, stored.InfoGame.TuVi);
            Assert.Equal(39, stored.ItemBag.Count);
        }

        [Fact]
        public async Task RewardListPreservesOptionsSplitsStacksAndCreatesFreshInstancesOnce()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            world.Hero.ItemBag.Clear();
            int quantity = world.Cache.ItemTemplates[3010].MaxStack + 3;
            world.Cache.TaskTemplates[1].Reward = new List<GameItem>
            {
                new GameItem { Id = 3010, Quantity = quantity, Uid = "template-uid" },
                new GameItem { Id = 1101, Quantity = 2, Uid = "template-uid", Options = new List<OptionItem> { new OptionItem { Id = 1, Param = 37 } } },
                new GameItem { Id = 3010, Quantity = 2, Options = new List<OptionItem> { new OptionItem { Id = 1, Param = 99 } } }
            };
            string template = JsonSerializer.Serialize(world.Cache.TaskTemplates[1].Reward);
            var tasks = new TaskHandler(world.Cache, world.Config);
            Assert.NotNull(await Change(world, hero => tasks.AcceptTask(hero, 1)));
            world.Hero.InfoTask.MainTask!.Count = 1;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            Assert.NotNull(await Change(world, hero => tasks.RewardDoneTask(hero, 1)));
            var stored = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Equal(quantity, stored.ItemBag.Where(item => item.Id == 3010 && item.Options.Count == 0).Sum(item => item.Quantity));
            Assert.Equal(2, stored.ItemBag.Count(item => item.Id == 1101 && item.GetParamOption(1) == 37));
            Assert.Equal(2, Assert.Single(stored.ItemBag, item => item.Id == 3010 && item.GetParamOption(1) == 99).Quantity);
            Assert.Equal(stored.ItemBag.Count, stored.ItemBag.Select(item => item.Uid).Distinct().Count());
            Assert.All(stored.ItemBag, item => Assert.True(Guid.TryParseExact(item.Uid, "N", out _)));
            Assert.Equal(template, JsonSerializer.Serialize(world.Cache.TaskTemplates[1].Reward));
            Assert.Null(await Change(world, hero => tasks.RewardDoneTask(hero, 1)));
            Assert.Equal(stored.ItemBag.Count, world.Hero.ItemBag.Count);
        }

        [Fact]
        public async Task LastRewardNotFittingDoesNotGiveEarlierItemsOrResources()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            var tasks = new TaskHandler(world.Cache, world.Config);
            Assert.NotNull(await Change(world, hero => tasks.AcceptTask(hero, 1)));
            world.Hero.InfoTask.MainTask!.Count = 1;
            world.Hero.ItemBag.Clear();
            var items = new ItemHandler(world.Cache);
            for (int index = 0; index < 39; index++)
            {
                var item = items.GetItemDefault(1101)!;
                item.IndexUI = index;
                world.Hero.ItemBag.Add(item);
            }
            world.Cache.TaskTemplates[1].Reward = new List<GameItem> { items.GetItemDefault(3010)!, items.GetItemDefault(3020)! };
            Assert.True(await world.Characters.SaveGame(world.Hero));
            int points = world.Hero.InfoGame.PointBudget;
            Assert.NotNull(tasks.RewardDoneTask(world.Hero, 1));
            Assert.Equal(39, world.Hero.ItemBag.Count);
            Assert.Null(await Change(world, hero => tasks.RewardDoneTask(hero, 1)));
            var stored = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Equal(39, stored.ItemBag.Count);
            Assert.Equal(0, stored.InfoGame.TuVi);
            Assert.Equal(points, stored.InfoGame.PointBudget);
            Assert.Empty(stored.InfoTask.Completed);
            Assert.NotNull(stored.InfoTask.MainTask);
        }

        [Fact]
        public async Task SkillsCheckClassRealmSlotsMpCooldownRangeAndPersist()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            var skills = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients);
            world.Hero.InfoGame.TuVi = 200;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            Assert.Null(await Change(world, hero => skills.LearnSkill(hero, 201)));
            Assert.Null(await Change(world, hero => skills.LearnSkill(hero, 102)));
            Assert.NotNull(await Change(world, hero => skills.LearnSkill(hero, 101)));
            Assert.Equal(180, world.Hero.InfoGame.TuVi);
            Assert.Null(await Change(world, hero => skills.LearnSkill(hero, 101)));
            Assert.Null(await Change(world, hero => skills.AssignSkill(hero, 101, 1)));
            var copy = world.Hero.Clone();
            copy.Skills[0].Level = 10;
            copy.InfoGame.SkillSlots[0] = 0;
            Assert.Equal(1, world.Hero.Skills[0].Level);
            Assert.Equal(101, world.Hero.InfoGame.SkillSlots[0]);
            world.Monster.InfoChar.Hp = 100000;
            world.Hero.InfoGame.Mp = world.Hero.InfoChar.MpFull;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            long mp = world.Hero.InfoGame.Mp;
            world.Monster.Position.X += 1000;
            var far = skills.AttackMonster(world.Hero, 1, world.Monster.LifeId, world.Hero.PlayToken, skillSlot: 0);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await far);
            Assert.Equal(mp, world.Hero.InfoGame.Mp);
            Assert.Equal(0, world.Hero.Skills[0].LastTimeUse);
            world.Monster.Position.X -= 1000;
            var cast = skills.AttackMonster(world.Hero, 1, world.Monster.LifeId, world.Hero.PlayToken, skillSlot: 0);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Equal(101, (await cast)!.SkillId);
            var data = world.Cache.SkillTemplates[101].SkillDataTemplates[0];
            Assert.Equal(mp - data.ManaUse, world.Hero.InfoGame.Mp);
            var again = skills.AttackMonster(world.Hero, 1, world.Monster.LifeId, world.Hero.PlayToken, skillSlot: 0);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await again);
            var stored = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Equal(world.Hero.InfoGame.Mp, stored.InfoGame.Mp);
            Assert.Equal(world.Hero.Skills[0].LastTimeUse, stored.Skills[0].LastTimeUse);
        }

        [Fact]
        public async Task FailedSkillCommitDoesNotConsumeMpOrPublishDamage()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            world.Hero.InfoGame.TuVi = 100;
            var skills = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients);
            Assert.NotNull(await Change(world, hero => skills.LearnSkill(hero, 101)));
            world.Monster.InfoChar.Hp = 100000;
            Assert.True(await world.Characters.SaveGame(world.Hero.Clone()));
            long mp = world.Hero.InfoGame.Mp;
            var cast = skills.AttackMonster(world.Hero, 1, world.Monster.LifeId, world.Hero.PlayToken, skillSlot: 0);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await cast);
            Assert.Equal(mp, world.Hero.InfoGame.Mp);
            Assert.Equal(0, world.Hero.Skills[0].LastTimeUse);
            Assert.Equal(100000, world.Monster.InfoChar.Hp);
            Assert.Empty(world.Zone.Attacks);
        }

        [Fact]
        public async Task TrialReservesOnceResumesOverdueStrikesAndRewardsOnce()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            await ReadyTrial(world);
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            int points = world.Hero.InfoGame.PointBudget;
            Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
            Assert.Equal(400, world.Hero.InfoGame.TuVi);
            Assert.Equal(100, world.Hero.InfoGame.Breakthrough.ReservedTuVi);
            Assert.Null(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
            Assert.Null(await Change(world, hero => null));
            long first = world.Hero.InfoGame.Breakthrough.NextStrikeTime;
            Assert.True(await handler.UpdateRealm(first, world.Characters));
            var saved = (await world.Characters.Load(world.Hero.Id))!;
            Assert.Equal(1, saved.InfoGame.Breakthrough.StrikeCount);
            var restored = new CharacterHandler(saved, world.Cache, world.Config);
            saved.CharacterHandler = restored;
            restored.InitGame();
            Assert.True(await restored.UpdateRealm(first + 100000, world.Characters));
            Assert.Equal(2, saved.RealmId);
            Assert.Equal(points + 10, saved.InfoGame.PointBudget);
            Assert.Equal("success", saved.InfoGame.Breakthrough.Result);
            Assert.False(saved.InfoGame.Breakthrough.Active);
            Assert.Equal(3, SkillHandler.GetSkillSlots(saved, world.Cache));
            Assert.False(await restored.UpdateRealm(first + 200000, world.Characters));
            Assert.Equal(points + 10, (await world.Characters.Load(saved.Id))!.InfoGame.PointBudget);
        }

        [Fact]
        public async Task TrialFailureAndCancelChargeOnlyReservedTuViOnce()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            await ReadyTrial(world);
            Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
            Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).CancelBreakthrough(), true));
            Assert.InRange(world.Hero.InfoGame.TuVi, 480, 490);
            Assert.Null(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).CancelBreakthrough(), true));
            long before = world.Hero.InfoGame.TuVi;
            Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
            world.Hero.InfoGame.Hp = 1;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            Assert.True(await handler.UpdateRealm(world.Hero.InfoGame.Breakthrough.NextStrikeTime, world.Characters));
            Assert.Equal(0, world.Hero.InfoGame.Hp);
            Assert.Equal(1, world.Hero.RealmId);
            Assert.InRange(before - world.Hero.InfoGame.TuVi, 10, 20);
            Assert.False(world.Hero.InfoGame.Breakthrough.Active);
        }

        [Fact]
        public async Task StaleTrialTickRollsBackHpStrikesAndRealm()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            await ReadyTrial(world);
            Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
            Assert.True(await world.Characters.SaveGame(world.Hero.Clone()));
            long hp = world.Hero.InfoGame.Hp;
            Assert.False(await ((CharacterHandler)world.Hero.CharacterHandler!).UpdateRealm(world.Hero.InfoGame.Breakthrough.NextStrikeTime + 100000, world.Characters));
            Assert.Equal(hp, world.Hero.InfoGame.Hp);
            Assert.Equal(0, world.Hero.InfoGame.Breakthrough.StrikeCount);
            Assert.Equal(1, (await world.Characters.Load(world.Hero.Id))!.RealmId);
        }

        [Theory]
        [InlineData(1, 101)]
        [InlineData(2, 201)]
        [InlineData(3, 301)]
        [InlineData(4, 401)]
        [InlineData(5, 501)]
        public async Task EachClassCanLearnItsActiveSkillAndEmptyMpRejectsIt(int classId, short skillId)
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            world.Hero.ClassId = classId;
            world.Hero.InfoGame.TuVi = 20;
            var skills = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients);
            Assert.NotNull(await Change(world, hero => skills.LearnSkill(hero, skillId)));
            world.Hero.InfoGame.Mp = 0;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            var cast = skills.AttackMonster(world.Hero, 1, world.Monster.LifeId, world.Hero.PlayToken, skillSlot: 0);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await cast);
            Assert.Equal(0, world.Hero.Skills[0].LastTimeUse);
            Assert.Equal(1, world.Monster.InfoChar.Hp);
        }

        [Fact]
        public async Task LuyenKhiAdvancementSpendsTuViRewardsPointsAndStopsAtUnopenedRealm()
        {
            await using var world = new WorldTests.World();
            await world.Init();
            Prepare(world);
            world.Hero.RealmId = 2;
            world.Hero.InfoGame.TuVi = 10000;
            Assert.True(await world.Characters.SaveGame(world.Hero));
            int budget = world.Hero.InfoGame.PointBudget;
            for (int target = 3; target <= 11; target++)
            {
                long before = world.Hero.InfoGame.TuVi;
                Assert.NotNull(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
                Assert.Equal(target, world.Hero.RealmId);
                Assert.Equal(before - world.Cache.RealmTemplates[target].TuViRequired, world.Hero.InfoGame.TuVi);
            }
            Assert.Equal(budget + 90, world.Hero.InfoGame.PointBudget);
            Assert.Equal(11, (await world.Characters.Load(world.Hero.Id))!.RealmId);
            Assert.Null(await Change(world, hero => ((CharacterHandler)hero.CharacterHandler!).AdvanceRealm()));
        }
    }
}
