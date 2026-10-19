
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

        private Collider2D groundSupport;
        private bool leavingByJump;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        public bool RefreshGrounding()
        {
            if (Body == null ||
                !TracePhysics.TryGetSupport(
                    Shape, worldMask, hits,
                    out var support, out var surfaceVelocity))
            {
                groundSupport = null;
                leavingByJump = false;
                IsGrounded = false;
                return false;
            }

            float relativeY = Body.velocity.y - surfaceVelocity.y;

            bool movingSurface =
                support.GetComponentInParent<MovingPlatform>() != null;

            bool continuingRide =
                !leavingByJump &&
                groundSupport == support &&
                movingSurface;

            bool landingOnPlatform =
                movingSurface &&
                !leavingByJump &&
                Body.velocity.y <= Mathf.Max(0, surfaceVelocity.y) + 0.1f;

            IsGrounded =
                relativeY <= 0.1f ||
                continuingRide ||
                landingOnPlatform;

            if (relativeY <= 0.1f)
                leavingByJump = false;

            groundSupport = IsGrounded ? support : null;

            return IsGrounded;
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Shape = GetComponent<BoxCollider2D>();

            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            if (Time.timeScale <= 0)
            {
                HorizontalInput = 0;
                sprint = false;
                jumpRequested = false;

                GameAudioManager.Instance?.SetFootsteps(false, false);
                return;
            }

            HorizontalInput =
                (Input.GetKey(KeyCode.D) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) ? 1 : 0);

            sprint =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.Space))
                RequestJump();
        }

        public void RequestJump()
        {
            if (Time.timeScale <= 0)
                return;

            jumpRequested = true;
            JumpRequested?.Invoke();
        }

        private void FixedUpdate()
        {
            RefreshGrounding();

            var velocity = Body.velocity;
            bool supported = IsGrounded;

            // 读取当前脚下移动平台的速度。
            Vector2 platformVelocity = Vector2.zero;

            if (supported && groundSupport != null)
            {
                MovingPlatform platform =
                    groundSupport.GetComponentInParent<MovingPlatform>();

                if (platform != null)
                    platformVelocity = platform.MotionVelocity;
            }

            // 保留原有落地音效逻辑。
            if (!hasLandedOnce)
            {
                hasLandedOnce = supported;
            }
            else if (airborneSinceLanding && supported)
            {
                GameAudioManager.Instance?.PlaySound(GameSound.Landing);
                airborneSinceLanding = false;
            }

            if (hasLandedOnce && !supported)
                airborneSinceLanding = true;

            bool moving =
                supported &&
                HorizontalInput != 0 &&
                Mathf.Abs(velocity.x - platformVelocity.x) > 0.05f;

            // 人物的移动速度 + 平台的水平速度。
            velocity.x =
                HorizontalInput * (sprint ? sprintSpeed : walkSpeed)
                + platformVelocity.x;

            // 站在移动平台上时，跟随平台的垂直运动。
            if (supported && !jumpRequested)
            {
                velocity.y = platformVelocity.y;
            }

            // 跳跃时继承平台的垂直速度。
            if (jumpRequested && IsGrounded)
            {
                velocity.y = jumpSpeed + platformVelocity.y;

                moving = false;
                airborneSinceLanding = hasLandedOnce;

                leavingByJump = true;
                IsGrounded = false;
                groundSupport = null;
            }

            GameAudioManager.Instance?.SetFootsteps(moving, sprint);

            jumpRequested = false;
            Body.velocity = velocity;
        }

        private void OnDisable()
        {
            HorizontalInput = 0;
            jumpRequested = false;

            GameAudioManager.Instance?.SetFootsteps(false, false);
        }
    }
}
