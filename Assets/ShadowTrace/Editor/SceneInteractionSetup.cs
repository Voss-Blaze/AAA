using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowTrace.Editor
{
    public static class SceneInteractionSetup
    {
        private const string ResourceRoot = "Assets/Art/environment/en_elements/";
        private static Sprite doorClosed, doorOpen, plateClosed, plateActive, lampOff, lampOn;

        [MenuItem("Tools/Shadow Trace/Configure Scene Interactions")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring interactions.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                doorClosed = SourceSprite("door1"); doorOpen = SourceSprite("door2");
                plateClosed = SourceSprite("PressurePlate1"); plateActive = SourceSprite("PressurePlate2");
                lampOff = SourceSprite("light1"); lampOn = SourceSprite("light2");
                foreach (string name in new[] { "spike1", "door1", "door2", "PressurePlate1", "PressurePlate2", "light1", "light2" })
                {
                    string path = ResourceRoot + name + ".prefab";
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { ConfigureObject(root, name); PrefabUtility.SaveAsPrefabAsset(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                string playerPath = "Assets/ShadowTrace/Prefabs/Player.prefab";
                var playerRoot = PrefabUtility.LoadPrefabContents(playerPath);
                try { GetOrAdd<PlayerLife>(playerRoot); PrefabUtility.SaveAsPrefabAsset(playerRoot, playerPath); }
                finally { PrefabUtility.UnloadPrefabContents(playerRoot); }

                var scene = EditorSceneManager.OpenScene(ActorAnimationSetup.SamplePath);
                var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(item => item.gameObject).ToArray();
                foreach (var item in objects)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(item);
                    ConfigureObject(item, source != null ? source.name : item.name);
                }
                var player = objects.Select(item => item.GetComponent<PlayerController2D>()).Single(item => item != null);
                var life = GetOrAdd<PlayerLife>(player.gameObject);
                var doors = objects.Select(item => item.GetComponent<InteractiveDoor>()).Where(item => item != null).ToArray();
                foreach (var door in doors)
                {
                    door.player = player;
                    door.GetComponent<SpriteRenderer>().sprite = doorClosed;
                    EditorUtility.SetDirty(door);
                }
                foreach (var plate in objects.Select(item => item.GetComponent<ShadowPressurePlate>()).Where(item => item != null))
                {
                    if (plate.targetDoor == null && doors.Length > 0) plate.targetDoor = doors[0];
                    plate.GetComponent<SpriteRenderer>().sprite = plateClosed;
                    EditorUtility.SetDirty(plate);
                }
                foreach (var lamp in objects.Select(item => item.GetComponent<ShadowLamp>()).Where(item => item != null))
                    lamp.GetComponent<SpriteRenderer>().sprite = lampOff;
                var hud = objects.Select(item => item.GetComponent<ShadowTraceHUD>()).Single(item => item != null);
                hud.life = life; hud.doors = doors;
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save SampleScene.");
                AssetDatabase.SaveAssets();
                Debug.Log("Scene interactions configured and saved: spikes, lamps, plates, doors and player death.");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static Sprite SourceSprite(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceRoot + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing state prefab: " + name);
            return prefab.GetComponent<SpriteRenderer>().sprite;
        }

        private static void ConfigureObject(GameObject root, string name)
        {
            if (name == "spike1")
            {
                ConfigureBox(root, root.GetComponent<SpriteRenderer>().sprite, true);
                GetOrAdd<SpikeHazard>(root);
            }
            else if (name == "light1" || name == "light2")
            {
                ConfigureBox(root, lampOff, true);
                var lamp = GetOrAdd<ShadowLamp>(root);
                lamp.unlitSprite = lampOff; lamp.litSprite = lampOn;
            }
            else if (name == "PressurePlate1" || name == "PressurePlate2")
            {
                var support = ConfigureBox(root, plateClosed, false);
                var imageBounds = CollisionBounds(plateClosed);
                if (Vector2.Distance(support.size, imageBounds.size) < 0.0001f &&
                    Vector2.Distance(support.offset, imageBounds.center) < 0.0001f)
                {
                    // Decorative posts are taller than the button. Support only its central top surface.
                    support.size = new Vector2(imageBounds.size.x * 0.65f, 0.2f);
                    support.offset = new Vector2(plateClosed.bounds.center.x, CentreSurfaceTop(plateClosed) - 0.1f);
                }
                var plate = GetOrAdd<ShadowPressurePlate>(root);
                plate.closedSprite = plateClosed; plate.activeSprite = plateActive;
                plate.pressArea = TriggerChild(root, "Press Area", new Vector2(support.size.x, 0.24f),
                    support.offset + Vector2.up * (support.size.y * 0.5f + 0.1f));
            }
            else if (name == "door1" || name == "door2")
            {
                var blocker = ConfigureBox(root, doorClosed, false);
                var door = GetOrAdd<InteractiveDoor>(root);
                door.closedSprite = doorClosed; door.openSprite = doorOpen;
                door.interactionArea = TriggerChild(root, "Interaction Area", blocker.size + new Vector2(1, 0.8f), blocker.offset);
            }
        }

        private static BoxCollider2D ConfigureBox(GameObject root, Sprite source, bool trigger)
        {
            var collider = root.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider2D>();
                var bounds = CollisionBounds(source);
                collider.size = bounds.size; collider.offset = bounds.center;
            }
            collider.isTrigger = trigger;
            return collider;
        }

        private static BoxCollider2D TriggerChild(GameObject root, string name, Vector2 size, Vector2 offset)
        {
            var child = root.transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
            var trigger = GetOrAdd<BoxCollider2D>(child.gameObject);
            trigger.size = size; trigger.offset = offset; trigger.isTrigger = true;
            return trigger;
        }

        private static Bounds CollisionBounds(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Interaction sprite is missing.");
            var points = new List<Vector2>();
            var bounds = sprite.bounds;
            bool first = true;
            for (int path = 0; path < sprite.GetPhysicsShapeCount(); path++)
            {
                sprite.GetPhysicsShape(path, points);
                foreach (var point in points)
                {
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static float CentreSurfaceTop(Sprite sprite)
        {
            float centre = sprite.bounds.center.x;
            float top = float.NegativeInfinity;
            var points = new List<Vector2>();
            for (int path = 0; path < sprite.GetPhysicsShapeCount(); path++)
            {
                sprite.GetPhysicsShape(path, points);
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[i]; var b = points[(i + 1) % points.Count];
                    if (centre < Mathf.Min(a.x, b.x) || centre > Mathf.Max(a.x, b.x)) continue;
                    if (Mathf.Abs(b.x - a.x) < 0.0001f) top = Mathf.Max(top, a.y, b.y);
                    else top = Mathf.Max(top, Mathf.Lerp(a.y, b.y, (centre - a.x) / (b.x - a.x)));
                }
            }
            return float.IsNegativeInfinity(top) ? sprite.bounds.max.y : top;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
