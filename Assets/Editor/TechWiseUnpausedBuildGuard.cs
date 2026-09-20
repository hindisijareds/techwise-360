using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Applies to every build entry point, including the Editor Build button.
public sealed class TechWiseUnpausedBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        var settings = File.ReadAllText("ProjectSettings/TimeManager.asset");
        if (TechWisePauseSession.Active || !Mathf.Approximately(Time.timeScale, 1f) ||
            !Regex.IsMatch(settings, @"(?m)^\s*m_TimeScale:\s*1(?:\.0+)?\s*$"))
            throw new BuildFailedException("Build stopped: simulation timing is paused. Close the pause diagnostic and set Project Settings > Time > Time Scale to 1 before building.");
    }
}
