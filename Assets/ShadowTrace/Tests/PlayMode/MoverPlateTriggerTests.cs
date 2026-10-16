#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class MoverPlateTriggerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private ShadowManager manager;
        private PlayerController2D player;
        private ShadowMoverPlate plate;
        private MovingPlatform platform;

        private GameObject New(string name, Vector2 position)
        {
            var go = new GameObject(name); objects.Add(go); go.transform.position = position;
            return go;
        }

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1;
            var floor = New("Floor", new Vector2(0, -1.5f));
            floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
            player = New("Player", new Vector2(-5, 0)).AddComponent<PlayerController2D>();
            player.Shape.size = new Vector2(0.8f, 1.4f);
            player.Body.gravityScale = 0;
            var source = New("Shadow source", new Vector2(-100, -100));
            source.AddComponent<SpriteRenderer>();
            var prefab = source.AddComponent<ShadowActor>();
            prefab.Shape.size = new Vector2(0.8f, 1.4f);
            manager = New("Manager", Vector2.zero).AddComponent<ShadowManager>();
            manager.player = player; manager.shadowPrefab = prefab;
            platform = New("Platform", new Vector2(10, 3)).AddComponent<MovingPlatform>();
            platform.offset = Vector2.right; platform.speed = 5; platform.carryRiders = false;
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PressurePlate1.prefab");
            Assert.IsNotNull(saved);
            foreach (var collider in saved.GetComponentsInChildren<Collider2D>(true)) Assert.IsTrue(collider.isTrigger);
            var instance = Object.Instantiate(saved, Vector3.zero, Quaternion.identity);
            objects.Add(instance);
            plate = instance.GetComponent<ShadowMoverPlate>();
            plate.targetPlatform = platform;
            Physics2D.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (manager != null) manager.ClearAll();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear();
            Time.timeScale = 1;
            yield return null;
        }

        private ShadowActor Spawn()
        {
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            foreach (var shadow in manager.Actors) if (shadow != null) return shadow;
            Assert.Fail("Expected shadow"); return null;
        }

        [Test] public void ActualSampleShadowPrefabLandsOnGroundAndActivatesTrigger()
        {
            manager.shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ShadowTrace/Prefabs/SampleShadow.prefab").GetComponent<ShadowActor>();
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, new Vector2(0, 4), 0));
            var shadow = manager.Actors[0];
            Physics2D.SyncTransforms(); plate.Refresh();
            Assert.That(shadow.Shape.bounds.min.y, Is.EqualTo(-0.985f).Within(0.005f));
            Assert.IsTrue(plate.IsPressed);
        }

        [UnityTest] public IEnumerator StationaryShadowTriggersPatrolAndRemovalReturnsPlatform()
        {
            var shadow = Spawn();
            Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsTrue(plate.IsPressed, "Overlap must work without matching the old plate-top height.");
            Assert.IsTrue(platform.IsPressed);
            yield return new WaitForSeconds(0.12f);
            Assert.Greater(platform.Position.x, platform.Origin.x);
            shadow.Despawn();
            yield return new WaitForSeconds(0.35f);
            Assert.IsFalse(plate.IsPressed);
            Assert.That(Vector2.Distance(platform.Position, platform.Origin), Is.LessThan(0.02f));
        }

        [UnityTest] public IEnumerator ToggleWaitsForExitBeforeSecondActivation()
        {
            plate.mode = MoverPlateMode.Toggle;
            var shadow = Spawn();
            Physics2D.SyncTransforms(); plate.Refresh();
            yield return new WaitForSeconds(0.35f);
            Assert.That(Vector2.Distance(platform.Position, platform.Destination), Is.LessThan(0.02f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(Vector2.Distance(platform.Position, platform.Destination), Is.LessThan(0.02f));
            shadow.Body.position = new Vector2(-4, 3);
            Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsFalse(plate.IsPressed);
            shadow.Body.position = Vector2.zero;
            Physics2D.SyncTransforms(); plate.Refresh();
            yield return new WaitForSeconds(0.35f);
            Assert.That(Vector2.Distance(platform.Position, platform.Origin), Is.LessThan(0.02f));
        }

        [Test] public void MultipleShadowsHoldTriggerUntilLastOneLeaves()
        {
            Spawn();
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            var first = manager.Actors[0]; var second = manager.Actors[1];
            Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsPressed);
            first.Despawn(); plate.Refresh(); Assert.IsTrue(plate.IsPressed);
            second.Despawn(); plate.Refresh(); Assert.IsFalse(plate.IsPressed);
            Assert.IsFalse(platform.IsPressed);
        }

        [Test] public void TriggerRespectsActorFiltersAndReleasesWhenDisabled()
        {
            player.Body.position = Vector2.zero;
            Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsFalse(plate.IsPressed);
            plate.playerCanPress = true; plate.Refresh(); Assert.IsTrue(plate.IsPressed);
            plate.playerCanPress = false;
            Spawn(); Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsPressed);
            plate.shadowCanPress = false; plate.Refresh(); Assert.IsFalse(plate.IsPressed);
            plate.shadowCanPress = true; plate.Refresh(); Assert.IsTrue(plate.IsPressed);
            plate.enabled = false; Assert.IsFalse(platform.IsPressed);
        }
    }
}
#endif
