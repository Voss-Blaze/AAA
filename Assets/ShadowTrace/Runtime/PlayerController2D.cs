using System;
using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [Min(0.1f)] public float walkSpeed = 4;
        [Min(0.1f)] public float sprintSpeed = 7;
        [Min(0.1f)] public float jumpSpeed = 10;
        public LayerMask worldMask = ~0;
        public int HorizontalInput { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting => sprint;
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Shape { get; private set; }
        public event Action JumpRequested;
        private bool jumpRequested;
        private bool sprint;
        private bool hasLandedOnce;
        private bool airborneSinceLanding;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Shape = GetComponent<BoxCollider2D>();
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            if (Time.timeScale <= 0)
            {
                HorizontalInput = 0; sprint = false; jumpRequested = false;
                GameAudioManager.Instance?.SetFootsteps(false, false);
                return;
            }
            HorizontalInput = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
        }

        /// <summary>Broadcast intent even if the player is airborne; each follower checks its own ground.</summary>
        public void RequestJump()
        {
            if (Time.timeScale <= 0) return;
            jumpRequested = true;
            JumpRequested?.Invoke();
        }

        private void FixedUpdate()
        {
            IsGrounded = TracePhysics.Grounded(Shape, worldMask, hits);
            var velocity = Body.velocity;
            bool supported = IsGrounded && velocity.y <= 0.1f;
            if (!hasLandedOnce) hasLandedOnce = supported;
            else if (airborneSinceLanding && supported)
            {
                GameAudioManager.Instance?.PlaySound(GameSound.Landing);
                airborneSinceLanding = false;
            }
            if (hasLandedOnce && !supported) airborneSinceLanding = true;
            bool moving = supported && HorizontalInput != 0 && Mathf.Abs(velocity.x) > 0.05f;
            velocity.x = HorizontalInput * (sprint ? sprintSpeed : walkSpeed);
            if (jumpRequested && IsGrounded && velocity.y <= 0.1f)
            {
                velocity.y = jumpSpeed; moving = false; airborneSinceLanding = hasLandedOnce;
            }
            GameAudioManager.Instance?.SetFootsteps(moving, sprint);
            jumpRequested = false;
            Body.velocity = velocity;
        }

        private void OnDisable()
        {
            HorizontalInput = 0; jumpRequested = false;
            GameAudioManager.Instance?.SetFootsteps(false, false);
        }
    }
}
