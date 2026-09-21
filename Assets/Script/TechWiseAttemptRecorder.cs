using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

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
    readonly List<TechWiseVrMistakeDetail> mistakeDetails = new();
    TechWiseAssessmentScoringSettings scoring = new();
    TechWiseVrComponentResult[] submittedResults;
    TechWiseAssessmentScore submittedScore;
    internal TechWiseVrComponentResult[] SubmittedResults => submittedResults;
    internal bool CanResetPrevious => CanSubmitMonitor && TechWiseAssemblyHistory.Count > 0;

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
    bool continuousPresence = true;
    int unmountCount;
    bool presenceSampled;
    bool lastUserPresence = true;
    internal bool CanSubmitMonitor => attemptActive && !attemptFinished;
    internal void SubmitFromMonitor() => FinishAttempt();
    public bool IsReadyToStart => attemptReady && !attemptActive && !attemptFinished;
    public bool IsFinished => attemptFinished;
    public string LastCompletionSummary => lastCompletionSummary;
    public string CurrentSyncStatus => BuildSyncStatusLine(lastAttemptId);
    public void StartCompetitionAttempt() => BeginAttempt();
    public static bool LocalVerificationMode;
    public string CurrentAttemptId => lastAttemptId;

    public readonly struct AttemptSnapshot
    {
        public readonly bool available;
        public readonly string activity;
        public readonly string status;
        public readonly int durationSeconds;
        public readonly int mistakes;
        public readonly int completedSteps;
        public readonly int totalSteps;
        public readonly bool resultsReady;
        public readonly bool resultsRevealed;
        public readonly int score;

        public AttemptSnapshot(
            bool available,
            string activity,
            string status,
            int durationSeconds,
            int mistakes,
            int completedSteps,
            int totalSteps,
            bool resultsReady = true,
            bool resultsRevealed = true,
            int score = 0)
        {
            this.available = available;
            this.activity = activity;
            this.status = status;
            this.durationSeconds = durationSeconds;
            this.mistakes = mistakes;
            this.completedSteps = completedSteps;
            this.totalSteps = totalSteps;
            this.resultsReady = resultsReady;
            this.resultsRevealed = resultsRevealed;
            this.score = score;
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
        TechWiseSimulationRuntime.MistakeRecorded += OnSimulationMistake;
        RefreshScene();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TechWiseSimulationRuntime.MistakeRecorded -= OnSimulationMistake;
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
                UpdateReadyDisplay("Ready. Click START with laser, or pull Trigger / press A to begin.");
            }

            return;
        }

        if (!attemptActive)
            return;

        UpdatePresenceTracking();

        RefreshCompletedRequirements();

        PositionVrFloatingHud(false);
        UpdateHud();
    }

    void RefreshScene()
    {
        ClearListeners();
        HideHud();
        HideStartPrompt();

        if (!IsGameplayScene(SceneManager.GetActiveScene().name) || !TechWiseSimulationModeManager.IsCompetitionMode)
            return;

        StopAllCoroutines();
        StartCoroutine(PrepareWhenReady());
    }

    IEnumerator PrepareWhenReady()
    {
        while (TechWiseDetailedAssemblyRuntime.Instance == null || !TechWiseDetailedAssemblyRuntime.Instance.Ready || TechWiseDisassemblyRuntime.IsPreparing)
            yield return null;
        RegisterSceneParts(); PrepareAttempt();
    }

    void RegisterSceneParts()
    {
        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
        {
            if (interactable.GetComponent<TechWiseFastener>() != null) continue;
            var stepId = TechWiseSimulationModeManager.ResolveStepId(interactable.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            grabInteractables.Add(interactable);
            stepByInteractable[interactable] = stepId;
            if (!interactableByStep.ContainsKey(stepId))
                interactableByStep[stepId] = interactable;



        }

        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            if (socket.GetComponent<TechWiseScrewHole>() != null) continue;
            var stepId = TechWiseSimulationModeManager.ResolveStepId(socket.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            sockets.Add(socket);
            stepBySocket[socket] = stepId;
            if (!socketByStep.ContainsKey(stepId))
                socketByStep[stepId] = socket;




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

        // Missing scene content must never reduce the assessment denominator.
        availableOrder.AddRange(TechWiseSimulationModeManager.GetExpectedOrder());
        var settingsAsset = Resources.Load<TextAsset>("TechWiseAssessmentScoring");
        scoring = (settingsAsset != null ? JsonUtility.FromJson<TechWiseAssessmentScoringSettings>(settingsAsset.text) : new TechWiseAssessmentScoringSettings()).Snapshot();
        mistakeDetails.Clear(); submittedResults = null; submittedScore = null;
        TechWiseAssemblyHistory.Clear();

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
        TechWiseSimulationRuntime.Instance?.SetManipulationLocked(true);
        ShowStartPrompt();
        UpdateReadyDisplay("Ready. Click START with laser, or pull Trigger / press A to begin.");
    }

    void BeginAttempt()
    {
        if (!attemptReady || attemptActive || attemptFinished || TechWiseDetailedAssemblyRuntime.Instance == null || !TechWiseDetailedAssemblyRuntime.Instance.Ready)
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
        continuousPresence = true;
        unmountCount = 0;
        presenceSampled = false;
        lastUserPresence = true;
        attemptReady = false;
        attemptActive = true;
        attemptFinished = false;
        attemptStartRealtime = Time.realtimeSinceStartup;
        attemptStartedAtUtc = DateTime.UtcNow;
        scoringEnabledRealtime = Time.realtimeSinceStartup + ScoringGraceSeconds;
        disassemblyArmedRealtime = Time.realtimeSinceStartup + DisassemblyArmSeconds;

        TechWiseSimulationRuntime.Instance?.SetManipulationLocked(false);

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

    void OnGrabSelected(SelectEnterEventArgs args) { }
    void OnGrabReleased(SelectExitEventArgs args) { }
    void OnSocketSelected(SelectEnterEventArgs args) { }
    void OnSocketExited(SelectExitEventArgs args) { }
    void OnComponentRecovered(string stepId) { } // Recovery is not a placement attempt.

    void RefreshCompletedRequirements()
    {
        var state = TechWiseSimulationRuntime.Instance;
        completedSteps.Clear(); completedOrder.Clear();
        if (state == null) return;
        foreach (string step in availableOrder)
            if (state.IsStepComplete(step)) { completedSteps.Add(step); completedOrder.Add(step); }
    }
    void OnSimulationMistake(TechWiseVrMistakeDetail source)
    {
        if (!CanSubmitMonitor || TechWiseAssemblyHistory.Restoring || source == null) return;
        var detail = JsonUtility.FromJson<TechWiseVrMistakeDetail>(JsonUtility.ToJson(source));
        TechWiseAssessmentScoring.Describe(detail, scoring, mistakeDetails.Count + 1, Time.realtimeSinceStartup - attemptStartRealtime);
        mistakeDetails.Add(detail);
        if (detail.kind == "wrong_order") wrongOrderCount++; else wrongPartCount++;
        UpdateHud();
    }
    public void ResetToPreviousPoint()
    {
        if (!CanResetPrevious || !TechWiseAssemblyHistory.Undo()) return;
        resetCount++;
        var detail = new TechWiseVrMistakeDetail { kind="reset_previous", explanation="Previous completed action restored.", correction="Repeat that action to continue." };
        TechWiseAssessmentScoring.Describe(detail, scoring, mistakeDetails.Count + 1, Time.realtimeSinceStartup - attemptStartRealtime);
        mistakeDetails.Add(detail); RefreshCompletedRequirements(); UpdateHud(detail.explanation);
    }
    public void ResetToBeginning()
    {
        if (!TechWiseSimulationModeManager.IsCompetitionMode) return;
        attemptActive=false; attemptReady=false;

        Time.timeScale=1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void UpdatePresenceTracking()
    {
        var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (headDevice.isValid && headDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool isPresent))
        {
            if (!presenceSampled)
            {
                presenceSampled = true;
                lastUserPresence = isPresent;
                if (!isPresent)
                {
                    continuousPresence = false;
                    unmountCount = 1;
                }
            }
            else
            {
                if (lastUserPresence && !isPresent)
                {
                    continuousPresence = false;
                    unmountCount++;
                }
                lastUserPresence = isPresent;
            }
        }
    }

    void FinishAttempt()
    {
        if (!CanSubmitMonitor)
            return;

        attemptFinished = true;
        attemptActive = false;
        attemptReady = false;
        RefreshCompletedRequirements();
        var componentResults = TechWiseSimulationRuntime.Instance?.CaptureAssessmentState();
        submittedResults = componentResults;
        TechWiseSimulationRuntime.Instance?.SetManipulationLocked(true);

        var completedAt = DateTime.UtcNow;
        var durationSeconds = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - attemptStartRealtime));
        lastDurationSeconds = durationSeconds;
        submittedScore = Score(durationSeconds, componentResults);
        var mistakePenalty = submittedScore.mistake_penalty;
        var timePenalty = submittedScore.time_penalty;
        var score = submittedScore.final_score;
        var localAttemptId = Guid.NewGuid().ToString("N");
        var profile = TechWiseSessionStore.GetProfile();

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
                control_mode = MainMenu.VrModeValue,
                offline_queued = true,
                station_id = MainMenu.CurrentStationId,
                device_id = SystemInfo.deviceUniqueIdentifier,
                headset_present_continuous = continuousPresence,
                headset_unmount_count = unmountCount,
                component_results = componentResults,
                schema_version = 2, elapsed_seconds = durationSeconds,
                scoring_configuration = scoring.Snapshot(), scoring_breakdown = submittedScore,
                mistake_details = mistakeDetails.ToArray(),
            },
        };

        lastAttemptId = localAttemptId;
        lastCompletionSummary = $"Complete. Score {score}% | Time {FormatSeconds(durationSeconds)} | Mistakes {MistakeCount}";
        lastHudExtraLine = $"{lastCompletionSummary}\nSaved locally. Syncing with dashboard...";

        if (!LocalVerificationMode)
        { TechWiseOfflineAttemptQueue.Enqueue(payload, profile?.id, TechWisePortalClient.PortalBaseUrl); TechWiseOfflineAttemptQueue.SyncNow(); }
        UpdateHud(lastHudExtraLine);
        if (!LocalVerificationMode) StartCoroutine(RefreshCompletionSyncStatusCoroutine());
        TechWiseCompetitionMonitor.ReceiveAssessment(componentResults, null);
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
        if (LocalVerificationMode) return "Local verification; no upload.";
        if (TechWiseOfflineAttemptQueue.HasSynced(localAttemptId))
            return "Synced to dashboard.";

        if (!TechWiseOfflineAttemptQueue.HasPending(localAttemptId))
            return "Syncing with dashboard...";

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
            availableOrder.Count, attemptFinished, attemptFinished, CalculateScore(duration));
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
        bool isVr = UnityEngine.XR.XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android || !MainMenu.IsDesktopModeSelected(false);
        hudCanvas.renderMode = isVr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        if (isVr)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 20f;
            hudCanvas.worldCamera = ResolveCamera();
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(850f, 440f);
            rect.localScale = Vector3.one * 0.0012f;
            PositionVrFloatingHud(true);
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        var panel = new GameObject("HUD Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        if (isVr)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
        else
        {
            panelRect.anchorMin = new Vector2(0.015f, 0.72f);
            panelRect.anchorMax = new Vector2(0.36f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.05f, 0.12f, 0.90f);
        panelImage.raycastTarget = false;

        var textObject = new GameObject("HUD Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        hudText = textObject.GetComponent<TextMeshProUGUI>();
        hudText.rectTransform.anchorMin = new Vector2(0.05f, 0.06f);
        hudText.rectTransform.anchorMax = new Vector2(0.95f, 0.94f);
        hudText.rectTransform.offsetMin = Vector2.zero;
        hudText.rectTransform.offsetMax = Vector2.zero;
        hudText.enableAutoSizing = true;
        hudText.fontSizeMin = 16f;
        hudText.fontSizeMax = 30f;
        hudText.textWrappingMode = TextWrappingModes.Normal;
        hudText.overflowMode = TextOverflowModes.Ellipsis;
        hudText.alignment = TextAlignmentOptions.TopLeft;
        hudText.color = Color.white;
        hudText.raycastTarget = false;
        if (isVr)
        {
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>().checkFor3DOcclusion=false;
            canvasObject.AddComponent<TechWiseDraggableUiPanel>().SetBounds(new Vector2(850,55), new Vector2(0,192));
            var titleObject=new GameObject("Competition move handle",typeof(RectTransform),typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(canvasObject.transform,false);
            var titleRect=titleObject.GetComponent<RectTransform>();titleRect.anchorMin=new Vector2(.02f,.88f);titleRect.anchorMax=new Vector2(.98f,1);titleRect.offsetMin=titleRect.offsetMax=Vector2.zero;
            var titleText=titleObject.GetComponent<TextMeshProUGUI>();titleText.text="Competition | Grip title to move";titleText.fontSize=24;titleText.color=new Color(.25f,.85f,1);titleText.alignment=TextAlignmentOptions.Center;titleText.raycastTarget=false;
            hudText.rectTransform.anchorMax=new Vector2(.95f,.85f);
            TechWiseScrollableText.Wrap(hudText);
        }
    }

    void PositionVrFloatingHud(bool snap = false)
    {
        if (!snap || hudCanvas == null || hudCanvas.renderMode != RenderMode.WorldSpace)
            return;

        var cam = ResolveCamera();
        if (cam == null)
            return;

        var camFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (camFwd.sqrMagnitude < 0.01f)
            camFwd = Vector3.forward;

        var right = Vector3.Cross(Vector3.up, camFwd).normalized;
        var targetPos = cam.transform.position + camFwd * 1.35f + Vector3.up * 0.32f - right * 0.44f;
        var targetRot = Quaternion.LookRotation(camFwd, Vector3.up);

        if (snap || hudCanvas.transform.position == Vector3.zero)
        {
            hudCanvas.transform.position = targetPos;
            hudCanvas.transform.rotation = targetRot;
        }
        else
        {
            hudCanvas.transform.position = Vector3.Lerp(hudCanvas.transform.position, targetPos, Time.deltaTime * 5f);
            hudCanvas.transform.rotation = Quaternion.Slerp(hudCanvas.transform.rotation, targetRot, Time.deltaTime * 5f);
        }
    }

    public (string time, int score, int mistakes, string progress) GetLiveMetrics()
    {
        var duration = attemptFinished || attemptActive
            ? Mathf.Max(0, Mathf.RoundToInt((attemptFinished ? lastDurationSeconds : Time.realtimeSinceStartup - attemptStartRealtime)))
            : 0;
        return (FormatSeconds(duration), CalculateScore(duration), MistakeCount, $"{completedSteps.Count}/{availableOrder.Count}");
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

        var trRaycaster = canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        trRaycaster.checkFor3DOcclusion = false;
        trRaycaster.checkFor2DOcclusion = false;

        var panel = new GameObject("Start Prompt Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var image = panel.GetComponent<Image>();
        image.color = new Color(0.02f, 0.05f, 0.1f, 0.88f);
        image.raycastTarget = false;

        var buttonObject = new GameObject("Start Competition Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.12f, 0.14f);
        buttonRect.anchorMax = new Vector2(0.88f, 0.52f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color32(18, 101, 210, 255);
        buttonImage.raycastTarget = true;
        startPromptButton = buttonObject.GetComponent<Button>();
        startPromptButton.targetGraphic = buttonImage;
        startPromptButton.onClick.AddListener(BeginAttempt);

        var boxCol = buttonObject.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(promptSize.x * 0.76f, promptSize.y * 0.38f, 15f);
        boxCol.center = Vector3.zero;

        var textObject = new GameObject("Start Prompt Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        startPromptText = textObject.GetComponent<TextMeshProUGUI>();
        startPromptText.rectTransform.anchorMin = Vector2.zero;
        startPromptText.rectTransform.anchorMax = Vector2.one;
        startPromptText.rectTransform.offsetMin = Vector2.zero;
        startPromptText.rectTransform.offsetMax = Vector2.zero;
        startPromptText.enableAutoSizing = true;
        startPromptText.fontSizeMin = 18f;
        startPromptText.fontSizeMax = 36f;
        startPromptText.alignment = TextAlignmentOptions.Center;
        startPromptText.textWrappingMode = TextWrappingModes.Normal;
        startPromptText.color = Color.white;
        startPromptText.raycastTarget = false;

        var helperTextObject = new GameObject("Start Prompt Helper", typeof(RectTransform), typeof(TextMeshProUGUI));
        helperTextObject.transform.SetParent(panel.transform, false);
        var helperText = helperTextObject.GetComponent<TextMeshProUGUI>();
        helperText.rectTransform.anchorMin = new Vector2(0.08f, 0.56f);
        helperText.rectTransform.anchorMax = new Vector2(0.92f, 0.90f);
        helperText.rectTransform.offsetMin = Vector2.zero;
        helperText.rectTransform.offsetMax = Vector2.zero;
        helperText.enableAutoSizing = true;
        helperText.fontSizeMin = 16f;
        helperText.fontSizeMax = 28f;
        helperText.alignment = TextAlignmentOptions.Center;
        helperText.textWrappingMode = TextWrappingModes.Normal;
        helperText.color = new Color(0.85f, 0.92f, 1f, 1f);
        helperText.raycastTarget = false;
        helperText.text = "Competition is ready!\nPoint laser and click START, or pull Trigger / press A.";

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
            : $"START {modeLabel.ToUpperInvariant()}\n<size=20>(Point & Click, or Pull Trigger / Press A)</size>";

        if (startPromptButton != null)
            startPromptButton.interactable = !(TechWiseSimulationModeManager.IsDisassembly && TechWiseDisassemblyRuntime.IsPreparing);
    }

    static void ResolveStartPromptPose(Camera camera, out Vector3 position, out Quaternion rotation, out float scale, out Vector2 size)
    {
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
            size = new Vector2(1100f, 620f);
            scale = 0.003f;
            return;
        }

        var camPos = camera != null ? camera.transform.position : new Vector3(0f, 1.45f, 0f);
        var camFwd = camera != null ? camera.transform.forward : Vector3.forward;
        camFwd = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
        if (camFwd.sqrMagnitude < 0.01f)
            camFwd = Vector3.forward;

        position = camPos + camFwd * 1.3f + Vector3.down * 0.06f;
        rotation = Quaternion.LookRotation(camFwd, Vector3.up);
        size = new Vector2(900f, 500f);
        scale = 0.0022f;
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

    TechWiseAssessmentScore Score(int seconds, TechWiseVrComponentResult[] rows)
    {
        return TechWiseAssessmentScoring.Calculate(scoring, mistakeDetails, seconds,
            TechWiseSimulationModeManager.IsDisassembly, rows?.Count(r=>r.complete) ?? 0, rows?.Length ?? 0);
    }
    int CalculateScore(int seconds) => submittedScore?.final_score ?? Score(seconds, TechWiseSimulationRuntime.Instance?.CaptureAssessmentState()).final_score;

    static bool WasStartPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame))
        {
            return true;
        }

        // Check VR controllers (Right Hand & Left Hand)
        if (CheckXrControllerStart(XRNode.RightHand) || CheckXrControllerStart(XRNode.LeftHand))
            return true;

        return false;
    }

    static bool CheckXrControllerStart(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        // Primary Button: 'A' button on right controller, 'X' button on left controller
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primary) && primary)
            return true;

        // Secondary Button: 'B' button on right controller, 'Y' button on left controller
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondary) && secondary)
            return true;

        // Index Trigger button or analog trigger pull
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn)
            return true;
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f)
            return true;

        // Grip button
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripBtn) && gripBtn)
            return true;

        return false;
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
