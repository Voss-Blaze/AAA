using UnityEngine;

namespace ShadowTrace
{
    public enum ActorMotionState { Idle, Walk, Run, Jump, Shadowtrace }

    /// <summary>One controller asset, independent movement and playback for every actor.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorAnimationDriver : MonoBehaviour
    {
        public const float MovementThreshold = 0.05f;
        public const float CastDuration = 0.5f;
        public ActorMotionState State { get; private set; }
        private static readonly int MotionState = Animator.StringToHash("MotionState");
        private PlayerController2D player;
        private ShadowActor shadow;
        private ShadowTraceAbility ability;
        private Animator animator;
        private SpriteRenderer sprite;
        private float castUntil;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void OnEnable()
        {
            player = GetComponent<PlayerController2D>();
            shadow = GetComponent<ShadowActor>();
            ability = GetComponent<ShadowTraceAbility>();
            animator = GetComponentInChildren<Animator>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            if (ability != null) ability.ShadowSpawned += OnShadowSpawned;
        }

        private bool Grounded => player != null ? player.Body != null && player.Body.velocity.y <= 0.1f &&
            TracePhysics.Grounded(player.Shape, player.worldMask, hits) : shadow != null && shadow.IsGrounded;

        private void OnShadowSpawned()
        {
            if (player != null && Grounded && player.HorizontalInput == 0 &&
                Mathf.Abs(player.Body.velocity.x) <= MovementThreshold)
                castUntil = Time.time + CastDuration;
        }

        private void LateUpdate()
        {
            var body = player != null ? player.Body : shadow != null ? shadow.Body : null;
            if (body == null || animator == null || sprite == null) return;
            bool grounded = Grounded;
            bool moving = Mathf.Abs(body.velocity.x) > MovementThreshold;
            int direction = player != null ? player.HorizontalInput : shadow.AnimationDirection;
            if (direction != 0) sprite.flipX = direction < 0;
            if (!grounded || moving || (player != null && direction != 0)) castUntil = 0;
            State = !grounded ? ActorMotionState.Jump :
                castUntil > Time.time ? ActorMotionState.Shadowtrace :
                moving ? (player != null && player.IsSprinting ? ActorMotionState.Run : ActorMotionState.Walk) :
                ActorMotionState.Idle;
            animator.SetInteger(MotionState, (int)State);
        }

        private void OnDisable()
        {
            if (ability != null) ability.ShadowSpawned -= OnShadowSpawned;
            castUntil = 0;
        }
    }
}
