using System.Collections.Concurrent;
using TuTien.Application.Interfaces.Client;
using TuTien.Database;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Manager
{
    public class ClientManager : IClientManager
    {
        public ConcurrentDictionary<int, ISession_ME> Sessions { get; }
        public ConcurrentDictionary<int, GameCharacter> Characters { get; }
        private readonly object _gate;
        private readonly ConfigManager _config;
        private bool _isClosing;

        public ClientManager(ConfigManager config)
        {
            _config = config;
            Sessions = new ConcurrentDictionary<int, ISession_ME>();
            Characters = new ConcurrentDictionary<int, GameCharacter>();
            _gate = new object();
        }

        public bool Add(ISession_ME session)
        {
            lock (_gate)
            {
                if (_isClosing || Sessions.Count >= _config.MaxClients)
                {
                    return false;
                }

                return Sessions.TryAdd(session.Id, session);
            }
        }

        public ISession_ME? GetSession(int id)
        {
            return Sessions.GetValueOrDefault(id);
        }

        public GameCharacter? GetCharacter(int id)
        {
            Characters.TryGetValue(id, out var character);
            return character;
        }

        public bool Add(GameCharacter character)
        {
            return Characters.TryAdd(character.Id, character);
        }

        public void RemoveCharacter(GameCharacter character)
        {
            ((ICollection<KeyValuePair<int, GameCharacter>>)Characters).Remove(new KeyValuePair<int, GameCharacter>(character.Id, character));
        }

        public ISession_ME[] GetAllSession()
        {
            return Sessions.Values.ToArray();
        }

        public void RemoveSession(ISession_ME session)
        {
            ((ICollection<KeyValuePair<int, ISession_ME>>)Sessions).Remove(new KeyValuePair<int, ISession_ME>(session.Id, session));
        }

        public async Task Clear()
        {
            ISession_ME[] sessions;

            lock (_gate)
            {
                _isClosing = true;
                sessions = GetAllSession();
            }

            foreach (var session in sessions)
            {
                session.CloseMessage();
            }

            await Task.WhenAll(sessions.Select(session => session.Completion));
            Sessions.Clear();
            Characters.Clear();
        }
    }
}
