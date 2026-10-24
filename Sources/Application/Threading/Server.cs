using TuTien.Application.Interfaces.Client;
using TuTien.Application.Interfaces.Map;
using TuTien.Application.Manager;
using TuTien.Database;

namespace TuTien.Application.Threading
{
    public partial class Server : IHostedService, IDisposable
    {
        private readonly MapManager _maps;
        private readonly IClientManager _clients;
        private readonly ConfigManager _config;
        private readonly InitData _initData;
        private readonly ILogger<Server> _logger;
        private readonly CancellationTokenSource _stop;
        private Task[] _workers;
        private int _disposed;
        private readonly IHostApplicationLifetime? _lifetime;
        private readonly IServiceProvider? _services;

        public bool IsRunning { get; private set; }

        public Server(MapManager maps, IClientManager clients, ConfigManager config, InitData initData,
            ILogger<Server> logger, IHostApplicationLifetime? lifetime = null, IServiceProvider? services = null)
        {
            _maps = maps;
            _clients = clients;
            _config = config;
            _initData = initData;
            _logger = logger;
            _lifetime = lifetime;
            _services = services;
            _stop = new CancellationTokenSource();
            _workers = Array.Empty<Task>();
        }

        public async Task StartServer(CancellationToken cancellationToken = default)
        {
            if (IsRunning || _stop.IsCancellationRequested)
            {
                return;
            }

            await _initData.Load(cancellationToken);
            await StartWorld();
        }

        public async Task StartWorld()
        {
            if (_workers.Length > 0 || _stop.IsCancellationRequested)
            {
                return;
            }
            await _maps.InitMapServer();

            var maps = _maps.GetAllMap();

            foreach (var map in maps)
            {
                map.Start();
            }

            IsRunning = true;
            if (maps.Length == 0)
            {
                return;
            }
            _workers = Enumerable.Range(0, _config.WorkerCount)
                .Select(index => Update(maps.Where(map => Math.Abs((long)map.Id) % _config.WorkerCount == index).ToArray()))
                .ToArray();
            _maps.StartWorld();
            StartTcp();
            _logger.LogInformation("World started: {MapCount} maps", maps.Length);
        }

        private async Task Update(IMap[] maps)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / _config.TickRate));

            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                {
                    long timeServer = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    foreach (var map in maps)
                    {
                        if (map is Map runtime)
                        {
                            await runtime.UpdateGame(timeServer);
                        }
                        else
                        {
                            map.Update(timeServer);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                IsRunning = false;
                _stop.Cancel();
                _logger.LogError(error, "Map update failed");
                _maps.BeginShutdown();
                foreach (var map in maps)
                {
                    map.Close();
                }
                _lifetime?.StopApplication();
                throw;
            }
        }

        public async Task StopServer(CancellationToken cancellationToken = default)
        {
            _maps.BeginShutdown();
            IsTcpRunning = false;
            _listener?.Stop();
            await _acceptClients;
            await _clients.Clear();
            _stop.Cancel();
            await Task.WhenAll(_workers);

            foreach (var map in _maps.GetAllMap())
            {
                map.Close();
            }

            IsRunning = false;
            _logger.LogInformation("Base server stopped");
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            return StartServer(cancellationToken);
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            return StopServer(cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _stop.Cancel();
            _listener?.Stop();
            _stop.Dispose();
        }
    }
}
