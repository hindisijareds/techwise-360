using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class TechWiseBatch1Build
{
    public static void Desktop() => Build(BuildTarget.StandaloneWindows64, "Builds/Batch1/Desktop/TechWise360.exe");
    public static void Quest() => Build(BuildTarget.Android, "Builds/Batch1/Quest/TechWise360.apk");

    static void Build(BuildTarget target, string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var version = PlayerSettings.bundleVersion;
        var versionCode = PlayerSettings.Android.bundleVersionCode;
        var preloadedAssets = PlayerSettings.GetPreloadedAssets();
        var il2CppArgs = PlayerSettings.GetAdditionalIl2CppArgs();
        var codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Android);
        try
        {
            var scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" };
            foreach (var scene in scenes)
                if (!File.Exists(scene)) throw new Exception($"Required scene missing: {scene}");
            Directory.CreateDirectory("Logs/Quest2");
            File.WriteAllLines("Logs/Quest2/scenes.txt", scenes);
            if (target == BuildTarget.Android)
            {
                TechWiseQuestMenuVerification.RepairMainMenuScene();
                TechWiseQuestMenuVerification.RepairGameplayScenes();
                TechWiseQuestMenuVerification.Verify();
            }
            // Allow memory-constrained machines to limit compiler concurrency.
            if (target == BuildTarget.Android &&
                int.TryParse(Environment.GetEnvironmentVariable("TECHWISE_BUILD_JOBS"), out var jobs) && jobs > 0)
            {
                PlayerSettings.SetAdditionalIl2CppArgs($"{il2CppArgs} --jobs={jobs}");
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output, target = target, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/Batch1");
            File.WriteAllText($"Logs/Batch1/build-{target}.txt",
                $"{report.summary.result}\nOutput: {output}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception($"Batch 1 {target} build failed. Inspect build log.");
        }
        finally
        {
            // Development builds must not retain changes made by build preprocessors.
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.SetPreloadedAssets(preloadedAssets);
            PlayerSettings.SetAdditionalIl2CppArgs(il2CppArgs);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, codeGeneration);
        }
    }
}
