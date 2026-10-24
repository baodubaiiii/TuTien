using System.Data.Common;
using TuTien.Model.Item;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Database.Player
{
    public partial class CharacterDB
    {
        public async Task<bool> HasMonsterKill(string lifeId)
        {
            await using var connection = await DbContext.Open();
            if (connection == null)
            {
                return false;
            }
            await using var command = DbContextAsync.Command(connection, "SELECT COUNT(*) FROM monster_kill WHERE life_id=@life", ("@life", lifeId));
            return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
        }

        public virtual async Task<bool> SaveMonsterKill(GameCharacter character, string lifeId, long reward, ItemMap? item, long now)
        {
            await using var connection = await DbContext.Open();
            if (connection == null)
            {
                return false;
            }
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var receipt = DbContextAsync.Command(connection,
                    "INSERT INTO monster_kill(life_id, character_id, map_id, zone_id, tu_vi, killed_at) VALUES(@life,@id,@map,@zone,@reward,@now)",
                    ("@life", lifeId), ("@id", character.Id), ("@map", character.MapId), ("@zone", character.ZoneId), ("@reward", reward), ("@now", now));
                receipt.Transaction = transaction;
                await receipt.ExecuteNonQueryAsync();
                await using var save = SaveGameCommand(connection, character);
                save.Transaction = transaction;
                if (await save.ExecuteNonQueryAsync() != 1)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
                if (item != null)
                {
                    await using var drop = DbContextAsync.Command(connection,
                        "INSERT INTO item_map(id,player_id,map_id,zone_id,item_id,quantity,x,y,owner_time,left_time) VALUES(@id,@player,@map,@zone,@item,@quantity,@x,@y,@owner,@left)",
                        ("@id", item.Id), ("@player", item.PlayerId), ("@map", item.MapId), ("@zone", item.ZoneId),
                        ("@item", item.ItemId), ("@quantity", item.Quantity), ("@x", item.X), ("@y", item.Y), ("@owner", item.OwnerTime), ("@left", item.LeftTime));
                    drop.Transaction = transaction;
                    await drop.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
                character.GameRevision++;
                return true;
            }
            catch (DbException error) when (DbContextAsync.IsDuplicate(error))
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public virtual async Task<bool> SavePickItem(GameCharacter character, ItemMap item, long now)
        {
            await using var connection = await DbContext.Open();
            if (connection == null)
            {
                return false;
            }
            await using var transaction = await connection.BeginTransactionAsync();
            await using var remove = DbContextAsync.Command(connection,
                "DELETE FROM item_map WHERE id=@id AND map_id=@map AND zone_id=@zone AND left_time>@now AND (player_id=@player OR owner_time<=@now)",
                ("@id", item.Id), ("@map", character.MapId), ("@zone", character.ZoneId), ("@now", now), ("@player", character.Id));
            remove.Transaction = transaction;
            if (await remove.ExecuteNonQueryAsync() != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
            await using var save = SaveGameCommand(connection, character);
            save.Transaction = transaction;
            if (await save.ExecuteNonQueryAsync() != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
            await transaction.CommitAsync();
            character.GameRevision++;
            return true;
        }

        public async Task<List<ItemMap>> LoadItemMaps(long now)
        {
            var items = new List<ItemMap>();
            await using var connection = await DbContext.Open();
            if (connection == null)
            {
                return items;
            }
            await using var cleanup = DbContextAsync.Command(connection, "DELETE FROM item_map WHERE left_time<=@now", ("@now", now));
            await cleanup.ExecuteNonQueryAsync();
            await using var command = DbContextAsync.Command(connection, "SELECT * FROM item_map ORDER BY map_id,zone_id,id");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new ItemMap
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    PlayerId = reader.GetInt32(reader.GetOrdinal("player_id")),
                    MapId = reader.GetInt32(reader.GetOrdinal("map_id")),
                    ZoneId = reader.GetInt32(reader.GetOrdinal("zone_id")),
                    ItemId = reader.GetInt16(reader.GetOrdinal("item_id")),
                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                    X = Convert.ToSingle(reader["x"]),
                    Y = Convert.ToSingle(reader["y"]),
                    OwnerTime = reader.GetInt64(reader.GetOrdinal("owner_time")),
                    LeftTime = reader.GetInt64(reader.GetOrdinal("left_time"))
                });
            }
            return items;
        }

        public async Task DeleteExpiredItemMaps(long now)
        {
            await using var connection = await DbContext.Open();
            if (connection != null)
            {
                await using var command = DbContextAsync.Command(connection, "DELETE FROM item_map WHERE left_time<=@now", ("@now", now));
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
