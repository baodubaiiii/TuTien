using System.Data.Common;
using GamePlayer = TuTien.Model.Player;

namespace TuTien.Database.Player
{
    public class UserDB
    {
        protected DbContextAsync DbContext { get; }
        private readonly ILogger<UserDB> _logger;

        public UserDB(DbContextAsync dbContext, ILogger<UserDB> logger)
        {
            DbContext = dbContext;
            _logger = logger;
        }

        public virtual async Task<GamePlayer?> Load(int id, CancellationToken cancellationToken = default)
        {
            return await LoadUser("id = @value", id, cancellationToken);
        }

        public virtual async Task<GamePlayer?> Login(string username, CancellationToken cancellationToken = default)
        {
            return await LoadUser("username = @value", username, cancellationToken);
        }

        private async Task<GamePlayer?> LoadUser(string condition, object value, CancellationToken cancellationToken)
        {
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return null;
            }

            await using var command = DbContextAsync.Command(connection,
                "SELECT id, username, Password, role FROM users WHERE " + condition, ("@value", value));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new GamePlayer
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Password = reader.GetString(2),
                Role = reader.GetInt32(3)
            };
        }

        public virtual async Task<GamePlayer?> CreateUser(string username, string password, CancellationToken cancellationToken = default)
        {
            await using var connection = await DbContext.Open(cancellationToken);
            if (connection == null)
            {
                return null;
            }

            await using var command = DbContextAsync.Command(connection,
                "INSERT INTO users(username, Password, role) VALUES(@username, @password, 0)",
                ("@username", username), ("@password", password));

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException error) when (DbContextAsync.IsDuplicate(error))
            {
                _logger.LogDebug("Registration rejected: account name already used");
                return null;
            }

            _logger.LogDebug("Account created");
            return await Login(username, cancellationToken);
        }

        public virtual Task<bool> Save(GamePlayer value, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("UserDB.Save has not been implemented");
            return Task.FromResult(false);
        }
    }
}
