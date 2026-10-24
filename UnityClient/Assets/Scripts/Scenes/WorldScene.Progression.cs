using System;
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
        public GameObject ProgressionPanel;
        public GameObject[] ProgressionPages;
        public Button[] ProgressionTabs;
        public Button ProgressionClose;
        public Button[] ProgressionButtons;
        public TMP_Text QuestTracker;
        public TMP_Dropdown TaskList;
        public TMP_Text TaskDetail;
        public Button TaskAction;
        public TMP_Dropdown SkillList;
        public TMP_Text SkillDetail;
        public Button LearnButton;
        public Button[] AssignButtons;
        public TMP_Text RealmDetail;
        public Button AdvanceButton;
        public Button CancelTrialButton;
        public TMP_Text TrialText;
        public TMP_Text ProgressionError;
        public Button[] SkillButtons;
        public Button NpcTasks;
        public Button NpcSkills;
        private JObject[] taskTemplates;
        private JObject[] skillTemplates;
        private int lastStrike;
        private float strikeUntil;
        private bool TrialActive => Client.Character["game"]["breakthrough"].Value<bool>("active");

        private void InitProgression()
        {
            taskTemplates = Client.Catalog["tasks"].OfType<JObject>().OrderBy(value => value.Value<int>("id")).ToArray();
            skillTemplates = Client.Catalog["skills"].OfType<JObject>()
                .Where(value => value.Value<bool>("isImplemented") && value.Value<int>("classId") == Client.Character.Value<int>("classId")).ToArray();
            TaskList.ClearOptions();
            TaskList.AddOptions(taskTemplates.Select(value => value.Value<string>("name")).ToList());
            SkillList.ClearOptions();
            SkillList.AddOptions(new[] { "Bỏ kỹ năng khỏi ô" }.Concat(skillTemplates.Select(value => value.Value<string>("name"))).ToList());
            SkillList.value = skillTemplates.Length > 0 ? 1 : 0;
            TaskList.onValueChanged.AddListener(RenderTask);
            SkillList.onValueChanged.AddListener(RenderSkill);
            ProgressionClose.onClick.AddListener(CloseProgression);
            TaskAction.onClick.AddListener(() => Run(ChangeTask));
            LearnButton.onClick.AddListener(() => Run(LearnSkill));
            AdvanceButton.onClick.AddListener(() => Run(AdvanceRealm));
            CancelTrialButton.onClick.AddListener(() => Run(CancelTrial));
            NpcTasks.onClick.AddListener(OpenTasks);
            NpcSkills.onClick.AddListener(OpenSkills);
            for (int i = 0; i < 3; i++)
            {
                int page = i;
                ProgressionTabs[i].onClick.AddListener(() => ShowProgression(page));
                ProgressionButtons[i].onClick.AddListener(() => ShowProgression(page));
                AssignButtons[i].onClick.AddListener(() => Run(() => AssignSkill(page)));
                SkillButtons[i].onClick.AddListener(() => Run(() => UseSkill(page)));
            }
            lastStrike = Client.Character["game"]["breakthrough"].Value<int>("strikeCount");
        }

        public void ShowProgression(int page)
        {
            PauseMovement();
            Beam.SetActive(false);
            Inventory.gameObject.SetActive(false);
            MenuPanel.SetActive(false);
            NpcPanel.SetActive(false);
            ZonePanel.SetActive(false);
            ProgressionPanel.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                ProgressionPages[i].SetActive(i == page);
                ProgressionTabs[i].interactable = i != page;
            }
            RenderProgression();
        }

        private void CloseProgression()
        {
            ProgressionPanel.SetActive(false);
        }

        private void OpenTasks()
        {
            ShowProgression(0);
        }

        private void OpenSkills()
        {
            ShowProgression(1);
        }

        private int UnlockedSlots()
        {
            int id = Client.Character["realm"].Value<int>("id");
            return Math.Max(1, Math.Min(3, Client.Catalog["realms"].OfType<JObject>().First(value => value.Value<int>("id") == id).Value<int>("skillSlots")));
        }

        private void RenderProgression()
        {
            var active = Client.Character["infoTask"]["mainTask"] as JObject;
            var task = active == null ? null : taskTemplates.FirstOrDefault(value => value.Value<int>("id") == active.Value<int>("id"));
            bool allDone = Client.Character["infoTask"]["completed"].Count() == taskTemplates.Length;
            QuestTracker.text = task == null ? (allDone ? "Đã hoàn thành nhiệm vụ tân thủ [J]" : "Nhiệm vụ: gặp Lâm Bá tại Thanh Vân Thôn [J]") :
                task["name"] + "  " + active["count"] + "/" + task["requiredCount"] + "\n" + task["description"];
            if (ProgressionPanel.activeSelf)
            {
                RenderTask(TaskList.value);
                RenderSkill(SkillList.value);
                RenderRealm();
            }
        }

        private void RenderTask(int index)
        {
            if (taskTemplates == null || index < 0 || index >= taskTemplates.Length)
            {
                return;
            }
            var task = taskTemplates[index];
            int id = task.Value<int>("id");
            var info = Client.Character["infoTask"];
            var active = info["mainTask"] as JObject;
            bool completed = info["completed"].Values<int>().Contains(id);
            bool accepted = active != null && active.Value<int>("id") == id;
            bool done = accepted && active.Value<int>("count") >= task.Value<int>("requiredCount");
            bool unlocked = task.Value<int>("previousId") == 0 || info["completed"].Values<int>().Contains(task.Value<int>("previousId"));
            string status = completed ? "Đã nhận thưởng" : accepted ? active["count"] + " / " + task["requiredCount"] : unlocked ? "Có thể nhận tại Lâm Bá" : "Hoàn thành nhiệm vụ trước để mở";
            var rewards = task["reward"].OfType<JObject>().Select(item =>
            {
                var template = Client.Catalog["items"].OfType<JObject>().First(value => value.Value<int>("id") == item.Value<int>("id"));
                return template["name"] + " x" + item["quantity"];
            });
            TaskDetail.text = task["description"] + "\n\nTiến độ: " + status + "\n\nPhần thưởng: " + task["rewardTuVi"] + " tu vi, " + task["rewardPoints"] + " điểm tiềm năng\n" + string.Join("\n", rewards);
            TaskAction.GetComponentInChildren<TMP_Text>().text = done ? "Trả nhiệm vụ" : accepted ? "Đang thực hiện" : completed ? "Đã hoàn thành" : "Nhận nhiệm vụ";
            TaskAction.interactable = !Busy && !TrialActive && NearNpc(task.Value<int>("npcId")) && !completed && (done || (active == null && unlocked));
        }

        private void RenderSkill(int index)
        {
            if (skillTemplates == null)
            {
                return;
            }
            var template = index == 0 ? null : skillTemplates[index - 1];
            var skill = template == null ? null : Client.Character["skills"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("templateId") == template.Value<int>("id"));
            int level = skill?.Value<int>("level") ?? 0;
            var next = template?["skillDataTemplates"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("point") == level + 1);
            SkillDetail.text = template == null ? "Chọn ô bên dưới để bỏ kỹ năng đang gán." : template["name"] + "   Cấp " + level + "/" + template["maxPoint"] +
                (next == null ? "\nĐã học đến cấp tối đa." : "\nCấp tiếp: công x" + (next.Value<float>("damage") / 100).ToString("0.00") +
                ", MP " + next["manaUse"] + ", hồi " + (next.Value<float>("coolDown") / 1000).ToString("0.0") + "s\nTầm " + next["dx"] +
                ", học tốn " + next["tuViCost"] + " tu vi\nYêu cầu: " + RealmName(next.Value<int>("realmId"))) + "\n\nHọc và nâng cấp tại Tiếp Dẫn Sứ (Thanh Vân Thôn).";
            LearnButton.GetComponentInChildren<TMP_Text>().text = level == 0 ? "Học kỹ năng" : "Nâng cấp";
            LearnButton.interactable = !Busy && !TrialActive && template != null && next != null && NearNpc(template.Value<int>("teacherNpcId")) &&
                Client.Character["game"].Value<long>("tuVi") >= next.Value<long>("tuViCost") && Client.Character["realm"].Value<int>("id") >= next.Value<int>("realmId");
            for (int i = 0; i < 3; i++)
            {
                int assigned = Client.Character["game"]["skillSlots"][i].Value<int>();
                var current = skillTemplates.FirstOrDefault(value => value.Value<int>("id") == assigned);
                AssignButtons[i].GetComponentInChildren<TMP_Text>().text = "Ô " + (i + 1) + "\n" + (i >= UnlockedSlots() ? "Mở ở Luyện Khí" : current?.Value<string>("name") ?? "Trống");
                AssignButtons[i].interactable = !Busy && !TrialActive && i < UnlockedSlots() && (template == null || skill != null);
            }
        }

        private string RealmName(int id)
        {
            var realm = Client.Catalog["realms"].OfType<JObject>().First(value => value.Value<int>("id") == id);
            return realm["name"] + " " + realm["stage"];
        }

        private void RenderRealm()
        {
            var current = Client.Catalog["realms"].OfType<JObject>().First(value => value.Value<int>("id") == Client.Character["realm"].Value<int>("id"));
            var next = Client.Catalog["realms"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == current.Value<int>("nextRealmId") && value.Value<bool>("isImplemented"));
            var trial = Client.Character["game"]["breakthrough"];
            string result = trial.Value<string>("result");
            RealmDetail.text = "Hiện tại: " + RealmName(current.Value<int>("id")) + "\nTu vi: " + Client.Character["game"]["tuVi"] +
                (next == null ? "\nCảnh giới tiếp theo chưa mở." : "\nTiến cấp: " + RealmName(next.Value<int>("id")) + "\nTiêu hao " + next["tuViRequired"] + " tu vi, nhận " + next["pointReward"] + " điểm tiềm năng\n" +
                (next.Value<bool>("requiresTrial") ? "Hoàn thành 4 nhiệm vụ tân thủ, vượt 3 đợt Lôi Kiếp.\nChuẩn bị đầy HP. Có thể dùng đan dược khi độ kiếp." : "Tiến cấp tại Lâm Bá khi đủ tu vi.")) +
                (result == "failed" ? "\n\nĐộ kiếp thất bại. Mất " + trial["lostTuVi"] + " tu vi." : result == "success" ? "\n\nĐộ kiếp thành công. Đã mở 3 ô kỹ năng." : "");
            if (TrialActive)
            {
                RealmDetail.text = "Đang độ kiếp: " + RealmName(trial.Value<int>("targetRealmId")) + "\nĐợt sét: " + trial["strikeCount"] + "/" + trial["damagePercent"].Count() +
                    "\nTu vi đã dành: " + trial["reservedTuVi"] + "\n\nDùng đan dược trong hành trang để hồi HP.\nNgắt kết nối vẫn giữ tiến độ.\nDừng hoặc thất bại mất " + trial["lossMin"] + "–" + trial["lossMax"] + "% tu vi đã dành.";
            }
            bool taskDone = next != null && (next.Value<int>("requiredTaskId") == 0 || Client.Character["infoTask"]["completed"].Values<int>().Contains(next.Value<int>("requiredTaskId")));
            AdvanceButton.interactable = !Busy && !TrialActive && next != null && taskDone && NearNpc(101) && Client.Character["game"].Value<long>("tuVi") >= next.Value<long>("tuViRequired");
            AdvanceButton.GetComponentInChildren<TMP_Text>().text = next != null && next.Value<bool>("requiresTrial") ? "Bắt đầu độ kiếp" : "Tiến cấp";
            CancelTrialButton.gameObject.SetActive(TrialActive);
            CancelTrialButton.interactable = !Busy;
        }

        private bool NearNpc(int id)
        {
            var npc = Client.Npcs.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == id);
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            return npc != null && self != null && Client.Character["game"].Value<long>("hp") > 0 && Vector2.Distance(
                new Vector2(npc.Value<float>("x"), npc.Value<float>("y")), new Vector2(self.Value<float>("x"), self.Value<float>("y"))) <= (Client.ServerInfo?.Value<int>("interactionRange") ?? 140);
        }

        private async Task ChangeProgression(string type, int id = 0, int slot = 0)
        {
            var data = new JObject
            {
                ["revision"] = Client.Character.Value<long>("gameRevision"),
                ["playToken"] = Client.Character.Value<string>("playToken")
            };
            if (type.StartsWith("task.") || type.StartsWith("skill."))
            {
                data["id"] = id;
            }
            if (type.StartsWith("skill."))
            {
                data["slot"] = slot;
            }
            var result = await Client.Request(type, data);
            Client.SetCharacter((JObject)result["data"]);
        }

        private Task ChangeTask()
        {
            int id = taskTemplates[TaskList.value].Value<int>("id");
            var active = Client.Character["infoTask"]["mainTask"] as JObject;
            return ChangeProgression(active != null && active.Value<int>("id") == id ? "task.claim" : "task.accept", id);
        }

        private Task LearnSkill()
        {
            return ChangeProgression("skill.learn", skillTemplates[SkillList.value - 1].Value<int>("id"));
        }

        private Task AssignSkill(int slot)
        {
            return ChangeProgression("skill.assign", SkillList.value == 0 ? 0 : skillTemplates[SkillList.value - 1].Value<int>("id"), slot);
        }

        private Task AdvanceRealm()
        {
            return ChangeProgression("realm.advance");
        }

        private Task CancelTrial()
        {
            return ChangeProgression("realm.cancel");
        }

        public async Task UseSkill(int slot)
        {
            var target = (targetIsMonster ? Client.Monsters : Client.Players).OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == targetId);
            if (target == null)
            {
                return;
            }
            await Client.Request("skill.use", new
            {
                slot,
                targetId,
                targetType = targetIsMonster ? "monster" : "player",
                lifeId = target.Value<string>("lifeId") ?? "",
                playToken = Client.Character.Value<string>("playToken")
            });
        }

        private void UpdateProgression(long now)
        {
            ProgressionError.text = ErrorText.text;
            if (ProgressionPanel.activeSelf)
            {
                RenderProgression();
            }
            bool blocked = Busy || !talentConfirmed || !Client.Connected || Inventory.gameObject.activeSelf || MenuPanel.activeSelf || NpcPanel.activeSelf || ZonePanel.activeSelf || DeathPanel.activeSelf || ProgressionPanel.activeSelf;
            var target = (targetIsMonster ? Client.Monsters : Client.Players).OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == targetId);
            var self = Client.Players.OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == Client.Character.Value<int>("id"));
            for (int i = 0; i < 3; i++)
            {
                int id = Client.Character["game"]["skillSlots"][i].Value<int>();
                var template = skillTemplates.FirstOrDefault(value => value.Value<int>("id") == id);
                var skill = Client.Character["skills"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("templateId") == id);
                var data = skill == null ? null : template?["skillDataTemplates"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("point") == skill.Value<int>("level"));
                long cooldown = data == null ? 0 : Math.Max(0, data.Value<long>("coolDown") - (now - skill.Value<long>("lastTimeUse")));
                bool range = data != null && target != null && self != null && Vector2.Distance(new Vector2(target.Value<float>("x"), target.Value<float>("y")), new Vector2(self.Value<float>("x"), self.Value<float>("y"))) <= data.Value<float>("dx");
                SkillButtons[i].GetComponentInChildren<TMP_Text>().text = "[" + (i + 1) + "] " + (i >= UnlockedSlots() ? "Chưa mở" : template?.Value<string>("name") ?? "Trống") +
                    (data == null ? "" : "\n" + (cooldown > 0 ? (cooldown / 1000f).ToString("0.0") + "s" : "MP " + data["manaUse"]));
                SkillButtons[i].interactable = !blocked && !TrialActive && range && cooldown == 0 && target.Value<long>("hp") > 0 &&
                    Client.Character["game"].Value<long>("mp") >= data.Value<long>("manaUse") && now - Client.Character["game"].Value<long>("lastAttackTime") >= Client.Character.Value<int>("attackCooldown");
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && SkillButtons[i].interactable)
                {
                    int slot = i;
                    Run(() => UseSkill(slot));
                }
            }
            var trial = Client.Character["game"]["breakthrough"];
            int strike = trial.Value<int>("strikeCount");
            if (strike > lastStrike)
            {
                strikeUntil = Time.unscaledTime + 0.4f;
            }
            lastStrike = strike;
            if (talentConfirmed)
            {
                Beam.SetActive(!ProgressionPanel.activeSelf && !Inventory.gameObject.activeSelf && Time.unscaledTime < strikeUntil);
            }
            TrialText.text = TrialActive ? "LÔI KIẾP  " + strike + "/" + trial["damagePercent"].Count() + "\nĐợt tiếp: " + (Math.Max(0, trial.Value<long>("nextStrikeTime") - now) / 1000f).ToString("0.0") + "s" :
                trial.Value<string>("result") == "success" ? "Độ kiếp thành công" : trial.Value<string>("result") == "failed" ? "Độ kiếp thất bại, mất " + trial["lostTuVi"] + " tu vi" : "";
            if (!blocked && Input.GetKeyDown(KeyCode.J))
            {
                ShowProgression(0);
            }
            else if (!blocked && Input.GetKeyDown(KeyCode.K))
            {
                ShowProgression(1);
            }
            else if (!blocked && Input.GetKeyDown(KeyCode.P))
            {
                ShowProgression(2);
            }
            foreach (var button in ProgressionButtons)
            {
                button.interactable = !Busy && talentConfirmed && !DeathPanel.activeSelf && !Inventory.gameObject.activeSelf && !MenuPanel.activeSelf && !NpcPanel.activeSelf && !ZonePanel.activeSelf;
            }
        }
    }
}
