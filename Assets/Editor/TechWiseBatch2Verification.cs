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

public static class TechWiseBatch2Verification
{
    static IEnumerator testFlow;
    static int checks;
    static int lastFrame = -1;
    static double deadline;
    const string RunningKey = "TechWise.Batch2.TestsRunning";
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
                TechWiseAttemptRecorder.LocalVerificationMode = true;
                var queue = TechWiseOfflineAttemptQueue.EnsureInstance();
                queue.StopAllCoroutines(); queue.enabled = false;
                testFlow = SimulationTests();
                EditorApplication.update += Tick;
            }
        };
    }
    public static void RunSimulationTests()
    {
        Directory.CreateDirectory("Logs/Batch2");
        VerifyCore();
        foreach (var key in PreferenceKeys)
        {
            SessionState.SetBool("Batch2.Had." + key, PlayerPrefs.HasKey(key));
            SessionState.SetString("Batch2.Pref." + key, PlayerPrefs.GetString(key, ""));
        }
        // Tests must neither send attempts using a real student nor overwrite real pending work.
        var queuePath = Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");
        if (File.Exists(queuePath + ".batch2-backup"))
        {
            File.Copy(queuePath + ".batch2-backup", queuePath, true);
            File.Delete(queuePath + ".batch2-backup");
        }
        SessionState.SetBool("Batch2.HadQueue", File.Exists(queuePath));
        if (File.Exists(queuePath)) File.Copy(queuePath, queuePath + ".batch2-backup", true);
        if (File.Exists(queuePath)) File.Delete(queuePath);
        foreach (var suffix in new[] { ".bak", ".tmp" })
        {
            SessionState.SetBool("Batch2.HadQueue" + suffix, File.Exists(queuePath + suffix));
            if (File.Exists(queuePath + suffix)) File.Copy(queuePath + suffix, queuePath + suffix + ".batch2-backup", true);
        }
        TechWiseSimulationModeManager.SetMode("assembly", "practice");
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue);
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
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
        TechWiseAttemptRecorder.LocalVerificationMode = false;
        SessionState.SetBool(RunningKey, false);
        var queue = TechWiseOfflineAttemptQueue.Instance;
        if (queue != null) { queue.StopAllCoroutines(); queue.enabled = false; }
        foreach (var key in PreferenceKeys)
            if (SessionState.GetBool("Batch2.Had." + key, false)) PlayerPrefs.SetString(key, SessionState.GetString("Batch2.Pref." + key, ""));
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        var queuePath = Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");
        if (SessionState.GetBool("Batch2.HadQueue", false))
        {
            File.Copy(queuePath + ".batch2-backup", queuePath, true);
            File.Delete(queuePath + ".batch2-backup");
        }
        else if (File.Exists(queuePath)) File.Delete(queuePath);
        foreach (var suffix in new[] { ".bak", ".tmp" })
        {
            if (SessionState.GetBool("Batch2.HadQueue" + suffix, false))
            {
                File.Copy(queuePath + suffix + ".batch2-backup", queuePath + suffix, true);
                File.Delete(queuePath + suffix + ".batch2-backup");
            }
            else if (File.Exists(queuePath + suffix)) File.Delete(queuePath + suffix);
        }
        var result = error == null ? $"PASS: {checks} Batch 2 assertions" : $"FAIL after {checks} assertions: {error}";
        File.WriteAllText("Logs/Batch2/playmode-results.txt", result);
        Debug.Log(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Debug.Log("BATCH2_PASS " + message);
    }
    static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    static void VerifyCore()
    {
        checks = 0;
        var clock = new TechWiseAssessmentClock();
        Check(clock.Elapsed(100) == 0 && !clock.Submit(100), "Unstarted timer does not tick or submit");
        Check(clock.Start(100, DateTime.UtcNow), "Official Start begins timer");
        var id = clock.AttemptId;
        Check(!clock.Start(200, DateTime.UtcNow) && clock.AttemptId == id, "Second Start cannot reset identity or timer");
        Check(clock.Elapsed(130.25) == 30.25, "Monotonic timer preserves subsecond precision");
        Check(clock.Elapsed(120) == 30.25, "Clock cannot move backwards");
        Check(clock.Submit(140.75) && clock.Elapsed(9999) == 40.75, "Submission freezes elapsed time");
        Check(!clock.Submit(200), "Only one submission permitted");
        var next = new TechWiseAssessmentClock(); next.Start(1, DateTime.UtcNow);
        Check(next.AttemptId != id, "New attempt gets a distinct identity");
        var settings = new TechWiseAssessmentScoringSettings();
        var mistakes = new System.Collections.Generic.List<TechWiseVrMistakeDetail>();
        Check(TechWiseAssessmentScoring.Calculate(settings, mistakes, 600, false, 8, 8).final_score == 100, "Existing assembly time allowance preserved");
        Check(TechWiseAssessmentScoring.Calculate(settings, mistakes, 450, true, 8, 8).final_score == 98, "Existing disassembly overtime interval preserved");
        Check(TechWiseAssessmentScoring.Calculate(settings, mistakes, int.MaxValue, false, 8, 8).final_score == 80, "Time penalty capped at twenty");
        var minor = new TechWiseVrMistakeDetail { kind = "incorrect_orientation" };
        var critical = new TechWiseVrMistakeDetail { kind = "missing_dependency" };
        TechWiseAssessmentScoring.Describe(minor, settings, 1, 1.2);
        TechWiseAssessmentScoring.Describe(critical, settings, 2, 2.2);
        Check(critical.score_penalty > minor.score_penalty && critical.order == 2, "Structured severity and ordering applied");
        mistakes.Add(minor);
        var before = TechWiseAssessmentScoring.Calculate(settings, mistakes, 50, false, 8, 8).final_score;
        mistakes.Add(critical);
        Check(TechWiseAssessmentScoring.Calculate(settings, mistakes, 50, false, 8, 8).final_score < before, "More mistakes reduce score even on fast attempt");
        for (int i=0; i<50; i++) mistakes.Add(critical);
        Check(TechWiseAssessmentScoring.Calculate(settings, mistakes, 1, false, 8, 8).final_score == 0, "Rushed error-heavy attempt scores zero, never negative");
        Check(TechWiseAssessmentScoring.Calculate(settings, new TechWiseVrMistakeDetail[0], 1, false, 0, 8).final_score == 0, "Empty assessment cannot earn speed points");
        var snapshot = settings.Snapshot(); settings.standardPenalty = 99;
        Check(snapshot.standardPenalty == 8, "Attempt scoring configuration is a copy");
        var malformed = new TechWiseAssessmentScoringSettings { overtimeIntervalSeconds = 0, maximumTimePenalty = 999 };
        Check(TechWiseAssessmentScoring.Calculate(malformed, new TechWiseVrMistakeDetail[0], int.MaxValue, false, 8, 8).final_score == 80, "Malformed configuration stays bounded");
        File.WriteAllText("Logs/Batch2/core-results.txt", $"PASS: {checks} core assertions");
    }

    static IEnumerator Frames(int count)
    {
        for (int frame=0; frame<count; frame++) yield return null;
    }

    static IEnumerator Load(string activity, string mode)
    {
        TechWiseSimulationModeManager.SetMode(activity, mode);
        SceneManager.LoadScene("Multiplayer");
        for (int frame=0; frame<30; frame++) yield return null;
        for (int frame=0; frame<120 && TechWiseDisassemblyRuntime.IsPreparing; frame++) yield return null;
        Check(!TechWiseDisassemblyRuntime.IsPreparing, "Scene preparation ends: " + activity + " " + mode);
    }

    static IEnumerator SimulationTests()
    {
        // This runner advances nested enumerators itself through Flatten below.
        return Flatten(Scenarios());
    }
    static IEnumerator Flatten(IEnumerator root)
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            if (!current.MoveNext()) { stack.Pop(); continue; }
            if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
            yield return current.Current;
        }
    }
    static TechWiseVrAttemptPayload LastPayload()
    {
        var queue = (TechWiseAttemptQueueFile)typeof(TechWiseOfflineAttemptQueue).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(TechWiseOfflineAttemptQueue.Instance);
        return queue.attempts[queue.attempts.Count-1].payload;
    }
    static IEnumerator RemovePart(TechWiseSimulationRuntime state, XRGrabInteractable part)
    {
        var ray = new GameObject("Batch2 simulated input").AddComponent<XRRayInteractor>();
        ray.interactionLayers = ~0;
        ray.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
        ray.StartManualInteraction((IXRSelectInteractable)part);
        Check(ray.IsSelecting(part), "XRI acquires part: " + state.StepOf(part));
        // Deterministic input fixture: exercises XRI ownership/release, not physical controller tracking.
        var away = new Vector3(20 + state.Parts.IndexOf(part), 2, 20);
        part.transform.position = away;
        ray.EndManualInteraction();
        part.transform.position = away;
        var body = part.GetComponent<Rigidbody>();
        if (body != null) { if (!body.isKinematic) body.linearVelocity = Vector3.zero; body.isKinematic = true; body.position = away; }
        Physics.SyncTransforms();
        UnityEngine.Object.Destroy(ray.gameObject);
        yield return Frames(3);
        var touched = (System.Collections.Generic.HashSet<XRGrabInteractable>)typeof(TechWiseSimulationRuntime)
            .GetField("touched", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        File.AppendAllText("Logs/Batch2/removal-diagnostics.txt", state.StepOf(part) + " position=" + part.transform.position +
            " held=" + TechWiseSimulationRuntime.IsHeld(part) + " installed=" + state.IsPartInstalled(part) + " touched=" + touched.Contains(part) + "\n");
    }
    static IEnumerator Scenarios()
    {
        yield return Frames(25);
        var state = TechWiseSimulationRuntime.Instance;
        Check(state.ConfigurationError == null && !state.ManipulationLocked, "Practice starts usable");
        Check(GameObject.Find("TechWise Practice Side Guide") != null, "Practice side panel survives");
        Check(GameObject.Find("Practice component indicator") != null, "Practice marker survives");
        state.RecordMistake("incorrect_orientation", "CPU", "Practice orientation feedback", "Rotate the CPU");
        Check(state.PracticeMistakes.Count == 1 && state.Feedback.Contains("Practice orientation"), "Practice still explains mistakes immediately");
        foreach (var step in TechWiseSimulationModeManager.AssemblyOrder)
        {
            Check(TechWiseDisassemblyRuntime.InstallStepIntoSocket(step), "Practice assembly placement: " + step);
            yield return Frames(2);
        }
        Check(state.PracticeComplete, "Practice assembly steps complete");
        yield return Load("disassembly", "practice");
        state = TechWiseSimulationRuntime.Instance;
        Check(state.IsInstalled("RAM") && GameObject.Find("TechWise Practice Side Guide") != null, "Practice disassembly setup and guidance survive");
        foreach (var part in state.Parts.Where(p => state.StepOf(p) == "CPUCooler").ToArray()) yield return RemovePart(state, part);
        Check(state.IsStepComplete("CPUCooler"), "Practice disassembly step progresses");

        foreach (var activity in new[] { "assembly", "disassembly" })
        {
            yield return Load(activity, "competition");
            state = TechWiseSimulationRuntime.Instance;
            var recorder = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
            Check(state.ManipulationLocked, "Assessment locked before Start: " + activity);
            Check(GameObject.Find("TechWise Practice Side Guide") == null && GameObject.Find("Practice component indicator") == null,
                "Assessment has no practice panel or marker: " + activity);
            Check(state.Sockets.All(s => !s.showInteractableHoverMeshes && !s.hoverSocketSnapping), "Assessment socket answer previews disabled");
            Invoke(recorder, "BeginAttempt");
            var id = recorder.CurrentAttemptId;
            Check(!string.IsNullOrEmpty(id) && !state.ManipulationLocked, "Official Start creates identity and unlocks input");
            Invoke(recorder, "BeginAttempt");
            Check(recorder.CurrentAttemptId == id, "Repeated Start preserves ID");
            var runningClock = (TechWiseAssessmentClock)typeof(TechWiseAttemptRecorder).GetField("attemptClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recorder);
            var beforePause = runningClock.Elapsed(Time.realtimeSinceStartupAsDouble);
            var oldScale = Time.timeScale;
            Time.timeScale = 0;
            yield return Frames(3);
            var afterPause = runningClock.Elapsed(Time.realtimeSinceStartupAsDouble);
            Time.timeScale = oldScale;
            Check(afterPause > beforePause, "Assessment time continues with timeScale zero");
            var feedbackBefore = state.Feedback;
            state.RecordMistake("incorrect_orientation", "CPU", "Secret assessment explanation", "Secret correction");
            TechWiseAttemptRecorder.TryGetSnapshot(out var snapshot);
            Check(snapshot.mistakes == 0 && snapshot.completedSteps == 0 && state.Feedback == feedbackBefore && state.PracticeMistakes.Count == 0,
                "Assessment records internally without revealing correctness");
            Check(!UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude).Any(t => t.text.Contains("Secret assessment")), "No rendered text receives immediate mistake explanation");
            // Additive scene callbacks must not recreate the active attempt or clear the workbench.
            var additive = SceneManager.CreateScene("Batch2 additive");
            var callback = typeof(TechWiseAttemptRecorder).GetMethod("OnSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
            callback.Invoke(recorder, new object[] { additive, LoadSceneMode.Additive });
            Check(recorder.CurrentAttemptId == id, "Additive scene notification does not reset attempt");
            SceneManager.UnloadSceneAsync(additive);
            if (activity == "assembly")
                foreach (var step in TechWiseSimulationModeManager.AssemblyOrder)
                {
                    Check(TechWiseDisassemblyRuntime.InstallStepIntoSocket(step), "Assessment assembly placement: " + step);
                    yield return Frames(2);
                }
            else
                foreach (var step in TechWiseSimulationModeManager.DisassemblyOrder)
                    foreach (var part in state.Parts.Where(p => state.StepOf(p) == step).ToArray()) yield return RemovePart(state, part);

            TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
            Check(snapshot.status == "Running" && !snapshot.resultsReady, "Complete work never auto-submits: " + activity);
            var queueCount = TechWiseOfflineAttemptQueue.PendingCount;
            Invoke(recorder, "FinishAttempt");
            TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
            var payload = LastPayload();
            File.WriteAllText("Logs/Batch2/" + activity + "-result.json", JsonUtility.ToJson(payload, true));
            Check(snapshot.resultsRevealed && state.ManipulationLocked && snapshot.score == 96, "DONE locks, validates and scores orientation error: " + activity);
            Check(payload.metadata.local_attempt_id == id && payload.metadata.component_results.All(c => c.complete), "Result preserves Start ID and complete component snapshot");
            Check(payload.metadata.mistake_details.Length == 1 && payload.metadata.mistake_details[0].category == "Incorrect Orientation", "Review carries actual mistake category");
            Check(GameObject.Find("View Mistakes Button") != null && GameObject.Find("Return Results Button") != null, "Result has explicit review and Return controls");
            Invoke(recorder, "ToggleMistakeReview");
            Check(GameObject.Find("Mistake Review Scroll") != null, "Mistake review opens on demand");
            var duration = snapshot.durationSeconds; var score = snapshot.score;
            Invoke(recorder, "FinishAttempt");
            TechWiseOfflineAttemptQueue.Enqueue(payload);
            Check(TechWiseOfflineAttemptQueue.PendingCount == queueCount + 1, "DONE and enqueue retries preserve one result");
            var frozen = JsonUtility.ToJson(payload);
            state.RecordMistake("wrong_part", "GPU", "late mutation", "late mutation");
            yield return Frames(6);
            TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
            Check(snapshot.durationSeconds == duration && snapshot.score == score && JsonUtility.ToJson(LastPayload()) == frozen, "Submitted timer, score and payload remain frozen");
            var partToGrab = state.Parts.First(p => state.StepOf(p) == "CPU");
            var blockedRay = new GameObject("Blocked assessment input").AddComponent<XRRayInteractor>(); blockedRay.interactionLayers = ~0;
            Check(!partToGrab.interactionManager.IsSelectPossible((IXRSelectInteractor)blockedRay, (IXRSelectInteractable)partToGrab), "Shared select filter rejects post-submission grabbing");
            UnityEngine.Object.Destroy(blockedRay.gameObject);
            PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
            yield return Frames(3);
            var hud = GameObject.Find("TechWise Competition HUD").GetComponent<Canvas>();
            Check(hud.renderMode == RenderMode.WorldSpace, "Submitted assessment HUD supports switching to VR");
            PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue);
            yield return Frames(3);
            Check(hud.renderMode == RenderMode.ScreenSpaceOverlay, "Submitted HUD switches back to desktop");
        }

        yield return Load("assembly", "competition");
        state = TechWiseSimulationRuntime.Instance;
        var incomplete = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        Invoke(incomplete, "BeginAttempt"); Invoke(incomplete, "FinishAttempt");
        Check(LastPayload().score_percent == 0 && LastPayload().metadata.mistake_details.Count(m => m.kind == "missing_component" && m.attempted_step.StartsWith("RAM module")) == 4,
            "Empty assembly scores zero and identifies each missing RAM module");
        yield return Load("disassembly", "competition");
        incomplete = UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();
        Invoke(incomplete, "BeginAttempt");
        state = TechWiseSimulationRuntime.Instance;
        var heldCpu = state.FindPart("CPU");
        var holdingRay = new GameObject("Held at DONE").AddComponent<XRRayInteractor>(); holdingRay.interactionLayers = ~0;
        holdingRay.transform.SetPositionAndRotation(heldCpu.transform.position, heldCpu.transform.rotation);
        holdingRay.StartManualInteraction((IXRSelectInteractable)heldCpu);
        Check(holdingRay.IsSelecting(heldCpu), "Disassembly part held at submission");
        heldCpu.transform.position = new Vector3(30, 2, 30);
        Invoke(incomplete, "FinishAttempt");
        Check(!holdingRay.hasSelection && !LastPayload().metadata.component_results.First(c => c.step == "CPU").complete,
            "DONE cancels grab without converting held part into completed removal");
        UnityEngine.Object.Destroy(holdingRay.gameObject);
        Check(LastPayload().score_percent == 0 && LastPayload().metadata.mistake_details.All(m => m.kind == "incomplete_removal"), "Unremoved disassembly scores zero with correct review categories");
    }
}
