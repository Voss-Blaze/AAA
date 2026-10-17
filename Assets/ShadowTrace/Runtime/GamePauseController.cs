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
        public Button continueButton;
        public Button checkpointButton;
        public Button restartButton;
        public LevelCheckpointController checkpoint;
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
            DisableNavigation(continueButton);
            DisableNavigation(checkpointButton);
            DisableNavigation(restartButton);
            Refresh();
        }

        private static void DisableNavigation(Button target)
        {
            if (target == null) return;
            var navigation = target.navigation; navigation.mode = Navigation.Mode.None;
            target.navigation = navigation;
        }

        private void OnEnable()
        {
            if (life != null) life.Died += HideForDeath;
            if (button != null) button.onClick.AddListener(TogglePause);
            if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
            if (checkpointButton != null) checkpointButton.onClick.AddListener(ReturnToCheckpoint);
            if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        }

        private void HideForDeath()
        {
            // Death owns the freeze now; hide the menu without briefly resuming gameplay.
            if (IsPaused) restoreTimeOnDestroy = true;
            IsPaused = false;
            Refresh();
        }

        public void ContinueGame() { if (IsPaused) TogglePause(); }
        public void ReturnToCheckpoint() { if (IsPaused && checkpoint != null) checkpoint.ReloadLevel(true); }
        public void RestartLevel() { if (IsPaused && checkpoint != null) checkpoint.ReloadLevel(false); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) UsePauseKey();
        }

        public void UsePauseKey()
        {
            if (LevelStoryController.InputBlocked || (life != null && life.IsDead)) return;
            TogglePause();
            GameAudioManager.Instance?.PlaySound(GameSound.HInteract);
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
            if (LevelStoryController.InputBlocked || (life != null && life.IsDead)) return;
            if (!IsPaused && Time.timeScale <= 0) return;
            if (IsPaused) Time.timeScale = previousTimeScale;
            else { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            IsPaused = !IsPaused;
            GameAudioManager.Instance?.SetGameplayPaused(IsPaused);
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
            if (life != null) life.Died -= HideForDeath;
            if (button != null) button.onClick.RemoveListener(TogglePause);
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinueGame);
            if (checkpointButton != null) checkpointButton.onClick.RemoveListener(ReturnToCheckpoint);
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartLevel);
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
