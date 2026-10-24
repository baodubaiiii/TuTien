using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TuTien.Client
{
    public partial class WorldScene : BaseScene
    {
        public Camera Camera;
        public TMP_Text Name;
        public TMP_Text Realm;
        public TMP_Text HpText;
        public TMP_Text MpText;
        public Image HpFill;
        public Image MpFill;
        public TMP_Text Target;
        public TMP_Text Location;
        public TMP_Text Effects;
        public ItemEffectView[] EffectSlots;
        public TMP_Text Connection;
        public TMP_Text AttackLabel;
        public TMP_Text CombatHint;
        public Image TargetHpFill;
        public Button MenuButton;
        public Button ResumeButton;
        public GameObject MenuPanel;
        public Button InventoryButton;
        public Button StatsButton;
        public Button AttackButton;
        public Button ChangeCharacter;
        public Button Logout;
        public InventoryPanel Inventory;
        public DemoJoystick Joystick;
        public GameObject TalentPanel;
        public TMP_Text TalentText;
        public Button ConfirmTalent;
        public GameObject Beam;
        public TMP_Text Announcement;
        public FloatingText FloatingPrefab;
        private MapView map;
        private GameObject playerPrefab;
        private GameObject remotePrefab;
        private GameObject npcPrefab;
        private readonly Dictionary<int, CharacterView> players = new Dictionary<int, CharacterView>();
        private readonly Dictionary<int, CharacterView> npcs = new Dictionary<int, CharacterView>();
        private readonly HashSet<long> shownAttacks = new HashSet<long>();
        private int targetId;
        private float nextMove;
        private bool keyboardMoving;
        private bool talentConfirmed;
        private bool ready;
        private bool connectionError;
        public int PlayerCount
        {
            get
            {
                return players.Count;
            }
        }

        private void Start()
        {
            if (!EnsureClient())
            {
                return;
            }

            if (!Camera)
            {
                Camera = UnityEngine.Camera.main;
            }

            if (Client.Character == null)
            {
                Run(() => GameClient.LoadScene("CharacterSelectScene"));
                return;
            }
            map = Instantiate(Client.WorldBundle.LoadAsset<GameObject>("MapRoot")).GetComponent<MapView>();
            map.Load((JObject)Client.Character["map"]);
            playerPrefab = Client.WorldBundle.LoadAsset<GameObject>("Player");
            remotePrefab = Client.WorldBundle.LoadAsset<GameObject>("RemotePlayer");
            npcPrefab = Client.WorldBundle.LoadAsset<GameObject>("Npc");
            InitWorld();
            InitProgression();
            InventoryButton.onClick.AddListener(() => OpenInventory(false));
            StatsButton.onClick.AddListener(() => OpenInventory(true));
            AttackButton.onClick.AddListener(() => Run(Attack));
            Logout.onClick.AddListener(() => Run(Client.Logout));
            ChangeCharacter.onClick.AddListener(() => Run(LeaveCharacter));
            ConfirmTalent.onClick.AddListener(() => Run(CompleteTalent));
            MenuButton.onClick.AddListener(ToggleMenu);
            ResumeButton.onClick.AddListener(ToggleMenu);
            talentConfirmed = Client.Character.Value<bool>("talentSeen");
            Client.Changed += Render;
            ready = true;
            Render();
            if (!talentConfirmed)
            {
                Run(RevealTalent);
            }
        }

        private async Task LeaveCharacter()
        {
            await Client.Request("character.leave");
            Client.ClearCharacter();
            await GameClient.LoadScene("CharacterSelectScene");
        }

        private async Task CompleteTalent()
        {
            await Client.Request("talent.complete");
            talentConfirmed = true;
            TalentPanel.SetActive(false);
        }

        public void OpenInventory(bool stats)
        {
            CloseProgression();
            MenuPanel.SetActive(false);
            keyboardMoving = false;
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            if (self != null)
            {
                RunMove(self.Value<float>("x"), self.Value<float>("y"));
            }

            Inventory.gameObject.SetActive(true);
            Inventory.ShowStats(stats);
        }

        private void ToggleMenu()
        {
            MenuPanel.SetActive(!MenuPanel.activeSelf);
            keyboardMoving = false;
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            if (self != null)
            {
                RunMove(self.Value<float>("x"), self.Value<float>("y"));
            }
        }

        private async Task RevealTalent()
        {
            Beam.SetActive(true);
            Announcement.text = "Thiên Đạo đang định đoạt Thiên Phú Tiên Thiên...";
            await Task.Delay(800);
            if (!this)
            {
                return;
            }

            Beam.SetActive(false);
            Announcement.text = string.Empty;
            TalentText.text = "Huyết Mạch: " + Client.Character["race"].Value<string>("name") +
                "\nLinh Căn: " + Client.Character["element"].Value<string>("name") + "\n\nThiên Phú Tộc\n" + RaceDescription();
            TalentPanel.SetActive(true);
        }

        private string RaceDescription()
        {
            string[] names = { "Khí Huyết", "Chân Lực", "Pháp Lực", "Linh Giác", "Sát Cơ" };
            string[] keys = { "khiHuyetPercent", "chanLucPercent", "phapLucPercent", "linhGiacPercent", "satCoPercent" };
            var lines = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                decimal value = Client.Character["race"].Value<decimal>(keys[i]);
                if (value != 0)
                {
                    lines.Add(names[i] + " " + (value > 0 ? "+" : "") + value + "%");
                }
            }
            return string.Join("\n", lines);
        }

        private void Render()
        {
            if (!ready || Client.Character == null)
            {
                return;
            }

            var character = Client.Character;
            RefreshWorld();
            Name.text = character.Value<string>("name");
            Realm.text = RealmName(character.Value<int>("realmId")) + " · " + character["className"];
            var info = character["stats"];
            HpText.text = "HP  " + info["hp"] + " / " + info["hpFull"];
            MpText.text = "MP  " + info["mp"] + " / " + info["mpFull"];
            HpFill.fillAmount = info.Value<long>("hp") / (float)Math.Max(1, info.Value<long>("hpFull"));
            MpFill.fillAmount = info.Value<long>("mp") / (float)Math.Max(1, info.Value<long>("mpFull"));
            Location.text = character["map"].Value<string>("name") + "\nKhu " + character["zoneId"] + " · " + Client.Players.Count + " đạo hữu";
            if (!string.IsNullOrEmpty(Client.Error))
            {
                ErrorText.text = Client.Error;
                connectionError = true;
            }
            else if (connectionError)
            {
                ErrorText.text = string.Empty;
                connectionError = false;
            }
            UpdateViews(Client.Players, players, false);
            UpdateViews(Client.Npcs, npcs, true);
            RenderWorld();
            RenderProgression();
            foreach (JObject attack in Client.Attacks)
            {
                if (!shownAttacks.Add(attack.Value<long>("id")))
                {
                    continue;
                }

                if (shownAttacks.Count > 512)
                {
                    shownAttacks.Clear();
                }

                var targets = attack.Value<string>("targetType") == "monster" ? monsters : players;
                if (targets.TryGetValue(attack.Value<int>("targetId"), out var target))
                {
                    var effect = Instantiate(FloatingPrefab, target.transform.position + Vector3.up * 2.2f, Quaternion.identity);
                    effect.Text.text = attack.Value<bool>("isDodge") ? "Né" : (attack.Value<bool>("isCrit") ? "Chí mạng -" : "-") + attack["damage"];
                    effect.Text.color = attack.Value<bool>("isDodge") ? Color.cyan : attack.Value<bool>("isCrit") ? new Color(1, 0.75f, 0.2f) : Color.white;
                }
            }
        }

        private void UpdateViews(JArray values, Dictionary<int, CharacterView> views, bool npc)
        {
            var ids = new HashSet<int>();
            int own = Client.Character.Value<int>("id");
            foreach (JObject value in values)
            {
                int id = value.Value<int>("id");
                ids.Add(id);
                if (!views.TryGetValue(id, out var view))
                {
                    var prefab = npc ? npcPrefab : id == own ? playerPrefab : remotePrefab;
                    view = Instantiate(prefab, map.transform).GetComponent<CharacterView>();
                    views.Add(id, view);
                }
                view.SetData(value, id == own && !npc, id == targetId && !npc && !targetIsMonster, npc);
            }
            foreach (int id in views.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }

        private void Update()
        {
            if (!ready || Client.Character == null)
            {
                return;
            }

            long now = Client.ServerTime + (long)((Time.unscaledTime - Client.ServerFrameTime) * 1000);
            UpdateEffects(now);
            UpdateWorld(now);
            UpdateProgression(now);
            Connection.text = Client.Connected ? "Đã kết nối" : "Mất kết nối";
            InventoryButton.interactable = Client.Connected && talentConfirmed && !Busy;
            StatsButton.interactable = InventoryButton.interactable;
            MenuButton.interactable = talentConfirmed && !Busy;
            ConfirmTalent.interactable = !Busy;
            bool blocked = Inventory.gameObject.activeSelf || MenuPanel.activeSelf || NpcPanel.activeSelf || ZonePanel.activeSelf || DeathPanel.activeSelf || ProgressionPanel.activeSelf || !talentConfirmed;
            if (talentConfirmed && Input.GetKeyDown(KeyCode.Escape))
            {
                if (ProgressionPanel.activeSelf)
                {
                    CloseProgression();
                }
                else if (Inventory.gameObject.activeSelf)
                {
                    Inventory.gameObject.SetActive(false);
                }
                else if (NpcPanel.activeSelf || ZonePanel.activeSelf)
                {
                    NpcPanel.SetActive(false);
                    ZonePanel.SetActive(false);
                }
                else
                {
                    ToggleMenu();
                }
                return;
            }

            if (!blocked && (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.C)))
            {
                OpenInventory(Input.GetKeyDown(KeyCode.C));
                return;
            }

            bool alive = Client.Character["game"].Value<long>("hp") > 0;
            bool allowed = Client.Connected && !blocked && alive && !Busy && !TrialActive;
            var target = (targetIsMonster ? Client.Monsters : Client.Players).OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == targetId);
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            bool inRange = target != null && self != null && Vector2.Distance(new Vector2(self.Value<float>("x"), self.Value<float>("y")),
                new Vector2(target.Value<float>("x"), target.Value<float>("y"))) <= Client.Character.Value<int>("attackRange");
            long cooldown = Math.Max(0, Client.Character.Value<int>("attackCooldown") - (now - Client.Character["game"].Value<long>("lastAttackTime")));
            Target.text = target == null ? "Chưa chọn mục tiêu" : target["name"] + "\nHP " + target["hp"] + " / " + target["hpMax"];
            Target.transform.parent.gameObject.SetActive(target != null);
            TargetHpFill.fillAmount = target == null ? 0 : target.Value<long>("hp") / (float)Math.Max(1, target.Value<long>("hpMax"));
            AttackButton.interactable = allowed && target != null && target.Value<long>("hp") > 0 && inRange && cooldown == 0;
            AttackLabel.text = cooldown > 0 ? "Hồi đòn " + (cooldown / 1000f).ToString("0.0") + "s" : "Đánh [Space]";
            if (!alive)
            {
                CombatHint.text = "Nhân vật đã hết sinh lực.";
            }
            else if (target == null)
            {
                CombatHint.text = "Tab hoặc chạm người chơi để chọn";
            }
            else if (target.Value<long>("hp") == 0)
            {
                CombatHint.text = "Mục tiêu đã hết sinh lực.";
            }
            else
            {
                CombatHint.text = inRange ? "Trong tầm đánh" : "Di chuyển đến gần mục tiêu";
            }
            if (allowed)
            {
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    SelectNextTarget();
                }

                if (Input.GetKeyDown(KeyCode.Space) && AttackButton.interactable)
                {
                    Run(Attack);
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    Run(Interact);
                }

                ReadMovement();
            }
            int id = Client.Character.Value<int>("id");
            if (players.TryGetValue(id, out var player))
            {
                float halfHeight = Camera.orthographicSize;
                float halfWidth = halfHeight * Camera.aspect;
                float x = map.Width > halfWidth * 2 ? Mathf.Clamp(player.transform.position.x, halfWidth, map.Width - halfWidth) : map.Width / 2;
                float y = map.Height > halfHeight * 2 ? Mathf.Clamp(player.transform.position.y, -map.Height + halfHeight, -halfHeight) : -map.Height / 2;
                Camera.transform.position = Vector3.Lerp(Camera.transform.position, new Vector3(x, y, -10), Time.deltaTime * 10);
            }
        }

        private void UpdateEffects(long now)
        {
            int index = 0;
            foreach (JObject effect in Client.Character["game"]["effects"])
            {
                if (effect.Value<long>("expireTime") <= now || index >= EffectSlots.Length)
                {
                    continue;
                }

                long seconds = Math.Max(0, (effect.Value<long>("expireTime") - now + 999) / 1000);
                var template = Client.Catalog["items"].OfType<JObject>().First(value => value.Value<int>("id") == effect.Value<int>("itemId"));
                var slot = EffectSlots[index++];
                slot.gameObject.SetActive(true);
                slot.SetData(template.Value<string>("name").Replace(template.Value<string>("subName") + " ", ""), seconds);
            }

            while (index < EffectSlots.Length)
            {
                EffectSlots[index++].gameObject.SetActive(false);
            }
            Effects.text = string.Empty;
        }

        private void ReadMovement()
        {
            Vector2 direction = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + Joystick.Direction, 1);
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            if (self == null)
            {
                return;
            }

            if (direction.sqrMagnitude > 0.01f && Time.unscaledTime >= nextMove)
            {
                keyboardMoving = true;
                nextMove = Time.unscaledTime + 0.1f;
                RunMove(self.Value<float>("x") + direction.x * 120, self.Value<float>("y") - direction.y * 120);
            }
            else if (direction.sqrMagnitude < 0.01f && keyboardMoving)
            {
                keyboardMoving = false;
                RunMove(self.Value<float>("x"), self.Value<float>("y"));
            }
            if (!Input.GetMouseButtonDown(0) || EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var point = Camera.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(point);
            var selected = hit ? hit.GetComponent<CharacterView>() : null;
            if (selected && selected.IsNpc)
            {
                Run(() => TalkNpc(selected.Id));
            }
            else if (selected && selected.IsMonster)
            {
                SelectMonster(selected.Id);
            }
            else if (selected && selected.Id != Client.Character.Value<int>("id"))
            {
                targetId = selected.Id;
                targetIsMonster = false;
                Render();
            }
            else if (!selected)
            {
                var portal = hit ? hit.GetComponent<WayPointView>() : null;
                var item = hit ? hit.GetComponent<ItemMapView>() : null;
                if (portal)
                {
                    Run(() => ChangeMap(portal.MapId));
                }
                else if (item)
                {
                    Run(() => PickItem(item.Id));
                }
                else
                {
                    RunMove(point.x * 100, -point.y * 100);
                }
            }
        }

        private void RunMove(float x, float y)
        {
            _ = Move(x, y);
        }

        private async Task Move(float x, float y)
        {
            try
            {
                await Client.Move(x, y);
            }
            catch (Exception error)
            {
                if (this)
                {
                    ErrorText.text = error.Message;
                }
            }
        }

        private void SelectNext()
        {
            var ids = Client.Players.OfType<JObject>().Where(value => value.Value<int>("id") != Client.Character.Value<int>("id")).Select(value => value.Value<int>("id")).ToArray();
            if (ids.Length == 0)
            {
                return;
            }

            int index = Array.IndexOf(ids, targetId);
            targetId = ids[(index + 1) % ids.Length];
            Render();
        }

        public void SelectTarget(int id)
        {
            targetId = id;
            targetIsMonster = false;
            Render();
        }

        private async Task Attack()
        {
            if (targetIsMonster)
            {
                var monster = Client.Monsters.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == targetId);
                if (monster != null)
                {
                    await Client.Request("monster.attack", new
                    {
                        targetId,
                        lifeId = monster.Value<string>("lifeId"),
                        playToken = Client.Character.Value<string>("playToken")
                    });
                }
                return;
            }
            await Client.Request("attack", new
            {
                targetId,
                playToken = Client.Character.Value<string>("playToken")
            });
        }

        private void OnDestroy()
        {
            if (Client != null)
            {
                Client.Changed -= Render;
            }

            if (map)
            {
                Destroy(map.gameObject);
            }
        }
    }
}
