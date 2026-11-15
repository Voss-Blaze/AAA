using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class TutorialHUDView : MonoBehaviour
    {
        public GameObject guidance;
        public GameObject handbook;
        public Text title, instruction, status, feedback, pointer;
        public Text handbookText;
        public Button retry, skip, next, manual, close;
        public Sprite unselected, selected;

        private void Awake()
        {
            guidance.SetActive(false);
            handbook.SetActive(false);
            pointer.gameObject.SetActive(false);
        }

        public void Bind(FirstPlayTutorial tutorial)
        {
            retry.onClick.AddListener(tutorial.Retry);
            skip.onClick.AddListener(tutorial.Skip);
            next.onClick.AddListener(tutorial.Continue);
            manual.onClick.AddListener(() => SetHandbook(!handbook.activeSelf));
            close.onClick.AddListener(() => SetHandbook(false));
            SetHandbook(false);
        }

        public void SetHandbook(bool open)
        {
            handbook.SetActive(open);
            manual.image.sprite = open ? selected : unselected;
            manual.image.overrideSprite = null;
            var state = manual.spriteState;
            state.highlightedSprite = state.selectedSprite = open ? selected : unselected;
            state.pressedSprite = selected;
            manual.spriteState = state;
        }
    }
}
