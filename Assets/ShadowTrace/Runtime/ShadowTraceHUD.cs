using UnityEngine;
using UnityEngine.UI;

namespace ShadowTrace
{
    public sealed class ShadowTraceHUD : MonoBehaviour
    {
        public ShadowTraceAbility ability;
        public ShadowManager manager;
        public PlayerLife life;
        public InteractiveDoor[] doors;
        public GameObject[] occupiedIcons;
        public GameObject tooltip;
        public Text tooltipText;
        private string priorityMessage;
        private float priorityUntil;

        public void ShowPriorityMessage(string text, float duration)
        {
            priorityMessage = text; priorityUntil = Time.time + duration; Refresh();
        }

        private void Start() { Refresh(); }
        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (occupiedIcons != null)
                for (int i = 0; i < occupiedIcons.Length; i++)
                    if (occupiedIcons[i] != null)
                        occupiedIcons[i].SetActive(manager != null && i < manager.Capacity && manager.Actors[i] != null);

            if (tooltip == null || tooltipText == null) return;
            string message = string.Empty;
            if (!LevelStoryController.InputBlocked && manager != null && (life == null || !life.IsDead))
            {
                if (Time.time < priorityUntil) message = priorityMessage;
                else if (ability != null && ability.Phase != TracePhase.Idle && manager.uiSettings != null)
                    message = manager.uiSettings.Countdown(ability.Phase, ability.Remaining);
                else if (Time.time < manager.MessageUntil) message = manager.Message;
            }
            bool visible = !string.IsNullOrEmpty(message);
            tooltipText.text = message ?? string.Empty;
            tooltipText.gameObject.SetActive(visible);
            tooltip.SetActive(visible);
        }
    }
}
