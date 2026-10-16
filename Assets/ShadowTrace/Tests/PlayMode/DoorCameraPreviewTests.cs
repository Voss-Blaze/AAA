using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor.SceneManagement;
#endif

namespace ShadowTrace.Tests
{
    public sealed class DoorCameraPreviewTests
    {
        private Scene previous;
        private Scene scene;
        private TraceCameraFollow camera;
        private Transform player;
        private ShadowPressurePlate plate;
        private InteractiveDoor door;
        private ShadowActor shadow;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Door camera preview test");
            SceneManager.SetActiveScene(scene);
            player = new GameObject("Player target").transform;
            door = new GameObject("Door target").AddComponent<InteractiveDoor>();
            door.transform.position = new Vector3(10, 3, 0);
            plate = new GameObject("Plate").AddComponent<ShadowPressurePlate>();
            plate.transform.position = new Vector3(200, 0, 0);
            plate.targetDoor = door;
            var area = new GameObject("Press area");
            area.transform.SetParent(plate.transform, false);
            area.transform.localPosition = new Vector3(0, 0.65f, 0);
            plate.pressArea = area.AddComponent<BoxCollider2D>();
            plate.pressArea.isTrigger = true; plate.pressArea.size = new Vector2(2, 0.5f);
            shadow = new GameObject("Shadow").AddComponent<ShadowActor>();
            shadow.Body.bodyType = RigidbodyType2D.Kinematic;
            shadow.transform.position = new Vector3(250, 0, 0);
            var go = new GameObject("Camera");
            go.transform.position = new Vector3(0, 0, -10);
            camera = go.AddComponent<TraceCameraFollow>();
            camera.target = player; camera.offset = Vector2.zero;
            camera.doorPreviewSpeed = 100;
            camera.doorPreviewHoldTime = 0.08f;
            Physics2D.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private void Press()
        {
            var support = plate.GetComponent<BoxCollider2D>().bounds;
            shadow.Body.position = new Vector2(support.center.x, support.max.y + shadow.Shape.bounds.extents.y + 0.015f);
            Physics2D.SyncTransforms(); plate.Refresh();
        }

        [UnityTest] public IEnumerator PlatePreviewsDoorHoldsAndReturnsToMovingPlayer()
        {
            Press();
            Assert.IsTrue(camera.IsPreviewing);
            float deadline = Time.realtimeSinceStartup + 3;
            var focus = new Vector3(door.transform.position.x, door.transform.position.y, -10);
            while (Vector3.Distance(camera.transform.position, focus) > 0.01f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(Vector3.Distance(camera.transform.position, focus), 0.01f);
            player.position = new Vector3(-5, 2, 0);
            yield return new WaitForSeconds(0.03f);
            Assert.Less(Vector3.Distance(camera.transform.position, focus), 0.01f);
            while (camera.IsPreviewing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(camera.IsPreviewing);
            Assert.Less(Vector3.Distance(camera.transform.position, new Vector3(-5, 2, -10)), 0.01f);
        }

        [UnityTest] public IEnumerator LatchedPlateCanPreviewAgainAfterReleaseButNotEveryRefresh()
        {
            plate.mode = PressurePlateMode.Latched;
            int presses = 0; plate.Pressed += _ => presses++;
            Press(); plate.Refresh(); plate.Refresh();
            Assert.AreEqual(1, presses);
            shadow.Body.position = new Vector2(250, 0); Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsTrue(plate.IsActive);
            Press(); Assert.AreEqual(2, presses);
            float deadline = Time.realtimeSinceStartup + 4;
            while (camera.IsPreviewing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(camera.IsPreviewing, "Queued repeated previews should complete and return");
        }

        [UnityTest] public IEnumerator PreviewRespectsSpeedPauseAndDestroyedDoor()
        {
            camera.doorPreviewSpeed = 1;
            camera.PreviewDoor(door);
            yield return null; yield return null;
            var before = camera.transform.position;
            yield return new WaitForSeconds(0.1f);
            float travel = Vector3.Distance(before, camera.transform.position);
            Assert.That(travel, Is.InRange(0.05f, 0.3f));
            Time.timeScale = 0; before = camera.transform.position;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(before, camera.transform.position);
            Object.Destroy(door.gameObject);
            Time.timeScale = 1; camera.doorPreviewSpeed = 100;
            float deadline = Time.realtimeSinceStartup + 2;
            while (camera.IsPreviewing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(camera.IsPreviewing);
            Assert.Less(Vector3.Distance(camera.transform.position, new Vector3(0, 0, -10)), 0.01f);
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator AllFiveLevelMechanismsBindTheirExistingFourDoors()
        {
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Level 1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => scene.isLoaded);
            var all = scene.GetRootGameObjects();
            var story = all.SelectMany(g => g.GetComponentsInChildren<LevelStoryController>(true)).Single();
            story.enabled = false;
            var follow = all.SelectMany(g => g.GetComponentsInChildren<TraceCameraFollow>(true)).Single();
            var plates = all.SelectMany(g => g.GetComponentsInChildren<ShadowPressurePlate>(true)).ToArray();
            Assert.AreEqual(5, plates.Length);
            Assert.AreEqual(4, plates.Select(p => p.targetDoor).Distinct().Count());
            var actor = new GameObject("Test shadow").AddComponent<ShadowActor>();
            SceneManager.MoveGameObjectToScene(actor.gameObject, scene);
            actor.Body.bodyType = RigidbodyType2D.Kinematic;
            follow.doorPreviewSpeed = 10000; follow.doorPreviewHoldTime = 0;
            foreach (var mechanism in plates)
            {
                var support = mechanism.GetComponent<BoxCollider2D>().bounds;
                actor.Body.position = new Vector2(support.center.x, support.max.y + actor.Shape.bounds.extents.y + 0.015f);
                Physics2D.SyncTransforms(); mechanism.Refresh();
                Assert.IsTrue(follow.IsPreviewing, mechanism.name);
                yield return null;
                Assert.AreSame(mechanism.targetDoor, follow.PreviewTarget);
                actor.Body.position = new Vector2(300, 300); Physics2D.SyncTransforms(); mechanism.Refresh();
                float deadline = Time.realtimeSinceStartup + 2;
                while (follow.IsPreviewing && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(follow.IsPreviewing);
            }
        }
#endif
    }
}
