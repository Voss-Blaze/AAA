#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShadowTrace.Tests
{
    public sealed class FirstPlayTutorialTests
    {
        private const string Level = "Assets/Scenes/Level 1.unity";
        private const string Menu = "Assets/Scenes/Log.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene previous, scene;
        private SaveGameService oldSaves;
        private string directory;
        private FirstPlayTutorial tutorial;
        private LevelCheckpointController checkpoint;
        private LevelStoryController story;
        private ShadowManager manager;
        private PlayerController2D player;

        private static T Find<T>(Scene target) where T : Component => target.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            oldSaves = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/FirstPlayTutorialTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory);
            previous = SceneManager.GetActiveScene();
            scene = EditorSceneManager.LoadSceneInPlayMode(Level, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => scene.isLoaded);
            SceneManager.SetActiveScene(scene);
            checkpoint = Find<LevelCheckpointController>(scene);
            story = Find<LevelStoryController>(scene);
            story.introductionDelay = 1000;
            player = checkpoint.player;
            player.enabled = false;
            player.Body.gravityScale = 0;
            Assert.IsTrue(checkpoint.SafePosition(player.transform.position, out var position));
            player.transform.position = position; player.Body.position = position;
            Physics2D.SyncTransforms();
            player.RefreshGrounding();
            manager = checkpoint.hud.manager;
            yield return null; yield return null;
            tutorial = Find<FirstPlayTutorial>(scene);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Tutorial cleanup"));
            foreach (var path in new[] { Level, Menu })
            {
                var loaded = SceneManager.GetSceneByPath(path);
                if (loaded.IsValid() && loaded.isLoaded && loaded != previous) yield return SceneManager.UnloadSceneAsync(loaded);
            }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            SaveGameService.Current = oldSaves;
        }

        private IEnumerator Begin()
        {
            Assert.AreEqual(TutorialStep.Introduction, tutorial.Step);
            typeof(LevelStoryController).GetField("<IntroductionFinished>k__BackingField", Private).SetValue(story, true);
            yield return null; yield return null;
            Assert.AreEqual(TutorialStep.Stay, tutorial.Step);
        }

        private ShadowActor Spawn(ShadowKind kind, Vector2? origin = null, int direction = 1)
        {
            var before = manager.Actors.Where(a => a != null).ToArray();
            Assert.IsTrue(manager.TrySpawn(kind, origin ?? player.Body.position, direction));
            return manager.Actors.Single(a => a != null && !before.Contains(a));
        }

        private void Stage(TutorialStep step) => typeof(FirstPlayTutorial).GetMethod("SetStep", Private).Invoke(tutorial, new object[] { step });

        [UnityTest] public IEnumerator StartsAfterIntroductionAndBindsTheThreeAuthoredLocations()
        {
            Assert.AreEqual(TutorialStep.Introduction, tutorial.Step);
            Assert.AreEqual("spike1", tutorial.TeachingSpike.name);
            Assert.AreEqual("level1-door-1690783784", tutorial.TeachingDoor.saveId);
            Assert.AreEqual("6eb2ef50c37f4be0a052727e7f419c80", tutorial.TeachingLamp.checkpointId);
            Assert.Greater(tutorial.TeachingSpike.transform.position.x, player.transform.position.x);
            Assert.Greater(tutorial.TeachingLamp.transform.position.x, tutorial.TeachingDoor.transform.position.x);
            yield return Begin();
            Spawn(ShadowKind.Approach);
            Assert.AreEqual(TutorialStep.Stay, tutorial.Step, "Wrong types must not complete a lesson.");
            var actor = Spawn(ShadowKind.Stay);
            Assert.AreEqual(TutorialStep.DeleteOne, tutorial.Step);
            manager.Select(actor.Number - 1);
            yield return new WaitForSeconds(0.2f);
            manager.Remove(actor);
            Assert.AreEqual(TutorialStep.Follow, tutorial.Step);
            Assert.AreEqual(TutorialStep.Follow, TutorialProgress.Read().step);
            Assert.IsTrue(SaveGameService.Current.BeginNewGame());
            Assert.AreEqual(TutorialStep.Follow, TutorialProgress.Read().step, "Tutorial progress is independent of checkpoint resets.");
        }

        [UnityTest] public IEnumerator FourTypesLeadThroughObstacleClimbAndActualSpikeDestruction()
        {
            yield return Begin();
            var stay = Spawn(ShadowKind.Stay);
            manager.Select(stay.Number - 1);
            yield return new WaitForSeconds(0.2f);
            manager.Remove(stay);
            var follow = Spawn(ShadowKind.Follow);
            var start = follow.Body.position;
            follow.Body.position = start + Vector2.left;
            yield return new WaitForSeconds(0.2f);
            follow.Body.position = start + Vector2.right;
            player.RefreshGrounding(); player.RequestJump();
            yield return null; yield return null;
            Assert.AreEqual(TutorialStep.Avoid, tutorial.Step);
            Spawn(ShadowKind.Avoid);
            float deadline = Time.realtimeSinceStartup + 8;
            while (tutorial.Step != TutorialStep.ClearAll && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(TutorialStep.ClearAll, tutorial.Step, "Avoid must visibly reverse against a practice obstacle.");
            manager.ClearAll(); manager.ShowNotice(ShadowNotice.Cleared);
            Assert.AreEqual(TutorialStep.Approach, tutorial.Step);
            var approach = Spawn(ShadowKind.Approach);
            deadline = Time.realtimeSinceStartup + 8;
            while (tutorial.Step != TutorialStep.Climb && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(TutorialStep.Climb, tutorial.Step);
            Assert.IsTrue(approach.Stopped);
            // Leave the shadow's volume so it can enable the player's collision.
            player.Body.position += Vector2.left * 2;
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.IsTrue(approach.PlayerCollisionEnabled);
            Vector2 rootOffset = (Vector2)player.Shape.bounds.center - player.Body.position;
            Vector2 atop = new Vector2(approach.Shape.bounds.center.x - rootOffset.x,
                approach.Shape.bounds.max.y + player.Shape.bounds.extents.y + 0.015f - rootOffset.y);
            player.transform.position = atop; player.Body.position = atop; player.Body.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); player.RefreshGrounding();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(TutorialStep.ApproachSpikes, tutorial.Step);
            Assert.IsNull(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>())
                .FirstOrDefault(t => t.name == "Temporary Tutorial Props"), "Practice props must be removed before the real gap.");
            player.Body.gravityScale = 3;
            var practice = TutorialProgress.Read().practicePosition;
            var origin = new Vector2(tutorial.TeachingSpike.GetComponent<Collider2D>().bounds.min.x - 0.7f, practice.y);
            var scout = Spawn(ShadowKind.Approach, origin);
            Assert.AreEqual(TutorialStep.ObserveSpikes, tutorial.Step);
            deadline = Time.realtimeSinceStartup + 8;
            while (tutorial.Step != TutorialStep.Sprint && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(TutorialStep.Sprint, tutorial.Step);
            Assert.IsTrue(scout == null || !scout.isActiveAndEnabled);
        }

        [UnityTest] public IEnumerator ClimbStepStaysBetweenPlayerAndShadowInBothDirections()
        {
            yield return Begin();
            foreach (int direction in new[] { -1, 1 })
            {
                manager.ClearAll();
                Stage(TutorialStep.Approach);
                var actor = Spawn(ShadowKind.Approach, direction: direction);
                float deadline = Time.realtimeSinceStartup + 8;
                while (tutorial.Step != TutorialStep.Climb && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(TutorialStep.Climb, tutorial.Step);
                var props = tutorial.GetComponentsInChildren<BoxCollider2D>()
                    .Where(c => c.transform.parent.name == "Temporary Tutorial Props").ToArray();
                var step = props.Single(c => c.name == "辅助矮台");
                Assert.AreEqual(-direction, Mathf.Sign(step.bounds.center.x - actor.Shape.bounds.center.x));
                Assert.AreEqual(Mathf.Sign(player.Shape.bounds.center.x - actor.Shape.bounds.center.x),
                    Mathf.Sign(step.bounds.center.x - actor.Shape.bounds.center.x));
                Assert.IsFalse(step.bounds.Intersects(actor.Shape.bounds));
                foreach (var obstacle in props.Where(c => c != step))
                {
                    Assert.IsFalse(step.bounds.Intersects(obstacle.bounds), "Foothold must not overlap either obstacle.");
                    Assert.That(step.bounds.min.y, Is.EqualTo(obstacle.bounds.min.y).Within(0.02f));
                }
                Assert.Less(Mathf.Abs(step.bounds.center.x - actor.Shape.bounds.center.x),
                    step.bounds.extents.x + actor.Shape.bounds.extents.x + 0.2f);
            }
        }

        [UnityTest] public IEnumerator ScoutingMarkerRendersAtTheApproachStartPoint()
        {
            yield return Begin();
            Stage(TutorialStep.ApproachSpikes);
            var camera = Find<TraceCameraFollow>(scene).GetComponent<Camera>();
            var spike = tutorial.TeachingSpike.GetComponent<Collider2D>().bounds;
            var target = new Vector3(spike.min.x - 1, player.Shape.bounds.min.y + 0.5f, 0);
            var follow = camera.GetComponent<TraceCameraFollow>();
            follow.enabled = false;
            camera.transform.position = new Vector3(target.x, target.y, camera.transform.position.z);
            yield return new WaitForSeconds(0.2f);
            Canvas.ForceUpdateCanvases();
            var marker = tutorial.view.pointer;
            Assert.IsTrue(marker.gameObject.activeInHierarchy);
            Assert.AreEqual("▼", marker.text);
            Assert.Greater(marker.cachedTextGenerator.vertexCount, 0, "Marker must generate visible geometry.");
            var screen = camera.WorldToScreenPoint(target);
            Assert.That(marker.transform.position.x, Is.EqualTo(screen.x).Within(1));
            Assert.That(marker.transform.position.y, Is.EqualTo(screen.y).Within(6));
            tutorial.Skip();
            yield return null;
            Assert.IsFalse(marker.gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator PracticeFailureRecoversAndSkippingRestoresNormalDeath()
        {
            yield return Begin();
            var origin = player.Body.position;
            player.Body.position += Vector2.right * 5;
            checkpoint.life.Die();
            Assert.IsFalse(checkpoint.life.IsDead);
            Assert.That(Vector2.Distance(origin, player.Body.position), Is.LessThan(0.05f));
            Assert.AreEqual(TutorialStep.Stay, tutorial.Step);
            tutorial.Skip();
            Assert.AreEqual(TutorialStep.Complete, TutorialProgress.Read().step);
            checkpoint.life.Die();
            Assert.IsTrue(checkpoint.life.IsDead);
        }

        [UnityTest] public IEnumerator SuccessfulSaveAndMenuResumeContinueWithZoomAndClosingMessage()
        {
            yield return Begin();
            Stage(TutorialStep.Lamp);
            tutorial.TeachingDoor.RestoreOpen();
            var lamp = tutorial.TeachingLamp;
            var actor = Spawn(ShadowKind.Stay);
            Vector2 offset = Vector2.Scale(actor.Shape.offset, actor.transform.lossyScale);
            actor.Body.position = (Vector2)lamp.GetComponent<Collider2D>().bounds.center - offset;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.AreEqual(TutorialStep.Resume, tutorial.Step);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var saved));
            Assert.AreEqual(lamp.checkpointId, saved.checkpointId);
            checkpoint.ExitLevel();
            yield return null; yield return null;
            var menu = Find<MainMenuController>(SceneManager.GetActiveScene());
            Assert.IsNotNull(menu.resumeButton.GetComponent<Outline>());
            menu.resumeButton.onClick.Invoke(); menu.resumeOKButton.onClick.Invoke();
            yield return null; yield return null; yield return null; yield return null;
            scene = SceneManager.GetActiveScene();
            tutorial = Find<FirstPlayTutorial>(scene);
            checkpoint = Find<LevelCheckpointController>(scene);
            Assert.IsTrue(checkpoint.HasResumed);
            Assert.AreEqual(TutorialStep.Zoom, tutorial.Step);
            Assert.That(Vector2.Distance(checkpoint.player.Body.position, saved.position), Is.LessThan(0.2f));
            var camera = Find<TraceCameraFollow>(scene);
            float size = camera.GetComponent<Camera>().orthographicSize;
            camera.ApplyScrollZoom(size > camera.minOrthographicSize ? 1 : -1);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(TutorialStep.Finish, tutorial.Step);
            StringAssert.Contains("多多观察", tutorial.CurrentInstruction);
            tutorial.Continue();
            Assert.AreEqual(TutorialStep.Complete, TutorialProgress.Read().step);
        }

        [UnityTest] public IEnumerator EditableHUDUsesRequestedSpritesAndNewStartAlwaysRestartsTutorial()
        {
            yield return Begin();
            var label = tutorial.view.instruction;
            var hudAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Tutorial/TutorialHUD.prefab");
            Assert.IsNotNull(hudAsset.GetComponent<TutorialHUDView>());
            Assert.IsNotNull(tutorial.view.GetComponentInParent<Canvas>());
            Assert.IsFalse(tutorial.view.transform.IsChildOf(tutorial.transform), "HUD must use the authored canvas hierarchy.");
            StringAssert.StartsWith("Assets/Prefab/Tutorial/", UnityEditor.AssetDatabase.GetAssetPath(tutorial.settings));
            var manualRect = tutorial.view.manual.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, manualRect.anchorMin);
            Assert.AreEqual(Vector2.zero, manualRect.anchorMax);
            var panelRect = tutorial.view.guidance.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0.5f, 1), panelRect.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 1), panelRect.pivot);
            Assert.AreSame(tutorial.view.unselected, tutorial.view.manual.image.sprite);
            tutorial.view.SetHandbook(true);
            Assert.AreSame(tutorial.view.selected, tutorial.view.manual.image.sprite);
            tutorial.view.SetHandbook(false);
            Assert.AreSame(tutorial.view.unselected, tutorial.view.manual.image.sprite);
            Assert.AreSame(tutorial.view.selected, tutorial.view.skip.spriteState.pressedSprite);
            var refresh = typeof(FirstPlayTutorial).GetMethod("RefreshText", Private);
            foreach (TutorialStep step in System.Enum.GetValues(typeof(TutorialStep)))
            {
                Stage(step);
                refresh.Invoke(tutorial, null);
                Canvas.ForceUpdateCanvases();
                Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height, "Text overflow in " + step);
                Assert.IsFalse(label.raycastTarget);
            }
            Assert.IsTrue(SaveGameService.Current.TrySave(new SaveGameService.Checkpoint
            { scenePath = Level, checkpointId = tutorial.TeachingLamp.checkpointId, position = player.transform.position }));
            SceneManager.LoadScene(Menu);
            yield return null; yield return null;
            var menu = Find<MainMenuController>(SceneManager.GetActiveScene());
            Assert.IsFalse(menu.tutorialPanel.GetComponentsInChildren<Button>(true).Any(b => b.name == "Replay First Play Tutorial"));
            Assert.AreEqual(TutorialStep.Complete, TutorialProgress.Read().step);
            menu.startButton.onClick.Invoke();
            Assert.AreEqual(TutorialStep.Complete, TutorialProgress.Read().step, "Cancelling new game must not reset tutorial progress.");
            menu.startOKButton.onClick.Invoke();
            yield return null; yield return null; yield return null;
            Assert.AreEqual(TutorialStep.Introduction, TutorialProgress.Read().step);
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
        }

        [UnityTest] public IEnumerator AreaRestrictsOnlyPlayerAndUnlocksAfterEachExercise()
        {
            yield return Begin();
            var area = tutorial.area;
            Assert.IsTrue(area.IsRestricting);
            var origin = player.Body.position;
            var scout = Spawn(ShadowKind.Approach);
            scout.Body.position = new Vector2(area.ActiveBounds.xMax + 3, origin.y);
            player.Body.position = new Vector2(area.ActiveBounds.xMax + 2, origin.y);
            player.Body.velocity = new Vector2(200, 0);
            Physics2D.SyncTransforms();
            area.Constrain(); Physics2D.SyncTransforms();
            Assert.LessOrEqual(player.Shape.bounds.max.x, area.ActiveBounds.xMax + 0.01f);
            Assert.LessOrEqual(player.Body.velocity.x, 0.01f);
            Assert.Greater(scout.Body.position.x, area.ActiveBounds.xMax, "Shadows must be able to reach the spikes.");
            player.Body.position = origin; Physics2D.SyncTransforms();
            float practiceRight = area.ActiveBounds.xMax;
            Stage(TutorialStep.ApproachSpikes);
            Assert.Greater(area.ActiveBounds.xMax, practiceRight);
            Stage(TutorialStep.Sprint);
            Assert.Greater(area.ActiveBounds.xMax, tutorial.TeachingSpike.GetComponent<Collider2D>().bounds.max.x + 2);
            Stage(TutorialStep.Door);
            Assert.Greater(area.ActiveBounds.xMax, tutorial.TeachingDoor.transform.position.x);
            Stage(TutorialStep.Lamp);
            Assert.Greater(area.ActiveBounds.xMax, tutorial.TeachingLamp.transform.position.x);
            tutorial.Skip();
            Assert.IsFalse(area.IsRestricting);
        }

        [UnityTest] public IEnumerator AuthoredFirstGapNeedsSprintForTheCurrentJumpSettings()
        {
            yield return Begin();
            tutorial.Skip();
            var shape = tutorial.TeachingSpike.GetComponent<Collider2D>().bounds;
            var ground = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>())
                .Where(c => TracePhysics.IsWorld(c) && c.bounds.min.x < shape.min.x && c.bounds.max.x > player.Body.position.x &&
                    c.bounds.max.y < player.Shape.bounds.center.y)
                .OrderByDescending(c => c.bounds.max.y).First();
            foreach (var hazard in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpikeHazard>()))
                hazard.gameObject.SetActive(false);
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            try
            {
                Vector2 initial = new Vector2(ground.bounds.max.x - player.Shape.bounds.extents.x - 0.05f,
                    TutorialProgress.Read().practicePosition.y);
                var physicsStep = typeof(PlayerController2D).GetMethod("FixedUpdate", Private);
                player.Body.gravityScale = 3;
                foreach (bool sprint in new[] { false, true })
                {
                    player.transform.position = initial; player.Body.position = initial; player.Body.velocity = Vector2.zero;
                    typeof(PlayerController2D).GetField("<HorizontalInput>k__BackingField", Private).SetValue(player, 1);
                    typeof(PlayerController2D).GetField("sprint", Private).SetValue(player, sprint);
                    Physics2D.SyncTransforms(); player.RefreshGrounding(); player.RequestJump();
                    for (int i = 0; i < 55; i++)
                    {
                        physicsStep.Invoke(player, null); Physics2D.Simulate(0.02f); Physics2D.SyncTransforms();
                    }
                    if (sprint)
                    {
                        Assert.Greater(player.Body.position.x, shape.max.x);
                        Assert.Greater(player.Shape.bounds.min.y, ground.bounds.max.y - 0.1f);
                    }
                    else Assert.Less(player.Shape.bounds.min.y, ground.bounds.max.y - 0.5f);
                }
            }
            finally { Physics2D.simulationMode = previousMode; }
        }
    }
}
#endif
