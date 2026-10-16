using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTrace
{
    public enum GameSound { HInteract, Run, Walk, DoorOpen, LampLit, Landing, Plate, Clock }

    public sealed class GameAudioManager : MonoBehaviour
    {
        [Serializable] public sealed class SceneMusic { public string scenePath; public AudioClip clip; }
        [Serializable] public sealed class SoundClip
        {
            public GameSound sound;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 1;
        }

        public SceneMusic[] music;
        public SoundClip[] sounds;
        public static GameAudioManager Instance { get; private set; }
        public static string PreferenceKeyPrefix { get; set; } = "ShadowTrace.Audio.";
        public float MasterVolume { get; private set; } = 1;
        public float MusicVolume { get; private set; } = 1;
        public float EffectsVolume { get; private set; } = 1;
        public bool MusicEnabled { get; private set; } = true;
        public bool EffectsEnabled { get; private set; } = true;
        public bool GameplayPaused { get; private set; }
        public bool ClockRequested { get; private set; }
        public GameSound? FootstepRequested { get; private set; }
        public AudioSource MusicSource { get; private set; }
        public AudioSource EffectsSource { get; private set; }
        public AudioSource FootstepSource { get; private set; }
        public AudioSource ClockSource { get; private set; }
        public event Action SettingsChanged;
        public event Action<GameSound> SoundPlayed;
        private bool clockPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            MusicSource = Source("Music", true);
            EffectsSource = Source("Effects", false);
            FootstepSource = Source("Footsteps", true);
            ClockSource = Source("Clock", true);
            ReloadSettings();
            SceneManager.activeSceneChanged += OnSceneChanged;
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private AudioSource Source(string name, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            return source;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Instance = null;
        }

        public void ReloadSettings()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKeyPrefix + "Master", 1));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKeyPrefix + "Music", 1));
            EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKeyPrefix + "Effects", 1));
            MusicEnabled = PlayerPrefs.GetInt(PreferenceKeyPrefix + "MusicEnabled", 1) != 0;
            EffectsEnabled = PlayerPrefs.GetInt(PreferenceKeyPrefix + "EffectsEnabled", 1) != 0;
            Apply(); SettingsChanged?.Invoke();
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(PreferenceKeyPrefix + "Master", MasterVolume);
            PlayerPrefs.SetFloat(PreferenceKeyPrefix + "Music", MusicVolume);
            PlayerPrefs.SetFloat(PreferenceKeyPrefix + "Effects", EffectsVolume);
            PlayerPrefs.SetInt(PreferenceKeyPrefix + "MusicEnabled", MusicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PreferenceKeyPrefix + "EffectsEnabled", EffectsEnabled ? 1 : 0);
            PlayerPrefs.Save(); Apply(); SettingsChanged?.Invoke();
        }

        public void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); SaveSettings(); }
        public void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); SaveSettings(); }
        public void SetEffectsVolume(float value) { EffectsVolume = Mathf.Clamp01(value); SaveSettings(); }
        public void SetMusicEnabled(bool value) { MusicEnabled = value; SaveSettings(); }
        public void SetEffectsEnabled(bool value)
        {
            EffectsEnabled = value;
            if (!value) EffectsSource.Stop();
            SaveSettings();
        }

        public SoundClip Clip(GameSound sound)
        {
            if (sounds != null) foreach (var item in sounds) if (item != null && item.sound == sound) return item;
            return null;
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            StopGameplaySounds();
            AudioClip clip = null;
            if (music != null) foreach (var item in music) if (item != null && item.scenePath == next.path) { clip = item.clip; break; }
            MusicSource.Stop(); MusicSource.clip = clip;
            if (clip != null) MusicSource.Play();
            Apply();
        }

        public void PlaySound(GameSound sound)
        {
            var item = Clip(sound);
            if (!EffectsEnabled || MasterVolume <= 0 || EffectsVolume <= 0 || item == null || item.clip == null) return;
            EffectsSource.PlayOneShot(item.clip, item.volume);
            SoundPlayed?.Invoke(sound);
        }

        public void SetFootsteps(bool moving, bool sprinting)
        {
            GameSound? desired = moving ? (sprinting ? GameSound.Run : GameSound.Walk) : (GameSound?)null;
            if (FootstepRequested == desired) return;
            FootstepRequested = desired; ApplyFootsteps();
        }

        public void SetClock(bool active)
        {
            if (ClockRequested == active) return;
            ClockRequested = active; ApplyClock();
        }

        public void SetGameplayPaused(bool paused)
        {
            GameplayPaused = paused; ApplyFootsteps(); ApplyClock();
        }

        public void StopGameplaySounds()
        {
            FootstepRequested = null; ClockRequested = false; GameplayPaused = false;
            FootstepSource.Stop(); FootstepSource.clip = null;
            ClockSource.Stop(); ClockSource.clip = null; clockPaused = false;
            EffectsSource.Stop();
        }

        private void Apply()
        {
            if (MusicSource == null) return;
            MusicSource.volume = MasterVolume * MusicVolume;
            MusicSource.mute = !MusicEnabled;
            EffectsSource.volume = MasterVolume * EffectsVolume;
            EffectsSource.mute = !EffectsEnabled;
            ApplyFootsteps(); ApplyClock();
        }

        private void ApplyFootsteps()
        {
            var item = FootstepRequested.HasValue ? Clip(FootstepRequested.Value) : null;
            if (!EffectsEnabled || GameplayPaused || item == null || item.clip == null)
            { FootstepSource.Stop(); FootstepSource.clip = null; return; }
            FootstepSource.volume = MasterVolume * EffectsVolume * item.volume;
            if (FootstepSource.clip == item.clip) return;
            FootstepSource.Stop(); FootstepSource.clip = item.clip; FootstepSource.Play();
        }

        private void ApplyClock()
        {
            var item = Clip(GameSound.Clock);
            if (!EffectsEnabled || !ClockRequested || item == null || item.clip == null)
            { ClockSource.Stop(); ClockSource.clip = null; clockPaused = false; return; }
            ClockSource.volume = MasterVolume * EffectsVolume * item.volume;
            if (GameplayPaused)
            {
                if (!clockPaused) { ClockSource.Pause(); clockPaused = true; }
                return;
            }
            if (clockPaused) { ClockSource.UnPause(); clockPaused = false; }
            if (ClockSource.clip == item.clip) return;
            ClockSource.Stop(); ClockSource.clip = item.clip; ClockSource.Play();
        }
    }
}
