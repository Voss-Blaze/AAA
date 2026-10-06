using UnityEngine;

namespace ShadowTrace
{
    public sealed class ShadowTraceHUD : MonoBehaviour
    {
        public ShadowTraceAbility ability;
        public ShadowManager manager;
        private GUIStyle label;
        private void OnGUI()
        {
            if (ability == null || manager == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Box(new Rect(12, 12, Mathf.Min(620, Screen.width - 24), 180), GUIContent.none);
            GUILayout.BeginArea(new Rect(24, 20, Mathf.Min(595, Screen.width - 48), 165));
            GUILayout.Label("A / D: Move   Shift: Sprint   Space: Jump", label);
            GUILayout.Label("E: Prepare / Cancel / Finish   1-8: Select   R: Delete   R R: Clear", label);
            string state = ability.Phase == TracePhase.Idle ? "Ready" :
                ability.Phase + "  " + ability.Remaining.ToString("0.0") + "s";
            if (ability.Phase == TracePhase.Recording) state += "  -> " + ability.PreviewKind;
            GUILayout.Label(state + "   Shadows: " + manager.Count + "/8   Selected: " +
                (manager.SelectedSlot < 0 ? "none" : (manager.SelectedSlot + 1).ToString()), label);
            if (Time.unscaledTime < manager.MessageUntil) GUILayout.Label(manager.Message, label);
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
