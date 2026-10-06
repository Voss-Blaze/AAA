using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTrace.Editor
{
    public static class ShadowTraceDemoBuilder
    {
        public const string DemoPath = "Assets/ShadowTrace/Scenes/ShadowTraceDemo.unity";
        private const string Root = "Assets/ShadowTrace";

        [MenuItem("Tools/Shadow Trace/Create Demo Assets")]
        public static void CreateDemoAssets()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogWarning("Exit Play Mode before creating assets."); return; }
            if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            { Debug.LogWarning("Save the current untitled scene before rebuilding demo assets."); return; }
            Directory.CreateDirectory(Root + "/Art");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");
            string spritePath = Root + "/Art/Block.png";
            if (!File.Exists(spritePath))
            {
                var texture = new Texture2D(16, 16);
                var pixels = new Color[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(spritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            string materialPath = Root + "/Art/ActorSurface.physicsMaterial2D";
            var surface = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (surface == null)
            {
                surface = new PhysicsMaterial2D("ActorSurface") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(surface, materialPath);
            }
            // Existing prefabs are preserved so Inspector tuning survives a demo rebuild.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Player.prefab");
            if (player == null)
            {
                var temporary = Actor("Player", sprite, surface, new Color(0.95f, 0.95f, 1));
                temporary.AddComponent<PlayerController2D>();
                temporary.AddComponent<ShadowTraceAbility>();
                player = PrefabUtility.SaveAsPrefabAsset(temporary, Root + "/Prefabs/Player.prefab");
                Object.DestroyImmediate(temporary);
            }
            var shadow = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Shadow.prefab");
            if (shadow == null)
            {
                var temporary = Actor("Shadow", sprite, surface, Color.cyan);
                temporary.GetComponent<Rigidbody2D>().mass = 4;
                temporary.AddComponent<ShadowActor>();
                shadow = PrefabUtility.SaveAsPrefabAsset(temporary, Root + "/Prefabs/Shadow.prefab");
                Object.DestroyImmediate(temporary);
            }
            var box = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/PushableBox.prefab");
            if (box == null)
            {
                var temporary = Actor("PushableBox", sprite, surface, new Color(0.8f, 0.55f, 0.2f));
                temporary.GetComponent<BoxCollider2D>().size = Vector2.one;
                temporary.GetComponent<SpriteRenderer>().size = Vector2.one;
                temporary.AddComponent<PushableBody2D>();
                box = PrefabUtility.SaveAsPrefabAsset(temporary, Root + "/Prefabs/PushableBox.prefab");
                Object.DestroyImmediate(temporary);
            }
            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            var root = new GameObject("Shadow Trace Demo");
            SceneManager.MoveGameObjectToScene(root, scene);
            var demo = root.AddComponent<ShadowTraceDemo>();
            demo.playerPrefab = player.GetComponent<PlayerController2D>();
            demo.shadowPrefab = shadow.GetComponent<ShadowActor>();
            demo.boxPrefab = box; demo.blockSprite = sprite;
            EditorSceneManager.SaveScene(scene, DemoPath);
            if (!Application.isBatchMode)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Shadow Trace demo ready: " + DemoPath);
        }

        [MenuItem("Tools/Shadow Trace/Open Demo Scene")]
        public static void OpenDemo()
        {
            if (!File.Exists(DemoPath)) CreateDemoAssets();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(DemoPath);
        }

        private static GameObject Actor(string name, Sprite sprite, PhysicsMaterial2D surface, Color color)
        {
            var actor = new GameObject(name);
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.8f, 1.4f);
            var collider = actor.AddComponent<BoxCollider2D>();
            collider.size = renderer.size; collider.sharedMaterial = surface;
            var body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 3;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            return actor;
        }
    }
}
