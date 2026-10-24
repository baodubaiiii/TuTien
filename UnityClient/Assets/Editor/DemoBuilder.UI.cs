using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client.Editor
{
    public static partial class DemoBuilder
    {
        private static readonly Color Muted = new Color(0.38f, 0.44f, 0.39f);
        private static readonly Color Line = new Color(0.7f, 0.75f, 0.68f);
        private static readonly Color Surface = new Color(0.9f, 0.92f, 0.87f);
        private static readonly Color Error = new Color(0.65f, 0.2f, 0.16f);

        private static Image Frame(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Panel(name, parent, position, size, color);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1, -1);
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize = 22, Color? color = null)
        {
            var text = Text(name, parent, value, position, size, fontSize, color);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(Transform target, Vector2 anchor, Vector2 position)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        private static void Divider(Transform parent, Vector2 position, float width)
        {
            var image = Panel("Divider", parent, position, new Vector2(width, 1), Line);
            image.raycastTarget = false;
        }

        private static Button Button(string name, Transform parent, string value, Vector2 position, Vector2 size, bool secondary = false)
        {
            var image = Frame(name, parent, position, size, secondary ? Paper : Jade);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.89f, 0.94f, 0.88f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.73f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.66f, 0.7f, 0.65f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            Text("Label", image.transform, value, Vector2.zero, size - new Vector2(16, 6), 24, secondary ? Ink : Paper);
            return button;
        }

        private static TMP_InputField Input(string name, Transform parent, string placeholder, Vector2 position, bool password = false)
        {
            var root = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            root.name = name;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(430, 54);
            root.GetComponent<Image>().color = new Color(0.98f, 0.98f, 0.95f);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1, -1);
            var input = root.GetComponent<TMP_InputField>();
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.characterLimit = password ? 128 : 24;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSize = 25;
                text.fontStyle = FontStyles.Normal;
                text.color = Ink;
            }
            ((TMP_Text)input.placeholder).text = placeholder;
            return input;
        }

        private static TMP_Dropdown Dropdown(string name, Transform parent, Vector2 position)
        {
            var root = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            root.name = name;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(430, 54);
            root.GetComponent<Image>().color = new Color(0.98f, 0.98f, 0.95f);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1, -1);
            var arrow = root.transform.Find("Arrow");
            if (arrow)
            {
                arrow.gameObject.SetActive(false);
            }
            Text("ArrowLabel", root.transform, "v", new Vector2(193, 0), new Vector2(26, 40), 22, Muted);
            var dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.template.sizeDelta = new Vector2(0, 220);
            dropdown.template.GetComponent<Image>().color = Paper;
            var item = dropdown.template.Find("Viewport/Content/Item").GetComponent<RectTransform>();
            item.sizeDelta = new Vector2(0, 48);
            var checkmark = item.Find("Item Checkmark");
            if (checkmark)
            {
                checkmark.GetComponent<Image>().color = Jade;
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSize = 24;
                text.color = Ink;
            }
            return dropdown;
        }

        private static void SceneTitle(Transform canvas, string title, string subtitle)
        {
            Label("Game", canvas, "TU TIÊN", new Vector2(-575, 385), new Vector2(330, 48), 30, Jade);
            Text("Title", canvas, title, new Vector2(0, 312), new Vector2(1100, 70), 44);
            Text("Subtitle", canvas, subtitle, new Vector2(0, 255), new Vector2(1200, 40), 23, Muted);
        }

        private static void CreateCharacterPreview()
        {
            var root = Rect("CharacterPreview", null, Vector2.zero, new Vector2(360, 340));
            var preview = root.gameObject.AddComponent<CharacterPreview>();
            preview.Figure = Rect("Figure", root, new Vector2(0, 30), new Vector2(160, 200)).gameObject;
            Panel("Stand", preview.Figure.transform, new Vector2(0, -95), new Vector2(150, 4), Line);
            preview.Robe = Panel("Robe", preview.Figure.transform, new Vector2(0, -10), new Vector2(86, 125), Jade);
            Panel("Belt", preview.Figure.transform, new Vector2(0, -20), new Vector2(86, 9), Ink);
            Panel("Head", preview.Figure.transform, new Vector2(0, 73), new Vector2(46, 48), new Color(0.8f, 0.71f, 0.57f));
            preview.Hair = Panel("Hair", preview.Figure.transform, new Vector2(0, 100), new Vector2(48, 24), Ink);
            preview.Name = Text("Name", root, "Đạo hữu", new Vector2(0, -104), new Vector2(350, 50), 26);
            preview.Description = Text("Description", root, "", new Vector2(0, -155), new Vector2(350, 44), 21, Muted);
            SavePrefab(root.gameObject, "Assets/Resources/Prefabs/CharacterPreview.prefab");
        }

        private static CharacterPreview Preview(Transform parent, Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/CharacterPreview.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).anchoredPosition = position;
            return root.GetComponent<CharacterPreview>();
        }

        private static void CreateItemSlot()
        {
            var root = Frame("ItemSlot", null, Vector2.zero, new Vector2(72, 80), Paper);
            var slot = root.gameObject.AddComponent<ItemSlot>();
            slot.Background = root;
            slot.Button = root.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = root;
            slot.Selection = Panel("Selected", root.transform, new Vector2(0, 37), new Vector2(66, 4), Jade);
            slot.Selection.raycastTarget = false;
            slot.Selection.enabled = false;
            slot.Label = Text("Name", root.transform, "", new Vector2(0, 12), new Vector2(66, 50), 15);
            slot.Label.enableAutoSizing = true;
            slot.Label.fontSizeMin = 12;
            slot.Label.fontSizeMax = 16;
            slot.Quantity = Text("Quantity", root.transform, "", new Vector2(18, -26), new Vector2(30, 26), 16, Jade);
            SavePrefab(root.gameObject, "Assets/Resources/Prefabs/ItemSlot.prefab");
        }

        private static void CreateStatRow()
        {
            var root = Panel("StatRow", null, Vector2.zero, new Vector2(960, 66), Surface);
            var row = root.gameObject.AddComponent<StatRow>();
            row.Label = Label("Name", root.transform, "Khí Huyết", new Vector2(-365, 0), new Vector2(190, 50), 24);
            row.BaseValue = Text("Base", root.transform, "", new Vector2(-200, 0), new Vector2(100, 50), 23);
            row.RaceValue = Text("Race", root.transform, "", new Vector2(-90, 0), new Vector2(100, 50), 22);
            row.ItemValue = Text("Item", root.transform, "", new Vector2(15, 0), new Vector2(100, 50), 22);
            row.EffectValue = Text("Effect", root.transform, "", new Vector2(120, 0), new Vector2(100, 50), 22);
            row.Value = Text("Value", root.transform, "", new Vector2(255, 0), new Vector2(160, 50), 23, Jade);
            row.Minus = Button("Minus", root.transform, "−", new Vector2(363, 0), new Vector2(46, 44), true);
            row.Plus = Button("Plus", root.transform, "+", new Vector2(425, 0), new Vector2(46, 44), true);
            SavePrefab(root.gameObject, "Assets/Resources/Prefabs/StatRow.prefab");
        }

        private static void CreateInventory()
        {
            var root = Panel("InventoryPanel", null, Vector2.zero, new Vector2(1600, 900), new Color(0.08f, 0.13f, 0.1f, 0.7f));
            Stretch((RectTransform)root.transform);
            var panel = root.gameObject.AddComponent<InventoryPanel>();
            var window = Frame("Window", root.transform, Vector2.zero, new Vector2(1420, 800), Paper);
            Label("Title", window.transform, "HÀNH TRANG", new Vector2(-450, 350), new Vector2(430, 48), 36);
            panel.CharacterInfo = Label("Character", window.transform, "", new Vector2(75, 350), new Vector2(570, 42), 22, Muted);
            panel.Close = Button("Close", window.transform, "Đóng [Esc]", new Vector2(595, 350), new Vector2(170, 48), true);
            Divider(window.transform, new Vector2(0, 310), 1360);
            panel.BagTab = Button("BagTab", window.transform, "Vật phẩm", new Vector2(-555, 270), new Vector2(240, 48), true);
            panel.StatsTab = Button("StatsTab", window.transform, "Nhân vật / Chỉ số", new Vector2(-285, 270), new Vector2(270, 48), true);
            panel.BagContent = Rect("BagContent", window.transform, new Vector2(0, -25), new Vector2(1360, 520)).gameObject;
            panel.StatsContent = Rect("StatsContent", window.transform, new Vector2(0, -25), new Vector2(1360, 520)).gameObject;
            panel.Bag = new ItemSlot[40];
            panel.Body = new ItemSlot[6];
            Label("BodyTitle", panel.BagContent.transform, "Trang bị", new Vector2(-520, 230), new Vector2(300, 40), 26);
            panel.BagCount = Label("BagTitle", panel.BagContent.transform, "Hành trang", new Vector2(20, 230), new Vector2(630, 40), 26);
            var detail = Frame("ItemDetail", panel.BagContent.transform, new Vector2(525, -20), new Vector2(300, 540), Surface);
            panel.ItemName = Label("ItemName", detail.transform, "Chưa chọn vật phẩm", new Vector2(0, 205), new Vector2(260, 82), 27, Jade);
            panel.Detail = Label("Detail", detail.transform, "", new Vector2(0, -5), new Vector2(260, 310), 22);
            panel.Detail.alignment = TextAlignmentOptions.TopLeft;
            panel.Use = Button("Use", detail.transform, "Sử dụng", new Vector2(0, -225), new Vector2(260, 54));
            panel.UseLabel = panel.Use.GetComponentInChildren<TMP_Text>();
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/ItemSlot.prefab");
            for (int i = 0; i < 6; i++)
            {
                var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
                slot.transform.SetParent(panel.BagContent.transform, false);
                var rect = (RectTransform)slot.transform;
                rect.anchoredPosition = new Vector2(-595 + (i % 2) * 155, 130 - (i / 2) * 145);
                rect.sizeDelta = new Vector2(140, 120);
                var item = slot.GetComponent<ItemSlot>();
                ((RectTransform)item.Label.transform).sizeDelta = new Vector2(128, 100);
                item.Label.fontSizeMax = 22;
                ((RectTransform)item.Selection.transform).anchoredPosition = new Vector2(0, 56);
                panel.Body[i] = item;
            }
            for (int i = 0; i < 40; i++)
            {
                var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
                slot.transform.SetParent(panel.BagContent.transform, false);
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(-270 + (i % 8) * 86, 150 - (i / 8) * 91);
                panel.Bag[i] = slot.GetComponent<ItemSlot>();
            }
            Text("BodyHint", panel.BagContent.transform, "Chọn đồ trong túi để trang bị.\nChọn ô đang mặc để tháo đồ.", new Vector2(-520, -265), new Vector2(300, 70), 20, Muted);
            var rows = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/StatRow.prefab");
            panel.Stats = new StatRow[5];
            string[] headings = { "Chỉ số", "Base", "Tộc", "Trang bị", "Đan", "Tổng (điểm)" };
            float[] columns = { -365, -200, -90, 15, 120, 255 };
            for (int i = 0; i < headings.Length; i++)
            {
                Text("Heading" + i, panel.StatsContent.transform, headings[i], new Vector2(-185 + columns[i], 180), new Vector2(i == 0 ? 190 : 150, 40), 21, Muted);
            }
            for (int i = 0; i < 5; i++)
            {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(rows);
                row.transform.SetParent(panel.StatsContent.transform, false);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(-185, 120 - i * 77);
                panel.Stats[i] = row.GetComponent<StatRow>();
            }
            panel.Remaining = Label("Remaining", panel.StatsContent.transform, "", new Vector2(-430, 240), new Vector2(470, 45), 29, Jade);
            panel.Pending = Label("Pending", panel.StatsContent.transform, "", new Vector2(55, 240), new Vector2(450, 45), 22, Muted);
            Label("CombatTitle", panel.StatsContent.transform, "Đang sử dụng", new Vector2(515, 200), new Vector2(270, 45), 27, Jade);
            panel.CombatStats = Label("CombatStats", panel.StatsContent.transform, "", new Vector2(515, 15), new Vector2(270, 290), 25);
            Text("PreviewHint", panel.StatsContent.transform, "Tổng là xem trước. Chỉ số chiến đấu cập nhật sau Áp dụng.\n% Tộc chỉ tính trên Base, gồm chỉ số gốc và điểm phân bổ.", new Vector2(-180, -265), new Vector2(960, 65), 21, Muted);
            panel.Apply = Button("Apply", panel.StatsContent.transform, "Áp dụng", new Vector2(540, -265), new Vector2(235, 52));
            panel.Cancel = Button("Cancel", panel.StatsContent.transform, "Hủy thay đổi", new Vector2(540, -195), new Vector2(235, 48), true);
            panel.Feedback = Label("Feedback", window.transform, "", new Vector2(-290, -340), new Vector2(760, 32), 21, Jade);
            panel.BusyText = Label("Busy", window.transform, "", new Vector2(460, -340), new Vector2(400, 32), 21, Muted);
            panel.ErrorText = Label("Error", window.transform, "", new Vector2(0, -375), new Vector2(1340, 35), 21, Error);
            panel.StatsContent.SetActive(false);
            root.gameObject.SetActive(false);
            SavePrefab(root.gameObject, "Assets/Resources/Prefabs/InventoryPanel.prefab");
        }

        private static void BuildLoading()
        {
            NewScene("LoadingScene", out var canvas);
            var controller = canvas.gameObject.AddComponent<LoadingScene>();
            SceneTitle(canvas, "Chuẩn bị nhập đạo", "Tải dữ liệu một lần để bắt đầu hành trình");
            var panel = Frame("DownloadPanel", canvas, new Vector2(0, -15), new Vector2(1000, 380), Paper);
            controller.Status = Text("Status", panel.transform, "Đang kiểm tra dữ liệu...", new Vector2(0, 110), new Vector2(900, 60), 32);
            controller.Size = Text("Size", panel.transform, "", new Vector2(0, 45), new Vector2(900, 40), 23, Muted);
            var track = Panel("Progress", panel.transform, new Vector2(0, -20), new Vector2(820, 16), Line);
            controller.Progress = track.gameObject.AddComponent<Slider>();
            var fill = Panel("Fill", track.transform, Vector2.zero, new Vector2(820, 16), Jade);
            controller.Progress.fillRect = (RectTransform)fill.transform;
            controller.Progress.fillRect.sizeDelta = Vector2.zero;
            controller.Progress.interactable = false;
            controller.Progress.value = 0;
            controller.DownloadButton = Button("Download", panel.transform, "Tải dữ liệu", new Vector2(0, -110), new Vector2(320, 58));
            controller.ErrorText = Text("Error", canvas, "", new Vector2(0, -280), new Vector2(1100, 60), 24, Error);
            Text("Footer", canvas, "Dữ liệu đã tải được giữ lại cho lần chơi sau.", new Vector2(0, -370), new Vector2(1000, 40), 21, Muted);
            SaveScene("LoadingScene");
        }

        private static void BuildLogin()
        {
            NewScene("LoginScene", out var canvas);
            Label("GameTitle", canvas, "TU TIÊN", new Vector2(-400, 140), new Vector2(570, 110), 80, Jade);
            Label("Intro", canvas, "Một bước nhập đạo.\nMột đời tu hành.", new Vector2(-400, 20), new Vector2(570, 120), 37);
            Label("Server", canvas, "Máy chủ demo · Khu vực 01", new Vector2(-400, -150), new Vector2(570, 45), 24, Muted);
            Label("Session", canvas, "Tiếp tục với tối đa 3 nhân vật\ntrong cùng một tài khoản.", new Vector2(-400, -220), new Vector2(570, 75), 23, Muted);
            var panel = Frame("LoginPanel", canvas, new Vector2(355, 0), new Vector2(600, 740), Paper);
            var controller = panel.gameObject.AddComponent<LoginScene>();
            controller.Title = Label("Title", panel.transform, "Đăng nhập", new Vector2(0, 300), new Vector2(490, 65), 42);
            Label("UserLabel", panel.transform, "Tài khoản", new Vector2(0, 222), new Vector2(430, 35));
            controller.Username = Input("Username", panel.transform, "Nhập tài khoản", new Vector2(0, 172));
            Label("PasswordLabel", panel.transform, "Mật khẩu", new Vector2(0, 113), new Vector2(430, 35));
            controller.Password = Input("Password", panel.transform, "Nhập mật khẩu", new Vector2(0, 63), true);
            ((RectTransform)controller.Password.transform).sizeDelta = new Vector2(345, 54);
            ((RectTransform)controller.Password.transform).anchoredPosition = new Vector2(-42, 63);
            controller.ShowPassword = Button("ShowPassword", panel.transform, "Hiện", new Vector2(177, 63), new Vector2(76, 54), true);
            controller.PasswordLabel = controller.ShowPassword.GetComponentInChildren<TMP_Text>();
            controller.ConfirmRow = Rect("ConfirmRow", panel.transform, new Vector2(0, -35), new Vector2(430, 105)).gameObject;
            Label("ConfirmLabel", controller.ConfirmRow.transform, "Nhập lại mật khẩu", new Vector2(0, 30), new Vector2(430, 35));
            controller.ConfirmPassword = Input("ConfirmPassword", controller.ConfirmRow.transform, "Nhập lại mật khẩu", new Vector2(0, -20), true);
            controller.ConfirmRow.SetActive(false);
            controller.Submit = Button("Submit", panel.transform, "Đăng nhập", new Vector2(0, -145), new Vector2(430, 58));
            controller.SubmitLabel = controller.Submit.GetComponentInChildren<TMP_Text>();
            controller.Register = Button("Register", panel.transform, "Chưa có tài khoản? Đăng ký", new Vector2(0, -220), new Vector2(470, 48), true);
            controller.RegisterLabel = controller.Register.GetComponentInChildren<TMP_Text>();
            controller.ErrorText = Text("Error", panel.transform, "", new Vector2(0, -283), new Vector2(510, 52), 21, Error);
            controller.BusyText = Text("Busy", panel.transform, "", new Vector2(0, -332), new Vector2(510, 32), 21, Muted);
            controller.Controls = new Selectable[] { controller.Username, controller.Password, controller.ConfirmPassword, controller.Submit, controller.Register, controller.ShowPassword };
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject, "Assets/Resources/Prefabs/LoginPanel.prefab");
            SaveScene("LoginScene");
        }

        private static void BuildSelect()
        {
            NewScene("CharacterSelectScene", out var canvas);
            SceneTitle(canvas, "Chọn đạo hữu", "Chọn nhân vật để nhập thế hoặc bắt đầu tại một ô trống");
            var controller = canvas.gameObject.AddComponent<CharacterSelectScene>();
            controller.Slots = new Button[3];
            controller.SlotText = new TMP_Text[3];
            controller.Previews = new CharacterPreview[3];
            controller.Account = Label("Account", canvas, "", new Vector2(-400, -330), new Vector2(560, 40), 23, Muted);
            controller.Count = Label("Count", canvas, "", new Vector2(440, 385), new Vector2(460, 40), 24, Muted);
            for (int i = 0; i < 3; i++)
            {
                var slot = Frame("Slot" + (i + 1), canvas, new Vector2(-450 + i * 450, -25), new Vector2(390, 480), Paper);
                Label("SlotNumber", slot.transform, "NHÂN VẬT 0" + (i + 1), new Vector2(0, 190), new Vector2(330, 35), 21, Muted);
                controller.Previews[i] = Preview(slot.transform, new Vector2(0, 15));
                controller.Slots[i] = Button("CharacterSlot" + (i + 1), slot.transform, "Đang tải...", new Vector2(0, -190), new Vector2(330, 54));
                controller.SlotText[i] = controller.Slots[i].GetComponentInChildren<TMP_Text>();
            }
            controller.RefreshButton = Button("Refresh", canvas, "Tải lại", new Vector2(300, -330), new Vector2(170, 50), true);
            controller.Logout = Button("Logout", canvas, "Đăng xuất", new Vector2(550, -330), new Vector2(235, 50), true);
            controller.ErrorText = Text("Error", canvas, "", new Vector2(0, -405), new Vector2(1300, 40), 23, Error);
            controller.BusyText = Text("Busy", canvas, "", new Vector2(0, -373), new Vector2(600, 32), 21, Muted);
            controller.Controls = new Selectable[] { controller.Slots[0], controller.Slots[1], controller.Slots[2], controller.RefreshButton, controller.Logout };
            SaveScene("CharacterSelectScene");
        }

        private static void BuildCreate()
        {
            NewScene("CharacterCreateScene", out var canvas);
            var root = Frame("CreatePanel", canvas, Vector2.zero, new Vector2(1360, 800), Paper);
            var controller = root.gameObject.AddComponent<CharacterCreateScene>();
            Label("Title", root.transform, "Khởi tạo đạo hữu", new Vector2(-365, 340), new Vector2(520, 65), 40);
            Divider(root.transform, new Vector2(0, 290), 1260);
            Label("NameLabel", root.transform, "Đạo hiệu", new Vector2(-360, 240), new Vector2(430, 35));
            controller.Name = Input("Name", root.transform, "Nhập đạo hiệu", new Vector2(-360, 190));
            controller.NameLength = Label("NameLength", root.transform, "0 / 24", new Vector2(-205, 140), new Vector2(110, 30), 19, Muted);
            Label("NameHint", root.transform, "2–24 ký tự: chữ, số và khoảng trắng", new Vector2(-415, 140), new Vector2(320, 30), 19, Muted);
            Label("WeaponLabel", root.transform, "Pháp khí khởi đầu", new Vector2(-360, 90), new Vector2(430, 35));
            controller.Class = Dropdown("Class", root.transform, new Vector2(-360, 40));
            Label("GenderLabel", root.transform, "Giới tính", new Vector2(-360, -20), new Vector2(430, 35));
            controller.Gender = Dropdown("Gender", root.transform, new Vector2(-360, -70));
            Label("AppearanceLabel", root.transform, "Ngoại hình", new Vector2(-360, -130), new Vector2(430, 35));
            controller.Appearance = Dropdown("Appearance", root.transform, new Vector2(-360, -180));
            controller.Figure = Preview(root.transform, new Vector2(80, 70));
            var talent = Frame("TalentInfo", root.transform, new Vector2(450, 40), new Vector2(300, 390), Surface);
            Label("TalentTitle", talent.transform, "Tiên Thiên", new Vector2(0, 140), new Vector2(260, 45), 30, Jade);
            Label("Race", talent.transform, "Huyết Mạch\nChưa định đoạt", new Vector2(0, 40), new Vector2(260, 85), 25);
            Label("Element", talent.transform, "Linh Căn\nChưa thức tỉnh", new Vector2(0, -70), new Vector2(260, 85), 25);
            Text("TalentHint", root.transform, "Tộc và Linh Căn được định đoạt một lần khi nhập thế.", new Vector2(260, -205), new Vector2(660, 65), 22, Muted);
            controller.Preview = Text("Description", root.transform, "", Vector2.zero, Vector2.zero);
            controller.Preview.gameObject.SetActive(false);
            controller.Create = Button("Create", root.transform, "Tạo nhân vật", new Vector2(400, -295), new Vector2(330, 58));
            controller.Back = Button("Back", root.transform, "Quay lại", new Vector2(-455, -295), new Vector2(240, 58), true);
            controller.ErrorText = Text("Error", root.transform, "", new Vector2(0, -365), new Vector2(1200, 40), 22, Error);
            controller.BusyText = Text("Busy", root.transform, "", new Vector2(0, -315), new Vector2(400, 30), 20, Muted);
            controller.Controls = new Selectable[] { controller.Name, controller.Class, controller.Gender, controller.Appearance, controller.Create, controller.Back };
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Resources/Prefabs/CharacterCreatePanel.prefab");
            SaveScene("CharacterCreateScene");
        }

        private static Image Bar(string name, Transform parent, Vector2 position, Color color, out TMP_Text text)
        {
            var track = Panel(name, parent, position, new Vector2(340, 28), Ink);
            var fill = Panel("Fill", track.transform, Vector2.zero, new Vector2(340, 28), color);
            fill.type = Image.Type.Filled;
            fill.sprite = square;
            fill.fillMethod = Image.FillMethod.Horizontal;
            text = Text("Value", track.transform, "", Vector2.zero, new Vector2(340, 28), 21, Paper);
            return fill;
        }

        private static void BuildWorld()
        {
            var camera = NewScene("WorldScene", out var canvas);
            camera.transform.position = new Vector3(8, -4.5f, -10);
            var hud = Rect("PlayerHUD", canvas, Vector2.zero, new Vector2(1600, 900));
            Stretch(hud);
            var controller = hud.gameObject.AddComponent<WorldScene>();
            controller.Camera = camera;
            var info = Frame("PlayerInfo", hud, new Vector2(-555, 315), new Vector2(420, 215), Paper);
            Anchor(info.transform, new Vector2(0, 1), new Vector2(245, -135));
            controller.Name = Label("Name", info.transform, "Đạo hữu", new Vector2(0, 67), new Vector2(370, 42), 31);
            controller.Realm = Label("Realm", info.transform, "", new Vector2(0, 27), new Vector2(370, 32), 22, Muted);
            controller.HpFill = Bar("HP", info.transform, new Vector2(0, -20), new Color(0.66f, 0.25f, 0.21f), out controller.HpText);
            controller.MpFill = Bar("MP", info.transform, new Vector2(0, -63), new Color(0.26f, 0.44f, 0.63f), out controller.MpText);
            controller.Effects = Text("Effects", hud, "", Vector2.zero, Vector2.zero);
            controller.EffectSlots = new ItemEffectView[5];
            for (int i = 0; i < 5; i++)
            {
                var slot = Frame("Effect" + i, hud, new Vector2(-720 + i * 88, 153), new Vector2(80, 88), Paper);
                Anchor(slot.transform, new Vector2(0, 1), new Vector2(80 + i * 88, -297));
                var view = slot.gameObject.AddComponent<ItemEffectView>();
                view.Name = Text("Name", slot.transform, "", new Vector2(0, 15), new Vector2(72, 48), 15);
                view.Name.enableAutoSizing = true;
                view.Name.fontSizeMin = 11;
                view.Name.fontSizeMax = 15;
                view.Time = Text("Time", slot.transform, "", new Vector2(0, -25), new Vector2(74, 34), 20, Jade);
                controller.EffectSlots[i] = view;
                slot.gameObject.SetActive(false);
            }
            var location = Frame("LocationPanel", hud, new Vector2(555, 355), new Vector2(420, 135), Paper);
            Anchor(location.transform, Vector2.one, new Vector2(-245, -95));
            controller.Location = Text("Location", location.transform, "", new Vector2(0, 18), new Vector2(380, 75), 28);
            controller.Connection = Text("Connection", location.transform, "Đã kết nối", new Vector2(-35, -42), new Vector2(290, 28), 20, Jade);
            controller.MenuButton = Button("Menu", hud, "Menu [Esc]", new Vector2(650, 245), new Vector2(225, 48), true);
            Anchor(controller.MenuButton.transform, Vector2.one, new Vector2(-150, -205));
            var menu = Frame("MenuPanel", hud, new Vector2(555, 40), new Vector2(420, 315), Paper);
            Anchor(menu.transform, Vector2.one, new Vector2(-245, -410));
            controller.MenuPanel = menu.gameObject;
            Text("Title", menu.transform, "Tùy chọn", new Vector2(0, 108), new Vector2(350, 45), 30);
            controller.ChangeCharacter = Button("ChangeCharacter", menu.transform, "Đổi nhân vật", new Vector2(0, 30), new Vector2(350, 48), true);
            controller.Logout = Button("Logout", menu.transform, "Đăng xuất", new Vector2(0, -35), new Vector2(350, 48), true);
            controller.ResumeButton = Button("Resume", menu.transform, "Tiếp tục", new Vector2(0, -103), new Vector2(350, 48));
            menu.gameObject.SetActive(false);
            var target = Frame("TargetPanel", hud, new Vector2(550, -200), new Vector2(420, 155), Paper);
            Anchor(target.transform, new Vector2(1, 0), new Vector2(-250, 250));
            controller.Target = Text("Target", target.transform, "Chưa chọn mục tiêu", new Vector2(0, 33), new Vector2(380, 70), 26);
            controller.TargetHpFill = Bar("TargetHP", target.transform, new Vector2(0, -37), new Color(0.66f, 0.25f, 0.21f), out var targetText);
            targetText.text = string.Empty;
            controller.CombatHint = Text("CombatHint", hud, "", new Vector2(550, -300), new Vector2(430, 40), 21, Paper);
            Anchor(controller.CombatHint.transform, new Vector2(1, 0), new Vector2(-250, 150));
            controller.AttackButton = Button("Attack", hud, "Đánh [Space]", new Vector2(550, -365), new Vector2(360, 65));
            Anchor(controller.AttackButton.transform, new Vector2(1, 0), new Vector2(-250, 85));
            controller.AttackLabel = controller.AttackButton.GetComponentInChildren<TMP_Text>();
            var dock = Frame("Actions", hud, new Vector2(0, -370), new Vector2(570, 105), Paper);
            Anchor(dock.transform, new Vector2(0.5f, 0), new Vector2(0, 80));
            controller.InventoryButton = Button("Inventory", dock.transform, "Hành trang [B]", new Vector2(-140, 0), new Vector2(250, 58), true);
            controller.StatsButton = Button("Stats", dock.transform, "Nhân vật [C]", new Vector2(140, 0), new Vector2(250, 58), true);
            var joystick = Frame("Joystick", hud, new Vector2(-650, -320), new Vector2(140, 140), Paper);
            Anchor(joystick.transform, Vector2.zero, new Vector2(150, 130));
            controller.Joystick = joystick.gameObject.AddComponent<DemoJoystick>();
            controller.Joystick.Handle = (RectTransform)Panel("Handle", joystick.transform, Vector2.zero, new Vector2(46, 46), Jade).transform;
            var controls = Panel("Controls", hud, Vector2.zero, new Vector2(430, 38), Paper);
            Anchor(controls.transform, Vector2.zero, new Vector2(250, 32));
            Text("Hint", controls.transform, "WASD / chạm để di chuyển", Vector2.zero, new Vector2(410, 38), 21, Muted);
            controller.ErrorText = Text("Error", hud, "", new Vector2(0, 250), new Vector2(650, 65), 24, new Color(0.98f, 0.77f, 0.6f));
            Anchor(controller.ErrorText.transform, new Vector2(0.5f, 1), new Vector2(0, -200));
            var inventoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/InventoryPanel.prefab");
            BuildWorldActions(controller, hud, info.transform);
            var inventory = (GameObject)PrefabUtility.InstantiatePrefab(inventoryPrefab);
            inventory.transform.SetParent(hud, false);
            controller.Inventory = inventory.GetComponent<InventoryPanel>();
            BuildProgression(controller, hud);
            controller.FloatingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/FloatingText.prefab").GetComponent<FloatingText>();
            var beam = Panel("Beam", hud, new Vector2(0, 50), new Vector2(160, 760), new Color(0.9f, 0.9f, 0.6f, 0.45f));
            controller.Beam = beam.gameObject;
            controller.Beam.SetActive(false);
            controller.Announcement = Text("Announcement", hud, "", new Vector2(0, 40), new Vector2(1300, 100), 38, Paper);
            var backdrop = Panel("TalentBackdrop", hud, Vector2.zero, new Vector2(1600, 900), new Color(0.08f, 0.13f, 0.1f, 0.8f));
            Stretch((RectTransform)backdrop.transform);
            var talent = Frame("TalentPanel", backdrop.transform, Vector2.zero, new Vector2(820, 620), Paper);
            Text("Title", talent.transform, "Thiên Phú Tiên Thiên", new Vector2(0, 235), new Vector2(750, 65), 40, Jade);
            controller.TalentText = Text("Talent", talent.transform, "", new Vector2(0, 10), new Vector2(740, 330), 31);
            controller.ConfirmTalent = Button("Confirm", talent.transform, "Nhập thế", new Vector2(0, -240), new Vector2(330, 58));
            controller.TalentPanel = backdrop.gameObject;
            controller.TalentPanel.SetActive(false);
            controller.Controls = new Selectable[] { controller.ChangeCharacter, controller.Logout, controller.ConfirmTalent };
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, "Assets/Resources/Prefabs/PlayerHUD.prefab");
            SaveScene("WorldScene");
        }
    }
}
