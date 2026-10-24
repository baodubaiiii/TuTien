using TuTien.Application.Interfaces.Zone;

namespace TuTien.Application.Interfaces.Map
{
    public interface IMap
    {
        int Id { get; set; }
        List<IZone> Zones { get; }
        bool IsRunning { get; }
        long TimeMap { get; }
        void Start();
        void Update(long timeServer);
        IZone? GetZoneById(int id);
        void Close();
    }
}
