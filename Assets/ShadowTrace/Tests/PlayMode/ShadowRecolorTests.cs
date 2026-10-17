#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class ShadowRecolorTests
    {
        private const string MaterialPath = "Assets/Shader/ShadowRecolorMaterial.mat";
        private readonly List<Object> objects = new List<Object>();
        private Camera camera;
        private SpriteRenderer renderer;
        private RenderTexture target;
        private Material material;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1;
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "Run these tests with a graphics device");
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            var cameraObject = new GameObject("Recolor test camera"); objects.Add(cameraObject);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 0.5f; camera.aspect = 1;
            camera.transform.position = new Vector3(400, 400, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); camera.targetTexture = target;
            var spriteObject = new GameObject("Recolor test sprite"); objects.Add(spriteObject);
            spriteObject.layer = 31; spriteObject.transform.position = new Vector3(400, 400, 0);
            renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = MakeSprite(false); renderer.sharedMaterial = material;
        }

        private Sprite MakeSprite(bool secondFrame)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            objects.Add(texture);
            var pixels = new Color[16 * 16];
            for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++) pixels[y * 16 + x] = Color.black;
            int begin = secondFrame ? 2 : 10;
            for (int y = begin; y < begin + 4; y++) for (int x = begin; x < begin + 4; x++) pixels[y * 16 + x] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16, 0, SpriteMeshType.FullRect);
            objects.Add(sprite); return sprite;
        }

        private Color Pixel(int x, int y)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
            var color = image.GetPixel(x, y);
            Object.DestroyImmediate(image); RenderTexture.active = previous;
            return color;
        }

        private static void White(Color color)
        {
            Assert.Greater(color.r, 0.9f); Assert.Greater(color.g, 0.9f); Assert.Greater(color.b, 0.9f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            camera.targetTexture = null; target.Release(); Object.Destroy(target);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear(); yield return null;
        }

        [UnityTest] public IEnumerator BlackBodyChangesColorWhileWhiteEyesAndTransparentOutlineRemain()
        {
            renderer.color = new Color(0.8f, 0.1f, 0.05f, 1);
            yield return null; yield return null; yield return null;
            var body = Pixel(40, 40);
            Assert.Greater(body.r, body.g * 3); Assert.Greater(body.r, 0.4f);
            White(Pixel(96, 96)); Assert.Less(Pixel(4, 4).a, 0.02f);
            renderer.color = new Color(0.05f, 0.8f, 0.1f, 1);
            yield return null; yield return null;
            body = Pixel(40, 40); Assert.Greater(body.g, body.r * 3);
            White(Pixel(96, 96));
            System.IO.Directory.CreateDirectory(".utmp");
            var previous = RenderTexture.active; RenderTexture.active = target;
            var capture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); capture.Apply();
            System.IO.File.WriteAllBytes(".utmp/shadow-recolor-gpu-check.png", capture.EncodeToPNG());
            Object.Destroy(capture); RenderTexture.active = previous;
        }

        [UnityTest] public IEnumerator FlipAnimationFrameSwapAndOpacityKeepRecoloring()
        {
            renderer.color = new Color(1, 0.05f, 0.05f, 1); renderer.flipX = true;
            yield return null; yield return null; yield return null;
            White(Pixel(32, 96)); Assert.Greater(Pixel(96, 96).r, Pixel(96, 96).g * 3);
            renderer.flipX = false; renderer.sprite = MakeSprite(true);
            yield return null; yield return null;
            White(Pixel(32, 32)); Assert.Greater(Pixel(96, 96).r, Pixel(96, 96).g * 3);
            renderer.color = new Color(1, 0.05f, 0.05f, 0.35f);
            yield return null; yield return null;
            Assert.That(Pixel(32, 32).a, Is.EqualTo(0.35f).Within(0.06f));
            Assert.That(Pixel(96, 96).a, Is.EqualTo(0.35f).Within(0.06f));
        }

        [TestCase(ShadowKind.Stay)] [TestCase(ShadowKind.Approach)]
        [TestCase(ShadowKind.Avoid)] [TestCase(ShadowKind.Follow)]
        public void ActualPrefabUsesEditablePerKindColorsAndSharesMaterial(ShadowKind kind)
        {
            var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab")); objects.Add(playerObject);
            var player = playerObject.GetComponent<PlayerController2D>();
            player.enabled = false; player.Body.simulated = false;
            var managerObject = new GameObject("Recolor manager"); objects.Add(managerObject);
            var manager = managerObject.AddComponent<ShadowManager>(); manager.player = player;
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/SampleShadow.prefab")); objects.Add(root);
            var actor = root.GetComponent<ShadowActor>();
            actor.stayBodyColor = Color.red; actor.approachBodyColor = Color.green;
            actor.avoidBodyColor = Color.blue; actor.followBodyColor = Color.magenta;
            actor.Initialize(kind, 1, 1, player, manager);
            var bodyRenderer = root.GetComponentInChildren<SpriteRenderer>();
            var expected = kind == ShadowKind.Stay ? Color.red : kind == ShadowKind.Approach ? Color.green : kind == ShadowKind.Avoid ? Color.blue : Color.magenta;
            Assert.AreEqual(expected, bodyRenderer.color); Assert.AreSame(material, bodyRenderer.sharedMaterial);
            actor.SetSelected(true); Assert.AreEqual(expected, bodyRenderer.color);
            Assert.IsTrue(actor.GetComponent<ShadowSelectionGlow>().selected);
            actor.SetSelected(false); Assert.AreEqual(expected, bodyRenderer.color);
            var backup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/Shadow.prefab");
            Assert.AreSame(material, backup.GetComponentInChildren<SpriteRenderer>(true).sharedMaterial);
            Assert.AreNotSame(material, player.GetComponentInChildren<SpriteRenderer>().sharedMaterial);
        }
    }
}
#endif
