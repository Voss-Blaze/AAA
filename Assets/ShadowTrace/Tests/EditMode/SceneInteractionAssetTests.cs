using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowTrace.Tests
{
    public sealed class SceneInteractionAssetTests
    {
        private const string Props = "Assets/Art/environment/en_elements/";
        private static GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Props + name + ".prefab");

        [Test] public void SwitchesUseTheConfirmedSpritePairsAndSeparateTriggerFromSolidBody()
        {
            var door = Prop("door1").GetComponent<InteractiveDoor>();
            Assert.AreSame(Prop("door1").GetComponent<SpriteRenderer>().sprite, door.closedSprite);
            Assert.AreSame(Prop("door2").GetComponent<SpriteRenderer>().sprite, door.openSprite);
            Assert.IsFalse(door.GetComponent<BoxCollider2D>().isTrigger); Assert.IsTrue(door.interactionArea.isTrigger);
            var plate = Prop("PressurePlate2").GetComponent<ShadowPressurePlate>();
            Assert.AreSame(Prop("PressurePlate1").GetComponent<SpriteRenderer>().sprite, plate.closedSprite);
            Assert.AreSame(Prop("PressurePlate2").GetComponent<SpriteRenderer>().sprite, plate.activeSprite);
            Assert.IsFalse(plate.GetComponent<BoxCollider2D>().isTrigger); Assert.IsTrue(plate.pressArea.isTrigger);
            var lamp = Prop("light2").GetComponent<ShadowLamp>();
            Assert.AreSame(Prop("light1").GetComponent<SpriteRenderer>().sprite, lamp.unlitSprite);
            Assert.AreSame(Prop("light2").GetComponent<SpriteRenderer>().sprite, lamp.litSprite);
            Assert.IsTrue(lamp.GetComponent<BoxCollider2D>().isTrigger);
            Assert.IsTrue(Prop("spike1").GetComponent<SpikeHazard>().GetComponent<BoxCollider2D>().isTrigger);
        }

        [Test] public void SampleSceneHasWiredDeathDoorAndPlateWithoutDuplicateComponents()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var player = all.Select(item => item.GetComponent<PlayerController2D>()).Single(item => item != null);
                Assert.AreEqual(1, player.GetComponents<PlayerLife>().Length);
                var door = all.Select(item => item.GetComponent<InteractiveDoor>()).Single(item => item != null);
                var plate = all.Select(item => item.GetComponent<ShadowPressurePlate>()).Single(item => item != null);
                Assert.AreSame(player, door.player); Assert.AreSame(door, plate.targetDoor);
                Assert.AreEqual(DoorRequirementMode.All, door.requirementMode);
                Assert.AreEqual(PressurePlateMode.Sustained, plate.mode);
                Assert.AreEqual(1, door.GetComponents<InteractiveDoor>().Length);
                Assert.AreEqual(1, plate.GetComponents<ShadowPressurePlate>().Length);
                var hud = all.Select(item => item.GetComponent<ShadowTraceHUD>()).Single(item => item != null);
                Assert.AreSame(player.GetComponent<PlayerLife>(), hud.life); CollectionAssert.Contains(hud.doors, door);
                Assert.AreSame(door.closedSprite, door.GetComponent<SpriteRenderer>().sprite);
                Assert.AreSame(plate.closedSprite, plate.GetComponent<SpriteRenderer>().sprite);
            }
            finally { if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
