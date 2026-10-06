using UnityEngine;

namespace ShadowTrace
{
    public sealed class TraceCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new Vector2(3, 1.5f);
        [Min(0.01f)] public float smoothTime = 0.15f;
        private Vector3 velocity;
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 destination = new Vector3(target.position.x + offset.x, target.position.y + offset.y, -10);
            transform.position = Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
        }
    }
}
