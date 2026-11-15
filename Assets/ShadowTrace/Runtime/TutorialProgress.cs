using System;
using System.IO;
using UnityEngine;

namespace ShadowTrace
{
    public enum TutorialStep
    {
        Introduction, Stay, DeleteOne, Follow, FollowPractice, Avoid, AvoidObserve,
        ClearAll, Approach, Climb, ApproachSpikes, ObserveSpikes, Sprint, Door,
        Lamp, Resume, Zoom, Finish, Complete
    }

    [Serializable]
    public sealed class TutorialProgress
    {
        public int version = 1;
        public TutorialStep step;
        public string scenePath;
        public string checkpointId;
        public Vector3 practicePosition;
        public bool hasPracticePosition;

        public static string FilePath => Path.Combine(Path.GetDirectoryName(SaveGameService.Current.FilePath), "tutorial.json");

        public static TutorialProgress Read()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<TutorialProgress>(File.ReadAllText(FilePath));
                    if (data != null && data.version == 1 && Enum.IsDefined(typeof(TutorialStep), data.step)) return data;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { }
            return new TutorialProgress();
        }

        public bool Write()
        {
            string temporary = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(temporary, JsonUtility.ToJson(this));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("Could not save tutorial progress: " + error.Message); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }
        }

        // A scene reload cannot restore transient practice actors.
        public static TutorialStep RestartableStep(TutorialStep step)
        {
            if (step == TutorialStep.DeleteOne) return TutorialStep.Stay;
            if (step == TutorialStep.FollowPractice) return TutorialStep.Follow;
            if (step == TutorialStep.AvoidObserve || step == TutorialStep.ClearAll) return TutorialStep.Avoid;
            if (step == TutorialStep.Climb) return TutorialStep.Approach;
            if (step == TutorialStep.ObserveSpikes) return TutorialStep.ApproachSpikes;
            return step;
        }
    }
}
