using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ShadowTrace
{
    public sealed class GamePauseController : MonoBehaviour
    {
        public Button button;
        public Image buttonImage;
        public Sprite pauseSprite;
        public Sprite playSprite;
        public GameObject blackout;
        public PlayerLife life;
        public bool IsPaused { get; private set; }
        private float previousTimeScale;
        private bool restoreTimeOnDestroy;

        private void Awake()
        {
            // Icons represent the game state, rather than the pointer state.
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                ClearButtonSelection();
            }
            Refresh();
        }

        private void OnEnable() { if (button != null) button.onClick.AddListener(TogglePause); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) TogglePause();
        }

        private void ClearButtonSelection()
        {
            var events = EventSystem.current;
            if (events != null && button != null && events.currentSelectedGameObject == button.gameObject)
                events.SetSelectedGameObject(null);
        }

        public void TogglePause()
        {
            ClearButtonSelection();
            if (life != null && life.IsDead) return;
            if (!IsPaused && Time.timeScale <= 0) return;
            if (IsPaused) Time.timeScale = previousTimeScale;
            else { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            IsPaused = !IsPaused;
            Refresh();
        }

        private void Refresh()
        {
            if (blackout != null) blackout.SetActive(IsPaused);
            if (buttonImage != null)
            {
                buttonImage.overrideSprite = null;
                buttonImage.sprite = IsPaused ? playSprite : pauseSprite;
            }
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(TogglePause);
            if (IsPaused)
            {
                restoreTimeOnDestroy = life != null && life.IsDead;
                if (!restoreTimeOnDestroy) Time.timeScale = previousTimeScale;
            }
            IsPaused = false;
            Refresh();
        }

        private void OnDestroy()
        {
            // Death may have occurred during a manual pause; scene unload must release our pause too.
            if (restoreTimeOnDestroy) Time.timeScale = previousTimeScale;
        }
    }
}
