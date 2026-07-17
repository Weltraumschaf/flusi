using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Flusi.EditorTools
{
    /// CLI-drivable release builds. Invoke via `-executeMethod
    /// Flusi.EditorTools.BuildScript.BuildMac` (or BuildLinux) in batch mode;
    /// see README.md for the full command. Not shipped in builds (Editor-only
    /// assembly).
    public static class BuildScript
    {
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/flusi.app");

        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/flusi");

        private static void Build(BuildTarget target, string locationPathName)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] {target} build succeeded: {locationPathName}");
                return;
            }

            Debug.LogError($"[BuildScript] {target} build failed with result {report.summary.result}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
