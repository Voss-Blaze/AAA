#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowTrace.Tests
{
    public sealed class GameCursorTests
    {
        private SaveGameService previousSaves;
        private Scene previous;
        private GameAudioManager previousAudio;
        private string directory;
        private GameObject testObject;

        [SetUp] public void SetUp()
        {
            previous = SceneManager.GetActiveScene(); previousAudio = GameAudioManager.Instance;
            previousSaves = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/CursorTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory); Time.timeScale = 1;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (testObject != null) Object.Destroy(testObject);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Cursor cleanup"));
            foreach (var path in new[] { "Assets/Scenes/Log.unity", "Assets/Scenes/Level 1.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previous) yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (previousAudio == null && GameAudioManager.Instance != null) Object.Destroy(GameAudioManager.Instance.gameObject);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            SaveGameService.Current.BeginNewGame(); if (Directory.Exists(directory)) Directory.Delete(directory);
            SaveGameService.Current = previousSaves; Time.timeScale = 1; yield return null;
        }

        [Test] public void CursorAssetAndNativeCursorUseReadableTextureWithTipHotspot()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Cursor.png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Cursor.png");
            Assert.AreEqual(TextureImporterType.Cursor, importer.textureType);
            Assert.IsTrue(texture.isReadable); Assert.IsFalse(importer.mipmapEnabled);
            Assert.LessOrEqual(texture.width, 64); Assert.LessOrEqual(texture.height, 64);
            Assert.AreEqual(TextureFormat.RGBA32, texture.format);
            Assert.AreSame(texture, PlayerSettings.defaultCursor);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Game Audio.prefab").GetComponent<GameCursor>();
            Assert.AreSame(texture, prefab.texture); Assert.AreEqual(PlayerSettings.cursorHotspot, prefab.hotspot);
            var point = prefab.hotspot;
            Assert.Greater(texture.GetPixel(Mathf.RoundToInt(point.x), texture.height - 1 - Mathf.RoundToInt(point.y)).a, 0.2f);
            testObject = new GameObject("Native cursor test");
            var controller = testObject.AddComponent<GameCursor>();
            controller.texture = texture; controller.hotspot = point;
            controller.Apply(); Assert.IsTrue(Cursor.visible); Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        }

        [UnityTest] public IEnumerator CustomCursorPersistsAcrossMenuLevelPauseAndReturn()
        {
            var menu = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Log.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return new WaitUntil(() => menu.isLoaded); SceneManager.SetActiveScene(menu); yield return null;
            var cursor = GameAudioManager.Instance.GetComponent<GameCursor>();
            Assert.IsNotNull(cursor); Assert.IsNotNull(cursor.texture); Assert.IsTrue(Cursor.visible);
            var controller = menu.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuController>(true)).Single();
            controller.OpenStart(); controller.ConfirmStart(); yield return null; yield return null;
            Assert.AreEqual("Assets/Scenes/Level 1.unity", SceneManager.GetActiveScene().path);
            Assert.AreSame(cursor, GameAudioManager.Instance.GetComponent<GameCursor>());
            var all = SceneManager.GetActiveScene().GetRootGameObjects();
            all.SelectMany(g => g.GetComponentsInChildren<LevelStoryController>(true)).Single().enabled = false;
            var pause = all.SelectMany(g => g.GetComponentsInChildren<GamePauseController>(true)).Single();
            pause.TogglePause(); Assert.IsTrue(Cursor.visible); pause.ContinueGame();
            all.SelectMany(g => g.GetComponentsInChildren<LevelCheckpointController>(true)).Single().ExitLevel();
            yield return null; yield return null;
            Assert.AreEqual("Assets/Scenes/Log.unity", SceneManager.GetActiveScene().path);
            Assert.AreSame(cursor, GameAudioManager.Instance.GetComponent<GameCursor>());
            Assert.IsTrue(Cursor.visible); Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        }
    }
}
#endif
