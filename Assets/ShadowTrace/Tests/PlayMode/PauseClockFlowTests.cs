#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShadowTrace.Tests
{
    public sealed class PauseClockFlowTests
    {
        private const string Level = "Assets/Scenes/Level 1.unity";
        private const string Menu = "Assets/Scenes/Log.unity";
        private Scene previous;
        private SaveGameService oldSaves;
        private string directory;
        private static T Find<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            oldSaves = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/PauseClockTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory);
            previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.LoadSceneInPlayMode(Level, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => scene.isLoaded);
            SceneManager.SetActiveScene(scene);
            PrepareLevel(); yield return null;
        }

        private static void PrepareLevel()
        {
            Find<LevelStoryController>().enabled = false;
            Find<LevelCheckpointController>().player.Body.gravityScale = 0;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            foreach (var overlay in Object.FindObjectsOfType<SceneFadeOverlay>()) Object.Destroy(overlay.gameObject);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Pause clock cleanup"));
            foreach (var path in new[] { Level, Menu })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previous) yield return SceneManager.UnloadSceneAsync(scene);
            }
            SaveGameService.Current.BeginNewGame();
            if (Directory.Exists(directory)) Directory.Delete(directory);
            SaveGameService.Current = oldSaves;
        }

        private static SaveGameService.Checkpoint Save()
        {
            var controller = Find<LevelCheckpointController>();
            var lamp = controller.lamps[0];
            Assert.IsTrue(controller.SafePosition(lamp.RespawnPosition, out var position));
            var data = new SaveGameService.Checkpoint { scenePath = Level, checkpointId = lamp.checkpointId, position = position };
            Assert.IsTrue(SaveGameService.Current.TrySave(data));
            return data;
        }

        [Test] public void ContinueButtonRestoresGameAndAllPauseButtonsDisableKeyboardSubmitSelection()
        {
            var pause = Find<GamePauseController>();
            Assert.IsNotNull(pause.checkpointButton); Assert.IsNotNull(pause.restartButton);
            pause.TogglePause(); Assert.IsTrue(pause.blackout.activeSelf); Assert.AreEqual(0, Time.timeScale);
            pause.continueButton.onClick.Invoke();
            Assert.IsFalse(pause.IsPaused); Assert.IsFalse(pause.blackout.activeSelf); Assert.AreEqual(1, Time.timeScale);
            foreach (var button in new[] { pause.continueButton, pause.checkpointButton, pause.restartButton })
                Assert.AreEqual(Navigation.Mode.None, button.navigation.mode);
            var scene = SceneManager.GetActiveScene(); pause.restartButton.onClick.Invoke();
            Assert.AreEqual(scene, SceneManager.GetActiveScene(), "Hidden buttons must not restart gameplay");
        }

        [UnityTest] public IEnumerator ReturnToCheckpointWithoutSaveStartsFreshAndReleasesPause()
        {
            var controller = Find<LevelCheckpointController>();
            var origin = controller.player.Body.position;
            controller.player.Body.position += new Vector2(20, 10); Physics2D.SyncTransforms();
            var pause = Find<GamePauseController>(); pause.TogglePause(); pause.checkpointButton.onClick.Invoke();
            yield return null; yield return null;
            PrepareLevel();
            Assert.Less(Vector2.Distance(origin, Find<LevelCheckpointController>().player.Body.position), 0.15f);
            Assert.AreEqual(1, Time.timeScale); Assert.IsFalse(Find<GamePauseController>().IsPaused);
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
        }

        [UnityTest] public IEnumerator ReturnToCheckpointRestoresSavedLampAndPreservesSave()
        {
            var data = Save();
            var pause = Find<GamePauseController>(); pause.TogglePause(); pause.checkpointButton.onClick.Invoke();
            yield return null; yield return null;
            PrepareLevel();
            var controller = Find<LevelCheckpointController>();
            Assert.Less(Vector2.Distance(data.position, controller.player.Body.position), 0.15f);
            Assert.IsTrue(controller.lamps.Single(l => l.checkpointId == data.checkpointId).IsLit);
            Assert.IsTrue(SaveGameService.Current.TryRead(out _)); Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(Find<GamePauseController>().blackout.activeSelf); Assert.AreEqual(0, Find<ShadowManager>().Count);
        }

        [UnityTest] public IEnumerator RestartDiscardsCheckpointAndRestoresInitialSpawn()
        {
            var origin = Find<LevelCheckpointController>().player.Body.position;
            Save();
            var pause = Find<GamePauseController>(); pause.TogglePause(); pause.restartButton.onClick.Invoke();
            yield return null; yield return null;
            PrepareLevel();
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
            Assert.Less(Vector2.Distance(origin, Find<LevelCheckpointController>().player.Body.position), 0.15f);
            Assert.IsTrue(Find<LevelCheckpointController>().lamps.All(l => !l.IsLit));
            Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest] public IEnumerator DeathKeepsBlackOverlayAcrossMenuLoadThenRevealsWithoutDeletingSave()
        {
            Save();
            var controller = Find<LevelCheckpointController>();
            controller.deathFadeDuration = 0.04f; controller.deathBlackDuration = 0.02f; controller.deathRevealDuration = 0.2f;
            bool coveredAtLoad = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) =>
            {
                if (scene.path != Menu) return;
                var overlay = Object.FindObjectOfType<SceneFadeOverlay>();
                coveredAtLoad = overlay != null && overlay.GetComponentInChildren<Image>().color.a >= 0.99f;
            };
            SceneManager.sceneLoaded += loaded;
            try
            {
                var pause = Find<GamePauseController>();
                controller.life.Die();
                Assert.IsTrue(controller.deathBlackout.gameObject.activeSelf);
                Assert.IsFalse(pause.blackout.activeSelf);
                float deadline = Time.realtimeSinceStartup + 8;
                while (SceneManager.GetActiveScene().path != Menu && Time.realtimeSinceStartup < deadline)
                {
                    if (pause != null) Assert.IsFalse(pause.blackout.activeSelf);
                    yield return null;
                }
                Assert.AreEqual(Menu, SceneManager.GetActiveScene().path);
                Assert.IsTrue(coveredAtLoad, "The scene must load behind a fully opaque persistent overlay");
                while (Object.FindObjectOfType<SceneFadeOverlay>() != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsNull(Object.FindObjectOfType<SceneFadeOverlay>());
                Assert.IsTrue(SaveGameService.Current.TryRead(out _)); Assert.AreEqual(1, Time.timeScale);
            }
            finally { SceneManager.sceneLoaded -= loaded; }
        }

        [UnityTest] public IEnumerator DeathFromGameplayOrPauseNeverShowsPauseMenu()
        {
            var controller = Find<LevelCheckpointController>();
            var pause = Find<GamePauseController>();
            Assert.AreNotSame(pause.blackout, controller.deathBlackout.gameObject);
            Assert.IsNull(controller.deathBlackout.GetComponentInChildren<Button>(true));
            // Reproduce the old shared reference, including death while the pause menu is open.
            controller.deathBlackout = pause.blackout.GetComponent<Image>();
            pause.TogglePause(); Assert.IsTrue(pause.blackout.activeSelf);
            controller.deathFadeDuration = 0.2f;
            controller.deathBlackDuration = 10;
            controller.life.Die();
            Assert.IsFalse(pause.blackout.activeSelf);
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(0, Time.timeScale);
            Assert.AreNotSame(pause.blackout, controller.deathBlackout.gameObject);
            Assert.IsTrue(controller.deathBlackout.gameObject.activeSelf);
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                Assert.IsFalse(pause.blackout.activeSelf);
                Assert.IsFalse(pause.continueButton.gameObject.activeInHierarchy);
            }
        }

        [UnityTest] public IEnumerator ClockStartsOnPlayerPressRotatesAllCollidersClockwiseAndRespectsPause()
        {
            var clock = Find<ClockMechanism>();
            // Exercise this configuration without depending on the designer's current Inspector choices.
            clock.playerCanPress = true; clock.shadowCanPress = false; clock.mode = ClockTriggerMode.Latched;
            Assert.IsNull(clock.GetComponent<ShadowMoverPlate>());
            Assert.IsTrue(clock.GetComponent<BoxCollider2D>().isTrigger);
            Assert.IsTrue(clock.playerCanPress); Assert.IsFalse(clock.shadowCanPress);
            var player = Find<LevelCheckpointController>().player;
            var hands = new[] { clock.hour, clock.minute, clock.second };
            var start = hands.Select(h => h.pivot.rotation).ToArray();
            foreach (var hand in hands)
            {
                Assert.AreEqual(RigidbodyType2D.Kinematic, hand.pivot.bodyType);
                Assert.IsTrue(hand.pivot.GetComponentsInChildren<Collider2D>().All(c => c.attachedRigidbody == hand.pivot));
            }
            player.Body.position = clock.pressArea.bounds.center; Physics2D.SyncTransforms(); clock.Refresh();
            Assert.IsTrue(clock.IsRunning);
            player.Body.position = new Vector2(300, 300); Physics2D.SyncTransforms(); clock.Refresh();
            Assert.IsTrue(clock.IsRunning, "Default mode remains latched after leaving");
            yield return new WaitForSeconds(0.12f);
            for (int i = 0; i < hands.Length; i++) Assert.Less(Mathf.DeltaAngle(start[i], hands[i].pivot.rotation), -0.05f);
            Time.timeScale = 0; float angle = clock.second.pivot.rotation;
            yield return new WaitForSecondsRealtime(0.1f); Assert.AreEqual(angle, clock.second.pivot.rotation);
        }

        [UnityTest] public IEnumerator ClockPressModeAndActorFiltersAreEditable()
        {
            var clock = Find<ClockMechanism>();
            clock.mode = ClockTriggerMode.WhilePressed;
            clock.playerCanPress = true; clock.shadowCanPress = false;
            var player = Find<LevelCheckpointController>().player;
            player.Body.position = clock.pressArea.bounds.center; Physics2D.SyncTransforms(); clock.Refresh();
            Assert.IsTrue(clock.IsRunning);
            player.Body.position = new Vector2(300, 300); Physics2D.SyncTransforms(); clock.Refresh();
            Assert.IsFalse(clock.IsRunning);
            var shadow = new GameObject("Clock test shadow").AddComponent<ShadowActor>();
            shadow.Body.bodyType = RigidbodyType2D.Kinematic;
            shadow.Body.position = clock.pressArea.bounds.center; Physics2D.SyncTransforms(); clock.Refresh();
            Assert.IsFalse(clock.IsRunning);
            clock.shadowCanPress = true; clock.Refresh(); Assert.IsTrue(clock.IsRunning);
            shadow.enabled = false; clock.Refresh(); Assert.IsFalse(clock.IsRunning);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0, clock.second.pivot.angularVelocity);
        }
    }
}
#endif
