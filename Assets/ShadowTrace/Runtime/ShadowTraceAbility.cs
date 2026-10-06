using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(PlayerController2D))]
    public sealed class ShadowTraceAbility : MonoBehaviour
    {
        public ShadowManager manager;
        [Min(0.01f)] public float preparationDuration = 3;
        [Min(0.01f)] public float recordingDuration = 6;
        [Min(0.01f)] public float stopThreshold = 0.3f;
        public TracePhase Phase { get; private set; }
        public float Remaining => Phase == TracePhase.Idle ? 0 : Mathf.Max(0, duration - elapsed);
        public ShadowKind PreviewKind => recording == null ? ShadowKind.Stay : recording.Classify(stopThreshold);
        private PlayerController2D player;
        private TraceClassification recording;
        private Vector2 origin;
        private float elapsed;
        private float duration;

        private void Awake() { player = GetComponent<PlayerController2D>(); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E)) UseTrace();
        }

        /// <summary>The same action as E, also usable by a future input adapter.</summary>
        public void UseTrace()
        {
            if (Phase == TracePhase.Preparing)
            {
                ResetTrace();
                if (manager != null) manager.ShowMessage("Preparation cancelled.");
            }
            else if (Phase == TracePhase.Recording)
            {
                // E may arrive between physics ticks; include the current direction without adding time.
                int input = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
                recording.Sample(input, 0);
                Finish();
            }
            else if (manager == null) Debug.LogWarning("ShadowTraceAbility needs a ShadowManager.", this);
            else if (manager.Full) manager.ShowMessage("Limit reached: delete a shadow first.");
            else { Phase = TracePhase.Preparing; elapsed = 0; duration = preparationDuration; }
        }

        private void FixedUpdate()
        {
            if (Phase == TracePhase.Idle) return;
            if (Phase == TracePhase.Recording)
                recording.Sample(player.HorizontalInput, Mathf.Min(Time.fixedDeltaTime, duration - elapsed));
            elapsed += Time.fixedDeltaTime;
            if (elapsed + 0.00001f < duration) return;
            if (Phase == TracePhase.Preparing)
            {
                origin = player.Body.position;
                recording = new TraceClassification();
                // Capture initial intent even if E immediately ends the recording on the next frame.
                recording.Sample(player.HorizontalInput, 0);
                Phase = TracePhase.Recording; elapsed = 0; duration = recordingDuration;
            }
            else Finish();
        }

        private void Finish()
        {
            if (manager != null) manager.TrySpawn(recording.Classify(stopThreshold), origin, recording.InitialDirection);
            ResetTrace();
        }

        private void ResetTrace() { Phase = TracePhase.Idle; elapsed = 0; recording = null; }
        private void OnDisable() { ResetTrace(); }
    }
}
