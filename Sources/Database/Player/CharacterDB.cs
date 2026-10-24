using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TuTien.Model.Client;
using TuTien.Model.Info;
using GameItem = TuTien.Model.Item.Item;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Database.Player
{
    public partial class CharacterDB
    {
        protected DbContextAsync DbContext { get; }
        private readonly Cache _cache;
        private readonly ILogger<CharacterDB> _logger;
        private readonly ConfigManager _config;

        public CharacterDB(DbContextAsync dbContext, Cache cache, ILogger<CharacterDB> logger, ConfigManager? config = null)
        {
            DbContext = dbContext;
            _cache = cache;
            _logger = logger;
            _config = config ?? new ConfigManager(new ConfigurationBuilder().Build());
        }

        public string? CheckCreate(CharacterCreate value)
        {
            value.Name = Regex.Replace((value.Name ?? string.Empty).Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");
            if (value.Slot < 1 || value.Slot > 3)
            {
                return "Mỗi tài khoản chỉ có 3 ô nhân vật.";
            }

            if (value.Name.Length < 2 || value.Name.Length > 24 || !Regex.IsMatch(value.Name, @"^[\p{L}\p{M}\p{Nd}]+(?: [\p{L}\p{M}\p{Nd}]+)*$"))
            {
                return "Đạo hiệu cần 2–24 ký tự, chỉ gồm chữ, số và khoảng trắng giữa các từ.";
            }

            if (!_cache.IsCreationReady)
            {
                return "Dữ liệu tạo nhân vật chưa sẵn sàng.";
            }

            if (!_cache.ClassTemplates.ContainsKey(value.ClassId))
            {
                return "Hãy chọn pháp khí khởi đầu hợp lệ.";
            }

            if (value.Gender < 0 || value.Gender > 1 || !_cache.AppearanceTemplates.TryGetValue(value.AppearanceId, out var appearance) || appearance.Gender != value.Gender)
            {
                return "Ngoại hình không phù hợp với giới tính đã chọn.";
            }

            return null;
        }

        public virtual async Task<GameCharacter?> Create(int playerId, CharacterCreate value, CancellationToken cancellationToken = default)
        {
            if (CheckCreate(value) != null)
            {
                return null;
            }

            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return null;
            }

            string sql = "INSERT INTO characters(user_id, slot, name, name_key, class_id, gender, appearance_id, InfoChar) " +
                "VALUES(@player, @slot, @name, @key, @class, @gender, @appearance, @info)";
            await using var command = DbContextAsync.Command(connection, sql,
                ("@player", playerId), ("@slot", value.Slot), ("@name", value.Name),
                ("@key", value.Name.ToUpperInvariant()), ("@class", value.ClassId),
                ("@gender", value.Gender), ("@appearance", value.AppearanceId),
                ("@info", JsonSerializer.Serialize(_cache.CharacterInfo)));

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException error) when (DbContextAsync.IsDuplicate(error))
            {
                _logger.LogDebug("Character creation rejected: occupied slot or duplicate name, player {PlayerId}", playerId);
                return null;
            }

            _logger.LogDebug("Character created: player {PlayerId}, slot {Slot}", playerId, value.Slot);
            var characters = await LoadAll(playerId, cancellationToken);
            return characters.Find(character => character.Slot == value.Slot);
        }

        public virtual async Task<GameCharacter?> Load(int id, CancellationToken cancellationToken = default)
        {
            var characters = await Read("id = @id", id, cancellationToken);
            return characters.FirstOrDefault();
        }

        public virtual async Task<GameCharacter?> Load(int id, int playerId, CancellationToken cancellationToken = default)
        {
            var character = await Load(id, cancellationToken);
            return character?.PlayerId == playerId ? character : null;
        }

        public virtual Task<List<GameCharacter>> LoadAll(int playerId, CancellationToken cancellationToken = default)
        {
            return Read("user_id = @id", playerId, cancellationToken);
        }

        private async Task<List<GameCharacter>> Read(string condition, int id, CancellationToken cancellationToken)
        {
            var characters = new List<GameCharacter>();
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return characters;
            }

            string sql = "SELECT id, user_id, slot, name, class_id, gender, appearance_id, race_id, element_id, talent_seen, InfoChar, realm_id, map_id, InfoGame, game_revision, position_x, position_y, ItemBody, ItemBag, ItemBox, Skills, InfoTask " +
                "FROM characters WHERE " + condition + " ORDER BY slot";
            await using var command = DbContextAsync.Command(connection, sql, ("@id", id));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var character = new GameCharacter
                {
                    Id = reader.GetInt32(0),
                    PlayerId = reader.GetInt32(1),
                    Slot = reader.GetInt32(2),
                    Name = reader.GetString(3),
                    ClassId = reader.GetInt32(4),
                    Gender = reader.GetInt32(5),
                    AppearanceId = reader.GetInt32(6),
                    RaceId = reader.GetInt32(7),
                    ElementId = reader.GetInt32(8),
                    TalentSeen = Convert.ToBoolean(reader.GetValue(9)),
                    InfoChar = JsonSerializer.Deserialize<InfoChar>(reader.GetString(10)) ?? throw new InvalidDataException("Invalid character info"),
                    RealmId = reader.GetInt32(11),
                    MapId = reader.GetInt32(12),
                    InfoGame = reader.IsDBNull(13) ? new InfoGame() : JsonSerializer.Deserialize<InfoGame>(reader.GetString(13)) ?? throw new InvalidDataException("Invalid gameplay data"),
                    GameRevision = reader.GetInt64(14),
                    BagCapacity = _config.BagCapacity,
                    MoveSpeed = _config.MoveSpeed,
                    Position = new TuTien.Model.ModelBase.Position { X = Convert.ToSingle(reader.GetValue(15)), Y = Convert.ToSingle(reader.GetValue(16)) },
                    ItemBody = JsonSerializer.Deserialize<List<GameItem>>(reader.GetString(17)) ?? throw new InvalidDataException("Invalid ItemBody"),
                    ItemBag = JsonSerializer.Deserialize<List<GameItem>>(reader.GetString(18)) ?? throw new InvalidDataException("Invalid ItemBag"),
                    ItemBox = JsonSerializer.Deserialize<List<GameItem>>(reader.GetString(19)) ?? throw new InvalidDataException("Invalid ItemBox"),
                    Skills = JsonSerializer.Deserialize<List<TuTien.Model.SkillCharacter.SkillCharacter>>(reader.GetString(20)) ?? throw new InvalidDataException("Invalid Skills"),
                    InfoTask = JsonSerializer.Deserialize<TuTien.Model.Task.TaskInfo>(reader.GetString(21)) ?? throw new InvalidDataException("Invalid InfoTask")
                };

                if (_cache.RaceTemplates.TryGetValue(character.RaceId, out var race))
                {
                    character.InfoChar.SetUpInfo(race);
                }

                character.CharacterHandler = new TuTien.Application.Handlers.Character.CharacterHandler(character, _cache, _config);
                if (character.InfoGame.Version > 0)
                {
                    character.CharacterHandler.SetUpInfo();
                }

                characters.Add(character);
            }

            return characters;
        }

        public virtual async Task<GameCharacter?> Enter(int id, int playerId, CancellationToken cancellationToken = default)
        {
            if (!_cache.IsCreationReady)
            {
                return null;
            }

            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return null;
            }

            int race = RandomNumberGenerator.GetInt32(1, 6);
            int element = RandomNumberGenerator.GetInt32(1, 6);
            await using var command = DbContextAsync.Command(connection,
                "UPDATE characters SET race_id = @race, element_id = @element WHERE id = @id AND user_id = @player AND race_id = 0 AND element_id = 0",
                ("@race", race), ("@element", element), ("@id", id), ("@player", playerId));
            int changed = await command.ExecuteNonQueryAsync(cancellationToken);

            if (changed > 0)
            {
                _logger.LogDebug("Innate talent assigned: character {CharacterId}", id);
            }

            return await Load(id, playerId, cancellationToken);
        }

        public virtual async Task<bool> CompleteTalent(int id, int playerId, CancellationToken cancellationToken = default)
        {
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return false;
            }

            await using var command = DbContextAsync.Command(connection,
                "UPDATE characters SET talent_seen = 1 WHERE id = @id AND user_id = @player AND race_id > 0 AND element_id > 0",
                ("@id", id), ("@player", playerId));
            await command.ExecuteNonQueryAsync(cancellationToken);
            var character = await Load(id, playerId, cancellationToken);
            return character?.TalentSeen == true;
        }

        public virtual Task<bool> Save(GameCharacter value, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("CharacterDB.Save has not been implemented; use character creation operations");
            return Task.FromResult(false);
        }

        public virtual async Task<bool> SaveGame(GameCharacter value, CancellationToken cancellationToken = default)
        {
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return false;
            }

            await using var command = SaveGameCommand(connection, value);
            bool saved = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
            if (saved)
            {
                value.GameRevision++;
            }
            return saved;
        }

        private static DbCommand SaveGameCommand(DbConnection connection, GameCharacter value)
        {
            return DbContextAsync.Command(connection,
                "UPDATE characters SET InfoChar = @info, InfoGame = @game, ItemBody = @body, ItemBag = @bag, ItemBox = @box, Skills = @skills, InfoTask = @task, " +
                "game_revision = game_revision + 1, map_id = @map, realm_id = @realm, position_x = @x, position_y = @y " +
                "WHERE id = @id AND user_id = @player AND game_revision = @revision",
                ("@info", JsonSerializer.Serialize(value.InfoChar)), ("@game", JsonSerializer.Serialize(value.InfoGame)),
                ("@body", JsonSerializer.Serialize(value.ItemBody)), ("@bag", JsonSerializer.Serialize(value.ItemBag)),
                ("@box", JsonSerializer.Serialize(value.ItemBox)), ("@map", value.MapId), ("@realm", value.RealmId),
                ("@skills", JsonSerializer.Serialize(value.Skills)), ("@task", JsonSerializer.Serialize(value.InfoTask)),
                ("@x", value.Position.X), ("@y", value.Position.Y), ("@id", value.Id), ("@player", value.PlayerId), ("@revision", value.GameRevision));
        }

        public virtual async Task<bool> SaveCombat(GameCharacter attacker, GameCharacter target, CancellationToken cancellationToken = default)
        {
            if (attacker.Id == target.Id)
            {
                return false;
            }
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return false;
            }
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            foreach (var character in new[] { attacker, target })
            {
                await using var command = SaveGameCommand(connection, character);
                command.Transaction = transaction;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }
            await transaction.CommitAsync(cancellationToken);
            attacker.GameRevision++;
            target.GameRevision++;
            return true;
        }

        public virtual async Task<bool> SavePosition(GameCharacter value, CancellationToken cancellationToken = default)
        {
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return false;
            }

            await using var command = DbContextAsync.Command(connection,
                "UPDATE characters SET position_x = @x, position_y = @y WHERE id = @id AND user_id = @player",
                ("@x", value.Position.X), ("@y", value.Position.Y), ("@id", value.Id), ("@player", value.PlayerId));
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
    }
}
