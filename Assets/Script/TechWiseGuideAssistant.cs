using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-8500)]
public sealed class TechWiseGuideAssistant : MonoBehaviour
{
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const string BlackboardName = "School blackboards";
    const string BoardCanvasName = "TechWise Blackboard Steps";
    const string RobotName = "TechWise AI Guide";
    const string OldStepPanelMarker = "TechWise Hidden Old Step Panel";
    const float SocketNearDistance = 0.45f;
    const float RotationGoodAngle = 25f;
    const float StepCompletionHoldSeconds = 0.85f;

    static readonly GuideStep[] Steps = TechWiseBuildDefinition.Components.Select((c,i)=>new GuideStep(
        c.id, "Step "+(i+1)+": "+c.name, c.handling+"\n"+c.orientation+"\nTarget: "+c.target,
        "Use the detailed lesson panel for the current action and each required screw.")).ToArray();

    readonly List<XRGrabInteractable> grabInteractables = new();
    internal static string TutorialInstruction(string id)
    {
        foreach (var step in Steps)
            if (step.id == id)
                return step.title + "\n" + step.hint + "\nHold either grip to grab. Rotate to align, then release into the highlighted target.";
        return "Follow the highlighted component and its matching target.";
    }
    readonly List<XRLockSocketInteractor> sockets = new();
    readonly Dictionary<XRBaseInteractable, string> stepByInteractable = new();
    readonly Dictionary<XRLockSocketInteractor, string> stepBySocket = new();
    readonly Dictionary<string, bool> completedSteps = new();
    readonly Dictionary<string, float> stepSocketedSince = new();

    Transform playerCamera;
    Transform robotRoot;
    Transform instructionBoard;
    TMP_Text robotText;
    TMP_Text boardTitleText;
    TMP_Text boardText;
    XRGrabInteractable heldInteractable;
    Coroutine competitionCleanupCoroutine;
    float refreshTimer;
    string lastHint;
    bool sceneTextCleanupComplete;

    struct GuideStep
    {
        public readonly string id;
        public readonly string title;
        public readonly string details;
        public readonly string hint;

        public GuideStep(string id, string title, string details, string hint)
        {
            this.id = id;
            this.title = title;
            this.details = details;
            this.hint = hint;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseGuideAssistant>() != null)
            return;

        var guideObject = new GameObject("TechWise Guide Assistant Runtime");
        DontDestroyOnLoad(guideObject);
        guideObject.AddComponent<TechWiseGuideAssistant>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshScene();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearListeners();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshScene();
    }

