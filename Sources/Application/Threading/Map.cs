using TuTien.Application.Interfaces.Map;
using TuTien.Application.Interfaces.Zone;
using System.Threading.Channels;

namespace TuTien.Application.Threading
{
    public class Map : IMap
    {
        public int Id { get; set; }
        public List<IZone> Zones { get; }
        public bool IsRunning { get; private set; }
        public long TimeMap { get; private set; }
        private readonly Channel<MapCommand> _commands;
        public Func<long, Task>? UpdateCharacters { get; set; }

        public Map()
        {
            Zones = new List<IZone>();
            _commands = Channel.CreateBounded<MapCommand>(new BoundedChannelOptions(512)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public virtual void Start()
        {
            IsRunning = true;
        }

        public virtual void Update(long timeServer)
        {
            if (!IsRunning)
            {
                return;
            }

            TimeMap = timeServer;

            foreach (var zone in Zones)
            {
                zone.ZoneHandler.Update(timeServer);
            }
        }

        public IZone? GetZoneById(int id)
        {
            return Zones.Find(zone => zone.Id == id);
        }

        public virtual void Close()
        {
            IsRunning = false;
            _commands.Writer.TryComplete();
            while (_commands.Reader.TryRead(out var command))
            {
                command.Cancel();
            }

            foreach (var zone in Zones)
            {
                zone.ZoneHandler.Close();
            }
        }

        public bool AddCommand(MapCommand command)
        {
            return IsRunning && _commands.Writer.TryWrite(command);
        }

        public async Task<bool> AddCommandAndWait(MapCommand command, CancellationToken cancellationToken)
        {
            if (!IsRunning)
            {
                return false;
            }
            try
            {
                await _commands.Writer.WriteAsync(command, cancellationToken);
                return true;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }

        public async Task UpdateGame(long timeServer)
        {
            if (!IsRunning)
            {
                return;
            }

            int count = 0;
            while (count++ < 256 && _commands.Reader.TryRead(out var command))
            {
                await command.Execute();
            }

            if (UpdateCharacters != null)
            {
                await UpdateCharacters(timeServer);
            }
            Update(timeServer);
        }
    }
}
