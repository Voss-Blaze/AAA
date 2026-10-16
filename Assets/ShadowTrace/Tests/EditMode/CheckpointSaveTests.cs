using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShadowTrace.Tests
{
    public sealed class CheckpointSaveTests
    {
        private string directory;
        private SaveGameService saves;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Application.dataPath, "../Temp/CheckpointTests/" + Guid.NewGuid().ToString("N"));
            saves = new SaveGameService(directory);
        }

        [TearDown] public void TearDown()
        {
            saves.BeginNewGame();
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }

        private static SaveGameService.Checkpoint Data(string id)
        {
            return new SaveGameService.Checkpoint { scenePath = "Assets/Scenes/Level 1.unity", checkpointId = id, position = new Vector3(2, 3, 0) };
        }

        [Test] public void SaveSurvivesServiceRestartAndOverwriteKeepsOneFile()
        {
            Assert.IsTrue(saves.TrySave(Data("first")));
            saves = new SaveGameService(directory);
            Assert.IsTrue(saves.TryRead(out var first)); Assert.AreEqual("first", first.checkpointId);
            Assert.AreEqual(new Vector3(2, 3, 0), first.position);
            Assert.IsTrue(saves.TrySave(Data("second")));
            Assert.IsTrue(new SaveGameService(directory).TryRead(out var second));
            Assert.AreEqual("second", second.checkpointId);
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }

        [Test] public void MissingCorruptAndUnsupportedSavesCannotResume()
        {
            Assert.IsFalse(saves.BeginResume("Assets/Scenes/Level 1.unity"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(saves.FilePath, "{broken");
            Assert.IsFalse(saves.TryRead(out _));
            var data = Data("id"); data.version = 99;
            File.WriteAllText(saves.FilePath, JsonUtility.ToJson(data));
            Assert.IsFalse(saves.TryRead(out _));
            data.version = 1; data.position = new Vector3(float.NaN, 0, 0);
            Assert.IsFalse(saves.TrySave(data));
        }

        [Test] public void NewGameClearsSaveAndResumeRequestIsConsumedOnlyOnce()
        {
            saves.TrySave(Data("id"));
            Assert.IsFalse(saves.BeginResume("another scene"));
            Assert.IsTrue(saves.BeginResume("Assets/Scenes/Level 1.unity"));
            Assert.IsTrue(saves.ConsumeResume("Assets/Scenes/Level 1.unity", out _));
            Assert.IsFalse(saves.ConsumeResume("Assets/Scenes/Level 1.unity", out _));
            Assert.IsTrue(saves.BeginResume("Assets/Scenes/Level 1.unity"));
            Assert.IsTrue(saves.BeginNewGame());
            Assert.IsFalse(saves.TryRead(out _));
            Assert.IsFalse(saves.ConsumeResume("Assets/Scenes/Level 1.unity", out _));
        }
    }
}
