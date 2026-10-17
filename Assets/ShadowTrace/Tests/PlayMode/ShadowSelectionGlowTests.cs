#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class ShadowSelectionGlowTests
    {
        private readonly List<Object> objects = new List<Object>();
        private Camera camera;
        private SpriteRenderer source;
        private ShadowSelectionGlow selection;
        private RenderTexture target;
        private Material material;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1;
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/ShadowRecolorMaterial.mat");
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            var cameraObject = new GameObject("Selection camera"); objects.Add(cameraObject);
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(400, 400, -10);
            camera.orthographic = true; camera.orthographicSize = 0.5f; camera.aspect = 1;
            camera.allowHDR = true; camera.allowMSAA = false;
            camera.cullingMask = 1 << 31; camera.backgroundColor = Color.clear; camera.clearFlags = CameraClearFlags.SolidColor;
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            target.Create(); camera.targetTexture = target;
            var root = new GameObject("Selection source"); objects.Add(root);
            root.layer = 31; root.transform.position = new Vector3(400, 400, 0);
            source = root.AddComponent<SpriteRenderer>(); source.sprite = MakeSprite();
            source.color = new Color(0.15f, 0.5f, 0.9f, 1); source.sharedMaterial = material;
            selection = root.AddComponent<ShadowSelectionGlow>();
            selection.outlineWidth = 2; selection.glowRadius = 4; selection.glowIntensity = 4; selection.glowOpacity = 0.8f;
        }

        private Sprite MakeSprite()
        {
            var image = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            objects.Add(image); var pixels = new Color[1024];
            for (int y = 6; y < 26; y++) for (int x = 10; x < 22; x++) pixels[y * 32 + x] = Color.black;
            for (int y = 20; y < 23; y++) for (int x = 17; x < 20; x++) pixels[y * 32 + x] = Color.white;
            image.SetPixels(pixels); image.Apply();
            var sprite = Sprite.Create(image, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32, 0, SpriteMeshType.FullRect);
            // Deliberately trim the original mesh to the opaque silhouette. The outline must extend past it.
            sprite.OverrideGeometry(new[] { new Vector2(10, 6), new Vector2(22, 6),
                new Vector2(22, 26), new Vector2(10, 26) }, new ushort[] { 0, 1, 2, 0, 2, 3 });
            objects.Add(sprite); return sprite;
        }

        private Color Pixel(int x, int y)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
            image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
            var color = image.GetPixel(x, y); Object.DestroyImmediate(image); RenderTexture.active = previous;
            return color;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            camera.targetTexture = null; target.Release(); Object.Destroy(target);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear(); yield return null;
        }

        [UnityTest] public IEnumerator OutlineExtendsPastTightMeshWithoutChangingBodyOrWhiteEyes()
        {
            yield return null; yield return null; yield return null;
            Color originalBody = Pixel(64, 64);
            Assert.Less(Pixel(36, 64).a, 0.01f);
            selection.SetSelected(true);
            yield return null; yield return null;
            Assert.Greater(Pixel(36, 64).a, 0.5f, "Outline must extend past the original sprite mesh");
            Assert.Greater(Pixel(24, 64).r, 0.01f, "A soft halo must extend past the solid outline");
            var body = Pixel(64, 64); Assert.That(body.r, Is.EqualTo(originalBody.r).Within(0.02f));
            Assert.That(body.b, Is.EqualTo(originalBody.b).Within(0.02f));
            var eye = Pixel(72, 84); Assert.Greater(eye.r, 0.9f); Assert.Greater(eye.g, 0.9f); Assert.Greater(eye.b, 0.9f);
            selection.SetSelected(false); yield return null; yield return null;
            Assert.Less(Pixel(36, 64).a, 0.01f); Assert.IsFalse(selection.OutlineRenderer.enabled);
        }

        [UnityTest] public IEnumerator SharedMaterialSelectionIsIsolatedAndSettingsCanChangeAtRuntime()
        {
            var root = new GameObject("Other shadow"); objects.Add(root); root.layer = 31;
            root.transform.position = new Vector3(402, 400, 0);
            var otherSource = root.AddComponent<SpriteRenderer>(); otherSource.sprite = source.sprite; otherSource.sharedMaterial = material;
            var other = root.AddComponent<ShadowSelectionGlow>();
            selection.SetSelected(true); yield return null; yield return null;
            Assert.IsTrue(selection.OutlineRenderer.enabled); Assert.IsNull(other.OutlineRenderer);
            Assert.AreEqual(0, material.GetFloat("_OutlineOnly"));
            Assert.AreSame(source.sharedMaterial, selection.OutlineRenderer.sharedMaterial);
            var properties = new MaterialPropertyBlock(); selection.OutlineRenderer.GetPropertyBlock(properties);
            Assert.AreEqual(1, properties.GetFloat("_OutlineOnly"));
            selection.outlineColor = Color.green; selection.glowEnabled = false; selection.outlineWidth = 3;
            yield return null; yield return null;
            Assert.Greater(Pixel(32, 64).g, Pixel(32, 64).r + 0.1f);
            var before = selection.OutlineRenderer.bounds.size.x;
            selection.outlineWidth = 8; yield return null; yield return null;
            Assert.Greater(selection.OutlineRenderer.bounds.size.x, before);
            selection.SetSelected(false); other.SetSelected(true); yield return null;
            Assert.IsFalse(selection.OutlineRenderer.enabled); Assert.IsTrue(other.OutlineRenderer.enabled);
        }

        [UnityTest] public IEnumerator BloomSpreadsHdrOutlineAndSwitchesOffWithSelection()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); objects.Add(profile);
            var bloom = profile.Add<Bloom>(true); objects.Add(bloom);
            bloom.threshold.Override(1.1f); bloom.intensity.Override(1); bloom.scatter.Override(0.7f);
            var volumeObject = new GameObject("Selection test Bloom"); objects.Add(volumeObject); volumeObject.layer = 31;
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = profile;
            var data = camera.GetUniversalAdditionalCameraData(); data.volumeLayerMask = 1 << 31; data.renderPostProcessing = false;
            selection.glowIntensity = 6; selection.SetSelected(true);
            yield return null; yield return null; yield return null;
            float without = Pixel(8, 64).r;
            data.renderPostProcessing = true;
            yield return null; yield return null; yield return null;
            float withBloom = Pixel(8, 64).r;
            Assert.Greater(withBloom, without + 0.001f, "Bloom should spread light outside the sampled halo");
            selection.SetSelected(false); yield return null; yield return null;
            Assert.Less(Pixel(8, 64).r, withBloom);
        }

        [UnityTest] public IEnumerator AnimatedFrameAndFlipUpdateTheOutlineMeshAndTexture()
        {
            selection.SetSelected(true); yield return null; yield return null;
            var original = selection.OutlineRenderer.GetComponent<MeshFilter>().sharedMesh.vertices[0];
            source.flipX = true; source.flipY = true; yield return null; yield return null;
            var flipped = selection.OutlineRenderer.GetComponent<MeshFilter>().sharedMesh.vertices[0];
            Assert.That(flipped.x, Is.EqualTo(-original.x).Within(0.001f));
            Assert.That(flipped.y, Is.EqualTo(-original.y).Within(0.001f));
            source.sprite = MakeSprite(); yield return null; yield return null;
            var properties = new MaterialPropertyBlock(); selection.OutlineRenderer.GetPropertyBlock(properties);
            Assert.AreSame(source.sprite.texture, properties.GetTexture("_MainTex"));
            Assert.AreEqual(source.sortingLayerID, selection.OutlineRenderer.sortingLayerID);
            Assert.Greater(Pixel(36, 64).a, 0.1f);
        }
    }
}
#endif
