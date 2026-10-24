using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TuTien.Client
{
    public class DemoSmoke : MonoBehaviour
    {
        private string output;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-tutienSmoke"))
            {
                yield break;
            }

            int index = Array.IndexOf(args, "-smokeOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "smoke");
            Directory.CreateDirectory(output);
            Task task = Run();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                File.WriteAllText(Path.Combine(output, "result.json"), new JObject { ["success"] = false, ["message"] = task.Exception.GetBaseException().Message }.ToString());
                Application.Quit(1);
            }
            else
            {
                File.WriteAllText(Path.Combine(output, "result.json"), new JObject { ["success"] = true, ["checks"] = "CDN/hash/bundle, auth/scenes/three slots/talent, inventory/stats/pills, viewport 16:9/4:3/wide, TCP combat and durable login, milestone 1 NPC/map/zone/PvE/drop/death/revive, milestone 2 task chain/rewards/learn/cast/slots/trial/realm/durable login" }.ToString());
                Application.Quit(0);
            }
        }

        private static async Task Wait(Func<bool> condition, string name, float timeout = 25)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                {
                    throw new Exception("Timeout: " + name);
                }

                await Task.Delay(50);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private void Capture(string name, int width = 1600, int height = 900)
        {
            Camera camera = Camera.main;
            var target = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            CheckLayout();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = previous;
            RenderTexture.active = active;
            target.Release();
            Destroy(target);
            Destroy(texture);
        }

        private static void CheckLayout()
        {
            var canvas = FindObjectsOfType<Canvas>().First(value => value.isRootCanvas).GetComponent<RectTransform>();
            var corners = new Vector3[4];
            foreach (var button in FindObjectsOfType<UnityEngine.UI.Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = canvas.InverseTransformPoint(corner);
                    Check(point.x >= canvas.rect.xMin - 1 && point.x <= canvas.rect.xMax + 1 &&
                        point.y >= canvas.rect.yMin - 1 && point.y <= canvas.rect.yMax + 1, "Button outside viewport: " + button.name);
                }
            }
        }

        private async Task Run()
        {
            await Wait(() => FindObjectOfType<LoadingScene>() && !FindObjectOfType<LoadingScene>().Busy && FindObjectOfType<LoadingScene>().DownloadButton.interactable, "prepare");
            Capture("loading");
            FindObjectOfType<LoadingScene>().DownloadButton.onClick.Invoke();
            await Wait(() => SceneManager.GetActiveScene().name == "LoginScene" && FindObjectOfType<LoginScene>(), "download/login", 60);
            await Task.Delay(200);
            Capture("login");
            Capture("login-4x3", 1280, 960);
            string unique = DateTime.UtcNow.Ticks.ToString("x");
            string username = "unity_" + unique;
            const string password = "UnityDemo123!";
            var login = FindObjectOfType<LoginScene>();
            login.Register.onClick.Invoke();
            login.Username.text = username;
            login.Password.text = password;
            login.ShowPassword.onClick.Invoke();
            Check(login.Password.contentType == TMPro.TMP_InputField.ContentType.Standard, "Show password did not toggle");
            login.ShowPassword.onClick.Invoke();
            login.ConfirmPassword.text = "DifferentPassword";
            login.Submit.onClick.Invoke();
            await Task.Delay(100);
            Check(FindObjectOfType<LoginScene>() && !string.IsNullOrEmpty(login.ErrorText.text), "Register confirmation did not validate");
            Capture("register-validation");
            login.ConfirmPassword.text = password;
            login.Submit.onClick.Invoke();
            await Wait(() => FindObjectOfType<CharacterSelectScene>() && !FindObjectOfType<CharacterSelectScene>().Busy, "slots");
            Check(FindObjectOfType<CharacterSelectScene>().Slots.Length == 3, "Three character slots missing");
            Capture("select");
            FindObjectOfType<CharacterSelectScene>().Slots[0].onClick.Invoke();
            await Wait(() => FindObjectOfType<CharacterCreateScene>(), "create scene");
            await Task.Delay(200);
            var create = FindObjectOfType<CharacterCreateScene>();
            create.Gender.value = 1;
            Check(create.Appearance.options.Count > 0, "Female appearances missing");
            Check(create.Figure.Description.text.Contains("Nữ"), "Female preview did not change");
            create.Gender.value = 0;
            Check(create.Appearance.value == 0, "Appearance did not reset");
            create.Name.text = "Unity " + unique.Substring(unique.Length - 8);
            Capture("create");
            create.Appearance.Show();
            await Task.Delay(100);
            Capture("appearance-options");
            var dropdownList = create.Appearance.transform.Find("Dropdown List");
            foreach (var label in dropdownList.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate();
                Check(!label.isTextOverflowing, "Dropdown option is clipped: " + label.text);
            }
            create.Appearance.Hide();
            create.Create.onClick.Invoke();
            await Wait(() => FindObjectOfType<CharacterSelectScene>() && !FindObjectOfType<CharacterSelectScene>().Busy, "created slots");
            Capture("select-created");
            FindObjectOfType<CharacterSelectScene>().Slots[0].onClick.Invoke();
            await Wait(() => FindObjectOfType<WorldScene>() && FindObjectOfType<WorldScene>().TalentPanel.activeSelf, "talent");
            var world = FindObjectOfType<WorldScene>();
            Capture("talent");
            world.ConfirmTalent.onClick.Invoke();
            await Wait(() => !world.TalentPanel.activeSelf && !world.Busy, "talent confirm");
            Check(string.IsNullOrEmpty(GameClient.Instance.Error), "Unexpected connection error after entering world");
            var client = GameClient.Instance;
            world.OpenInventory(false);
            var inventory = world.Inventory;
            Check(inventory.Bag.Length == 40 && inventory.Body.Length == 6, "Inventory slot counts incorrect");
            JObject weapon = client.Character["game"]["itemBag"].OfType<JObject>().First(item => item.Value<int>("id") == 1101);
            inventory.Bag[weapon.Value<int>("indexUI")].Button.onClick.Invoke();
            inventory.Use.onClick.Invoke();
            await Wait(() => client.Character["game"]["itemBody"].Count() == 1 && !inventory.Busy, "equip");
            inventory.ShowStats(true);
            Check(!inventory.Apply.interactable, "Unchanged potential should not be saved");
            inventory.Stats[1].Plus.onClick.Invoke();
            Check(inventory.Apply.interactable && inventory.Pending.text.Contains("chưa lưu"), "Draft points feedback missing");
            inventory.Cancel.onClick.Invoke();
            Check(!inventory.Apply.interactable, "Cancel did not reset draft points");
            for (int i = 0; i < 40; i++)
            {
                inventory.Stats[1].Plus.onClick.Invoke();
            }

            for (int i = 0; i < 3; i++)
            {
                inventory.Stats[1].Minus.onClick.Invoke();
            }

            inventory.Apply.onClick.Invoke();
            await Wait(() => client.Character["game"]["points"][1].Value<int>() == 37 && !inventory.Busy, "potential");
            Capture("stats");
            Capture("stats-4x3", 1280, 960);
            inventory.ShowStats(false);
            JObject pill = client.Character["game"]["itemBag"].OfType<JObject>().First(item => item.Value<int>("id") == 3010);
            inventory.Bag[pill.Value<int>("indexUI")].Button.onClick.Invoke();
            inventory.Use.onClick.Invoke();
            await Wait(() => client.Character["game"]["effects"].Count() == 1 && !inventory.Busy, "pill");
            Check(!string.IsNullOrEmpty(inventory.Detail.text) && !string.IsNullOrEmpty(inventory.ItemName.text), "Item details missing");
            Capture("inventory");
            Capture("inventory-wide", 1920, 810);
            Check(!inventory.Bag[pill.Value<int>("indexUI")].Quantity.isTextOverflowing, "Item quantity is clipped");
            inventory.Close.onClick.Invoke();
            await Wait(() => world.EffectSlots.Any(value => value.gameObject.activeSelf && value.Time.text.Contains(":")), "pill HUD timer");
            Check(!world.EffectSlots.First(value => value.gameObject.activeSelf).Time.isTextOverflowing, "Pill timer is clipped");
            world.MenuButton.onClick.Invoke();
            Check(world.MenuPanel.activeSelf, "Game menu did not open");
            Capture("menu");
            world.ResumeButton.onClick.Invoke();
            Check(!world.MenuPanel.activeSelf, "Game menu did not close");
            JObject self = client.Players.OfType<JObject>().First(value => value.Value<int>("id") == client.Character.Value<int>("id"));
            float x = self.Value<float>("x");
            await client.Move(x + 50, self.Value<float>("y"));
            await Wait(() => client.Players.OfType<JObject>().First(value => value.Value<int>("id") == client.Character.Value<int>("id")).Value<float>("x") > x + 30, "movement");
            using (var peer = new Session_ME())
            {
                await peer.Connect(client.Settings.Host, client.Settings.Port);
                await peer.SendMessage("register", new
                {
                    username = "peer_" + unique,
                    password
                });
                JObject created = await peer.SendMessage("character.create", new
                {
                    slot = 1,
                    name = "Peer " + unique.Substring(unique.Length - 8),
                    classId = 1,
                    gender = 0,
                    appearanceId = 101
                });
                int targetId = created["data"].Value<int>("id");
                await peer.SendMessage("character.enter", new
                {
                    id = targetId
                });
                await peer.SendMessage("talent.complete");
                await Wait(() => world.PlayerCount >= 2 && client.Players.OfType<JObject>().Any(value => value.Value<int>("id") == targetId), "multiplayer");
                world.SelectTarget(targetId);
                world.AttackButton.onClick.Invoke();
                await Wait(() => client.Attacks.OfType<JObject>().Any(value => value.Value<int>("attackerId") == client.Character.Value<int>("id")), "combat");
                JObject attack = client.Attacks.OfType<JObject>().Last(value => value.Value<int>("attackerId") == client.Character.Value<int>("id"));
                JObject victim = (await peer.SendMessage("characters"))["data"]["slots"][0]["character"] as JObject;
                long expected = attack.Value<bool>("isDodge") ? 0 : Math.Max(0, client.Character["stats"].Value<long>("damageFull") - victim["stats"].Value<long>("defenceFull")) * (attack.Value<bool>("isCrit") ? 2 : 1);
                Check(attack.Value<long>("damage") == expected, "Combat formula differs from server stats");
                await Task.Delay(150);
                Capture("world");
                Capture("world-4x3", 1280, 960);
            }
            string uid = client.Character["game"]["itemBody"][0].Value<string>("uid");
            await client.Logout();
            await Wait(() => FindObjectOfType<LoginScene>(), "logout");
            login = FindObjectOfType<LoginScene>();
            login.Username.text = username;
            login.Password.text = password;
            login.Submit.onClick.Invoke();
            await Wait(() => FindObjectOfType<CharacterSelectScene>() && !FindObjectOfType<CharacterSelectScene>().Busy, "login again");
            FindObjectOfType<CharacterSelectScene>().Slots[0].onClick.Invoke();
            await Wait(() => FindObjectOfType<WorldScene>() && FindObjectOfType<WorldScene>().PlayerCount > 0, "restore world");
            Check(client.Character["game"]["itemBody"][0].Value<string>("uid") == uid, "Equipment was not saved");
            Check(client.Character["game"]["points"][1].Value<int>() == 37, "Potential was not saved");
            Check(client.Character.Value<bool>("talentSeen"), "Talent confirmation was not saved");
            await RunWorld();
            await CheckRewardList();
            await RunProgression(username, password);
        }

        private async Task Walk(float x, float y)
        {
            var client = GameClient.Instance;
            await client.Move(x, y);
            try
            {
                await Wait(() =>
                {
                    var self = client.Players.OfType<JObject>().First(value => value.Value<int>("id") == client.Character.Value<int>("id"));
                    return Vector2.Distance(new Vector2(self.Value<float>("x"), self.Value<float>("y")), new Vector2(x, y)) < 90;
                }, "world walk");
            }
            catch (Exception)
            {
                Capture("walk-failure");
                var self = client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == client.Character.Value<int>("id"));
                throw new Exception("Walk to " + x + "," + y + " failed; map " + client.Character["mapId"] +
                    "; position " + self?["x"] + "," + self?["y"] + "; HP " + client.Character["game"]["hp"] +
                    "; axes " + Input.GetAxisRaw("Horizontal") + "," + Input.GetAxisRaw("Vertical") +
                    "; joystick " + FindObjectOfType<WorldScene>().Joystick.Direction + "; " + FindObjectOfType<WorldScene>().ErrorText.text);
            }
        }

        private async Task RunWorld()
        {
            var client = GameClient.Instance;
            var world = FindObjectOfType<WorldScene>();
            await Task.Delay(250);
            await Walk(550, 600);
            await world.TalkNpc(101);
            Check(world.NpcPanel.activeSelf && !string.IsNullOrEmpty(world.NpcText.text), "NPC dialog missing");
            Capture("npc");
            world.NpcClose.onClick.Invoke();
            world.ZoneButton.onClick.Invoke();
            await Wait(() => world.ZonePanel.activeSelf && !world.Busy, "zone picker");
            Check(world.ZoneList.options.Count == 4, "Zone list missing");
            Capture("zones");
            world.ZoneClose.onClick.Invoke();
            await Walk(1440, 600);
            Capture("waypoint");
            await world.ChangeMap(2);
            await Wait(() => client.Character.Value<int>("mapId") == 2 && client.Monsters.Count == 3, "training map");
            world = FindObjectOfType<WorldScene>();
            await Task.Delay(1100);
            world.ZoneButton.onClick.Invoke();
            await Wait(() => world.ZonePanel.activeSelf && !world.Busy, "isolated training zone");
            world.ZoneList.value = 2;
            world.ZoneConfirm.onClick.Invoke();
            await Wait(() => client.Character.Value<int>("zoneId") == 3 && !world.Busy, "training zone three");
            int id = client.Monsters[0].Value<int>("id");
            string life = client.Monsters[0].Value<string>("lifeId");
            world.SelectMonster(id);
            await Task.Delay(100);
            Capture("monsters");
            for (int index = 0; index < 20; index++)
            {
                var monster = client.Monsters.OfType<JObject>().First(value => value.Value<int>("id") == id);
                if (monster.Value<long>("hp") == 0)
                {
                    break;
                }
                if (world.AttackButton.interactable)
                {
                    world.AttackButton.onClick.Invoke();
                }
                else
                {
                    var self = client.Players.OfType<JObject>().First(value => value.Value<int>("id") == client.Character.Value<int>("id"));
                    if (Vector2.Distance(new Vector2(self.Value<float>("x"), self.Value<float>("y")), new Vector2(monster.Value<float>("x"), monster.Value<float>("y"))) > 110)
                    {
                        await client.Move(monster.Value<float>("x"), monster.Value<float>("y"));
                    }
                }
                await Task.Delay(850);
            }
            await Wait(() => client.Items.OfType<JObject>().Any(value => value.Value<int>("playerId") == client.Character.Value<int>("id")), "monster drop");
            long tuVi = client.Character["game"].Value<long>("tuVi");
            Check(tuVi > 0, "Tu vi reward missing");
            Capture("drop");
            var item = client.Items.OfType<JObject>().First(value => value.Value<int>("playerId") == client.Character.Value<int>("id"));
            await Walk(item.Value<float>("x"), item.Value<float>("y"));
            await world.PickItem(item.Value<string>("id"));
            Capture("pickup");
            world.ZoneButton.onClick.Invoke();
            await Wait(() => world.ZonePanel.activeSelf && !world.Busy, "change training zone");
            world.ZoneList.value = 3;
            world.ZoneConfirm.onClick.Invoke();
            await Wait(() => client.Character.Value<int>("zoneId") == 4 && !world.Busy, "zone changed");
            await Wait(() => client.Character["game"].Value<long>("hp") == 0 && world.DeathPanel.activeSelf, "monster death", 60);
            Capture("death");
            await Wait(() => world.ReviveButton.interactable, "revive ready");
            world.ReviveButton.onClick.Invoke();
            await Wait(() => client.Character.Value<int>("mapId") == 1 && client.Character["game"].Value<long>("hp") > 0 && !world.Busy, "revived village");
            Check(client.Character["game"].Value<long>("tuVi") == tuVi, "Revive lost tu vi");
            Capture("revive");
        }

        private async Task CheckRewardList()
        {
            var client = GameClient.Instance;
            var world = FindObjectOfType<WorldScene>();
            var task = (JObject)client.Catalog["tasks"][0];
            var original = task["reward"].DeepClone();
            try
            {
                task["reward"] = new JArray(Enumerable.Range(0, 20).Select(index => original[0].DeepClone()));
                world.ShowProgression(0);
                world.TaskList.value = 0;
                await Task.Delay(150);
                Canvas.ForceUpdateCanvases();
                var scroll = world.TaskDetail.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                Check(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height, "Reward list is not scrollable");
                scroll.verticalNormalizedPosition = 0;
                Capture("reward-list");
            }
            finally
            {
                task["reward"] = original;
                world.ProgressionClose.onClick.Invoke();
            }
        }

        private async Task TaskAction(WorldScene world, int index, bool claim)
        {
            world.ShowProgression(0);
            world.TaskList.value = index;
            await Wait(() => world.TaskAction.interactable, "task action " + (index + 1));
            world.TaskAction.onClick.Invoke();
            await Wait(() => !world.Busy && (claim ? GameClient.Instance.Character["infoTask"]["completed"].Values<int>().Contains(index + 1) :
                (GameClient.Instance.Character["infoTask"]["mainTask"] as JObject)?.Value<int>("id") == index + 1), "task saved " + (index + 1));
            Capture(claim ? "task-reward" : "task-active");
            world.ProgressionClose.onClick.Invoke();
        }

        private async Task Travel(int mapId)
        {
            var client = GameClient.Instance;
            var portal = client.Character["map"]["waypoint"].OfType<JObject>().First(value => value.Value<int>("mapId") == mapId);
            await Walk(portal.Value<float>("x"), portal.Value<float>("y"));
            await FindObjectOfType<WorldScene>().ChangeMap(mapId);
            await Wait(() => client.Character.Value<int>("mapId") == mapId, "progression map");
            await Task.Delay(1100);
        }

        private async Task FightTask(bool skill)
        {
            var client = GameClient.Instance;
            var world = FindObjectOfType<WorldScene>();
            for (int tries = 0; tries < 90 && client.Character["infoTask"]["mainTask"].Value<int>("count") < 2; tries++)
            {
                var target = client.Monsters.OfType<JObject>().FirstOrDefault(value => value.Value<long>("hp") > 0);
                if (target == null)
                {
                    await Task.Delay(500);
                    continue;
                }
                if (client.Character["game"].Value<long>("hp") < client.Character["stats"].Value<long>("hpFull") * 0.65)
                {
                    await HealProgression(false);
                }
                world.SelectMonster(target.Value<int>("id"));
                var self = client.Players.OfType<JObject>().First(value => value.Value<int>("id") == client.Character.Value<int>("id"));
                if (Vector2.Distance(new Vector2(self.Value<float>("x"), self.Value<float>("y")), new Vector2(target.Value<float>("x"), target.Value<float>("y"))) > 110)
                {
                    await client.Move(target.Value<float>("x"), target.Value<float>("y"));
                }
                else if (skill ? world.SkillButtons[0].interactable : world.AttackButton.interactable)
                {
                    (skill ? world.SkillButtons[0] : world.AttackButton).onClick.Invoke();
                }
                await Task.Delay(850);
                Check(client.Character["game"].Value<long>("hp") > 0, "Quest combat died");
            }
            Check(client.Character["infoTask"]["mainTask"].Value<int>("count") == 2, "Quest combat did not progress");
            await HealProgression(true);
            Capture(skill ? "skill-cast" : "task-kills");
        }

        private async Task RunProgression(string username, string password)
        {
            var client = GameClient.Instance;
            var world = FindObjectOfType<WorldScene>();
            foreach (var equipment in client.Character["game"]["itemBag"].OfType<JObject>().Where(value => value.Value<int>("id") >= 1100 && value.Value<int>("id") < 3000).ToArray())
            {
                world.OpenInventory(false);
                world.Inventory.Bag[equipment.Value<int>("indexUI")].Button.onClick.Invoke();
                long revision = client.Character.Value<long>("gameRevision");
                world.Inventory.Use.onClick.Invoke();
                await Wait(() => client.Character.Value<long>("gameRevision") > revision && !world.Inventory.Busy, "prepare equipment");
                world.Inventory.Close.onClick.Invoke();
            }
            await HealProgression(true);
            await Walk(550, 600);
            await TaskAction(world, 0, false);
            var npc = client.Npcs.OfType<JObject>().First(value => value.Value<int>("id") == 102);
            await Walk(npc.Value<float>("x"), npc.Value<float>("y"));
            await world.TalkNpc(102);
            world.NpcClose.onClick.Invoke();
            await Walk(550, 600);
            await TaskAction(world, 0, true);
            await TaskAction(world, 1, false);
            await Travel(2);
            await FightTask(false);
            await Travel(1);
            await Walk(550, 600);
            await TaskAction(world, 1, true);
            await TaskAction(world, 2, false);
            npc = client.Npcs.OfType<JObject>().First(value => value.Value<int>("id") == 102);
            await Walk(npc.Value<float>("x"), npc.Value<float>("y"));
            world.ShowProgression(1);
            await Wait(() => world.LearnButton.interactable, "learn ready");
            world.LearnButton.onClick.Invoke();
            await Wait(() => client.Character["skills"].Count() == 1 && !world.Busy, "learn saved");
            Capture("skills");
            Capture("skills-4x3", 1280, 960);
            world.ProgressionClose.onClick.Invoke();
            await Walk(550, 600);
            await TaskAction(world, 2, true);
            await TaskAction(world, 3, false);
            await HealProgression(true);
            await Travel(2);
            await FightTask(true);
            await Travel(1);
            await Walk(550, 600);
            await TaskAction(world, 3, true);
            await HealProgression(true);
            int points = client.Character["game"].Value<int>("pointBudget");
            world.ShowProgression(2);
            await Wait(() => world.AdvanceButton.interactable, "trial ready");
            Capture("cultivation");
            world.AdvanceButton.onClick.Invoke();
            await Wait(() => client.Character["game"]["breakthrough"].Value<bool>("active") && !world.Busy, "trial start");
            world.RealmDetail.ForceMeshUpdate();
            Check(!world.RealmDetail.isTextOverflowing, "Active trial instructions are clipped");
            Capture("trial-dialog");
            world.ProgressionClose.onClick.Invoke();
            await Wait(() => client.Character["game"]["breakthrough"].Value<int>("strikeCount") >= 1, "trial first strike");
            Capture("trial");
            await Wait(() => client.Character.Value<int>("realmId") == 2, "trial success");
            Check(client.Character["game"].Value<int>("pointBudget") == points + 10, "Realm point reward missing");
            world.ShowProgression(2);
            Check(!world.Beam.activeSelf, "Trial beam covers cultivation window");
            Capture("realm-success");
            Capture("realm-wide", 1920, 900);
            world.ShowProgression(1);
            await Wait(() => world.AssignButtons[2].interactable, "third skill slot");
            world.AssignButtons[2].onClick.Invoke();
            await Wait(() => client.Character["game"]["skillSlots"][2].Value<int>() != 0 && !world.Busy, "assign slot three");
            Check(client.Character["game"]["skillSlots"][0].Value<int>() == 0, "Duplicate skill assignment");
            int id = client.Character.Value<int>("id");
            await client.Logout();
            await Wait(() => FindObjectOfType<LoginScene>(), "progression logout");
            var login = FindObjectOfType<LoginScene>();
            login.Username.text = username;
            login.Password.text = password;
            login.Submit.onClick.Invoke();
            await Wait(() => FindObjectOfType<CharacterSelectScene>() && !FindObjectOfType<CharacterSelectScene>().Busy, "progression login");
            FindObjectOfType<CharacterSelectScene>().Slots[0].onClick.Invoke();
            await Wait(() => FindObjectOfType<WorldScene>() && FindObjectOfType<WorldScene>().PlayerCount > 0 && client.Character.Value<int>("id") == id, "progression restored");
            Check(client.Character.Value<int>("realmId") == 2 && client.Character["infoTask"]["completed"].Count() == 4 && client.Character["game"]["skillSlots"][2].Value<int>() != 0, "Progression not durable");
        }

        private async Task HealProgression(bool full)
        {
            var client = GameClient.Instance;
            var world = FindObjectOfType<WorldScene>();
            while (client.Character["game"].Value<long>("hp") < client.Character["stats"].Value<long>("hpFull") * (full ? 1 : 0.65))
            {
                world.OpenInventory(false);
                var pill = client.Character["game"]["itemBag"].OfType<JObject>().First(value => value.Value<int>("id") == 3010);
                world.Inventory.Bag[pill.Value<int>("indexUI")].Button.onClick.Invoke();
                long revision = client.Character.Value<long>("gameRevision");
                world.Inventory.Use.onClick.Invoke();
                await Wait(() => client.Character.Value<long>("gameRevision") > revision && !world.Inventory.Busy, "heal progression");
                world.Inventory.Close.onClick.Invoke();
            }
        }
    }
}
