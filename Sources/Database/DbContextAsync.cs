using System.Data.Common;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace TuTien.Database
{
    public class DbContextAsync
    {
        private readonly ConfigManager _config;
        private readonly ILogger<DbContextAsync> _logger;

        public DbContextAsync(ConfigManager config, ILogger<DbContextAsync> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task Init(CancellationToken cancellationToken = default)
        {
            if (_config.DatabaseProvider == "MySql")
            {
                await using var database = await Open(cancellationToken) ?? throw new InvalidOperationException("MySQL is not configured");
                await using var check = Command(database, "SELECT COUNT(*) FROM characters");
                await check.ExecuteScalarAsync(cancellationToken);
                _logger.LogInformation("Game database ready: MySql");
                return;
            }

            if (_config.DatabaseProvider != "Sqlite")
            {
                return;
            }

            await using var connection = await Open(cancellationToken);
            if (connection == null)
            {
                return;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "database", "character_creation.sqlite.sql");
            string sql = await File.ReadAllTextAsync(path, cancellationToken);
            await using var command = Command(connection, sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Character creation database ready: {Provider}", _config.DatabaseProvider);
        }

        public async Task<DbConnection?> Open(CancellationToken cancellationToken = default)
        {
            DbConnection connection;

            if (_config.DatabaseProvider == "Sqlite")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_config.SqliteFile)!);
                var options = new SqliteConnectionStringBuilder
                {
                    DataSource = _config.SqliteFile,
                    ForeignKeys = true,
                    DefaultTimeout = 15
                };
                connection = new SqliteConnection(options.ToString());
            }
            else if (_config.DatabaseProvider == "MySql" && !string.IsNullOrWhiteSpace(_config.ConnectionString))
            {
                connection = new MySqlConnection(_config.ConnectionString);
            }
            else
            {
                _logger.LogWarning("Database is not configured");
                return null;
            }

            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public static DbCommand Command(DbConnection connection, string sql, params (string Name, object Value)[] values)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var value in values)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = value.Name;
                parameter.Value = value.Value;
                command.Parameters.Add(parameter);
            }

            return command;
        }

        public static bool IsDuplicate(DbException error)
        {
            return error is SqliteException { SqliteErrorCode: 19 } || error is MySqlException { Number: 1062 };
        }
    }
}
