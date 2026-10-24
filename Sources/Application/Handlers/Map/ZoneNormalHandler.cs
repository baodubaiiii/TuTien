using TuTien.Application.Interfaces.Map;
using TuTien.Application.Interfaces.Zone;

namespace TuTien.Application.Handlers.Map
{
    public class ZoneNormalHandler : IZoneHandler
    {
        protected IZone Zone { get; }

        public ZoneNormalHandler(IZone zone)
        {
            Zone = zone;
        }

        public virtual void Update(long timeServer)
        {
        }

        public virtual void Close()
        {
        }
    }
}
