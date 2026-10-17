#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class ActorAnimationTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private PlayerController2D player;
        private ShadowManager manager;
        private ShadowTraceAbility ability;
        private ActorAnimationDriver driver;
        private Animator animator;
        private GameObject floor;
        private SimulationMode2D previousMode;

        [SetUp] public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            floor = new GameObject("Animation test ground");
            floor.transform.position = new Vector3(0, -1.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/Player.prefab");
            player = Object.Instantiate(prefab, new Vector3(0, -0.285f), Quaternion.identity).GetComponent<PlayerController2D>();
            player.enabled = false;
            ability = player.GetComponent<ShadowTraceAbility>(); ability.enabled = false;
            driver = player.GetComponent<ActorAnimationDriver>();
            animator = player.GetComponentInChildren<Animator>();
            manager = new GameObject("Animation test manager").AddComponent<ShadowManager>();
            manager.player = player;
            manager.shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/Shadow.prefab").GetComponent<ShadowActor>();
            ability.manager = manager;
            Physics2D.SyncTransforms();
            Animate(driver);
        }

        [TearDown] public void TearDown()
        {
            foreach (var actor in manager.Actors) if (actor != null) Object.DestroyImmediate(actor.gameObject);
            Object.DestroyImmediate(manager.gameObject); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(floor);
            Physics2D.simulationMode = previousMode;
        }

        private void Input(int direction, bool sprint = false)
        {
            typeof(PlayerController2D).GetProperty("HorizontalInput").SetValue(player, direction);
            typeof(PlayerController2D).GetField("sprint", Private).SetValue(player, sprint);
        }

        private static void Animate(ActorAnimationDriver target)
        {
            typeof(ActorAnimationDriver).GetMethod("LateUpdate", Private).Invoke(target, null);
            target.GetComponentInChildren<Animator>().Update(0.02f);
        }

        private void Finish(bool valid = true)
        {
            typeof(ShadowTraceAbility).GetField("recording", Private).SetValue(ability, new TraceClassification());
            typeof(ShadowTraceAbility).GetField("origin", Private).SetValue(ability, valid ? player.Body.position : new Vector2(100, 100));
            typeof(ShadowTraceAbility).GetMethod("Finish", Private).Invoke(ability, null);
            Animate(driver);
        }

        private void AssertState(ActorMotionState expected)
        {
            Assert.AreEqual(expected, driver.State);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(expected.ToString()), "Animator must follow the driver.");
        }

        [Test] public void MotionUsesActualSpeedAndShiftAloneStaysIdle()
        {
            Input(0, true); Animate(driver); AssertState(ActorMotionState.Idle);
            Input(-1); player.Body.velocity = new Vector2(-4, 0); Animate(driver);
            AssertState(ActorMotionState.Walk); Assert.IsTrue(player.GetComponentInChildren<SpriteRenderer>().flipX);
            Input(1, true); player.Body.velocity = new Vector2(7, 0); Animate(driver);
            AssertState(ActorMotionState.Run); Assert.IsFalse(player.GetComponentInChildren<SpriteRenderer>().flipX);
            player.Body.velocity = Vector2.zero; Animate(driver); AssertState(ActorMotionState.Idle); // pressing against a wall
        }

        [Test] public void AirborneStateSurvivesRepeatedJumpRequestsAndLandingRestoresWalk()
        {
            Input(1); player.Body.position = new Vector2(0, 3); player.Body.velocity = new Vector2(4, 5);
            Physics2D.SyncTransforms(); Animate(driver); AssertState(ActorMotionState.Jump);
            animator.Update(0.2f); float progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            player.RequestJump(); Animate(driver);
            AssertState(ActorMotionState.Jump);
            Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, progress);
            player.Body.position = new Vector2(0, -0.285f); player.Body.velocity = new Vector2(4, 0);
            Physics2D.SyncTransforms(); Animate(driver); AssertState(ActorMotionState.Walk);
        }

        [TestCase(0f, 2f)] [TestCase(2f, 0f)] [TestCase(0f, -2f)]
        public void MovingPlatformRiderStaysGroundedAndIdleThenCanJump(float x, float y)
        {
            floor.SetActive(false);
            var platformObject = new GameObject("Animation moving platform");
            try
            {
                platformObject.transform.position = new Vector3(0, -1.5f, 0);
                var platform = platformObject.AddComponent<MovingPlatform>();
                platform.GetComponent<BoxCollider2D>().size = new Vector2(10, 1);
                platform.offset = new Vector2(x, y); platform.speed = 2; platform.SetPressed(true);
                player.Body.gravityScale = 0;
                Physics2D.SyncTransforms();
                Input(0);
                for (int i = 0; i < 12; i++)
                {
                    typeof(MovingPlatform).GetMethod("FixedUpdate", Private).Invoke(platform, null);
                    typeof(PlayerController2D).GetMethod("FixedUpdate", Private).Invoke(player, null);
                    Physics2D.Simulate(0.02f); Physics2D.SyncTransforms();
                    Animate(driver); AssertState(ActorMotionState.Idle);
                    Assert.IsTrue(player.IsGrounded);
                }
                platform.offset = Vector2.zero;
                player.Body.velocity = new Vector2(0, Mathf.Max(0, y));
                typeof(MovingPlatform).GetMethod("FixedUpdate", Private).Invoke(platform, null);
                Animate(driver); AssertState(ActorMotionState.Idle);
                player.RequestJump();
                typeof(PlayerController2D).GetMethod("FixedUpdate", Private).Invoke(player, null);
                Animate(driver); AssertState(ActorMotionState.Jump);
                Assert.Greater(player.Body.velocity.y, 5);
            }
            finally { Object.DestroyImmediate(platformObject); }
        }

        [UnityTest] public IEnumerator SuccessfulCastExpiresWithoutLockingMovement()
        {
            Finish(); Assert.AreEqual(1, manager.Count); AssertState(ActorMotionState.Shadowtrace);
            yield return new WaitForSeconds(0.55f);
            Animate(driver); AssertState(ActorMotionState.Idle);
        }

        [Test] public void FailedOrMovingCastDoesNotPlayAndJumpInterruptsSuccessfulCast()
        {
            Finish(false); Assert.AreEqual(0, manager.Count); AssertState(ActorMotionState.Idle);
            Input(1); Finish(); Assert.AreEqual(1, manager.Count); AssertState(ActorMotionState.Idle);
            Input(0); Finish(); AssertState(ActorMotionState.Shadowtrace);
            player.Body.velocity = new Vector2(0, 10); Animate(driver); AssertState(ActorMotionState.Jump);
        }

        [TestCase(ShadowKind.Stay)] [TestCase(ShadowKind.Approach)]
        [TestCase(ShadowKind.Avoid)] [TestCase(ShadowKind.Follow)]
        public void ShadowsUseTheirOwnMotionAndKeepSelectionTint(ShadowKind kind)
        {
            var prefabShape = manager.shadowPrefab.GetComponent<BoxCollider2D>();
            var scale = manager.shadowPrefab.transform.lossyScale;
            float halfHeight = prefabShape.size.y * Mathf.Abs(scale.y) * 0.5f;
            var origin = new Vector2(4, -1 + halfHeight + 0.015f - prefabShape.offset.y * scale.y);
            Assert.IsTrue(manager.TrySpawn(kind, origin, -1));
            var shadow = manager.Actors[0];
            var animation = shadow.GetComponent<ActorAnimationDriver>();
            Input(1, true); player.Body.velocity = new Vector2(7, 0);
            shadow.Body.velocity = kind == ShadowKind.Stay ? Vector2.zero : new Vector2(-4, 0);
            Animate(animation);
            Assert.AreEqual(kind == ShadowKind.Stay ? ActorMotionState.Idle : ActorMotionState.Walk, animation.State);
            var bodyColor = shadow.GetComponentInChildren<SpriteRenderer>().color;
            manager.Select(0); Animate(animation);
            Assert.IsTrue(shadow.GetComponent<ShadowSelectionGlow>().selected);
            Assert.AreEqual(bodyColor, shadow.GetComponentInChildren<SpriteRenderer>().color);
            Assert.AreEqual(prefabShape.offset, shadow.Shape.offset);
            if (kind == ShadowKind.Stay) return;
            shadow.Body.position = new Vector2(4, 4); shadow.Body.velocity = new Vector2(-4, -2);
            Physics2D.SyncTransforms(); Animate(animation); Assert.AreEqual(ActorMotionState.Jump, animation.State);
        }
    }

    public sealed class SampleAnimationSceneTests
    {
        private Scene previous;
        private Scene sample;

        [UnityTest] public IEnumerator SampleStartsWithOneWiredAnimatedPlayerAndGroundColliders()
        {
            previous = SceneManager.GetActiveScene();
            SceneManager.sceneLoaded += Loaded;
            sample = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => sample.isLoaded);
            yield return new WaitForSeconds(0.6f);
            var players = Object.FindObjectsOfType<PlayerController2D>();
            Assert.AreEqual(1, players.Length);
            var player = players[0];
            Assert.IsTrue(player.CompareTag("Player")); Assert.IsTrue(player.IsGrounded);
            var ability = player.GetComponent<ShadowTraceAbility>();
            var manager = ability.manager;
            Assert.IsNotNull(manager); Assert.AreSame(player, manager.player);
            Assert.AreEqual(player.Shape.size, manager.shadowPrefab.GetComponent<BoxCollider2D>().size);
            Assert.IsNotNull(Object.FindObjectOfType<ShadowTraceHUD>());
            Assert.AreSame(player.transform, Camera.main.GetComponent<TraceCameraFollow>().target);
            var driver = player.GetComponent<ActorAnimationDriver>();
            yield return null;
            Assert.AreEqual(ActorMotionState.Idle, driver.State);
            Assert.AreSame(player.GetComponentInChildren<Animator>().runtimeAnimatorController,
                manager.shadowPrefab.GetComponentInChildren<Animator>().runtimeAnimatorController);
            int grounds = 0;
            foreach (var root in sample.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>())
                    if (item.CompareTag("Ground"))
                    {
                        grounds++;
                        Assert.IsNotNull(item.GetComponent<Collider2D>());
                        Assert.IsFalse(item.GetComponent<Collider2D>().isTrigger);
                    }
            Assert.AreEqual(2, grounds);
            Assert.IsTrue(manager.TrySpawn(ShadowKind.Follow, new Vector2(player.Body.position.x, player.Body.position.y), 1));
            manager.Select(0);
            Assert.IsTrue(manager.Actors[0].GetComponent<ShadowSelectionGlow>().selected);
            manager.ClearAll();
        }

        private void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SampleScene") return;
            sample = scene; SceneManager.SetActiveScene(scene);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= Loaded;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        }
    }
}
#endif
