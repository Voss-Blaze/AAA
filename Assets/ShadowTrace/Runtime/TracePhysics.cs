using UnityEngine;

namespace ShadowTrace
{
    public static class TracePhysics
    {
        public static bool IsWorld(Collider2D collider)
        {
            return collider != null && !collider.isTrigger &&
                collider.GetComponentInParent<PlayerController2D>() == null &&
                collider.GetComponentInParent<ShadowActor>() == null;
        }

        public static ContactFilter2D Filter(LayerMask mask)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(mask);
            filter.useTriggers = false;
            return filter;
        }

        public static bool Grounded(Collider2D collider, LayerMask mask, RaycastHit2D[] results)
        {
            int count = collider.Cast(Vector2.down, Filter(mask), results, 0.08f);
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (hit.normal.y < 0.5f) continue;
                var shadow = hit.collider.GetComponentInParent<ShadowActor>();
                if (IsWorld(hit.collider) || (shadow != null && shadow.PlayerCollisionEnabled)) return true;
            }
            return false;
        }

        public static Collider2D Obstacle(Collider2D collider, int direction, float distance,
            LayerMask mask, RaycastHit2D[] results)
        {
            int count = collider.Cast(Vector2.right * direction, Filter(mask), results, distance);
            Collider2D nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (!IsWorld(hit.collider) || hit.normal.x * direction > -0.3f) continue;
                if (hit.distance < nearestDistance) { nearest = hit.collider; nearestDistance = hit.distance; }
            }
            return nearest;
        }

        public static bool SpawnPosition(Vector2 start, ShadowKind kind, Vector2 size,
            LayerMask mask, float groundSearchDistance, out Vector2 position)
        {
            position = start;
            if (kind == ShadowKind.Stay)
            {
                // Box cast includes the whole footprint, instead of selecting ground under its centre only.
                var hits = Physics2D.BoxCastAll(start, new Vector2(size.x * 0.95f, 0.04f),
                    0, Vector2.down, groundSearchDistance, mask);
                bool found = false;
                foreach (var hit in hits)
                {
                    if (!IsWorld(hit.collider) || hit.normal.y < 0.5f) continue;
                    position = new Vector2(start.x, hit.point.y + size.y * 0.5f + 0.015f);
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            foreach (var collider in Physics2D.OverlapBoxAll(position, size * 0.96f, 0, mask))
                if (IsWorld(collider)) return false;
            return true;
        }
    }
}
