using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShadowTrace.Tests
{
    public sealed class TutorialProgressTests
    {
        private SaveGameService previous;
        private string directory;

        [SetUp] public void SetUp()
        {
            previous = SaveGameService.Current;
            directory = Path.Combine(Application.dataPath, "../Temp/TutorialProgressTests/" + System.Guid.NewGuid().ToString("N"));
            SaveGameService.Current = new SaveGameService(directory);
        }

        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            SaveGameService.Current = previous;
        }

        [Test] public void ProgressSurvivesServiceRestartAndNewGameWithoutTouchingCheckpoint()
        {
            Assert.IsTrue(SaveGameService.Current.TrySave(new SaveGameService.Checkpoint
            { scenePath = "level", checkpointId = "lamp", position = Vector3.zero }));
            var progress = new TutorialProgress { scenePath = "level", checkpointId = "lamp", step = TutorialStep.Resume };
            Assert.IsTrue(progress.Write());
            SaveGameService.Current = new SaveGameService(directory);
            Assert.AreEqual(TutorialStep.Resume, TutorialProgress.Read().step);
            Assert.IsTrue(SaveGameService.Current.TryRead(out _));
            Assert.IsTrue(SaveGameService.Current.BeginNewGame());
            Assert.AreEqual(TutorialStep.Resume, TutorialProgress.Read().step);
            Assert.IsFalse(SaveGameService.Current.TryRead(out _));
        }

        [Test] public void CorruptUnsupportedAndUnknownStepsRestartSafely()
        {
            Directory.CreateDirectory(directory);
            foreach (var json in new[] { "{broken", "{\"version\":99,\"step\":1}", "{\"version\":1,\"step\":999}" })
            {
                File.WriteAllText(TutorialProgress.FilePath, json);
                Assert.AreEqual(TutorialStep.Introduction, TutorialProgress.Read().step);
            }
        }

        [TestCase(TutorialStep.DeleteOne, TutorialStep.Stay)]
        [TestCase(TutorialStep.FollowPractice, TutorialStep.Follow)]
        [TestCase(TutorialStep.AvoidObserve, TutorialStep.Avoid)]
        [TestCase(TutorialStep.Climb, TutorialStep.Approach)]
        [TestCase(TutorialStep.ObserveSpikes, TutorialStep.ApproachSpikes)]
        [TestCase(TutorialStep.Sprint, TutorialStep.Sprint)]
        [TestCase(TutorialStep.Complete, TutorialStep.Complete)]
        public void ReloadRestartsOnlyTheCurrentTransientExercise(TutorialStep saved, TutorialStep expected)
        {
            Assert.AreEqual(expected, TutorialProgress.RestartableStep(saved));
        }
    }
}
