#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class CheckpointFlowTests
    {
        private const string MenuPath = "Assets/Scenes/Log.unity";
        private const string LevelPath = "Assets/Scenes/Level 1.unity";
        private SaveGameService previousSaves;
        private string directory;
        private Scene previous;

        private static T Find<T>() where T : Component
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            previousSaves = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/CheckpointFlowTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory);
            previous = SceneManager.GetActiveScene();
            var menu = EditorSceneManager.LoadSceneInPlayMode(MenuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => menu.isLoaded);
            SceneManager.SetActiveScene(menu); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Checkpoint test cleanup"));
            foreach (var path in new[] { MenuPath, LevelPath })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previous) yield return SceneManager.UnloadSceneAsync(scene);
            }
            SaveGameService.Current.BeginNewGame();
            if (Directory.Exists(directory)) Directory.Delete(directory);
            SaveGameService.Current = previousSaves;
        }

        private static IEnumerator NewGame()
        {
            var menu = Find<MainMenuController>();
            menu.startButton.onClick.Invoke(); menu.startOKButton.onClick.Invoke();
            yield return null; yield return null;
        }

        private static IEnumerator Light(ShadowLamp lamp)
        {
            var manager = Find<ShadowManager>();
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, manager.player.Body.position, 0));
            var shadow = manager.Actors.First(a => a != null);
            var bounds = lamp.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = Vector2.Scale(shadow.Shape.offset, shadow.transform.lossyScale);
            shadow.Body.position = (Vector2)bounds.center - offset;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.IsTrue(lamp.IsLit);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var data));
            Assert.AreEqual(lamp.checkpointId, data.checkpointId);
            manager.ClearAll(); yield return null;
        }

        [UnityTest] public IEnumerator ResumeWithoutSaveClosesPopupAndShowsThreeSecondHint()
        {
            var menu = Find<MainMenuController>();
            menu.resumeButton.onClick.Invoke(); Assert.IsTrue(menu.resumePanel.activeSelf);
            menu.resumeNOButton.onClick.Invoke(); Assert.IsFalse(menu.resumePanel.activeSelf);
            Assert.IsFalse(menu.noSaveHint.activeSelf);
            menu.resumeButton.onClick.Invoke(); menu.resumeOKButton.onClick.Invoke();
            Assert.IsFalse(menu.resumePanel.activeSelf); Assert.IsTrue(menu.noSaveHint.activeSelf);
            Assert.AreEqual(3, menu.noSaveHintDuration);
            Assert.AreEqual(MenuPath, SceneManager.GetActiveScene().path);
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(menu.noSaveHint.activeSelf);
            yield return new WaitForSecondsRealtime(2.15f);
            Assert.IsFalse(menu.noSaveHint.activeSelf);
        }

        [UnityTest] public IEnumerator LitLampPersistsAcrossRestartAndResumeRestoresPositionAndLight()
        {
            yield return NewGame();
            var controller = Find<LevelCheckpointController>();
            yield return Light(controller.lamps[0]);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var data));
            SaveGameService.Current = new SaveGameService(directory);
            SceneManager.LoadScene(MenuPath); yield return null; yield return null;
            var menu = Find<MainMenuController>();
            menu.resumeButton.onClick.Invoke(); menu.resumeOKButton.onClick.Invoke();
            yield return null; yield return null;
            controller = Find<LevelCheckpointController>();
            Assert.That(Vector2.Distance(controller.player.Body.position, data.position), Is.LessThan(0.1f));
            Assert.IsTrue(controller.lamps.Single(l => l.checkpointId == data.checkpointId).IsLit);
            Assert.AreEqual(0, Find<ShadowManager>().Count);
        }

        [UnityTest] public IEnumerator NewLampOverwritesCheckpointAndDeathReturnsToMenuWithoutDeletingSave()
        {
            yield return NewGame();
            var controller = Find<LevelCheckpointController>();
            Assert.GreaterOrEqual(controller.lamps.Length, 2);
            yield return Light(controller.lamps[0]);
            yield return Light(controller.lamps[1]);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var data));
            Assert.AreEqual(controller.lamps[1].checkpointId, data.checkpointId);
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
            controller.deathFadeDuration = 0.04f; controller.deathBlackDuration = 0.04f;
            controller.life.Die();
            Assert.IsTrue(controller.deathBlackout.gameObject.activeSelf);
            Assert.AreEqual(0, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(MenuPath, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var afterDeath));
            Assert.AreEqual(data.checkpointId, afterDeath.checkpointId);
        }

        [UnityTest] public IEnumerator MissingLampInSaveReturnsToMenuWithHint()
        {
            SaveGameService.Current.TrySave(new SaveGameService.Checkpoint
            { scenePath = LevelPath, checkpointId = "removed lamp", position = Vector3.zero });
            var menu = Find<MainMenuController>();
            menu.resumeButton.onClick.Invoke(); menu.resumeOKButton.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(MenuPath, SceneManager.GetActiveScene().path);
            Assert.IsTrue(Find<MainMenuController>().noSaveHint.activeSelf);
        }
    }
}
#endif
