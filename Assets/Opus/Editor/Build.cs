using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Opus.Editor
{
    public static class Build
    {
        public const string WindowsOutput = "Builds/Windows/CopycatOpus.exe";

        [MenuItem("Opus/3 Build the Windows exe")]
        public static void Windows() => BatchUtil.Run("Windows build", () =>
        {
            Setup.GenerateContentInternal();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Setup.ScenePath },
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {summary.result}, {summary.totalErrors} errors.");

            Debug.Log($"[Opus] Exe ready: {Path.GetFullPath(WindowsOutput)} " +
                      $"({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0} s)");
        });
    }
}
