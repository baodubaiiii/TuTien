using TMPro;
using UnityEngine;

namespace TuTien.Client.Editor
{
    public static partial class DemoBuilder
    {
        private static Transform Dialog(string name, Transform parent, string title, out GameObject backdrop)
        {
            var root = Panel(name + "Backdrop", parent, Vector2.zero, new Vector2(1600, 900), new Color(0.08f, 0.13f, 0.1f, 0.75f));
            Stretch((RectTransform)root.transform);
            backdrop = root.gameObject;
            var window = Frame(name, root.transform, Vector2.zero, new Vector2(760, 480), Paper);
            Text("Title", window.transform, title, new Vector2(0, 170), new Vector2(680, 65), 35, Jade);
            root.gameObject.SetActive(false);
            return window.transform;
        }

        private static void BuildWorldActions(WorldScene controller, Transform hud, Transform info)
        {
            ((RectTransform)info).sizeDelta = new Vector2(420, 250);
            Anchor(info, new Vector2(0, 1), new Vector2(245, -150));
            ((RectTransform)controller.Name.transform).anchoredPosition = new Vector2(0, 85);
            ((RectTransform)controller.Realm.transform).anchoredPosition = new Vector2(0, 47);
            ((RectTransform)controller.HpFill.transform.parent).anchoredPosition = new Vector2(0, -6);
            ((RectTransform)controller.MpFill.transform.parent).anchoredPosition = new Vector2(0, -49);
            controller.TuVi = Label("TuVi", info, "Tu vi: 0", new Vector2(0, -100), new Vector2(370, 34), 21, Jade);
            for (int i = 0; i < controller.EffectSlots.Length; i++)
            {
                Anchor(controller.EffectSlots[i].transform, new Vector2(0, 1), new Vector2(80 + i * 88, -330));
            }
            controller.InteractButton = Button("Interact", hud, "Tương tác [E]", Vector2.zero, new Vector2(260, 58), true);
            Anchor(controller.InteractButton.transform, Vector2.zero, new Vector2(390, 210));
            controller.ZoneButton = Button("Zones", hud, "Đổi khu", Vector2.zero, new Vector2(220, 48), true);
            Anchor(controller.ZoneButton.transform, Vector2.one, new Vector2(-385, -205));
            var npc = Dialog("NpcDialog", hud, "Trò chuyện", out controller.NpcPanel);
            controller.NpcName = Text("NpcName", npc, "", new Vector2(0, 110), new Vector2(680, 45), 27);
            controller.NpcText = Text("NpcText", npc, "", new Vector2(0, 5), new Vector2(670, 140), 25);
            controller.NpcZones = Button("NpcZones", npc, "Đổi khu vực", new Vector2(-170, -150), new Vector2(290, 54));
            controller.NpcClose = Button("NpcClose", npc, "Đóng", new Vector2(180, -150), new Vector2(260, 54), true);
            controller.NpcError = Text("Error", npc, "", new Vector2(0, -215), new Vector2(680, 40), 20, Error);
            var zones = Dialog("ZoneDialog", hud, "Chọn khu vực", out controller.ZonePanel);
            Text("Hint", zones, "Số đạo hữu hiện tại / sức chứa", new Vector2(0, 80), new Vector2(600, 45), 23, Muted);
            controller.ZoneList = Dropdown("ZoneList", zones, new Vector2(0, 0));
            controller.ZoneConfirm = Button("ChangeZone", zones, "Chuyển khu", new Vector2(-170, -150), new Vector2(290, 54));
            controller.ZoneClose = Button("ZoneClose", zones, "Đóng", new Vector2(180, -150), new Vector2(260, 54), true);
            controller.ZoneError = Text("Error", zones, "", new Vector2(0, -215), new Vector2(680, 40), 20, Error);
            var death = Dialog("DeathDialog", hud, "Hồi sinh", out controller.DeathPanel);
            controller.DeathText = Text("DeathText", death, "", new Vector2(0, 0), new Vector2(670, 220), 27);
            controller.ReviveButton = Button("Revive", death, "Trở về thôn", new Vector2(0, -165), new Vector2(340, 60));
        }
    }
}
