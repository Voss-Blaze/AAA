using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTrace.Editor
{
    /// <summary>Persists configuration; no scene generation is needed at runtime.</summary>
    public static class ActorAnimationSetup
    {
        public const string SamplePath = "Assets/Scenes/SampleScene.unity";
        public const string ControllerPath = "Assets/Animation/Player.controller";
        public const string SampleShadowPath = "Assets/ShadowTrace/Prefabs/SampleShadow.prefab";
        private const string PrefabRoot = "Assets/ShadowTrace/Prefabs/";
        private static readonly string[] States = { "Idle", "Walk", "Run", "Jump", "Shadowtrace" };

        [MenuItem("Tools/Shadow Trace/Configure Shared Actor Animation")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring assets.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ConfigureController();
                ConfigurePrefab(PrefabRoot + "Player.prefab");
                ConfigurePrefab(PrefabRoot + "Shadow.prefab");
                ConfigureSample();
                AssetDatabase.SaveAssets();
                Debug.Log("Shared actor animation configured: controller, prefabs and SampleScene saved.");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static void ConfigureController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Player.controller is missing.");
            var machine = controller.layers[0].stateMachine;
            controller.parameters = new[] { new AnimatorControllerParameter
                { name = "MotionState", type = AnimatorControllerParameterType.Int, defaultInt = 0 } };
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            for (int i = 0; i < States.Length; i++)
            {
                string name = States[i];
                var state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == name);
                if (state == null) state = machine.AddState(name);
                foreach (var transition in state.transitions) state.RemoveTransition(transition);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/" + name + ".anim");
                if (clip == null) throw new InvalidOperationException("Missing animation: " + name);
                RepairClip(clip, name);
                state.motion = clip; state.writeDefaultValues = false;
                var entry = machine.AddAnyStateTransition(state);
                entry.AddCondition(AnimatorConditionMode.Equals, i, "MotionState");
                entry.hasExitTime = false; entry.duration = 0; entry.canTransitionToSelf = false;
                if (i == 0) machine.defaultState = state;
            }
            // Place the nodes so the saved graph is readable in the Animator window.
            machine.states = machine.states.Select((item, index) => new ChildAnimatorState
                { state = item.state, position = new Vector3(320 + (index % 3) * 240, 80 + (index / 3) * 160) }).ToArray();
            EditorUtility.SetDirty(controller);
        }

        private static void RepairClip(AnimationClip clip, string name)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk" || name == "Run";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (name != "Jump") return;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                var valid = frames.Where(frame => frame.value is Sprite sprite &&
                    sprite.bounds.size.x >= 0.1f && sprite.bounds.size.y >= 0.5f).ToArray();
                if (valid.Length == 0) throw new InvalidOperationException("Jump has no valid character frames.");
                if (valid.Length == frames.Length) continue;
                for (int i = 0; i < valid.Length; i++) valid[i].time = i / clip.frameRate;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, valid);
            }
        }

        private static void ConfigurePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureVisual(root, root.GetComponent<BoxCollider2D>().size);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureVisual(GameObject root, Vector2 size)
        {
            var visual = root.transform.Find("Visual");
            var oldSprite = root.GetComponent<SpriteRenderer>();
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var created = visual.gameObject.AddComponent<SpriteRenderer>();
                if (oldSprite != null)
                {
                    created.sharedMaterial = oldSprite.sharedMaterial;
                    created.color = oldSprite.color;
                    created.sortingLayerID = oldSprite.sortingLayerID;
                    created.sortingOrder = oldSprite.sortingOrder;
                    created.flipX = oldSprite.flipX;
                }
            }
            if (oldSprite != null) UnityEngine.Object.DestroyImmediate(oldSprite);
            var oldAnimator = root.GetComponent<Animator>();
            if (oldAnimator != null) UnityEngine.Object.DestroyImmediate(oldAnimator);
            var renderer = GetOrAdd<SpriteRenderer>(visual.gameObject);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Idle.anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(idle).First(item => item.type == typeof(SpriteRenderer));
            renderer.sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, binding)[0].value;
            renderer.drawMode = SpriteDrawMode.Simple;
            // Imported frames have bottom-centre pivots. Only the visual changes frame size.
            visual.localPosition = new Vector3(0, -size.y * 0.5f, 0);
            float scale = size.y / renderer.sprite.bounds.size.y;
            visual.localScale = Vector3.one * scale;
            var animator = GetOrAdd<Animator>(visual.gameObject);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            GetOrAdd<ActorAnimationDriver>(root);
        }

        private static void ConfigureSample()
        {
            var scene = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(item => item.gameObject).ToArray();
            var playerObject = objects.Single(item => item.CompareTag("Player"));
            var shape = playerObject.GetComponent<BoxCollider2D>();
            if (shape == null)
            {
                var sprite = playerObject.GetComponent<SpriteRenderer>();
                if (sprite == null || sprite.sprite == null) throw new InvalidOperationException("Player sprite is missing.");
                // Preserve the original world-space appearance while moving the root to the body centre.
                playerObject.transform.position += playerObject.transform.TransformVector(sprite.sprite.bounds.center);
                shape = playerObject.AddComponent<BoxCollider2D>();
                shape.size = sprite.sprite.bounds.size;
                shape.offset = Vector2.zero;
            }
            var body = GetOrAdd<Rigidbody2D>(playerObject);
            body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 3;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            shape.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/ShadowTrace/Art/ActorSurface.physicsMaterial2D");
            var player = GetOrAdd<PlayerController2D>(playerObject);
            var ability = GetOrAdd<ShadowTraceAbility>(playerObject);
            ConfigureVisual(playerObject, shape.size);

            foreach (var ground in objects.Where(item => item.CompareTag("Ground")))
            {
                var renderer = ground.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null) throw new InvalidOperationException("Ground sprite is missing.");
                var existing = ground.GetComponent<Collider2D>();
                var collider = existing as BoxCollider2D;
                if (existing != null && (collider == null || collider.size != (Vector2)renderer.sprite.bounds.size ||
                    collider.offset != (Vector2)renderer.sprite.bounds.center)) continue;
                if (collider == null) collider = ground.AddComponent<BoxCollider2D>();
                var bounds = CollisionBounds(renderer.sprite);
                collider.size = bounds.size; collider.offset = bounds.center;
                collider.isTrigger = false;
            }

            Physics2D.SyncTransforms();
            Vector2 worldSize = Vector2.Scale(shape.size, playerObject.transform.lossyScale);
            if (TracePhysics.SpawnPosition(playerObject.transform.position, ShadowKind.Stay, worldSize,
                player.worldMask, 20, out var supportedStart))
            {
                playerObject.transform.position = new Vector3(supportedStart.x, supportedStart.y, playerObject.transform.position.z);
            }
            else
            {
                var nearest = objects.Where(item => item.CompareTag("Ground"))
                    .Select(item => item.GetComponent<Collider2D>())
                    .OrderBy(item => (item.bounds.center - playerObject.transform.position).sqrMagnitude).First();
                playerObject.transform.position = new Vector3(nearest.bounds.center.x,
                    nearest.bounds.max.y + worldSize.y * 0.5f + 0.015f, playerObject.transform.position.z);
            }

            ConfigureSampleShadow(shape.size, playerObject.transform.localScale);
            var manager = objects.Select(item => item.GetComponent<ShadowManager>()).FirstOrDefault(item => item != null);
            if (manager == null) manager = new GameObject("Shadow Manager").AddComponent<ShadowManager>();
            manager.player = player;
            manager.shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SampleShadowPath).GetComponent<ShadowActor>();
            ability.manager = manager;
            var hud = objects.Select(item => item.GetComponent<ShadowTraceHUD>()).FirstOrDefault(item => item != null);
            if (hud == null) hud = manager.gameObject.AddComponent<ShadowTraceHUD>();
            hud.ability = ability; hud.manager = manager;
            var camera = objects.Select(item => item.GetComponent<Camera>()).First(item => item != null && item.CompareTag("MainCamera"));
            camera.orthographic = true;
            GetOrAdd<TraceCameraFollow>(camera.gameObject).target = playerObject.transform;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save SampleScene.");
        }

        private static void ConfigureSampleShadow(Vector2 size, Vector3 scale)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SampleShadowPath) == null)
                AssetDatabase.CopyAsset(PrefabRoot + "Shadow.prefab", SampleShadowPath);
            var root = PrefabUtility.LoadPrefabContents(SampleShadowPath);
            try
            {
                root.name = "SampleShadow"; root.transform.localScale = scale;
                var shape = root.GetComponent<BoxCollider2D>();
                shape.size = size; shape.offset = Vector2.zero;
                ConfigureVisual(root, size);
                PrefabUtility.SaveAsPrefabAsset(root, SampleShadowPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Bounds CollisionBounds(Sprite sprite)
        {
            var vertices = new List<Vector2>();
            var bounds = sprite.bounds;
            bool first = true;
            for (int path = 0; path < sprite.GetPhysicsShapeCount(); path++)
            {
                sprite.GetPhysicsShape(path, vertices);
                foreach (var vertex in vertices)
                {
                    if (first) { bounds = new Bounds(vertex, Vector3.zero); first = false; }
                    else bounds.Encapsulate(vertex);
                }
            }
            return bounds;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