    void Update()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            SetRobotVisible(false);
            return;
        }

        if (TechWiseSimulationModeManager.IsCompetitionMode)
        {
            SetRobotVisible(false);
            return;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.25f;
            if (boardText == null || !boardText.gameObject.activeInHierarchy)
            {
                SetupBlackboard();
            }
            RefreshProgress();
            UpdateBoardText();
        }

        SetRobotVisible(false);
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }

    void RefreshScene()
    {
        ClearListeners();

        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            SetRobotVisible(false);
            return;
        }

        instructionBoard = null;
        boardTitleText = null;
        boardText = null;
        sceneTextCleanupComplete = false;

        playerCamera = ResolvePlayerCamera();

        SetupBlackboard();
        HideRestartCautionPanels();
        RefreshTutorialVideos();
        if (TechWiseSimulationModeManager.IsCompetitionMode)
        {
            StartCompetitionUiCleanup();
        }
        else
        {
            RestoreKnowledgeCorner();
            if (TechWiseSimulationModeManager.IsPracticeMode || TechWiseSimulationModeManager.IsTutorialMode)
                HideOldFloatingStepPanels();
            if (TechWiseSimulationModeManager.IsDisassembly)
                StartCompetitionUiCleanup();
        }
        RegisterInteractablesAndSockets();
        RefreshProgress();
        UpdateBoardText();
        if (TechWiseSimulationModeManager.IsCompetitionMode || TechWiseSimulationModeManager.IsDisassembly)
            HideCompetitionPracticeUi();
        SetRobotVisible(false);
        lastHint = null;
    }

    void RefreshTutorialVideos()
    {
        if (!TechWiseSimulationModeManager.IsCompetitionMode)
            return;

        foreach (var player in FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include))
        {
            if (player == null || !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded)
                continue;

            var root = FindVideoRoot(player.transform);
            if (root != null)
                root.gameObject.SetActive(false);
            else
                player.gameObject.SetActive(false);
        }
    }

    void ClearListeners()
    {
        foreach (var interactable in grabInteractables)
        {
            if (interactable == null)
                continue;

            interactable.selectEntered.RemoveListener(OnGrabSelected);
            interactable.selectExited.RemoveListener(OnGrabReleased);
        }

        foreach (var socket in sockets)
        {
            if (socket == null)
                continue;

            socket.selectEntered.RemoveListener(OnSocketSelected);
        }

        grabInteractables.Clear();
        sockets.Clear();
        stepByInteractable.Clear();
        stepBySocket.Clear();
        stepSocketedSince.Clear();
        heldInteractable = null;
    }

    static Transform ResolvePlayerCamera()
    {
        var xrOrigin = FindAnyObjectByType<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
            return xrOrigin.Camera.transform;

        return Camera.main != null ? Camera.main.transform : null;
    }

    static void RestoreKnowledgeCorner()
    {
        foreach (var transformInScene in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (transformInScene == null)
                continue;

            var obj = transformInScene.gameObject;
            if (!obj.scene.IsValid() || !obj.scene.isLoaded)
                continue;

            var name = obj.name.Trim();
            if (name.StartsWith("Table Knowledge Corner", System.StringComparison.Ordinal) ||
                name == "Knowledge Corner" ||
                HasKnowledgeCornerParent(transformInScene))
            {
                obj.SetActive(true);
            }
        }
    }

    static bool HasKnowledgeCornerParent(Transform transform)
    {
        var current = transform.parent;
        while (current != null)
        {
            var name = current.name.Trim();
            if (name.StartsWith("Table Knowledge Corner", System.StringComparison.Ordinal) ||
                name == "Knowledge Corner")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void SetupBlackboard()
    {
        if (!TechWiseSimulationModeManager.IsCompetitionMode && TryUseExistingCautionBoardTexts())
        {
            UpdateBoardText();
            return;
        }

        if (!TechWiseSimulationModeManager.IsCompetitionMode)
        {
            var cautionTitle = FindCautionTitleText();
            if (cautionTitle != null && cautionTitle.transform.parent != null)
                cautionTitle.transform.parent.gameObject.SetActive(false);
        }

        var blackboard = instructionBoard != null ? instructionBoard : ResolveInstructionBoard();
        if (blackboard == null)
            return;

        instructionBoard = blackboard;
        HideCautionBoardText(blackboard);

        var existing = blackboard.Find(BoardCanvasName);
        var canvasObject = existing != null ? existing.gameObject : new GameObject(BoardCanvasName, typeof(Canvas), typeof(CanvasScaler));
        var boardBounds = CalculateRendererBounds(blackboard, existing);
        canvasObject.transform.SetParent(blackboard, true);
        canvasObject.SetActive(true);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1100f, 620f);
        canvasRect.localScale = Vector3.one * 0.003f;
        PositionCanvasOnBoard(canvasObject.transform, blackboard, boardBounds);

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 24f;

        boardText = canvasObject.GetComponentInChildren<TMP_Text>(true);
        if (boardText == null)
        {
            var textObject = new GameObject("Steps Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            boardText = textObject.GetComponent<TextMeshProUGUI>();
        }

        var textRect = boardText.rectTransform;
        textRect.anchorMin = new Vector2(0.04f, 0.06f);
        textRect.anchorMax = new Vector2(0.96f, 0.94f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        boardText.fontSize = 31f;
        boardText.enableAutoSizing = true;
        boardText.fontSizeMin = 14f;
        boardText.fontSizeMax = 34f;
        boardText.alignment = TextAlignmentOptions.Top;
        boardText.color = Color.white;
        boardText.textWrappingMode = TextWrappingModes.Normal;
        boardText.overflowMode = TextOverflowModes.Truncate;
        boardText.raycastTarget = false;
    }

    bool TryUseExistingCautionBoardTexts()
    {
        var titleText = FindCautionTitleText();
        if (titleText == null || titleText.transform.parent == null)
            return false;

        var boardGroup = titleText.transform.parent;
        var infoText = FindCautionInfoText(boardGroup, titleText);
        if (infoText == null)
            return false;

        instructionBoard = boardGroup;
        boardTitleText = titleText;
        boardText = infoText;

        if (TechWiseTutorialRuntime.InTutorial)
            foreach (var text in boardGroup.GetComponentsInChildren<TMP_Text>(true))
                if (text != titleText && text != infoText) text.gameObject.SetActive(false);

        boardGroup.gameObject.SetActive(true);
        boardTitleText.gameObject.SetActive(true);
        boardText.gameObject.SetActive(true);

        boardTitleText.text = "PC Assembly Guide";
        boardTitleText.enableAutoSizing = true;
        boardTitleText.fontSizeMin = 18f;
        boardTitleText.fontSizeMax = 42f;
        boardTitleText.alignment = TextAlignmentOptions.Center;
        boardTitleText.color = Color.white;
        boardTitleText.raycastTarget = false;

        boardText.enableAutoSizing = true;
        boardText.fontSizeMin = 19f;
        boardText.fontSizeMax = 46f;
        boardText.alignment = TextAlignmentOptions.Top;
        boardText.color = Color.white;
        boardText.textWrappingMode = TextWrappingModes.Normal;
        boardText.overflowMode = TextOverflowModes.Truncate;
        boardText.margin = new Vector4(10f, 4f, 10f, 4f);
        boardText.raycastTarget = false;

        var infoRect = boardText.rectTransform;
        infoRect.localScale = new Vector3(0.068f, 0.125f, 0.068f);
        infoRect.sizeDelta = new Vector2(
            1080f,
            620f);

        return true;
    }

    static TMP_Text FindCautionTitleText()
    {
        TMP_Text fallbackTitle = null;

        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text == null || !text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            var value = text.text ?? string.Empty;
            if (value.IndexOf("CAUTION", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return text;

            if (text.transform.name == "Title" &&
                (value.IndexOf("PC Assembly Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 FindCautionInfoText(text.transform.parent, text) != null))
            {
                fallbackTitle = text;
            }
        }

        return fallbackTitle;
    }

    static TMP_Text FindCautionInfoText(Transform boardGroup, TMP_Text titleText)
    {
        if (boardGroup == null)
            return null;

        TMP_Text largestSibling = null;
        var largestArea = 0f;

        foreach (var text in boardGroup.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || ReferenceEquals(text, titleText))
                continue;

            if (text.transform.name == "INFO")
                return text;

            var value = text.text ?? string.Empty;
            if (value.IndexOf("WORKSTATION", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("VR SESSION", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Step ", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            var rect = text.rectTransform.rect;
            var area = Mathf.Abs(rect.width * rect.height);
            if (area > largestArea)
            {
                largestArea = area;
                largestSibling = text;
            }
        }

        return largestSibling;
    }

    void PositionCanvasOnBoard(Transform canvasTransform, Transform blackboard, Bounds boardBounds)
    {
        var center = boardBounds.center;
        var facing = blackboard.forward;

        if (playerCamera != null)
        {
            var toCamera = playerCamera.position - center;
            if (toCamera.sqrMagnitude > 0.01f)
                facing = toCamera.normalized;
        }

        canvasTransform.position = center + facing * 0.045f;
        canvasTransform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
    }

    static void HideCautionBoardText(Transform blackboard)
    {
        foreach (var text in blackboard.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || text.GetComponentInParent<Canvas>()?.name == BoardCanvasName)
                continue;

            text.gameObject.SetActive(false);
        }
    }

    static Transform ResolveInstructionBoard()
    {
        var practiceBoard = FindNamedTransform(BlackboardName);
        if (practiceBoard != null)
            return practiceBoard;

        var cautionBoard = FindBoardContainingText("CAUTION");
        if (cautionBoard != null)
            return cautionBoard;

        return null;
    }

    static Transform FindBoardContainingText(string textFragment)
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            if (!text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            if (text.text.IndexOf(textFragment, System.StringComparison.OrdinalIgnoreCase) < 0)
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
            var bounds = CalculateRendererBounds(current);
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
            var bounds = CalculateRendererBounds(current);
            var size = bounds.size;
            var magnitude = size.magnitude;

            if (magnitude > 0.35f && magnitude < 8.5f)
                return current;

            current = current.parent;
            depth++;
        }

        return null;
    }

    void SetupRobot()
    {
        if (robotRoot != null)
            return;

        var root = new GameObject(RobotName);
        DontDestroyOnLoad(root);
        robotRoot = root.transform;

        var bodyMaterial = CreateMaterial("TechWise Robot Body", new Color(0.1f, 0.42f, 0.95f, 1f));
        var faceMaterial = CreateMaterial("TechWise Robot Face", new Color(0.03f, 0.06f, 0.08f, 1f));
        var glowMaterial = CreateMaterial("TechWise Robot Glow", new Color(0.1f, 1f, 0.35f, 1f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Robot Body";
        body.transform.SetParent(robotRoot, false);
        body.transform.localScale = new Vector3(0.14f, 0.1f, 0.14f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        Destroy(body.GetComponent<Collider>());

        var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Robot Face";
        face.transform.SetParent(robotRoot, false);
        face.transform.localPosition = new Vector3(0f, 0.01f, 0.075f);
        face.transform.localScale = new Vector3(0.1f, 0.035f, 0.01f);
        face.GetComponent<Renderer>().sharedMaterial = faceMaterial;
        Destroy(face.GetComponent<Collider>());

        CreateEye("Left Eye", new Vector3(-0.025f, 0.02f, 0.083f), glowMaterial);
        CreateEye("Right Eye", new Vector3(0.025f, 0.02f, 0.083f), glowMaterial);

        var canvasObject = new GameObject("Robot Hint Bubble", typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        canvasObject.transform.SetParent(robotRoot, false);
        canvasObject.transform.localPosition = new Vector3(0.34f, 0.02f, 0f);
        canvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 190f);

        var image = canvasObject.GetComponent<Image>();
        image.color = new Color(0.02f, 0.04f, 0.06f, 0.86f);
        image.raycastTarget = false;

        var textObject = new GameObject("Hint Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        robotText = textObject.GetComponent<TextMeshProUGUI>();
        robotText.rectTransform.anchorMin = new Vector2(0.055f, 0.1f);
        robotText.rectTransform.anchorMax = new Vector2(0.945f, 0.9f);
        robotText.rectTransform.offsetMin = Vector2.zero;
        robotText.rectTransform.offsetMax = Vector2.zero;
        robotText.fontSize = 22f;
        robotText.enableAutoSizing = true;
        robotText.fontSizeMin = 12f;
        robotText.fontSizeMax = 24f;
        robotText.textWrappingMode = TextWrappingModes.Normal;
        robotText.overflowMode = TextOverflowModes.Overflow;
        robotText.alignment = TextAlignmentOptions.MidlineLeft;
        robotText.color = Color.white;
        robotText.raycastTarget = false;
    }

    void CreateEye(string eyeName, Vector3 localPosition, Material material)
    {
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = eyeName;
        eye.transform.SetParent(robotRoot, false);
        eye.transform.localPosition = localPosition;
        eye.transform.localScale = Vector3.one * 0.015f;
        eye.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(eye.GetComponent<Collider>());
    }

    static Material CreateMaterial(string materialName, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = materialName,
            color = color,
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    void RegisterInteractablesAndSockets()
    {
        foreach (var step in Steps)
            completedSteps[step.id] = false;

        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
        {
            var stepId = ResolveStepId(interactable.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            grabInteractables.Add(interactable);
            stepByInteractable[interactable] = stepId;
            interactable.selectEntered.AddListener(OnGrabSelected);
            interactable.selectExited.AddListener(OnGrabReleased);
        }

        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            var stepId = ResolveStepId(socket.transform);
            if (string.IsNullOrEmpty(stepId))
                continue;

            sockets.Add(socket);
            stepBySocket[socket] = stepId;
            socket.selectEntered.AddListener(OnSocketSelected);
        }
    }

    void OnGrabSelected(SelectEnterEventArgs args)
    {
        if (args == null || args.interactorObject is XRSocketInteractor)
            return;

        heldInteractable = args.interactableObject as XRGrabInteractable;
    }

    void OnGrabReleased(SelectExitEventArgs args)
    {
        if (args == null || args.interactorObject is XRSocketInteractor)
            return;

        if (ReferenceEquals(heldInteractable, args.interactableObject))
            heldInteractable = null;
    }

    void OnSocketSelected(SelectEnterEventArgs args)
    {
        RefreshProgress();
        UpdateBoardText();

        if (TechWiseSimulationModeManager.IsCompetitionMode)
            return;

        var interactable = args?.interactableObject as XRGrabInteractable;
        var stepId = interactable != null && stepByInteractable.TryGetValue(interactable, out var fromInteractable)
            ? fromInteractable
            : null;

        var step = FindStep(stepId);
        SetRobotHint(step.title != null
            ? $"Nice. {GetRobotStepTitle(step)} is in the correct place."
            : "Nice. That part is locked into place.");
    }

    void RefreshProgress()
    {
        if (TechWiseSimulationRuntime.Instance != null)
        {
            foreach (var step in Steps)
                completedSteps[step.id] = TechWiseSimulationRuntime.Instance.IsStepComplete(step.id);
            return;
        }
        foreach (var step in Steps)
            completedSteps[step.id] = false;

        var socketedSteps = new HashSet<string>();
        foreach (var socket in sockets)
        {
            if (socket == null || !stepBySocket.TryGetValue(socket, out var stepId))
                continue;

            if (socket.interactablesSelected.Count <= 0)
                continue;

            socketedSteps.Add(stepId);
            if (!stepSocketedSince.ContainsKey(stepId))
                stepSocketedSince[stepId] = Time.time;

            completedSteps[stepId] = Time.time - stepSocketedSince[stepId] >= StepCompletionHoldSeconds;
        }

        foreach (var step in Steps)
        {
            if (!socketedSteps.Contains(step.id))
                stepSocketedSince.Remove(step.id);
        }
    }

    static string BoardSummary(TechWiseDetailedAssemblyRuntime phase)
    {
        var lines=phase.Instruction.Split('\n');
        return string.Join("\n\n",lines.Where(l=>l.StartsWith("<b>Objective:") || l.StartsWith("<b>Action:") || l.StartsWith("<b>Progress:") || l.StartsWith("<b>Next:")))
            + "\n\nRead and scroll the floating lesson panel for controls, handling, alignment and corrections.";
    }
    void UpdateBoardText()
    {
        if (boardText == null)
            return;

        if (TechWiseTutorialRuntime.InTutorial && TechWiseTutorialRuntime.Instance != null)
        {
            EnsurePracticeBoardTextVisible();
            var tutorial = TechWiseTutorialRuntime.Instance;
            if (boardTitleText != null) boardTitleText.text = tutorial.Heading;
            boardText.text = tutorial.stage >= TechWiseTutorialRuntime.Stage.Assembly && TechWiseDetailedAssemblyRuntime.Active ? BoardSummary(TechWiseDetailedAssemblyRuntime.Instance) : tutorial.Instructions + "\n\n" + tutorial.Feedback;
            return;
        }

        EnsurePracticeBoardTextVisible();
        if (!TechWiseSimulationModeManager.IsCompetitionMode && boardTitleText != null)
        {
            boardTitleText.fontStyle = FontStyles.Italic;
            boardTitleText.characterSpacing = 1.5f;
            boardTitleText.color = new Color(0.94f, 0.96f, 0.90f);
        }

        if (TechWiseSimulationModeManager.IsCompetitionMode)
        {
            if (boardTitleText != null)
                boardTitleText.text = "VR Competition";

            boardText.text = TechWiseSimulationModeManager.IsDisassembly
                ? "Disassembly competition is ready.\n\nStart from the competition prompt, then remove PC parts in the correct servicing order. Step prompts and labels are hidden while the timer, mistakes, and score are recorded."
                : "Assembly competition is ready.\n\nStart from the competition prompt, then install PC parts in the correct order. Step prompts and labels are hidden while the timer, mistakes, and score are recorded.";
            return;
        }

        if (TechWiseDetailedAssemblyRuntime.Active)
        {
            var phase = TechWiseDetailedAssemblyRuntime.Instance;
            if (boardTitleText != null) boardTitleText.text = phase.Heading;
            boardText.text = BoardSummary(phase);
            return;
        }

        if (boardTitleText != null)
            boardTitleText.text = TechWiseSimulationModeManager.IsTutorialMode ? "Tutorial Mode" : "Practice Mode";

        var builder = new StringBuilder();
        var currentStep = GetCurrentStep();
        var currentIndex = GetCurrentStepIndex();

        if (boardTitleText == null)
        {
            builder.AppendLine("PC Assembly Guide");
            builder.AppendLine();
        }

        if (currentStep.title != null)
        {
            builder.Append("Step ");
            builder.Append(currentIndex + 1);
            builder.Append(" of ");
            builder.AppendLine(Steps.Length.ToString());
            builder.AppendLine(currentStep.title);
            builder.AppendLine();
            builder.AppendLine(currentStep.details);
            builder.AppendLine();
            builder.Append("Progress: ");
            builder.Append(GetCompletedStepCount());
            builder.Append('/');
            builder.Append(Steps.Length);
            builder.Append(" parts installed");
        }
        else
        {
            builder.AppendLine("Build complete");
            builder.AppendLine();
            builder.AppendLine("You have successfully attached all the component to complete the PC build.");
        }

        boardText.text = builder.ToString();
    }

    void EnsurePracticeBoardTextVisible()
    {
        if (TechWiseSimulationModeManager.IsCompetitionMode)
            return;

        if (instructionBoard != null)
            instructionBoard.gameObject.SetActive(true);

        if (boardTitleText != null)
        {
            boardTitleText.gameObject.SetActive(true);
            boardTitleText.enabled = true;
        }

        if (boardText != null)
        {
            boardText.gameObject.SetActive(true);
            boardText.enabled = true;
        }
    }

    bool IsCurrentBoardText(TMP_Text text)
    {
        if (text == null)
            return false;

        return ReferenceEquals(text, boardTitleText) ||
            ReferenceEquals(text, boardText) ||
            HasParent(text.transform, instructionBoard);
    }

    void UpdateDisassemblyPracticeBoardText()
    {
        if (boardTitleText != null)
            boardTitleText.text = "Practice Mode";

        var builder = new StringBuilder();
        if (boardTitleText == null)
        {
            builder.AppendLine("PC Disassembly Guide");
            builder.AppendLine();
        }

        builder.AppendLine("Practice the safe removal order before trying competition mode.");
        builder.AppendLine();
        builder.AppendLine("1. Remove the CPU cooler.");
        builder.AppendLine("2. Remove the power supply unit.");
        builder.AppendLine("3. Remove storage drives.");
        builder.AppendLine("4. Remove the graphics card.");
        builder.AppendLine("5. Remove the M.2 SSD.");
        builder.AppendLine("6. Remove RAM.");
        builder.AppendLine("7. Remove the CPU.");
        builder.AppendLine("8. Remove the motherboard last.");
        builder.AppendLine();
        builder.AppendLine("Keep parts organized and avoid forcing any component.");

        boardText.text = builder.ToString();
    }

    void HideOldFloatingStepPanels()
    {
        if (sceneTextCleanupComplete)
            return;

        sceneTextCleanupComplete = true;

        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            if (!text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            if (IsCurrentBoardText(text) || IsProtectedInstructionText(text) || !LooksLikeOldFloatingStepPanel(text.text))
                continue;

            var panel = FindOldStepPanelRoot(text.transform);
            if (panel != null && !IsProtectedTransform(panel))
            {
                foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                    graphic.enabled = false;

                panel.gameObject.name = OldStepPanelMarker + " - " + panel.gameObject.name;
                panel.gameObject.SetActive(false);
                continue;
            }

            text.gameObject.name = OldStepPanelMarker + " - " + text.gameObject.name;
            text.enabled = false;
            text.gameObject.SetActive(false);
        }

    }

    void HideRestartCautionPanels()
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            if (!text.gameObject.scene.IsValid() || !text.gameObject.scene.isLoaded)
                continue;

            if (IsCurrentBoardText(text))
                continue;

            var value = text.text;
            if (value.IndexOf("PLEASE DON'T MAKE A MESS", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                value.IndexOf("YOU WILL NEED TO RESTART", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                value.IndexOf("RESTART THE VR SESSION", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var panel = FindLargestRendererParent(text.transform, maxDepth: 6) ??
                FindOldStepPanelRoot(text.transform) ??
                text.transform;

            if (panel != null && !IsCompetitionProtectedTransform(panel))
                panel.gameObject.SetActive(false);
            else
                text.gameObject.SetActive(false);
        }
    }

    void StartCompetitionUiCleanup()
    {
        HideCompetitionPracticeUi();
        if (competitionCleanupCoroutine != null)
            StopCoroutine(competitionCleanupCoroutine);

        competitionCleanupCoroutine = StartCoroutine(DelayedCompetitionUiCleanup());
    }

    IEnumerator DelayedCompetitionUiCleanup()
    {
        yield return null;
        HideCompetitionPracticeUi();
        yield return new WaitForSeconds(0.25f);
        HideCompetitionPracticeUi();
        yield return new WaitForSeconds(1f);
        HideCompetitionPracticeUi();
        competitionCleanupCoroutine = null;
    }

    void HideCompetitionPracticeUi()
    {
        foreach (var transformInScene in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (transformInScene == null ||
                !transformInScene.gameObject.scene.IsValid() ||
                !transformInScene.gameObject.scene.isLoaded ||
                IsCompetitionProtectedTransform(transformInScene))
            {
                continue;
            }

            if (LooksLikeCompetitionPracticeRoot(transformInScene.name))
                transformInScene.gameObject.SetActive(false);
        }

        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
        {
            if (text == null || IsCompetitionProtectedText(text))
                continue;

            var value = text.text ?? string.Empty;
            if (!LooksLikeCompetitionPracticeText(value) && !LooksLikeCompetitionPracticeRoot(text.transform.name))
                continue;

            var panel = FindOldStepPanelRoot(text.transform);
            if (panel != null && !IsCompetitionProtectedTransform(panel))
            {
                panel.gameObject.SetActive(false);
                continue;
            }

            text.gameObject.SetActive(false);
        }
    }

    bool IsCompetitionProtectedText(TMP_Text text)
    {
        if (text == null)
            return true;

        return ReferenceEquals(text, boardTitleText) ||
            ReferenceEquals(text, boardText) ||
            HasParent(text.transform, instructionBoard) ||
            IsCompetitionProtectedTransform(text.transform);
    }

    bool IsCompetitionProtectedTransform(Transform transform)
    {
        if (transform == null)
            return true;

        if (HasParent(transform, instructionBoard))
            return true;

        var current = transform;
        while (current != null)
        {
            var name = current.name.Trim();
            if (name == BoardCanvasName ||
                name == RobotName ||
                name == "TechWise Competition HUD" ||
                name == "TechWise Competition Start Prompt" ||
                name == "Start Prompt Canvas" ||
                name == "Start Prompt Panel" ||
                name == "Start Competition Button" ||
                name == "Start Prompt Text" ||
                name == "Start Prompt Helper" ||
                name == "TechWise In-Game Menu" ||
                name == "TechWise In-Game Menu Canvas" ||
                name == "TechWise Blackboard Steps" ||
                name == "HUD Panel" ||
                name == "HUD Text")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    static bool HasParent(Transform transform, Transform possibleParent)
    {
        if (transform == null || possibleParent == null)
            return false;

        var current = transform;
        while (current != null)
        {
            if (ReferenceEquals(current, possibleParent))
                return true;

            current = current.parent;
        }

        return false;
    }

    static bool LooksLikeCompetitionPracticeRoot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.IndexOf("Knowledge Corner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Table Knowledge Corner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Tutorial Step Canva", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (TechWiseSimulationModeManager.IsCompetitionMode &&
                (name.IndexOf("Real Life Simulation", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Real-life simulation", System.StringComparison.OrdinalIgnoreCase) >= 0)) ||
            name.IndexOf("PC Assembly Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Assembly Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (TechWiseSimulationModeManager.IsCompetitionMode && name.IndexOf("Video Group", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            (TechWiseSimulationModeManager.IsCompetitionMode && name.IndexOf("Video Player", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            (TechWiseSimulationModeManager.IsCompetitionMode && name.IndexOf("Video Image", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            name.IndexOf("Info Step Picture", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Item Label", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool LooksLikeCompetitionPracticeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return LooksLikeOldFloatingStepPanel(value) ||
            (TechWiseSimulationModeManager.IsCompetitionMode && value.IndexOf("Real Life Simulation", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            (TechWiseSimulationModeManager.IsCompetitionMode && value.IndexOf("Real-life simulation video", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            value.IndexOf("PLEASE DON'T MAKE A MESS", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("IF YOUR COMPONENTS MISSING", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("YOU WILL NEED TO RESTART", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("RESTART THE VR SESSION", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Knowledge Corner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Grab the", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("green guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("RAM slot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Thermal Paste", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("POWER SUPPLY", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("GPU Connector", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("GRAPHIC CARD", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("CPU COOLER", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.Trim().Equals("CPU", System.StringComparison.OrdinalIgnoreCase) ||
            value.Trim().Equals("RAM", System.StringComparison.OrdinalIgnoreCase) ||
            value.Trim().Equals("SSD", System.StringComparison.OrdinalIgnoreCase) ||
            value.Trim().Equals("M.2 SSD", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool LooksLikeOldFloatingStepPanel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf("Step ", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("PC Assembly Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Firstly, open the CPU socket", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("green guide appears", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("In a real-life simulation", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Transform FindOldStepPanelRoot(Transform textTransform)
    {
        var current = textTransform.parent;
        var depth = 0;

        while (current != null && depth < 5)
        {
            var name = current.name.Trim();
            var isStepPanelName = name.StartsWith("Step ", System.StringComparison.OrdinalIgnoreCase) &&
                name.IndexOf("Component", System.StringComparison.OrdinalIgnoreCase) < 0;

            if (isStepPanelName && (current.GetComponent<RectTransform>() != null || current.GetComponent<Image>() != null))
                return current;

            current = current.parent;
            depth++;
        }

        return null;
    }

    static Transform FindVideoRoot(Transform start)
    {
        var current = start;
        var depth = 0;
        Transform best = null;

        while (current != null && depth < 8)
        {
            var name = current.name.Trim();
            if (name.IndexOf("Video", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Real Life Simulation", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Real-life simulation", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                best = current;
            }

            current = current.parent;
            depth++;
        }

        return best;
    }

    static bool IsProtectedInstructionText(TMP_Text text)
    {
        if (text == null)
            return true;

        return text.GetComponentInParent<Canvas>()?.name == BoardCanvasName ||
            HasKnowledgeCornerParent(text.transform) ||
            IsProtectedTransform(text.transform);
    }

    static bool IsProtectedTransform(Transform transform)
    {
        var current = transform;
        while (current != null)
        {
            var name = current.name.Trim();
            if (name == BoardCanvasName ||
                name == RobotName ||
                name.IndexOf("Knowledge Corner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Table Knowledge Corner", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void UpdateRobotFollow()
    {
        if (robotRoot == null)
            return;

        if (playerCamera == null)
            playerCamera = ResolvePlayerCamera();
        if (playerCamera == null)
            return;

        var targetPosition = playerCamera.position -
            playerCamera.right * 0.62f +
            playerCamera.up * 0.36f +
            playerCamera.forward * 1.25f;

        robotRoot.position = Vector3.Lerp(robotRoot.position, targetPosition, 1f - Mathf.Exp(-5f * Time.deltaTime));

        var lookDirection = playerCamera.position - robotRoot.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            robotRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, playerCamera.up);
    }

    void UpdateRobotHint()
    {
        if (TechWiseSimulationModeManager.IsCompetitionMode)
        {
            SetRobotVisible(false);
            return;
        }

        if (TechWiseSimulationModeManager.IsDisassembly)
        {
            SetRobotHint("Practice disassembly carefully. Follow the board order and keep each removed part organized.");
            return;
        }

        if (heldInteractable == null)
        {
            var currentStep = GetCurrentStep();
            SetRobotHint(currentStep.title != null
                ? $"Next: {GetRobotStepTitle(currentStep)}. {currentStep.hint}"
                : "Great work. All listed components are installed.");
            return;
        }

        var heldStepId = stepByInteractable.TryGetValue(heldInteractable, out var stepId) ? stepId : ResolveStepId(heldInteractable.transform);
        var validSocket = FindNearestSocket(heldInteractable, true, out var validDistance);
        var anySocket = FindNearestSocket(heldInteractable, false, out var anyDistance);

        if (validSocket != null && validDistance <= SocketNearDistance)
        {
            var state = TechWiseSimulationRuntime.Instance;
            if (state != null && state.Sockets.Contains(validSocket))
            {
                var problem = state.PlacementProblem(validSocket, heldInteractable, out _, out var correction);
                SetRobotHint(problem == null ? "Correct place and rotation. Release it to attach." : correction);
                return;
            }
            var targetRotation = validSocket.attachTransform != null ? validSocket.attachTransform.rotation : validSocket.transform.rotation;
            var angle = Quaternion.Angle(heldInteractable.transform.rotation, targetRotation);
            SetRobotHint(angle <= RotationGoodAngle
                ? "Correct place and rotation. Release it to attach."
                : "Correct place. Rotate the part until it lines up with the green guide.");
            return;
        }

        if (anySocket != null && anyDistance <= SocketNearDistance && (validSocket == null || !ReferenceEquals(anySocket, validSocket)))
        {
            SetRobotHint("That is not the matching slot. Move the part toward its green guide.");
            return;
        }

        var step = FindStep(heldStepId);
        SetRobotHint(step.title != null
            ? $"Holding {GetRobotStepTitle(step)}. Move it near the matching socket."
            : "Move the part near the matching socket. The correct target will glow green.");
    }

    XRLockSocketInteractor FindNearestSocket(XRGrabInteractable interactable, bool requireValid, out float nearestDistance)
    {
        nearestDistance = float.MaxValue;
        XRLockSocketInteractor nearest = null;

        foreach (var socket in sockets)
        {
            if (socket == null)
                continue;

            if (!socket.isActiveAndEnabled || !socket.socketActive ||
                (socket.hasSelection && !socket.IsSelecting(interactable))) continue;
            if (requireValid && !TechWiseSimulationRuntime.Matches(socket, interactable.transform))
                continue;

            var socketPosition = socket.attachTransform != null ? socket.attachTransform.position : socket.transform.position;
            var distance = Vector3.Distance(interactable.GetAttachTransform(socket).position, socketPosition);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = socket;
        }

        return nearest;
    }

    void SetRobotHint(string hint)
    {
        if (robotText == null || string.IsNullOrWhiteSpace(hint) || hint == lastHint)
            return;

        lastHint = hint;
        robotText.text = hint;
    }

    void SetRobotVisible(bool visible)
    {
        if (robotRoot != null)
            robotRoot.gameObject.SetActive(visible);
    }

    GuideStep GetCurrentStep()
    {
        if (TechWiseDetailedAssemblyRuntime.Active && TechWiseDetailedAssemblyRuntime.Instance.Ready)
        {
            var current = TechWiseDetailedAssemblyRuntime.Instance.CurrentPartStep;
            foreach (var step in Steps) if (step.id == current) return step;
            return default;
        }
        foreach (var step in Steps)
        {
            if (!completedSteps.TryGetValue(step.id, out var completed) || !completed)
                return step;
        }

        return default;
    }

    int GetCurrentStepIndex()
    {
        var currentStep = GetCurrentStep();
        for (var i = 0; i < Steps.Length; i++)
        {
            if (Steps[i].id == currentStep.id)
                return i;
        }

        return Steps.Length;
    }

    int GetCompletedStepCount()
    {
        var count = 0;
        foreach (var step in Steps)
        {
            if (completedSteps.TryGetValue(step.id, out var completed) && completed)
                count++;
        }

        return count;
    }

    static GuideStep FindStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return default;

        foreach (var step in Steps)
        {
            if (step.id == stepId)
                return step;
        }

        return default;
    }

    static string GetRobotStepTitle(GuideStep step)
    {
        if (string.IsNullOrWhiteSpace(step.title))
            return "this part";

        var colonIndex = step.title.IndexOf(':');
        return colonIndex >= 0 && colonIndex + 1 < step.title.Length
            ? step.title.Substring(colonIndex + 1).Trim()
            : step.title.Trim();
    }

    static string ResolveStepId(Transform transform)
    {
        return TechWiseSimulationModeManager.ResolveStepId(transform);
    }

    static string GetSearchName(Transform transform)
    {
        var builder = new StringBuilder();
        var current = transform;
        var depth = 0;
        while (current != null && depth < 4)
        {
            builder.Append(current.name).Append(' ');
            current = current.parent;
            depth++;
        }

        var keychain = transform.GetComponent<Keychain>() ?? transform.GetComponentInParent<Keychain>();
        if (keychain != null)
            builder.Append(keychain.name).Append(' ');

        var socket = transform.GetComponent<XRLockSocketInteractor>() ?? transform.GetComponentInParent<XRLockSocketInteractor>();
        if (socket != null && socket.keychainLock != null)
        {
            foreach (var key in socket.keychainLock.requiredKeys)
            {
                if (key != null)
                    builder.Append(key.name).Append(' ');
            }
        }

        return builder.ToString().ToLowerInvariant();
    }

    static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment))
                return true;
        }

        return false;
    }

    static Transform FindNamedTransform(string objectName)
    {
        foreach (var transformInScene in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (transformInScene != null && transformInScene.name == objectName)
                return transformInScene;
        }

        return null;
    }

    static Bounds CalculateRendererBounds(Transform root, Transform ignoredRoot = null)
    {
        var bounds = new Bounds(root.position, Vector3.one);
        var hasBounds = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer == null)
                continue;
            if (ignoredRoot != null && renderer.transform.IsChildOf(ignoredRoot))
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

        return bounds;
    }
}
