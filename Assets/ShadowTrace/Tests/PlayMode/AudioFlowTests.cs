#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class AudioFlowTests
    {
        private const string MenuPath = "Assets/Scenes/Log.unity";
        private const string LevelPath = "Assets/Scenes/Level 1.unity";
        private string oldPrefix;
        private SaveGameService oldSaves;
        private string saveDirectory;
        private Scene previous;
        private readonly List<Object> objects = new List<Object>();

        private static T Find<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            if (GameAudioManager.Instance != null) Object.DestroyImmediate(GameAudioManager.Instance.gameObject);
            oldPrefix = GameAudioManager.PreferenceKeyPrefix;
            GameAudioManager.PreferenceKeyPrefix = "AudioTests." + System.Guid.NewGuid().ToString("N") + ".";
            oldSaves = SaveGameService.Current;
            saveDirectory = Path.Combine(Application.dataPath, "../Temp/AudioTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(saveDirectory);
            previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.LoadSceneInPlayMode(MenuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => scene.isLoaded);
            SceneManager.SetActiveScene(scene); yield return null;
            Assert.IsNotNull(GameAudioManager.Instance);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Audio test cleanup"));
            foreach (var path in new[] { MenuPath, LevelPath })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previous) yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (GameAudioManager.Instance != null) Object.DestroyImmediate(GameAudioManager.Instance.gameObject);
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            foreach (var key in new[] { "Master", "Music", "Effects", "MusicEnabled", "EffectsEnabled" })
                PlayerPrefs.DeleteKey(GameAudioManager.PreferenceKeyPrefix + key);
            PlayerPrefs.Save(); GameAudioManager.PreferenceKeyPrefix = oldPrefix;
            SaveGameService.Current.BeginNewGame();
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory);
            SaveGameService.Current = oldSaves;
        }

        private static IEnumerator NewGame()
        {
            var menu = Find<MainMenuController>(); menu.OpenStart(); menu.ConfirmStart();
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator SettingsButtonsSlidersAndPreferencesControlAllChannels()
        {
            var audio = GameAudioManager.Instance;
            Assert.AreEqual("关卡界面音乐", audio.MusicSource.clip.name);
            Assert.AreEqual(8, audio.sounds.Length);
            Assert.IsTrue(audio.sounds.All(s => s.clip != null));
            Assert.IsTrue(audio.MusicEnabled); Assert.IsTrue(audio.EffectsEnabled);
            var menu = Find<MainMenuController>(); menu.OpenSettings(); menu.ShowAudio();
            yield return null;
            var panel = Find<AudioSettingsPanel>();
            Assert.AreEqual(panel.enabledText, panel.musicStateText.text);
            panel.masterSlider.value = 0.5f; panel.musicSlider.value = 0.4f; panel.effectsSlider.value = 0.25f;
            Assert.That(audio.MusicSource.volume, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(audio.EffectsSource.volume, Is.EqualTo(0.125f).Within(0.001f));
            Assert.AreEqual("50%", panel.masterPercentage.text);
            audio.SetFootsteps(true, false); audio.SetClock(true);
            Assert.AreEqual(audio.Clip(GameSound.Walk).clip, audio.FootstepSource.clip);
            panel.musicButton.onClick.Invoke(); panel.effectsButton.onClick.Invoke();
            Assert.AreEqual(panel.disabledText, panel.musicStateText.text);
            Assert.AreEqual(panel.disabledText, panel.effectsStateText.text);
            Assert.IsTrue(audio.MusicSource.mute); Assert.IsNull(audio.ClockSource.clip); Assert.IsNull(audio.FootstepSource.clip);
            int effects = 0; audio.SoundPlayed += sound => effects++;
            audio.PlaySound(GameSound.HInteract); Assert.AreEqual(0, effects);
            menu.ClosePanels(); Object.DestroyImmediate(audio.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Game Audio.prefab");
            Object.Instantiate(prefab); yield return null;
            audio = GameAudioManager.Instance;
            Assert.AreEqual(0.5f, audio.MasterVolume); Assert.AreEqual(0.4f, audio.MusicVolume);
            Assert.AreEqual(0.25f, audio.EffectsVolume); Assert.IsFalse(audio.MusicEnabled); Assert.IsFalse(audio.EffectsEnabled);
            menu.OpenSettings(); menu.ShowAudio(); yield return null;
            panel.musicButton.onClick.Invoke(); panel.effectsButton.onClick.Invoke();
            Assert.IsTrue(audio.MusicEnabled); Assert.IsTrue(audio.EffectsEnabled);
        }

        [UnityTest] public IEnumerator ClockTracksPreparationRecordingCancellationPauseAndAutomaticFinish()
        {
            yield return NewGame();
            var audio = GameAudioManager.Instance;
            Assert.AreEqual("各界面音乐", audio.MusicSource.clip.name);
            var ability = Find<ShadowTraceAbility>(); var pause = Find<GamePauseController>();
            ability.preparationDuration = 0.08f; ability.recordingDuration = 0.4f;
            int h = 0; audio.SoundPlayed += sound => { if (sound == GameSound.HInteract) h++; };
            ability.UseTrace(); Assert.IsTrue(audio.ClockRequested);
            Assert.AreEqual(audio.Clip(GameSound.Clock).clip, audio.ClockSource.clip);
            float remaining = ability.Remaining;
            pause.UsePauseKey(); Assert.IsTrue(audio.GameplayPaused); Assert.AreEqual(1, h);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(remaining, ability.Remaining);
            pause.UsePauseKey(); Assert.IsFalse(audio.GameplayPaused); Assert.AreEqual(2, h);
            yield return new WaitForSeconds(0.12f);
            Assert.AreEqual(TracePhase.Recording, ability.Phase); Assert.IsTrue(audio.ClockRequested);
            ability.UseTrace(); Assert.IsFalse(audio.ClockRequested); Assert.IsNull(audio.ClockSource.clip);
            ability.UseTrace(); ability.UseTrace(); Assert.IsFalse(audio.ClockRequested);
            ability.preparationDuration = 0.04f; ability.recordingDuration = 0.06f;
            ability.UseTrace(); yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(TracePhase.Idle, ability.Phase); Assert.IsFalse(audio.ClockRequested);
        }

        [UnityTest] public IEnumerator SaveHintOverridesCountdownThenQPreservesCheckpointAndResumeDoesNotReplayLamp()
        {
            yield return NewGame();
            var audio = GameAudioManager.Instance; var controller = Find<LevelCheckpointController>();
            var manager = Find<ShadowManager>(); var hud = controller.hud;
            var settings = Object.Instantiate(manager.uiSettings); objects.Add(settings);
            manager.uiSettings = settings; settings.saveNoticeDuration = 0.5f;
            int lampSounds = 0; audio.SoundPlayed += sound => { if (sound == GameSound.LampLit) lampSounds++; };
            hud.ability.UseTrace();
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, manager.player.Body.position, 0));
            var shadow = manager.Actors.First(a => a != null); var lamp = controller.lamps[0];
            shadow.Body.position = (Vector2)lamp.GetComponent<BoxCollider2D>().bounds.center - Vector2.Scale(shadow.Shape.offset, shadow.transform.lossyScale);
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.IsTrue(SaveGameService.Current.TryRead(out var checkpoint));
            hud.Refresh(); Assert.AreEqual(settings.saveSucceeded, hud.tooltipText.text); Assert.AreEqual(1, lampSounds);
            yield return new WaitForSeconds(0.65f); hud.Refresh();
            Assert.AreEqual(settings.Countdown(hud.ability.Phase, hud.ability.Remaining), hud.tooltipText.text);
            Find<GamePauseController>().UsePauseKey(); controller.ExitLevel(); yield return null; yield return null;
            Assert.AreEqual(MenuPath, SceneManager.GetActiveScene().path); Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(SaveGameService.Current.TryRead(out var saved)); Assert.AreEqual(checkpoint.checkpointId, saved.checkpointId);
            Assert.IsFalse(audio.ClockRequested); Assert.IsNull(audio.FootstepRequested);
            Assert.AreEqual("关卡界面音乐", audio.MusicSource.clip.name);
            var menu = Find<MainMenuController>(); menu.OpenResume(); menu.ConfirmResume(); yield return null; yield return null;
            controller = Find<LevelCheckpointController>();
            Assert.That(Vector2.Distance(controller.player.Body.position, saved.position), Is.LessThan(0.1f));
            Assert.AreEqual(1, lampSounds, "Restoring a lit checkpoint must not play the first-light sound.");
            Assert.AreEqual(1, Object.FindObjectsOfType<GameAudioManager>().Length);
        }

        [UnityTest] public IEnumerator QWithoutCheckpointReturnsToMenuWithoutCreatingSave()
        {
            yield return NewGame(); Find<LevelCheckpointController>().ExitLevel(); yield return null; yield return null;
            Assert.AreEqual(MenuPath, SceneManager.GetActiveScene().path);
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
            var menu = Find<MainMenuController>(); menu.OpenResume(); menu.ConfirmResume();
            Assert.IsTrue(menu.noSaveHint.activeSelf);
        }

        [UnityTest] public IEnumerator MovementLoopsAreExclusiveAndLandingPlaysOnceAfterAirborne()
        {
            var audio = GameAudioManager.Instance;
            var floor = new GameObject("Audio test floor"); objects.Add(floor);
            floor.transform.position = new Vector2(100, -0.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10, 1);
            var go = new GameObject("Audio test player"); objects.Add(go);
            go.transform.position = new Vector2(100, 0.715f);
            var player = go.AddComponent<PlayerController2D>(); player.Shape.size = new Vector2(0.8f, 1.4f);
            player.Body.gravityScale = 0; player.enabled = false;
            int landings = 0; audio.SoundPlayed += sound => { if (sound == GameSound.Landing) landings++; };
            typeof(PlayerController2D).GetField("<HorizontalInput>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 1);
            player.Body.velocity = Vector2.right * 2;
            Physics2D.SyncTransforms(); go.SendMessage("FixedUpdate");
            Assert.AreEqual(0, landings); Assert.AreEqual(GameSound.Walk, audio.FootstepRequested);
            Assert.AreEqual(audio.Clip(GameSound.Walk).clip, audio.FootstepSource.clip);
            typeof(PlayerController2D).GetField("sprint", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            go.SendMessage("FixedUpdate"); Assert.AreEqual(GameSound.Run, audio.FootstepRequested);
            Assert.AreEqual(audio.Clip(GameSound.Run).clip, audio.FootstepSource.clip);
            player.Body.position = new Vector2(100, 3); Physics2D.SyncTransforms(); go.SendMessage("FixedUpdate");
            Assert.IsNull(audio.FootstepRequested);
            player.Body.position = new Vector2(100, 0.715f); player.Body.velocity = Vector2.down;
            Physics2D.SyncTransforms(); go.SendMessage("FixedUpdate"); go.SendMessage("FixedUpdate");
            Assert.AreEqual(1, landings);
            yield return null;
        }

        [UnityTest] public IEnumerator BothPlateTypesAndSuccessfulDoorPassPlayOnlyOnActivation()
        {
            yield return NewGame();
            var audio = GameAudioManager.Instance; var manager = Find<ShadowManager>();
            int plateSounds = 0, doorSounds = 0;
            audio.SoundPlayed += sound => { if (sound == GameSound.Plate) plateSounds++; if (sound == GameSound.DoorOpen) doorSounds++; };
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, manager.player.Body.position, 0));
            var shadow = manager.Actors.First(a => a != null);
            var moverObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PressurePlate1.prefab"),
                new Vector3(150, 3, 0), Quaternion.identity);
            objects.Add(moverObject);
            var mover = moverObject.GetComponent<ShadowMoverPlate>();
            Vector2 offset = Vector2.Scale(shadow.Shape.offset, shadow.transform.lossyScale);
            shadow.Body.position = (Vector2)mover.pressArea.bounds.center - offset;
            Physics2D.SyncTransforms(); mover.Refresh(); mover.Refresh();
            Assert.AreEqual(1, plateSounds);
            shadow.Body.position = new Vector2(200, 200); Physics2D.SyncTransforms(); mover.Refresh();
            var plateObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PressurePlate2.prefab"),
                new Vector3(155, 3, 0), Quaternion.identity);
            objects.Add(plateObject);
            var plate = plateObject.GetComponent<ShadowPressurePlate>();
            var top = plate.GetComponent<BoxCollider2D>().bounds;
            shadow.Body.position = new Vector2(top.center.x - offset.x, top.max.y + shadow.Shape.bounds.extents.y + 0.015f - offset.y);
            Physics2D.SyncTransforms(); plate.Refresh(); plate.Refresh();
            Assert.IsTrue(plate.IsActive); Assert.AreEqual(2, plateSounds);
            var doorGo = new GameObject("Audio test door"); objects.Add(doorGo);
            doorGo.transform.position = manager.player.transform.position;
            var door = doorGo.AddComponent<InteractiveDoor>();
            var areaGo = new GameObject("Interaction area"); areaGo.transform.SetParent(doorGo.transform, false);
            door.interactionArea = areaGo.AddComponent<BoxCollider2D>(); door.interactionArea.isTrigger = true;
            door.interactionArea.size = Vector2.one * 10;
            Physics2D.SyncTransforms();
            Assert.IsTrue(door.TryPass(manager.player)); Assert.IsFalse(door.TryPass(manager.player));
            Assert.AreEqual(1, doorSounds);
            yield return null;
        }
    }
}
#endif
