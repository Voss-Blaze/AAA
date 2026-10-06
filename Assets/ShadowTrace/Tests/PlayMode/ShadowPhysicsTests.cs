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

        [UnityTest] public IEnumerator FollowMaintainsOffsetAndCannotCrossWall()
        {
            var actor = Spawn(ShadowKind.Follow, Vector2.zero);
            player.Body.position += Vector2.up;
            yield return new WaitForSeconds(0.3f);
            Assert.That(actor.transform.position.y, Is.EqualTo(1).Within(0.1f));
            var wall = World("Wall", new Vector2(2, 1), new Vector2(0.5f, 6));
            player.Body.position += Vector2.right * 5;
            yield return new WaitForSeconds(0.7f);
            Assert.Less(actor.transform.position.x, 1.5f);
            Object.Destroy(wall);
            yield return new WaitForSeconds(0.7f);
            Assert.That(actor.transform.position.x, Is.EqualTo(5).Within(0.1f));
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
    }
}
