using UnityEngine;

namespace ShadowTrace
{
    [DefaultExecutionOrder(1000), DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class CameraBackgroundFollow : MonoBehaviour
    {
        public Camera targetCamera;
        [Tooltip("背景平面与摄影机的距离。")]
        [Min(0.01f)] public float distance = 10;
        [Tooltip("覆盖余量：1 表示刚好覆盖，1.02 表示额外放大 2%。")]
        [Min(1)] public float coverage = 1.02f;
        private SpriteRenderer spriteRenderer;

        private void OnEnable() { FollowNow(); }
        private void LateUpdate() { FollowNow(); }

        public void FollowNow()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (targetCamera == null || spriteRenderer.sprite == null) return;
            var bounds = spriteRenderer.sprite.bounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0) return;

            float depth = Mathf.Clamp(distance, targetCamera.nearClipPlane + 0.01f, targetCamera.farClipPlane - 0.01f);
            float height = targetCamera.orthographic ? targetCamera.orthographicSize * 2 :
                2 * depth * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = Mathf.Max(height * targetCamera.aspect / bounds.size.x, height / bounds.size.y)
                * Mathf.Max(1, coverage);
            transform.rotation = targetCamera.transform.rotation;
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            if (Mathf.Abs(parentScale.x) < 0.0001f || Mathf.Abs(parentScale.y) < 0.0001f || Mathf.Abs(parentScale.z) < 0.0001f) return;
            transform.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1 / parentScale.z);

            // A sprite's pivot need not be at its centre. Centre the image rather than its transform origin.
            var centre = bounds.center;
            if (spriteRenderer.flipX) centre.x = -centre.x;
            if (spriteRenderer.flipY) centre.y = -centre.y;
            transform.position = targetCamera.transform.position + targetCamera.transform.forward * depth
                - transform.TransformVector(centre);
        }
    }
}
