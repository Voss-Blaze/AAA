using UnityEngine;

namespace ShadowTrace
{
    /// <summary>
    /// 移动平台：在「初始位置」与「初始位置 + offset」之间往返，由 ShadowMoverPlate 这类机关驱动。
    /// 支持两种驱动方式：持续按压（SetPressed）与按一下（PressOnce）。
    /// </summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [Tooltip("相对初始位置的世界位移：决定平台走多远、往哪边走。例如 (0,2) 向上 2 格，(3,0) 向右 3 格。")]
        public Vector2 offset = new Vector2(0f, 2f);

        [Tooltip("移动速度（世界单位/秒）。")]
        [Min(0.05f)] public float speed = 2f;

        [Tooltip("是否把踩在平台顶上的刚体一起驮着走。")]
        public bool carryRiders = true;

        /// <summary>平台在场景里的起始位置，也就是轨迹的一端。</summary>
        public Vector2 Origin { get; private set; }

        /// <summary>轨迹的另一端。</summary>
        public Vector2 Destination => Origin + offset;

        /// <summary>平台当前的实际位置。</summary>
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        /// <summary>当前是否正在移动。</summary>
        public bool IsMoving { get; private set; }

        /// <summary>持续按压模式下是否正被踩着。</summary>
        public bool IsPressed { get; private set; }
        public Vector2 MotionVelocity { get; private set; }

        private const float ArriveSqrEpsilon = 0.0001f;
        private Rigidbody2D body;
        private BoxCollider2D shape;
        private bool heldDriving;
        private bool heldPressed;
        private bool toggleDriving;
        private bool toggleMoving;
        private bool pendingPress;
        private bool atDestination;
        private Vector2 toggleGoal;
        private readonly Collider2D[] riders = new Collider2D[16];

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();
            // Kinematic：不吃重力、不会被角色推动，但能挡住角色、把角色顶起来。
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // 让「停留」影子那类 Kinematic 刚体也能站在平台上。
            body.useFullKinematicContacts = true;
            Origin = body.position;
        }

        /// <summary>持续按压驱动：踩着就朝终点走，松开后沿原路回起始位置并停住。</summary>
        public void SetPressed(bool pressed)
        {
            heldDriving = true;
            heldPressed = pressed;
            IsPressed = pressed;
        }

        /// <summary>按一下驱动：去另一个端点；若正在移动，这一下等走到端点后再反向。</summary>
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
            if (toggleDriving) IsMoving = TickToggle();
            else if (heldDriving) IsMoving = TickHeld();
            else IsMoving = false;
        }

        private bool TickHeld()
        {
            return !Step(heldPressed ? Destination : Origin);
        }

        private bool TickToggle()
        {
            if (!toggleMoving) return false;
            if (!Step(toggleGoal)) return true;
            toggleMoving = false;
            atDestination = !atDestination;
            if (!pendingPress) return false;
            // 到端点后立刻反向，不在端点停留。
            pendingPress = false;
            StartLeg(atDestination ? Origin : Destination);
            return true;
        }

        private void StartLeg(Vector2 goal)
        {
            toggleGoal = goal;
            toggleMoving = true;
        }

        /// <summary>朝 goal 走一步；返回 true 表示已经站在 goal 上了。</summary>
        private bool Step(Vector2 goal)
        {
            Vector2 here = body.position;
            if ((here - goal).sqrMagnitude <= ArriveSqrEpsilon) return true;
            Vector2 next = Vector2.MoveTowards(here, goal, speed * Time.fixedDeltaTime);
            if (next == here) return false;
            MotionVelocity = (next - here) / Time.fixedDeltaTime;
            body.MovePosition(next);
            if (carryRiders) CarryRiders(next - here);
            return false;
        }

        /// <summary>把站在平台顶面上的刚体一起搬 delta 那么远。</summary>
        private void CarryRiders(Vector2 delta)
        {
            if (shape == null) return;
            Bounds top = shape.bounds;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(~0);
            filter.useTriggers = false;
            var size = new Vector2(Mathf.Max(top.size.x - 0.02f, 0.1f), 0.12f);
            var centre = new Vector2(top.center.x, top.max.y + 0.02f);
            int count = Physics2D.OverlapBox(centre, size, 0f, filter, riders);
            for (int i = 0; i < count; i++)
            {
                var collider = riders[i];
                if (collider == null) continue;
                var rider = collider.attachedRigidbody;
                if (rider == null || rider == body || !rider.simulated) continue;
                var player = rider.GetComponent<PlayerController2D>();
                if (player != null && !player.RefreshGrounding()) continue;
                if (player == null && rider.velocity.y > Mathf.Max(0, MotionVelocity.y) + 0.1f) continue;
                rider.position += delta;
            }
        }

        private void OnDrawGizmos()
        {
            Vector2 origin = body != null ? Origin : (Vector2)transform.position;
            Vector2 end = origin + offset;
            Gizmos.color = new Color(0.25f, 0.75f, 1f, 1f);
            Gizmos.DrawWireSphere(origin, 0.12f);
            Gizmos.DrawWireSphere(end, 0.12f);
            Gizmos.DrawLine(origin, end);
            Vector2 tick = new Vector2(-offset.y, offset.x).normalized * 0.25f;
            Gizmos.DrawLine(origin - tick, origin + tick);
            Gizmos.DrawLine(end - tick, end + tick);
        }
    }
}
