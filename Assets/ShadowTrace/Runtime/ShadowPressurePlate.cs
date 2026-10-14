using UnityEngine;

namespace ShadowTrace
{
    public enum PressurePlateMode { Sustained, Latched }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ShadowPressurePlate : MonoBehaviour
    {
        public PressurePlateMode mode = PressurePlateMode.Sustained;
        public InteractiveDoor targetDoor;
        public BoxCollider2D pressArea;
        public Sprite closedSprite;
        public Sprite activeSprite;
        public bool IsActive { get; private set; }
        private BoxCollider2D support;
        private SpriteRenderer sprite;
        private InteractiveDoor registeredDoor;
        private readonly Collider2D[] overlaps = new Collider2D[32];

        private void Awake()
        {
            support = GetComponent<BoxCollider2D>(); sprite = GetComponent<SpriteRenderer>();
            if (closedSprite != null) sprite.sprite = closedSprite;
        }

        private void FixedUpdate() { Refresh(); }

        public void Refresh()
        {
            if (registeredDoor != targetDoor)
            {
                if (registeredDoor != null) registeredDoor.Unregister(this);
                registeredDoor = targetDoor;
                if (registeredDoor != null) registeredDoor.Register(this);
            }
            bool pressed = false;
            if (isActiveAndEnabled && pressArea != null && support != null)
            {
                var filter = new ContactFilter2D().NoFilter();
                int count = pressArea.OverlapCollider(filter, overlaps);
                var surface = support.bounds;
                for (int i = 0; i < count; i++)
                {
                    var shadow = overlaps[i].GetComponentInParent<ShadowActor>();
                    if (shadow == null || !shadow.isActiveAndEnabled || shadow.Body == null || !shadow.Body.simulated) continue;
                    var feet = shadow.Shape.bounds;
                    // Side contacts and ascending jumps do not count as pressing the top surface.
                    if (Mathf.Abs(feet.min.y - surface.max.y) <= 0.12f && shadow.Body.velocity.y <= 0.1f &&
                        feet.center.x >= surface.min.x && feet.center.x <= surface.max.x)
                    { pressed = true; break; }
                }
            }
            IsActive = mode == PressurePlateMode.Latched ? IsActive || pressed : pressed;
            var frame = IsActive ? activeSprite : closedSprite;
            if (frame != null && sprite != null) sprite.sprite = frame;
        }
    }
}
