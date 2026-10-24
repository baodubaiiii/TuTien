namespace TuTien.Database
{
    public class ConfigManager
    {
        public int MaxClients { get; }
        public int MaxMessageBytes { get; }
        public int SendQueueCapacity { get; }
        public int MaxQueuedBytes { get; }
        public int TickRate { get; }
        public int WorkerCount { get; }
        public bool TcpEnabled { get; }
        public string TcpHost { get; }
        public int TcpPort { get; }
        public string CdnBaseUrl { get; }
        public string ConnectionString { get; }
        public string DatabaseProvider { get; }
        public string SqliteFile { get; }
        public int StartingPoints { get; }
        public int BagCapacity { get; }
        public float MoveSpeed { get; }
        public int HpPerKhiHuyet { get; }
        public int MpPerPhapLuc { get; }
        public decimal CritPerSatCo { get; }
        public decimal DodgePerLinhGiac { get; }
        public float AttackRange { get; }
        public int AttackCooldown { get; }
        public int InteractionRange { get; }
        public int ReviveDelay { get; }
        public int DropOwnerSeconds { get; }
        public int DropLifeSeconds { get; }
        public int MapChangeCooldown { get; }
        public int TrialStrikeInterval { get; }
        public int[] TrialDamagePercent { get; }
        public int TrialLossMin { get; }
        public int TrialLossMax { get; }

        public ConfigManager(IConfiguration config)
        {
            MaxClients = Math.Clamp(config.GetValue("Server:MaxClients", 3500), 1, 10000);
            MaxMessageBytes = Math.Clamp(config.GetValue("Server:MaxMessageBytes", 16384), 1024, 65536);
            SendQueueCapacity = Math.Clamp(config.GetValue("Server:SendQueueCapacity", 64), 1, 256);
            MaxQueuedBytes = Math.Clamp(config.GetValue("Server:MaxQueuedBytes", 524288), MaxMessageBytes, 4194304);
            TickRate = Math.Clamp(config.GetValue("Server:TickRate", 20), 1, 100);
            WorkerCount = Math.Clamp(config.GetValue("Server:WorkerCount", 1), 1, 4);
            TcpEnabled = config.GetValue("Server:TcpEnabled", true);
            TcpHost = config["Server:TcpHost"] ?? "127.0.0.1";
            TcpPort = Math.Clamp(config.GetValue("Server:TcpPort", 7777), 1, 65535);
            CdnBaseUrl = config["Cdn:BaseUrl"] ?? string.Empty;
            ConnectionString = config.GetConnectionString("Game") ?? string.Empty;
            DatabaseProvider = config["Database:Provider"] ?? "None";
            SqliteFile = Path.GetFullPath(config["Database:SqliteFile"] ?? ".runtime/character-creation/game.sqlite");
            StartingPoints = Math.Clamp(config.GetValue("Game:StartingPoints", 100), 0, 10000);
            BagCapacity = Math.Clamp(config.GetValue("Game:BagCapacity", 40), 8, 120);
            MoveSpeed = Math.Clamp(config.GetValue("Game:MoveSpeed", 180f), 60, 600);
            HpPerKhiHuyet = Math.Clamp(config.GetValue("Game:HpPerKhiHuyet", 10), 1, 100);
            MpPerPhapLuc = Math.Clamp(config.GetValue("Game:MpPerPhapLuc", 5), 1, 100);
            CritPerSatCo = Math.Clamp(config.GetValue("Game:CritPerSatCo", 0.1m), 0, 100);
            DodgePerLinhGiac = Math.Clamp(config.GetValue("Game:DodgePerLinhGiac", 0.1m), 0, 100);
            AttackRange = Math.Clamp(config.GetValue("Game:AttackRange", 120f), 30, 500);
            AttackCooldown = Math.Clamp(config.GetValue("Game:AttackCooldown", 800), 200, 5000);
            InteractionRange = Math.Clamp(config.GetValue("Game:InteractionRange", 140), 60, 300);
            ReviveDelay = Math.Clamp(config.GetValue("Game:ReviveDelay", 3000), 0, 30000);
            DropOwnerSeconds = Math.Clamp(config.GetValue("Game:DropOwnerSeconds", 30), 1, 300);
            DropLifeSeconds = Math.Clamp(config.GetValue("Game:DropLifeSeconds", 180), DropOwnerSeconds, 3600);
            MapChangeCooldown = Math.Clamp(config.GetValue("Game:MapChangeCooldown", 1000), 0, 10000);
            TrialStrikeInterval = Math.Clamp(config.GetValue("Game:TrialStrikeInterval", 3000), 200, 30000);
            TrialDamagePercent = config.GetSection("Game:TrialDamagePercent").Get<int[]>() ?? new[] { 20, 30, 40 };
            if (TrialDamagePercent.Length < 1 || TrialDamagePercent.Length > 36 || TrialDamagePercent.Any(value => value < 1 || value > 100))
            {
                throw new InvalidDataException("Invalid trial damage configuration");
            }
            TrialLossMin = Math.Clamp(config.GetValue("Game:TrialLossMin", 10), 0, 100);
            TrialLossMax = Math.Clamp(config.GetValue("Game:TrialLossMax", 20), TrialLossMin, 100);
        }
    }
}
