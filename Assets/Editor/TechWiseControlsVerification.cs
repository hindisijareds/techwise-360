using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Touch = UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile.OculusTouchController;

/// <summary>Play-mode integration checks with simulated Touch inputs; no real controller claims.</summary>
public static class TechWiseControlsVerification
{
    const string RunKey = "TechWise.CompleteControls.Verifying";
    const string Output = "Logs/CompleteControls";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Prefs = { TechWiseSimulationModeManager.GameModeKey, TechWiseSimulationModeManager.SimulationTypeKey,
        TechWiseSimulationModeManager.CompetitionIdKey, TechWiseSimulationModeManager.CompetitionTitleKey, MainMenu.ControlModeKey };
    static IEnumerator flow;
    static int frame = -1, checks;
    static double deadline;
    static Touch left, right;
    static TechWiseTutorialRuntime lesson;
    static XRRayInteractor hand;
    static readonly List<string> passed = new();

    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false)) return;
            TechWiseAttemptRecorder.LocalVerificationMode = true;
            // Untracked scene interactors can dwell/poke the welcome button at
            // their default pose. Only the explicitly simulated ray drives QA.
            SceneManager.sceneLoaded += DisableUntrackedInteractors;
            DisableUntrackedInteractors(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            var queue = TechWiseOfflineAttemptQueue.EnsureInstance(); queue.StopAllCoroutines(); queue.enabled = false;
            checks = 0; passed.Clear(); deadline = EditorApplication.timeSinceStartup + 300;
            flow = Scenarios(); EditorApplication.update += Tick;
        };
    }
    static void DisableUntrackedInteractors(Scene scene, LoadSceneMode mode)
    {
        foreach (var interactor in UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include))
            if (interactor is not XRSocketInteractor) interactor.enabled = false;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach (var key in Prefs)
        {
            SessionState.SetBool(RunKey + key, PlayerPrefs.HasKey(key));
            SessionState.SetString(RunKey + "Value" + key, PlayerPrefs.GetString(key, ""));
        }
        TechWiseSimulationModeManager.SetMode("assembly", "tutorial");
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
        EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity");
        SessionState.SetBool(RunKey, true);
        EditorApplication.EnterPlaymode();
    }
    public static void InspectScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity");
        var lines = new List<string>();
        foreach (var part in UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include))
        {
            if (TechWiseSimulationModeManager.IsKnowledgeDisplay(part.transform)) continue;
            lines.Add("PART " + TechWiseSimulationModeManager.ResolveStepId(part.transform) + " | " + PathOf(part.transform) + " | pos=" + part.transform.position);
        }
        foreach (var socket in UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Content.Interaction.XRLockSocketInteractor>(FindObjectsInactive.Include))
            if (!TechWiseSimulationModeManager.IsKnowledgeDisplay(socket.transform)) lines.Add("SOCKET " + TechWiseSimulationModeManager.ResolveStepId(socket.transform) + " | " + PathOf(socket.transform));
        Directory.CreateDirectory(Output); File.WriteAllLines(Output + "/scene-parts.txt", lines);
    }
    static string PathOf(Transform t)
    {
        var path = t.name + (t.gameObject.activeSelf ? "" : "[OFF]");
        while ((t = t.parent) != null) path = t.name + (t.gameObject.activeSelf ? "" : "[OFF]") + "/" + path;
        return path;
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Tutorial test timeout");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended during tutorial verification");
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!flow.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SceneManager.sceneLoaded -= DisableUntrackedInteractors;
        SessionState.SetBool(RunKey, false);
        if (left != null && left.added) InputSystem.RemoveDevice(left);
        if (right != null && right.added) InputSystem.RemoveDevice(right);
        foreach (var key in Prefs)
            if (SessionState.GetBool(RunKey + key, false)) PlayerPrefs.SetString(key, SessionState.GetString(RunKey + "Value" + key, ""));
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        string result = error == null ? $"PASS: {checks} simulated play-mode assertions." : $"FAIL after {checks} assertions: {error}";
        File.WriteAllText(Output + "/playmode-results.txt", string.Join("\n", passed) + "\n" + result +
            "\nPhysical Quest controller operation requires an in-headset test.");
        Debug.Log(result); EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; passed.Add("PASS " + name); Debug.Log("TUTORIAL_PASS " + name);
        File.WriteAllText(Output + "/playmode-progress.txt", string.Join("\n", passed));
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static string Stage => Get(lesson, "stage").ToString();
    static void StageIs(string expected) => Check(Stage == expected, "Stage is " + expected + " (actual " + Stage + ")");
    static Button StartButton() => UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include)
        .First(t => t != null && !string.IsNullOrEmpty(t.text) && t.text.Trim().Equals("Let's get started", StringComparison.OrdinalIgnoreCase)).GetComponentInParent<Button>(true);
    static void Grip(Touch device, bool down)
    {
        Set(device.gripPressed, down ? 1f : 0f); Set(device.grip, down ? 1f : 0f);
    }
    static void Set<T>(InputControl<T> control, T value) where T : struct
    {
        using (StateEvent.From(control.device, out var data))
        {
            control.WriteValueIntoEvent(value, data);
            InputSystem.QueueEvent(data);
        }
        // The runner is called from EditorApplication.update; explicitly process
        // the queued event in the player buffer rather than the editor buffer.
        typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
    }
    static IEnumerator Scenarios()
    {
        for (int i = 0; i < 35; i++) yield return null;
        lesson = UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialRuntime>();
        Debug.Log("Tutorial setup diagnostics: scene=" + SceneManager.GetActiveScene().name + " mode=" + TechWiseSimulationModeManager.GameMode +
            " lesson=" + (lesson != null) + " camera=" + (Camera.main != null) + " simulation=" + (TechWiseSimulationRuntime.Instance != null) +
            " button=" + (StartButton() != null));
        Check(lesson != null && (bool)Get(lesson, "initialized"), "Tutorial bootstrapped in Singleplayer");
        StageIs("Welcome");
        var state = TechWiseSimulationRuntime.Instance;
        Check(StartButton().onClick.GetPersistentEventCount() == 0, "Start routes through training, deferring authored assembly callbacks");
        left = InputSystem.AddDevice<Touch>(); InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
        right = InputSystem.AddDevice<Touch>(); InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
        var handObject = new GameObject("Left Controller Test Ray"); hand = handObject.AddComponent<XRRayInteractor>();
        hand.enableUIInteraction = true;
        Call(lesson, "BindActions");
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            foreach (var map in asset.actionMaps)
                if (map.name.StartsWith("XRI ")) map.Enable();
        var dormantCpu = UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include)
            .First(p => TechWiseSimulationModeManager.ResolveStepId(p.transform) == "CPU");
        Check(!(bool)Call(state, "ValidateSelection", hand, dormantCpu), "PC selection filter locked before training");
        Check(state.Parts.Where(p=>state.StepOf(p)?.StartsWith("Fan")==true).All(p=>!p.gameObject.activeInHierarchy),"No premature tutorial fans before training");
        StartButton().onClick.Invoke();
        StageIs("Point");
        Check(!(bool)Get(lesson, "startedAssembly"), "Start did not invoke assembly callbacks");
        Call(lesson, "ConfirmPointer", false); StageIs("Point");
        var target = UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialTriggerTarget>();
        Check(target != null, "Tracked trigger target exists");
        target.OnPointerClick(new PointerEventData(EventSystem.current)); StageIs("Point");
        Capture("point.png");
        // Verify UI ray geometry independently of synthetic input dispatch.
        RayCheck(target.GetComponent<Button>());
        var module = EventSystem.current.GetComponent<XRUIInputModule>();
        module.RegisterInteractor(hand);
        Check(hand.TryGetUIModel(out var model), "Test ray registered with the existing XR UI module");
        var pointer = new TrackedDeviceEventData(EventSystem.current) { pointerId = model.pointerId };
        Set(left.primaryButton, 1f);
        target.OnPointerDown(pointer); target.OnPointerClick(pointer); StageIs("Point");
        Set(left.primaryButton, 0f);
        Set(left.triggerPressed, 1f);
        Set(left.trigger, 1f);
        Debug.Log("TRIGGER_TEST device=" + left.triggerPressed.isPressed + " value=" + left.trigger.ReadValue() + " module=" + EventSystem.current.currentInputModule +
            " interactor=" + pointer.interactor + " held=" + Call(lesson, "TriggerHeld", hand.transform));
        foreach (var pair in (Dictionary<string, InputAction>)Get(lesson, "actions"))
            if (pair.Key.EndsWith("UI Press")) Debug.Log("TRIGGER_ACTION " + pair.Key + " enabled=" + pair.Value.enabled + " pressed=" + pair.Value.IsPressed() + " controls=" + string.Join(",", pair.Value.controls.Select(c => c.path)));
        target.OnPointerDown(pointer);
        Set(left.triggerPressed, 0f);
        Set(left.trigger, 0f);
        target.OnPointerClick(pointer); StageIs("Move");
        Call(lesson, "ObserveMovement", false, 0.25f); StageIs("Move");
        Call(lesson, "ObserveMovement", true, 0f); StageIs("Move");
        Call(lesson, "ObserveMovement", true, 0.21f); StageIs("Turn");
        Call(lesson, "ObserveTurn", false, 30f); StageIs("Turn");
        Call(lesson, "ObserveTurn", true, 30f); StageIs("Grab");
        var block = (XRGrabInteractable)Get(lesson, "practiceObject");
        Check(block != null && !state.Parts.Contains(block), "Training block isolated from PC simulation");
        Check(!(bool)Call(state, "ValidateSelection", hand, dormantCpu), "PC remains locked throughout training");
        Grip(left, true);
        // Pin synthetic test ray to the block without asking the hardware to move.
        hand.transform.position = block.transform.position - Vector3.forward * 0.05f;
        hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, block);
        StageIs("Manipulate");
        Check((bool)Get(lesson, "gripArmed"), "Left grip accepted by actual grab event");
        Grip(left, false); hand.interactionManager.SelectExit((IXRSelectInteractor)hand, block);
        StageIs("Manipulate");
        handObject.name = "Right Controller Test Ray";
        Grip(right, true); hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, block);
        Check((bool)Get(lesson, "gripArmed"), "Right grip accepted for retry");
        // Actual object transforms plus bound joystick input must both change.
        Call(lesson, "ObserveManipulation"); StageIs("Manipulate");
        Set(right.thumbstick, new Vector2(0.8f, 0.8f));
        block.transform.position += Vector3.right * 0.08f;
        block.transform.rotation = Quaternion.Euler(0, 35, 0) * block.transform.rotation;
        Call(lesson, "ObserveManipulation"); StageIs("Place");
        Set(right.thumbstick, Vector2.zero);
        var destination = (Transform)Get(lesson, "practiceTarget");
        block.transform.position = destination.position + Vector3.up * 0.5f;
        Grip(right, false); hand.interactionManager.SelectExit((IXRSelectInteractor)hand, block);
        Call(lesson, "CheckPracticePlacement"); StageIs("Place");
        Grip(right, true); hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, block);
        block.transform.SetPositionAndRotation(destination.position, destination.rotation);
        Call(lesson, "CheckPracticePlacement"); StageIs("Place");
        Grip(right, false); hand.interactionManager.SelectExit((IXRSelectInteractor)hand, block);
        block.transform.SetPositionAndRotation(destination.position, destination.rotation);
        Call(lesson, "CheckPracticePlacement"); StageIs("ControlsComplete");
        Capture("controls-complete.png");
        for (int i = 0; i < 120 && Stage == "ControlsComplete"; i++) yield return null;
        StageIs("Assembly");
        Check(state.ConfigurationError == null, "Assembly configuration valid after authored activation: " + state.ConfigurationError);
        Check(Get(lesson, "practiceObject") == null, "Training objects removed after completion");
        Check(!StartButton().gameObject.activeInHierarchy, "Original simulation-start callbacks closed welcome");
        Check(hand.interactionManager.IsSelectPossible(hand, state.FindPart("CPU")), "PC selection restored after training");
        for(int i=0;i<15;i++)yield return null;
        var lessonView=UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialView>();
        var title=lessonView.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="Lesson title");
        var drag=title.GetComponentInParent<TechWiseDraggableUiPanel>();
        Physics.SyncTransforms();Check(drag.Collider.bounds.Contains(title.rectTransform.TransformPoint(title.rectTransform.rect.center)),"Visible title is inside the grip hitbox");
        var scroll=lessonView.GetComponentInChildren<ScrollRect>();Canvas.ForceUpdateCanvases();
        Check(scroll.content.rect.height>scroll.viewport.rect.height,"Detailed instructions retain all text in scroll viewport");
        ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-4)},ExecuteEvents.scrollHandler);
        Check(scroll.verticalNormalizedPosition<.999f,"Lesson accepts UI scroll events");
        Grip(right,true);hand.interactionManager.SelectEnter((IXRSelectInteractor)hand,drag.GrabInteractable);
        Check(TechWiseSimulationRuntime.IsHeld(drag.GrabInteractable),"Existing XR grip can select the lesson title");
        drag.transform.position+=Vector3.right*.2f;Grip(right,false);hand.interactionManager.SelectExit((IXRSelectInteractor)hand,drag.GrabInteractable);
        var placedPanel=drag.transform.position;for(int i=0;i<10;i++)yield return null;
        Check(Vector3.Distance(drag.transform.position,placedPanel)<.001f,"Released lesson panel retains user placement");
        scroll.verticalNormalizedPosition=1;
        Capture("assembly.png");
        Check(TechWiseDetailedAssemblyRuntime.Instance.Ready,"Complete workbench ready after controller training");
        Check(state.Parts.Where(p=>state.StepOf(p)?.StartsWith("Fan")==true).All(p=>!p.gameObject.activeInHierarchy),"Fans remain hidden until the fan lesson");
        Check(TechWiseDetailedAssemblyRuntime.Instance.Fasteners.All(f=>!f.Inserted),"Controller training does not auto-install screws");
        InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right);
        UnityEngine.Object.Destroy(hand.gameObject);
        ClearEditorGuids();
        var reload = SceneManager.LoadSceneAsync("Singleplayer");
        while (!reload.isDone) yield return null;
        for (int i = 0; i < 30; i++) yield return null;
        lesson = UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialRuntime>(); StageIs("Welcome");
        StartButton().onClick.Invoke();
        Call(lesson, "SkipControls"); StageIs("Assembly");
        Call(lesson, "SkipControls"); StageIs("Assembly");
        Check((bool)Get(lesson, "startedAssembly"), "Skip starts assembly once");
        TechWiseSimulationModeManager.SetMode("assembly", "practice"); ClearEditorGuids(); reload = SceneManager.LoadSceneAsync("Multiplayer");
        while (!reload.isDone) yield return null;
        for (int i = 0; i < 30; i++) yield return null;
        Check(UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialView>() == null, "Tutorial view absent in Practice");
        Check(!TechWiseSimulationRuntime.Instance.ManipulationLocked, "Practice manipulation unchanged");
        Check(UnityEngine.Object.FindAnyObjectByType<TechWisePracticeGuidePanel>() != null, "Existing Practice guide preserved");
        TechWiseSimulationModeManager.SetMode("assembly", "competition"); ClearEditorGuids(); reload = SceneManager.LoadSceneAsync("Multiplayer");
        while (!reload.isDone) yield return null;
        for (int i = 0; i < 30; i++) yield return null;
        Check(UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialView>() == null, "Tutorial view absent in Competition");
        Check(TechWiseSimulationRuntime.Instance.ManipulationLocked, "Competition still waits for its own Start");
        reload = SceneManager.LoadSceneAsync("MainMenu");
        while (!reload.isDone) yield return null;
        for (int i = 0; i < 20; i++) yield return null;
        Check(UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialView>() == null, "Lesson cleaned up on Main Menu");
    }
    static void ClearEditorGuids()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Tutorials.Core.SceneObjectGuidManager")).FirstOrDefault(t=>t!=null);
        if(type!=null){var manager=type.GetProperty("Instance").GetValue(null);((IDictionary)type.GetField("m_Components",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager)).Clear();}
    }
    static void RayCheck(Button target)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        var camera = canvas.worldCamera;
        Canvas.ForceUpdateCanvases();
        var center = target.transform.TransformPoint(((RectTransform)target.transform).rect.center);
        var hits = new List<RaycastResult>();
        canvas.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(new TrackedDeviceEventData(EventSystem.current)
        { layerMask = ~0, rayPoints = new List<Vector3> { camera.transform.position, center + (center - camera.transform.position).normalized } }, hits);
        Check(hits.Any(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == target.gameObject), "New lesson button receives a tracked ray hit");
    }
    static void Capture(string filename)
    {
        var panel = GameObject.Find("Tutorial lesson panel");
        var cameraObject = new GameObject("Tutorial QA camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(panel.transform.position - panel.transform.forward * 1.5f, panel.transform.rotation);
        camera.nearClipPlane = 0.01f; camera.fieldOfView = 55;
        if (Camera.main != null) camera.cullingMask = Camera.main.cullingMask;
        var rt = new RenderTexture(1280, 800, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        File.WriteAllBytes(Output + "/" + filename, tex.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null;
        UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(cameraObject);
    }
    public static void BuildQuest()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory("Builds/InteractiveTutorial");
        var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
        var preloaded = PlayerSettings.GetPreloadedAssets();
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" },
                locationPathName = "Builds/InteractiveTutorial/TechWise360.apk", target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText(Output + "/build-summary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Interactive tutorial APK build failed");
        }
        finally { PlayerSettings.SetPreloadedAssets(preloaded); File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings); }
    }
}
