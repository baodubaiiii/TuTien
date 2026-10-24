using System.Collections.Concurrent;
using GameClan = TuTien.Model.Clan.Clan;

namespace TuTien.Application.Manager
{
    public class ClanManager
    {
        private readonly ConcurrentDictionary<int, GameClan> _clans;

        public ClanManager()
        {
            _clans = new ConcurrentDictionary<int, GameClan>();
        }

        public GameClan? Get(int id)
        {
            return _clans.GetValueOrDefault(id);
        }

        public bool Add(GameClan clan)
        {
            return _clans.TryAdd(clan.Id, clan);
        }

        public bool Remove(int id)
        {
            return _clans.TryRemove(id, out _);
        }

        public void Clear()
        {
            _clans.Clear();
        }
    }
}
