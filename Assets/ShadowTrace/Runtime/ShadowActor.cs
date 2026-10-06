using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ShadowActor : MonoBehaviour
    {
        [Min(0.01f)] public float obstacleDistance = 0.08f;
        [Min(0.02f)] public float reverseInterval = 0.2f;
        [Min(0.05f)] public float blockedDuration = 0.4f;
        [Min(0.1f)] public float pushForce = 35;
        [Min(0.1f)] public float followSpeed = 10;
        [Min(0.001f)] public float followDeadZone = 0.025f;
        public LayerMask worldMask = ~0;
        public ShadowKind Kind { get; private set; }
        public int Number { get; private set; }
        public bool Stopped { get; private set; }
        public bool PlayerCollisionEnabled { get; private set; }
        public BoxCollider2D Shape { get; private set; }
        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private PlayerController2D player;
        private ShadowManager manager;
        private Vector2 offset;
        private Vector2 previousPosition;
        private int direction;
        private float speed;
        private float blockedTime;
        private float nextReverse;
        private Color baseColor;
        private bool initialized;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            Shape = GetComponent<BoxCollider2D>();
            sprite = GetComponent<SpriteRenderer>();
        }

        public void Initialize(ShadowKind kind, int number, int initialDirection,
            PlayerController2D owner, ShadowManager registry)
        {
            if (body == null) Awake();
            Kind = kind; Number = number; direction = initialDirection == 0 ? 1 : initialDirection;
            player = owner; manager = registry; speed = player.walkSpeed;
            offset = body.position - player.Body.position;
            previousPosition = body.position;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (kind == ShadowKind.Stay)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.useFullKinematicContacts = true;
                body.gravityScale = 0;
            }
            else
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = kind == ShadowKind.Follow ? 0 : player.Body.gravityScale;
            }
            baseColor = kind == ShadowKind.Stay ? new Color(0.4f, 0.7f, 1f, 0.8f) :
                kind == ShadowKind.Approach ? new Color(1f, 0.6f, 0.3f, 0.8f) :
                kind == ShadowKind.Avoid ? new Color(0.85f, 0.4f, 1f, 0.8f) : new Color(0.3f, 1f, 0.7f, 0.8f);
            sprite.color = baseColor;
            Physics2D.IgnoreCollision(Shape, player.Shape, true);
            initialized = true;
        }

        public void SetSelected(bool selected)
        {
            if (sprite != null) sprite.color = selected ? Color.yellow : baseColor;
        }

        private void FixedUpdate()
        {
            if (!initialized || player == null) return;
            if (Kind == ShadowKind.Stay) return;
            if (Kind == ShadowKind.Follow)
            {
                Vector2 delta = player.Body.position + offset - body.position;
                body.velocity = delta.magnitude <= followDeadZone ? Vector2.zero :
                    Vector2.ClampMagnitude(delta / Time.fixedDeltaTime, followSpeed);
                return;
            }
            if (Stopped)
            {
                // Delay enabling collisions until the player is outside the shadow.
                if (!PlayerCollisionEnabled && !Shape.Distance(player.Shape).isOverlapped)
                {
                    Physics2D.IgnoreCollision(Shape, player.Shape, false);
                    PlayerCollisionEnabled = true;
                }
                return;
            }

            var obstacle = TracePhysics.Obstacle(Shape, direction, obstacleDistance, worldMask, hits);
            if (Kind == ShadowKind.Avoid)
            {
                if (obstacle != null)
                {
                    if (Time.fixedTime >= nextReverse)
                    {
                        direction = -direction;
                        nextReverse = Time.fixedTime + reverseInterval;
                    }
                    else { body.velocity = new Vector2(0, body.velocity.y); return; }
                }
            }
            else if (obstacle != null)
            {
                var pushable = obstacle.GetComponentInParent<PushableBody2D>();
                if (pushable == null || !pushable.CanPush) { Stop(); return; }
                pushable.Push(direction, pushForce);
                float progress = (body.position.x - previousPosition.x) * direction;
                blockedTime = progress < speed * Time.fixedDeltaTime * 0.05f ? blockedTime + Time.fixedDeltaTime : 0;
                if (blockedTime >= blockedDuration) { Stop(); return; }
            }
            else blockedTime = 0;
            previousPosition = body.position;
            body.velocity = new Vector2(direction * speed, body.velocity.y);
        }

        private void Stop()
        {
            Stopped = true;
            body.velocity = new Vector2(0, body.velocity.y);
            body.constraints |= RigidbodyConstraints2D.FreezePositionX;
        }

        private void OnDestroy() { if (manager != null) manager.NotifyDestroyed(this); }
    }
}
