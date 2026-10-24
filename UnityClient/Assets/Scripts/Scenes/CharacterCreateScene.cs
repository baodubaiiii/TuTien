using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class CharacterCreateScene : BaseScene
    {
        public TMP_InputField Name;
        public TMP_Dropdown Class;
        public TMP_Dropdown Gender;
        public TMP_Dropdown Appearance;
        public TMP_Text Preview;
        public CharacterPreview Figure;
        public TMP_Text NameLength;
        public Button Create;
        public Button Back;
        private JObject[] appearances;
        private JObject[] classes;

        private void Start()
        {
            if (!EnsureClient())
            {
                return;
            }

            classes = Client.Catalog["classes"].OfType<JObject>().ToArray();
            Class.ClearOptions();
            Class.AddOptions(classes.Select(value => value.Value<string>("name")).ToList());
            Gender.ClearOptions();
            Gender.AddOptions(new System.Collections.Generic.List<string> { "Nam Tu", "Nữ Tu" });
            Gender.onValueChanged.AddListener(value => ChangeGender());
            Appearance.onValueChanged.AddListener(value => RefreshPreview());
            Name.onValueChanged.AddListener(value => RefreshPreview());
            Class.onValueChanged.AddListener(value => RefreshPreview());
            ChangeGender();
            Back.onClick.AddListener(() => Run(() => GameClient.LoadScene("CharacterSelectScene")));
            Create.onClick.AddListener(() => Run(CreateCharacter));
        }

        private async Task CreateCharacter()
        {
            await Client.Request("character.create", new
            {
                slot = CharacterSelectScene.CreateSlot,
                name = Name.text.Trim(),
                classId = classes[Class.value].Value<int>("id"),
                gender = Gender.value,
                appearanceId = appearances[Appearance.value].Value<int>("id")
            });
            await GameClient.LoadScene("CharacterSelectScene");
        }

        private void ChangeGender()
        {
            appearances = Client.Catalog["appearances"].OfType<JObject>().Where(value => value.Value<int>("gender") == Gender.value).ToArray();
            Appearance.ClearOptions();
            Appearance.AddOptions(appearances.Select(value => value.Value<string>("name")).ToList());
            Appearance.value = 0;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (appearances == null || appearances.Length == 0)
            {
                return;
            }

            Preview.text = (string.IsNullOrWhiteSpace(Name.text) ? "Đạo hữu" : Name.text.Trim()) + "\n\n" +
                (Gender.value == 0 ? "Nam Tu" : "Nữ Tu") + "\n" + appearances[Appearance.value].Value<string>("name") +
                "\n\nTộc và Linh Căn sẽ được Thiên Đạo định đoạt khi nhập thế.";
            Figure.SetData(string.IsNullOrWhiteSpace(Name.text) ? "Đạo hữu" : Name.text.Trim(), Gender.value,
                appearances[Appearance.value].Value<int>("id"), classes[Class.value].Value<string>("name") + " · " + appearances[Appearance.value].Value<string>("name"));
            NameLength.text = Name.text.Trim().Length + " / 24";
            Create.interactable = !Busy && Name.text.Trim().Length >= 2;
        }

        protected override void OnBusyChanged()
        {
            base.OnBusyChanged();
            RefreshPreview();
        }
    }
}
