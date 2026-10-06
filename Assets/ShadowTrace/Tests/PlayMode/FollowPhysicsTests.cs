using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    /// <summary>Run the actual controllers and solver in fixed steps, independently of keyboard/frame timing.</summary>
    public sealed class FollowPhysicsTests
    {
        private const float GroundY = -0.285f;
        private const float UpperY = 2.715f;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ShadowActor> followers = new List<ShadowActor>();
        private static readonly MethodInfo PlayerTick = typeof(PlayerController2D).GetMethod(
            "FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo ShadowTick = typeof(ShadowActor).GetMethod(
            "FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        private PlayerController2D player;
        private ShadowManager manager;
        private PhysicsMaterial2D surface;
        private SimulationMode2D previousMode;

        [SetUp] public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            surface = new PhysicsMaterial2D("Test surface") { friction = 0, bounciness = 0 };
            player = Actor("Player", new Vector2(-4, GroundY)).AddComponent<PlayerController2D>();
            var source = Actor("Shadow source", new Vector2(-100, -100));
            source.GetComponent<Rigidbody2D>().gravityScale = 0;
            source.AddComponent<SpriteRenderer>();
            var prefab = source.AddComponent<ShadowActor>();
            var root = new GameObject("Manager"); objects.Add(root);
            manager = root.AddComponent<ShadowManager>();
            manager.player = player; manager.shadowPrefab = prefab;
            World("Ground", new Vector2(0, -1.5f), new Vector2(40, 1));
            Physics2D.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            manager.ClearAll();
            foreach (var item in objects) Object.Destroy(item);
            Object.Destroy(surface);
            objects.Clear(); followers.Clear();
            Physics2D.simulationMode = previousMode;
            yield return null;
        }

        private GameObject Actor(string name, Vector2 position)
        {
            var item = new GameObject(name); objects.Add(item);
            item.transform.position = position;
            var shape = item.AddComponent<BoxCollider2D>();
            shape.size = new Vector2(0.8f, 1.4f); shape.sharedMaterial = surface;
            var body = item.AddComponent<Rigidbody2D>();
            body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            return item;
        }

        private GameObject World(string name, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name); objects.Add(item);
            item.transform.position = position;
            item.AddComponent<BoxCollider2D>().size = size;
            return item;
        }

        private ShadowActor Follow(Vector2 position)
        {
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Follow, position, 1));
            var follower = manager.Actors[manager.Count - 1];
            followers.Add(follower);
            return follower;
        }

        private void Input(int direction, bool sprint = false)
        {
            // Set the existing input snapshot without changing the production keyboard input API.
            typeof(PlayerController2D).GetProperty("HorizontalInput").SetValue(player, direction);
            typeof(PlayerController2D).GetField("sprint", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, sprint);
        }

        private void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Physics2D.SyncTransforms();
                PlayerTick.Invoke(player, null);
                foreach (var follower in followers) ShadowTick.Invoke(follower, null);
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
        }

        private static Rigidbody2D Body(ShadowActor follower) => follower.GetComponent<Rigidbody2D>();
        private float Gap(ShadowActor follower) => Body(follower).position.x - player.Body.position.x;

        [TestCase(-1)] [TestCase(1)] public void NormalMovementPreservesGapAndReleaseStopsBoth(int direction)
        {
            var follower = Follow(new Vector2(0, GroundY));
            Step(5); float gap = Gap(follower);
            Input(direction); Step(30);
            Assert.That(Gap(follower), Is.EqualTo(gap).Within(0.03f));
            Assert.That(Body(follower).velocity.x, Is.EqualTo(direction * player.walkSpeed).Within(0.01f));
            Input(0); float playerX = player.Body.position.x; float followerX = Body(follower).position.x;
            Step(20);
            Assert.That(player.Body.position.x, Is.EqualTo(playerX).Within(0.01f));
            Assert.That(Body(follower).position.x, Is.EqualTo(followerX).Within(0.01f));
        }

        [TestCase(true)] [TestCase(false)] public void EitherActorCanBeBlockedWithoutRestoringOldGap(bool blockPlayer)
        {
            var follower = Follow(new Vector2(0, GroundY));
            var wall = World("Wall", new Vector2(blockPlayer ? -2 : 2, 0), new Vector2(0.4f, 2));
            Step(5); float initialGap = Gap(follower);
            Input(1); Step(45);
            Assert.That(Mathf.Abs(Gap(follower) - initialGap), Is.GreaterThan(0.8f));
            Assert.IsFalse(follower.Stopped);
            if (blockPlayer) Assert.Greater(Body(follower).position.x, 3);
            else Assert.Less(Body(follower).position.x, 1.5f);
            Object.DestroyImmediate(wall);
            Step(3); float newGap = Gap(follower);
            Step(25);
            Assert.That(Gap(follower), Is.EqualTo(newGap).Within(0.03f));
            Input(0); Step(15);
            Assert.That(Gap(follower), Is.EqualTo(newGap).Within(0.03f));
        }

        [Test] public void SprintOnlyAcceleratesPlayerAndAllowsPassingWithoutCatchUp()
        {
            var follower = Follow(new Vector2(0, GroundY));
            Step(5); Assert.Greater(Gap(follower), 0);
            Input(1, true); Step(80);
            Assert.That(player.Body.velocity.x, Is.EqualTo(player.sprintSpeed).Within(0.01f));
            Assert.That(Body(follower).velocity.x, Is.EqualTo(player.walkSpeed).Within(0.01f));
            Assert.Less(Gap(follower), 0, "The player must be able to cross through the follower.");
            Assert.IsTrue(Physics2D.GetIgnoreCollision(follower.Shape, player.Shape));
            Input(1); Step(3); float newGap = Gap(follower);
            Step(25); Assert.That(Gap(follower), Is.EqualTo(newGap).Within(0.03f));
            Input(0); Step(20); Assert.That(Gap(follower), Is.EqualTo(newGap).Within(0.03f));
            Assert.IsFalse(follower.PlayerCollisionEnabled);
        }

        [TestCase(true)] [TestCase(false)] public void SeparateFloorsShareHorizontalInputOnly(bool playerAbove)
        {
            World("Upper floor", new Vector2(0, 1.75f), new Vector2(20, 0.5f));
            player.Body.position = new Vector2(-2, playerAbove ? UpperY : GroundY);
            var follower = Follow(new Vector2(-6, playerAbove ? GroundY : UpperY));
            Step(5); float gap = Gap(follower);
            Input(1); Step(20);
            Assert.That(Gap(follower), Is.EqualTo(gap).Within(0.03f));
            Assert.That(player.Body.position.y, Is.EqualTo(playerAbove ? UpperY : GroundY).Within(0.04f));
            Assert.That(Body(follower).position.y, Is.EqualTo(playerAbove ? GroundY : UpperY).Within(0.04f));
            Assert.That(Body(follower).gravityScale, Is.EqualTo(player.Body.gravityScale));
        }

        [Test] public void FollowerCanPassBlockedPlayerOnAnotherFloor()
        {
            World("Upper floor", new Vector2(0, 1.75f), new Vector2(20, 0.5f));
            World("Player wall", new Vector2(1, 0), new Vector2(0.4f, 2));
            player.Body.position = new Vector2(0, GroundY);
            var follower = Follow(new Vector2(-3, UpperY));
            Step(5); Assert.Less(Gap(follower), 0);
            Input(1); Step(60);
            Assert.Greater(Gap(follower), 1);
            Input(-1); Step(3); float gap = Gap(follower);
            Step(15); Assert.That(Gap(follower), Is.EqualTo(gap).Within(0.03f));
        }

        [Test] public void FollowerFallsToLowerFloorWhilePlayerStaysAbove()
        {
            World("Upper floor", new Vector2(0, 1.75f), new Vector2(6, 0.5f));
            player.Body.position = new Vector2(-2.5f, UpperY);
            var follower = Follow(new Vector2(2, UpperY));
            Step(5); Input(1); Step(50);
            Assert.That(player.Body.position.y, Is.EqualTo(UpperY).Within(0.04f));
            Assert.That(Body(follower).position.y, Is.EqualTo(GroundY).Within(0.06f));
            Input(-1); Step(20);
            Assert.That(Body(follower).position.y, Is.EqualTo(GroundY).Within(0.04f));
            Assert.That(player.Body.position.y, Is.EqualTo(UpperY).Within(0.04f));
        }

        [Test] public void JumpBroadcastChecksEachGroundAndDoesNotDependOnSelection()
        {
            var first = Follow(new Vector2(0, GroundY));
            var second = Follow(new Vector2(3, GroundY));
            var airborne = Follow(new Vector2(6, 3));
            Step(5); manager.Select(0);
            player.RequestJump(); Step(1);
            Assert.Greater(player.Body.velocity.y, 8);
            Assert.Greater(Body(first).velocity.y, 8);
            Assert.Greater(Body(second).velocity.y, 8);
            Assert.Less(Body(airborne).velocity.y, 0);
            float firstVelocity = Body(first).velocity.y;
            player.RequestJump(); Step(1);
            Assert.Less(Body(first).velocity.y, firstVelocity, "Repeated requests must not double-jump.");
        }

        [Test] public void GroundedFollowerCanJumpWhenPlayerIsAirborne()
        {
            var follower = Follow(new Vector2(0, GroundY));
            Step(5); player.Body.position = new Vector2(-4, 6);
            player.Body.velocity = Vector2.zero;
            player.RequestJump(); Step(1);
            Assert.Less(player.Body.velocity.y, 0);
            Assert.Greater(Body(follower).velocity.y, 8);
        }

        [Test] public void AirborneFollowerCannotJumpWithGroundedPlayerOrStoreJumpUntilLanding()
        {
            var follower = Follow(new Vector2(0, 3));
            Step(5); player.RequestJump(); Step(1);
            Assert.Greater(player.Body.velocity.y, 8);
            Assert.Less(Body(follower).velocity.y, 0);
            Step(70);
            Assert.That(Body(follower).position.y, Is.EqualTo(GroundY).Within(0.04f));
            Assert.That(Body(follower).velocity.y, Is.EqualTo(0).Within(0.02f));
        }
    }
}
