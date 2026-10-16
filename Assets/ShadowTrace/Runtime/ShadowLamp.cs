using UnityEngine;

namespace ShadowTrace
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class ShadowLamp : MonoBehaviour
    {
        public Sprite unlitSprite;
        public Sprite litSprite;
        public string checkpointId;
        public Transform respawnPoint;
        public Vector3 respawnOffset = new Vector3(1.5f, 2, 0);
        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position + respawnOffset;
        public event System.Action<ShadowLamp> Lit;
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
            RestoreLit();
            GameAudioManager.Instance?.PlaySound(GameSound.LampLit);
            Lit?.Invoke(this);
        }

        public void RestoreLit()
        {
            IsLit = true;
            if (litSprite != null && sprite != null) sprite.sprite = litSprite;
        }
    }
}
