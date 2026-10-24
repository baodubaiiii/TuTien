using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class CharacterSelectScene : BaseScene
    {
        public Button[] Slots;
        public TMP_Text[] SlotText;
        public CharacterPreview[] Previews;
        public TMP_Text Account;
        public TMP_Text Count;
        public Button RefreshButton;
        public Button Logout;
        public static int CreateSlot = 1;

        private void Start()
        {
            if (!EnsureClient())
            {
                return;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Slots[i].onClick.AddListener(() => Run(() => Select(index)));
            }
            Logout.onClick.AddListener(() => Run(Client.Logout));
            RefreshButton.onClick.AddListener(() => Run(Refresh));
            Account.text = "Tài khoản: " + Client.AccountName;
            Run(Refresh);
        }

        private async Task Refresh()
        {
            foreach (var button in Slots)
            {
                button.interactable = false;
            }

            await Client.RefreshCharacters();
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                JToken character = Client.Slots["slots"][i]["character"];
                bool empty = character.Type == JTokenType.Null;
                SlotText[i].text = empty ? "+ Tạo nhân vật" : "Nhập thế";
                if (empty)
                {
                    Previews[i].SetData("Ô nhân vật trống", 0, 0, "Khởi đầu một hành trình mới");
                }
                else
                {
                    count++;
                    Previews[i].SetData(character.Value<string>("name"), character.Value<int>("gender"), character.Value<int>("appearanceId"),
                        character.Value<string>("className") + " · " + character["realm"].Value<string>("name"));
                }

                Slots[i].interactable = true;
            }
            Count.text = count + " / 3 nhân vật";
        }

        private async Task Select(int index)
        {
            if (Client.Slots == null)
            {
                return;
            }

            JToken value = Client.Slots["slots"][index]["character"];
            if (value.Type == JTokenType.Null)
            {
                CreateSlot = index + 1;
                await GameClient.LoadScene("CharacterCreateScene");
            }
            else
            {
                await Client.Enter(value.Value<int>("id"));
            }
        }
    }
}
