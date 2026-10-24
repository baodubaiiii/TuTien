using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TuTien.Database;
using TuTien.Model.Template;

namespace TuTien.Tools.DatabaseBuilder
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static async Task<int> Main(string[] args)
        {
            try
            {
                string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
                if (!File.Exists(Path.Combine(root, "TuTien.Server.csproj")))
                {
                    throw new InvalidDataException("Unexpected project directory");
                }

                if (args.Contains("--create"))
                {
                    await Create(root);
                }
                else if (args.Contains("--reset-demo"))
                {
                    await ResetDemo(root);
                }
                else if (args.Contains("--migrate-world") || args.Contains("--migrate-progression") || args.Contains("--migrate-task-reward"))
                {
                    string admin = Environment.GetEnvironmentVariable("TUTIEN_DB_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Admin connection is required");
                    var options = new MySqlConnectionStringBuilder(admin);
                    if (options.Server != "127.0.0.1" || options.Port != 3307)
                    {
                        throw new InvalidOperationException("World migration is restricted to the project instance");
                    }
                    options.AllowUserVariables = true;
                    await using var connection = new MySqlConnection(options.ConnectionString);
                    await connection.OpenAsync();
                    await connection.ChangeDatabaseAsync("tutien");
                    await using var marker = new MySqlCommand("SELECT name FROM tutien_schema WHERE id = 1", connection);
                    if ((string?)await marker.ExecuteScalarAsync() != "TuTien local database")
                    {
                        throw new InvalidOperationException("Unexpected database owner marker");
                    }
                    if (args.Contains("--migrate-progression"))
                    {
                        await ApplyProgression(connection, root);
                    }
                    else if (args.Contains("--migrate-task-reward"))
                    {
                        await ApplyTaskReward(connection, root);
                    }
                    else
                    {
                        await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "006_world.mysql.sql")));
                    }
                }

                await Export(root);
                return 0;
            }
            catch (MySqlException error)
            {
                Console.Error.WriteLine($"Database operation failed. MySQL error {error.Number}, SQL state {error.SqlState}.");
                return 1;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Database operation failed: {error.GetType().Name}.");
                return 1;
            }
        }

        private static IConfiguration Configuration(string root)
        {
            return new ConfigurationBuilder().SetBasePath(root)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Local.json", true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static async Task ResetDemo(string root)
        {
            string admin = Environment.GetEnvironmentVariable("TUTIEN_DB_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Admin connection is required");
            var options = new MySqlConnectionStringBuilder(admin);
            if (options.Server != "127.0.0.1" || options.Port != 3307)
            {
                throw new InvalidOperationException("Demo reset is restricted to the project instance");
            }

            await using var connection = new MySqlConnection(admin);
            await connection.OpenAsync();
            await connection.ChangeDatabaseAsync("tutien");
            await using var marker = new MySqlCommand("SELECT name FROM tutien_schema WHERE id = 1", connection);
            if ((string?)await marker.ExecuteScalarAsync() != "TuTien local database")
            {
                throw new InvalidOperationException("Unexpected database owner marker");
            }

            await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "005_reference_layout.mysql.sql")));
            await Create(root);
            await Execute(connection, "INSERT IGNORE INTO tutien_schema(id, name) VALUES(3, 'Reference layout: InfoChar, inventory columns and map arrNpc')");
            Console.WriteLine("Demo accounts and characters reset. Reference schema applied on port 3307.");
        }

        private static async Task Create(string root)
        {
            string admin = Environment.GetEnvironmentVariable("TUTIEN_DB_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Use tools/Setup-Database.ps1 for the isolated database");
            var adminOptions = new MySqlConnectionStringBuilder(admin);
            adminOptions.AllowUserVariables = true;
            await using var connection = new MySqlConnection(adminOptions.ConnectionString);
            await connection.OpenAsync();
            await Execute(connection, "CREATE DATABASE IF NOT EXISTS tutien CHARACTER SET utf8mb4 COLLATE utf8mb4_bin");
            await connection.ChangeDatabaseAsync("tutien");

            await using var guard = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'tutien'", connection);
            long tables = Convert.ToInt64(await guard.ExecuteScalarAsync());
            await using var marker = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'tutien' AND table_name = 'tutien_schema'", connection);
            bool owned = Convert.ToInt64(await marker.ExecuteScalarAsync()) > 0;
            if (tables > 0 && !owned)
            {
                throw new InvalidOperationException("Existing database is not managed by this project");
            }

            await Execute(connection, "CREATE TABLE IF NOT EXISTS tutien_schema (id INT PRIMARY KEY, name VARCHAR(100) NOT NULL)");
            await Execute(connection, "INSERT IGNORE INTO tutien_schema(id, name) VALUES(1, 'TuTien local database')");
            string core = await File.ReadAllTextAsync(Path.Combine(root, "database", "character_creation.mysql.sql"));
            await Execute(connection, core.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal));
            await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "002_templates.mysql.sql")));
            await using var progression = new MySqlCommand("SELECT COUNT(*) FROM tutien_schema WHERE id = 5", connection);
            if (Convert.ToInt64(await progression.ExecuteScalarAsync()) == 0)
            {
                await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "seeds", "001_tutien_templates.mysql.sql")));
            }
            await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "006_world.mysql.sql")));
            await ApplyProgression(connection, root);
            foreach (string column in new[] { "realm_id", "map_id" })
            {
                await using var check = new MySqlCommand("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'tutien' AND table_name = 'characters' AND column_name = @column", connection);
                check.Parameters.AddWithValue("@column", column);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) == 0)
                {
                    await Execute(connection, "ALTER TABLE characters ADD COLUMN " + column + " INT NOT NULL DEFAULT 1");
                }
            }

            foreach (string kind in new[] { "realm", "map" })
            {
                string constraint = "fk_character_" + kind;
                await using var check = new MySqlCommand("SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_schema = 'tutien' AND table_name = 'characters' AND constraint_name = @name", connection);
                check.Parameters.AddWithValue("@name", constraint);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) == 0)
                {
                    await Execute(connection, "ALTER TABLE characters ADD CONSTRAINT " + constraint + " FOREIGN KEY(" + kind + "_id) REFERENCES " + kind + "_template(id)");
                }
            }

            string localPath = Path.Combine(root, "appsettings.Local.json");
            var existing = Configuration(root).GetConnectionString("Game");
            string gameConnection;
            if (!string.IsNullOrWhiteSpace(existing) && new MySqlConnectionStringBuilder(existing).Database == "tutien" &&
                new MySqlConnectionStringBuilder(existing).Port == adminOptions.Port)
            {
                gameConnection = existing;
            }
            else
            {
                string username = "tutien_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
                string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                await using var createUser = new MySqlCommand($"CREATE USER '{username}'@'127.0.0.1' IDENTIFIED BY @password", connection);
                createUser.Parameters.AddWithValue("@password", password);
                await createUser.ExecuteNonQueryAsync();
                await Execute(connection, $"GRANT SELECT, INSERT, UPDATE, DELETE ON tutien.* TO '{username}'@'127.0.0.1'");
                gameConnection = new MySqlConnectionStringBuilder
                {
                    Server = "127.0.0.1",
                    Port = adminOptions.Port,
                    Database = "tutien",
                    UserID = username,
                    Password = password,
                    CharacterSet = "utf8mb4"
                }.ConnectionString;
            }

            var local = File.Exists(localPath) ? JsonNode.Parse(await File.ReadAllTextAsync(localPath))!.AsObject() : new JsonObject();
            local["Database"] = new JsonObject { ["Provider"] = "MySql" };
            local["ConnectionStrings"] = new JsonObject { ["Game"] = gameConnection };
            await File.WriteAllTextAsync(localPath, local.ToJsonString(JsonOptions));
            Console.WriteLine("Database tutien ready on loopback. Runtime account has data permissions only. Credentials stored in ignored appsettings.Local.json.");
        }

        private static async Task ApplyProgression(MySqlConnection connection, string root)
        {
            await using var marker = new MySqlCommand("SELECT COUNT(*) FROM tutien_schema WHERE id = 5", connection);
            if (Convert.ToInt64(await marker.ExecuteScalarAsync()) == 0)
            {
                await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "007_progression.mysql.sql")));
            }
            await ApplyTaskReward(connection, root);
        }

        private static async Task ApplyTaskReward(MySqlConnection connection, string root)
        {
            await using var marker = new MySqlCommand("SELECT COUNT(*) FROM tutien_schema WHERE id = 6", connection);
            if (Convert.ToInt64(await marker.ExecuteScalarAsync()) == 0)
            {
                await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "008_task_reward.mysql.sql")));
            }
            await Execute(connection, await File.ReadAllTextAsync(Path.Combine(root, "database", "009_task_reward_items.mysql.sql")));
        }

        private static async Task Execute(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<List<T>> Read<T>(MySqlConnection connection, string table) where T : new()
        {
            var properties = typeof(T).GetProperties().Where(property => property.SetMethod != null).ToArray();
            await using var command = new MySqlCommand("SELECT * FROM `" + table + "` ORDER BY id", connection);
            await using var reader = await command.ExecuteReaderAsync();
            var values = new List<T>();
            while (await reader.ReadAsync())
            {
                var value = new T();
                foreach (var property in properties)
                {
                    string column = property.Name == "ArrNpc" ? "arrNpc" : Regex.Replace(property.Name, "(?<!^)[A-Z]", match => "_" + match.Value).ToLowerInvariant();
                    int ordinal = reader.GetOrdinal(column);
                    if (reader.IsDBNull(ordinal))
                    {
                        continue;
                    }

                    object? data = property.PropertyType == typeof(string) || property.PropertyType.IsPrimitive || property.PropertyType == typeof(decimal)
                        ? Convert.ChangeType(reader.GetValue(ordinal), property.PropertyType)
                        : JsonSerializer.Deserialize(reader.GetString(ordinal), property.PropertyType, JsonOptions);
                    property.SetValue(value, data);
                }

                values.Add(value);
            }

            return values;
        }

        private static async Task Export(string root)
        {
            string connectionString = Configuration(root).GetConnectionString("Game") ?? throw new InvalidOperationException("No game connection configured");
            var options = new MySqlConnectionStringBuilder(connectionString);
            if (options.Database != "tutien")
            {
                throw new InvalidOperationException("Unexpected database name");
            }

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using (var grants = new MySqlCommand("SHOW GRANTS", connection))
            await using (var reader = await grants.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string grant = reader.GetString(0);
                    if (new[] { "ALL PRIVILEGES", "CREATE", "ALTER", "DROP", "GRANT OPTION" }.Any(privilege => grant.Contains(privilege, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("Runtime database account must not have schema or administrative permissions");
                    }
                }
            }

            var items = await Read<ItemTemplate>(connection, "item_template");
            var itemOptions = await Read<ItemOptionTemplate>(connection, "item_option_template");
            var maps = await Read<MapTemplate>(connection, "map_template");
            var npcs = await Read<NpcTemplate>(connection, "npc_template");
            var skills = await Read<SkillTemplate>(connection, "skill_template");
            var realms = await Read<RealmTemplate>(connection, "realm_template");
            var monsters = await Read<MonsterTemplate>(connection, "monster_template");
            var tasks = await Read<TaskTemplate>(connection, "task_template");

            var cache = new Cache
            {
                ItemTemplates = items.ToDictionary(value => value.Id),
                ItemOptionTemplates = itemOptions.ToDictionary(value => value.Id),
                MapTemplates = maps.ToDictionary(value => value.Id),
                MonsterTemplates = monsters.ToDictionary(value => value.Id),
                TaskTemplates = tasks.ToDictionary(value => value.Id),
                NpcTemplates = npcs.ToDictionary(value => value.Id),
                SkillTemplates = skills.ToDictionary(value => value.Id),
                RealmTemplates = realms.ToDictionary(value => value.Id)
            };
            cache.CheckTemplates();
            string path = Path.Combine(root, "content", "source", "base", "catalog.json");
            var catalog = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            catalog["items"] = JsonSerializer.SerializeToNode(items, JsonOptions);
            catalog["itemOptions"] = JsonSerializer.SerializeToNode(itemOptions, JsonOptions);
            catalog["maps"] = JsonSerializer.SerializeToNode(maps, JsonOptions);
            catalog["monsters"] = JsonSerializer.SerializeToNode(monsters, JsonOptions);
            catalog["tasks"] = JsonSerializer.SerializeToNode(tasks, JsonOptions);
            catalog["npcs"] = JsonSerializer.SerializeToNode(npcs, JsonOptions);
            catalog["skills"] = JsonSerializer.SerializeToNode(skills, JsonOptions);
            catalog["realms"] = JsonSerializer.SerializeToNode(realms, JsonOptions);
            catalog.Remove("mapNpcs");
            await File.WriteAllTextAsync(path + ".tmp", catalog.ToJsonString(JsonOptions));
            File.Move(path + ".tmp", path, true);
            var receipt = new
            {
                database = "tutien",
                port = options.Port,
                runtimeDataOnly = true,
                createdUtc = DateTime.UtcNow,
                counts = new { items = items.Count, itemOptions = itemOptions.Count, maps = maps.Count, npcs = npcs.Count, monsters = monsters.Count, skills = skills.Count, tasks = tasks.Count, realms = realms.Count, mapNpcCount = maps.Sum(map => map.ArrNpc.Length) }
            };
            Directory.CreateDirectory(Path.Combine(root, ".runtime"));
            await File.WriteAllTextAsync(Path.Combine(root, ".runtime", "database-setup.json"), JsonSerializer.Serialize(receipt, JsonOptions));
            Console.WriteLine(JsonSerializer.Serialize(receipt, JsonOptions));
        }
    }
}
