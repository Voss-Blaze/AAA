using System.Collections.Generic;
using UnityEngine;

namespace ShadowTrace
{
    public sealed class TraceCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new Vector2(3, 1.5f);
        [Min(0.01f)] public float smoothTime = 0.15f;
        [Header("鼠标滚轮缩放")]
        [Tooltip("每格滚轮改变的 Size。向上滚动拉近，向下滚动拉远。")]
        [Min(0.01f)] public float zoomSpeed = 1;
        [Tooltip("最近视野的 Size 下限；数值越小，画面越近。")]
        [Min(0.01f)] public float minOrthographicSize = 4;
        [Tooltip("最远视野的 Size 上限；数值越大，可视范围越大。")]
        [Min(0.01f)] public float maxOrthographicSize = 12;
        [Header("机关大门镜头")]
        [Tooltip("镜头前往大门和返回玩家时的速度，单位：世界单位/秒。")]
        [Min(0.01f)] public float doorPreviewSpeed = 25;
        [Tooltip("镜头到达对应大门后的停留时间，单位：秒。")]
        [Min(0)] public float doorPreviewHoldTime = 0.6f;
        public bool IsPreviewing => phase != PreviewPhase.Follow || previews.Count > 0;
        public InteractiveDoor PreviewTarget => previewDoor;
        private enum PreviewPhase { Follow, Outward, Hold, Return }
        private PreviewPhase phase;
        private InteractiveDoor previewDoor;
        private float holdRemaining;
        private readonly Queue<InteractiveDoor> previews = new Queue<InteractiveDoor>();
        private readonly List<ShadowPressurePlate> plates = new List<ShadowPressurePlate>();
        private Vector3 velocity;
        private Camera viewCamera;

        private void Update() { ApplyScrollZoom(Input.mouseScrollDelta.y); }

        public void ApplyScrollZoom(float scroll)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || LevelStoryController.InputBlocked || Mathf.Approximately(scroll, 0)) return;
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (viewCamera == null || !viewCamera.orthographic) return;
            float minimum = Mathf.Max(0.01f, minOrthographicSize);
            float maximum = Mathf.Max(minimum, maxOrthographicSize);
            viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize - scroll * Mathf.Max(0.01f, zoomSpeed), minimum, maximum);
        }

        private void OnValidate()
        {
            minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        }

        private void OnEnable()
        {
            // Bind within this camera's scene, including initially inactive mechanisms.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var plate in root.GetComponentsInChildren<ShadowPressurePlate>(true))
                {
                    plates.Add(plate);
                    plate.Pressed += OnPlatePressed;
                }
        }

        private void OnPlatePressed(ShadowPressurePlate plate) { PreviewDoor(plate.targetDoor); }

        public void PreviewDoor(InteractiveDoor door)
        {
            if (!isActiveAndEnabled || door == null || door.gameObject.scene != gameObject.scene) return;
            previews.Enqueue(door);
        }

        private void LateUpdate()
        {
            if (target == null || Time.timeScale <= 0) return;
            Vector3 destination = new Vector3(target.position.x + offset.x, target.position.y + offset.y, -10);
            if (phase == PreviewPhase.Follow)
            {
                while (previews.Count > 0 && previewDoor == null) previewDoor = previews.Dequeue();
                if (previewDoor != null) { phase = PreviewPhase.Outward; velocity = Vector3.zero; }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
                    return;
                }
            }
            if (previewDoor == null || !previewDoor.isActiveAndEnabled) phase = PreviewPhase.Return;
            if (phase == PreviewPhase.Outward || phase == PreviewPhase.Hold)
            {
                var sprite = previewDoor.GetComponent<SpriteRenderer>();
                Vector3 centre = sprite != null && sprite.sprite != null ? sprite.bounds.center : previewDoor.transform.position;
                Vector3 doorPosition = new Vector3(centre.x, centre.y, destination.z);
                transform.position = Vector3.MoveTowards(transform.position, doorPosition, Mathf.Max(0.01f, doorPreviewSpeed) * Time.deltaTime);
                if (phase == PreviewPhase.Outward && (transform.position - doorPosition).sqrMagnitude < 0.0001f)
                {
                    phase = PreviewPhase.Hold;
                    holdRemaining = doorPreviewHoldTime;
                }
                else if (phase == PreviewPhase.Hold)
                {
                    holdRemaining -= Time.deltaTime;
                    if (holdRemaining <= 0) phase = PreviewPhase.Return;
                }
            }
            else if (phase == PreviewPhase.Return)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(0.01f, doorPreviewSpeed) * Time.deltaTime);
                if ((transform.position - destination).sqrMagnitude < 0.0001f)
                {
                    phase = PreviewPhase.Follow;
                    previewDoor = null;
                    velocity = Vector3.zero;
                }
            }
        }

        private void OnDisable()
        {
            foreach (var plate in plates) if (plate != null) plate.Pressed -= OnPlatePressed;
            plates.Clear(); previews.Clear(); previewDoor = null;
            phase = PreviewPhase.Follow; velocity = Vector3.zero;
        }
    }
}
