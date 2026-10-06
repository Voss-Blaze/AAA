#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class DemoSceneTests
    {
        private Scene previousScene;
        private Scene demoScene;

        [UnityTest] public IEnumerator DemoSceneStartsWithWiredPlayerCameraAndHUD()
        {
            previousScene = SceneManager.GetActiveScene();
            SceneManager.sceneLoaded += OnDemoLoaded;
            demoScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/ShadowTrace/Scenes/ShadowTraceDemo.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => demoScene.isLoaded);
            yield return new WaitForSeconds(0.4f);
            var player = Object.FindObjectOfType<PlayerController2D>();
            var manager = Object.FindObjectOfType<ShadowManager>();
            var hud = Object.FindObjectOfType<ShadowTraceHUD>();
            Assert.IsNotNull(player); Assert.IsNotNull(manager); Assert.IsNotNull(hud);
            Assert.AreSame(player, manager.player);
            Assert.IsNotNull(manager.shadowPrefab);
            Assert.AreSame(manager, player.GetComponent<ShadowTraceAbility>().manager);
            Assert.AreSame(manager, hud.manager);
            Assert.IsNotNull(Camera.main);
            Assert.IsTrue(Camera.main.orthographic);
            Assert.IsTrue(player.IsGrounded);
            Assert.IsNotNull(Object.FindObjectOfType<PushableBody2D>());
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= OnDemoLoaded;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (demoScene.IsValid() && demoScene.isLoaded) yield return SceneManager.UnloadSceneAsync(demoScene);
        }

        private void OnDemoLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "ShadowTraceDemo") return;
            demoScene = scene;
            SceneManager.SetActiveScene(scene);
        }
    }
}
#endif
