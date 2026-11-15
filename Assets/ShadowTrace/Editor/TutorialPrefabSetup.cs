using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace.Editor
{
    public static class TutorialPrefabSetup
    {
        public const string Folder = "Assets/Prefab/Tutorial";
        private const string Level = "Assets/Scenes/Level 1.unity";

        [MenuItem("Tools/Shadow Trace/Configure Editable Tutorial")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(Level);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var checkpoint = all.Select(t => t.GetComponent<LevelCheckpointController>()).First(c => c != null);
            var story = checkpoint.GetComponent<LevelStoryController>();
            var font = story.introduction.font;
            var normal = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/environment/UI/Button/Unselected.png").OfType<Sprite>().First();
            var selected = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/environment/UI/Button/Selected.png").OfType<Sprite>().First();
            var block = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ShadowTrace/Art/Block.png");
            var obstacle = CreateBlock("TutorialObstacle", block, new Vector2(0.65f, 1.4f));
            var step = CreateBlock("TutorialStep", block, new Vector2(0.9f, 0.6f));
            var settings = CreateLessons();
            var hudPrefab = CreateHUD(font, normal, selected, settings);
            var areaPrefab = CreateArea();
            var tutorial = checkpoint.GetComponent<FirstPlayTutorial>();
            if (tutorial == null) tutorial = checkpoint.gameObject.AddComponent<FirstPlayTutorial>();
            var canvas = all.Select(t => t.GetComponent<Canvas>()).First(c => c != null && c.isRootCanvas);
            var view = all.Select(t => t.GetComponent<TutorialHUDView>()).FirstOrDefault(v => v != null);
            if (view == null) view = ((GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, canvas.transform)).GetComponent<TutorialHUDView>();
            var area = all.Select(t => t.GetComponent<TutorialArea>()).FirstOrDefault(a => a != null);
            if (area == null) area = ((GameObject)PrefabUtility.InstantiatePrefab(areaPrefab.gameObject, scene)).GetComponent<TutorialArea>();
            var anchor = all.FirstOrDefault(t => t.name == "Tutorial Practice Anchor");
            if (anchor == null)
            {
                anchor = new GameObject("Tutorial Practice Anchor").transform;
                anchor.SetParent(checkpoint.transform, false);
                anchor.position = new Vector3(96.5f, 23.6f, 0);
            }
            tutorial.view = view; tutorial.settings = settings; tutorial.area = area;
            tutorial.obstaclePrefab = obstacle; tutorial.climbStepPrefab = step; tutorial.practiceAnchor = anchor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial);
            EditorUtility.SetDirty(tutorial);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Editable tutorial configured in " + Folder);
        }

        private static GameObject CreateBlock(string name, Sprite sprite, Vector2 size)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size;
            renderer.color = new Color(0.88f, 0.88f, 0.86f); renderer.sortingOrder = 20;
            root.GetComponent<BoxCollider2D>().size = size;
            var edges = new[] { new Vector4(0, size.y / 2, size.x, 0.04f), new Vector4(0, -size.y / 2, size.x, 0.04f),
                new Vector4(-size.x / 2, 0, 0.04f, size.y), new Vector4(size.x / 2, 0, 0.04f, size.y) };
            foreach (var edge in edges)
            {
                var child = new GameObject("Dark Outline", typeof(SpriteRenderer)); child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(edge.x, edge.y);
                var line = child.GetComponent<SpriteRenderer>(); line.sprite = sprite; line.drawMode = SpriteDrawMode.Sliced;
                line.size = new Vector2(edge.z, edge.w); line.color = new Color(0.2f, 0.2f, 0.22f); line.sortingOrder = 21;
            }
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return asset;
        }

        private static TutorialArea CreateArea()
        {
            string path = Folder + "/TutorialArea.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<TutorialArea>();
            var root = new GameObject("Tutorial Area", typeof(TutorialArea));
            var area = root.GetComponent<TutorialArea>();
            area.ranges = new[] {
                Range("影子基础练习", TutorialStep.Introduction, TutorialStep.Climb, 90.5f, 108.8f),
                Range("地刺观察", TutorialStep.ApproachSpikes, TutorialStep.ObserveSpikes, 90.5f, 109.2f),
                Range("疾行跨越", TutorialStep.Sprint, TutorialStep.Sprint, 90.5f, 124.5f),
                Range("旗帜机关与大门", TutorialStep.Door, TutorialStep.Door, 114.5f, 137),
                Range("存档路灯", TutorialStep.Lamp, TutorialStep.Lamp, 129, 165)
            };
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return asset.GetComponent<TutorialArea>();
        }

        private static TutorialArea.StageRange Range(string label, TutorialStep first, TutorialStep last, float left, float right)
            => new TutorialArea.StageRange { label = label, first = first, last = last, bounds = new Rect(left, 10, right - left, 25) };

        private static TutorialLessonSettings CreateLessons()
        {
            string path = Folder + "/TutorialLessons.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TutorialLessonSettings>(path);
            if (existing != null) return existing;
            var data = ScriptableObject.CreateInstance<TutorialLessonSettings>();
            data.lessons = new[] {
                Lesson(TutorialStep.Stay, "静止型影子", "按 E，开始准备影子。", "保持不动，再按 E 结束。"),
                Lesson(TutorialStep.DeleteOne, "消除影子", "按数字键选中影子，再按一次 R。"),
                Lesson(TutorialStep.Follow, "跟随型影子", "按 E，准备留下跟随型影子。", "先移动，再停住半秒，按 E 结束。"),
                Lesson(TutorialStep.FollowPractice, "观察跟随", "向左、向右移动，再按空格跳跃。"),
                Lesson(TutorialStep.Avoid, "避让型影子", "按 E，准备留下避让型影子。", "先向右，再向左，按 E 结束。"),
                Lesson(TutorialStep.AvoidObserve, "观察避让", "观察影子碰到障碍后转向。"),
                Lesson(TutorialStep.ClearAll, "清除全部", "快速按两次 R，清除所有影子。"),
                Lesson(TutorialStep.Approach, "趋近型影子", "按 E，准备留下趋近型影子。", "按住 D 向右移动，同时按 E 结束。"),
                Lesson(TutorialStep.Climb, "影子也是落脚点", "借助矮台，跳到停止的影子头顶。"),
                Lesson(TutorialStep.ApproachSpikes, "影子探路", "走到 ▼ 标记处，按 E 准备。", "按住 D，立即按 E，留下朝右的影子。"),
                Lesson(TutorialStep.ObserveSpikes, "观察地刺", "留在安全地面，观察影子接触地刺。"),
                Lesson(TutorialStep.Sprint, "疾行跳跃", "按住 D + Shift 疾行，在边缘前按空格。"),
                Lesson(TutorialStep.Door, "旗帜机关", "让影子踩住带旗帜的机关。"),
                Lesson(TutorialStep.Lamp, "路灯存档", "让影子触碰路灯，点亮后即可存档。"),
                Lesson(TutorialStep.Resume, "返回与续行", "按 Q 返回菜单，选择“续行”回到这里。"),
                Lesson(TutorialStep.Zoom, "调整视野", "滚动鼠标滚轮：向下拉远，向上拉近。"),
                Lesson(TutorialStep.Finish, "开始探索", "多多观察，才能顺利通关。加油！")
            };
            data.handbook = "操作手册\n\nA / D 移动　Shift 疾行　空格跳跃\nE 开始准备；准备时再按取消，记录时再按结束\n静止型：不移动　趋近型：始终同向移动\n跟随型：移动后停住　避让型：改变方向\n跟随型响应你的操作，不会自动寻路\n数字键选影子　R 清除选中　快速两次 R 清除全部\n停止的趋近型影子可踩踏；地刺会消除影子\n旗帜机关控制大门，有些需要两个同时激活\n解锁后靠近按 F；首次通行清除影子，大门永久开启\n让影子触碰路灯存档，已通过的大门也会保存\nQ 返回菜单，续行恢复进度　H 暂停\n滚轮向下拉远、向上拉近";
            AssetDatabase.CreateAsset(data, path); return data;
        }

        private static TutorialLessonSettings.Lesson Lesson(TutorialStep step, string title, string text, string recording = "")
            => new TutorialLessonSettings.Lesson { step = step, title = title, instruction = text, recordingInstruction = recording };

        private static GameObject CreateHUD(Font font, Sprite normal, Sprite selected, TutorialLessonSettings settings)
        {
            string path = Folder + "/TutorialHUD.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Tutorial HUD", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(TutorialHUDView));
            var rootRect = root.GetComponent<RectTransform>(); rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Canvas>().overrideSorting = true; root.GetComponent<Canvas>().sortingOrder = 120;
            var view = root.GetComponent<TutorialHUDView>(); view.unselected = normal; view.selected = selected;
            var panel = Rect("Guidance", root.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(820, 218));
            var background = panel.AddComponent<Image>(); background.color = new Color(0.055f, 0.055f, 0.065f, 0.94f); background.raycastTarget = false;
            var border = panel.AddComponent<Outline>(); border.effectColor = new Color(0.75f, 0.75f, 0.72f, 0.7f); border.effectDistance = Vector2.one;
            view.guidance = panel;
            view.title = Text("Title", panel.transform, font, new Vector2(20, -10), new Vector2(780, 32), 26);
            view.title.alignment = TextAnchor.MiddleCenter; view.title.text = "静止型影子";
            view.instruction = Text("Instruction", panel.transform, font, new Vector2(20, -48), new Vector2(780, 62), 25);
            view.instruction.alignment = TextAnchor.MiddleCenter; view.instruction.text = "按 E，开始准备影子。";
            view.status = Text("Status", panel.transform, font, new Vector2(20, -112), new Vector2(780, 25), 20);
            view.status.alignment = TextAnchor.MiddleCenter; view.status.color = new Color(0.8f, 0.8f, 0.8f);
            view.feedback = Text("Feedback", panel.transform, font, new Vector2(20, -140), new Vector2(780, 26), 20);
            view.feedback.alignment = TextAnchor.MiddleCenter;
            view.retry = Button("重试本步", panel.transform, font, normal, selected, new Vector2(174, -172), new Vector2(146, 36));
            view.skip = Button("跳过教学", panel.transform, font, normal, selected, new Vector2(337, -172), new Vector2(146, 36));
            view.next = Button("继续", panel.transform, font, normal, selected, new Vector2(500, -172), new Vector2(146, 36));
            view.manual = Button("操作手册", root.transform, font, normal, selected, Vector2.zero, new Vector2(184, 58));
            var manualRect = view.manual.GetComponent<RectTransform>(); manualRect.anchorMin = manualRect.anchorMax = Vector2.zero;
            manualRect.pivot = Vector2.zero; manualRect.anchoredPosition = new Vector2(24, 24);
            view.pointer = Text("Focus", root.transform, font, Vector2.zero, new Vector2(70, 50), 34); view.pointer.alignment = TextAnchor.MiddleCenter;
            view.pointer.text = "▼";
            view.pointer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var book = Rect("Handbook", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 640));
            book.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.05f, 0.98f);
            var bookText = Text("Controls", book.transform, font, new Vector2(30, -24), new Vector2(800, 530), 23); bookText.text = settings.handbook;
            view.handbookText = bookText;
            view.handbook = book;
            view.close = Button("关闭手册", book.transform, font, normal, selected, new Vector2(338, -566), new Vector2(184, 52));
            book.SetActive(false);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return asset;
        }

        private static GameObject Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size; return go;
        }

        private static Text Text(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size).AddComponent<Text>();
            label.font = font; label.fontSize = fontSize; label.color = Color.white; label.raycastTarget = false;
            label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }

        private static Button Button(string name, Transform parent, Font font, Sprite normal, Sprite selected, Vector2 position, Vector2 size)
        {
            var go = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            var image = go.AddComponent<Image>(); image.sprite = normal;
            var button = go.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = normal, pressedSprite = selected, selectedSprite = normal, disabledSprite = normal };
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var label = Text("Label", go.transform, font, Vector2.zero, size, 20); label.alignment = TextAnchor.MiddleCenter; label.text = name;
            return button;
        }
    }
}
