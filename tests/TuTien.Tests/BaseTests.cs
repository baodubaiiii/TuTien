using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TuTien.Application.Handlers.Item;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Interfaces.Map;
using TuTien.Application.Main;
using TuTien.Application.Manager;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model;
using TuTien.Model.Option;
using TuTien.Model.Template;
using TuTien.Protocol;
using GameCharacter = TuTien.Model.Character.Character;
using GameItem = TuTien.Model.Item.Item;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Tests
{
    public class BaseTests
    {
        private static ConfigManager Config(params (string Key, string Value)[] values)
        {
            return new ConfigManager(new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
                .Build());
        }

        [Fact]
        public void ModelsDoNotCreateSampleGameplay()
        {
            var character = new GameCharacter();
            var boss = new TuTien.Model.Character.Boss();
            Assert.Empty(character.ItemBag);
            Assert.Empty(character.ItemBody);
            Assert.Empty(character.Skills);
            Assert.Null(character.InfoTask.MainTask);
            Assert.Null(character.Clan);
            Assert.Equal(0, character.InfoChar.Hp);
            Assert.Empty(boss.ItemBag);
            Assert.Empty(new GameZone().Bosses);
            Assert.Empty(new GameZone().Npcs);
            Assert.Empty(new Cache().ItemTemplates);
        }

        [Fact]
        public void ItemBaseOnlyReadsItsOwnOptions()
        {
            var item = new GameItem();
            item.Options.Add(new OptionItem { Id = 6, Param = 25 });
            Assert.Equal(25, item.GetParamOption(6));
            Assert.Equal(0, item.GetParamOption(99));
            Assert.Null(new ItemHandler().GetItemDefault(1));
        }

        [Fact]
        public async Task UnconfiguredGameplayHandlersDoNotMutateCharacters()
        {
            var character = new GameCharacter();
            character.InfoChar.Hp = 100;
            character.CharacterHandler!.SetUpInfo();
            character.CharacterHandler.Update();
            Assert.Null(await new SkillHandler().AttackPlayer(character, 2));
            Assert.Null(await new SkillHandler().AttackMonster(character, 3, "", ""));
            Assert.Equal(100, character.InfoChar.Hp);
            Assert.Empty(character.ItemBag);
        }

        [Fact]
        public async Task MapOnlyUpdatesRegisteredZoneHooks()
        {
            var manager = new MapManager();
            await manager.InitMapServer();
            Assert.Empty(manager.GetAllMap());
            var map = new Map { Id = 1 };
            var handler = new ZoneHandler();
            map.Zones.Add(new GameZone { Id = 1, Map = map, ZoneHandler = handler });
            manager.Add(map);
            map.Update(10);
            Assert.Equal(0, handler.Updates);
            map.Start();
            map.Update(20);
            Assert.Equal(1, handler.Updates);
            Assert.Equal(20, map.TimeMap);
            map.Close();
            map.Update(30);
            Assert.Equal(1, handler.Updates);
            Assert.Equal(1, handler.Closed);
        }

        [Fact]
        public async Task ControllerOnlyImplementsPing()
        {
            var client = new Client(1);
            var controller = new Controller(client, NullLogger<Controller>.Instance);
            await controller.OnMessage(new ClientMessage { Sequence = 1, Ping = new Ping { ClientTime = 42 } }, CancellationToken.None);
            Assert.Equal(42, client.Messages[0].Pong.ClientTime);
            await controller.OnMessage(new ClientMessage { Sequence = 2, EquipItem = new EquipItem { ItemId = "sample" } }, CancellationToken.None);
            Assert.False(client.Messages[1].Result.Success);
            Assert.Equal("base_only", client.Messages[1].Result.Code);
            Assert.False(client.IsLogin);
            Assert.Null(client.Player);
        }

        [Fact]
        public async Task ClientManagerEnforcesCapacityAndClosesSessions()
        {
            var clients = new ClientManager(Config(("Server:MaxClients", "1")));
            var first = new Client(1);
            Assert.True(clients.Add(first));
            Assert.False(clients.Add(new Client(2)));
            clients.RemoveSession(new Client(1));
            Assert.Same(first, clients.GetSession(1));
            await clients.Clear();
            Assert.True(first.Closed);
            Assert.Empty(clients.GetAllSession());
            Assert.False(clients.Add(new Client(3)));
        }

        [Fact]
        public void ServerCanBeDisposedMoreThanOnce()
        {
            var config = Config();
            var maps = new MapManager();
            var clients = new ClientManager(config);
            using var http = new HttpClient();
            var data = new InitData(http, config, new Cache(), NullLogger<InitData>.Instance);
            var server = new Server(maps, clients, config, data, NullLogger<Server>.Instance);

            server.Dispose();
            server.Dispose();
        }

        [Fact]
        public void CharacterRegistryLookupAndRemovalUseTheCharacterInstance()
        {
            var clients = new ClientManager(Config());
            var old = new GameCharacter { Id = 1, PlayerId = 9 };
            Assert.True(clients.Add(old));
            Assert.Same(old, clients.GetCharacter(1));
            Assert.Null(clients.GetCharacter(2));
            clients.RemoveCharacter(old);
            var current = new GameCharacter { Id = 1, PlayerId = 9 };
            Assert.True(clients.Add(current));
            clients.RemoveCharacter(old);
            Assert.Same(current, clients.GetCharacter(1));
            Assert.False(clients.Add(old));
        }

        [Fact]
        public async Task LoginAndDatabaseHooksNeverPretendSuccess()
        {
            var log = new LoginLogger();
            var db = new DbContextAsync(Config(), NullLogger<DbContextAsync>.Instance);
            var users = new UserDB(db, NullLogger<UserDB>.Instance);
            var login = new LoginGame(users, log);
            Assert.False(await login.Login(new Client(1), "account", "secret-password"));
            Assert.DoesNotContain(log.Text, value => value.Contains("secret-password"));
            var characters = new CharacterDB(db, new Cache(), NullLogger<CharacterDB>.Instance);
            Assert.Null(await characters.Load(1));
            Assert.False(await characters.Save(new GameCharacter()));
            Assert.Null(await db.Open());
        }

        [Fact]
        public async Task CdnVerifiesHashAndDoesNotPartiallyReplaceCache()
        {
            byte[] catalog = JsonSerializer.SerializeToUtf8Bytes(new
            {
                items = Array.Empty<object>(),
                itemOptions = Array.Empty<object>(),
                skills = Array.Empty<object>(),
                npcs = Array.Empty<object>(),
                bosses = Array.Empty<object>(),
                tasks = Array.Empty<object>()
            });
            string hash = Convert.ToHexString(SHA256.HashData(catalog)).ToLowerInvariant();
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = "base",
                files = new[] { new { key = "catalog", path = "catalog." + hash[..16] + ".json", sha256 = hash, size = catalog.Length } }
            });
            var handler = new CdnHandler(manifest, catalog);
            var cache = new Cache();
            using var http = new HttpClient(handler);
            var loader = new InitData(http, Config(("Cdn:BaseUrl", "https://cdn.example/base")), cache, NullLogger<InitData>.Instance);
            Assert.True(await loader.Load());
            cache.ItemTemplates.Add(9, new ItemTemplate { Id = 9, Name = "existing" });
            handler.Catalog = new byte[catalog.Length];
            Assert.False(await loader.Load());
            Assert.Equal("existing", cache.ItemTemplates[9].Name);
        }

        private sealed class ZoneHandler : IZoneHandler
        {
            public int Updates
            {
                get; private set;
            }

            public int Closed
            {
                get; private set;
            }

            public void Update(long timeServer)
            {
                Updates++;
            }

            public void Close()
            {
                Closed++;
            }
        }

        private sealed class Client : ISession_ME
        {
            public int Id
            {
                get;
            }

            public Player? Player
            {
                get; set;
            }

            public bool IsLogin
            {
                get; set;
            }

            public bool Closed
            {
                get; private set;
            }

            public Task Completion => Task.CompletedTask;
            public List<ServerMessage> Messages { get; } = new List<ServerMessage>();
            public Client(int id)
            {
                Id = id;
            }

            public bool IsConnected()
            {
                return !Closed;
            }

            public void SendMessage(ServerMessage message)
            {
                Messages.Add(message);
            }

            public void CloseMessage()
            {
                Closed = true;
            }
        }

        private sealed class LoginLogger : ILogger<LoginGame>
        {
            public List<string> Text { get; } = new List<string>();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Text.Add(formatter(state, exception));
            }
        }

        private sealed class CdnHandler : HttpMessageHandler
        {
            private readonly byte[] _manifest;
            public byte[] Catalog
            {
                get; set;
            }

            public CdnHandler(byte[] manifest, byte[] catalog)
            {
                _manifest = manifest;
                Catalog = catalog;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith("manifest.json") ? _manifest : Catalog)
                });
            }
        }
    }
}
