using System;
using UnityEngine;

namespace ShadowTrace
{
    [DefaultExecutionOrder(100)]
    public sealed class TutorialArea : MonoBehaviour
    {
        [Serializable] public sealed class StageRange
        {
            public string label;
            public TutorialStep first, last;
            public Rect bounds;
        }
        [Tooltip("矩形坐标相对本物体；底边用于显示范围，掉落仍由教学重试处理。")]
        public StageRange[] ranges;
        public bool IsRestricting { get; private set; }
        public Rect ActiveBounds { get; private set; }
        private PlayerController2D player;

        public void Configure(TutorialStep step, PlayerController2D target)
        {
            player = target;
            IsRestricting = false;
            if (ranges == null) return;
            foreach (var range in ranges)
                if (step >= range.first && step <= range.last)
                {
                    ActiveBounds = new Rect(range.bounds.position + (Vector2)transform.position, range.bounds.size);
                    IsRestricting = true;
                    return;
                }
        }

        private void FixedUpdate() { Constrain(); }

        public void Constrain()
        {
            if (!IsRestricting || player == null || player.Body == null || !player.Body.simulated) return;
            var body = player.Body;
            var shape = player.Shape.bounds;
            Vector2 position = body.position, velocity = body.velocity;
            float left = ActiveBounds.xMin + position.x - shape.min.x;
            float right = ActiveBounds.xMax - shape.max.x + position.x;
            if (left > right) return;
            position.x = Mathf.Clamp(position.x, left, right);
            position.y = Mathf.Min(position.y, ActiveBounds.yMax - shape.max.y + body.position.y);
            float dt = Time.fixedDeltaTime;
            velocity.x = Mathf.Clamp(velocity.x, (left - position.x) / dt, (right - position.x) / dt);
            velocity.y = Mathf.Min(velocity.y, (ActiveBounds.yMax - shape.max.y + body.position.y - position.y) / dt);
            if (position != body.position) body.position = position;
            if (velocity != body.velocity) body.velocity = velocity;
        }

        private void OnDrawGizmosSelected()
        {
            if (ranges == null) return;
            for (int i = 0; i < ranges.Length; i++)
            {
                Gizmos.color = Color.Lerp(new Color(0.7f, 0.9f, 1), Color.white, i / (float)Mathf.Max(1, ranges.Length));
                var rect = ranges[i].bounds;
                Gizmos.DrawWireCube(transform.position + (Vector3)rect.center, new Vector3(rect.width, rect.height, 0));
            }
        }
    }
}
