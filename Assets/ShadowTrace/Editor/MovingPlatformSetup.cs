using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTrace.Editor
{
    /// <summary>
    /// 一键把当前场景里的「PressurePlate1 + moveplatform」接成一套机关：
    /// 给平台补 Kinematic 刚体（没有碰撞体时按精灵轮廓补一个），给压板挂上 ShadowMoverPlate，
    /// 并关掉压板原来那个只会开门的 ShadowPressurePlate，避免两个脚本抢精灵。
    /// </summary>
    public static class MovingPlatformSetup
    {
        private const string PlatformName = "moveplatform";
        private const string PlateName = "PressurePlate1";

        [MenuItem("Tools/Shadow Trace/Setup Moving Platform (SampleScene)")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[MovingPlatform] 请先退出 Play 模式再执行。");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var platform = Find(scene, PlatformName);
            var plate = Find(scene, PlateName);
            if (platform == null || plate == null)
            {
                Debug.LogError("[MovingPlatform] 场景 " + scene.name + " 里找不到物体 \"" + PlatformName +
                               "\" 和 \"" + PlateName + "\"，请确认名字没改。");
                return;
            }

            var body = platform.GetComponent<Rigidbody2D>();
            if (body == null) body = Undo.AddComponent<Rigidbody2D>(platform);
            // Kinematic：不吃重力、不会被角色推动，但能挡住角色、把角色顶起来。
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;
            EditorUtility.SetDirty(body);

            var shape = platform.GetComponent<BoxCollider2D>();
            bool shapeAdded = shape == null;
            if (shapeAdded)
            {
                shape = Undo.AddComponent<BoxCollider2D>(platform);
                var bounds = SpriteOutlineBounds(platform);
                shape.size = bounds.size;
                shape.offset = bounds.center;
            }
            shape.isTrigger = false;
            EditorUtility.SetDirty(shape);

            var moving = platform.GetComponent<MovingPlatform>();
            if (moving == null) moving = Undo.AddComponent<MovingPlatform>(platform);
            moving.enabled = true;
            EditorUtility.SetDirty(moving);

            var legacy = plate.GetComponent<ShadowPressurePlate>();
            if (legacy != null && legacy.enabled)
            {
                legacy.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(legacy);
            }

            // The plate only detects actors; the level ground supplies physical support.
            var plateShape = plate.GetComponent<BoxCollider2D>();
            if (plateShape != null)
            {
                Undo.RecordObject(plateShape, "Use trigger plate");
                plateShape.isTrigger = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(plateShape);
                EditorUtility.SetDirty(plateShape);
            }

            var mover = plate.GetComponent<ShadowMoverPlate>();
            if (mover == null) mover = Undo.AddComponent<ShadowMoverPlate>(plate);
            mover.enabled = true;
            if (mover.pressArea == null) mover.pressArea = FirstTrigger(plate);
            if (mover.pressArea == null) mover.pressArea = plateShape;
            if (mover.pressArea != null)
            {
                Undo.RecordObject(mover.pressArea, "Use trigger area");
                mover.pressArea.isTrigger = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mover.pressArea);
                EditorUtility.SetDirty(mover.pressArea);
            }
            if (mover.targetPlatform == null) mover.targetPlatform = moving;
            var frame = plate.GetComponent<SpriteRenderer>();
            if (mover.idleSprite == null)
                mover.idleSprite = legacy != null && legacy.closedSprite != null
                    ? legacy.closedSprite
                    : frame != null ? frame.sprite : null;
            if (mover.pressedSprite == null && legacy != null) mover.pressedSprite = legacy.activeSprite;
            // 设计限制：机关只认影子，玩家踩上去不生效。
            mover.playerCanPress = false;
            EditorUtility.SetDirty(mover);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[MovingPlatform] 已接好：" + PlateName + " 驱动 " + PlatformName +
                      "（平台：Kinematic 刚体" + (shapeAdded ? " + 按精灵轮廓新加的碰撞体" : "，沿用已有碰撞体") +
                      "；压板：ShadowMoverPlate，当前模式 " + mover.mode + "，平台位移 " + moving.offset +
                      "）。按 Ctrl+S 保存场景，然后直接 Play 就能看到效果。");
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var found = Find(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform Find(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var deeper = Find(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static BoxCollider2D FirstTrigger(GameObject root)
        {
            var own = root.GetComponent<Collider2D>();
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider == own) continue;
                var box = collider as BoxCollider2D;
                if (box != null && box.isTrigger) return box;
            }
            return null;
        }

        /// <summary>精灵可见轮廓的局部包围盒，和 SceneInteractionSetup 用的是同一套办法。</summary>
        private static Bounds SpriteOutlineBounds(GameObject target)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            var sprite = renderer != null ? renderer.sprite : null;
            if (sprite == null) return new Bounds(Vector3.zero, Vector3.one);
            var bounds = sprite.bounds;
            bool first = true;
            var points = new List<Vector2>();
            for (int path = 0; path < sprite.GetPhysicsShapeCount(); path++)
            {
                points.Clear();
                sprite.GetPhysicsShape(path, points);
                foreach (var point in points)
                {
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
