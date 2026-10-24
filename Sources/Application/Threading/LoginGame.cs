using System.Text.RegularExpressions;
using TuTien.Application.Interfaces.Client;
using TuTien.Database.Player;
using GamePlayer = TuTien.Model.Player;

namespace TuTien.Application.Threading
{
    public class LoginGame
    {
        private readonly UserDB _users;
        private readonly ILogger<LoginGame> _logger;

        public LoginGame(UserDB users, ILogger<LoginGame> logger)
        {
            _users = users;
            _logger = logger;
        }

        public static bool CheckAccount(string username, string password)
        {
            return Regex.IsMatch(username, "^[a-zA-Z0-9_]{3,24}$") && password.Length >= 8 && password.Length <= 128;
        }

        public virtual async Task<GamePlayer?> Login(string username, string password, CancellationToken cancellationToken = default)
        {
            if (!CheckAccount(username, password))
            {
                return null;
            }

            username = username.ToLowerInvariant();
            var player = await _users.Login(username, cancellationToken);
            if (player == null || !string.Equals(player.Password, password, StringComparison.Ordinal))
            {
                _logger.LogDebug("Login rejected: invalid credentials");
                return null;
            }

            _logger.LogDebug("Login accepted: {PlayerId}", player.Id);
            return player;
        }

        public virtual async Task<GamePlayer?> CreateUser(string username, string password, CancellationToken cancellationToken = default)
        {
            if (!CheckAccount(username, password))
            {
                return null;
            }

            return await _users.CreateUser(username.ToLowerInvariant(), password, cancellationToken);
        }

        public virtual async Task<bool> Login(ISession_ME session, string username, string password, CancellationToken cancellationToken = default)
        {
            var player = await Login(username, password, cancellationToken);
            if (player == null)
            {
                return false;
            }

            session.Player = player;
            session.IsLogin = true;
            player.Session = session;
            return true;
        }
    }
}
