using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class BuildRecoveryProbe
{
    [InitializeOnLoadMethod]
    private static void VerifyReload()
    {
        EditorApplication.delayCall += () =>
        {
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/build-recovery-editor-ready.txt", DateTime.UtcNow.ToString("O"));
            Debug.Log("BUILD_RECOVERY_EDITOR_READY");
        };
    }
}
