using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class TechWisePauseResetVerification
{
    const string Key = "TechWise.PauseReset.Verification";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Prefs = { MainMenu.ControlModeKey, TechWiseSimulationModeManager.GameModeKey,
        TechWiseSimulationModeManager.SimulationTypeKey, TechWiseSimulationModeManager.CompetitionIdKey,
        TechWiseSimulationModeManager.CompetitionTitleKey };
    static readonly List<string> errors = new(), framework = new();
    static IEnumerator flow;
    static int frame = -1, checks;
    static double deadline;
    static TechWiseInGameMenu menu;
    static Canvas canvas;
    public static void Run()
    {
        Directory.CreateDirectory("Logs/PauseReset");
        SessionState.SetBool(Key + "wristHad", PlayerPrefs.HasKey("TechWise360.WristShortcutHidden"));
        SessionState.SetInt(Key + "wristValue", PlayerPrefs.GetInt("TechWise360.WristShortcutHidden"));
        PlayerPrefs.SetInt("TechWise360.WristShortcutHidden", 0);
        foreach (var key in Prefs) { SessionState.SetBool(Key + key, PlayerPrefs.HasKey(key)); SessionState.SetString(Key + key + "value", PlayerPrefs.GetString(key)); }
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
        TechWiseSimulationModeManager.SetMode("assembly", "practice");
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            TechWiseAttemptRecorder.LocalVerificationMode = true;
            TechWiseOfflineAttemptQueue.VerificationQueuePath = Path.GetFullPath("Logs/PauseReset/test-queue.json");
            var queue = TechWiseOfflineAttemptQueue.EnsureInstance(); queue.StopAllCoroutines(); queue.enabled = false;
            Application.logMessageReceived += Log;
            deadline = EditorApplication.timeSinceStartup + 240; flow = Scenarios(); EditorApplication.update += Tick;
        };
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (message.StartsWith("No available indices for interactor registration.") || message.TrimStart().StartsWith("- Slot ")) framework.Add(message);
        else errors.Add(message + "\n" + stack);
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Pause/reset verification timed out");
            EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            if (frame == Time.frameCount) return; frame = Time.frameCount;
            if (!flow.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
        if (menu != null) Call(menu, "SetMenuOpen", false);
        Time.timeScale = 1;
        if (SessionState.GetBool(Key + "wristHad", false)) PlayerPrefs.SetInt("TechWise360.WristShortcutHidden", SessionState.GetInt(Key + "wristValue", 0));
        else PlayerPrefs.DeleteKey("TechWise360.WristShortcutHidden");
        foreach (var key in Prefs) if (SessionState.GetBool(Key + key, false)) PlayerPrefs.SetString(key, SessionState.GetString(Key + key + "value", "")); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        File.WriteAllText("Logs/PauseReset/results.txt", (error == null ? "PASS" : error.ToString()) + $"\nChecks: {checks}\nRuntime errors: {errors.Count}\nKnown XR framework diagnostics: {framework.Count}\nPhysical Quest controller testing not performed.\n" + string.Join("\n", errors) + "\n" + string.Join("\n", framework));
        EditorApplication.Exit(error == null && errors.Count == 0 ? 0 : 1);
    }
    static void Check(bool value, string description) { if (!value) throw new Exception(description); checks++; Debug.Log("PAUSE_PASS " + description); }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    static Button Button(string name) => canvas.GetComponentsInChildren<Button>(true).First(b => b.name == name);
    static void PointAndClick(string name, float side)
    {
        Canvas.ForceUpdateCanvases();
        var button = Button(name);
        var target = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
        var from = Camera.main.transform.position + Camera.main.transform.right * side - Vector3.up * .15f;
        var data = new TrackedDeviceEventData(EventSystem.current) { rayPoints = new List<Vector3> { from, from + (target - from).normalized * 4f }, layerMask = -1, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); canvas.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(data, hits);
        var hit = hits.FirstOrDefault();
        var actual = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Button>() : null;
        Check(actual == button, name + " hit by " + (side < 0 ? "left" : "right") + " tracked UI ray (hit " + (actual != null ? actual.name : hit.gameObject?.name ?? "none") + ")");
        data.pointerCurrentRaycast = hit; data.position = hit.screenPosition;
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static void ClearEditorGuidRegistry()
    {
        // Unity Learn's editor-only scene GUID cache otherwise rejects reopening its sample scene.
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Tutorials.Core.SceneObjectGuidManager")).FirstOrDefault(t => t != null);
        var instance = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        (type?.GetField("m_Components", Hidden)?.GetValue(instance) as IDictionary)?.Clear();
    }
    static IEnumerator Scenarios()
    {
        Check(Time.timeScale == 1 && !TechWisePauseSession.Active, "Fresh session starts with simulation time running");
        float bootTime = Time.time, bootFixedTime = Time.fixedTime;
        for (int i = 0; i < 90; i++) yield return null;
        Check(Time.time > bootTime && Time.fixedTime > bootFixedTime, "Startup advances both simulation and grab physics updates");
        var move = UnityEngine.Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(FindObjectsInactive.Include);
        Check(move != null && move.enabled, "Configured continuous movement provider enabled at startup");
        var motion = (Vector3)typeof(UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider)
            .GetMethod("ComputeDesiredMove", Hidden).Invoke(move, new object[] { Vector2.up });
        Check(motion.sqrMagnitude > 0, "Existing joystick locomotion computes nonzero motion at startup");
        var calloutObject = new GameObject("Inactive callout regression");
        var callout = calloutObject.AddComponent<Unity.VRTemplate.Callout>();
        int beforeCalloutErrors = errors.Count;
        calloutObject.SetActive(false);
        callout.GazeHoverStart(); callout.GazeHoverEnd();
        Check(errors.Count == beforeCalloutErrors, "Inactive controller callouts safely receive gaze enter/exit");
        calloutObject.SetActive(true); callout.enabled = false;
        callout.GazeHoverStart(); callout.GazeHoverEnd();
        Check(errors.Count == beforeCalloutErrors, "Disabled controller callouts safely receive gaze enter/exit");
        UnityEngine.Object.Destroy(calloutObject);
        menu = UnityEngine.Object.FindAnyObjectByType<TechWiseInGameMenu>();
        canvas = (Canvas)typeof(TechWiseInGameMenu).GetField("canvas", Hidden).GetValue(menu);
        Check(canvas.renderMode == RenderMode.WorldSpace && !canvas.gameObject.activeSelf, "Quest menu is world-space and hidden while playing");
        Check(UnityEngine.Object.FindAnyObjectByType<XRUIInputModule>() != null, "Existing XR UI input module present");
        var wrist = (Canvas)typeof(TechWiseInGameMenu).GetField("vrPauseShortcut", Hidden).GetValue(menu);
        Check(wrist != null && wrist.gameObject.activeInHierarchy && wrist.renderMode == RenderMode.WorldSpace, "Visible Quest wrist Pause / Settings button present");
        const string guideKey = "TechWise360.ControlsGuideVisible";
        bool hadGuideSetting = PlayerPrefs.HasKey(guideKey); int guideSetting = PlayerPrefs.GetInt(guideKey, 1);
        Call(menu, "SetControlsExpanded", false); yield return null;
        Check(wrist.gameObject.activeInHierarchy, "Wrist pause stays visible when controls guide is hidden");
        if (hadGuideSetting) PlayerPrefs.SetInt(guideKey, guideSetting); else PlayerPrefs.DeleteKey(guideKey); PlayerPrefs.Save();
        var menuCanvas = canvas; canvas = wrist;
        PointAndClick("Wrist Pause Settings", .2f); canvas = menuCanvas; yield return null;
        Check(Time.timeScale == 0 && canvas.gameObject.activeInHierarchy && !wrist.gameObject.activeInHierarchy, "Wrist button opens pause menu through existing tracked ray system");
        PointAndClick("Resume Button", -.2f); yield return null;
        Check(wrist.gameObject.activeInHierarchy, "Wrist pause returns after Resume");
        canvas = wrist; PointAndClick("Hide wrist shortcut", .2f); canvas = menuCanvas; yield return null;
        Check(!wrist.gameObject.activeInHierarchy, "Hide dismisses wrist shortcut without pausing");
        Call(menu, "SetMenuOpen", true); yield return null;
        var nearRays = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include);
        Check(nearRays.Length > 0 && nearRays.All(r => !r.enableNearCasting && r.interactionLayers.value == 0), "Paused controller cannot prioritize nearby PC grab targets over UI: " + nearRays.Length + " configured rays");
        foreach (var ray in nearRays)
        {
            // Controller objects are dormant in a batch Editor without a tracked headset.
            var activated = new List<GameObject>();
            for (var ancestor = ray.transform; ancestor != null; ancestor = ancestor.parent)
                if (!ancestor.gameObject.activeSelf) { activated.Add(ancestor.gameObject); ancestor.gameObject.SetActive(true); }
            ray.PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase.Dynamic);
            var uiModel = new TrackedDeviceModel(900); ray.UpdateUIModel(ref uiModel);
            Check(uiModel.raycastPoints.Count >= 2, "Existing NearFarInteractor produces UI ray points at timeScale zero");
            foreach (var obj in activated) obj.SetActive(false);
        }
        PointAndClick("Settings Button", .2f); yield return null;
        PointAndClick("Shortcut Visibility", .2f); PointAndClick("Back to Pause Menu", -.2f); PointAndClick("Resume Button", .2f); yield return null;
        Check(wrist.gameObject.activeInHierarchy, "Settings can restore hidden wrist shortcut");
        Check(nearRays.All(r => r.enableNearCasting && r.interactionLayers.value != 0), "Resume restores near grabbing and original interaction layers");
        InputSystem.RegisterLayout("{\"name\":\"PauseTestController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"menuButton\",\"layout\":\"Button\",\"format\":\"FLT\",\"sizeInBits\":32}]}");
        var device = InputSystem.AddDevice("PauseTestController"); InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.LeftHand);
        var key = device["menuButton"] as ButtonControl;
        InputSystem.QueueDeltaStateEvent(key, 1f); InputSystem.Update();
        Check((bool)Call(menu, "WasMenuShortcutPressed"), "Left controller Menu rising edge detected (value " + key.ReadValue() + ", device " + UnityEngine.InputSystem.XR.XRController.leftHand?.name + ")");
        Check(!(bool)Call(menu, "WasMenuShortcutPressed"), "Holding Menu does not repeatedly toggle");
        InputSystem.QueueDeltaStateEvent(key, 0f); InputSystem.Update();
        Check(!(bool)Call(menu, "WasMenuShortcutPressed"), "Menu release does not toggle"); InputSystem.RemoveDevice(device);
        var state = TechWiseSimulationRuntime.Instance;
        var phase = UnityEngine.Object.FindAnyObjectByType<TechWiseDetailedAssemblyRuntime>();
        Check(phase.Ready && state.ConfigurationError == null, "Practice assembly initialized");
        foreach (var interactor in UnityEngine.Object.FindObjectsByType<XRBaseInteractor>()) if (interactor is not XRSocketInteractor) interactor.enabled = false;
        var handObject = new GameObject("Pause verification hand", typeof(SphereCollider), typeof(Rigidbody));
        handObject.GetComponent<SphereCollider>().isTrigger = true; handObject.GetComponent<Rigidbody>().isKinematic = true;
        var hand = handObject.AddComponent<XRDirectInteractor>(); hand.interactionLayers = ~0;
        var cpu = state.FindPart("CPU"); var socket = state.FindSocket("CPU"); var manager = cpu.interactionManager;
        manager.SelectEnter((IXRSelectInteractor)hand, cpu);
        Check(cpu.isSelected, "Existing XR grab works before pause");
        int mistakes = state.PracticeMistakes.Count;
        var enabledProviders = UnityEngine.Object.FindObjectsByType<LocomotionProvider>().Where(p => p.enabled).ToArray();
        Call(menu, "SetMenuOpen", true);
        Check(Time.timeScale == 0 && canvas.gameObject.activeSelf, "Opening menu pauses physics and shows panel");
        Check(!cpu.isSelected && state.PracticeMistakes.Count == mistakes, "Pause safely cancels held part without a release mistake");
        Check(!manager.IsSelectPossible((IXRSelectInteractor)hand, cpu), "Cannot grab parts while menu is open");
        Check(enabledProviders.All(p => !p.enabled), "Locomotion disabled while paused");
        Check(!state.ManipulationLocked, "Pause preserves original assembly lock state");
        float scaledTime = Time.time;
        for (int i = 0; i < 8; i++) yield return null;
        Check(Time.time == scaledTime, "Simulation time remains frozen across frames");
        Capture("pause-menu.png");
        PointAndClick("Settings Button", -.2f); yield return null;
        Check(Button("Back to Pause Menu").gameObject.activeInHierarchy, "Settings opens readable panel while paused");
        Capture("settings.png");
        PointAndClick("Back to Pause Menu", .2f);
        PointAndClick("Controls Button", .2f); yield return null;
        Capture("controls.png");
        PointAndClick("Back to Pause Menu", -.2f);
        PointAndClick("Resume Button", .2f);
        Check(Time.timeScale == 1 && !canvas.gameObject.activeSelf, "Controller Resume restores gameplay");
        Check(enabledProviders.All(p => p.enabled), "Original locomotion restored");
        Check(manager.IsSelectPossible((IXRSelectInteractor)hand, cpu), "Parts selectable again after resume");
        // Seat CPU and close its real locks, then reset a genuinely modified assembly.
        cpu.transform.rotation = socket.GetAttachTransform(cpu).rotation * Quaternion.Inverse(cpu.GetAttachTransform(socket).rotation) * cpu.transform.rotation;
        cpu.transform.position += socket.GetAttachTransform(cpu).position - cpu.GetAttachTransform(socket).position;
        Call(state, "AttachPartToSocket", cpu, socket);
        Call(phase.Cover, "Observe", 0f); Call(phase.Cover, "Commit"); Call(phase.Lever, "Observe", 0f); Call(phase.Lever, "Commit");
        Check(state.IsInstalled("CPU"), "Modified assembly contains installed locked CPU");
        Call(menu, "SetMenuOpen", true);
        Check(socket.IsSelecting(cpu), "Pause retains installed socket ownership");
        yield return null;
        ClearEditorGuidRegistry(); PointAndClick("Reset Practice Button", -.2f);
        for (int i = 0; i < 90; i++) yield return null;
        phase = UnityEngine.Object.FindAnyObjectByType<TechWiseDetailedAssemblyRuntime>(); state = TechWiseSimulationRuntime.Instance;
        Check(cpu == null && phase.Ready, "Reset recreates scene and detailed assembly");
        Check(!state.IsInstalled("CPU") && !phase.Cover.Closed && !phase.Lever.Closed, "Reset clears installed CPU and locks");
        Check(!phase.PasteApplied && !phase.CasePrepared && !phase.M2Lowered && phase.CoolerScrews.All(s => s.Progress == 0), "Reset clears paste, enclosure, M2 and screws");
        Check(state.PracticeMistakes.Count == 0 && !state.PracticeComplete && state.CurrentStep == "CPU", "Reset clears mistakes and progress");
        Check(Time.timeScale == 1 && !canvas.gameObject.activeSelf && TechWiseSimulationModeManager.IsPracticeMode, "Reset resumes same practice mode");
        Call(menu, "SetMenuOpen", true); ClearEditorGuidRegistry(); Call(menu, "ResetPractice");
        for (int i = 0; i < 60; i++) yield return null;
        Check(Time.timeScale == 1 && UnityEngine.Object.FindObjectsByType<TechWiseInGameMenu>().Length == 1, "Repeated reset preserves one working menu");
        TechWiseSimulationModeManager.SetMode("disassembly", "practice");
        ClearEditorGuidRegistry(); Call(menu, "ResetPractice");
        for (int i = 0; i < 120 || TechWiseDisassemblyRuntime.IsPreparing; i++) yield return null;
        state = TechWiseSimulationRuntime.Instance;
        Check(state.Sockets.Any(s => s.hasSelection), "Disassembly practice starts with assembled components");
        var installed = state.Parts.First(p => state.IsPartInstalled(p));
        foreach (var owner in installed.interactorsSelecting.ToArray()) installed.interactionManager.SelectExit(owner, installed);
        installed.transform.position += Vector3.up * 2f;
        Call(menu, "SetMenuOpen", true); yield return null; ClearEditorGuidRegistry(); PointAndClick("Reset Practice Button", .2f);
        for (int i = 0; i < 120 || TechWiseDisassemblyRuntime.IsPreparing; i++) yield return null;
        Check(installed == null && TechWiseSimulationModeManager.IsDisassembly && state.Sockets.Any(s => s.hasSelection), "Disassembly reset rebuilds PC and keeps activity type");
        Check(state.PracticeMistakes.Count == 0 && !state.PracticeComplete, "Disassembly reset clears results");
        TechWiseSimulationModeManager.SetMode("assembly", "tutorial");
        ClearEditorGuidRegistry(); SceneManager.LoadScene("Singleplayer");
        for (int i = 0; i < 100; i++) yield return null;
        var tutorial = UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialRuntime>();
        Call(tutorial, "BeginControls");
        var lesson = typeof(TechWiseTutorialRuntime).GetField("stage", Hidden).GetValue(tutorial);
        Call(menu, "SetMenuOpen", true);
        Check(!Button("Reset Practice Button").gameObject.activeSelf, "Reset hidden during Tutorial");
        for (int i = 0; i < 8; i++) yield return null;
        Check(Equals(lesson, typeof(TechWiseTutorialRuntime).GetField("stage", Hidden).GetValue(tutorial)), "Tutorial stage preserved during pause");
        PointAndClick("Resume Button", -.2f);
        Check(Time.timeScale == 1 && Equals(lesson, typeof(TechWiseTutorialRuntime).GetField("stage", Hidden).GetValue(tutorial)), "Tutorial resumes same lesson");
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue); yield return null;
        Call(menu, "SetMenuOpen", true);
        Check(canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponent<TrackedDeviceGraphicRaycaster>().enabled, "Existing VR-only menu policy ignores legacy desktop preference");
        Button("Resume Button").onClick.Invoke(); Check(Time.timeScale == 1, "Resume still works with a legacy desktop preference");
        TechWiseSimulationModeManager.SetMode("assembly", "competition");
        Call(menu, "SetMenuOpen", true);
        Check(!Button("Reset Practice Button").gameObject.activeSelf, "Reset hidden in Competition");
        int sceneHandle = SceneManager.GetActiveScene().handle; Call(menu, "ResetPractice");
        Check(SceneManager.GetActiveScene().handle == sceneHandle && Time.timeScale == 0, "Competition cannot invoke practice reset");
        ClearEditorGuidRegistry(); Call(menu, "BackToMainMenu");
        for (int i = 0; i < 60; i++) yield return null;
        Check(SceneManager.GetActiveScene().name == "MainMenu" && Time.timeScale == 1 && !canvas.gameObject.activeSelf, "Leaving paused session restores time and main menu");
    }
    static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var go = new GameObject("Pause capture", typeof(Camera)); var camera = go.GetComponent<Camera>(); camera.CopyFrom(Camera.main);
        camera.transform.SetPositionAndRotation(Camera.main.transform.position, canvas.transform.rotation); camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.fieldOfView = 60; var rt = new RenderTexture(1600, 1000, 24); camera.targetTexture = rt;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
        File.WriteAllBytes("Logs/PauseReset/" + name, texture.EncodeToPNG()); RenderTexture.active = previous;
        UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(rt);
    }
    public static void BuildQuest()
    {
        Directory.CreateDirectory("Builds/QuestPauseReset"); Directory.CreateDirectory("Logs/PauseReset");
        var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset"); var preloaded = PlayerSettings.GetPreloadedAssets();
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" }, locationPathName = "Builds/QuestPauseReset/TechWise360.apk", target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText("Logs/PauseReset/build-summary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Quest pause/reset build failed");
        }
        finally { EditorUserBuildSettings.buildAppBundle = bundle; PlayerSettings.SetPreloadedAssets(preloaded); File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings); }
    }
}
