using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class TechWiseCompetitionMonitorVerification
{
    public static void BuildQuest()
    {
        Directory.CreateDirectory("Builds/CompetitionMonitor"); Directory.CreateDirectory("Logs/CompetitionMonitor");
        var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
        var preloaded = PlayerSettings.GetPreloadedAssets();
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" },
                locationPathName = "Builds/CompetitionMonitor/TechWise360.apk", target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText("Logs/CompetitionMonitor/build-summary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Competition monitor APK build failed");
        }
        finally { PlayerSettings.SetPreloadedAssets(preloaded); File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings); }
    }
    sealed class MonitorProbe
    {
        readonly TechWiseCompetitionMonitor monitor;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public MonitorProbe(TechWiseCompetitionMonitor value) => monitor = value;
        public string State => typeof(TechWiseCompetitionMonitor).GetProperty("State", Flags).GetValue(monitor).ToString();
        public System.Collections.Generic.IReadOnlyList<string> Errors => (System.Collections.Generic.IReadOnlyList<string>)typeof(TechWiseCompetitionMonitor).GetProperty("Errors", Flags).GetValue(monitor);
        public void Press() => typeof(TechWiseCompetitionMonitor).GetMethod("Press", Flags).Invoke(monitor, null);
        public void SetAssessment(TechWiseVrComponentResult[] values, string error) => typeof(TechWiseCompetitionMonitor).GetMethod("SetAssessment", Flags).Invoke(monitor, new object[] { values, error });
    }
    const string Key = "TechWise.Monitor.Verification";
    static IEnumerator flow;
    static int frame = -1, checks;
    static double deadline;
    static readonly string[] Prefs = { TechWiseSimulationModeManager.GameModeKey, TechWiseSimulationModeManager.SimulationTypeKey };
    public static void Run()
    {
        foreach (var key in Prefs) { SessionState.SetBool(Key + key, PlayerPrefs.HasKey(key)); SessionState.SetString(Key + key + "value", PlayerPrefs.GetString(key)); }
        TechWiseSimulationModeManager.SetMode("assembly", "competition");
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            TechWiseAttemptRecorder.LocalVerificationMode = true;
            Directory.CreateDirectory("Logs/CompetitionMonitor");
            TechWiseOfflineAttemptQueue.VerificationQueuePath = Path.GetFullPath("Logs/CompetitionMonitor/test-queue.json");
            var queue = TechWiseOfflineAttemptQueue.EnsureInstance(); queue.StopAllCoroutines(); queue.enabled = false;
            foreach (var interactor in UnityEngine.Object.FindObjectsByType<XRBaseInteractor>()) if (interactor is not XRSocketInteractor) interactor.enabled = false;
            deadline = EditorApplication.timeSinceStartup + 180; flow = Scenarios(); EditorApplication.update += Tick;
        };
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Monitor verification timed out");
            EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            if (frame == Time.frameCount) return; frame = Time.frameCount;
            if (!flow.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Check(bool condition, string description)
    { if (!condition) throw new Exception(description); checks++; Debug.Log("MONITOR_PASS " + description); }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Key, false);
        foreach (var key in Prefs) if (SessionState.GetBool(Key + key, false)) PlayerPrefs.SetString(key, SessionState.GetString(Key + key + "value", "")); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save(); Directory.CreateDirectory("Logs/CompetitionMonitor");
        File.WriteAllText("Logs/CompetitionMonitor/results.txt", error == null ? $"PASS: {checks} local monitor checks. Physical VR interaction not tested." : error.ToString());
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static IEnumerator Scenarios()
    {
        for (int i = 0; i < 30; i++) yield return null;
        var monitor = new MonitorProbe(UnityEngine.Object.FindAnyObjectByType<TechWiseCompetitionMonitor>());
        Check(GameObject.Find("Competition verification monitor") != null, "Monitor created beside competition PC");
        Check(GameObject.Find("Monitor cable to PC unit").GetComponent<LineRenderer>().positionCount == 5, "Visible cable connects monitor and case");
        Check(GameObject.Find("Verification display").GetComponent<TrackedDeviceGraphicRaycaster>() != null, "Existing tracked UI raycaster used");
        monitor.Press(); Check(monitor.State == "Waiting", "Done cannot submit before competition start");
        var recorder = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        typeof(TechWiseAttemptRecorder).GetMethod("BeginAttempt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder, null);
        monitor.Press();
        TechWiseAttemptRecorder.TryGetSnapshot(out var submitted);
        Check(submitted.resultsRevealed && TechWiseSimulationRuntime.Instance.ManipulationLocked, "Monitor DONE uses existing submission and locks workbench");
        Check(monitor.State == "Ready" && monitor.Errors.Count > 0, "Incomplete captured assembly prepares test with component errors");
        monitor.Press(); Check(monitor.State == "Checking", "Test starts POST sequence");
        while (monitor.State == "Checking") yield return null;
        Check(monitor.State == "Error", "Missing parts fail startup"); Capture("errors.png");
        var complete = TechWiseSimulationModeManager.AssemblyOrder.Select(step => new TechWiseVrComponentResult { step = step, component = step, complete = true }).ToArray();
        monitor.SetAssessment(complete, null); monitor.Press();
        complete[0].complete = false;
        while (monitor.State == "Checking") yield return null;
        Check(monitor.State == "Desktop", "Complete frozen assessment boots to desktop"); Capture("desktop.png");
        monitor.Press(); monitor.Press();
        Check(monitor.State == "Checking", "Repeated press cannot duplicate boot");
        while (monitor.State == "Checking") yield return null;
        monitor.SetAssessment(null, "Missing target"); Check(monitor.Errors.Count == 9, "Missing assessment and configuration fail closed");
        TechWiseSimulationModeManager.SetMode("assembly", "practice"); yield return null; yield return null;
        Check(GameObject.Find("Competition verification monitor") == null, "Monitor removed outside Competition");
        TechWiseSimulationModeManager.SetMode("disassembly", "competition"); yield return null;
        Check(!(bool)typeof(TechWiseCompetitionMonitor).GetProperty("Applies", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), "Disassembly competition unchanged");
    }
    static void Capture(string name)
    {
        var panel = GameObject.Find("Verification display").transform;
        var obj = new GameObject("Monitor QA camera"); var camera = obj.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(panel.position - panel.forward * 1.2f, panel.rotation); camera.nearClipPlane = .01f; camera.fieldOfView = 38;
        if (Camera.main != null) camera.cullingMask = Camera.main.cullingMask;
        var rt = new RenderTexture(1200, 800, 24); camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); tex.Apply(); Directory.CreateDirectory("Logs/CompetitionMonitor"); File.WriteAllBytes("Logs/CompetitionMonitor/" + name, tex.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(obj);
    }
}
