using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using TuTien.Model.Info;
using TuTien.Model.Template;
using Xunit;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Tests
{
    public class GameplayTests
    {
        internal static Cache Catalog()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "TuTien.Server.csproj")))
            {
                root = root.Parent;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "content/source/base/catalog.json")));
            var data = document.RootElement;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return new Cache
            {
                ItemTemplates = data.GetProperty("items").Deserialize<List<ItemTemplate>>(options)!.ToDictionary(value => value.Id),
                ItemOptionTemplates = data.GetProperty("itemOptions").Deserialize<List<ItemOptionTemplate>>(options)!.ToDictionary(value => value.Id),
                NpcTemplates = data.GetProperty("npcs").Deserialize<List<NpcTemplate>>(options)!.ToDictionary(value => value.Id),
                SkillTemplates = data.GetProperty("skills").Deserialize<List<SkillTemplate>>(options)!.ToDictionary(value => value.Id),
                TaskTemplates = data.GetProperty("tasks").Deserialize<List<TaskTemplate>>(options)!.ToDictionary(value => value.Id),
                RaceTemplates = data.GetProperty("races").Deserialize<List<RaceTemplate>>(options)!.ToDictionary(value => value.Id),
                ElementTemplates = data.GetProperty("elements").Deserialize<List<ElementTemplate>>(options)!.ToDictionary(value => value.Id),
                ClassTemplates = data.GetProperty("classes").Deserialize<List<ClassTemplate>>(options)!.ToDictionary(value => value.Id),
                AppearanceTemplates = data.GetProperty("appearances").Deserialize<List<AppearanceTemplate>>(options)!.ToDictionary(value => value.Id),
                RealmTemplates = data.GetProperty("realms").Deserialize<List<RealmTemplate>>(options)!.ToDictionary(value => value.Id),
                MapTemplates = data.GetProperty("maps").Deserialize<List<MapTemplate>>(options)!.ToDictionary(value => value.Id),
                MonsterTemplates = data.TryGetProperty("monsters", out var monsters) ? monsters.Deserialize<List<MonsterTemplate>>(options)!.ToDictionary(value => value.Id) : new(),
                CharacterInfo = data.GetProperty("characterStats").Deserialize<InfoChar>(options)!
            };
        }

        private static ConfigManager Config()
        {
            return new ConfigManager(new ConfigurationBuilder().Build());
        }

        private static GameCharacter Character(Cache cache)
        {
            var character = new GameCharacter { Id = 1, PlayerId = 1, ClassId = 1, Gender = 0, RaceId = 3, TalentSeen = true, InfoChar = cache.CharacterInfo.Clone() };
            character.CharacterHandler = new CharacterHandler(character, cache, Config());
            ((CharacterHandler)character.CharacterHandler).InitGame();
            return character;
        }

        [Fact]
        public void StarterKitIsGrantedOnceAndOptionsAreNotShared()
        {
            var cache = Catalog();
            var character = Character(cache);
            Assert.Equal(8, character.ItemBag.Count);
            Assert.False(((CharacterHandler)character.CharacterHandler!).InitGame());
            Assert.Equal(8, character.ItemBag.Count);
            Assert.Equal(8, character.ItemBag.Select(item => item.Uid).Distinct().Count());
            var first = new ItemHandler(cache).GetItemDefault(1101)!;
            var second = new ItemHandler(cache).GetItemDefault(1101)!;
            first.Options[0].Param = 999;
            Assert.Equal(5, second.Options[0].Param);
            Assert.Equal(5, cache.ItemTemplates[1101].Options[0].Param);
        }

        [Fact]
        public void EquipmentUsesBodySlotAndSwapsBackIntoOriginalBagSlot()
        {
            var cache = Catalog();
            var character = Character(cache);
            var handler = new ItemHandler(cache);
            var weapon = character.ItemBag.Single(item => item.Id == 1101);
            Assert.True(handler.UseItemBag(character, weapon.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out _));
            Assert.Single(character.ItemBody);
            Assert.Equal(112m, character.InfoChar.ChanLucFull);
            Assert.False(handler.UseItemBag(character, weapon.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out _));
            var next = handler.GetItemDefault(1101)!;
            next.IndexUI = 15;
            character.ItemBag.Add(next);
            Assert.True(handler.UseItemBag(character, next.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out _));
            Assert.Equal(next.Uid, character.ItemBody.Single().Uid);
            Assert.Equal(15, character.ItemBag.Single(item => item.Uid == weapon.Uid).IndexUI);
            Assert.True(handler.RemoveItemBody(character, 0, 40, out _));
            Assert.Empty(character.ItemBody);
            Assert.Equal(107m, character.InfoChar.ChanLucFull);
        }

        [Fact]
        public void InvalidEquipmentOrUnsupportedTypeDoesNotConsumeItem()
        {
            var cache = Catalog();
            var character = Character(cache);
            var handler = new ItemHandler(cache);
            var wrong = handler.GetItemDefault(1102)!;
            wrong.IndexUI = 8;
            character.ItemBag.Add(wrong);
            Assert.False(handler.UseItemBag(character, wrong.Uid, 1, out _));
            Assert.Empty(character.ItemBody);
            var stone = handler.GetItemDefault(5001)!;
            stone.IndexUI = 9;
            character.ItemBag.Add(stone);
            Assert.False(handler.UseItemBag(character, stone.Uid, 1, out _));
            Assert.Equal(1, stone.Quantity);
        }

        [Fact]
        public void PillsConsumeOneRefreshDurationAndExpireWithoutStacking()
        {
            var cache = Catalog();
            var character = Character(cache);
            character.InfoGame.Hp = 500;
            character.CharacterHandler!.SetUpInfo();
            var handler = new ItemHandler(cache);
            var pill = character.ItemBag.Single(item => item.Id == 3010);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.True(handler.UseItemBag(character, pill.Uid, now, out _));
            Assert.Equal(4, pill.Quantity);
            Assert.Equal(1200, character.InfoChar.HpFull);
            Assert.Equal(800, character.InfoGame.Hp);
            Assert.Single(character.InfoGame.Effects);
            Assert.Equal(now + 300000, character.InfoGame.Effects[0].ExpireTime);
            Assert.True(handler.UseItemBag(character, pill.Uid, now + 1000, out _));
            Assert.Single(character.InfoGame.Effects);
            Assert.Equal(120m, character.InfoChar.KhiHuyetFull);
            Assert.Equal(now + 301000, character.InfoGame.Effects[0].ExpireTime);
            Assert.True(((CharacterHandler)character.CharacterHandler).Update(now + 301001));
            Assert.Empty(character.InfoGame.Effects);
            Assert.Equal(1000, character.InfoChar.HpFull);
            Assert.InRange(character.InfoGame.Hp, 0, 1000);
        }

        [Fact]
        public void PotentialCanBeAllocatedAndRefundedButNeverExceedBudget()
        {
            var cache = Catalog();
            var character = Character(cache);
            var handler = (CharacterHandler)character.CharacterHandler!;
            Assert.True(handler.ChangePotential(new[] { 0, 20, 0, 0, 0 }, out _));
            Assert.Equal(128.4m, character.InfoChar.ChanLucFull);
            handler.SetUpInfo();
            Assert.Equal(128.4m, character.InfoChar.ChanLucFull);
            Assert.False(handler.ChangePotential(new[] { 100, 100, 0, 0, 0 }, out _));
            Assert.False(handler.ChangePotential(new[] { -1, 0, 0, 0, 0 }, out _));
            Assert.True(handler.ChangePotential(new int[5], out _));
            Assert.Equal(107m, character.InfoChar.ChanLucFull);
        }

        [Fact]
        public void FullBagDoesNotLoseEquippedItemWhenRemovingBody()
        {
            var cache = Catalog();
            var character = Character(cache);
            var handler = new ItemHandler(cache);
            var weapon = character.ItemBag.Single(item => item.Id == 1101);
            Assert.True(handler.UseItemBag(character, weapon.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out _));
            for (int slot = 0; slot < 40; slot++)
            {
                if (character.ItemBag.All(item => item.IndexUI != slot))
                {
                    var item = handler.GetItemDefault(5001)!;
                    item.IndexUI = slot;
                    character.ItemBag.Add(item);
                }
            }
            Assert.False(handler.RemoveItemBody(character, 0, 40, out _));
            Assert.Equal(40, character.ItemBag.Count);
            Assert.Equal(weapon.Uid, character.ItemBody.Single().Uid);
        }

        [Fact]
        public void DifferentPillFamiliesCoexistAndStrongerEffectIsNotDowngraded()
        {
            var cache = Catalog();
            var character = Character(cache);
            var handler = new ItemHandler(cache);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var hp = character.ItemBag.Single(item => item.Id == 3010);
            var mp = character.ItemBag.Single(item => item.Id == 3011);
            Assert.True(handler.UseItemBag(character, hp.Uid, now, out _));
            Assert.True(handler.UseItemBag(character, mp.Uid, now, out _));
            Assert.Equal(2, character.InfoGame.Effects.Count);
            character.InfoGame.Effects.Single(effect => effect.StatId == 1).Param = 40;
            int count = hp.Quantity;
            Assert.False(handler.UseItemBag(character, hp.Uid, now, out _));
            Assert.Equal(count, hp.Quantity);
        }

        [Fact]
        public void MapRejectsWallsBoundsAndNonFinitePositions()
        {
            var maps = new TuTien.Application.Manager.MapManager(Catalog());
            Assert.True(maps.CanStand(1, 800, 600));
            Assert.False(maps.CanStand(1, 500, 300));
            Assert.False(maps.CanStand(1, 0, 600));
            Assert.False(maps.CanStand(1, float.NaN, 600));
        }

        [Fact]
        public void CorruptedInventoryDoesNotReceiveAnotherStarterKit()
        {
            var cache = Catalog();
            var character = Character(cache);
            character.ItemBag[1].Uid = character.ItemBag[0].Uid;
            Assert.Throws<InvalidDataException>(() => ((CharacterHandler)character.CharacterHandler!).InitGame());
            Assert.Equal(8, character.ItemBag.Count);
        }

        [Fact]
        public async Task GameSavePersistsDataAndSerializesItemUseWithoutReplacingTheLiveCharacter()
        {
            string folder = Path.Combine(Path.GetTempPath(), "TuTienGameplayTests", Guid.NewGuid().ToString("N"));
            try
            {
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:SqliteFile"] = Path.Combine(folder, "game.sqlite")
                }).Build();
                var config = new ConfigManager(settings);
                var db = new DbContextAsync(config, NullLogger<DbContextAsync>.Instance);
                await db.Init();
                var cache = Catalog();
                var users = new UserDB(db, NullLogger<UserDB>.Instance);
                var user = await users.CreateUser("game", "unit-test-hash");
                var characters = new CharacterDB(db, cache, NullLogger<CharacterDB>.Instance, config);
                var character = await characters.Create(user!.Id, new CharacterCreate { Slot = 1, Name = "Đạo Hữu", ClassId = 1, Gender = 0, AppearanceId = 101 });
                character = await characters.Enter(character!.Id, user.Id);
                character!.CharacterHandler = new CharacterHandler(character, cache, config);
                ((CharacterHandler)character.CharacterHandler).InitGame();
                var stored = new ItemHandler(cache).GetItemDefault(1101)!;
                stored.IndexUI = 0;
                character.ItemBox.Add(stored);
                Assert.True(await characters.SaveGame(character));
                var stale = character.Clone();
                ((CharacterHandler)character.CharacterHandler).ChangePotential(new[] { 1, 2, 3, 4, 5 }, out _);
                Assert.True(await characters.SaveGame(character));
                Assert.False(await characters.SaveGame(stale));
                var reloaded = await characters.Load(character.Id, user.Id);
                Assert.Equal(new[] { 1, 2, 3, 4, 5 }, reloaded!.InfoGame.Points);
                Assert.Equal(character.GameRevision, reloaded.GameRevision);
                Assert.Equal(character.ItemBag[0].Uid, reloaded.ItemBag[0].Uid);
                Assert.Equal(stored.Uid, Assert.Single(reloaded.ItemBox).Uid);
                var clone = reloaded.Clone();
                clone.ItemBox[0].Options[0].Param = 999;
                Assert.NotEqual(999, reloaded.ItemBox[0].Options[0].Param);
                await using (var connection = await db.Open())
                await using (var read = DbContextAsync.Command(connection!, "SELECT InfoChar, InfoGame, ItemBody, ItemBag, ItemBox FROM characters WHERE id = @id", ("@id", reloaded.Id)))
                await using (var reader = await read.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    using var info = JsonDocument.Parse(reader.GetString(0));
                    using var game = JsonDocument.Parse(reader.GetString(1));
                    using var box = JsonDocument.Parse(reader.GetString(4));
                    Assert.True(info.RootElement.TryGetProperty("OriginalChanLuc", out _));
                    Assert.False(game.RootElement.TryGetProperty("ItemBag", out _));
                    Assert.False(game.RootElement.TryGetProperty("ItemBody", out _));
                    Assert.Equal(stored.Uid, box.RootElement[0].GetProperty("Uid").GetString());
                }
                Assert.False(((CharacterHandler)reloaded.CharacterHandler!).InitGame());
                Assert.Equal(8, reloaded.ItemBag.Count);

                var map = new TuTien.Application.Threading.Map { Id = 1 };
                var zone = new TuTien.Model.Map.Zone { Id = 1, Map = map };
                map.Zones.Add(zone);
                map.Start();
                reloaded.Zone = zone;
                reloaded.TalentSeen = true;
                reloaded.PlayToken = Guid.NewGuid().ToString("N");
                zone.JoinZone(reloaded);
                var handler = (CharacterHandler)reloaded.CharacterHandler!;
                var itemHandler = new ItemHandler(cache);
                string weapon = reloaded.ItemBag.Single(item => item.Id == 1101).Uid;
                long revision = reloaded.GameRevision;
                Func<GameCharacter, string?> equip = value => itemHandler.UseItemBag(value, weapon, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out string error) ? null : error;

                var first = handler.Handle(reloaded.PlayToken, revision, characters, equip, reason => { }, CancellationToken.None);
                var duplicate = handler.Handle(reloaded.PlayToken, revision, characters, equip, reason => { }, CancellationToken.None);
                await map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Assert.NotNull(await first);
                Assert.Null(await duplicate);
                Assert.Same(reloaded, zone.Characters[reloaded.Id]);
                Assert.Same(handler, reloaded.CharacterHandler);
                Assert.Single(reloaded.ItemBody);
                Assert.Equal(revision + 1, reloaded.GameRevision);
                Assert.Single((await characters.Load(reloaded.Id, user.Id))!.ItemBody);
                Assert.Equal(stored.Uid, Assert.Single((await characters.Load(reloaded.Id, user.Id))!.ItemBox).Uid);

                handler.Close();
                var afterClose = handler.Handle(reloaded.PlayToken, reloaded.GameRevision, characters, equip, reason => { }, CancellationToken.None);
                await map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Assert.Null(await afterClose);
                Assert.Equal(revision + 1, reloaded.GameRevision);
                map.Close();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
