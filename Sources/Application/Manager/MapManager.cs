using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Interfaces.Map;
using TuTien.Application.Main;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.ModelBase;
using TuTien.Model.Monster;
using TuTien.Application.Handlers.Monster;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Manager
{
    public partial class MapManager
    {
        private readonly Dictionary<int, IMap> _maps;
        private readonly ConcurrentDictionary<int, (int MapId, int CharacterId, int SessionId)> _players;
        private readonly SemaphoreSlim[] _joinGates;
        private readonly Cache _cache;
        private readonly CharacterDB? _characters;
        private readonly IClientManager _clients;
        private readonly ConfigManager _config;
        private readonly ILogger<MapManager> _logger;
        private bool _stopping;
        private volatile bool _ready;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        public MapManager(Cache? cache = null, CharacterDB? characters = null, ConfigManager? config = null,
            ILogger<MapManager>? logger = null, IClientManager? clients = null)
        {
            _maps = new Dictionary<int, IMap>();
            _players = new ConcurrentDictionary<int, (int, int, int)>();
            _joinGates = Enumerable.Range(0, 64).Select(index => new SemaphoreSlim(1, 1)).ToArray();
            _cache = cache ?? new Cache();
            _characters = characters;
            _config = config ?? new ConfigManager(new ConfigurationBuilder().Build());
            _clients = clients ?? new ClientManager(_config);
            _logger = logger ?? NullLogger<MapManager>.Instance;
        }

        public virtual async Task InitMapServer()
        {
            if (_maps.Count > 0)
            {
                return;
            }

            foreach (var template in _cache.MapTemplates.Values)
            {
                var map = new Map { Id = template.Id };
                for (int id = 1; id <= template.ZoneNumber; id++)
                {
                    var zone = new GameZone { Id = id, Map = map };
                    if (_characters != null)
                    {
                        foreach (var spawn in template.Mob)
                        {
                            if (!CanStand(template.Id, spawn.X, spawn.Y))
                            {
                                throw new InvalidDataException("Monster spawn is blocked");
                            }
                            var monster = new MonsterMap
                            {
                                Id = spawn.Id,
                                Template = _cache.MonsterTemplates[spawn.TemplateId],
                                Name = _cache.MonsterTemplates[spawn.TemplateId].Name,
                                Zone = zone,
                                HomeX = spawn.X,
                                HomeY = spawn.Y
                            };
                            monster.MonsterHandler = new MonsterMapHandler(monster, this, _characters, _cache, _config);
                            zone.MonsterMaps.Add(monster.Id, monster);
                        }
                    }
                    foreach (var entry in template.ArrNpc)
                    {
                        var npc = _cache.NpcTemplates[(short)entry[3]];
                        zone.Npcs.Add(new TuTien.Model.Map.Npc
                        {
                            Id = npc.Id,
                            Status = entry[0],
                            Name = npc.Name,
                            Avatar = npc.Avatar,
                            Chats = npc.Chats.ToList(),
                            X = entry[1],
                            Y = entry[2],
                            Zone = zone
                        });
                    }
                    map.Zones.Add(zone);
                }
                long lastTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                map.UpdateCharacters = async time =>
                {
                    float delta = Math.Clamp((time - lastTick) / 1000f, 0, 0.1f);
                    lastTick = time;
                    foreach (var zone in map.Zones)
                    {
                        foreach (var character in zone.Characters.Values.OfType<GameCharacter>().ToArray())
                        {
                            if (character.IsChangingMap)
                            {
                                continue;
                            }
                            if (((CharacterHandler)character.CharacterHandler!).Update(time))
                            {
                                character.StateVersion++;
                            }
                            if (_characters != null)
                            {
                                await ((CharacterHandler)character.CharacterHandler!).UpdateRealm(time, _characters);
                            }
                            MoveCharacter(character, delta);
                            if (character.PositionDirty && time - character.LastPositionSave >= 10000 && _characters != null)
                            {
                                if (!await _characters.SavePosition(character))
                                {
                                    throw new IOException("Position save failed");
                                }
                                character.PositionDirty = false;
                                character.LastPositionSave = time;
                            }
                        }
                        var world = (GameZone)zone;
                        foreach (var monster in world.MonsterMaps.Values)
                        {
                            await monster.MonsterHandler.Update(time, delta);
                        }
                        var expired = world.ItemMaps.Values.Where(item => item.LeftTime <= time).ToArray();
                        foreach (var item in expired)
                        {
                            world.ItemMaps.Remove(item.Id);
                        }
                        if (expired.Length > 0 && _characters != null)
                        {
                            await _characters.DeleteExpiredItemMaps(time);
                        }
                    }
                };
                _maps.Add(map.Id, map);
            }

            if (_characters != null)
            {
                foreach (var item in await _characters.LoadItemMaps(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                {
                    if (_maps.GetValueOrDefault(item.MapId)?.GetZoneById(item.ZoneId) is GameZone zone &&
                        zone.ItemMaps.Count < 128 && _cache.ItemTemplates.TryGetValue(item.ItemId, out var template) &&
                        item.Quantity > 0 && item.Quantity <= template.MaxStack && CanStand(item.MapId, item.X, item.Y))
                    {
                        zone.ItemMaps.Add(item.Id, item);
                    }
                }
            }
        }

        public bool IsWorldReady
        {
            get { return _ready && !_stopping; }
        }

        public void StartWorld()
        {
            _ready = true;
        }

        public IMap? Get(int id)
        {
            return _maps.GetValueOrDefault(id);
        }

        public bool Add(IMap map)
        {
            return _maps.TryAdd(map.Id, map);
        }

        public bool Remove(int id)
        {
            return _maps.Remove(id);
        }

        public IMap[] GetAllMap()
        {
            return _maps.Values.ToArray();
        }

        private async Task<T?> Run<T>(int mapId, Func<Map, Task<T?>> action, CancellationToken cancellationToken = default, bool waitForSpace = false) where T : class
        {
            if (_maps.GetValueOrDefault(mapId) is not Map map)
            {
                return null;
            }

            var completion = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var commandToken = deadline.Token;
            var command = new MapCommand(async () =>
            {
                if (commandToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(commandToken);
                    return;
                }

                try
                {
                    completion.TrySetResult(await action(map));
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
            }, () => completion.TrySetResult(null));
            bool added = waitForSpace ? await map.AddCommandAndWait(command, deadline.Token) : map.AddCommand(command);
            if (!added)
            {
                _logger.LogDebug("Map command queue unavailable: {MapId}", mapId);
                return null;
            }
            return waitForSpace ? await completion.Task : await completion.Task.WaitAsync(deadline.Token);
        }

        private GameCharacter? GetCharacter(Map map, int playerId, int characterId, int? sessionId = null)
        {
            var character = _clients.GetCharacter(characterId);
            if (character == null || character.PlayerId != playerId || character.Zone?.Map != map ||
                (sessionId.HasValue && character.PlaySessionId != sessionId.Value))
            {
                return null;
            }

            return character;
        }

        public async Task<GameCharacter?> JoinMap(int playerId, int characterId, int sessionId, CancellationToken cancellationToken)
        {
            if (!IsWorldReady || _characters == null)
            {
                return null;
            }

            var gate = _joinGates[(int)((uint)playerId % _joinGates.Length)];
            await gate.WaitAsync(cancellationToken);
            try
            {
                var loaded = await _characters.Load(characterId, playerId, cancellationToken);
                if (loaded == null || loaded.RaceId == 0 || !_cache.MapTemplates.ContainsKey(loaded.MapId))
                {
                    return null;
                }
                if (_players.TryGetValue(playerId, out var previous))
                {
                    await OutMapCore(playerId, previous.CharacterId, previous.SessionId);
                    if (_players.ContainsKey(playerId))
                    {
                        return null;
                    }
                }
                loaded = await _characters.Load(characterId, playerId, cancellationToken);
                if (loaded == null)
                {
                    return null;
                }
                return await Run<GameCharacter>(loaded.MapId, async map =>
                {
                    var zone = map.Zones.OfType<GameZone>().FirstOrDefault(value => value.Characters.Count + value.Reservations.Count < _cache.MapTemplates[map.Id].MaxPlayer);
                    if (zone == null)
                    {
                        return null;
                    }
                    loaded.CharacterHandler = new CharacterHandler(loaded, _cache, _config);
                    bool initialized = ((CharacterHandler)loaded.CharacterHandler).InitGame();
                    bool expired = ((CharacterHandler)loaded.CharacterHandler).Update(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    loaded.BagCapacity = _config.BagCapacity;
                    loaded.MoveSpeed = _config.MoveSpeed;
                    loaded.PlaySessionId = sessionId;
                    loaded.PlayToken = Guid.NewGuid().ToString("N");
                    loaded.ZoneId = zone.Id;
                    loaded.Zone = zone;
                    if (!CanStand(loaded.MapId, loaded.Position.X, loaded.Position.Y))
                    {
                        var template = _cache.MapTemplates[loaded.MapId];
                        loaded.Position = new Position { X = template.SpawnX, Y = template.SpawnY };
                    }
                    loaded.TargetX = loaded.Position.X;
                    loaded.TargetY = loaded.Position.Y;
                    loaded.LastPositionSave = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    loaded.StateVersion = 1;
                    if ((initialized || expired) && !await _characters.SaveGame(loaded))
                    {
                        return null;
                    }
                    if (!zone.JoinZone(loaded))
                    {
                        return null;
                    }
                    if (!_clients.Add(loaded))
                    {
                        zone.OutZone(loaded.Id);
                        return null;
                    }
                    _players[playerId] = (loaded.MapId, loaded.Id, sessionId);
                    _logger.LogDebug("Character joined: {CharacterId}, map {MapId}, zone {ZoneId}", loaded.Id, loaded.MapId, zone.Id);
                    return loaded;
                }, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task OutMap(int playerId, int characterId, int sessionId)
        {
            var gate = _joinGates[(int)((uint)playerId % _joinGates.Length)];
            await gate.WaitAsync();
            try
            {
                await OutMapCore(playerId, characterId, sessionId);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task OutMapCore(int playerId, int characterId, int sessionId)
        {
            if (!_players.TryGetValue(playerId, out var active) || active.CharacterId != characterId || active.SessionId != sessionId)
            {
                return;
            }
            await Run<GameCharacter>(active.MapId, async map =>
            {
                var character = GetCharacter(map, playerId, characterId, sessionId);
                if (character == null)
                {
                    return null;
                }
                if (!((CharacterHandler)character.CharacterHandler!).IsClosed && _characters != null && !await _characters.SavePosition(character))
                {
                    throw new IOException("Final position save failed");
                }
                character.CharacterHandler!.Close();
                ((GameZone)character.Zone!).OutZone(character.Id);
                _clients.RemoveCharacter(character);
                ((ICollection<KeyValuePair<int, (int MapId, int CharacterId, int SessionId)>>)_players)
                    .Remove(new KeyValuePair<int, (int, int, int)>(playerId, active));
                _logger.LogDebug("Character left: {CharacterId}", character.Id);
                return character;
            }, waitForSpace: true);
        }

        public async Task<bool> SetPosition(int playerId, int characterId, int sessionId, float x, float y, CancellationToken cancellationToken)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y) || !_players.TryGetValue(playerId, out var active) || active.CharacterId != characterId || active.SessionId != sessionId)
            {
                return false;
            }
            var accepted = await Run<GameCharacter>(active.MapId, map =>
            {
                var character = GetCharacter(map, playerId, characterId, sessionId);
                if (character == null || character.IsChangingMap || character.InfoGame.Breakthrough.Active || !character.TalentSeen || character.InfoGame.Hp <= 0 || ((CharacterHandler)character.CharacterHandler!).IsClosed)
                {
                    return Task.FromResult<GameCharacter?>(null);
                }
                var template = _cache.MapTemplates[map.Id];
                character.TargetX = Math.Clamp(x, 16, template.Width - 16);
                character.TargetY = Math.Clamp(y, 16, template.Height - 16);
                return Task.FromResult<GameCharacter?>(character);
            }, cancellationToken);
            return accepted != null;
        }

        public bool CanStand(int mapId, float x, float y)
        {
            var map = _cache.MapTemplates[mapId];
            if (!float.IsFinite(x) || !float.IsFinite(y) || x < 16 || y < 16 || x > map.Width - 16 || y > map.Height - 16)
            {
                return false;
            }
            foreach (var rectangle in map.MapCollisions)
            {
                float left = rectangle.GetProperty("x").GetSingle();
                float top = rectangle.GetProperty("y").GetSingle();
                float width = rectangle.GetProperty("width").GetSingle();
                float height = rectangle.GetProperty("height").GetSingle();
                if (x + 14 > left && x - 14 < left + width && y + 14 > top && y - 14 < top + height)
                {
                    return false;
                }
            }
            return true;
        }

        private void MoveCharacter(GameCharacter character, float delta)
        {
            if (!character.TalentSeen || character.InfoGame.Hp <= 0 || character.InfoGame.Breakthrough.Active)
            {
                return;
            }
            float dx = character.TargetX - character.Position.X;
            float dy = character.TargetY - character.Position.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance < 0.1f)
            {
                return;
            }
            float step = Math.Min(distance, _config.MoveSpeed * delta);
            float x = character.Position.X + dx / distance * step;
            float y = character.Position.Y + dy / distance * step;
            bool moved = false;
            if (Math.Abs(x - character.Position.X) > 0.001f && CanStand(character.MapId, x, character.Position.Y))
            {
                character.Position.X = x;
                moved = true;
            }
            if (Math.Abs(y - character.Position.Y) > 0.001f && CanStand(character.MapId, character.Position.X, y))
            {
                character.Position.Y = y;
                moved = true;
            }
            if (!moved)
            {
                character.TargetX = character.Position.X;
                character.TargetY = character.Position.Y;
            }
            character.PositionDirty = true;
        }

        public async Task ConfirmTalent(int playerId, int characterId)
        {
            if (_players.TryGetValue(playerId, out var active) && active.CharacterId == characterId)
            {
                await Run<GameCharacter>(active.MapId, map =>
                {
                    var character = GetCharacter(map, playerId, characterId);
                    if (character != null)
                    {
                        character.TalentSeen = true;
                        character.StateVersion++;
                    }
                    return Task.FromResult(character);
                });
            }
        }

        public Task<byte[]?> GetFrame(int playerId, int characterId, int sessionId, long knownVersion, CancellationToken cancellationToken)
        {
            if (!_players.TryGetValue(playerId, out var active) || active.CharacterId != characterId || active.SessionId != sessionId)
            {
                return Task.FromResult<byte[]?>(null);
            }
            return Run<byte[]>(active.MapId, map =>
            {
                var character = GetCharacter(map, playerId, characterId, sessionId);
                if (character == null || ((CharacterHandler)character.CharacterHandler!).IsClosed)
                {
                    return Task.FromResult<byte[]?>(null);
                }
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    type = "state",
                    version = 1,
                    serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    stateVersion = character.StateVersion,
                    mapId = character.MapId,
                    zoneId = character.ZoneId,
                    monsters = ((GameZone)character.Zone!).MonsterMaps.Values.Select(value => new
                    {
                        value.Id,
                        templateId = value.Template.Id,
                        value.LifeId,
                        value.Name,
                        x = value.Position.X,
                        y = value.Position.Y,
                        hp = value.InfoChar.Hp,
                        hpMax = value.InfoChar.HpFull,
                        value.IsDie,
                        value.RespawnTime,
                        state = value.IsDie ? "dead" : value.IsReturning ? "return" : value.TargetId > 0 ? "chase" : "patrol"
                    }).ToArray(),
                    items = ((GameZone)character.Zone!).ItemMaps.Values.Where(value => value.LeftTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToArray(),
                    npcs = ((GameZone)character.Zone!).Npcs.Select(value => new { value.Id, value.Status, value.Avatar, value.Name, value.X, value.Y }).ToArray(),
                    attacks = ((GameZone)character.Zone).Attacks.Where(value => value.Time > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 3000).ToArray(),
                    character = knownVersion == character.StateVersion ? null : Service.CharacterInfo(character, _cache),
                    players = character.Zone!.Characters.Values.OfType<GameCharacter>().Where(value => !value.IsChangingMap).Select(value => new
                    {
                        value.Id,
                        value.Name,
                        x = value.Position.X,
                        y = value.Position.Y,
                        value.Gender,
                        hp = value.InfoChar.Hp,
                        mp = value.InfoChar.Mp,
                        hpMax = value.InfoChar.HpFull,
                        mpMax = value.InfoChar.MpFull
                    }).ToArray()
                }, Json);
                return Task.FromResult<byte[]?>(bytes);
            }, cancellationToken);
        }

        public void BeginShutdown()
        {
            _stopping = true;
        }
    }
}
