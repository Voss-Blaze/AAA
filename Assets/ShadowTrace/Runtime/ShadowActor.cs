
using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(ShadowSelectionGlow))]
    public sealed class ShadowActor : MonoBehaviour
    {
        [Min(0.01f)] public float obstacleDistance = 0.08f;
        [Min(0.02f)] public float reverseInterval = 0.2f;
        [Min(0.05f)] public float blockedDuration = 0.4f;
        [Min(0.1f)] public float pushForce = 35;
        public LayerMask worldMask = ~0;

        [Header("影子身体颜色")]
        [Tooltip("身体颜色；A 控制身体与眼睛的整体透明度，眼睛的 RGB 保持白色。")]
        public Color stayBodyColor = new Color(0.4f, 0.7f, 1f, 0.8f);
        public Color approachBodyColor = new Color(1f, 0.6f, 0.3f, 0.8f);
        public Color avoidBodyColor = new Color(0.85f, 0.4f, 1f, 0.8f);
        public Color followBodyColor = new Color(0.3f, 1f, 0.7f, 0.8f);

        public ShadowKind Kind { get; private set; }
        public int Number { get; private set; }
        public bool Stopped { get; private set; }
        public bool PlayerCollisionEnabled { get; private set; }
        public BoxCollider2D Shape { get; private set; }
        public Rigidbody2D Body => body;

        public bool IsGrounded => initialized && (Kind == ShadowKind.Stay ||
            (body.velocity.y <= 0.1f &&
             TracePhysics.Grounded(Shape, worldMask, hits, false)));

        public int AnimationDirection =>
            Kind == ShadowKind.Follow && player != null
                ? player.HorizontalInput
                : direction;

        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private ShadowSelectionGlow selectionGlow;
        private PlayerController2D player;
        private ShadowManager manager;

        private bool jumpRequested;
        private bool holdingPressurePlate;

        private Vector2 previousPosition;
        private int direction;
        private float speed;
        private float blockedTime;
        private float nextReverse;
        private Color baseColor;
        private bool initialized;
        private TextMesh numberLabel;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            Shape = GetComponent<BoxCollider2D>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            selectionGlow = GetComponent<ShadowSelectionGlow>();
        }

        public void Initialize(
            ShadowKind kind,
            int number,
            int initialDirection,
            PlayerController2D owner,
            ShadowManager registry)
        {
            if (body == null)
                Awake();

            if (player != null)
                player.JumpRequested -= OnPlayerJumpRequested;

            Kind = kind;
            Number = number;
            direction = initialDirection == 0 ? 1 : initialDirection;
            player = owner;
            manager = registry;
            speed = player.walkSpeed;

            jumpRequested = false;
            holdingPressurePlate = false;
            Stopped = false;
            PlayerCollisionEnabled = false;
            blockedTime = 0f;
            nextReverse = 0f;

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
                body.gravityScale = player.Body.gravityScale;
            }

            if (kind == ShadowKind.Follow)
                player.JumpRequested += OnPlayerJumpRequested;

            baseColor = kind == ShadowKind.Stay ? stayBodyColor :
                kind == ShadowKind.Approach ? approachBodyColor :
                kind == ShadowKind.Avoid ? avoidBodyColor : followBodyColor;

            if (sprite != null)
                sprite.color = baseColor;

            if (selectionGlow != null)
                selectionGlow.SetSelected(false);

            Physics2D.IgnoreCollision(Shape, player.Shape, true);

            initialized = true;
            ConfigureNumberLabel();
        }

        private void ConfigureNumberLabel()
        {
            var settings = manager != null ? manager.uiSettings : null;

            if (settings == null || settings.shadowNumberFont == null)
                return;

            if (numberLabel == null)
            {
                var labelObject = new GameObject("Shadow Number");
                labelObject.layer = gameObject.layer;
                labelObject.transform.SetParent(transform, false);
                numberLabel = labelObject.AddComponent<TextMesh>();
            }

            numberLabel.font = settings.shadowNumberFont;
            numberLabel.fontSize = settings.shadowNumberFontSize;
            numberLabel.characterSize = settings.shadowNumberCharacterSize;
            numberLabel.color = settings.shadowNumberColor;
            numberLabel.anchor = TextAnchor.LowerCenter;
            numberLabel.alignment = TextAlignment.Center;
            numberLabel.text = Number.ToString(
                settings.shadowNumberFormat,
                System.Globalization.CultureInfo.InvariantCulture);

            var renderer = numberLabel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = settings.shadowNumberFont.material;
            renderer.sortingLayerID = sprite.sortingLayerID;
            renderer.sortingOrder = sprite.sortingOrder + 10;

            LateUpdate();
        }

        private void LateUpdate()
        {
            if (numberLabel == null ||
                manager == null ||
                manager.uiSettings == null)
                return;

            var labelTransform = numberLabel.transform;

            labelTransform.position =
                new Vector3(
                    Shape.bounds.center.x,
                    Shape.bounds.max.y,
                    transform.position.z)
                + manager.uiSettings.shadowNumberOffset;

            labelTransform.rotation = Camera.main != null
                ? Camera.main.transform.rotation
                : Quaternion.identity;

            // 保持编号正立，并抵消影子自身的缩放。
            var scale = transform.lossyScale;

            labelTransform.localScale = new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1 / scale.x : 1,
                Mathf.Abs(scale.y) > 0.0001f ? 1 / scale.y : 1,
                Mathf.Abs(scale.z) > 0.0001f ? 1 / scale.z : 1);
        }

        public void SetSelected(bool selected)
        {
            if (selectionGlow != null)
                selectionGlow.SetSelected(selected);
        }

        /// <summary>
        /// 影子踩中压力板后锁定移动。
        /// 锁定后不再移动、巡逻、跟随玩家或响应跳跃。
        /// </summary>
        public void HoldOnPressurePlate(ShadowPressurePlate plate)
        {
            if (plate == null ||
                holdingPressurePlate ||
                body == null ||
                !initialized)
                return;

            holdingPressurePlate = true;
            jumpRequested = false;

            if (body.bodyType == RigidbodyType2D.Dynamic)
            {
                // 清除水平速度，保留竖直物理运动。
                body.velocity = new Vector2(0f, body.velocity.y);

                // 固定水平位置，允许影子受重力影响站在机关上。
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;
            }
            else
            {
                // Stay 类型本身没有重力，也停止水平运动。
                body.velocity = Vector2.zero;
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;
            }
        }

        public void Despawn()
        {
            if (manager != null)
            {
                manager.Remove(this);
            }
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || player == null)
                return;

            // 优先处理机关锁定，避免其他移动逻辑重新赋予速度。
            if (holdingPressurePlate)
            {
                jumpRequested = false;

                if (body.bodyType == RigidbodyType2D.Dynamic)
                {
                    // 防止外部移动逻辑或碰撞产生的水平速度造成滑动。
                    body.velocity = new Vector2(0f, body.velocity.y);
                }
                else
                {
                    body.velocity = Vector2.zero;
                }

                return;
            }

            if (Kind == ShadowKind.Stay)
                return;

            if (Kind == ShadowKind.Follow)
            {
                var velocity = body.velocity;
                velocity.x = player.HorizontalInput * player.walkSpeed;

                if (jumpRequested &&
                    velocity.y <= 0.1f &&
                    TracePhysics.Grounded(Shape, worldMask, hits, false))
                {
                    velocity.y = player.jumpSpeed;
                }

                jumpRequested = false;
                body.velocity = velocity;
                return;
            }

            if (Stopped)
            {
                // 等玩家离开影子后再恢复双方碰撞。
                if (!PlayerCollisionEnabled &&
                    !Shape.Distance(player.Shape).isOverlapped)
                {
                    Physics2D.IgnoreCollision(Shape, player.Shape, false);
                    PlayerCollisionEnabled = true;
                }

                return;
            }

            var obstacle = TracePhysics.Obstacle(
                Shape, direction, obstacleDistance, worldMask, hits);

            if (Kind == ShadowKind.Avoid)
            {
                if (obstacle != null)
                {
                    if (Time.fixedTime >= nextReverse)
                    {
                        direction = -direction;
                        nextReverse = Time.fixedTime + reverseInterval;
                    }
                    else
                    {
                        body.velocity = new Vector2(0f, body.velocity.y);
                        return;
                    }
                }
            }
            else if (obstacle != null)
            {
                var pushable = obstacle.GetComponentInParent<PushableBody2D>();

                if (pushable == null || !pushable.CanPush)
                {
                    Stop();
                    return;
                }

                pushable.Push(direction, pushForce);

                float progress =
                    (body.position.x - previousPosition.x) * direction;

                blockedTime =
                    progress < speed * Time.fixedDeltaTime * 0.05f
                        ? blockedTime + Time.fixedDeltaTime
                        : 0f;

                if (blockedTime >= blockedDuration)
                {
                    Stop();
                    return;
                }
            }
            else
            {
                blockedTime = 0f;
            }

            previousPosition = body.position;
            body.velocity = new Vector2(direction * speed, body.velocity.y);
        }

        private void Stop()
        {
            Stopped = true;
            body.velocity = new Vector2(0f, body.velocity.y);
            body.constraints |= RigidbodyConstraints2D.FreezePositionX;
        }

        private void OnPlayerJumpRequested()
        {
            if (isActiveAndEnabled && !holdingPressurePlate)
                jumpRequested = true;
        }

        private void OnDisable()
        {
            jumpRequested = false;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.JumpRequested -= OnPlayerJumpRequested;

            if (manager != null)
                manager.NotifyDestroyed(this);
        }
    }
}
