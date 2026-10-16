using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class ShadowPhysicsTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private PlayerController2D player;
        private ShadowActor prefab;
        private ShadowManager manager;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1;
            player = Create("Player", new Vector2(-5, 0)).AddComponent<PlayerController2D>();
            player.Body.gravityScale = 0;
            var source = Create("Prefab", new Vector2(-100, -100));
            source.AddComponent<SpriteRenderer>();
            source.GetComponent<Rigidbody2D>().gravityScale = 0;
            prefab = source.AddComponent<ShadowActor>();
            manager = new GameObject("Manager").AddComponent<ShadowManager>();
            objects.Add(manager.gameObject);
            manager.player = player; manager.shadowPrefab = prefab;
            World("Floor", new Vector2(0, -1.5f), new Vector2(30, 1));
            Physics2D.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            manager.ClearAll();
            foreach (var item in objects) Object.Destroy(item);
            objects.Clear();
            yield return null;
        }

        private GameObject Create(string name, Vector2 position)
        {
            var item = new GameObject(name); objects.Add(item); item.transform.position = position;
            item.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 1.4f);
            item.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
            return item;
        }

        private GameObject World(string name, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name); objects.Add(item); item.transform.position = position;
            item.AddComponent<BoxCollider2D>().size = size;
            return item;
        }

        private ShadowActor Spawn(ShadowKind kind, Vector2 position, int direction = 1)
        {
            Assert.IsTrue(manager.TrySpawn(kind, position, direction));
            var actor = manager.Actors[manager.Count - 1];
            actor.gameObject.SetActive(true);
            return actor;
        }

        [TestCase(1f, 1f)] [TestCase(0.15f, 0.15f)]
        [TestCase(-0.15f, 0.15f)] [TestCase(0.15f, -0.15f)]
        public void StayUsesScaledColliderOffsetToPlaceActualFeetOnGround(float scaleX, float scaleY)
        {
            prefab.Shape.size = new Vector2(2, 4);
            prefab.Shape.offset = new Vector2(0.7f, -1.8f);
            prefab.transform.localScale = new Vector3(scaleX, scaleY, 1);
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, new Vector2(1, 4), 0));
            var shadow = manager.Actors[0];
            Physics2D.SyncTransforms();
            Assert.That(shadow.Shape.bounds.min.y, Is.EqualTo(-0.985f).Within(0.005f));
            Assert.That(shadow.Shape.bounds.center.x, Is.EqualTo(1 + 0.7f * scaleX).Within(0.005f));
        }

        [Test] public void StaySearchesForGroundUnderOffsetFootprint()
        {
            prefab.Shape.offset = new Vector2(3, -0.3f);
            World("Raised support", new Vector2(3, 1), new Vector2(1, 0.5f));
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, new Vector2(0, 4), 0));
            Physics2D.SyncTransforms();
            Assert.That(manager.Actors[0].Shape.bounds.min.y, Is.EqualTo(1.265f).Within(0.005f));
        }

        [Test] public void SpawnChecksActualOffsetColliderInsteadOfRootPosition()
        {
            prefab.Shape.offset = new Vector2(3, 0);
            var wall = World("Wall at collider", new Vector2(3, 0), Vector2.one);
            Assert.IsFalse(manager.TrySpawn(ShadowKind.Approach, Vector2.zero, 1));
            Assert.AreEqual(0, manager.Count);
            wall.transform.position = Vector2.zero;
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Approach, Vector2.zero, 1));
            Assert.AreEqual(1, manager.Count);
        }

        [UnityTest] public IEnumerator SpikesDestroyAllShadowKindsAndReleaseTheirIconsAndNumbers()
        {
            var spike = new GameObject("Spike"); objects.Add(spike);
            spike.AddComponent<BoxCollider2D>().isTrigger = true;
            spike.AddComponent<SpikeHazard>();
            var hud = manager.gameObject.AddComponent<ShadowTraceHUD>();
            hud.manager = manager;
            var icon = new GameObject("Occupied icon"); objects.Add(icon);
            hud.occupiedIcons = new[] { icon };
            foreach (ShadowKind kind in System.Enum.GetValues(typeof(ShadowKind)))
            {
                Assert.IsTrue(manager.TrySpawn(kind, Vector2.zero, 1));
                var shadow = manager.Actors[0];
                Assert.AreEqual(1, shadow.Number);
                manager.Select(0); hud.Refresh();
                Assert.IsTrue(icon.activeSelf);
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.IsTrue(shadow == null);
                Assert.AreEqual(0, manager.Count);
                Assert.AreEqual(-1, manager.SelectedSlot);
                hud.Refresh(); Assert.IsFalse(icon.activeSelf);
                Assert.AreEqual(1, Time.timeScale);
            }
        }

        [UnityTest] public IEnumerator SpikeStillKillsPlayer()
        {
            var life = player.gameObject.AddComponent<PlayerLife>();
            var spike = new GameObject("Spike"); objects.Add(spike);
            spike.transform.position = player.Body.position;
            spike.AddComponent<BoxCollider2D>().isTrigger = true;
            spike.AddComponent<SpikeHazard>();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(life.IsDead);
            Assert.AreEqual(0, Time.timeScale);
            Object.DestroyImmediate(life);
            Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest] public IEnumerator ApproachStopsAndBecomesPermanentPlatform()
        {
            var wall = World("Wall", new Vector2(2, 0), new Vector2(0.5f, 4));
            var actor = Spawn(ShadowKind.Approach, Vector2.zero);
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(actor.Stopped); Assert.IsTrue(actor.PlayerCollisionEnabled);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(actor.Shape, player.Shape));
            float x = actor.transform.position.x;
            Object.Destroy(wall);
            yield return new WaitForSeconds(0.3f);
            Assert.That(actor.transform.position.x, Is.EqualTo(x).Within(0.03f));
            player.Body.position = actor.GetComponent<Rigidbody2D>().position + Vector2.up * 1.6f;
            player.Body.gravityScale = 3;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(player.IsGrounded, "The player must be able to stand on the stopped shadow.");
            Assert.Greater(player.Body.position.y, actor.transform.position.y + 1.2f);
        }

        [UnityTest] public IEnumerator ApproachPushesDynamicMarkedBox()
        {
            var box = Create("Box", new Vector2(1.2f, 0));
            box.GetComponent<Rigidbody2D>().gravityScale = 0;
            box.AddComponent<PushableBody2D>();
            var actor = Spawn(ShadowKind.Approach, Vector2.zero);
            yield return new WaitForSeconds(0.7f);
            Assert.Greater(box.transform.position.x, 2f);
            Assert.IsFalse(actor.Stopped);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(actor.Shape, player.Shape));
        }

        [UnityTest] public IEnumerator AvoidReversesAtWall()
        {
            World("Wall", new Vector2(2, 0), new Vector2(0.5f, 4));
            var actor = Spawn(ShadowKind.Avoid, Vector2.zero);
            yield return new WaitForSeconds(0.8f);
            Assert.Less(actor.GetComponent<Rigidbody2D>().velocity.x, 0);
            Assert.IsFalse(actor.Stopped);
        }

        [Test] public void StayFindsGroundAndBlockedSpawnDoesNotAllocate()
        {
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, new Vector2(0, 3), 0));
            Assert.That(manager.Actors[0].transform.position.y, Is.EqualTo(-0.285f).Within(0.04f));
            World("Occupied", new Vector2(3, 0), new Vector2(2, 2));
            Physics2D.SyncTransforms();
            Assert.IsFalse(manager.TrySpawn(ShadowKind.Approach, new Vector2(3, 0), 1));
            Assert.AreEqual(1, manager.Count);
        }

        [UnityTest] public IEnumerator OverlappingPlayerDelaysPlatformCollision()
        {
            World("Wall", new Vector2(0.72f, 0), new Vector2(0.5f, 4));
            player.Body.position = Vector2.zero;
            var actor = Spawn(ShadowKind.Approach, Vector2.zero);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(actor.Stopped);
            Assert.IsFalse(actor.PlayerCollisionEnabled);
            player.Body.position = new Vector2(-5, 0);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.PlayerCollisionEnabled);
        }

        [UnityTest] public IEnumerator BoxBlockedByWallEventuallyStopsApproach()
        {
            var box = Create("Box", new Vector2(1.2f, 0));
            box.AddComponent<PushableBody2D>();
            box.GetComponent<Rigidbody2D>().gravityScale = 0;
            World("Wall", new Vector2(2.1f, 0), new Vector2(0.5f, 4));
            var actor = Spawn(ShadowKind.Approach, Vector2.zero);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(actor.Stopped);
        }

        [UnityTest] public IEnumerator PreparationCanCancelAndRecordingCanFinishEarly()
        {
            var ability = player.gameObject.AddComponent<ShadowTraceAbility>();
            ability.manager = manager;
            ability.preparationDuration = 0.06f;
            ability.recordingDuration = 0.5f;
            ability.UseTrace();
            Assert.AreEqual(TracePhase.Preparing, ability.Phase);
            ability.UseTrace();
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.AreEqual(0, manager.Count);
            ability.UseTrace();
            yield return new WaitForSeconds(0.12f);
            Assert.AreEqual(TracePhase.Recording, ability.Phase);
            ability.UseTrace();
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.AreEqual(1, manager.Count);
        }

        [UnityTest] public IEnumerator RecordingAutomaticallyFinishesAndFullCapacityPreventsPreparation()
        {
            var ability = player.gameObject.AddComponent<ShadowTraceAbility>();
            ability.manager = manager; ability.preparationDuration = 0.04f; ability.recordingDuration = 0.06f;
            ability.UseTrace();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.AreEqual(1, manager.Count);
            for (int i = 1; i < 8; i++) Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            ability.UseTrace();
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.AreEqual(8, manager.Count);
            for (int i = 1; i < 8; i++)
                Assert.IsTrue(Physics2D.GetIgnoreCollision(manager.Actors[0].Shape, manager.Actors[i].Shape));
        }

        [TestCase(0, 1)] [TestCase(3, 3)] [TestCase(12, 12)]
        public void ConfiguredCapacityLimitsSpawningAndReusesDeletedNumber(int configured, int expected)
        {
            objects.Remove(manager.gameObject);
            Object.DestroyImmediate(manager.gameObject);
            var root = new GameObject("Configured manager");
            root.SetActive(false); objects.Add(root);
            manager = root.AddComponent<ShadowManager>();
            manager.maxShadows = configured; manager.player = player; manager.shadowPrefab = prefab;
            root.SetActive(true);
            Assert.AreEqual(expected, manager.Capacity);
            for (int i = 0; i < expected; i++) Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            Assert.IsTrue(manager.Full); Assert.IsFalse(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            int hole = expected / 2;
            manager.Select(hole);
            Object.DestroyImmediate(manager.Actors[hole].gameObject);
            Assert.AreEqual(-1, manager.SelectedSlot); Assert.IsFalse(manager.Full);
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Stay, Vector2.zero, 0));
            Assert.AreEqual(hole + 1, manager.Actors[hole].Number);
            Assert.AreEqual(expected, manager.Count);
            manager.Select(expected - 1); manager.CycleSelection();
            Assert.AreEqual(0, manager.SelectedSlot);
        }
    }
}
