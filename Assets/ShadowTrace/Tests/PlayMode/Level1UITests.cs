#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ShadowTrace.Tests
{
    public sealed class Level1UITests
    {
        private Scene previous;
        private Scene scene;
        private ShadowManager manager;
        private ShadowTraceHUD hud;
        private GamePauseController pause;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            previous = SceneManager.GetActiveScene();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Level 1.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => scene.isLoaded);
            SceneManager.SetActiveScene(scene);
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            manager = all.Select(t => t.GetComponent<ShadowManager>()).Single(t => t != null);
            hud = all.Select(t => t.GetComponent<ShadowTraceHUD>()).Single(t => t != null);
            pause = all.Select(t => t.GetComponent<GamePauseController>()).Single(t => t != null);
            manager.player.Body.gravityScale = 0;
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (pause != null && pause.IsPaused && !pause.life.IsDead) pause.TogglePause();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = 1;
        }

        [UnityTest] public IEnumerator FixedSlotsHideAndRefillTheirOwnIcons()
        {
            Assert.AreEqual(5, manager.Capacity);
            Assert.IsFalse(hud.tooltip.activeSelf);
            Assert.IsTrue(hud.occupiedIcons.All(icon => !icon.activeSelf));
            var origin = manager.player.Body.position;
            for (int i = 0; i < 5; i++) Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, origin, 0));
            for (int i = 0; i < 5; i++)
            {
                var actor = manager.Actors[i];
                var label = actor.GetComponentInChildren<TextMesh>();
                Assert.IsNotNull(label);
                Assert.AreEqual((i + 1).ToString("00"), label.text);
                Assert.AreSame(manager.uiSettings.shadowNumberFont, label.font);
                Assert.Greater(label.transform.position.y, actor.Shape.bounds.max.y);
            }
            hud.Refresh();
            Assert.IsTrue(hud.occupiedIcons.All(icon => icon.activeSelf));
            Assert.AreEqual(manager.uiSettings.spawned, hud.tooltipText.text);
            hud.ability.UseTrace(); hud.Refresh();
            Assert.AreEqual(TracePhase.Idle, hud.ability.Phase);
            Assert.AreEqual(manager.uiSettings.limitReached, hud.tooltipText.text);
            Object.DestroyImmediate(manager.Actors[0].gameObject);
            Object.DestroyImmediate(manager.Actors[2].gameObject);
            hud.Refresh();
            Assert.IsFalse(hud.occupiedIcons[0].activeSelf);
            Assert.IsFalse(hud.occupiedIcons[2].activeSelf);
            Assert.IsTrue(hud.occupiedIcons[1].activeSelf);
            Assert.IsTrue(hud.occupiedIcons[3].activeSelf);
            Assert.IsTrue(hud.occupiedIcons[4].activeSelf);
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, origin, 0));
            Assert.AreEqual(1, manager.Actors[0].Number);
            hud.Refresh();
            Assert.IsTrue(hud.occupiedIcons[0].activeSelf);
            Assert.IsFalse(hud.occupiedIcons[2].activeSelf);
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, origin, 0));
            Assert.AreEqual(3, manager.Actors[2].Number);
            Assert.AreEqual("03", manager.Actors[2].GetComponentInChildren<TextMesh>().text);
            hud.Refresh();
            Assert.IsTrue(hud.occupiedIcons.All(icon => icon.activeSelf));
            yield return null;
        }

        [UnityTest] public IEnumerator CountdownPauseAndEditableMessagesWorkTogether()
        {
            var ability = hud.ability;
            ability.UseTrace(); hud.Refresh();
            Assert.AreEqual("3", hud.tooltipText.text);
            float remaining = ability.Remaining;
            pause.button.onClick.Invoke();
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(pause.blackout.activeSelf);
            Assert.AreSame(pause.playSprite, pause.buttonImage.sprite);
            Assert.Greater(pause.button.transform.GetSiblingIndex(), pause.blackout.transform.GetSiblingIndex());
            bool jumped = false;
            manager.player.JumpRequested += () => jumped = true;
            manager.player.RequestJump(); ability.UseTrace(); manager.Select(0);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(jumped);
            Assert.AreEqual(TracePhase.Preparing, ability.Phase);
            Assert.AreEqual(remaining, ability.Remaining);
            pause.button.onClick.Invoke();
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(pause.blackout.activeSelf);
            Assert.AreSame(pause.pauseSprite, pause.buttonImage.sprite);
            yield return new WaitForSeconds(1.05f);
            hud.Refresh(); Assert.AreEqual("2", hud.tooltipText.text);
            yield return new WaitForSeconds(1);
            hud.Refresh(); Assert.AreEqual("1", hud.tooltipText.text);
            yield return new WaitForSeconds(1);
            hud.Refresh();
            Assert.AreEqual(TracePhase.Recording, ability.Phase);
            Assert.AreEqual(manager.uiSettings.Countdown(TracePhase.Recording, ability.Remaining), hud.tooltipText.text);
            ability.UseTrace(); hud.Refresh();
            Assert.AreEqual(1, manager.Count);
            Assert.AreEqual(manager.uiSettings.spawned, hud.tooltipText.text);
            var custom = Object.Instantiate(manager.uiSettings);
            custom.spawned = "Custom message"; manager.uiSettings = custom;
            manager.ShowNotice(ShadowNotice.Spawned); hud.Refresh();
            Assert.AreEqual(custom.spawned, hud.tooltipText.text);
            pause.TogglePause();
            float until = manager.MessageUntil - Time.time;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(until, manager.MessageUntil - Time.time);
            pause.TogglePause();
            yield return new WaitForSeconds(custom.noticeDuration + 0.1f);
            hud.Refresh(); Assert.IsFalse(hud.tooltip.activeSelf);
            Object.Destroy(custom);
        }

        [UnityTest] public IEnumerator AutomaticCompletionAndDeathCannotBeResumedByButton()
        {
            hud.ability.preparationDuration = 0.04f;
            hud.ability.recordingDuration = 0.06f;
            hud.ability.UseTrace();
            yield return new WaitForSeconds(0.2f);
            hud.Refresh();
            Assert.AreEqual(1, manager.Count);
            Assert.AreEqual(manager.uiSettings.spawned, hud.tooltipText.text);
            pause.life.Die(); pause.button.onClick.Invoke();
            Assert.AreEqual(0, Time.timeScale);
        }

        [UnityTest] public IEnumerator DeathDuringManualPauseDoesNotLeaveNextScenePaused()
        {
            pause.TogglePause(); pause.life.Die(); pause.TogglePause();
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0, Time.timeScale);
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest] public IEnumerator PauseButtonDoesNotCaptureJumpThroughSubmit()
        {
            var events = EventSystem.current;
            Assert.IsNotNull(events);
            Assert.AreEqual(Navigation.Mode.None, pause.button.navigation.mode);
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(pause.button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.AreNotSame(pause.button.gameObject, events.currentSelectedGameObject);
            ExecuteEvents.Execute(pause.button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsTrue(pause.IsPaused);
            ExecuteEvents.Execute(pause.button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(pause.IsPaused);
            Assert.AreNotSame(pause.button.gameObject, events.currentSelectedGameObject);
            bool jumped = false;
            manager.player.JumpRequested += () => jumped = true;
            manager.player.RequestJump();
            ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            Assert.IsTrue(jumped);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(pause.IsPaused);
        }
    }
}
#endif
