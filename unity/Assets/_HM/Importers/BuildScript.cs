using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HM.Importers
{
    /// <summary>Standalone macOS Apple Silicon build. Output goes to unity/Build/ (git-ignored).</summary>
    public static class BuildScript
    {
        public const string OutputRelative = "Build/HMUpscale.app";

        [MenuItem("HM/Build/macOS arm64 (development)")]
        public static void BuildMacDevelopmentMenu() => Build(true);

        /// <summary>-executeMethod HM.Importers.BuildScript.BuildMac</summary>
        public static void BuildMac()
        {
            var result = Build(false);
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>-executeMethod HM.Importers.BuildScript.BuildMacDevelopment</summary>
        public static void BuildMacDevelopment()
        {
            var result = Build(true);
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildResult Build(bool development)
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_HM/Scenes/Boot.unity", "Assets/_HM/Scenes/SliceFarm.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"HM build: {report.summary.result} size={report.summary.totalSize / (1024 * 1024)}MB time={report.summary.totalTime} -> {output}");
            return report.summary.result;
        }
    }
}
