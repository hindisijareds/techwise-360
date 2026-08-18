using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-7600)]
public sealed class TechWiseAttemptRecorder : MonoBehaviour
{
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const string StartPromptName = "TechWise Competition Start Prompt";
    const string BlackboardName = "School blackboards";
    const string GuideBoardCanvasName = "TechWise Blackboard Steps";
    const float DisassemblyMovedAwayDistance = 0.32f;
    const float ScoringGraceSeconds = 0.35f;
    const float DisassemblyArmSeconds = 0.75f;
    const float MistakeCooldownSeconds = 1.5f;
    const int WrongStepPenalty = 8;
    const int ResetPenalty = 3;
    const int MaxTimePenalty = 20;

    readonly List<XRGrabInteractable> grabInteractables = new();
    readonly List<XRLockSocketInteractor> sockets = new();
    readonly Dictionary<XRGrabInteractable, string> stepByInteractable = new();
    readonly Dictionary<string, XRGrabInteractable> interactableByStep = new();
    readonly Dictionary<XRLockSocketInteractor, string> stepBySocket = new();
    readonly Dictionary<string, XRLockSocketInteractor> socketByStep = new();
    readonly Dictionary<string, Vector3> disassemblySocketPositions = new();
    readonly Dictionary<string, float> mistakeCooldowns = new();
    readonly List<string> availableOrder = new();
    readonly List<string> completedOrder = new();
    readonly List<string> skippedSteps = new();
    readonly HashSet<string> completedSteps = new();
    readonly HashSet<string> skippedStepSet = new();
    readonly HashSet<string> playerTouchedSteps = new();
    readonly HashSet<string> disassemblyRemovalCandidates = new();

    Canvas hudCanvas;
    TMP_Text hudText;
    GameObject startPromptObject;
    Button startPromptButton;
    TMP_Text startPromptText;
    float attemptStartRealtime;
    float disassemblyArmedRealtime;
    float scoringEnabledRealtime;
    int lastDurationSeconds;
    DateTime attemptStartedAtUtc;
    bool attemptReady;
    bool attemptActive;
    bool attemptFinished;
    bool pendingDisassemblyRebaseline;
    int wrongOrderCount;
    int wrongPartCount;
    int resetCount;
    string lastAttemptId;
    string lastCompletionSummary;
    string lastHudExtraLine;

    public readonly struct AttemptSnapshot
    {
        public readonly bool available;
        public readonly string activity;
        public readonly string status;
        public readonly int durationSeconds;
        public readonly int mistakes;
        public readonly int completedSteps;
        public readonly int totalSteps;

        public AttemptSnapshot(
            bool available,
            string activity,
            string status,
            int durationSeconds,
            int mistakes,
            int completedSteps,
            int totalSteps)
        {
            this.available = available;
            this.activity = activity;
            this.status = status;
            this.durationSeconds = durationSeconds;
            this.mistakes = mistakes;
            this.completedSteps = completedSteps;
            this.totalSteps = totalSteps;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseAttemptRecorder>() != null)
            return;

        var recorderObject = new GameObject("TechWise Attempt Recorder");
        DontDestroyOnLoad(recorderObject);
        recorderObject.AddComponent<TechWiseAttemptRecorder>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TechWiseComponentRecovery.ComponentRecovered += OnComponentRecovered;
        TechWiseOfflineAttemptQueue.QueueChanged += OnQueueChanged;
        RefreshScene();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TechWiseComponentRecovery.ComponentRecovered -= OnComponentRecovered;
        TechWiseOfflineAttemptQueue.QueueChanged -= OnQueueChanged;
        ClearListeners();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshScene();
    }

    void Update()
    {
        if (attemptReady && !attemptActive && !attemptFinished)
        {
            if (WasStartPressedThisFrame())
                BeginAttempt();
            else if (TechWiseSimulationModeManager.IsDisassembly && TechWiseDisassemblyRuntime.IsPreparing)
                UpdateReadyDisplay("Preparing the PC for disassembly. Wait for the Start prompt.");
            else if (pendingDisassemblyRebaseline)
            {
                RebaselineDisassemblyPositions();
                UpdateReadyDisplay("Ready. Click Start Competition with the crosshair or press Enter/Space.");
            }

            return;
        }

        if (!attemptActive)
            return;

        if (TechWiseSimulationModeManager.IsDisassembly)
            CheckMovedDisassemblyParts();

        UpdateHud();
    }

