using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class TechWiseAssemblyPolishBuild
{
    public static void BuildQuest()
    {
        Directory.CreateDirectory("Builds/AssemblyPolish"); Directory.CreateDirectory("Logs/AssemblyPolish");
        var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
        var preloaded = PlayerSettings.GetPreloadedAssets(); bool bundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" },
                locationPathName = "Builds/AssemblyPolish/TechWise360.apk", target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText("Logs/AssemblyPolish/build-summary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Assembly polish APK build failed");
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = bundle; PlayerSettings.SetPreloadedAssets(preloaded);
            File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings);
        }
    }
}
