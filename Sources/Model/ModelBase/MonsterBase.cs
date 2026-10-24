using TuTien.Model.Info;

namespace TuTien.Model.ModelBase
{
    public class MonsterBase
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Position Position { get; set; } = new Position();
        public InfoChar InfoChar { get; set; } = new InfoChar();
        public bool IsDie { get; set; }
    }
}
