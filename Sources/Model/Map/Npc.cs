using TuTien.Application.Interfaces.Npc;
using TuTien.Application.Interfaces.Zone;

namespace TuTien.Model.Map
{
    public class Npc
    {
        public short Id { get; set; }
        public int Status { get; set; }
        public int Avatar { get; set; }
        public string Name { get; set; }
        public List<string> Chats { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public IZone? Zone { get; set; }
        public INpcHandler? Handler { get; set; }

        public Npc()
        {
            Name = string.Empty;
            Chats = new List<string>();
        }
    }
}
