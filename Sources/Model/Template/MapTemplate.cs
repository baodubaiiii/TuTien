using System.Text.Json;
using TuTien.Model.Map;

namespace TuTien.Model.Template
{
    public class MapTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int RealmId { get; set; }
        public int MaxPlayer { get; set; }
        public int ZoneNumber { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int TileId { get; set; }
        public int BgId { get; set; }
        public int BgType { get; set; }
        public bool IsDouble { get; set; }
        public int Teleport { get; set; }
        public List<WayPoint> Waypoint { get; set; }
        public MonsterSpawn[] Mob { get; set; }
        public int[][] ArrNpc { get; set; }
        public JsonElement[] Background { get; set; }
        public JsonElement[] Action { get; set; }
        public JsonElement[] MapCollisions { get; set; }
        public string AssetKey { get; set; }

        public MapTemplate()
        {
            Name = string.Empty;
            AssetKey = string.Empty;
            Waypoint = new List<WayPoint>();
            Mob = Array.Empty<MonsterSpawn>();
            ArrNpc = Array.Empty<int[]>();
            Background = Array.Empty<JsonElement>();
            Action = Array.Empty<JsonElement>();
            MapCollisions = Array.Empty<JsonElement>();
        }
    }
}
