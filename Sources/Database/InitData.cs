using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using TuTien.Model.Template;
using TuTien.Model.Info;

namespace TuTien.Database
{
    public class InitData
    {
        private readonly HttpClient _http;
        private readonly ConfigManager _config;
        private readonly Cache _cache;
        private readonly ILogger<InitData> _logger;

        public InitData(HttpClient http, ConfigManager config, Cache cache, ILogger<InitData> logger)
        {
            _http = http;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        public Task<bool> Load(CancellationToken cancellationToken = default)
        {
            return Load(_config.CdnBaseUrl, cancellationToken);
        }

        public async Task<bool> Load(string baseUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogDebug("External CDN is not configured; local catalog will load after HTTP startup");
                return false;
            }

            try
            {
                if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
                    (root.Scheme != "https" && !(root.Scheme == "http" && root.IsLoopback)))
                {
                    throw new InvalidDataException("CDN must use HTTPS or loopback HTTP");
                }

                using var manifest = JsonDocument.Parse(await ReadFile(new Uri(root, "manifest.json"), 65536, cancellationToken));
                var data = manifest.RootElement;
                var entries = data.GetProperty("files");
                if (data.GetProperty("version").GetString() != "base" || entries.GetArrayLength() != 1)
                {
                    throw new InvalidDataException("Invalid base manifest");
                }

                var entry = entries[0];
                string hash = entry.GetProperty("sha256").GetString() ?? string.Empty;
                string path = entry.GetProperty("path").GetString() ?? string.Empty;
                int size = entry.GetProperty("size").GetInt32();
                if (entry.GetProperty("key").GetString() != "catalog" || !Regex.IsMatch(hash, "^[a-f0-9]{64}$") ||
                    path != "catalog." + hash[..16] + ".json" || size < 1 || size > 8388608)
                {
                    throw new InvalidDataException("Invalid catalog entry");
                }

                byte[] bytes = await ReadFile(new Uri(root, path), 8388608, cancellationToken);
                if (bytes.Length != size || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != hash)
                {
                    throw new InvalidDataException("Catalog hash mismatch");
                }

                using var document = JsonDocument.Parse(bytes);
                var catalog = document.RootElement;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = catalog.GetProperty("items").Deserialize<List<ItemTemplate>>(options)!.ToDictionary(value => value.Id);
                var itemOptions = catalog.GetProperty("itemOptions").Deserialize<List<ItemOptionTemplate>>(options)!.ToDictionary(value => value.Id);
                var skills = catalog.GetProperty("skills").Deserialize<List<SkillTemplate>>(options)!.ToDictionary(value => value.Id);
                var npcs = catalog.GetProperty("npcs").Deserialize<List<NpcTemplate>>(options)!.ToDictionary(value => value.Id);
                var bosses = catalog.GetProperty("bosses").Deserialize<List<BossTemplate>>(options)!.ToDictionary(value => value.Id);
                var tasks = catalog.GetProperty("tasks").Deserialize<List<TaskTemplate>>(options)!.ToDictionary(value => value.Id);
                var maps = ReadTemplates<MapTemplate>(catalog, "maps", options).ToDictionary(value => value.Id);
                var monsters = ReadTemplates<MonsterTemplate>(catalog, "monsters", options).ToDictionary(value => value.Id);
                var realms = ReadTemplates<RealmTemplate>(catalog, "realms", options).ToDictionary(value => value.Id);

                var races = ReadTemplates<RaceTemplate>(catalog, "races", options).ToDictionary(value => value.Id);
                var elements = ReadTemplates<ElementTemplate>(catalog, "elements", options).ToDictionary(value => value.Id);
                var classes = ReadTemplates<ClassTemplate>(catalog, "classes", options).ToDictionary(value => value.Id);
                var appearances = ReadTemplates<AppearanceTemplate>(catalog, "appearances", options).ToDictionary(value => value.Id);
                var stats = new InfoChar();
                if (catalog.TryGetProperty("characterStats", out var info))
                {
                    stats = info.Deserialize<InfoChar>(options) ?? throw new InvalidDataException("Invalid character stats");
                }

                if (races.Count > 0 && (races.Count != 5 || elements.Count != 5 || classes.Count != 5 ||
                    Enumerable.Range(1, 5).Any(id => !races.ContainsKey(id) || !elements.ContainsKey(id) || !classes.ContainsKey(id)) ||
                    appearances.Values.Any(value => value.Gender < 0 || value.Gender > 1) ||
                    !appearances.Values.Any(value => value.Gender == 0) || !appearances.Values.Any(value => value.Gender == 1)))
                {
                    throw new InvalidDataException("Invalid character creation templates");
                }

                var templates = new Cache
                {
                    ItemTemplates = items,
                    ItemOptionTemplates = itemOptions,
                    SkillTemplates = skills,
                    NpcTemplates = npcs,
                    MapTemplates = maps,
                    MonsterTemplates = monsters,
                    TaskTemplates = tasks,
                    RealmTemplates = realms
                };
                templates.CheckTemplates();

                _cache.ItemTemplates = items;
                _cache.ItemOptionTemplates = itemOptions;
                _cache.SkillTemplates = skills;
                _cache.NpcTemplates = npcs;
                _cache.BossTemplates = bosses;
                _cache.TaskTemplates = tasks;
                _cache.MapTemplates = maps;
                _cache.MonsterTemplates = monsters;
                _cache.RealmTemplates = realms;

                _cache.RaceTemplates = races;
                _cache.ElementTemplates = elements;
                _cache.ClassTemplates = classes;
                _cache.AppearanceTemplates = appearances;
                _cache.CharacterInfo = stats;
                _logger.LogInformation("Base templates loaded from CDN");
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                _logger.LogError(error, "CDN load failed");
                return false;
            }
        }

        private static List<T> ReadTemplates<T>(JsonElement catalog, string key, JsonSerializerOptions options)
        {
            return catalog.TryGetProperty(key, out var value) ? value.Deserialize<List<T>>(options)! : new List<T>();
        }

        private async Task<byte[]> ReadFile(Uri uri, int maxBytes, CancellationToken cancellationToken)
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > maxBytes)
            {
                throw new InvalidDataException("CDN file exceeds limit");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + count > maxBytes)
                {
                    throw new InvalidDataException("CDN file exceeds limit");
                }

                output.Write(buffer, 0, count);
            }

            return output.ToArray();
        }
    }
}
