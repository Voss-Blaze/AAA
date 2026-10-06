using UnityEngine;

namespace ShadowTrace
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PushableBody2D : MonoBehaviour
    {
        [Min(0.1f)] public float maximumHorizontalSpeed = 4;
        private Rigidbody2D body;
        private void Awake() { body = GetComponent<Rigidbody2D>(); }
        public bool CanPush => body != null && body.bodyType == RigidbodyType2D.Dynamic &&
            (body.constraints & RigidbodyConstraints2D.FreezePositionX) == 0;
        public void Push(int direction, float force)
        {
            if (!CanPush) return;
            if (body.velocity.x * direction < maximumHorizontalSpeed)
                body.AddForce(Vector2.right * (direction * force), ForceMode2D.Force);
        }
    }
}
