using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        public Button musicButton;
        public Button effectsButton;
        public Text musicStateText;
        public Text effectsStateText;
        public string enabledText;
        public string disabledText;
        public string percentageFormat;
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider effectsSlider;
        public Text masterPercentage;
        public Text musicPercentage;
        public Text effectsPercentage;
        private GameAudioManager audioManager;
        private bool started;

        private void Start() { started = true; Bind(); }
        private void OnEnable() { if (started) Bind(); }
        private void OnDisable()
        {
            if (audioManager == null) return;
            audioManager.SettingsChanged -= Refresh;
            musicButton.onClick.RemoveListener(ToggleMusic); effectsButton.onClick.RemoveListener(ToggleEffects);
            masterSlider.onValueChanged.RemoveListener(MasterChanged);
            musicSlider.onValueChanged.RemoveListener(MusicChanged);
            effectsSlider.onValueChanged.RemoveListener(EffectsChanged);
            audioManager = null;
        }

        private void Bind()
        {
            if (audioManager != null) return;
            audioManager = GameAudioManager.Instance;
            if (audioManager == null) return;
            audioManager.SettingsChanged += Refresh;
            musicButton.onClick.AddListener(ToggleMusic); effectsButton.onClick.AddListener(ToggleEffects);
            masterSlider.onValueChanged.AddListener(MasterChanged);
            musicSlider.onValueChanged.AddListener(MusicChanged);
            effectsSlider.onValueChanged.AddListener(EffectsChanged);
            Refresh();
        }

        private void ToggleMusic() { audioManager.SetMusicEnabled(!audioManager.MusicEnabled); }
        private void ToggleEffects() { audioManager.SetEffectsEnabled(!audioManager.EffectsEnabled); }
        private void MasterChanged(float value) { audioManager.SetMasterVolume(Mathf.InverseLerp(masterSlider.minValue, masterSlider.maxValue, value)); }
        private void MusicChanged(float value) { audioManager.SetMusicVolume(Mathf.InverseLerp(musicSlider.minValue, musicSlider.maxValue, value)); }
        private void EffectsChanged(float value) { audioManager.SetEffectsVolume(Mathf.InverseLerp(effectsSlider.minValue, effectsSlider.maxValue, value)); }

        private void Refresh()
        {
            musicStateText.text = audioManager.MusicEnabled ? enabledText : disabledText;
            effectsStateText.text = audioManager.EffectsEnabled ? enabledText : disabledText;
            SetSlider(masterSlider, masterPercentage, audioManager.MasterVolume);
            SetSlider(musicSlider, musicPercentage, audioManager.MusicVolume);
            SetSlider(effectsSlider, effectsPercentage, audioManager.EffectsVolume);
        }

        private void SetSlider(Slider slider, Text label, float value)
        {
            slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, value));
            if (label != null) label.text = (percentageFormat ?? string.Empty).Replace("{0}", Mathf.RoundToInt(value * 100).ToString());
        }
    }
}
