using UnityEngine;

namespace ShadowTrace
{
    public sealed class ShadowTraceHUD : MonoBehaviour
    {
        public ShadowTraceAbility ability;
        public ShadowManager manager;
        public PlayerLife life;
        public InteractiveDoor[] doors;
        private GUIStyle label;
        private void OnGUI()
        {
            if (ability == null || manager == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Box(new Rect(12, 12, Mathf.Min(620, Screen.width - 24), 215), GUIContent.none);
            GUILayout.BeginArea(new Rect(24, 20, Mathf.Min(595, Screen.width - 48), 200));
            GUILayout.Label("A / D: Move   Shift: Sprint   Space: Jump", label);
            string selection = manager.Capacity == 1 ? "1" : "1-" + Mathf.Min(manager.Capacity, 9);
            if (manager.Capacity > 9) selection += " / Tab";
            GUILayout.Label("E: Prepare / Cancel / Finish   " + selection + ": Select   R: Delete   R R: Clear", label);
            string state = ability.Phase == TracePhase.Idle ? "Ready" :
                ability.Phase + "  " + ability.Remaining.ToString("0.0") + "s";
            if (ability.Phase == TracePhase.Recording) state += "  -> " + ability.PreviewKind;
            GUILayout.Label(state + "   Shadows: " + manager.Count + "/" + manager.Capacity + "   Selected: " +
                (manager.SelectedSlot < 0 ? "none" : (manager.SelectedSlot + 1).ToString()), label);
            if (Time.unscaledTime < manager.MessageUntil) GUILayout.Label(manager.Message, label);
            if (life != null && life.IsDead) GUILayout.Label("You died. Game paused. Stop Play to restart.", label);
            else if (doors != null)
                foreach (var door in doors)
                    if (door != null && door.Contains(manager.player))
                    {
                        GUILayout.Label(door.PassageGranted ? "Passage allowed." :
                            door.IsUnlocked ? "F: Pass through door (clears all shadows)." : "Door locked: activate the shadow plates.", label);
                        break;
                    }
            GUILayout.EndArea();
            Camera camera = Camera.main;
            if (camera == null) return;
            foreach (var actor in manager.Actors)
            {
                if (actor == null) continue;
                Vector3 point = camera.WorldToScreenPoint(actor.transform.position + Vector3.up * 1.1f);
                if (point.z <= 0) continue;
                GUI.Label(new Rect(point.x - 45, Screen.height - point.y, 150, 26),
                    "#" + actor.Number + " " + actor.Kind + (actor.Stopped ? " [Platform]" : ""), label);
            }
        }
    }
}
