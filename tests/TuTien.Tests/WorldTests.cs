using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Application.Handlers.Monster;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.Manager;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using TuTien.Model.Item;
using TuTien.Model.Monster;
using TuTien.Model.Template;
using Xunit;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Tests
{
    public class WorldTests
    {
        internal sealed class World : IAsyncDisposable
        {
            public string Folder { get; } = Path.Combine(Path.GetTempPath(), "TuTienWorldTests", Guid.NewGuid().ToString("N"));
            public Cache Cache { get; } = GameplayTests.Catalog();
            public ConfigManager Config { get; private set; } = null!;
            public DbContextAsync Database { get; private set; } = null!;
            public CharacterDB Characters { get; private set; } = null!;
            public GameCharacter Hero { get; private set; } = null!;
            public ClientManager Clients { get; private set; } = null!;
            public Map Map { get; } = new Map { Id = 1 };
            public GameZone Zone { get; private set; } = null!;
            public MonsterMap Monster { get; private set; } = null!;

            public async Task Init()
            {
                Config = new ConfigManager(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:SqliteFile"] = Path.Combine(Folder, "game.sqlite"),
                    ["Game:CritPerSatCo"] = "0",
                    ["Game:DodgePerLinhGiac"] = "0",
                    ["Game:MapChangeCooldown"] = "0",
                    ["Game:ReviveDelay"] = "0"
                }).Build());
                Database = new DbContextAsync(Config, NullLogger<DbContextAsync>.Instance);
                await Database.Init();
                Characters = new CharacterDB(Database, Cache, NullLogger<CharacterDB>.Instance, Config);
                var users = new UserDB(Database, NullLogger<UserDB>.Instance);
                var user = await users.CreateUser("world", "world-test-password");
                Hero = (await Characters.Create(user!.Id, new CharacterCreate { Slot = 1, Name = "World Hero", ClassId = 1, Gender = 0, AppearanceId = 101 }))!;
                Hero = (await Characters.Enter(Hero.Id, user.Id))!;
                await Characters.CompleteTalent(Hero.Id, user.Id);
                Hero.TalentSeen = true;
                Hero.BagCapacity = 40;
                Hero.PlayToken = Guid.NewGuid().ToString("N");
                ((CharacterHandler)Hero.CharacterHandler!).InitGame();
                Assert.True(await Characters.SaveGame(Hero));
                Clients = new ClientManager(Config);
                Zone = new GameZone { Id = 1, Map = Map };
                Map.Zones.Add(Zone);
                Map.Start();
                Hero.Zone = Zone;
                Hero.ZoneId = 1;
                Zone.JoinZone(Hero);
                Clients.Add(Hero);
                var template = new MonsterTemplate
                {
                    Id = 1,
                    Name = "Test Monster",
                    Hp = 1,
                    Damage = 2000,
                    Defence = 0,
                    Speed = 70,
                    AttackRange = 90,
                    AggroRange = 240,
                    LeashRange = 420,
                    AttackCooldown = 1500,
                    RespawnSeconds = 15,
                    TuVi = 10,
                    DropItemId = 3010,
                    DropQuantity = 1,
                    DropChance = 100
                };
                Monster = new MonsterMap { Id = 1, Name = template.Name, Template = template, HomeX = Hero.Position.X, HomeY = Hero.Position.Y, Zone = Zone };
                Monster.MonsterHandler = new MonsterMapHandler(Monster, new MapManager(Cache), Characters, Cache, Config);
                Zone.MonsterMaps.Add(1, Monster);
            }

            public async Task<ItemMap> Kill()
            {
                var request = new SkillHandler(Cache, Config, Characters, Clients).AttackMonster(Hero, Monster.Id, Monster.LifeId, Hero.PlayToken);
                await Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Assert.NotNull(await request);
                return Assert.Single(Zone.ItemMaps.Values);
            }

            public ValueTask DisposeAsync()
            {
                Map.Close();
                SqliteConnection.ClearAllPools();
                string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TuTienWorldTests")) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(Folder).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, true);
                }
                return ValueTask.CompletedTask;
            }
        }

        [Fact]
        public async Task KillAndPickupCommitOnlyOnceAndGroundItemsReload()
        {
            await using var world = new World();
            await world.Init();
            var item = await world.Kill();
            Assert.True(world.Monster.IsDie);
            Assert.Equal(10, world.Hero.InfoGame.TuVi);
            Assert.Equal(10, (await world.Characters.Load(world.Hero.Id))!.InfoGame.TuVi);
            Assert.True(await world.Characters.HasMonsterKill(world.Monster.LifeId));
            Assert.Equal(item.Id, Assert.Single(await world.Characters.LoadItemMaps(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())).Id);
            var restored = new MapManager(world.Cache, world.Characters, world.Config);
            await restored.InitMapServer();
            Assert.True(((GameZone)restored.Get(1)!.GetZoneById(1)!).ItemMaps.ContainsKey(item.Id));
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            int before = world.Hero.ItemBag.Where(value => value.Id == 3010).Sum(value => value.Quantity);
            var first = handler.PickItemMap(item.Id, world.Hero.PlayToken, world.Characters, _ => { }, CancellationToken.None);
            var second = handler.PickItemMap(item.Id, world.Hero.PlayToken, world.Characters, _ => { }, CancellationToken.None);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.NotNull(await first);
            Assert.Null(await second);
            Assert.Empty(await world.Characters.LoadItemMaps(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            Assert.Equal(before + 1, (await world.Characters.Load(world.Hero.Id))!.ItemBag.Where(value => value.Id == 3010).Sum(value => value.Quantity));
            var repeat = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients)
                .AttackMonster(world.Hero, world.Monster.Id, world.Monster.LifeId, world.Hero.PlayToken);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await repeat);
            Assert.Equal(10, world.Hero.InfoGame.TuVi);
        }

        [Fact]
        public async Task FullBagAndWrongOwnerKeepTheDropIntact()
        {
            await using var world = new World();
            await world.Init();
            var item = await world.Kill();
            item.PlayerId = world.Hero.Id + 1;
            var handler = (CharacterHandler)world.Hero.CharacterHandler!;
            var wrongOwner = handler.PickItemMap(item.Id, world.Hero.PlayToken, world.Characters, _ => { }, CancellationToken.None);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await wrongOwner);
            item.PlayerId = world.Hero.Id;
            world.Hero.ItemBag.Clear();
            for (int index = 0; index < 40; index++)
            {
                var equipment = new ItemHandler(world.Cache).GetItemDefault(1101)!;
                equipment.IndexUI = index;
                world.Hero.ItemBag.Add(equipment);
            }
            Assert.True(await world.Characters.SaveGame(world.Hero));
            long revision = world.Hero.GameRevision;
            var full = handler.PickItemMap(item.Id, world.Hero.PlayToken, world.Characters, _ => { }, CancellationToken.None);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await full);
            Assert.Equal(revision, world.Hero.GameRevision);
            Assert.True(world.Zone.ItemMaps.ContainsKey(item.Id));
            Assert.Single(await world.Characters.LoadItemMaps(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        [Fact]
        public async Task StaleKillRollsBackReceiptRewardAndDrop()
        {
            await using var world = new World();
            await world.Init();
            Assert.True(await world.Characters.SaveGame(world.Hero.Clone()));
            var request = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients)
                .AttackMonster(world.Hero, world.Monster.Id, world.Monster.LifeId, world.Hero.PlayToken);
            await world.Map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.Null(await request);
            Assert.False(world.Monster.IsDie);
            Assert.Equal(1, world.Monster.InfoChar.Hp);
            Assert.Equal(0, (await world.Characters.Load(world.Hero.Id))!.InfoGame.TuVi);
            Assert.False(await world.Characters.HasMonsterKill(world.Monster.LifeId));
            Assert.Empty(await world.Characters.LoadItemMaps(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        [Fact]
        public async Task AlreadyCommittedKillRecoversTheDropWithoutAnotherReward()
        {
            await using var world = new World();
            await world.Init();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var draft = world.Hero.Clone();
            draft.InfoGame.TuVi = 10;
            var item = new ItemMap { MapId = 1, ZoneId = 1, PlayerId = world.Hero.Id, ItemId = 3010, Quantity = 1, X = world.Hero.Position.X, Y = world.Hero.Position.Y, OwnerTime = now + 30000, LeftTime = now + 180000 };
            Assert.True(await world.Characters.SaveMonsterKill(draft, world.Monster.LifeId, 10, item, now));
            var request = new SkillHandler(world.Cache, world.Config, world.Characters, world.Clients)
                .AttackMonster(world.Hero, world.Monster.Id, world.Monster.LifeId, world.Hero.PlayToken);
            await world.Map.UpdateGame(now);
            Assert.Null(await request);
            Assert.True(world.Monster.IsDie);
            Assert.Equal(item.Id, Assert.Single(world.Zone.ItemMaps.Values).Id);
            Assert.Equal(10, (await world.Characters.Load(world.Hero.Id))!.InfoGame.TuVi);
        }

        [Fact]
        public async Task MonsterPersistsDeathAndRespawnsWithANewLife()
        {
            await using var world = new World();
            await world.Init();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await world.Monster.MonsterHandler.Update(now + 5000, 0.05f);
            Assert.Equal(0, world.Hero.InfoGame.Hp);
            Assert.True(world.Hero.InfoGame.DeathTime > 0);
            Assert.Equal(0, (await world.Characters.Load(world.Hero.Id))!.InfoGame.Hp);
            Assert.Equal("monster", Assert.Single(world.Zone.Attacks).AttackerType);
            string life = world.Monster.LifeId;
            world.Monster.IsDie = true;
            world.Monster.RespawnTime = now + 10000;
            await world.Monster.MonsterHandler.Update(now + 11000, 0.05f);
            Assert.False(world.Monster.IsDie);
            Assert.NotEqual(life, world.Monster.LifeId);
            Assert.Equal(world.Monster.Template.Hp, world.Monster.InfoChar.Hp);
        }

        [Fact]
        public async Task MonsterReturnsHomeAfterLosingALeashedTarget()
        {
            await using var world = new World();
            await world.Init();
            world.Monster.Position.X = world.Monster.HomeX + 300;
            world.Monster.InfoChar.HpFull = 100;
            world.Monster.InfoChar.Hp = 1;
            world.Monster.TargetId = world.Hero.Id;
            world.Hero.Position.X = world.Monster.HomeX + 700;
            long hp = world.Hero.InfoGame.Hp;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await world.Monster.MonsterHandler.Update(now, 0.1f);
            Assert.Equal(0, world.Monster.TargetId);
            Assert.True(world.Monster.IsReturning);
            Assert.Equal(hp, world.Hero.InfoGame.Hp);
            for (int index = 1; index <= 10; index++)
            {
                await world.Monster.MonsterHandler.Update(now + index * 1000, 1);
            }
            Assert.False(world.Monster.IsReturning);
            Assert.Equal(100, world.Monster.InfoChar.Hp);
        }

        [Fact]
        public async Task MapTransferWorksWithOneWorkerAndKeepsTheCharacterReference()
        {
            await using var world = new World();
            await world.Init();
            foreach (var template in world.Cache.MapTemplates.Values)
            {
                template.Mob = Array.Empty<TuTien.Model.Map.MonsterSpawn>();
            }
            var clients = new ClientManager(world.Config);
            world.Cache.MapTemplates[2].MaxPlayer = 1;
            var maps = new MapManager(world.Cache, world.Characters, world.Config, clients: clients);
            await maps.InitMapServer();
            foreach (GameZone zone in maps.Get(2)!.Zones)
            {
                zone.JoinZone(new GameCharacter { Id = 10000 + zone.Id });
            }
            foreach (var value in maps.GetAllMap())
            {
                value.Start();
            }
            maps.StartWorld();
            using var stop = new CancellationTokenSource();
            Task worker = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    foreach (Map value in maps.GetAllMap())
                    {
                        await value.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    }
                    await Task.Delay(5);
                }
            });
            try
            {
                var hero = await maps.JoinMap(world.Hero.PlayerId, world.Hero.Id, 17, CancellationToken.None);
                Assert.NotNull(hero);
                string oldToken = hero!.PlayToken;
                string error = string.Empty;
                Assert.Null(await maps.ChangeMap(hero.PlayerId, hero.Id, 17, hero.PlayToken, 2, 0, false, reason => error = reason, CancellationToken.None));
                var handler = (CharacterHandler)hero.CharacterHandler!;
                await handler.Handle(hero.PlayToken, hero.GameRevision, world.Characters, draft =>
                {
                    draft.Position.X = 1450;
                    draft.Position.Y = 600;
                    return null;
                }, _ => { }, CancellationToken.None);
                Assert.Null(await maps.ChangeMap(hero.PlayerId, hero.Id, 17, hero.PlayToken, 2, 0, false, reason => error = reason, CancellationToken.None));
                Assert.Same(hero, clients.GetCharacter(hero.Id));
                Assert.False(hero.IsChangingMap);
                Assert.Equal(1, hero.MapId);
                var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                ((Map)maps.Get(2)!).AddCommand(new MapCommand(() =>
                {
                    foreach (GameZone zone in maps.Get(2)!.Zones)
                    {
                        zone.OutZone(10000 + zone.Id);
                    }
                    cleared.SetResult();
                    return Task.CompletedTask;
                }, () => cleared.TrySetCanceled()));
                await cleared.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var changed = await maps.ChangeMap(hero.PlayerId, hero.Id, 17, hero.PlayToken, 2, 0, false, reason => error = reason, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(hero, changed);
                Assert.Same(hero, clients.GetCharacter(hero.Id));
                Assert.Equal(2, hero.MapId);
                Assert.NotEqual(oldToken, hero.PlayToken);
                Assert.Equal(2, (await world.Characters.Load(hero.Id))!.MapId);
                Assert.Null(await handler.Handle(oldToken, hero.GameRevision, world.Characters, _ => null, _ => { }, CancellationToken.None));
                Assert.Same(hero, await maps.ChangeMap(hero.PlayerId, hero.Id, 17, hero.PlayToken, 2, 2, false, reason => error = reason, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(2, hero.ZoneId);
                await maps.OutMap(hero.PlayerId, hero.Id, 17).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Null(clients.GetCharacter(hero.Id));
            }
            finally
            {
                stop.Cancel();
                await worker;
                foreach (var map in maps.GetAllMap())
                {
                    map.Close();
                }
            }
        }
    }
}
