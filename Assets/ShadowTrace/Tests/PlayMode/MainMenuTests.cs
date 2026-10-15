#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class MainMenuTests
    {
        private Scene previous;
        private Scene menuScene;
        private MainMenuController menu;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            previous = SceneManager.GetActiveScene();
            menuScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Log.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => menuScene.isLoaded);
            SceneManager.SetActiveScene(menuScene);
            menu = menuScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Menu test cleanup"));
            if (menuScene.IsValid() && menuScene.isLoaded) yield return SceneManager.UnloadSceneAsync(menuScene);
            var level = SceneManager.GetSceneByPath("Assets/Scenes/Level 1.unity");
            if (level.IsValid() && level.isLoaded && level != previous) yield return SceneManager.UnloadSceneAsync(level);
        }

        [UnityTest] public IEnumerator ConfirmationPanelsOpenCancelAndBlockBackgroundButtons()
        {
            Assert.IsFalse(menu.startPanel.activeSelf);
            Assert.IsFalse(menu.quitPanel.activeSelf);
            Assert.IsFalse(menu.settingsPanel.activeSelf);
            menu.startButton.onClick.Invoke();
            Assert.IsTrue(menu.startPanel.activeSelf);
            Assert.IsFalse(menu.quitButton.IsInteractable());
            Assert.IsFalse(menu.settingsButton.IsInteractable());
            menu.startNOButton.onClick.Invoke();
            Assert.IsFalse(menu.startPanel.activeSelf);
            Assert.IsTrue(menu.quitButton.IsInteractable());
            menu.quitButton.onClick.Invoke();
            Assert.IsTrue(menu.quitPanel.activeSelf);
            Assert.IsFalse(menu.startPanel.activeSelf);
            menu.quitNOButton.onClick.Invoke();
            Assert.IsFalse(menu.quitPanel.activeSelf);
            Assert.IsTrue(menu.settingsButton.IsInteractable());
            yield return null;
        }

        [UnityTest] public IEnumerator SettingsTabsKeepTheirHighlightAndReopenOnTutorial()
        {
            var tutorialNormal = menu.tutorialButton.image.sprite;
            var audioNormal = menu.audioButton.image.sprite;
            var tutorialActive = menu.tutorialButton.spriteState.pressedSprite;
            var audioActive = menu.audioButton.spriteState.pressedSprite;
            menu.settingsButton.onClick.Invoke();
            Assert.IsTrue(menu.settingsPanel.activeSelf);
            Assert.IsTrue(menu.tutorialPanel.activeSelf);
            Assert.IsFalse(menu.audioPanel.activeSelf);
            Assert.AreSame(tutorialActive, menu.tutorialButton.image.sprite);
            menu.audioButton.onClick.Invoke();
            Assert.IsFalse(menu.tutorialPanel.activeSelf);
            Assert.IsTrue(menu.audioPanel.activeSelf);
            Assert.IsTrue(menu.tutorialButton.gameObject.activeInHierarchy);
            Assert.IsTrue(menu.audioButton.gameObject.activeInHierarchy);
            Assert.AreSame(audioActive, menu.audioButton.image.sprite);
            var events = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(menu.audioButton.gameObject, events, ExecuteEvents.pointerExitHandler);
            yield return null;
            Assert.AreSame(audioActive, menu.audioButton.image.sprite);
            menu.tutorialButton.onClick.Invoke();
            Assert.IsTrue(menu.tutorialPanel.activeSelf);
            Assert.AreSame(audioNormal, menu.audioButton.image.sprite);
            menu.audioButton.onClick.Invoke();
            menu.backButton.onClick.Invoke();
            Assert.IsFalse(menu.settingsPanel.activeSelf);
            menu.settingsButton.onClick.Invoke();
            Assert.IsTrue(menu.tutorialPanel.activeSelf);
            Assert.IsFalse(menu.audioPanel.activeSelf);
            Assert.AreSame(tutorialActive, menu.tutorialButton.image.sprite);
        }

        [UnityTest] public IEnumerator StartConfirmationLoadsLevel1AndRestoresGameTime()
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded("Assets/Scenes/Level 1.unity"));
            menu.startButton.onClick.Invoke();
            Time.timeScale = 0;
            menu.startOKButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("Assets/Scenes/Level 1.unity", SceneManager.GetActiveScene().path);
            Assert.AreEqual(1, Time.timeScale);
        }
    }
}
#endif
