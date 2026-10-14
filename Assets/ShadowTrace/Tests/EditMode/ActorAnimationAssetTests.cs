using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowTrace.Tests
{
    public sealed class ActorAnimationAssetTests
    {
        private const string ControllerPath = "Assets/Animation/Player.controller";

        [Test] public void ControllerHasFiveExclusiveImmediateTransitions()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller);
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual("MotionState", controller.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Int, controller.parameters[0].type);
            var machine = controller.layers[0].stateMachine;
            Assert.AreEqual("Idle", machine.defaultState.name);
            Assert.AreEqual(5, machine.states.Length);
            Assert.AreEqual(5, machine.anyStateTransitions.Length);
            foreach (ActorMotionState state in System.Enum.GetValues(typeof(ActorMotionState)))
            {
                var transition = machine.anyStateTransitions.Single(item => item.destinationState.name == state.ToString());
                Assert.IsFalse(transition.hasExitTime); Assert.IsFalse(transition.canTransitionToSelf);
                Assert.AreEqual(0, transition.duration);
                Assert.AreEqual(1, transition.conditions.Length);
                Assert.AreEqual(AnimatorConditionMode.Equals, transition.conditions[0].mode);
                Assert.AreEqual((int)state, transition.conditions[0].threshold);
                Assert.AreEqual("MotionState", transition.conditions[0].parameter);
                var clip = (AnimationClip)transition.destinationState.motion;
                Assert.IsNotNull(clip);
                Assert.AreEqual(state == ActorMotionState.Idle || state == ActorMotionState.Walk || state == ActorMotionState.Run,
                    AnimationUtility.GetAnimationClipSettings(clip).loopTime);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach (var frame in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    {
                        var sprite = frame.value as Sprite;
                        Assert.IsNotNull(sprite);
                        Assert.GreaterOrEqual(sprite.bounds.size.x, 0.1f);
                        Assert.GreaterOrEqual(sprite.bounds.size.y, 0.5f);
                        Assert.AreEqual(0, sprite.pivot.y, 0.01f, "Character frames must keep their feet anchored.");
                    }
            }
        }

        [TestCase("Player")] [TestCase("Shadow")] [TestCase("SampleShadow")]
        public void PrefabsShareControllerAndKeepAnimationSeparateFromPhysics(string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShadowTrace/Prefabs/" + name + ".prefab");
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.GetComponent<ActorAnimationDriver>());
            Assert.IsNull(root.GetComponent<Animator>());
            Assert.IsNull(root.GetComponent<SpriteRenderer>());
            Assert.AreEqual(1, root.GetComponentsInChildren<Animator>(true).Length);
            var animator = root.GetComponentInChildren<Animator>(true);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath), animator.runtimeAnimatorController);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(Vector2.zero, root.GetComponent<BoxCollider2D>().offset);
            if (name != "SampleShadow") Assert.AreEqual(new Vector2(0.8f, 1.4f), root.GetComponent<BoxCollider2D>().size);
        }
    }
}
