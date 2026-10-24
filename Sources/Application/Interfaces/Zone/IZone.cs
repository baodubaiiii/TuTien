using TuTien.Application.Interfaces.Character;
using TuTien.Application.Interfaces.Map;

namespace TuTien.Application.Interfaces.Zone
{
    public interface IZone
    {
        int Id { get; set; }
        IMap? Map { get; set; }
        IReadOnlyDictionary<int, ICharacter> Characters { get; }
        IZoneHandler ZoneHandler { get; set; }
    }
}
