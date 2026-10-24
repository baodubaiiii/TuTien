using TuTien.Application.Interfaces.Character;
using TuTien.Application.Interfaces.Client;

namespace TuTien.Model
{
    public class Player
    {
        public int Id { get; set; }
        public string Username { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string Password { get; set; }
        public int Role { get; set; }
        public bool IsOnline { get; set; }
        public ICharacter? Character { get; set; }
        public ISession_ME? Session { get; set; }

        public Player()
        {
            Username = string.Empty;
            Password = string.Empty;
        }
    }
}
