using System;
using UnityEditor;
using UnityEngine;

namespace Opus.Editor
{
    /// <summary>
    /// For -executeMethod entry points: on an error it exits with code 1 in batchmode,
    /// so the tools/*.ps1 scripts catch the failure.
    /// </summary>
    static class BatchUtil
    {
        public static void Run(string label, Action action)
        {
            try
            {
                action();
                Debug.Log($"[Opus] {label}: done");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Opus] {label}: ERROR\n{exception}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }
    }
}
