using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client
{
    public partial class WorldScene
    {
        public TMP_Text TuVi;
        public Button InteractButton;
        public Button ZoneButton;
        public GameObject NpcPanel;
        public TMP_Text NpcName;
        public TMP_Text NpcText;
        public Button NpcClose;
        public Button NpcZones;
        public GameObject ZonePanel;
        public TMP_Dropdown ZoneList;
        public Button ZoneConfirm;
        public Button ZoneClose;
        public TMP_Text ZoneError;
        public TMP_Text NpcError;
        public GameObject DeathPanel;
        public TMP_Text DeathText;
        public Button ReviveButton;
        private readonly Dictionary<int, CharacterView> monsters = new Dictionary<int, CharacterView>();
        private readonly Dictionary<string, ItemMapView> itemViews = new Dictionary<string, ItemMapView>();
        private GameObject monsterPrefab;
        private GameObject itemPrefab;
        private bool targetIsMonster;
        private int worldMapId;
        private int worldZoneId;
        private JObject[] zones;

        private void InitWorld()
        {
            monsterPrefab = Client.WorldBundle.LoadAsset<GameObject>("Monster");
            itemPrefab = Client.WorldBundle.LoadAsset<GameObject>("ItemMap");
            worldMapId = Client.Character["map"].Value<int>("id");
            worldZoneId = Client.Character.Value<int>("zoneId");
            InteractButton.onClick.AddListener(() => Run(Interact));
            ZoneButton.onClick.AddListener(() => Run(OpenZones));
            NpcZones.onClick.AddListener(() => Run(OpenZones));
            NpcClose.onClick.AddListener(() => NpcPanel.SetActive(false));
            ZoneClose.onClick.AddListener(() => ZonePanel.SetActive(false));
            ZoneConfirm.onClick.AddListener(() => Run(ChangeZone));
            ReviveButton.onClick.AddListener(() => Run(Revive));
        }

        private void RefreshWorld()
        {
            int mapId = Client.Character["map"].Value<int>("id");
            int zoneId = Client.Character.Value<int>("zoneId");
            if (worldMapId == mapId && worldZoneId == zoneId)
            {
                return;
            }
            worldMapId = mapId;
            worldZoneId = zoneId;
            Destroy(map.gameObject);
            players.Clear();
            npcs.Clear();
            monsters.Clear();
            itemViews.Clear();
            shownAttacks.Clear();
            targetId = 0;
            targetIsMonster = false;
            map = Instantiate(Client.WorldBundle.LoadAsset<GameObject>("MapRoot")).GetComponent<MapView>();
            map.Load((JObject)Client.Character["map"]);
        }

        private void RenderWorld()
        {
            TuVi.text = "Tu vi: " + Client.Character["game"].Value<long>("tuVi");
            var ids = new HashSet<int>();
            foreach (JObject value in Client.Monsters)
            {
                int id = value.Value<int>("id");
                ids.Add(id);
                if (!monsters.TryGetValue(id, out var view))
                {
                    view = Instantiate(monsterPrefab, map.transform).GetComponent<CharacterView>();
                    monsters.Add(id, view);
                }
                view.SetData(value, false, targetIsMonster && targetId == id, false, true);
            }
            foreach (int id in monsters.Keys.Where(value => !ids.Contains(value)).ToArray())
            {
                Destroy(monsters[id].gameObject);
                monsters.Remove(id);
            }
            var items = new HashSet<string>();
            foreach (JObject value in Client.Items)
            {
                string id = value.Value<string>("id");
                items.Add(id);
                if (!itemViews.TryGetValue(id, out var view))
                {
                    view = Instantiate(itemPrefab, map.transform).GetComponent<ItemMapView>();
                    view.Id = id;
                    itemViews.Add(id, view);
                }
                view.transform.position = new Vector3(value.Value<float>("x") / 100, -value.Value<float>("y") / 100, 0);
                var template = Client.Catalog["items"].OfType<JObject>().First(item => item.Value<int>("id") == value.Value<int>("itemId"));
                view.Name.text = template.Value<string>("name").Replace(template.Value<string>("subName") + " ", "") + " x" + value["quantity"];
            }
            foreach (string id in itemViews.Keys.Where(value => !items.Contains(value)).ToArray())
            {
                Destroy(itemViews[id].gameObject);
                itemViews.Remove(id);
            }
        }

        private void UpdateWorld(long now)
        {
            bool dead = Client.Character["game"].Value<long>("hp") <= 0;
            DeathPanel.SetActive(dead);
            if (dead)
            {
                Inventory.gameObject.SetActive(false);
                MenuPanel.SetActive(false);
                NpcPanel.SetActive(false);
                ZonePanel.SetActive(false);
                CloseProgression();
                long delay = Client.ServerInfo?.Value<int>("reviveDelay") ?? 3000;
                long remaining = Math.Max(0, delay - (now - Client.Character["game"].Value<long>("deathTime")));
                DeathText.text = "Đạo hữu đã hết sinh lực.\nTu vi được giữ nguyên.\n\n" + (remaining > 0 ? "Có thể hồi sinh sau " + (remaining / 1000f).ToString("0.0") + "s" : "Trở về Thanh Vân Thôn để tiếp tục.");
                ReviveButton.interactable = !Busy && Client.Connected && remaining == 0;
                ReviveButton.GetComponentInChildren<TMP_Text>().text = remaining > 0 ? "Chờ hồi sinh..." : "Trở về thôn";
            }
            bool allowed = Client.Connected && talentConfirmed && !dead && !Busy && !TrialActive && !ProgressionPanel.activeSelf && !Inventory.gameObject.activeSelf && !MenuPanel.activeSelf && !NpcPanel.activeSelf && !ZonePanel.activeSelf;
            InteractButton.interactable = allowed;
            ZoneButton.interactable = allowed;
            ZoneConfirm.interactable = !Busy && zones != null && ZoneList.value < zones.Length &&
                zones[ZoneList.value].Value<int>("id") != Client.Character.Value<int>("zoneId") &&
                zones[ZoneList.value].Value<int>("count") < zones[ZoneList.value].Value<int>("capacity");
            ZoneError.text = ErrorText.text;
            NpcError.text = ErrorText.text;
        }

        public void SelectMonster(int id)
        {
            targetId = id;
            targetIsMonster = true;
            Render();
        }

        private void SelectNextTarget()
        {
            var ids = Client.Monsters.OfType<JObject>().Where(value => value.Value<long>("hp") > 0).Select(value => value.Value<int>("id")).ToArray();
            if (ids.Length > 0)
            {
                int index = targetIsMonster ? Array.IndexOf(ids, targetId) : -1;
                SelectMonster(ids[(index + 1) % ids.Length]);
            }
            else
            {
                targetIsMonster = false;
                SelectNext();
            }
        }

        public async Task TalkNpc(int id)
        {
            PauseMovement();
            var result = await Client.Request("npc.menu", new { id, playToken = Client.Character.Value<string>("playToken") });
            if (!this)
            {
                return;
            }
            NpcName.text = result["data"].Value<string>("name");
            NpcText.text = result["data"].Value<string>("text");
            NpcPanel.SetActive(true);
        }

        private async Task OpenZones()
        {
            PauseMovement();
            var result = await Client.Request("world.zones");
            if (!this)
            {
                return;
            }
            zones = result["data"]["zones"].OfType<JObject>().ToArray();
            ZoneList.ClearOptions();
            ZoneList.AddOptions(zones.Select(value => "Khu " + value["id"] + "   " + value["count"] + " / " + value["capacity"]).ToList());
            int current = Array.FindIndex(zones, value => value.Value<int>("id") == Client.Character.Value<int>("zoneId"));
            ZoneList.value = Math.Max(0, current);
            NpcPanel.SetActive(false);
            ZonePanel.SetActive(true);
        }

        private async Task ChangeZone()
        {
            await Client.ChangeMap("zone.change", new
            {
                zoneId = zones[ZoneList.value].Value<int>("id"),
                playToken = Client.Character.Value<string>("playToken")
            });
            if (this)
            {
                ZonePanel.SetActive(false);
            }
        }

        public async Task ChangeMap(int mapId)
        {
            PauseMovement();
            await Client.ChangeMap("map.change", new { mapId, playToken = Client.Character.Value<string>("playToken") });
        }

        public async Task PickItem(string id)
        {
            var result = await Client.Request("item.pick", new { id, playToken = Client.Character.Value<string>("playToken") });
            Client.SetCharacter((JObject)result["data"]);
            if (this)
            {
                ErrorText.text = "Đã nhặt vật phẩm vào hành trang.";
            }
        }

        private async Task Revive()
        {
            await Client.ChangeMap("character.revive", new { playToken = Client.Character.Value<string>("playToken") });
        }

        private void PauseMovement()
        {
            keyboardMoving = false;
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            if (self != null)
            {
                RunMove(self.Value<float>("x"), self.Value<float>("y"));
            }
        }

        private async Task Interact()
        {
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            if (self == null)
            {
                return;
            }
            var position = new Vector2(self.Value<float>("x"), self.Value<float>("y"));
            float distance = Client.ServerInfo?.Value<int>("interactionRange") ?? 140;
            string type = string.Empty;
            string itemId = string.Empty;
            int id = 0;
            foreach (JObject value in Client.Items.Concat(Client.Npcs).Concat(Client.Character["map"]["waypoint"]))
            {
                float current = Vector2.Distance(position, new Vector2(value.Value<float>("x"), value.Value<float>("y")));
                if (current > distance)
                {
                    continue;
                }
                distance = current;
                if (value["itemId"] != null)
                {
                    type = "item";
                    itemId = value.Value<string>("id");
                }
                else if (value["mapId"] != null)
                {
                    type = "map";
                    id = value.Value<int>("mapId");
                }
                else
                {
                    type = "npc";
                    id = value.Value<int>("id");
                }
            }
            switch (type)
            {
                case "item":
                    await PickItem(itemId);
                    break;
                case "map":
                    await ChangeMap(id);
                    break;
                case "npc":
                    await TalkNpc(id);
                    break;
                default:
                    ErrorText.text = "Đến gần NPC, vật phẩm hoặc lối ra để tương tác.";
                    break;
            }
        }
    }
}
