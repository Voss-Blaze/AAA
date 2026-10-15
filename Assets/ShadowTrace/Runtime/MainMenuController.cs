using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public GameObject menuPanel;
        public Button startButton;
        public Button quitButton;
        public Button settingsButton;
        public GameObject startPanel;
        public Button startOKButton;
        public Button startNOButton;
        public GameObject quitPanel;
        public Button quitOKButton;
        public Button quitNOButton;
        public GameObject settingsPanel;
        public Button tutorialButton;
        public Button audioButton;
        public Button backButton;
        public GameObject tutorialPanel;
        public GameObject audioPanel;
        [SerializeField] private string level1ScenePath;

        private CanvasGroup menuGroup;
        private Sprite tutorialNormal;
        private Sprite tutorialSelected;
        private Sprite audioNormal;
        private Sprite audioSelected;

        private void Awake()
        {
            if (menuPanel != null)
            {
                menuGroup = menuPanel.GetComponent<CanvasGroup>();
                if (menuGroup == null) menuGroup = menuPanel.AddComponent<CanvasGroup>();
            }
            CaptureTab(tutorialButton, out tutorialNormal, out tutorialSelected);
            CaptureTab(audioButton, out audioNormal, out audioSelected);
            ShowTutorial();
            ClosePanels();
        }

        private static void CaptureTab(Button button, out Sprite normal, out Sprite selected)
        {
            normal = button != null && button.image != null ? button.image.sprite : null;
            selected = button == null ? null : button.spriteState.selectedSprite;
            if (selected == null && button != null) selected = button.spriteState.pressedSprite;
            if (selected == null && button != null) selected = button.spriteState.highlightedSprite;
            if (selected == null) selected = normal;
            // Keep the configured active sprite visible after pointer release and pointer exit.
            if (button != null) button.transition = Selectable.Transition.None;
        }

        private void OnEnable() { Bind(true); }
        private void OnDisable() { Bind(false); }

        private void Bind(bool add)
        {
            Listen(startButton, OpenStart, add);
            Listen(quitButton, OpenQuit, add);
            Listen(settingsButton, OpenSettings, add);
            Listen(startOKButton, ConfirmStart, add);
            Listen(startNOButton, ClosePanels, add);
            Listen(quitOKButton, ConfirmQuit, add);
            Listen(quitNOButton, ClosePanels, add);
            Listen(tutorialButton, ShowTutorial, add);
            Listen(audioButton, ShowAudio, add);
            Listen(backButton, ClosePanels, add);
        }

        private static void Listen(Button button, UnityAction action, bool add)
        {
            if (button == null) return;
            if (add) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }

        public void OpenStart() { ShowPanel(startPanel); }
        public void OpenQuit() { ShowPanel(quitPanel); }
        public void OpenSettings() { ShowTutorial(); ShowPanel(settingsPanel); }
        public void ClosePanels() { ShowPanel(null); }

        private void ShowPanel(GameObject panel)
        {
            if (startPanel != null) startPanel.SetActive(panel == startPanel);
            if (quitPanel != null) quitPanel.SetActive(panel == quitPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (panel != null) panel.transform.SetAsLastSibling();
            if (menuGroup != null) menuGroup.interactable = panel == null;
            if (settingsButton != null) settingsButton.interactable = panel == null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowTutorial() { ShowTab(true); }
        public void ShowAudio() { ShowTab(false); }

        private void ShowTab(bool tutorial)
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(tutorial);
            if (audioPanel != null) audioPanel.SetActive(!tutorial);
            SetTabSprite(tutorialButton, tutorial ? tutorialSelected : tutorialNormal);
            SetTabSprite(audioButton, tutorial ? audioNormal : audioSelected);
        }

        private static void SetTabSprite(Button button, Sprite sprite)
        {
            if (button == null || button.image == null) return;
            button.image.overrideSprite = null;
            button.image.sprite = sprite;
        }

        public void ConfirmStart()
        {
            if (startPanel == null || !startPanel.activeInHierarchy) return;
            if (string.IsNullOrEmpty(level1ScenePath) || !Application.CanStreamedLevelBeLoaded(level1ScenePath))
            {
                Debug.LogError("Configure a built scene in MainMenuController.level1ScenePath.", this);
                return;
            }
            Time.timeScale = 1;
            SceneManager.LoadScene(level1ScenePath);
        }

        public void ConfirmQuit()
        {
            if (quitPanel == null || !quitPanel.activeInHierarchy) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
