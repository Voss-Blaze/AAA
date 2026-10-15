using UnityEngine;

namespace ShadowTrace
{
    public enum ShadowNotice { Spawned, LimitReached, PreparationCancelled, Cleared, MissingReferences, SpawnBlocked }

    [CreateAssetMenu(menuName = "Shadow Trace/UI Settings")]
    public sealed class ShadowUISettings : ScriptableObject
    {
        public string preparationFormat;
        public string recordingFormat;
        [TextArea] public string spawned;
        [TextArea] public string limitReached;
        [TextArea] public string preparationCancelled;
        [TextArea] public string cleared;
        [TextArea] public string missingReferences;
        [TextArea] public string spawnBlocked;
        [Min(0)] public float noticeDuration = 2;
        public Font shadowNumberFont;
        public string shadowNumberFormat;
        public Vector3 shadowNumberOffset = new Vector3(0, 0.25f, 0);
        [Min(1)] public int shadowNumberFontSize = 64;
        [Min(0.001f)] public float shadowNumberCharacterSize = 0.08f;
        public Color shadowNumberColor = Color.white;

        public string Notice(ShadowNotice notice)
        {
            switch (notice)
            {
                case ShadowNotice.Spawned: return spawned;
                case ShadowNotice.LimitReached: return limitReached;
                case ShadowNotice.PreparationCancelled: return preparationCancelled;
                case ShadowNotice.Cleared: return cleared;
                case ShadowNotice.MissingReferences: return missingReferences;
                case ShadowNotice.SpawnBlocked: return spawnBlocked;
                default: return string.Empty;
            }
        }

        public string Countdown(TracePhase phase, float remaining)
        {
            string format = phase == TracePhase.Preparing ? preparationFormat : recordingFormat;
            return (format ?? string.Empty).Replace("{0}", Mathf.Max(1, Mathf.CeilToInt(remaining)).ToString());
        }
    }
}
