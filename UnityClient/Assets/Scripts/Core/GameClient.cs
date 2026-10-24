using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TuTien.Client
{
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance { get; private set; }
        public ServerSettings Settings;
        public JObject Catalog { get; private set; }
        public JObject Character { get; private set; }
        public JArray Players { get; private set; } = new JArray();
        public JArray Npcs { get; private set; } = new JArray();
        public JArray Attacks { get; private set; } = new JArray();
        public JArray Monsters { get; private set; } = new JArray();
        public JArray Items { get; private set; } = new JArray();
        public JObject ServerInfo { get; private set; }
        public JObject Slots { get; private set; }
        public AssetBundle WorldBundle { get; private set; }
        public int AccountId { get; private set; }
        public string AccountName { get; private set; }
        public long ServerTime { get; private set; }
        public float ServerFrameTime { get; private set; }
        public long DownloadSize { get; private set; }
        public string Error { get; private set; }

        public bool Connected
        {
            get
            {
                return network.IsConnected;
            }
        }

        public event Action Changed;

        private readonly Session_ME network = new Session_ME();
        private readonly ConcurrentQueue<JObject> messages = new ConcurrentQueue<JObject>();
        private readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly List<JObject> downloads = new List<JObject>();
        private float nextPing;
        private string catalogFile;
        private string bundleFile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            DontDestroyOnLoad(gameObject);
            network.OnMessage = value => messages.Enqueue(value);
        }

        private void Update()
        {
            while (messages.TryDequeue(out JObject message))
            {
                if (message.Value<string>("type") == "disconnected")
                {
                    Error = "Mất kết nối máy chủ. Hãy đăng nhập lại.";
                    Changed?.Invoke();
                    continue;
                }
                if (message.Value<string>("type") != "state")
                {
                    continue;
                }
                ServerTime = message.Value<long>("serverTime");
                Error = string.Empty;
                ServerFrameTime = Time.unscaledTime;
                Players = message["players"] as JArray ?? new JArray();
                Npcs = message["npcs"] as JArray ?? new JArray();
                Attacks = message["attacks"] as JArray ?? new JArray();
                Monsters = message["monsters"] as JArray ?? new JArray();
                Items = message["items"] as JArray ?? new JArray();
                if (message["character"] is JObject character)
                {
                    SetCharacter(character, false);
                }
                Changed?.Invoke();
            }
            if (Connected && Time.unscaledTime >= nextPing)
            {
                nextPing = Time.unscaledTime + 10;
                _ = Ping();
            }
        }

        private async Task Ping()
        {
            try
            {
                await network.SendMessage("ping");
            }
            catch (Exception)
            {
                if (!Connected)
                {
                    Error = "Mất kết nối máy chủ. Hãy đăng nhập lại.";
                    Changed?.Invoke();
                }
            }
        }

        public async Task Prepare()
        {
            Error = string.Empty;
            downloads.Clear();
            catalogFile = null;
            bundleFile = null;
            var bootstrap = JObject.Parse(await http.GetStringAsync(Settings.HttpUrl + "/api/bootstrap"));
            string baseUrl = new Uri(new Uri(Settings.HttpUrl), bootstrap.Value<string>("cdnBaseUrl") + "/").ToString();
            JObject catalog = JObject.Parse(await http.GetStringAsync(baseUrl + "manifest.json"));
            JObject world = JObject.Parse(await http.GetStringAsync(Settings.HttpUrl + "/cdn/unity/manifest.json"));
            foreach (JObject entry in catalog["files"])
            {
                var file = (JObject)entry.DeepClone();
                file["url"] = baseUrl + entry.Value<string>("path");
                file["local"] = CachePath(file);
                downloads.Add(file);
                if (entry.Value<string>("key") == "catalog")
                {
                    catalogFile = file.Value<string>("local");
                }
            }
            foreach (JObject entry in world["files"])
            {
                var file = (JObject)entry.DeepClone();
                file["url"] = Settings.HttpUrl + "/cdn/unity/" + entry.Value<string>("path");
                file["local"] = CachePath(file);
                downloads.Add(file);
                if (entry.Value<string>("key") == "world")
                {
                    bundleFile = file.Value<string>("local");
                }
            }
            if (catalogFile == null || bundleFile == null)
            {
                throw new IOException("Dữ liệu tải chưa đầy đủ.");
            }
            DownloadSize = downloads.Where(file => !ValidFile(file)).Sum(file => file.Value<long>("size"));
        }

        public async Task Download(Action<float> progress)
        {
            int completed = 0;
            foreach (var file in downloads)
            {
                if (!ValidFile(file))
                {
                    byte[] bytes = await http.GetByteArrayAsync(file.Value<string>("url"));
                    if (bytes.LongLength != file.Value<long>("size") || Hash(bytes) != file.Value<string>("sha256"))
                    {
                        throw new IOException("Dữ liệu tải bị lỗi. Hãy thử lại.");
                    }
                    string path = file.Value<string>("local");
                    File.WriteAllBytes(path + ".part", bytes);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    File.Move(path + ".part", path);
                }
                progress?.Invoke(++completed / (float)downloads.Count);
            }
            Catalog = JObject.Parse(File.ReadAllText(catalogFile));
            if (WorldBundle == null)
            {
                var bundle = AssetBundle.LoadFromFileAsync(bundleFile);
                while (!bundle.isDone)
                {
                    await Task.Yield();
                }

                WorldBundle = bundle.assetBundle;
            }
            if (WorldBundle == null)
            {
                throw new IOException("Không mở được dữ liệu bản đồ.");
            }
            await Connect();
        }

        private static string CachePath(JObject file)
        {
            string hash = file.Value<string>("sha256");
            long size = file.Value<long>("size");
            if (hash == null || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || size < 1 || size > 268435456)
            {
                throw new IOException("Danh mục tải không hợp lệ.");
            }
            string root = Path.Combine(Application.persistentDataPath, "cache");
            Directory.CreateDirectory(root);
            return Path.Combine(root, hash);
        }

        private static bool ValidFile(JObject file)
        {
            string path = file.Value<string>("local");
            return File.Exists(path) && new FileInfo(path).Length == file.Value<long>("size") && Hash(File.ReadAllBytes(path)) == file.Value<string>("sha256");
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }

        public async Task Connect()
        {
            Error = string.Empty;
            while (messages.TryDequeue(out _))
            {
            }
            ClearCharacter();
            await network.Connect(Settings.Host, Settings.Port);
            nextPing = Time.unscaledTime + 10;
            ServerInfo = (JObject)(await Request("bootstrap"))["data"];
        }

        public Task<JObject> Request(string type, object data = null)
        {
            return network.SendMessage(type, data);
        }

        public async Task Login(string username, string password, bool register)
        {
            if (!Connected)
            {
                await Connect();
            }

            JObject response = await Request(register ? "register" : "login", new
            {
                username,
                password
            });
            AccountId = response["data"].Value<int>("id");
            AccountName = response["data"].Value<string>("username");
            await LoadScene("CharacterSelectScene");
        }

        public async Task RefreshCharacters()
        {
            Slots = (JObject)(await Request("characters"))["data"];
        }

        public async Task Enter(int id)
        {
            ClearCharacter();
            var entered = await Request("character.enter", new
            {
                id
            });
            string token = entered["data"].Value<string>("playToken");
            float deadline = Time.realtimeSinceStartup + 10;
            while (Character == null || Character.Value<int>("id") != id || Character.Value<string>("playToken") != token)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new IOException("Chưa nhận được nhân vật.");
                }

                await Task.Yield();
            }
            await LoadScene("WorldScene");
        }

        public async Task Move(float x, float y)
        {
            await network.SendMessage("move", new
            {
                x,
                y,
                playToken = Character?.Value<string>("playToken") ?? string.Empty
            }, false);
        }

        public void SetCharacter(JObject value, bool notify = true)
        {
            if (Character != null && Character.Value<int>("id") == value.Value<int>("id") && Character.Value<string>("playToken") == value.Value<string>("playToken"))
            {
                long oldRevision = Character.Value<long>("gameRevision");
                long revision = value.Value<long>("gameRevision");
                if (revision < oldRevision || (revision == oldRevision && value.Value<long>("stateVersion") < Character.Value<long>("stateVersion")))
                {
                    return;
                }
            }
            Character = (JObject)value.DeepClone();
            if (notify)
            {
                Changed?.Invoke();
            }
        }

        public async Task Logout()
        {
            if (Connected)
            {
                await Request("logout");
            }

            AccountId = 0;
            AccountName = string.Empty;
            ClearCharacter();
            await LoadScene("LoginScene");
        }

        public void ClearCharacter()
        {
            Character = null;
            Players = new JArray();
            Npcs = new JArray();
            Attacks = new JArray();
            Monsters = new JArray();
            Items = new JArray();
        }

        public async Task ChangeMap(string type, object data)
        {
            var response = await Request(type, data);
            string token = response["data"].Value<string>("playToken");
            float deadline = Time.realtimeSinceStartup + 10;
            while (Character == null || Character.Value<string>("playToken") != token)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new IOException("Chưa nhận được khu vực mới. Hãy vào lại game.");
                }
                await Task.Yield();
            }
        }

        public static async Task LoadScene(string name)
        {
            var operation = SceneManager.LoadSceneAsync(name);
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            network.Dispose();
            http.Dispose();
            if (WorldBundle)
            {
                WorldBundle.Unload(true);
            }

            Instance = null;
        }
    }
}