    void RefreshScene()
    {
        ClearListeners();
        HideHud();
        HideStartPrompt();

        if (!IsGameplayScene(SceneManager.GetActiveScene().name) || !TechWiseSimulationModeManager.IsCompetitionMode)
            return;

        RegisterSceneParts();
        PrepareAttempt();
    }

    void RegisterSceneParts()
    {
        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
        {
            var stepId = TechWiseSimulationModeManager.ResolveStepId(interactable.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            grabInteractables.Add(interactable);
            stepByInteractable[interactable] = stepId;
            if (!interactableByStep.ContainsKey(stepId))
                interactableByStep[stepId] = interactable;

            interactable.selectEntered.AddListener(OnGrabSelected);
            interactable.selectExited.AddListener(OnGrabReleased);
        }

        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            var stepId = TechWiseSimulationModeManager.ResolveStepId(socket.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            sockets.Add(socket);
            stepBySocket[socket] = stepId;
            if (!socketByStep.ContainsKey(stepId))
                socketByStep[stepId] = socket;

            socket.selectEntered.AddListener(OnSocketSelected);
            socket.selectExited.AddListener(OnSocketExited);

            var attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
            disassemblySocketPositions[stepId] = attach.position;
        }
    }

    void PrepareAttempt()
    {
        availableOrder.Clear();
        completedOrder.Clear();
        completedSteps.Clear();
        skippedSteps.Clear();
        skippedStepSet.Clear();
        mistakeCooldowns.Clear();
        playerTouchedSteps.Clear();
        disassemblyRemovalCandidates.Clear();
        wrongOrderCount = 0;
        wrongPartCount = 0;
        resetCount = 0;
        attemptReady = false;
        attemptActive = false;
        attemptFinished = false;
        lastDurationSeconds = 0;
        lastAttemptId = string.Empty;
        lastCompletionSummary = string.Empty;
        lastHudExtraLine = string.Empty;
        pendingDisassemblyRebaseline = false;
        attemptStartRealtime = Time.realtimeSinceStartup;
        attemptStartedAtUtc = DateTime.UtcNow;

        foreach (var stepId in TechWiseSimulationModeManager.GetExpectedOrder())
        {
            if (interactableByStep.ContainsKey(stepId) && socketByStep.ContainsKey(stepId))
                availableOrder.Add(stepId);
            else
                AddSkippedStep(stepId);
        }

        EnsureHud();

        if (availableOrder.Count == 0)
        {
            UpdateHud("No matching PC parts and sockets were found for this competition.");
            return;
        }

        if (TechWiseSimulationModeManager.IsDisassembly)
        {
            pendingDisassemblyRebaseline = TechWiseDisassemblyRuntime.IsPreparing;
            if (!pendingDisassemblyRebaseline)
                RebaselineDisassemblyPositions();
        }

        attemptReady = true;
        ShowStartPrompt();
        UpdateReadyDisplay("Ready. Click Start Competition with the crosshair or press Enter/Space.");
    }

    void BeginAttempt()
    {
        if (!attemptReady || attemptActive || attemptFinished)
            return;

        if (TechWiseSimulationModeManager.IsDisassembly && TechWiseDisassemblyRuntime.IsPreparing)
        {
            UpdateReadyDisplay("Preparing the PC for disassembly. Start will unlock when setup is finished.");
            return;
        }

        if (pendingDisassemblyRebaseline)
            RebaselineDisassemblyPositions();

        completedOrder.Clear();
        completedSteps.Clear();
        mistakeCooldowns.Clear();
        playerTouchedSteps.Clear();
        disassemblyRemovalCandidates.Clear();
        wrongOrderCount = 0;
        wrongPartCount = 0;
        resetCount = 0;
        lastDurationSeconds = 0;
        attemptReady = false;
        attemptActive = true;
        attemptFinished = false;
        attemptStartRealtime = Time.realtimeSinceStartup;
        attemptStartedAtUtc = DateTime.UtcNow;
        scoringEnabledRealtime = Time.realtimeSinceStartup + ScoringGraceSeconds;
        disassemblyArmedRealtime = Time.realtimeSinceStartup + DisassemblyArmSeconds;

        HideStartPrompt();
        UpdateHud("Run started. Step hints are hidden for competition mode.");
    }

    void ClearListeners()
    {
        foreach (var socket in sockets)
        {
            if (socket == null)
                continue;

            socket.selectEntered.RemoveListener(OnSocketSelected);
            socket.selectExited.RemoveListener(OnSocketExited);
        }

        foreach (var interactable in grabInteractables)
        {
            if (interactable == null)
                continue;

            interactable.selectEntered.RemoveListener(OnGrabSelected);
            interactable.selectExited.RemoveListener(OnGrabReleased);
        }

        grabInteractables.Clear();
        sockets.Clear();
        stepByInteractable.Clear();
        interactableByStep.Clear();
        stepBySocket.Clear();
        socketByStep.Clear();
        disassemblySocketPositions.Clear();
        availableOrder.Clear();
        completedOrder.Clear();
        completedSteps.Clear();
        skippedSteps.Clear();
        skippedStepSet.Clear();
        mistakeCooldowns.Clear();
        playerTouchedSteps.Clear();
        disassemblyRemovalCandidates.Clear();
        attemptReady = false;
        attemptActive = false;
        pendingDisassemblyRebaseline = false;
    }

    void OnGrabSelected(SelectEnterEventArgs args)
    {
        if (!attemptActive || attemptFinished || args?.interactorObject is XRSocketInteractor)
            return;

        var interactable = args?.interactableObject as XRGrabInteractable;
        var stepId = interactable != null && stepByInteractable.TryGetValue(interactable, out var fromPart)
            ? fromPart
            : null;

        if (string.IsNullOrEmpty(stepId) || completedSteps.Contains(stepId) || !availableOrder.Contains(stepId))
            return;

        playerTouchedSteps.Add(stepId);
        if (TechWiseSimulationModeManager.IsDisassembly)
            disassemblyRemovalCandidates.Add(stepId);
    }

    void OnGrabReleased(SelectExitEventArgs args)
    {
        if (!attemptActive || attemptFinished || !TechWiseSimulationModeManager.IsDisassembly)
            return;

        if (args?.interactorObject is XRSocketInteractor)
            return;

        var interactable = args?.interactableObject as XRGrabInteractable;
        var stepId = interactable != null && stepByInteractable.TryGetValue(interactable, out var fromPart)
            ? fromPart
            : null;

        if (string.IsNullOrEmpty(stepId) || !disassemblyRemovalCandidates.Contains(stepId))
            return;

        TryRecordMovedDisassemblyStep(stepId);
    }

    void OnSocketSelected(SelectEnterEventArgs args)
    {
        if (!attemptActive || attemptFinished || !TechWiseSimulationModeManager.IsAssembly)
            return;

        var socket = args?.interactorObject as XRLockSocketInteractor;
        var interactable = args?.interactableObject as XRGrabInteractable;
        var socketStep = socket != null && stepBySocket.TryGetValue(socket, out var fromSocket) ? fromSocket : null;
        var interactableStep = interactable != null && stepByInteractable.TryGetValue(interactable, out var fromPart) ? fromPart : null;

        if (!string.IsNullOrEmpty(socketStep) && !string.IsNullOrEmpty(interactableStep) && socketStep != interactableStep)
        {
            if (RecordMistake("wrong_part", $"{interactableStep}->{socketStep}"))
                UpdateHud($"Wrong part for that slot. Mistakes: {MistakeCount}");
            return;
        }

        RecordStep(socketStep ?? interactableStep);
    }

    void OnSocketExited(SelectExitEventArgs args)
    {
        if (!attemptActive || attemptFinished || !TechWiseSimulationModeManager.IsDisassembly)
            return;

        var socket = args?.interactorObject as XRLockSocketInteractor;
        var interactable = args?.interactableObject as XRGrabInteractable;
        var socketStep = socket != null && stepBySocket.TryGetValue(socket, out var fromSocket) ? fromSocket : null;
        var interactableStep = interactable != null && stepByInteractable.TryGetValue(interactable, out var fromPart) ? fromPart : null;
        var stepId = socketStep ?? interactableStep;

        if (string.IsNullOrEmpty(stepId) || !playerTouchedSteps.Contains(stepId))
            return;

        RecordStep(stepId);
    }

    void CheckMovedDisassemblyParts()
    {
        if (Time.realtimeSinceStartup < disassemblyArmedRealtime)
            return;

        foreach (var stepId in availableOrder)
        {
            if (completedSteps.Contains(stepId))
                continue;

            if (!interactableByStep.TryGetValue(stepId, out var interactable) ||
                !disassemblySocketPositions.TryGetValue(stepId, out var socketPosition))
            {
                continue;
            }

            if (!disassemblyRemovalCandidates.Contains(stepId))
                continue;

            if (IsSelectedBySocket(interactable))
                continue;

            var distance = Vector3.Distance(interactable.transform.position, socketPosition);
            if (distance >= DisassemblyMovedAwayDistance)
                RecordStep(stepId);
        }
    }

    void TryRecordMovedDisassemblyStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId) ||
            completedSteps.Contains(stepId) ||
            !disassemblyRemovalCandidates.Contains(stepId) ||
            !interactableByStep.TryGetValue(stepId, out var interactable) ||
            !disassemblySocketPositions.TryGetValue(stepId, out var socketPosition))
        {
            return;
        }

        if (Vector3.Distance(interactable.transform.position, socketPosition) >= DisassemblyMovedAwayDistance)
            RecordStep(stepId);
    }

    void RecordStep(string stepId)
    {
        if (!attemptActive || string.IsNullOrEmpty(stepId) || completedSteps.Contains(stepId) || !availableOrder.Contains(stepId))
            return;

        var expectedStep = GetCurrentExpectedStep();
        if (!string.IsNullOrEmpty(expectedStep) && stepId != expectedStep)
            RecordMistake("wrong_order", $"{stepId}_before_{expectedStep}");

        completedSteps.Add(stepId);
        completedOrder.Add(stepId);

        if (completedSteps.Count >= availableOrder.Count)
            FinishAttempt();
        else
            UpdateHud();
    }

    bool RecordMistake(string kind, string key)
    {
        if (!attemptActive || attemptFinished || Time.realtimeSinceStartup < scoringEnabledRealtime)
            return false;

        if (kind == "reset")
            return false;

        var cooldownKey = $"{kind}:{key}";
        if (mistakeCooldowns.TryGetValue(cooldownKey, out var lastRecordedAt) &&
            Time.realtimeSinceStartup - lastRecordedAt < MistakeCooldownSeconds)
        {
            return false;
        }

        mistakeCooldowns[cooldownKey] = Time.realtimeSinceStartup;

        if (kind == "reset")
            resetCount++;
        else if (kind == "wrong_part")
            wrongPartCount++;
        else
            wrongOrderCount++;

        return true;
    }

    void OnComponentRecovered(string stepId)
    {
        if (!attemptActive || attemptFinished || string.IsNullOrEmpty(stepId) || completedSteps.Contains(stepId))
            return;

        if (!playerTouchedSteps.Contains(stepId))
            return;

        var counted = RecordMistake("reset", stepId);
        if (TechWiseSimulationModeManager.IsDisassembly)
        {
            TechWiseDisassemblyRuntime.InstallStepIntoSocket(stepId);
            disassemblyArmedRealtime = Time.realtimeSinceStartup + DisassemblyArmSeconds;
        }

        if (counted)
            UpdateHud($"Part reset recorded. Mistakes: {MistakeCount}");
    }

    void FinishAttempt()
    {
        if (attemptFinished)
            return;

        attemptFinished = true;
        attemptActive = false;
        attemptReady = false;

        var completedAt = DateTime.UtcNow;
        var durationSeconds = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - attemptStartRealtime));
        lastDurationSeconds = durationSeconds;
        var mistakePenalty = wrongOrderCount * WrongStepPenalty + wrongPartCount * WrongStepPenalty + resetCount * ResetPenalty;
        var overtimeSeconds = Mathf.Max(0, durationSeconds - TechWiseSimulationModeManager.TargetSecondsForCurrentMode());
        var timePenalty = Mathf.Min(MaxTimePenalty, (overtimeSeconds / 30) * 2);
        var score = CalculateScore(durationSeconds);
        var localAttemptId = Guid.NewGuid().ToString("N");

        var payload = new TechWiseVrAttemptPayload
        {
            simulation_type = TechWiseSimulationModeManager.SimulationType,
            competition_id = TechWiseSimulationModeManager.CompetitionId,
            score_percent = score,
            duration_seconds = durationSeconds,
            mistakes = MistakeCount,
            started_at = attemptStartedAtUtc.ToString("o"),
            completed_at = completedAt.ToString("o"),
            metadata = new TechWiseVrAttemptMetadata
            {
                local_attempt_id = localAttemptId,
                mode = TechWiseSimulationModeManager.CompetitionMode,
                expected_order = availableOrder.ToArray(),
                completed_order = completedOrder.ToArray(),
                skipped_steps = skippedSteps.ToArray(),
                wrong_order_count = wrongOrderCount,
                wrong_part_count = wrongPartCount,
                reset_count = resetCount,
                time_penalty = timePenalty,
                mistake_penalty = mistakePenalty,
                game_version = Application.version,
                control_mode = PlayerPrefs.GetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue),
                offline_queued = true,
            },
        };

        lastAttemptId = localAttemptId;
        lastCompletionSummary = $"Complete. Score {score}% | Time {FormatSeconds(durationSeconds)} | Mistakes {MistakeCount}";
        lastHudExtraLine = $"{lastCompletionSummary}\nSaved locally. Syncing with dashboard...";

        TechWiseOfflineAttemptQueue.Enqueue(payload);
        UpdateHud(lastHudExtraLine);
        StartCoroutine(RefreshCompletionSyncStatusCoroutine());
    }

    IEnumerator RefreshCompletionSyncStatusCoroutine()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            yield return new WaitForSecondsRealtime(1.5f);

            if (!attemptFinished || string.IsNullOrWhiteSpace(lastAttemptId))
                yield break;

            lastHudExtraLine = $"{lastCompletionSummary}\n{BuildSyncStatusLine(lastAttemptId)}";
            UpdateHud(lastHudExtraLine);

            if (!TechWiseOfflineAttemptQueue.HasPending(lastAttemptId))
                yield break;
        }
    }

    void OnQueueChanged(int pendingCount)
    {
        if (!attemptFinished || string.IsNullOrWhiteSpace(lastAttemptId))
            return;

        lastHudExtraLine = $"{lastCompletionSummary}\n{BuildSyncStatusLine(lastAttemptId)}";
        UpdateHud(lastHudExtraLine);
    }

    string BuildSyncStatusLine(string localAttemptId)
    {
        if (!TechWiseOfflineAttemptQueue.HasPending(localAttemptId))
            return "Synced to dashboard.";

        if (!TechWiseSessionStore.HasSession)
            return $"Saved locally. Log in to sync. Pending uploads: {TechWiseOfflineAttemptQueue.PendingCount}";

        if (Application.internetReachability == NetworkReachability.NotReachable)
            return $"Saved offline. Pending uploads: {TechWiseOfflineAttemptQueue.PendingCount}";

        var error = TechWiseOfflineAttemptQueue.GetLastError(localAttemptId);
        return string.IsNullOrWhiteSpace(error)
            ? $"Upload pending. Pending uploads: {TechWiseOfflineAttemptQueue.PendingCount}"
            : $"Upload pending: {error}";
    }

    int MistakeCount => wrongOrderCount + wrongPartCount + resetCount;

    public static bool TryGetSnapshot(out AttemptSnapshot snapshot)
    {
        var recorder = FindAnyObjectByType<TechWiseAttemptRecorder>();
        if (recorder == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = recorder.CreateSnapshot();
        return snapshot.available;
    }

    AttemptSnapshot CreateSnapshot()
    {
        var hasRunContext = TechWiseSimulationModeManager.IsCompetitionMode ||
            attemptReady ||
            attemptActive ||
            attemptFinished ||
            availableOrder.Count > 0;

        if (!hasRunContext)
            return new AttemptSnapshot(false, "Open activity", "Unavailable", 0, 0, 0, 0);

        var modeLabel = TechWiseSimulationModeManager.IsDisassembly ? "Disassembly" : "Assembly";
        var runMode = TechWiseSimulationModeManager.IsCompetitionMode ? "Competition" : "Practice";
        var state = attemptFinished ? "Complete" : attemptActive ? "Running" : attemptReady ? "Ready" : "Paused";
        var duration = attemptFinished || attemptActive
            ? Mathf.Max(0, Mathf.RoundToInt(attemptFinished ? lastDurationSeconds : Time.realtimeSinceStartup - attemptStartRealtime))
            : 0;

        return new AttemptSnapshot(
            true,
            $"{modeLabel} {runMode}",
            state,
            duration,
            MistakeCount,
            completedSteps.Count,
            availableOrder.Count);
    }

    string GetCurrentExpectedStep()
    {
        foreach (var stepId in availableOrder)
        {
            if (!completedSteps.Contains(stepId))
                return stepId;
        }

        return null;
    }

    static bool IsSelectedBySocket(XRGrabInteractable interactable)
    {
        if (interactable == null || !interactable.isSelected)
            return false;

        foreach (var interactor in interactable.interactorsSelecting)
        {
            if (interactor is XRSocketInteractor)
                return true;
        }

        return false;
    }

    void RebaselineDisassemblyPositions()
    {
        pendingDisassemblyRebaseline = false;
        disassemblySocketPositions.Clear();
        playerTouchedSteps.Clear();
        disassemblyRemovalCandidates.Clear();

        foreach (var stepId in availableOrder)
        {
            if (interactableByStep.TryGetValue(stepId, out var interactable) && interactable != null)
                disassemblySocketPositions[stepId] = interactable.transform.position;
            else if (socketByStep.TryGetValue(stepId, out var socket) && socket != null)
            {
                var attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                disassemblySocketPositions[stepId] = attach.position;
            }
        }
    }

    void EnsureHud()
    {
        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(true);
            return;
        }

        var canvasObject = new GameObject("TechWise Competition HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("HUD Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.015f, 0.72f);
        panelRect.anchorMax = new Vector2(0.36f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.05f, 0.1f, 0.92f);
        panelImage.raycastTarget = false;

        var textObject = new GameObject("HUD Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        hudText = textObject.GetComponent<TextMeshProUGUI>();
        hudText.rectTransform.anchorMin = new Vector2(0.04f, 0.08f);
        hudText.rectTransform.anchorMax = new Vector2(0.96f, 0.92f);
        hudText.rectTransform.offsetMin = Vector2.zero;
        hudText.rectTransform.offsetMax = Vector2.zero;
        hudText.enableAutoSizing = true;
        hudText.fontSizeMin = 14f;
        hudText.fontSizeMax = 26f;
        hudText.textWrappingMode = TextWrappingModes.Normal;
        hudText.overflowMode = TextOverflowModes.Ellipsis;
        hudText.alignment = TextAlignmentOptions.TopLeft;
        hudText.color = Color.white;
        hudText.raycastTarget = false;
    }

    void HideHud()
    {
        if (hudCanvas != null)
            hudCanvas.gameObject.SetActive(false);
    }

    void ShowStartPrompt()
    {
        if (startPromptObject != null)
        {
            startPromptObject.SetActive(true);
            UpdateStartPromptText();
            return;
        }

        var camera = ResolveCamera();
        ResolveStartPromptPose(camera, out var promptPosition, out var promptRotation, out var promptScale, out var promptSize);

        startPromptObject = new GameObject(StartPromptName);
        startPromptObject.transform.SetPositionAndRotation(promptPosition, promptRotation);

        var canvasObject = new GameObject("Start Prompt Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(startPromptObject.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 2000;
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = promptSize;
        canvasRect.localScale = Vector3.one * promptScale;

        var panel = new GameObject("Start Prompt Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var image = panel.GetComponent<Image>();
        image.color = new Color(0.02f, 0.05f, 0.1f, 0f);
        image.raycastTarget = false;

        var buttonObject = new GameObject("Start Competition Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.27f, 0.34f);
        buttonRect.anchorMax = new Vector2(0.73f, 0.52f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color32(18, 101, 210, 255);
        startPromptButton = buttonObject.GetComponent<Button>();
        startPromptButton.targetGraphic = buttonImage;
        startPromptButton.onClick.AddListener(BeginAttempt);

        var textObject = new GameObject("Start Prompt Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        startPromptText = textObject.GetComponent<TextMeshProUGUI>();
        startPromptText.rectTransform.anchorMin = Vector2.zero;
        startPromptText.rectTransform.anchorMax = Vector2.one;
        startPromptText.rectTransform.offsetMin = Vector2.zero;
        startPromptText.rectTransform.offsetMax = Vector2.zero;
        startPromptText.enableAutoSizing = true;
        startPromptText.fontSizeMin = 18f;
        startPromptText.fontSizeMax = 34f;
        startPromptText.alignment = TextAlignmentOptions.Center;
        startPromptText.textWrappingMode = TextWrappingModes.Normal;
        startPromptText.color = Color.white;
        startPromptText.raycastTarget = false;

        var helperTextObject = new GameObject("Start Prompt Helper", typeof(RectTransform), typeof(TextMeshProUGUI));
        helperTextObject.transform.SetParent(panel.transform, false);
        var helperText = helperTextObject.GetComponent<TextMeshProUGUI>();
        helperText.rectTransform.anchorMin = new Vector2(0.12f, 0.58f);
        helperText.rectTransform.anchorMax = new Vector2(0.88f, 0.82f);
        helperText.rectTransform.offsetMin = Vector2.zero;
        helperText.rectTransform.offsetMax = Vector2.zero;
        helperText.enableAutoSizing = true;
        helperText.fontSizeMin = 18f;
        helperText.fontSizeMax = 30f;
        helperText.alignment = TextAlignmentOptions.Center;
        helperText.textWrappingMode = TextWrappingModes.Normal;
        helperText.color = Color.white;
        helperText.raycastTarget = false;
        helperText.text = "Competition is ready.\nThe timer starts when you press Start.";

        UpdateStartPromptText();
    }

    void HideStartPrompt()
    {
        if (startPromptButton != null)
            startPromptButton.onClick.RemoveListener(BeginAttempt);

        if (startPromptObject != null)
            Destroy(startPromptObject);

        startPromptObject = null;
        startPromptButton = null;
        startPromptText = null;
    }

    void UpdateReadyDisplay(string line)
    {
        lastHudExtraLine = line;
        UpdateHud(line);
        UpdateStartPromptText();
    }

    void UpdateStartPromptText()
    {
        if (startPromptText == null)
            return;

        var modeLabel = TechWiseSimulationModeManager.IsDisassembly ? "Disassembly" : "Assembly";
        startPromptText.text = TechWiseSimulationModeManager.IsDisassembly && TechWiseDisassemblyRuntime.IsPreparing
            ? "Preparing..."
            : $"Start {modeLabel} Competition";

        if (startPromptButton != null)
            startPromptButton.interactable = !(TechWiseSimulationModeManager.IsDisassembly && TechWiseDisassemblyRuntime.IsPreparing);
    }

    static void ResolveStartPromptPose(Camera camera, out Vector3 position, out Quaternion rotation, out float scale, out Vector2 size)
    {
        size = new Vector2(1100f, 620f);
        scale = 0.003f;

        var surface = ResolveVideoSimulationSurface() ?? ResolveBlackboard();
        if (surface != null)
        {
            var bounds = CalculateRendererBounds(surface, GuideBoardCanvasName);
            var center = bounds.center;
            var facing = surface.forward;

            if (camera != null)
            {
                var toCamera = camera.transform.position - center;
                if (toCamera.sqrMagnitude > 0.01f)
                    facing = toCamera.normalized;
            }

            position = center + facing * 0.055f;
            rotation = Quaternion.LookRotation(-facing, Vector3.up);
            return;
        }

        position = camera != null
            ? camera.transform.position + camera.transform.forward * 1.8f + Vector3.down * 0.12f
            : new Vector3(0f, 1.55f, 1.8f);
        rotation = camera != null
            ? Quaternion.LookRotation(camera.transform.position - position, Vector3.up)
            : Quaternion.identity;
        size = new Vector2(620f, 280f);
        scale = 0.0025f;
    }

    static Transform ResolveBlackboard()
    {
        var named = FindNamedTransform(BlackboardName);
        if (named != null)
            return named;

        var welcomeBoard = FindBoardContainingText("Welcome to Tutorial Module");
        if (welcomeBoard != null)
            return welcomeBoard;

        var cautionBoard = FindBoardContainingText("CAUTION");
        if (cautionBoard != null)
            return cautionBoard;

        return FindBoardContainingText("VR Competition");
    }

    static Transform ResolveVideoSimulationSurface()
    {
        var realLifeBoard = FindBoardContainingText("Real Life Simulation") ??
            FindBoardContainingText("Real-life simulation video");
        if (realLifeBoard != null)
            return realLifeBoard;

        return FindNamedTransformContaining("Video Group") ??
            FindNamedTransformContaining("Video Player") ??
            FindNamedTransformContaining("Video Image");
    }

    static Transform FindNamedTransform(string exactName)
    {
        foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || !transform.gameObject.scene.IsValid() || !transform.gameObject.scene.isLoaded)
                continue;

            if (transform.name == exactName)
                return transform;
        }

        return null;
    }

    static Transform FindNamedTransformContaining(string nameFragment)
    {
        foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || !transform.gameObject.scene.IsValid() || !transform.gameObject.scene.isLoaded)
                continue;

            if (transform.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return transform;
        }

        return null;
    }

    static Transform FindBoardContainingText(string textFragment)
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            if (!text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            if (text.text.IndexOf(textFragment, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            return FindLargestRendererParent(text.transform, maxDepth: 8) ??
                FindNearestRendererParent(text.transform, maxDepth: 8) ??
                text.transform;
        }

        return null;
    }

    static Transform FindLargestRendererParent(Transform start, int maxDepth)
    {
        Transform best = null;
        var bestArea = 0f;
        var current = start;
        var depth = 0;

        while (current != null && depth <= maxDepth)
        {
            var bounds = CalculateRendererBounds(current, GuideBoardCanvasName);
            var size = bounds.size;
            var area = size.x * size.y;

            if (size.magnitude > 0.35f && size.magnitude < 8.5f && area > bestArea)
            {
                bestArea = area;
                best = current;
            }

            current = current.parent;
            depth++;
        }

        return best;
    }

    static Transform FindNearestRendererParent(Transform start, int maxDepth)
    {
        var current = start;
        var depth = 0;

        while (current != null && depth <= maxDepth)
        {
            var bounds = CalculateRendererBounds(current, GuideBoardCanvasName);
            var magnitude = bounds.size.magnitude;

            if (magnitude > 0.35f && magnitude < 8.5f)
                return current;

            current = current.parent;
            depth++;
        }

        return null;
    }

    static Bounds CalculateRendererBounds(Transform root, string excludedChildName)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(root.position, Vector3.one);

        foreach (var renderer in renderers)
        {
            if (renderer == null || IsUnderNamedParent(renderer.transform, excludedChildName))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(root.position, Vector3.one);
    }

    static bool IsUnderNamedParent(Transform transform, string excludedName)
    {
        if (string.IsNullOrWhiteSpace(excludedName))
            return false;

        var current = transform;
        while (current != null)
        {
            if (current.name == excludedName)
                return true;

            current = current.parent;
        }

        return false;
    }

    void UpdateHud(string extraLine = "")
    {
        if (hudText == null)
            return;

        var duration = attemptFinished || attemptActive
            ? Mathf.Max(0, Mathf.RoundToInt((attemptFinished ? lastDurationSeconds : Time.realtimeSinceStartup - attemptStartRealtime)))
            : 0;
        var modeLabel = TechWiseSimulationModeManager.IsDisassembly ? "Disassembly" : "Assembly";
        var state = attemptFinished ? "Complete" : attemptActive ? "Running" : attemptReady ? "Ready" : "Unavailable";

        hudText.text =
            $"{modeLabel} Competition\n" +
            $"Status: {state}\n" +
            $"Score: {CalculateScore(duration)}%   Time: {FormatSeconds(duration)}\n" +
            $"Mistakes: {MistakeCount}\n" +
            $"Progress: {completedSteps.Count}/{availableOrder.Count}\n" +
            "Step hints: hidden\n" +
            (skippedSteps.Count > 0 ? $"Skipped missing: {string.Join(", ", skippedSteps)}\n" : string.Empty) +
            (string.IsNullOrWhiteSpace(extraLine) ? string.Empty : extraLine);
    }

    void AddSkippedStep(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId) || !skippedStepSet.Add(stepId))
            return;

        skippedSteps.Add(stepId);
    }

    int CalculateScore(int durationSeconds)
    {
        var mistakePenalty = wrongOrderCount * WrongStepPenalty + wrongPartCount * WrongStepPenalty;
        var overtimeSeconds = Mathf.Max(0, durationSeconds - TechWiseSimulationModeManager.TargetSecondsForCurrentMode());
        var timePenalty = Mathf.Min(MaxTimePenalty, (overtimeSeconds / 30) * 2);
        return Mathf.Clamp(100 - mistakePenalty - timePenalty, 0, 100);
    }

    static bool WasStartPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame);
    }

    static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        return FindAnyObjectByType<Camera>();
    }

    static string FormatSeconds(int seconds)
    {
        var minutes = seconds / 60;
        var remaining = seconds % 60;
        return $"{minutes:00}:{remaining:00}";
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }
}
