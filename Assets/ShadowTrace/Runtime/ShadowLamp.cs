using UnityEngine;

namespace ShadowTrace
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ShadowLamp : MonoBehaviour
    {
        public Sprite unlitSprite;
        public Sprite litSprite;
        public bool IsLit { get; private set; }
        private SpriteRenderer sprite;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            if (unlitSprite != null) sprite.sprite = unlitSprite;
        }

        private void OnTriggerEnter2D(Collider2D other) { Touch(other); }
        private void OnTriggerStay2D(Collider2D other) { Touch(other); }
        private void Touch(Collider2D other)
        {
            var shadow = other.GetComponentInParent<ShadowActor>();
            if (IsLit || shadow == null || !shadow.isActiveAndEnabled) return;
            IsLit = true;
            if (litSprite != null) sprite.sprite = litSprite;
        }
    }
}
