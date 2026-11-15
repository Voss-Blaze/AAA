using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTrace.Tests
{
    public sealed class TutorialAssetTests
    {
        private const string Folder = "Assets/Prefab/Tutorial/";

        [Test] public void HUDUsesEditablePrefabAndRequestedButtonResources()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TutorialHUD.prefab");
            var view = root.GetComponent<TutorialHUDView>();
            Assert.AreEqual("Unselected_0", view.unselected.name);
            Assert.AreEqual("Selected_0", view.selected.name);
            Assert.AreEqual(new Vector2(0.5f, 1), view.guidance.GetComponent<RectTransform>().anchorMin);
            Assert.AreEqual(Vector2.zero, view.manual.GetComponent<RectTransform>().anchorMin);
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                Assert.AreSame(view.unselected, button.image.sprite);
                Assert.AreSame(view.selected, button.spriteState.pressedSprite);
            }
            Assert.IsTrue(root.GetComponentsInChildren<UnityEngine.UI.Text>(true).All(t => !t.raycastTarget));
        }

        [Test] public void PracticeBlocksUseNeutralWhiteAndEditableColliders()
        {
            foreach (var name in new[] { "TutorialObstacle", "TutorialStep" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
                var renderer = root.GetComponent<SpriteRenderer>();
                Assert.Greater(renderer.color.r, 0.8f);
                Assert.Less(Mathf.Abs(renderer.color.r - renderer.color.b), 0.05f);
                Assert.AreEqual(root.GetComponent<BoxCollider2D>().size, renderer.size);
                Assert.AreEqual(4, root.transform.childCount);
            }
            var settings = AssetDatabase.LoadAssetAtPath<TutorialLessonSettings>(Folder + "TutorialLessons.asset");
            foreach (TutorialStep step in System.Enum.GetValues(typeof(TutorialStep)))
            {
                if (step == TutorialStep.Introduction || step == TutorialStep.Complete) continue;
                var lesson = settings.Find(step);
                Assert.IsNotNull(lesson, step.ToString());
                Assert.LessOrEqual(lesson.instruction.Length, 45);
                Assert.LessOrEqual(lesson.recordingInstruction.Length, 45);
            }
        }

        [Test] public void LevelReferencesSceneHUDAndAreaBeforeEnteringPlayMode()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Level 1.unity");
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Level 1.unity", OpenSceneMode.Additive);
            try
            {
                var tutorial = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FirstPlayTutorial>(true)).Single();
                Assert.IsNotNull(tutorial.view);
                Assert.AreEqual(Folder + "TutorialHUD.prefab", PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tutorial.view));
                Assert.AreEqual(Folder + "TutorialArea.prefab", PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tutorial.area));
                Assert.AreEqual(Folder + "TutorialLessons.asset", AssetDatabase.GetAssetPath(tutorial.settings));
                Assert.IsNotNull(tutorial.practiceAnchor);
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
