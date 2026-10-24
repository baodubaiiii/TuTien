using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client.Editor
{
    public static partial class DemoBuilder
    {
        private static void BuildProgression(WorldScene controller, Transform hud)
        {
            controller.ProgressionButtons = new Button[3];
            controller.SkillButtons = new Button[3];
            string[] titles = { "Nhiệm vụ [J]", "Kỹ năng [K]", "Tu luyện [P]" };
            for (int i = 0; i < 3; i++)
            {
                controller.ProgressionButtons[i] = Button("Progression" + i, hud, titles[i], Vector2.zero, new Vector2(200, 48), true);
                Anchor(controller.ProgressionButtons[i].transform, Vector2.one, new Vector2(-145, -270 - i * 60));
                controller.SkillButtons[i] = Button("SkillSlot" + i, hud, "[" + (i + 1) + "] Trống", Vector2.zero, new Vector2(175, 76), true);
                controller.SkillButtons[i].GetComponentInChildren<TMP_Text>().fontSize = 19;
                Anchor(controller.SkillButtons[i].transform, new Vector2(0.5f, 0), new Vector2(-190 + i * 190, 183));
            }
            controller.QuestTracker = Label("QuestTracker", hud, "", Vector2.zero, new Vector2(540, 100), 22, Paper);
            Anchor(controller.QuestTracker.transform, new Vector2(0.5f, 1), new Vector2(0, -75));
            controller.TrialText = Text("Trial", hud, "", Vector2.zero, new Vector2(600, 90), 27, Paper);
            Anchor(controller.TrialText.transform, new Vector2(0.5f, 1), new Vector2(0, -290));
            var root = Panel("ProgressionBackdrop", hud, Vector2.zero, new Vector2(1600, 900), new Color(0.08f, 0.13f, 0.1f, 0.78f));
            Stretch((RectTransform)root.transform);
            controller.ProgressionPanel = root.gameObject;
            var window = Frame("ProgressionPanel", root.transform, Vector2.zero, new Vector2(940, 680), Paper);
            Text("Title", window.transform, "Đạo lộ", new Vector2(-300, 285), new Vector2(240, 60), 35, Jade);
            controller.ProgressionClose = Button("ProgressionClose", window.transform, "Đóng [Esc]", new Vector2(320, 285), new Vector2(210, 48), true);
            controller.ProgressionTabs = new Button[3];
            controller.ProgressionPages = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                controller.ProgressionTabs[i] = Button("Tab" + i, window.transform, titles[i], new Vector2(-290 + i * 290, 215), new Vector2(275, 50), true);
                controller.ProgressionPages[i] = Rect("Page" + i, window.transform, new Vector2(0, -25), new Vector2(860, 420)).gameObject;
            }
            var task = controller.ProgressionPages[0].transform;
            controller.TaskList = Dropdown("Tasks", task, new Vector2(0, 145));
            ((RectTransform)controller.TaskList.transform).sizeDelta = new Vector2(800, 55);
            var taskViewport = Panel("TaskViewport", task, new Vector2(0, -5), new Vector2(800, 230), Paper);
            taskViewport.gameObject.AddComponent<RectMask2D>();
            var taskScroll = taskViewport.gameObject.AddComponent<ScrollRect>();
            controller.TaskDetail = Label("TaskDetail", taskViewport.transform, "", Vector2.zero, new Vector2(780, 220), 25);
            var taskContent = controller.TaskDetail.rectTransform;
            taskContent.anchorMin = new Vector2(0, 1);
            taskContent.anchorMax = Vector2.one;
            taskContent.pivot = new Vector2(0.5f, 1);
            taskContent.anchoredPosition = Vector2.zero;
            taskContent.sizeDelta = new Vector2(-20, 220);
            controller.TaskDetail.overflowMode = TextOverflowModes.Overflow;
            taskContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            taskScroll.viewport = (RectTransform)taskViewport.transform;
            taskScroll.content = taskContent;
            taskScroll.horizontal = false;
            taskScroll.movementType = ScrollRect.MovementType.Clamped;
            controller.TaskAction = Button("TaskAction", task, "Nhận nhiệm vụ", new Vector2(0, -175), new Vector2(360, 55));
            var skill = controller.ProgressionPages[1].transform;
            controller.SkillList = Dropdown("Skills", skill, new Vector2(0, 145));
            ((RectTransform)controller.SkillList.transform).sizeDelta = new Vector2(800, 55);
            controller.SkillDetail = Label("SkillDetail", skill, "", new Vector2(0, 5), new Vector2(800, 210), 23);
            controller.LearnButton = Button("LearnSkill", skill, "Học kỹ năng", new Vector2(0, -135), new Vector2(330, 50));
            controller.AssignButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                controller.AssignButtons[i] = Button("Assign" + i, skill, "Ô " + (i + 1), new Vector2(-280 + i * 280, -215), new Vector2(265, 76), true);
                controller.AssignButtons[i].GetComponentInChildren<TMP_Text>().fontSize = 21;
            }
            var realm = controller.ProgressionPages[2].transform;
            controller.RealmDetail = Label("RealmDetail", realm, "", new Vector2(0, 30), new Vector2(810, 300), 24);
            controller.AdvanceButton = Button("AdvanceRealm", realm, "Tiến cấp", new Vector2(-205, -195), new Vector2(360, 55));
            controller.CancelTrialButton = Button("CancelTrial", realm, "Dừng độ kiếp", new Vector2(210, -195), new Vector2(330, 55), true);
            controller.ProgressionError = Text("Error", window.transform, "", new Vector2(0, -305), new Vector2(850, 45), 21, Error);
            root.gameObject.SetActive(false);
            var npc = controller.NpcClose.transform.parent;
            ((RectTransform)controller.NpcZones.transform).anchoredPosition = new Vector2(-170, -170);
            ((RectTransform)controller.NpcClose.transform).anchoredPosition = new Vector2(180, -170);
            controller.NpcTasks = Button("NpcTasks", npc, "Nhiệm vụ", new Vector2(-170, -105), new Vector2(290, 50), true);
            controller.NpcSkills = Button("NpcSkills", npc, "Truyền công", new Vector2(180, -105), new Vector2(260, 50), true);
        }
    }
}
