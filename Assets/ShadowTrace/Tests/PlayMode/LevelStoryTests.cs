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
    public sealed class LevelStoryTests
    {
        private Scene previous;
        private Scene level;
        private LevelStoryController story;
        private SaveGameService oldSaves;
        private string directory;
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            oldSaves = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/StoryTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory);
            previous = SceneManager.GetActiveScene();
            level = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Level 1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => level.isLoaded);
            SceneManager.SetActiveScene(level);
            story = Find<LevelStoryController>(level);
            Assert.AreEqual(2, story.introductionDelay);
            story.introductionDelay = 1000;
            story.fadeDuration = 0.03f;
            story.checkpoint.player.Body.gravityScale = 0;
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Story cleanup"));
            foreach (var path in new[] { "Assets/Scenes/Level 1.unity", "Assets/Scenes/Log.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previous) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = 1;
            SaveGameService.Current.BeginNewGame();
            if (Directory.Exists(directory)) Directory.Delete(directory);
            SaveGameService.Current = oldSaves;
        }

        private IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (story != null && !story.WaitingForAdvance && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(story != null && story.WaitingForAdvance, "Dialogue failed to become ready");
            yield return null; yield return null;
        }

        private IEnumerator FinishCurrent()
        {
            var text = story.CurrentText;
            float deadline = Time.realtimeSinceStartup + 5;
            while (story != null && story.CurrentText == text && Time.realtimeSinceStartup < deadline)
            {
                if (story.WaitingForAdvance)
                {
                    yield return null; yield return null;
                    story.Advance();
                }
                yield return null;
            }
            Assert.IsTrue(story == null || story.CurrentText != text, "Dialogue did not finish");
        }

        [UnityTest] public IEnumerator IntroductionWaitsForGameplayAndUsesEditableLines()
        {
            Assert.IsFalse(story.talkPanel.activeSelf);
            Assert.IsFalse(story.endingPanel.activeSelf);
            story.introductionDelay = 0.12f;
            story.pause.TogglePause();
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsFalse(story.IsPlaying);
            story.pause.TogglePause();
            yield return Ready();
            Assert.AreSame(story.introduction, story.CurrentText);
            Assert.IsFalse(story.CurrentText.text.Contains("\n"));
            Assert.AreEqual(0, Time.timeScale);
            story.pause.UsePauseKey(); story.pause.button.onClick.Invoke(); story.checkpoint.ExitLevel();
            Assert.IsFalse(story.pause.IsPaused);
            Assert.AreEqual(level, SceneManager.GetActiveScene());
            Assert.AreEqual(0, Time.timeScale);
            var first = story.CurrentText.text;
            story.Advance(); story.Advance();
            yield return Ready();
            Assert.AreNotEqual(first, story.CurrentText.text);
            yield return FinishCurrent();
            Assert.IsFalse(story.IsPlaying);
            Assert.IsFalse(story.talkPanel.activeSelf);
            Assert.AreEqual(1, Time.timeScale);
            story.Request(story.introduction); yield return null;
            Assert.IsFalse(story.IsPlaying, "Same stage must not replay");
        }

        [UnityTest] public IEnumerator ThreeActualDoorsTriggerTheirOwnStoriesOnce()
        {
            foreach (var binding in story.doorStories)
            {
                foreach (var plate in level.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ShadowPressurePlate>(true)))
                    if (plate.targetDoor == binding.door) { plate.targetDoor = null; binding.door.Unregister(plate); }
                var player = story.checkpoint.player;
                player.transform.position = binding.door.interactionArea.bounds.center;
                player.Body.position = player.transform.position;
                Physics2D.SyncTransforms();
                Assert.IsTrue(binding.door.TryPass(player));
                yield return Ready();
                Assert.AreSame(binding.text, story.CurrentText);
                yield return FinishCurrent();
                Assert.IsFalse(story.IsPlaying);
            }
        }

        [UnityTest] public IEnumerator DoorHintsFollowProximityAndHideDuringDialogue()
        {
            var hints = level.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoorInteractionPrompt>(true)).ToArray();
            Assert.AreEqual(4, hints.Length);
            foreach (var hint in hints)
            {
                Assert.AreEqual("[F]", hint.promptText);
                Assert.AreEqual(Color.white, hint.color);
                Assert.AreEqual("font", hint.font.name);
                var player = story.checkpoint.player;
                var door = hint.GetComponent<InteractiveDoor>();
                player.transform.position = door.interactionArea.bounds.center; player.Body.position = player.transform.position;
                Physics2D.SyncTransforms(); yield return null; yield return null;
                Assert.IsTrue(hint.Label.gameObject.activeSelf);
                player.transform.position += Vector3.up * 100; player.Body.position = player.transform.position;
                Physics2D.SyncTransforms(); yield return null; yield return null;
                Assert.IsFalse(hint.Label.gameObject.activeSelf);
            }
            story.Request(story.introduction); yield return Ready();
            Assert.IsTrue(hints.All(h => !h.Label.gameObject.activeSelf));
        }

        [UnityTest] public IEnumerator OnlyPlayerEndsGameAndCompletionClearsSaveAndReturnsToMenu()
        {
            Assert.IsTrue(SaveGameService.Current.TrySave(new SaveGameService.Checkpoint
            { scenePath = level.path, checkpointId = story.checkpoint.lamps[0].checkpointId, position = story.checkpoint.player.transform.position }));
            var end = Find<EndGameTrigger>(level);
            Assert.IsTrue(end.GetComponent<BoxCollider2D>().isTrigger);
            // A shadow collider must not trigger completion, even if placed inside the door.
            var dummy = new GameObject("Not a player");
            var collider = dummy.AddComponent<BoxCollider2D>();
            end.SendMessage("OnTriggerEnter2D", collider);
            Object.Destroy(dummy);
            Assert.IsFalse(story.EndingStarted);
            var player = story.checkpoint.player;
            player.transform.position = end.GetComponent<BoxCollider2D>().bounds.center;
            player.Body.position = player.transform.position;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            yield return Ready();
            Assert.IsTrue(story.EndingStarted);
            Assert.AreSame(story.endingTalk, story.CurrentText);
            Assert.IsTrue(SaveGameService.Current.TryRead(out _));
            yield return FinishCurrent();
            yield return Ready();
            Assert.AreSame(story.endingWords, story.CurrentText);
            Assert.IsFalse(story.talkPanel.activeSelf);
            Assert.IsTrue(story.endingPanel.activeSelf);
            Assert.AreEqual(0, Time.timeScale);
            yield return FinishCurrent();
            yield return null; yield return null;
            Assert.AreEqual("Assets/Scenes/Log.unity", SceneManager.GetActiveScene().path);
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
            Assert.IsFalse(LevelStoryController.InputBlocked);
            Assert.AreEqual(1, Time.timeScale);
            var menu = Find<MainMenuController>(SceneManager.GetActiveScene());
            menu.OpenResume(); menu.resumeOKButton.onClick.Invoke();
            Assert.IsFalse(menu.resumePanel.activeSelf);
            Assert.IsTrue(menu.noSaveHint.activeSelf);
            Assert.AreEqual(3, menu.noSaveHintDuration);
        }

        [Test] public void ManualNewlinesSplitDialogueButBlankLinesDoNotCreatePages()
        {
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, LevelStoryController.SplitLines("first\r\n\r\nsecond\n \nthird\r"));
        }
    }
}
#endif
