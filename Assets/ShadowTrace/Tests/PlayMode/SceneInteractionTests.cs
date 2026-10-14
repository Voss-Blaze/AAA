#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class SceneInteractionTests
    {
        private const string Props = "Assets/Art/environment/en_elements/";
        private readonly List<GameObject> objects = new List<GameObject>();
        private PlayerController2D player;
        private ShadowManager manager;
        private ShadowTraceAbility ability;
        private SimulationMode2D previousMode;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp] public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Time.timeScale = 1;
            player = Spawn("Assets/ShadowTrace/Prefabs/Player.prefab", new Vector2(-8, 10)).GetComponent<PlayerController2D>();
            manager = New("Test shadow manager").AddComponent<ShadowManager>();
            manager.player = player;
            manager.shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/Shadow.prefab").GetComponent<ShadowActor>();
            ability = player.GetComponent<ShadowTraceAbility>(); ability.manager = manager;
            var floor = New("Test player floor");
            floor.transform.position = new Vector2(-8, 8.8f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(5, 1);
            Physics2D.SyncTransforms();
        }

        private GameObject New(string name)
        { var root = new GameObject(name); objects.Add(root); return root; }

        private GameObject Spawn(string path, Vector2 position)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
            objects.Add(root); return root;
        }

        private ShadowPressurePlate Plate(float x, PressurePlateMode mode, InteractiveDoor target = null)
        {
            var plate = Spawn(Props + "PressurePlate2.prefab", new Vector2(x, 0)).GetComponent<ShadowPressurePlate>();
            plate.mode = mode; plate.targetDoor = target;
            var support = plate.GetComponent<BoxCollider2D>(); support.size = new Vector2(2, 0.3f); support.offset = Vector2.zero;
            plate.pressArea.size = new Vector2(2, 0.24f); plate.pressArea.offset = new Vector2(0, 0.25f);
            Physics2D.SyncTransforms(); plate.Refresh(); return plate;
        }

        private InteractiveDoor Door()
        {
            var door = Spawn(Props + "door1.prefab", new Vector2(6, 0)).GetComponent<InteractiveDoor>();
            var blocker = door.GetComponent<BoxCollider2D>(); blocker.size = new Vector2(0.4f, 3); blocker.offset = Vector2.zero;
            door.interactionArea.size = new Vector2(2, 4); door.interactionArea.offset = Vector2.zero;
            door.player = player; Physics2D.SyncTransforms(); return door;
        }

        private ShadowActor Shadow(Vector2 position, ShadowKind kind = ShadowKind.Stay)
        {
            Assert.IsTrue(manager.TrySpawn(kind, position, 1));
            foreach (var shadow in manager.Actors)
                if (shadow != null && Vector2.Distance(shadow.Body.position, position) < 0.1f) return shadow;
            Assert.Fail("Expected shadow near spawn position."); return null;
        }

        private static void Move(PlayerController2D actor, Vector2 position)
        { actor.Body.position = position; actor.Body.velocity = Vector2.zero; Physics2D.SyncTransforms(); }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var shadow in manager.Actors) if (shadow != null) Object.DestroyImmediate(shadow.gameObject);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); Physics2D.simulationMode = previousMode; Time.timeScale = 1;
            yield return null;
        }

        [Test] public void PlateAcceptsOnlyShadowFeetAndIgnoresSideOrAscendingContacts()
        {
            var plate = Plate(0, PressurePlateMode.Sustained);
            Move(player, new Vector2(0, 0.865f)); plate.Refresh(); Assert.IsFalse(plate.IsActive);
            Move(player, new Vector2(-8, 10));
            var shadow = Shadow(new Vector2(0, 0.865f));
            Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsActive);
            Assert.AreSame(plate.activeSprite, plate.GetComponent<SpriteRenderer>().sprite);
            shadow.Body.position = new Vector2(1.3f, 0.865f); Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsFalse(plate.IsActive);
            shadow.Body.position = new Vector2(0, 0.865f); shadow.Body.velocity = Vector2.up * 5;
            Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsFalse(plate.IsActive);
        }

        [Test] public void SustainedPlateRemainsActiveUntilLastShadowLeavesOrIsDeleted()
        {
            var plate = Plate(0, PressurePlateMode.Sustained);
            var first = Shadow(new Vector2(-0.5f, 0.865f));
            Shadow(new Vector2(0.5f, 0.865f));
            Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsActive);
            Object.DestroyImmediate(first.gameObject); Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsActive);
            manager.ClearAll(); Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsFalse(plate.IsActive);
            Assert.AreSame(plate.closedSprite, plate.GetComponent<SpriteRenderer>().sprite);
        }

        [Test] public void LatchedPlateStaysActiveAfterShadowDeletion()
        {
            var plate = Plate(0, PressurePlateMode.Latched);
            Shadow(new Vector2(0, 0.865f)); Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsTrue(plate.IsActive);
            manager.ClearAll(); Physics2D.SyncTransforms(); plate.Refresh(); Assert.IsTrue(plate.IsActive);
        }

        [Test] public void SavedPlatePrefabSupportsShadowOnCentralButtonSurface()
        {
            var plate = Spawn(Props + "PressurePlate2.prefab", Vector2.zero).GetComponent<ShadowPressurePlate>();
            Physics2D.SyncTransforms();
            var support = plate.GetComponent<BoxCollider2D>();
            var surface = support.bounds;
            var shadow = Shadow(new Vector2(surface.center.x, surface.max.y + 0.715f));
            Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsTrue(plate.IsActive);
            Assert.AreEqual(surface.max.y + 0.015f, shadow.Shape.bounds.min.y, 0.03f);
            Assert.Less(support.size.x, plate.closedSprite.bounds.size.x);
            Assert.Less(support.size.y, plate.closedSprite.bounds.size.y);
        }

        [Test] public void DoorCombinesMultiplePlatesAndDoesNotTreatDisabledPlateAsSatisfied()
        {
            var door = Door();
            var first = Plate(0, PressurePlateMode.Latched, door);
            var second = Plate(3, PressurePlateMode.Sustained, door);
            Assert.IsFalse(door.IsUnlocked);
            Shadow(new Vector2(0, 0.865f)); Physics2D.SyncTransforms(); first.Refresh();
            Assert.IsFalse(door.IsUnlocked);
            door.requirementMode = DoorRequirementMode.Any; Assert.IsTrue(door.IsUnlocked);
            door.requirementMode = DoorRequirementMode.All;
            Shadow(new Vector2(3, 0.865f)); Physics2D.SyncTransforms(); second.Refresh(); Assert.IsTrue(door.IsUnlocked);
            second.enabled = false; Assert.IsFalse(door.IsUnlocked);
            second.enabled = true; second.Refresh(); Assert.IsTrue(door.IsUnlocked);
            manager.ClearAll(); Physics2D.SyncTransforms(); first.Refresh(); second.Refresh(); Assert.IsFalse(door.IsUnlocked);
        }

        [Test] public void OnlyNearbyEnabledPlayerCanPassUnlockedDoorAndPassCancelsTrace()
        {
            var door = Door(); var plate = Plate(0, PressurePlateMode.Sustained, door);
            Move(player, new Vector2(5, 0));
            Assert.IsFalse(door.TryPass(player));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(door.GetComponent<BoxCollider2D>(), player.Shape));
            Move(player, new Vector2(-8, 10));
            Shadow(new Vector2(0, 0.865f)); Physics2D.SyncTransforms(); plate.Refresh();
            Assert.IsTrue(door.IsUnlocked); Assert.IsFalse(door.TryPass(player));
            Move(player, new Vector2(5, 0));
            ability.UseTrace(); Assert.AreEqual(TracePhase.Preparing, ability.Phase);
            player.enabled = false; Assert.IsFalse(door.TryPass(player)); player.enabled = true;
            Assert.IsTrue(door.TryPass(player));
            Assert.AreEqual(0, manager.Count); Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.IsFalse(plate.IsActive); Assert.IsFalse(door.IsUnlocked);
            Assert.AreSame(door.closedSprite, door.GetComponent<SpriteRenderer>().sprite);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(door.GetComponent<BoxCollider2D>(), player.Shape));
            Assert.IsTrue(door.PassageGranted); Assert.IsFalse(door.TryPass(player));
            Move(player, new Vector2(6.3f, 0));
            typeof(InteractiveDoor).GetMethod("Update", Private).Invoke(door, null);
            Assert.IsTrue(door.PassageGranted); // condition lost while player is still inside the doorway
            Move(player, new Vector2(8, 0));
            typeof(InteractiveDoor).GetMethod("Update", Private).Invoke(door, null);
            Assert.IsFalse(door.PassageGranted);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(door.GetComponent<BoxCollider2D>(), player.Shape));
        }

        [Test] public void PassingCancelsRecordingAndLatchedDoorRemainsVisuallyOpen()
        {
            var door = Door(); var plate = Plate(0, PressurePlateMode.Latched, door);
            Shadow(new Vector2(0, 0.865f)); Physics2D.SyncTransforms(); plate.Refresh();
            Move(player, new Vector2(5, 0));
            ability.preparationDuration = 0.01f; ability.UseTrace();
            typeof(ShadowTraceAbility).GetMethod("FixedUpdate", Private).Invoke(ability, null);
            Assert.AreEqual(TracePhase.Recording, ability.Phase);
            Assert.IsTrue(door.TryPass(player));
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.IsTrue(door.IsUnlocked); Assert.AreSame(door.openSprite, door.GetComponent<SpriteRenderer>().sprite);
        }

        [UnityTest] public IEnumerator LampIgnoresPlayerAndStaysLitAfterShadowDisappears()
        {
            var lamp = Spawn(Props + "light2.prefab", Vector2.zero).GetComponent<ShadowLamp>();
            var trigger = lamp.GetComponent<BoxCollider2D>(); trigger.size = Vector2.one; trigger.offset = Vector2.zero;
            Move(player, Vector2.zero); Physics2D.Simulate(0.02f); yield return null;
            Assert.IsFalse(lamp.IsLit); Assert.AreSame(lamp.unlitSprite, lamp.GetComponent<SpriteRenderer>().sprite);
            Move(player, new Vector2(-8, 10)); Shadow(Vector2.zero, ShadowKind.Follow);
            Physics2D.SyncTransforms(); Physics2D.Simulate(0.02f); yield return null;
            Assert.IsTrue(lamp.IsLit); Assert.AreSame(lamp.litSprite, lamp.GetComponent<SpriteRenderer>().sprite);
            manager.ClearAll(); yield return null; Assert.IsTrue(lamp.IsLit);
        }

        [UnityTest] public IEnumerator SpikeIgnoresShadowButPlayerDiesAndEffectFinishesWhilePaused()
        {
            var spike = Spawn(Props + "spike1.prefab", Vector2.zero).GetComponent<SpikeHazard>();
            var trigger = spike.GetComponent<BoxCollider2D>(); trigger.size = Vector2.one; trigger.offset = Vector2.zero;
            var shadow = Shadow(Vector2.zero, ShadowKind.Follow);
            Physics2D.SyncTransforms(); Physics2D.Simulate(0.02f); yield return null;
            Assert.IsNotNull(shadow); Assert.AreEqual(1, manager.Count); Assert.AreEqual(1, Time.timeScale);
            var life = player.GetComponent<PlayerLife>(); life.effectDuration = 0.08f;
            ability.UseTrace(); Move(player, Vector2.zero);
            Physics2D.Simulate(0.02f); yield return null;
            Assert.IsTrue(life.IsDead); Assert.AreEqual(0, Time.timeScale);
            Assert.IsFalse(player.enabled); Assert.IsFalse(player.Body.simulated); Assert.IsFalse(manager.enabled);
            Assert.AreEqual(TracePhase.Idle, ability.Phase);
            Assert.IsFalse(player.GetComponent<ActorAnimationDriver>().enabled);
            Assert.AreEqual(1, manager.Count, "Death pauses the scene without clearing existing shadows.");
            ability.UseTrace(); Assert.AreEqual(TracePhase.Idle, ability.Phase);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(0, player.GetComponentInChildren<SpriteRenderer>().color.a, 0.001f);
            Object.DestroyImmediate(life); Assert.AreEqual(1, Time.timeScale);
        }
    }
}
#endif
