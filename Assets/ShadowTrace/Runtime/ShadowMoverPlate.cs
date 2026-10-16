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
    /// 带动移动平台的触发机关：符合配置的角色与 Press Area 重叠时触发，不产生实体阻挡。
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

        /// <summary>当前是否正被踩着。</summary>
        public bool IsPressed { get; private set; }

        private SpriteRenderer sprite;
        private bool wasPressed;
        private readonly Collider2D[] overlaps = new Collider2D[32];

        private void Awake()
        {
            var ownShape = GetComponent<BoxCollider2D>();
            ownShape.isTrigger = true;
            sprite = GetComponent<SpriteRenderer>();
            if (idleSprite == null && sprite != null) idleSprite = sprite.sprite;
            if (pressArea == null)
            {
                // 没手动指定时，自动找子物体上的触发盒（PressurePlate 预制体里的 Press Area）。
                foreach (var candidate in GetComponentsInChildren<Collider2D>(true))
                {
                    if (candidate == ownShape || !candidate.isTrigger) continue;
                    pressArea = candidate as BoxCollider2D;
                    break;
                }
            }
            if (pressArea == null) pressArea = ownShape;
            pressArea.isTrigger = true;
            if (targetPlatform == null) targetPlatform = FindObjectOfType<MovingPlatform>();
            ApplySprite(false);
        }

        private void FixedUpdate() { Refresh(); }

        public void Refresh()
        {
            bool pressed = Detect();
            bool edge = pressed && !wasPressed;
            if (edge) GameAudioManager.Instance?.PlaySound(GameSound.Plate);
            wasPressed = pressed;
            IsPressed = pressed;
            ApplySprite(pressed);
            if (targetPlatform == null) return;
            if (mode == MoverPlateMode.Patrol) targetPlatform.SetPressed(pressed);
            else if (edge) targetPlatform.PressOnce();
        }

        /// <summary>触发区域中是否还有符合配置的角色。</summary>
        private bool Detect()
        {
            if (!isActiveAndEnabled || pressArea == null || !pressArea.enabled || !pressArea.gameObject.activeInHierarchy) return false;
            var filter = new ContactFilter2D().NoFilter();
            int count = pressArea.OverlapCollider(filter, overlaps);
            for (int i = 0; i < count; i++)
            {
                if (AcceptsActor(overlaps[i])) return true;
            }
            return false;
        }

        private bool AcceptsActor(Collider2D collider)
        {
            if (shadowCanPress)
            {
                var shadow = collider.GetComponentInParent<ShadowActor>();
                if (shadow != null && shadow.isActiveAndEnabled && shadow.Body != null &&
                    shadow.Body.simulated && shadow.Shape != null)
                {
                    return true;
                }
            }
            if (playerCanPress)
            {
                var player = collider.GetComponentInParent<PlayerController2D>();
                if (player != null && player.isActiveAndEnabled && player.Body != null && player.Body.simulated && player.Shape != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDisable()
        {
            wasPressed = false;
            IsPressed = false;
            ApplySprite(false);
            if (mode == MoverPlateMode.Patrol && targetPlatform != null) targetPlatform.SetPressed(false);
        }

        private void ApplySprite(bool pressed)
        {
            if (sprite == null) return;
            var frame = pressed ? pressedSprite : idleSprite;
            if (frame != null && sprite.sprite != frame) sprite.sprite = frame;
        }
    }
}
