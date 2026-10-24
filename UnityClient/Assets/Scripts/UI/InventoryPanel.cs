using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class InventoryPanel : BaseScene
    {
        public ItemSlot[] Bag;
        public ItemSlot[] Body;
        public StatRow[] Stats;
        public TMP_Text Detail;
        public TMP_Text Remaining;
        public TMP_Text CombatStats;
        public TMP_Text CharacterInfo;
        public TMP_Text BagCount;
        public TMP_Text Pending;
        public TMP_Text Feedback;
        public TMP_Text ItemName;
        public Button Use;
        public TMP_Text UseLabel;
        public Button Apply;
        public Button Cancel;
        public Button Close;
        public Button BagTab;
        public Button StatsTab;
        public GameObject BagContent;
        public GameObject StatsContent;
        private string selectedUid;
        private int selectedBody = -1;
        private int[] points = new int[5];
        private bool dirty;
        private static readonly string[] Names = { "Khí Huyết", "Chân Lực", "Pháp Lực", "Linh Giác", "Sát Cơ" };
        private static readonly string[] BodyNames = { "Pháp khí", "Pháp bào", "Đạo quan", "Đạo ngoa", "Hộ đái", "Ngọc bội" };
        private static readonly string[] Keys = { "khiHuyet", "chanLuc", "phapLuc", "linhGiac", "satCo" };

        private void Awake()
        {
            for (int i = 0; i < Bag.Length; i++)
            {
                int index = i;
                Bag[i].Button.onClick.AddListener(() => SelectItemBag(index));
            }
            for (int i = 0; i < Body.Length; i++)
            {
                int index = i;
                Body[i].Button.onClick.AddListener(() => SelectItemBody(index));
            }
            for (int i = 0; i < Stats.Length; i++)
            {
                int index = i;
                Stats[i].Plus.onClick.AddListener(() => ChangePoint(index, 1));
                Stats[i].Minus.onClick.AddListener(() => ChangePoint(index, -1));
            }
            Close.onClick.AddListener(() => gameObject.SetActive(false));
            Cancel.onClick.AddListener(CancelPoints);
            BagTab.onClick.AddListener(() => ShowStats(false));
            StatsTab.onClick.AddListener(() => ShowStats(true));
            Use.onClick.AddListener(() => Run(UseItem));
            Apply.onClick.AddListener(() => Run(ApplyPoints));
        }

        private void SelectItemBag(int index)
        {
            if (Busy)
            {
                return;
            }

            var item = FindItem("itemBag", index);
            selectedUid = item?.Value<string>("uid");
            selectedBody = -1;
            Render();
        }

        private void SelectItemBody(int index)
        {
            if (Busy)
            {
                return;
            }

            selectedBody = index;
            selectedUid = null;
            Render();
        }

        private void CancelPoints()
        {
            dirty = false;
            Feedback.text = "Đã hủy thay đổi điểm.";
            Render();
        }

        private void OnEnable()
        {
            if (Client == null)
            {
                return;
            }

            dirty = false;
            selectedUid = null;
            selectedBody = -1;
            Feedback.text = string.Empty;
            ErrorText.text = string.Empty;
            Client.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            if (Client != null)
            {
                Client.Changed -= Render;
            }
        }

        public void ShowStats(bool show)
        {
            BagContent.SetActive(!show);
            StatsContent.SetActive(show);
            BagTab.interactable = show;
            StatsTab.interactable = !show;
        }

        private JObject FindItem(string source, int index)
        {
            return Client.Character?["game"]?[source]?.OfType<JObject>().FirstOrDefault(value => value.Value<int>("indexUI") == index);
        }

        private JObject GetItemTemplate(int id)
        {
            return Client.Catalog["items"].OfType<JObject>().FirstOrDefault(value => value.Value<int>("id") == id);
        }

        public void Render()
        {
            if (!this || Client.Character == null)
            {
                return;
            }

            var character = Client.Character;
            var game = character["game"];
            CharacterInfo.text = character.Value<string>("name") + " · " + character["realm"].Value<string>("name") + " · " + character["race"]?.Value<string>("name");
            BagCount.text = "Hành trang  " + game["itemBag"].Count() + " / " + Bag.Length;
            if (!dirty)
            {
                points = game["points"].Values<int>().ToArray();
            }

            for (int i = 0; i < Bag.Length; i++)
            {
                var item = FindItem("itemBag", i);
                var template = item == null ? null : GetItemTemplate(item.Value<int>("id"));
                string name = template == null ? string.Empty : template.Value<string>("name").Replace(template.Value<string>("subName") + " ", "");
                Bag[i].SetData(name, item?.Value<int>("quantity") ?? 0,
                    item != null && item.Value<string>("uid") == selectedUid);
            }
            for (int i = 0; i < Body.Length; i++)
            {
                var item = FindItem("itemBody", i);
                string name = item == null ? "Trống" : GetItemTemplate(item.Value<int>("id")).Value<string>("name");
                Body[i].SetData(BodyNames[i] + "\n" + name, 1, selectedBody == i);
            }
            int remaining = game.Value<int>("pointBudget") - points.Sum();
            Remaining.text = "Điểm còn lại: " + remaining;
            Pending.text = dirty ? "Có thay đổi chưa lưu" : "Chỉ số đã lưu";
            Apply.interactable = dirty && !Busy && Client.Connected;
            Cancel.interactable = dirty && !Busy;
            for (int i = 0; i < Stats.Length; i++)
            {
                string key = Keys[i];
                string original = "original" + char.ToUpperInvariant(key[0]) + key.Substring(1);
                decimal baseStat = character["stats"].Value<decimal>(original) + points[i];
                decimal racePercent = character["race"].Value<decimal>(key + "Percent");
                decimal fromItem = character["stats"].Value<decimal>(key + "FromItem");
                decimal fromEffect = character["stats"].Value<decimal>(key + "FromEffect");
                decimal value = baseStat * (100 + racePercent) / 100 + fromItem + fromEffect;
                Stats[i].Label.text = Names[i];
                Stats[i].Value.text = value.ToString("0.##") + "   (" + points[i] + ")";
                Stats[i].BaseValue.text = baseStat.ToString("0.##");
                Stats[i].RaceValue.text = (racePercent > 0 ? "+" : "") + racePercent.ToString("0.##") + "%";
                Stats[i].RaceValue.color = racePercent < 0 ? new Color(0.65f, 0.2f, 0.16f) : new Color(0.16f, 0.4f, 0.33f);
                Stats[i].ItemValue.text = "+" + fromItem.ToString("0.##");
                Stats[i].EffectValue.text = "+" + fromEffect.ToString("0.##");
                Stats[i].Plus.interactable = !Busy && remaining > 0;
                Stats[i].Minus.interactable = !Busy && points[i] > 0;
            }
            var info = character["stats"];
            CombatStats.text = "Công: " + info["damageFull"] + "\nThủ: " + info["defenceFull"] +
                "\nChí mạng: " + info["critFull"] + "%\nNé: " + info["dodgeFull"] + "%\n\nHP: " + info["hp"] + " / " + info["hpFull"] +
                "\nMP: " + info["mp"] + " / " + info["mpFull"];
            var selected = selectedBody >= 0 ? FindItem("itemBody", selectedBody) : game["itemBag"].OfType<JObject>().FirstOrDefault(value => value.Value<string>("uid") == selectedUid);
            Use.interactable = false;
            ItemName.text = "Chưa chọn vật phẩm";
            Detail.text = "Chọn một ô trong hành trang hoặc trang bị để xem thuộc tính.";
            UseLabel.text = "Sử dụng";
            if (selected != null)
            {
                var template = GetItemTemplate(selected.Value<int>("id"));
                int type = template.Value<int>("type");
                ItemName.text = template.Value<string>("name");
                Detail.text = template.Value<string>("description") + "\n\nSố lượng: " + selected.Value<int>("quantity");
                foreach (JObject option in selected["options"])
                {
                    var value = Client.Catalog["itemOptions"].OfType<JObject>().FirstOrDefault(entry => entry.Value<int>("id") == option.Value<int>("id"));
                    Detail.text += "\n" + (value?.Value<string>("name") ?? "Thuộc tính +#").Replace("#", option["param"].ToString());
                }

                if (type == 6)
                {
                    Detail.text += "\n\nHiệu lực: " + template.Value<int>("durationSeconds") / 60 + " phút" +
                        "\nTăng " + Names[template.Value<int>("effectStatId") - 1] + ": +" + template["effectParam"];
                }

                if (selectedBody >= 0)
                {
                    UseLabel.text = "Tháo đồ";
                }
                else
                {
                    UseLabel.text = type <= 5 ? "Trang bị" : "Dùng đan dược";
                }

                Use.interactable = !Busy && Client.Connected && (selectedBody >= 0 || type <= 6);
            }
        }

        private void ChangePoint(int index, int amount)
        {
            if (Busy)
            {
                return;
            }

            int remaining = Client.Character["game"].Value<int>("pointBudget") - points.Sum();
            if ((amount > 0 && remaining < amount) || points[index] + amount < 0)
            {
                return;
            }

            points[index] += amount;
            dirty = true;
            Feedback.text = string.Empty;
            Render();
        }

        private async Task ApplyPoints()
        {
            var character = Client.Character;
            JObject result = await Client.Request("potential", new
            {
                points,
                revision = character.Value<long>("gameRevision"),
                playToken = character.Value<string>("playToken")
            });
            Client.SetCharacter((JObject)result["data"]);
            dirty = false;
            Feedback.text = "Đã lưu điểm tiềm năng.";
            Render();
        }

        private async Task UseItem()
        {
            var character = Client.Character;
            JObject result;
            if (selectedBody >= 0)
            {
                result = await Client.Request("body.remove", new
                {
                    slot = selectedBody,
                    revision = character.Value<long>("gameRevision"),
                    playToken = character.Value<string>("playToken")
                });
            }
            else
            {
                result = await Client.Request("bag.use", new
                {
                    uid = selectedUid,
                    revision = character.Value<long>("gameRevision"),
                    playToken = character.Value<string>("playToken")
                });
            }

            Client.SetCharacter((JObject)result["data"]);
            Feedback.text = selectedBody >= 0 ? "Đã tháo trang bị." : "Đã sử dụng vật phẩm.";
            Render();
        }

        protected override void OnBusyChanged()
        {
            base.OnBusyChanged();
            if (Client != null && Client.Character != null)
            {
                Render();
            }
        }
    }
}
