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
            if (manager != null && (life == null || !life.IsDead))
            {
                if (ability != null && ability.Phase != TracePhase.Idle && manager.uiSettings != null)
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
