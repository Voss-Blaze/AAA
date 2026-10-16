using System;
using System.IO;
using UnityEngine;

namespace ShadowTrace
{
    public sealed class SaveGameService
    {
        [Serializable]
        public sealed class Checkpoint
        {
            public int version = 1;
            public string scenePath;
            public string checkpointId;
            public Vector3 position;
        }

        public static SaveGameService Current { get; set; } = new SaveGameService();
        public string FilePath { get; }
        private Checkpoint pendingResume;
        public bool ShowNewGameHint { get; set; }

        public SaveGameService(string directory = null)
        {
            FilePath = Path.Combine(directory ?? Application.persistentDataPath, "checkpoint.json");
        }

        private static bool Valid(Checkpoint data)
        {
            return data != null && data.version == 1 && !string.IsNullOrEmpty(data.scenePath) &&
                !string.IsNullOrEmpty(data.checkpointId) && Finite(data.position.x) &&
                Finite(data.position.y) && Finite(data.position.z);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public bool TryRead(out Checkpoint data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                var loaded = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(FilePath));
                if (!Valid(loaded)) return false;
                data = loaded;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { return false; }
        }

        public bool TrySave(Checkpoint data)
        {
            if (!Valid(data)) return false;
            string temporary = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(temporary, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("Could not write checkpoint: " + error.Message); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }
        }

        public bool BeginNewGame()
        {
            pendingResume = null;
            ShowNewGameHint = false;
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("Could not clear checkpoint: " + error.Message); return false; }
        }

        public bool BeginResume(string scenePath)
        {
            pendingResume = null;
            if (!TryRead(out var data) || data.scenePath != scenePath) return false;
            pendingResume = data;
            return true;
        }

        public bool ConsumeResume(string scenePath, out Checkpoint data)
        {
            data = pendingResume;
            pendingResume = null;
            return data != null && data.scenePath == scenePath;
        }
    }
}
