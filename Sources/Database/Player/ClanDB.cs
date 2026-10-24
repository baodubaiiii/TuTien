using GameClan = TuTien.Model.Clan.Clan;

namespace TuTien.Database.Player
{
    public class ClanDB
    {
        protected DbContextAsync DbContext { get; }
        private readonly ILogger<ClanDB> _logger;

        public ClanDB(DbContextAsync dbContext, ILogger<ClanDB> logger)
        {
            DbContext = dbContext;
            _logger = logger;
        }

        public virtual Task<GameClan?> Load(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("ClanDB.Load is a base hook");
            return Task.FromResult<GameClan?>(null);
        }

        public virtual Task<bool> Save(GameClan value, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("ClanDB.Save has not been implemented");
            return Task.FromResult(false);
        }
    }
}
