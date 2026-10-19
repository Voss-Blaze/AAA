using UnityEngine;

namespace ShadowTrace
{
    /// <summary>
    /// 移动平台：在初始位置与初始位置 + offset 之间移动。
    /// 支持持续按压、按一下切换方向，以及人物跟随平台移动。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [Tooltip("相对初始位置的世界位移。")]
        public Vector2 offset = new Vector2(0f, 2f);

        [Tooltip("移动速度（世界单位/秒）。")]
        [Min(0.05f)]
        public float speed = 2f;

        [Tooltip("是否带动站在平台顶部的刚体。")]
        public bool carryRiders = true;

        public Vector2 Origin { get; private set; }

        public Vector2 Destination => Origin + offset;

        public Vector2 Position =>
            body != null ? body.position : (Vector2)transform.position;

        public bool IsMoving { get; private set; }

        public bool IsPressed { get; private set; }

        public Vector2 MotionVelocity { get; private set; }

        private const float ArriveSqrEpsilon = 0.0001f;

        // 人物脚底与平台顶面的允许检测误差。
        private const float RiderBelowTolerance = 0.15f;
        private const float RiderAboveTolerance = 0.20f;

        private Rigidbody2D body;
        private BoxCollider2D shape;

        private bool heldDriving;
        private bool heldPressed;

        private bool toggleDriving;
        private bool toggleMoving;
        private bool pendingPress;
        private bool atDestination;

        private Vector2 toggleGoal;

        private readonly Collider2D[] riders = new Collider2D[32];

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;

            Origin = body.position;
        }

        /// <summary>
        /// 持续按压：按下后前进，松开后返回起点。
        /// </summary>
        public void SetPressed(bool pressed)
        {
            heldDriving = true;
            heldPressed = pressed;
            IsPressed = pressed;
        }

        /// <summary>
        /// 按一下：移动到另一端；移动过程中再次触发则到端点后反向。
        /// </summary>
        public void PressOnce()
        {
            toggleDriving = true;

            if (toggleMoving)
            {
                pendingPress = true;
                return;
            }

            StartLeg(atDestination ? Origin : Destination);
        }

        private void FixedUpdate()
        {
            MotionVelocity = Vector2.zero;

            if (body == null || offset.sqrMagnitude <= 0f)
            {
                IsMoving = false;
                return;
            }

            if (toggleDriving)
            {
                IsMoving = TickToggle();
            }
            else if (heldDriving)
            {
                IsMoving = TickHeld();
            }
            else
            {
                IsMoving = false;
            }
        }

        private bool TickHeld()
        {
            return !Step(heldPressed ? Destination : Origin);
        }

        private bool TickToggle()
        {
            if (!toggleMoving)
                return false;

            if (!Step(toggleGoal))
            {
                return true;
            }

            toggleMoving = false;
            atDestination = !atDestination;

            if (!pendingPress)
                return false;

            pendingPress = false;
            StartLeg(atDestination ? Origin : Destination);

            return true;
        }

        private void StartLeg(Vector2 goal)
        {
            toggleGoal = goal;
            toggleMoving = true;
        }

        /// <summary>
        /// 移动平台一步，并在同一个物理更新中带动平台上的刚体。
        /// </summary>
        private bool Step(Vector2 goal)
        {
            Vector2 here = body.position;

            if ((here - goal).sqrMagnitude <= ArriveSqrEpsilon)
                return true;

            Vector2 next = Vector2.MoveTowards(
                here,
                goal,
                speed * Time.fixedDeltaTime
            );

            if (next == here)
                return false;

            Vector2 delta = next - here;

            MotionVelocity = delta / Time.fixedDeltaTime;

            // 使用 Rigidbody2D 移动平台。
            body.MovePosition(next);

            // 在同一物理更新中搬运站在平台上的人物。
            if (carryRiders)
            {
                CarryRiders(delta);
            }

            return false;
        }

        /// <summary>
        /// 检测平台顶面附近的碰撞体。
        /// 不再依赖 PlayerController2D.RefreshGrounding()，
        /// 避免接地检测偶尔失败时人物漏掉平台位移。
        /// </summary>
        private void CarryRiders(Vector2 delta)
        {
            if (shape == null || delta.sqrMagnitude <= 0f)
                return;

            Bounds platformBounds = shape.bounds;
            float topY = platformBounds.max.y;

            // 只检测平台顶面附近，尽量避免把平台侧面的物体带走。
            Vector2 checkSize = new Vector2(
                Mathf.Max(platformBounds.size.x - 0.04f, 0.1f),
                0.22f
            );

            Vector2 checkCenter = new Vector2(
                platformBounds.center.x,
                topY + 0.06f
            );

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;

            int count = Physics2D.OverlapBox(
                checkCenter,
                checkSize,
                0f,
                filter,
                riders
            );

            for (int i = 0; i < count; i++)
            {
                Collider2D riderCollider = riders[i];

                if (riderCollider == null || riderCollider.isTrigger)
                    continue;

                Rigidbody2D rider = riderCollider.attachedRigidbody;

                if (rider == null ||
                    rider == body ||
                    !rider.simulated)
                {
                    continue;
                }

                // 同一个刚体可能挂有多个碰撞体，避免重复搬运。
                bool alreadyProcessed = false;

                for (int j = 0; j < i; j++)
                {
                    if (riders[j] != null &&
                        riders[j].attachedRigidbody == rider)
                    {
                        alreadyProcessed = true;
                        break;
                    }
                }

                if (alreadyProcessed)
                    continue;

                Bounds riderBounds = riderCollider.bounds;
                float feetY = riderBounds.min.y;

                // 只搬运脚底接近平台顶面的物体。
                if (feetY < topY - RiderBelowTolerance ||
                    feetY > topY + RiderAboveTolerance)
                {
                    continue;
                }

                // 直接补偿平台的位移，保留人物原有速度。
                rider.position = rider.position + delta;
            }
        }

        private void OnDrawGizmos()
        {
            Vector2 origin = body != null
                ? Origin
                : (Vector2)transform.position;

            Vector2 end = origin + offset;

            Gizmos.color = new Color(0.25f, 0.75f, 1f, 1f);

            Gizmos.DrawWireSphere(origin, 0.12f);
            Gizmos.DrawWireSphere(end, 0.12f);
            Gizmos.DrawLine(origin, end);

            Vector2 tick =
                new Vector2(-offset.y, offset.x).normalized * 0.25f;

            Gizmos.DrawLine(origin - tick, origin + tick);
            Gizmos.DrawLine(end - tick, end + tick);
        }
    }
}