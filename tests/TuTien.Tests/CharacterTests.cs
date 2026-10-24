using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using TuTien.Model.Info;
using TuTien.Model.Template;
using Xunit;
using GamePlayer = TuTien.Model.Player;

namespace TuTien.Tests
{
    public class CharacterTests : IAsyncLifetime
    {
        private readonly string _path;
        private readonly DbContextAsync _db;
        private readonly Cache _cache;
        private readonly UserDB _users;
        private readonly CharacterDB _characters;

        public CharacterTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "TuTienCharacterTests", Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:SqliteFile"] = Path.Combine(_path, "test.sqlite")
            }).Build();
            _db = new DbContextAsync(new ConfigManager(configuration), NullLogger<DbContextAsync>.Instance);
            _cache = ReadCatalog();
            _users = new UserDB(_db, NullLogger<UserDB>.Instance);
            _characters = new CharacterDB(_db, _cache, NullLogger<CharacterDB>.Instance);
        }

        public Task InitializeAsync()
        {
            return _db.Init();
        }

        public Task DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }

            return Task.CompletedTask;
        }

        private static Cache ReadCatalog()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TuTien.Server.csproj")))
            {
                directory = directory.Parent;
            }

            string path = Path.Combine(directory!.FullName, "content", "source", "base", "catalog.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var root = document.RootElement;
            return new Cache
            {
                RaceTemplates = root.GetProperty("races").Deserialize<List<RaceTemplate>>(options)!.ToDictionary(value => value.Id),
                ElementTemplates = root.GetProperty("elements").Deserialize<List<ElementTemplate>>(options)!.ToDictionary(value => value.Id),
                ClassTemplates = root.GetProperty("classes").Deserialize<List<ClassTemplate>>(options)!.ToDictionary(value => value.Id),
                AppearanceTemplates = root.GetProperty("appearances").Deserialize<List<AppearanceTemplate>>(options)!.ToDictionary(value => value.Id),
                CharacterInfo = root.GetProperty("characterStats").Deserialize<InfoChar>(options)!
            };
        }

        private static CharacterCreate CreateValue(int slot, string name)
        {
            return new CharacterCreate { Slot = slot, Name = name, ClassId = 4, Gender = 1, AppearanceId = 202 };
        }

        [Theory]
        [InlineData(1, -10, 6, 6, 0, 0)]
        [InlineData(2, -10, 0, 10, 5, 0)]
        [InlineData(3, 0, 7, 0, -10, 10)]
        [InlineData(4, 10, 3, 0, -8, 0)]
        [InlineData(5, 0, -8, 0, 8, 7)]
        public void RaceTableMatchesRequestedValuesAndUsesOnlyBase(int id, int hp, int strength, int magic, int sense, int critical)
        {
            var info = new InfoChar
            {
                OriginalKhiHuyet = 100,
                OriginalChanLuc = 100,
                OriginalPhapLuc = 100,
                OriginalLinhGiac = 100,
                OriginalSatCo = 100,
                KhiHuyetFromItem = 200,
                ChanLucFromItem = 200,
                PhapLucFromItem = 200,
                LinhGiacFromItem = 200,
                SatCoFromItem = 200
            };

            info.SetUpInfo(_cache.RaceTemplates[id]);
            Assert.Equal(300m + hp, info.KhiHuyetFull);
            Assert.Equal(300m + strength, info.ChanLucFull);
            Assert.Equal(300m + magic, info.PhapLucFull);
            Assert.Equal(300m + sense, info.LinhGiacFull);
            Assert.Equal(300m + critical, info.SatCoFull);
            info.SetUpInfo(_cache.RaceTemplates[id]);
            Assert.Equal(300m + strength, info.ChanLucFull);
        }

        [Fact]
        public async Task AuthenticationUsesDemoPasswordColumn()
        {
            var login = new LoginGame(_users, NullLogger<LoginGame>.Instance);
            var player = await login.CreateUser("Test_Account", "secret-password");
            Assert.NotNull(player);
            Assert.Equal("secret-password", player.Password);
            Assert.DoesNotContain("secret-password", JsonSerializer.Serialize(player));
            Assert.Null(await login.CreateUser("test_account", "different-password"));
            Assert.Null(await login.Login("TEST_ACCOUNT", "incorrect-password"));
            Assert.Null(await login.Login("TEST_ACCOUNT", "secret-password "));
            Assert.Equal(player.Id, (await login.Login("TEST_ACCOUNT", "secret-password"))!.Id);
            Assert.Equal(player.Password, (await _users.Load(player.Id))!.Password);
        }

        [Fact]
        public async Task ThreeSlotsAndGlobalNamesAreEnforcedByDatabase()
        {
            var player = await _users.CreateUser("owner", "test-hash");
            var other = await _users.CreateUser("other", "test-hash");
            var results = await Task.WhenAll(
                _characters.Create(player!.Id, CreateValue(1, "Đạo Hữu Một")),
                _characters.Create(player.Id, CreateValue(1, "Đạo Hữu Hai")));
            Assert.Single(results, value => value != null);
            string existing = results.First(value => value != null)!.Name;
            Assert.Null(await _characters.Create(other!.Id, CreateValue(1, existing.ToLowerInvariant())));
            Assert.NotNull(await _characters.Create(player.Id, CreateValue(2, "Nhân Vật Hai")));
            Assert.NotNull(await _characters.Create(player.Id, CreateValue(3, "Nhân Vật Ba")));
            Assert.Null(await _characters.Create(player.Id, CreateValue(4, "Nhân Vật Bốn")));
            Assert.Equal(3, (await _characters.LoadAll(player.Id)).Count);
        }

        [Fact]
        public void CreationRejectsWrongGenderAppearanceAndUnknownClass()
        {
            var value = CreateValue(1, "Đạo Hữu");
            Assert.Null(_characters.CheckCreate(value));
            value.Gender = 0;
            Assert.NotNull(_characters.CheckCreate(value));
            value.AppearanceId = 101;
            value.ClassId = 99;
            Assert.NotNull(_characters.CheckCreate(value));
        }

        [Fact]
        public async Task TalentIsAssignedOnceAndSurvivesNewDbObjects()
        {
            var player = await _users.CreateUser("owner", "test-hash");
            var other = await _users.CreateUser("other", "test-hash");
            var character = await _characters.Create(player!.Id, CreateValue(1, "Tiên Thiên"));
            Assert.Equal(0, character!.RaceId);
            Assert.False(await _characters.CompleteTalent(character.Id, player.Id));
            Assert.Null(await _characters.Enter(character.Id, other!.Id));
            Assert.False(await _characters.CompleteTalent(character.Id, other.Id));

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => _characters.Enter(character.Id, player.Id)));
            var first = results[0]!;
            Assert.InRange(first.RaceId, 1, 5);
            Assert.InRange(first.ElementId, 1, 5);
            Assert.All(results, value =>
            {
                Assert.Equal(first.RaceId, value!.RaceId);
                Assert.Equal(first.ElementId, value.ElementId);
                Assert.False(value.TalentSeen);
            });

            var reloaded = new CharacterDB(_db, _cache, NullLogger<CharacterDB>.Instance);
            var saved = await reloaded.Enter(character.Id, player.Id);
            Assert.Equal(first.RaceId, saved!.RaceId);
            Assert.Equal(first.ElementId, saved.ElementId);
            Assert.True(await reloaded.CompleteTalent(character.Id, player.Id));
            Assert.True((await reloaded.Enter(character.Id, player.Id))!.TalentSeen);
            Assert.Equal(first.RaceId, (await reloaded.Load(character.Id))!.RaceId);
        }
    }
}
