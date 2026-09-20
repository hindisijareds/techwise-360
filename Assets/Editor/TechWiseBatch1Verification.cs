using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System.Reflection;

public static class TechWiseBatch1Verification
{
    static IEnumerator testFlow;
    static int checks;
    static int lastFrame = -1;
    static double deadline;
    const string RunningKey = "TechWise.Batch1.TestsRunning";
    static readonly string[] PreferenceKeys = { TechWiseSimulationModeManager.SimulationTypeKey, TechWiseSimulationModeManager.GameModeKey,
        TechWiseSimulationModeManager.CompetitionIdKey, TechWiseSimulationModeManager.CompetitionTitleKey, MainMenu.ControlModeKey };

    [InitializeOnLoadMethod]
    static void ResumeTests()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                checks = 0; deadline = EditorApplication.timeSinceStartup + 240;
                var queue = TechWiseOfflineAttemptQueue.EnsureInstance();
                queue.StopAllCoroutines(); queue.enabled = false;
                testFlow = SimulationTests();
                EditorApplication.update += Tick;
            }
        };
    }
    public static void RunSimulationTests()
    {
        foreach (var key in PreferenceKeys)
        {
            SessionState.SetBool("Batch1.Had." + key, PlayerPrefs.HasKey(key));
            SessionState.SetString("Batch1.Pref." + key, PlayerPrefs.GetString(key, ""));
        }
        // Tests must neither send attempts using a real student nor overwrite real pending work.
        var queuePath = Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");
        if (File.Exists(queuePath + ".batch1-backup"))
        {
            File.Copy(queuePath + ".batch1-backup", queuePath, true);
            File.Delete(queuePath + ".batch1-backup");
        }
        SessionState.SetBool("Batch1.HadQueue", File.Exists(queuePath));
        if (File.Exists(queuePath)) File.Copy(queuePath, queuePath + ".batch1-backup", true);
        if (File.Exists(queuePath)) File.Delete(queuePath);
        TechWiseSimulationModeManager.SetMode("assembly", "practice");
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue);
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }
    public static void RunAssemblyReleaseTests()
    {
        SessionState.SetBool("TechWise.AssemblyReleaseOnly", true);
        RunSimulationTests();
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play-mode test timeout");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (!testFlow.MoveNext()) EndTests(null);
        }
        catch (Exception exception) { EndTests(exception); }
    }
    static void EndTests(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunningKey, false);
        var queue = TechWiseOfflineAttemptQueue.Instance;
        if (queue != null) { queue.StopAllCoroutines(); queue.enabled = false; }
        foreach (var key in PreferenceKeys)
            if (SessionState.GetBool("Batch1.Had." + key, false)) PlayerPrefs.SetString(key, SessionState.GetString("Batch1.Pref." + key, ""));
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        var queuePath = Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");
        if (SessionState.GetBool("Batch1.HadQueue", false))
        {
            File.Copy(queuePath + ".batch1-backup", queuePath, true);
            File.Delete(queuePath + ".batch1-backup");
        }
        else if (File.Exists(queuePath)) File.Delete(queuePath);
        var result = error == null ? $"PASS: {checks} Batch 1 assertions" : $"FAIL after {checks} assertions: {error}";
        File.WriteAllText("Logs/Batch1/playmode-results.txt", result);
        Debug.Log(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Debug.Log("BATCH1_PASS " + message);
    }
    static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    static string DescribeStep(TechWiseSimulationRuntime state, string step)
    {
        var touched = (System.Collections.Generic.HashSet<XRGrabInteractable>)typeof(TechWiseSimulationRuntime)
            .GetField("touched", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
        return string.Join("; ", state.Parts.Where(p => TechWiseSimulationModeManager.ResolveStepId(p.transform) == step).Select(p =>
            $"{p.name}: position={p.transform.position}, held={TechWiseSimulationRuntime.IsHeld(p)}, installed={state.IsPartInstalled(p)}, touched={touched.Contains(p)}, nearestTarget=" +
            state.Sockets.Where(s => TechWiseSimulationModeManager.ResolveStepId(s.transform) == step)
                .Min(s => Vector3.Distance(p.transform.position, s.GetAttachTransform(p).position)).ToString("F3")));
    }
    static IEnumerator SimulationTests()
    {
        for (int frame = 0; frame < 20; frame++) yield return null;
        var state = TechWiseSimulationRuntime.Instance;
        Check(state != null && state.Parts.Count >= 11 && state.ConfigurationError == null, "Practice scene has all curriculum components and sockets: " + state?.ConfigurationError);
        Check(state.Parts.All(p => !TechWiseSimulationModeManager.IsKnowledgeDisplay(p.transform)), "Knowledge display excluded from tracking");
        Check(GameObject.Find("TechWise Practice Side Guide") != null, "Desktop practice side guide created");
        var cpu = state.FindPart("CPU"); var cpuSocket = state.FindSocket("CPU"); var ramSocket = state.FindSocket("RAM");
        Check(!TechWiseSimulationRuntime.Matches(ramSocket, cpu.transform), "CPU rejected by RAM key lock");
        Check(!TechWiseSimulationRuntime.Matches(cpuSocket, state.FindPart("RAM").transform), "RAM rejected by CPU key lock");
        var missingCooler = state.PlacementProblem(state.FindSocket("CPUCooler"), state.FindPart("CPUCooler"), out _, out _);
        Check(missingCooler != null, "Unprepared cooler placement rejected");
        var assemblyHand = new GameObject("Assembly release test hand").AddComponent<XRDirectInteractor>();
        assemblyHand.interactionLayers = ~0;
        var board = state.FindPart("Motherboard");
        Check(!state.MotherboardReady && !((IXRSelectInteractable)board).IsSelectableBy(assemblyHand), "Incomplete motherboard rejects hand selection");
        Check(board.GetComponent<Rigidbody>().isKinematic, "Incomplete motherboard stays on workbench");
        assemblyHand.transform.SetPositionAndRotation(cpu.transform.position, cpu.transform.rotation);
        assemblyHand.StartManualInteraction((IXRSelectInteractable)cpu);
        yield return null;
        assemblyHand.EndManualInteraction();
        yield return null;
        Check(!cpu.GetComponent<Rigidbody>().isKinematic && cpu.GetComponent<Rigidbody>().useGravity &&
            cpu.colliders.All(c => c == null || !c.isTrigger), "Loose released CPU has gravity and solid colliders");
        foreach (var step in TechWiseSimulationModeManager.AssemblyOrder)
        {
            foreach (var part in state.Parts.Where(p => state.StepOf(p) == step))
            {
                var target = state.Sockets.First(s => !s.hasSelection && TechWiseSimulationRuntime.Matches(s, part.transform));
                assemblyHand.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                assemblyHand.StartManualInteraction((IXRSelectInteractable)part);
                Check(assemblyHand.IsSelecting(part), "Assembly hand owns " + part.name);
                Check(part.colliders.All(c => c == null || c.isTrigger), "Held part can reach recessed socket: " + part.name);
                yield return null;
                var attach = part.GetAttachTransform(target);
                var destination = target.GetAttachTransform(part);
                part.transform.rotation = destination.rotation * Quaternion.Inverse(attach.rotation) * part.transform.rotation;
                part.transform.position += destination.position - attach.position + Vector3.up * 0.04f;
                Check(target.placementPreviewValid(part), "Release preview accepts nearby aligned part: " + part.name);
                assemblyHand.EndManualInteraction();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Check(target.IsSelecting(part), "Hand release snaps " + part.name);
            }
            for (int frame = 0; frame < 3; frame++) yield return null;
            Check(state.IsInstalled(step), "Installed state " + step);
            Check(state.CurrentStep != step, "Step guide advances after " + step);
            if (step == "CPUCooler") Check(state.MotherboardReady && ((IXRSelectInteractable)board).IsSelectableBy(assemblyHand), "Complete motherboard unlocks for hand selection");
        }
        UnityEngine.Object.Destroy(assemblyHand.gameObject);
        yield return null;
        Check(state.CurrentStep == null && state.PracticeComplete, "Practice assembly completes from live sockets");
        Check(!state.PracticeMistakes.Any(m => m.kind == "wrong_order"), "Correct assembly order has no sequence mistakes");
        var extraRoot = new GameObject("Occupied socket test part", typeof(BoxCollider), typeof(Rigidbody), typeof(Keychain));
        foreach (var key in cpuSocket.keychainLock.requiredKeys) extraRoot.GetComponent<Keychain>().AddKey(key);
        var extra = extraRoot.AddComponent<XRGrabInteractable>();
        Check(!cpuSocket.CanSelect((IXRSelectInteractable)extra), "Occupied socket refuses second object");
        UnityEngine.Object.Destroy(extra.gameObject);
        state.RecordMistake("wrong_part", "RAM", "Test wrong target", "Test guidance");
        Check(state.PracticeMistakes.Count == 1, "Practice mistake retained for end review");
        if (SessionState.GetBool("TechWise.AssemblyReleaseOnly", false))
        {
            SessionState.SetBool("TechWise.AssemblyReleaseOnly", false);
            yield break;
        }
        for (int frame = 0; frame < 12; frame++) yield return null;
        yield return null;
        TechWiseSimulationModeManager.SetMode("disassembly", "practice");
        SceneManager.LoadScene("Multiplayer");
        for (int frame = 0; frame < 30; frame++) yield return null;
        state = TechWiseSimulationRuntime.Instance;
        Check(!TechWiseDisassemblyRuntime.IsPreparing && TechWiseDisassemblyRuntime.LastSkippedSteps.Length == 0, "Disassembly setup succeeds without skipped parts");
        Check(TechWiseSimulationModeManager.AssemblyOrder.All(state.IsInstalled), "Disassembly starts fully installed");
        Check(state.CurrentStep == "CPUCooler", "Disassembly current step starts with cooler");
        var handObject = new GameObject("Test input interactor");
        var hand = handObject.AddComponent<XRDirectInteractor>();
        hand.interactionLayers = ~0;
        foreach (var step in TechWiseSimulationModeManager.DisassemblyOrder)
        {
            foreach (var part in state.Parts.Where(p => TechWiseSimulationModeManager.ResolveStepId(p.transform) == step))
            {
            hand.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
            hand.StartManualInteraction((IXRSelectInteractable)part);
            Check(hand.IsSelecting(part), "Direct grab takes ownership: " + part.name);
            yield return null;
            hand.transform.position += Vector3.right * 3;
            for (int frame = 0; frame < 15; frame++) yield return null;
            hand.EndManualInteraction();
            for (int frame = 0; frame < 3; frame++) yield return null;
            var networkTransform = part.GetComponent<XRMultiplayer.ClientNetworkTransform>();
            Check(networkTransform == null || !networkTransform.enabled, "Local release keeps network transform disabled: " + part.name);
            }
            Check(state.IsStepComplete(step), "Disassembly release " + step + " | " + DescribeStep(state, step));
        }
        Check(state.CurrentStep == null && state.PracticeComplete, "Practice disassembly completes");
        TechWiseSimulationModeManager.SetMode("assembly", "competition");
        SceneManager.LoadScene("Multiplayer");
        for (int frame = 0; frame < 15; frame++) yield return null;
        state = TechWiseSimulationRuntime.Instance;
        Check(GameObject.Find("TechWise Practice Side Guide") == null, "Assessment has no practice side panel");
        Check(state.Sockets.All(s => !s.showInteractableHoverMeshes && !s.hoverSocketSnapping), "Assessment disables socket answer previews");
        Check(state.ManipulationLocked, "Assessment manipulation locked before Start");
        var recorder = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        Invoke(recorder, "BeginAttempt");
        Check(!state.ManipulationLocked, "Start unlocks input");
        for (int frame = 0; frame < 3; frame++) yield return null;
        Invoke(recorder, "FinishAttempt");
        TechWiseAttemptRecorder.TryGetSnapshot(out var snapshot);
        Check(snapshot.resultsRevealed && snapshot.score == 0 && snapshot.mistakes == state.CaptureAssessmentState().Length, "DONE validates empty build, reveals individual component omissions and zero score");
        Check(state.ManipulationLocked, "DONE locks further manipulation");
        Check(TechWiseOfflineAttemptQueue.PendingCount == 1, "Submitted result saved to isolated local queue");
        Invoke(recorder, "FinishAttempt");
        Check(TechWiseOfflineAttemptQueue.PendingCount == 1, "Repeated DONE does not enqueue twice");
        var duration = snapshot.durationSeconds;
        for (int frame = 0; frame < 5; frame++) yield return null;
        TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        Check(snapshot.durationSeconds == duration, "Submitted timer remains stopped");
        TechWiseSimulationModeManager.SetMode("assembly", "competition");
        SceneManager.LoadScene("Multiplayer");
        for (int frame = 0; frame < 20; frame++) yield return null;
        state = TechWiseSimulationRuntime.Instance;
        Check(state.ConfigurationError == null, "Multiplayer scene has full component coverage: " + state.ConfigurationError);
        recorder = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        Invoke(recorder, "BeginAttempt");
        foreach (var step in TechWiseSimulationModeManager.AssemblyOrder)
        {
            Check(TechWiseDisassemblyRuntime.InstallStepIntoSocket(step), "Assessment snap " + step);
            yield return null;
        }
        TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        Check(!snapshot.resultsReady && snapshot.status == "Running", "All parts placed does not finish assessment");
        Check(snapshot.mistakes == 0 && snapshot.completedSteps == 0, "Assessment snapshot withholds correctness before submission");
        Invoke(recorder, "FinishAttempt");
        TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        Check(snapshot.score == 100 && snapshot.resultsRevealed, "Correct complete build scores 100 after DONE");
        TechWiseSimulationModeManager.SetMode("disassembly", "competition");
        SceneManager.LoadScene("Multiplayer");
        for (int frame = 0; frame < 30; frame++) yield return null;
        state = TechWiseSimulationRuntime.Instance;
        recorder = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        Check(TechWiseDisassemblyRuntime.LastSkippedSteps.Length == 0, "Assessment disassembly prepares all parts");
        Invoke(recorder, "BeginAttempt");
        var ray = new GameObject("Test desktop ray").AddComponent<XRRayInteractor>();
        ray.interactionLayers = ~0;
        foreach (var step in TechWiseSimulationModeManager.DisassemblyOrder)
        {
            foreach (var part in state.Parts.Where(p => TechWiseSimulationModeManager.ResolveStepId(p.transform) == step))
            {
                ray.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                ray.StartManualInteraction((IXRSelectInteractable)part);
                Check(ray.IsSelecting(part), "Ray grab takes ownership: " + part.name);
                yield return null;
                ray.transform.position += Vector3.right * 3;
                for (int frame = 0; frame < 15; frame++) yield return null;
                ray.EndManualInteraction();
                yield return null;
            }
        }
        TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        Check(snapshot.status == "Running", "Assessment disassembly waits for DONE after removal");
        Invoke(recorder, "FinishAttempt");
        TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        Check(snapshot.resultsRevealed && snapshot.score == 100, "Ray-driven correct disassembly submits successfully");
    }
    public static void AuditScenes()
    {
        var output = new StringBuilder();
        foreach (var scenePath in new[] { "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" })
        {
            EditorSceneManager.OpenScene(scenePath);
            output.AppendLine(scenePath);
            foreach (var part in UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
            {
                var step = TechWiseSimulationModeManager.ResolveStepId(part.transform);
                if (step == null) continue;
                output.AppendLine($"PART {step}: {PathOf(part.transform)} @ {part.transform.position}");
            }
            foreach (var socket in UnityEngine.Object.FindObjectsByType<XRLockSocketInteractor>(FindObjectsSortMode.None))
            {
                var step = TechWiseSimulationModeManager.ResolveStepId(socket.transform);
                if (step == null) continue;
                output.AppendLine($"SOCKET {step}: {PathOf(socket.transform)} @ {socket.transform.position}");
            }
        }
        Directory.CreateDirectory("Logs/Batch1");
        File.WriteAllText("Logs/Batch1/scene-audit.txt", output.ToString());
        Debug.Log("BATCH1_SCENE_AUDIT_COMPLETE");
    }
    static string PathOf(Transform target) => target.parent == null ? target.name : PathOf(target.parent) + "/" + target.name;
}
