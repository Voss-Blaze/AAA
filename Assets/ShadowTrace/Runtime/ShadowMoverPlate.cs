using UnityEngine;

namespace ShadowTrace
{
    /// <summary>机关的触发方式。</summary>
    public enum MoverPlateMode
    {
        /// <summary>巡逻：踩着就一直朝终点走，松开后沿原路回起始位置并停住。</summary>
        Patrol,

        /// <summary>交替：按一下换一次方向；必须离开之后再踩才算一次。</summary>
        Toggle,
    }

    /// <summary>
    /// 带动移动平台的压板。按压判定与 ShadowPressurePlate 一致：踩在板顶、脚底贴合板面、且不是在往上跳。
    /// 只认「生成的影子」（ShadowActor）：玩家踩上去不触发。被踩住时切换到 pressedSprite，平台按 mode 移动。
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ShadowMoverPlate : MonoBehaviour
    {
        [Tooltip("巡逻：踩着就移动，松开就回来。交替：按一下走一次。")]
        public MoverPlateMode mode = MoverPlateMode.Patrol;

        [Tooltip("被这个机关驱动的移动平台。留空时自动找场景里的第一个。")]
        public MovingPlatform targetPlatform;

        [Tooltip("检测按压用的触发盒，一般是子物体 Press Area。")]
        public BoxCollider2D pressArea;

        [Tooltip("没人踩时显示的精灵。留空则沿用物体当前精灵。")]
        public Sprite idleSprite;

        [Tooltip("被踩住时显示的精灵。留空则不换图。")]
        public Sprite pressedSprite;

        [Tooltip("角色玩家踩上去也能触发。默认关闭：机关只认影子。")]
        public bool playerCanPress = false;

        [Tooltip("影子踩上去也能触发。")]
        public bool shadowCanPress = true;

        [Tooltip("脚底与板面之间允许的高度差，超过就不算踩在板顶。")]
        [Min(0f)] public float pressTolerance = 0.12f;

        /// <summary>当前是否正被踩着。</summary>
        public bool IsPressed { get; private set; }

        private BoxCollider2D support;
        private SpriteRenderer sprite;
        private bool wasPressed;
        private readonly Collider2D[] overlaps = new Collider2D[32];

        private void Awake()
        {
            support = GetComponent<BoxCollider2D>();
            sprite = GetComponent<SpriteRenderer>();
            if (idleSprite == null && sprite != null) idleSprite = sprite.sprite;
            if (pressArea == null)
            {
                // 没手动指定时，自动找子物体上的触发盒（PressurePlate 预制体里的 Press Area）。
                foreach (var candidate in GetComponentsInChildren<Collider2D>(true))
                {
                    if (candidate == support || !candidate.isTrigger) continue;
                    pressArea = candidate as BoxCollider2D;
                    break;
                }
            }
            if (targetPlatform == null) targetPlatform = FindObjectOfType<MovingPlatform>();
            ApplySprite(false);
        }

        private void FixedUpdate()
        {
            bool pressed = Detect();
            bool edge = pressed && !wasPressed;
            wasPressed = pressed;
            IsPressed = pressed;
            ApplySprite(pressed);
            if (targetPlatform == null) return;
            if (mode == MoverPlateMode.Patrol) targetPlatform.SetPressed(pressed);
            else if (edge) targetPlatform.PressOnce();
        }

        /// <summary>是否有人踩在板顶。</summary>
        private bool Detect()
        {
            if (!isActiveAndEnabled || support == null || pressArea == null || !pressArea.enabled) return false;
            var surface = support.bounds;
            var filter = new ContactFilter2D().NoFilter();
            int count = pressArea.OverlapCollider(filter, overlaps);
            for (int i = 0; i < count; i++)
            {
                if (!TryActor(overlaps[i], out var shape, out var body)) continue;
                var feet = shape.bounds;
                // 侧面蹭到和正在往上跳的都不算踩在板顶。
                if (Mathf.Abs(feet.min.y - surface.max.y) > pressTolerance) continue;
                if (body.velocity.y > 0.1f) continue;
                if (feet.center.x < surface.min.x || feet.center.x > surface.max.x) continue;
                return true;
            }
            return false;
        }

        private bool TryActor(Collider2D collider, out Collider2D shape, out Rigidbody2D body)
        {
            shape = null;
            body = null;
            if (shadowCanPress)
            {
                var shadow = collider.GetComponentInParent<ShadowActor>();
                if (shadow != null && shadow.isActiveAndEnabled && shadow.Body != null &&
                    shadow.Body.simulated && shadow.Shape != null)
                {
                    shape = shadow.Shape;
                    body = shadow.Body;
                    return true;
                }
            }
            if (playerCanPress)
            {
                var player = collider.GetComponentInParent<PlayerController2D>();
                if (player != null && player.isActiveAndEnabled && player.Body != null && player.Shape != null)
                {
                    shape = player.Shape;
                    body = player.Body;
                    return true;
                }
            }
            return false;
        }

        private void ApplySprite(bool pressed)
        {
            if (sprite == null) return;
            var frame = pressed ? pressedSprite : idleSprite;
            if (frame != null && sprite.sprite != frame) sprite.sprite = frame;
        }
    }
}
