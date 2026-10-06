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
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Shape { get; private set; }
        public event Action JumpRequested;
        private bool jumpRequested;
        private bool sprint;
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
            HorizontalInput = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
        }

        /// <summary>Broadcast intent even if the player is airborne; each follower checks its own ground.</summary>
        public void RequestJump()
        {
            jumpRequested = true;
            JumpRequested?.Invoke();
        }

        private void FixedUpdate()
        {
            IsGrounded = TracePhysics.Grounded(Shape, worldMask, hits);
            var velocity = Body.velocity;
            velocity.x = HorizontalInput * (sprint ? sprintSpeed : walkSpeed);
            if (jumpRequested && IsGrounded && velocity.y <= 0.1f) velocity.y = jumpSpeed;
            jumpRequested = false;
            Body.velocity = velocity;
        }

        private void OnDisable() { HorizontalInput = 0; jumpRequested = false; }
    }
}
