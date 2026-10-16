using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ShadowTrace.Tests
{
    public sealed class CameraBackgroundTests
    {
        private readonly List<Object> objects = new List<Object>();
        private float previousTimeScale;
#if UNITY_EDITOR
        private Scene level;
        private Scene previous;
        private GameAudioManager previousAudio;
#endif

        [SetUp] public void SetUp()
        {
            previousTimeScale = Time.timeScale;
#if UNITY_EDITOR
            previous = SceneManager.GetActiveScene(); previousAudio = GameAudioManager.Instance;
#endif
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
#if UNITY_EDITOR
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (level.IsValid() && level.isLoaded) yield return SceneManager.UnloadSceneAsync(level);
            if (previousAudio == null && GameAudioManager.Instance != null) Object.Destroy(GameAudioManager.Instance.gameObject);
#endif
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear(); Time.timeScale = previousTimeScale;
            yield return null;
        }

        private static void AssertCoverage(Camera camera, SpriteRenderer renderer)
        {
            float depth = Vector3.Dot(renderer.bounds.center - camera.transform.position, camera.transform.forward);
            foreach (var corner in new[] { Vector2.zero, Vector2.one, Vector2.up, Vector2.right })
            {
                var point = camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, depth));
                Assert.That(point.x, Is.InRange(renderer.bounds.min.x - 0.001f, renderer.bounds.max.x + 0.001f));
                Assert.That(point.y, Is.InRange(renderer.bounds.min.y - 0.001f, renderer.bounds.max.y + 0.001f));
            }
            var centre = camera.WorldToViewportPoint(renderer.bounds.center);
            Assert.That(centre.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(centre.y, Is.EqualTo(0.5f).Within(0.001f));
            var scale = renderer.transform.lossyScale;
            Assert.That(Mathf.Abs(scale.x), Is.EqualTo(Mathf.Abs(scale.y)).Within(0.001f));
        }

        [TestCase(1.7777778f, 8f)] [TestCase(0.5625f, 12f)] [TestCase(3.5f, 5f)]
        public void ImageCoversViewportWithoutStretchingEvenWithOffsetPivot(float aspect, float size)
        {
            var cameraObject = new GameObject("Background test camera"); objects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.aspect = aspect; camera.orthographicSize = size; camera.transform.position = new Vector3(33, -12, -10);
            var image = new Texture2D(80, 40); objects.Add(image);
            var sprite = Sprite.Create(image, new Rect(0, 0, 80, 40), new Vector2(0.15f, 0.85f), 10, 0, SpriteMeshType.FullRect);
            objects.Add(sprite);
            var background = new GameObject("Background test image"); objects.Add(background);
            var renderer = background.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            var follow = background.AddComponent<CameraBackgroundFollow>(); follow.targetCamera = camera;
            follow.FollowNow(); AssertCoverage(camera, renderer);
            camera.transform.position += new Vector3(-90, 70, 0); camera.orthographicSize *= 1.7f;
            renderer.flipX = true; follow.FollowNow(); AssertCoverage(camera, renderer);
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator LevelBackgroundFollowsCameraAfterMovementAndFitsZoomWhilePaused()
        {
            level = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Level 1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => level.isLoaded);
            SceneManager.SetActiveScene(level);
            yield return null;
            var follow = level.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CameraBackgroundFollow>(true)).Single();
            var camera = follow.targetCamera; var renderer = follow.GetComponent<SpriteRenderer>();
            Assert.Less(renderer.sortingOrder, 0);
            AssertCoverage(camera, renderer);
            var cameraFollow = camera.GetComponent<TraceCameraFollow>();
            var target = new GameObject("Background test camera target"); objects.Add(target);
            target.transform.position = cameraFollow.target.position + new Vector3(20, 10, 0);
            cameraFollow.target = target.transform;
            yield return null; yield return null;
            AssertCoverage(camera, renderer);
            Time.timeScale = 0;
            camera.transform.position += new Vector3(-17, 6, 0); camera.orthographicSize = 15; camera.aspect = 2.4f;
            yield return null; yield return null;
            AssertCoverage(camera, renderer);
        }
#endif
    }
}
