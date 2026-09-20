using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class MainMenu : MonoBehaviour
{
    public const string ControlModeKey = "TechWise360.ControlMode";
    public const string DesktopModeValue = "Desktop";
    public const string VrModeValue = "VR";
    const string ControlModeExplicitKey = "TechWise360.ControlModeExplicit";

    [SerializeField] string singleplayerSceneName = "Singleplayer";
    [SerializeField, FormerlySerializedAs("multiplayerSceneName")] string practiceSceneName = "Multiplayer";
    [SerializeField] string competitionSceneName = "Multiplayer";
    [SerializeField] bool useUiToolkitMenu = false;
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject controlsPanel;
    [SerializeField] Button desktopModeButton;
    [SerializeField] Button vrModeButton;

    TechWiseMenuController uiToolkitMenu;
    bool uiToolkitMenuActive;
    string portalStatusMessage = string.Empty;

    Canvas rootCanvas;
    bool vrMenuPlaced;
    RectTransform modernRoot;
    RectTransform loginGroup;
    RectTransform signedInGroup;
    RectTransform modalBackdrop;
    RectTransform practiceModal;
    RectTransform competitionModal;
    RectTransform settingsModal;

    TMP_InputField portalIdentifierInput;
    TMP_InputField portalPasswordInput;
    TMP_Text portalStatusText;
    TMP_Text syncCardTitleText;
    TMP_Text syncCardSubtitleText;
    TMP_Text syncPillText;
    TMP_Text signedInNameText;
    TMP_Text signedInActivityText;
    TMP_Text signedInPendingText;
    TMP_Text competitionModalStatusText;
    TMP_Text competitionAssemblyText;
    TMP_Text competitionDisassemblyText;
    TMP_Text settingsModalStatusText;
    TMP_Text stationBadgeText;
    TMP_Text signedInStationText;
    Button switchStationButton;
    Button checkStationButton;
    Button unpairStationButton;
    Coroutine stationPollingCoroutine;

    public static readonly string[] AvailableStations = new[]
    {
        "station-01",
        "station-02",
        "station-03",
        "station-04",
        "station-05",
        "station-06"
    };

    public static string CurrentStationId
    {
        get => PlayerPrefs.GetString("TechWise_StationId", "station-01");
        set
        {
            PlayerPrefs.SetString("TechWise_StationId", value);
            PlayerPrefs.Save();
        }
    }

    public static string CurrentStationDisplayName
    {
        get
        {
            var id = CurrentStationId;
            return id switch
            {
                "station-01" => "Station 01 (Desk 1)",
                "station-02" => "Station 02 (Desk 2)",
                "station-03" => "Station 03 (Desk 3)",
                "station-04" => "Station 04 (Desk 4)",
                "station-05" => "Station 05 (Desk 5)",
                "station-06" => "Station 06 (Desk 6)",
                _ => id.ToUpper()
            };
        }
    }

    Button portalLoginButton;
    Button portalRefreshButton;
    Button portalLogoutButton;
    Button startButton;
    Button practiceButton;
    Button competitionButton;
    Button controlsButton;
    Button settingsButton;
    Button logoutButton;
    Button quitButton;

    MenuActionVisual startVisual;
    MenuActionVisual practiceVisual;
    MenuActionVisual competitionVisual;
    MenuActionVisual controlsVisual;
    MenuActionVisual settingsVisual;
    MenuActionVisual quitVisual;
    MenuActionVisual logoutVisual;
    TMP_Text accountLabelText;
    RectTransform accountArea;

    readonly List<TechWiseVrCompetition> activeCompetitions = new();

    // Some Quest/OpenXR runtimes expose the Touch trigger through the legacy XR
    // feature API before (or instead of) resolving the Starter Assets UI Press
    // InputAction binding. Feed both sources into XRI's normal UI pointer model so
    // a physical trigger press always produces the expected pointer down/up/click.
    sealed class ControllerTriggerButtonReader : IXRInputButtonReader
    {
        readonly XRInputButtonReader actionReader;
        readonly XRNode node;
        int sampledFrame = -1;
        bool hasSample;
        bool previousPressed;
        bool currentPressed;
        float currentValue;

        public ControllerTriggerButtonReader(XRInputButtonReader actionReader, XRNode node)
        {
            this.actionReader = actionReader;
            this.node = node;
        }

        public bool ReadIsPerformed()
        {
            SampleDevice();
            return actionReader.ReadIsPerformed() || currentPressed;
        }

        public bool ReadWasPerformedThisFrame()
        {
            SampleDevice();
            return actionReader.ReadWasPerformedThisFrame() || (currentPressed && !previousPressed);
        }

        public bool ReadWasCompletedThisFrame()
        {
            SampleDevice();
            return actionReader.ReadWasCompletedThisFrame() || (!currentPressed && previousPressed);
        }

        public float ReadValue()
        {
            SampleDevice();
            return Mathf.Max(actionReader.ReadValue(), currentValue);
        }

        public bool TryReadValue(out float value)
        {
            SampleDevice();
            var readAction = actionReader.TryReadValue(out var actionValue);
            value = Mathf.Max(actionValue, currentValue);
            return readAction || currentValue > 0f;
        }

        void SampleDevice()
        {
            if (sampledFrame == Time.frameCount)
                return;

            sampledFrame = Time.frameCount;
            var pressed = false;
            var val = 0f;

            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var tb) && tb)
                    pressed = true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out var tv))
                {
                    val = Mathf.Max(val, tv);
                    if (tv >= 0.5f) pressed = true;
                }
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var pb) && pb)
                    pressed = true;
                if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out var sb) && sb)
                    pressed = true;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out var gb) && gb)
                    pressed = true;
                if (device.TryGetFeatureValue(CommonUsages.grip, out var gv) && gv >= 0.5f)
                    pressed = true;
            }

#if ENABLE_INPUT_SYSTEM
            var isLeft = node == XRNode.LeftHand;
            foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
            {
                if (dev is UnityEngine.InputSystem.XR.XRController controller)
                {
                    var name = controller.name;
                    var sideMatch = isLeft
                        ? name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0
                        : name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (sideMatch)
                    {
                        var triggerBtn = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerButton");
                        if (triggerBtn != null && triggerBtn.isPressed) pressed = true;

                        var triggerVal = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                        if (triggerVal != null)
                        {
                            var tv = triggerVal.ReadValue();
                            val = Mathf.Max(val, tv);
                            if (tv >= 0.5f) pressed = true;
                        }

                        var primaryBtn = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
                        if (primaryBtn != null && primaryBtn.isPressed) pressed = true;

                        var secondaryBtn = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("secondaryButton");
                        if (secondaryBtn != null && secondaryBtn.isPressed) pressed = true;

                        var gripBtn = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("gripButton");
                        if (gripBtn != null && gripBtn.isPressed) pressed = true;
                    }
                }
            }
#endif

            if (!hasSample)
            {
                hasSample = true;
                previousPressed = pressed;
            }
            else
            {
                previousPressed = currentPressed;
            }

            currentPressed = pressed;
            currentValue = pressed ? Mathf.Max(val, 1f) : val;
        }
    }

    public event Action MenuStateChanged;

    public bool IsUiToolkitMenuActive => uiToolkitMenuActive;
    public bool IsSignedIn => TechWiseSessionStore.HasSession;
    public bool IsDesktopSelected => IsDesktopModeSelected();
    public int ActiveCompetitionCount => activeCompetitions.Count;
    public int PendingSyncCount => TechWiseOfflineAttemptQueue.PendingCount;
    public string PortalStatusMessage => portalStatusMessage;

    public string StudentDisplayName
    {
        get
        {
            var profile = TechWiseSessionStore.GetProfile();
            if (!string.IsNullOrWhiteSpace(profile?.full_name))
                return profile.full_name;
            if (!string.IsNullOrWhiteSpace(profile?.username))
                return profile.username;
            if (!string.IsNullOrWhiteSpace(profile?.email))
                return profile.email;
            return "Student";
        }
    }

    public string StudentFirstName => FirstName(StudentDisplayName);
    public bool HasCompetitionFor(string simulationType) => FindCompetitionFor(simulationType) != null;

    public string CompetitionTitleFor(string simulationType)
    {
        var competition = FindCompetitionFor(simulationType);
        return competition == null ? string.Empty : competition.title;
    }

    sealed class MenuActionVisual
    {
        public Button button;
        public RectTransform root;
        public Image rootImage;
        public TMP_Text title;
        public TMP_Text subtitle;
        public TMP_Text status;
        public TMP_Text icon;
        public Image iconGlyph;
        public TMP_Text arrow;
        public Color32 accent;
        public string signedInSubtitle;
        public string signedOutSubtitle;
        public bool requiresLogin;
        public bool dangerous;
    }

    public static bool IsDesktopModeSelected(bool desktopModeOverridesVrWhenHeadsetPresent = false)
    {
        return false;
    }

    public static bool IsVrHardwareAvailable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Quest initializes tracking after some scene Awake callbacks.
        return true;
#else
        if (XRSettings.isDeviceActive)
            return true;

        var headDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
        foreach (var device in headDevices)
        {
            if (device.isValid)
                return true;
        }

        return false;
#endif
    }

    public void SinglePlayer()
    {
        StartAssemblyLesson();
    }

    public void MultiPlayer()
    {
        PracticeMode();
    }

    public void PracticeMode()
    {
        ShowPracticeChooser();
    }

    public void ShowPracticeChooser()
    {
        HideModal(competitionModal);
        HideModal(settingsModal);
        EnsurePracticeModal();
        ShowModal(practiceModal);
        SetHighlightedAction(practiceVisual);
    }

    public void ShowCompetitionChooser()
    {
        if (!EnsureLoggedInForMode())
            return;

        HideModal(practiceModal);
        HideModal(settingsModal);
        EnsureCompetitionModal();
        RefreshCompetitionModal();
        ShowModal(competitionModal);
        SetHighlightedAction(competitionVisual);
    }

    public void ShowSettings()
    {
        HideModal(practiceModal);
        HideModal(competitionModal);
        EnsureSettingsModal();
        RefreshSettingsModal();
        ShowModal(settingsModal);
        SetHighlightedAction(settingsVisual);
    }

    public void CloseModals()
    {
        if (uiToolkitMenuActive && uiToolkitMenu != null)
            uiToolkitMenu.CloseModal();

        HideModal(practiceModal);
        HideModal(competitionModal);
        HideModal(settingsModal);
        if (modalBackdrop != null)
            modalBackdrop.gameObject.SetActive(false);
        SetHighlightedAction(null);
    }

    public void StartAssemblyLesson()
    {
        TechWiseSimulationModeManager.SetMode(
            TechWiseSimulationModeManager.AssemblyType,
            TechWiseSimulationModeManager.TutorialMode);
        LoadScene(singleplayerSceneName);
    }

    public void StartAssemblyPractice()
    {
        TechWiseSimulationModeManager.SetMode(
            TechWiseSimulationModeManager.AssemblyType,
            TechWiseSimulationModeManager.PracticeMode);
        LoadScene(practiceSceneName);
    }

    public void StartDisassemblyPractice()
    {
        TechWiseSimulationModeManager.SetMode(
            TechWiseSimulationModeManager.DisassemblyType,
            TechWiseSimulationModeManager.PracticeMode);
        LoadScene(practiceSceneName);
    }

    public void StartAssemblyCompetition()
    {
        StartCompetition(TechWiseSimulationModeManager.AssemblyType);
    }

    public void StartDisassemblyCompetition()
    {
        StartCompetition(TechWiseSimulationModeManager.DisassemblyType);
    }

    public void ShowControls()
    {
        if (uiToolkitMenuActive && uiToolkitMenu != null)
        {
            uiToolkitMenu.ShowControlsScreen();
            return;
        }

        UpdateControlsPanelText();
        SetPanelState(false, true);
    }

    public void ShowMainMenu()
    {
        if (uiToolkitMenuActive && uiToolkitMenu != null)
        {
            uiToolkitMenu.ShowMainMenuScreen();
            return;
        }

        SetPanelState(true, false);
    }

    public void UseDesktopMode()
    {
        SetControlMode(VrModeValue);
    }

    public void UseVrMode()
    {
        SetControlMode(VrModeValue);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void OnEnable()
    {
        stationPollingCoroutine = StartCoroutine(StationPollingLoop());
    }

    void OnDisable()
    {
        if (stationPollingCoroutine != null)
        {
            StopCoroutine(stationPollingCoroutine);
            stationPollingCoroutine = null;
        }
    }

    IEnumerator StationPollingLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (!TechWiseSessionStore.HasSession)
            {
                var station = CurrentStationId;
                yield return TechWisePortalClient.EnsureInstance().CheckStationStatusCoroutine(station, (ok, error, resp) =>
                {
                    if (ok && resp != null && resp.status == "paired")
                    {
                        RefreshMenuAuthState();
                        StartCoroutine(LoadCompetitionsCoroutine());
                        RefreshPortalStatus($"Station paired! Welcome, {resp.student?.full_name ?? resp.profile?.full_name ?? "Student"}.");
                    }
                });
            }
        }
    }

    public void CycleNextStation()
    {
        var current = CurrentStationId;
        var index = Array.IndexOf(AvailableStations, current);
        if (index < 0) index = 0;
        var next = AvailableStations[(index + 1) % AvailableStations.Length];
        CurrentStationId = next;
        UpdateStationUi();
        CheckStationNow();
    }

    public void CheckStationNow()
    {
        if (TechWiseSessionStore.HasSession)
        {
            SetPortalStatus("Already paired and linked.");
            return;
        }

        SetPortalStatus($"Checking {CurrentStationDisplayName}...");
        StartCoroutine(TechWisePortalClient.EnsureInstance().CheckStationStatusCoroutine(CurrentStationId, (ok, error, resp) =>
        {
            if (ok && resp != null && resp.status == "paired")
            {
                RefreshMenuAuthState();
                StartCoroutine(LoadCompetitionsCoroutine());
                RefreshPortalStatus($"Station paired! Welcome, {resp.student?.full_name ?? resp.profile?.full_name ?? "Student"}.");
            }
            else if (!ok)
            {
                SetPortalStatus($"Could not check station: {error}");
            }
            else
            {
                SetPortalStatus($"No pairing claim yet for {CurrentStationDisplayName}.\nClick 'Pair Station & Launch VR' on student dashboard.");
            }
        }));
    }

    public void UnpairStation()
    {
        var station = CurrentStationId;
        SetPortalStatus("Unpairing station...");
        StartCoroutine(TechWisePortalClient.EnsureInstance().UnpairStationCoroutine(station, (ok, message) =>
        {
            LogoutPortal();
            SetPortalStatus("Station unpaired. Ready for new student.");
        }));
    }

    void UpdateStationUi()
    {
        if (stationBadgeText != null)
            stationBadgeText.text = CurrentStationDisplayName;
        if (signedInStationText != null)
            signedInStationText.text = $"{CurrentStationDisplayName} Linked";
    }

    void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PlayerPrefs.SetString(ControlModeKey, VrModeValue);
        PlayerPrefs.SetInt(ControlModeExplicitKey, 1);
        PlayerPrefs.Save();

        EnsureEventSystem();
        if (IsVrHardwareAvailable())
            StartCoroutine(RefreshVrUiInteractors());

        if (useUiToolkitMenu && !IsVrHardwareAvailable())
        {
            uiToolkitMenu = GetComponent<TechWiseMenuController>();
            if (uiToolkitMenu == null)
                uiToolkitMenu = gameObject.AddComponent<TechWiseMenuController>();

            uiToolkitMenuActive = uiToolkitMenu.Initialize(this);
        }

        if (!uiToolkitMenuActive)
            EnsureModernLayout();

        ShowMainMenu();
        RefreshModeButtons();
        RefreshMenuAuthState();
        RefreshPortalStatus();
        TechWiseOfflineAttemptQueue.SyncNow();
        StartCoroutine(LoadCompetitionsCoroutine());
    }

    void LateUpdate()
    {
        if (IsVrHardwareAvailable())
            MaintainVrInteractors();

        if (vrMenuPlaced || rootCanvas == null || !IsVrHardwareAvailable())
            return;

        var camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled)
            return;

        ConfigureVrMenu(camera);
        // Allow the XR origin to establish its initial tracked pose before fixing the panel in place.
        vrMenuPlaced = Time.timeSinceLevelLoad > 1f;
    }

    void ConfigureVrMenu(Camera camera)
    {
        EnsureEventSystem();
        rootCanvas.renderMode = RenderMode.WorldSpace;
        rootCanvas.worldCamera = camera;
        var rect = rootCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one * 0.00125f;
        var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        rect.SetPositionAndRotation(camera.transform.position + forward * 1.8f,
            Quaternion.LookRotation(forward, Vector3.up));
        var scaler = rootCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;
        var trRaycaster = rootCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() ?? rootCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        trRaycaster.ignoreReversedGraphics = false;
        trRaycaster.checkFor3DOcclusion = false;
        trRaycaster.checkFor2DOcclusion = false;
        trRaycaster.enabled = true;

        var standardRaycaster = rootCanvas.GetComponent<GraphicRaycaster>();
        if (standardRaycaster != null)
            standardRaycaster.enabled = false;

        Canvas.ForceUpdateCanvases();
    }

    void MaintainVrInteractors()
    {
        EnsureEventSystem();
        var xrModule = FindAnyObjectByType<XRUIInputModule>();
        if (xrModule == null)
            return;

        xrModule.enableXRInput = true;
        xrModule.enabled = true;

        foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactor == null) continue;
            interactor.enableFarCasting = true;
            interactor.enableUIInteraction = true;
            xrModule.RegisterInteractor(interactor);

            if (interactor.transform.parent != null &&
                interactor.transform.parent.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var isLeftController = interactor.transform.parent.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
                var triggerReader = interactor.uiPressInput;
                if (triggerReader != null && triggerReader.bypass is not ControllerTriggerButtonReader)
                    triggerReader.bypass = new ControllerTriggerButtonReader(
                        triggerReader,
                        isLeftController ? XRNode.LeftHand : XRNode.RightHand);

                var visual = interactor.GetComponentInChildren<CurveVisualController>(true);
                if (visual != null)
                {
                    visual.gameObject.SetActive(true);
                    visual.enabled = true;
                    visual.extendLineToEmptyHit = true;
                    visual.maxVisualCurveDistance = 10f;
                    visual.restingVisualLineLength = 0.5f;
                    var lr = visual.GetComponentInChildren<LineRenderer>(true);
                    if (lr != null)
                    {
                        lr.enabled = true;
                        lr.gameObject.SetActive(true);
                    }
                }
            }
        }

        foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ray == null) continue;
            ray.enableUIInteraction = true;
            xrModule.RegisterInteractor(ray);
        }

        foreach (var poke in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (poke == null) continue;
            poke.enableUIInteraction = true;
            xrModule.RegisterInteractor(poke);
        }
    }

    IEnumerator RefreshVrUiInteractors()
    {
        yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (var modality in FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include))
        {
            modality.enabled = false;
            if (modality.leftHand != null) modality.leftHand.SetActive(false);
            if (modality.rightHand != null) modality.rightHand.SetActive(false);
            if (modality.leftController != null) modality.leftController.SetActive(true);
            if (modality.rightController != null) modality.rightController.SetActive(true);
        }
        yield return null;
#endif
        MaintainVrInteractors();
    }

    public void LoginToPortal(string identifier, string password, Action<bool, string> onComplete = null)
    {
        identifier = identifier?.Trim() ?? string.Empty;
        password ??= string.Empty;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            const string missingMessage = "Enter your student username or email and password.";
            SetPortalStatus(missingMessage);
            onComplete?.Invoke(false, missingMessage);
            return;
        }

        SetPortalStatus("Signing in...");
        TechWisePortalClient.EnsureInstance().Login(identifier, password, (ok, message) =>
        {
            SetPortalStatus(message);
            if (ok)
            {
                RefreshMenuAuthState();
                StartCoroutine(LoadCompetitionsCoroutine());
            }

            onComplete?.Invoke(ok, message);
        });
    }

    public void LogoutFromPortal()
    {
        LogoutPortal();
    }

    public void ConnectWebsiteAccount()
    {
        var code = portalIdentifierInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Application.OpenURL(TechWisePortalClient.PortalBaseUrl + "/vr-connect.html");
            SetPortalStatus("Generate a website code, paste it above, then press Connect Code.");
            return;
        }
        SetPortalStatus("Connecting website account...");
        StartCoroutine(TechWisePortalClient.EnsureInstance().ExchangeConnectionCodeCoroutine(code, (ok, message) =>
        {
            portalIdentifierInput.text = "";
            portalPasswordInput.text = "";
            SetPortalStatus(message);
            if (ok) { RefreshMenuAuthState(); StartCoroutine(LoadCompetitionsCoroutine()); }
        }));
    }

    public void RefreshActivitiesFromMenu()
    {
        StartCoroutine(LoadCompetitionsCoroutine());
    }

    public void SyncPendingAttemptsFromMenu()
    {
        TechWiseOfflineAttemptQueue.SyncNow();
        RefreshPortalStatus("Sync requested. Pending attempts will upload when online.");
    }

    public void SetPortalStatusFromMenu(string message)
    {
        SetPortalStatus(message);
    }

    void StartCompetition(string simulationType)
    {
        if (!TechWiseSessionStore.HasSession)
        {
            SetPortalStatus("Log in before starting a competition attempt.");
            RefreshMenuAuthState();
            return;
        }

        var competition = FindCompetitionFor(simulationType);
        TechWiseSimulationModeManager.SetMode(
            simulationType,
            TechWiseSimulationModeManager.CompetitionMode,
            competition?.id ?? string.Empty,
            competition?.title ?? string.Empty);

        if (competition == null)
            SetPortalStatus("No matching active VR activity found. This run will sync as an open competition attempt.");

        LoadScene(competitionSceneName);
    }

    void LoginToPortal()
    {
        if (portalIdentifierInput == null || portalPasswordInput == null)
            return;

        LoginToPortal(portalIdentifierInput.text, portalPasswordInput.text, (ok, _) =>
        {
            if (ok)
                portalPasswordInput.text = string.Empty;
        });
    }

    void LogoutPortal()
    {
        TechWiseSessionStore.Clear();
        activeCompetitions.Clear();
        CloseModals();
        RefreshMenuAuthState();
        RefreshPortalStatus();
    }

    IEnumerator LoadCompetitionsCoroutine()
    {
        if (!TechWiseSessionStore.HasSession)
        {
            RefreshPortalStatus();
            RefreshMenuAuthState();
            yield break;
        }

        SetPortalStatus("Loading active VR activities...");
        var complete = false;
        var ok = false;
        var message = string.Empty;
        TechWiseStudentLeaderboardResponse response = null;

        yield return TechWisePortalClient.EnsureInstance().LoadStudentLeaderboardCoroutine((success, error, result) =>
        {
            ok = success;
            message = error;
            response = result;
            complete = true;
        });

        if (!complete)
            yield break;

        activeCompetitions.Clear();
        if (ok && response?.competitions != null)
        {
            foreach (var competition in response.competitions)
            {
                if (competition != null && competition.status == "active")
                    activeCompetitions.Add(competition);
            }
        }

        RefreshPortalStatus(ok ? string.Empty : message);
        RefreshMenuAuthState();
        RefreshCompetitionModal();
        TechWiseOfflineAttemptQueue.SyncNow();
    }

    void EnsureModernLayout()
    {
        EnsureEventSystem();

        if (modernRoot != null)
            return;

        // Hide any pre-baked overlay canvas from the scene so it doesn't block the VR world space menu
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.gameObject.name.IndexOf("TechWise", StringComparison.OrdinalIgnoreCase) >= 0 &&
                c.gameObject.name.IndexOf("Runtime", StringComparison.OrdinalIgnoreCase) < 0)
            {
                c.gameObject.SetActive(false);
            }
        }

        var canvasObject = new GameObject("TechWise 360 Runtime Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.SetActive(true);
        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 5000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (mainPanel != null && mainPanel != gameObject && !transform.IsChildOf(mainPanel.transform))
            mainPanel.SetActive(false);
        if (controlsPanel != null && controlsPanel != gameObject && !transform.IsChildOf(controlsPanel.transform))
            controlsPanel.SetActive(false);

        modernRoot = CreateRect("TechWise Modern Menu Root", rootCanvas.transform, Vector2.zero, Vector2.one);
        BuildMainPanel();
        BuildControlsPanel();
    }

    void BuildMainPanel()
    {
        var main = CreateRect("Main Panel", modernRoot, Vector2.zero, Vector2.one);
        mainPanel = main.gameObject;
        CreateBackground(main);
        BuildBranding(main);
        BuildStudentSyncCard(main);
        BuildNavigationPanel(main);
        CreateSafetyNote(main);
        BuildModalBackdrop(main);
    }

    void CreateBackground(Transform parent)
    {
        var background = CreateRect("Flat Background", parent, Vector2.zero, Vector2.one);
        var bgImg = background.gameObject.AddComponent<Image>();
        bgImg.color = new Color32(239, 248, 255, 255);
        bgImg.raycastTarget = false;

        var leftWash = CreateRect("Left Pale Blue Area", parent, new Vector2(0f, 0f), new Vector2(0.56f, 1f));
        var leftImg = leftWash.gameObject.AddComponent<Image>();
        leftImg.color = new Color32(226, 242, 253, 255);
        leftImg.raycastTarget = false;

        var rightWhite = CreateRect("Right White Area", parent, new Vector2(0.56f, 0f), new Vector2(1f, 1f));
        var rightImg = rightWhite.gameObject.AddComponent<Image>();
        rightImg.color = new Color32(250, 253, 255, 255);
        rightImg.raycastTarget = false;

        var divider = CreateRect("Vertical Section Divider", parent, new Vector2(0.56f, 0f), new Vector2(0.5615f, 1f));
        var divImg = divider.gameObject.AddComponent<Image>();
        divImg.color = new Color32(207, 231, 245, 255);
        divImg.raycastTarget = false;

        var bottomLine = CreateRect("Bottom Blue Line", parent, new Vector2(0.045f, 0.055f), new Vector2(0.955f, 0.06f));
        var botImg = bottomLine.gameObject.AddComponent<Image>();
        botImg.color = new Color32(60, 181, 235, 255);
        botImg.raycastTarget = false;
    }

    void BuildBranding(Transform parent)
    {
        var title = CreateText(parent, "Brand Title TechWise", "TECHWISE", 70f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = new Color32(11, 16, 32, 255);
        Stretch(title.rectTransform, new Vector2(0.04f, 0.825f), new Vector2(0.31f, 0.92f));

        var title360 = CreateText(parent, "Brand Title 360", "360", 70f, FontStyles.Bold, TextAlignmentOptions.Left);
        title360.color = new Color32(11, 99, 246, 255);
        Stretch(title360.rectTransform, new Vector2(0.315f, 0.825f), new Vector2(0.43f, 0.92f));

        var tagline = CreateText(parent, "Brand Tagline", "Learn. Practice. Compete.", 30f, FontStyles.Normal, TextAlignmentOptions.Left);
        tagline.color = new Color32(95, 113, 139, 255);
        Stretch(tagline.rectTransform, new Vector2(0.043f, 0.765f), new Vector2(0.37f, 0.81f));

        var underline = CreateRect("Brand Underline", parent, new Vector2(0.21f, 0.68f), new Vector2(0.25f, 0.685f));
        Stretch(underline, new Vector2(0.043f, 0.742f), new Vector2(0.073f, 0.747f));
        var underImg = underline.gameObject.AddComponent<Image>();
        underImg.color = TechWiseUITheme.PrimaryBlue;
        underImg.raycastTarget = false;
    }

    void BuildStudentSyncCard(Transform parent)
    {
        var card = CreateRect("Student Sync Card", parent, new Vector2(0.04f, 0.295f), new Vector2(0.53f, 0.735f));
        var cardImg = card.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(cardImg, Color.white, true);
        cardImg.raycastTarget = false;

        var icon = CreateIconBadge(card, "Sync Icon", "User", TechWiseUITheme.PrimaryBlue, new Vector2(0.045f, 0.785f), new Vector2(0.125f, 0.93f));
        icon.fontSizeMax = 28f;

        syncCardTitleText = CreateText(card, "Sync Title", "Student Sync", 34f, FontStyles.Bold, TextAlignmentOptions.Left);
        syncCardTitleText.color = new Color32(11, 16, 32, 255);
        Stretch(syncCardTitleText.rectTransform, new Vector2(0.165f, 0.84f), new Vector2(0.72f, 0.93f));

        syncCardSubtitleText = CreateText(card, "Sync Subtitle", "Log in with a student account to sync competition attempts.", 18f, FontStyles.Normal, TextAlignmentOptions.Left);
        syncCardSubtitleText.color = TechWiseUITheme.MutedText;
        Stretch(syncCardSubtitleText.rectTransform, new Vector2(0.165f, 0.765f), new Vector2(0.72f, 0.835f));

        var pill = CreateRect("Sync Status Pill", card, new Vector2(0.745f, 0.805f), new Vector2(0.92f, 0.895f));
        var pillImg = pill.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(pillImg, new Color32(220, 252, 231, 245), false);
        pillImg.raycastTarget = false;
        syncPillText = CreateText(pill, "Sync Pill Text", "SYNCED", 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        syncPillText.color = TechWiseUITheme.Green;
        Stretch(syncPillText.rectTransform, Vector2.zero, Vector2.one);

        var line = CreateRect("Sync Divider", card, new Vector2(0.04f, 0.735f), new Vector2(0.96f, 0.739f));
        var lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color32(215, 226, 242, 255);
        lineImg.raycastTarget = false;

        loginGroup = CreateRect("Logged Out Group", card, new Vector2(0.06f, 0.075f), new Vector2(0.94f, 0.69f));

        var stationHub = CreateRect("Station Hub Box", loginGroup, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
        TechWiseUITheme.StylePanel(stationHub.gameObject.AddComponent<Image>(), new Color32(244, 248, 255, 255), false);
        var hubOutline = stationHub.gameObject.AddComponent<Outline>();
        hubOutline.effectColor = new Color32(202, 219, 241, 200);
        hubOutline.effectDistance = new Vector2(1f, -1f);
        hubOutline.useGraphicAlpha = false;

        CreateIconBadge(stationHub, "Station Icon", "VR", TechWiseUITheme.PrimaryBlue, new Vector2(0.04f, 0.56f), new Vector2(0.18f, 0.9f));

        var stationHeader = CreateText(stationHub, "Station Header", "HEADSET LAB STATION", 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        stationHeader.color = TechWiseUITheme.PrimaryBlue;
        Stretch(stationHeader.rectTransform, new Vector2(0.21f, 0.74f), new Vector2(0.96f, 0.92f));

        stationBadgeText = CreateText(stationHub, "Station Badge Text", CurrentStationDisplayName, 22f, FontStyles.Bold, TextAlignmentOptions.Left);
        stationBadgeText.color = TechWiseUITheme.Navy;
        Stretch(stationBadgeText.rectTransform, new Vector2(0.21f, 0.52f), new Vector2(0.96f, 0.74f));

        var stationSub = CreateText(stationHub, "Station Sub", "Option B auto-pairing: Pair this station on your dashboard to log in without typing.", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
        stationSub.color = TechWiseUITheme.MutedText;
        Stretch(stationSub.rectTransform, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.48f));

        switchStationButton = CreateLargeButton(stationHub, "Switch Station Button", "Change Station", "Cycle station 01 - 06", CycleNextStation, new Vector2(0.04f, 0.04f), new Vector2(0.48f, 0.25f), TechWiseUITheme.PrimaryBlue, Color.white, "Sync");
        checkStationButton = CreateLargeButton(stationHub, "Check Station Button", "Check Now", "Check dashboard pairing", CheckStationNow, new Vector2(0.52f, 0.04f), new Vector2(0.96f, 0.25f), Color.white, TechWiseUITheme.PrimaryBlue);

        portalStatusText = CreateText(loginGroup, "Portal Status", $"Waiting for pairing on {CurrentStationDisplayName}...\nOpen your dashboard to link.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
        portalStatusText.color = TechWiseUITheme.Navy;
        Stretch(portalStatusText.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.38f));

        signedInGroup = CreateRect("Logged In Group", card, new Vector2(0.06f, 0.065f), new Vector2(0.94f, 0.68f));
        signedInNameText = CreateInfoLine(signedInGroup, "Signed In Name", "Logged in as Student.", "User", 0.78f);
        signedInStationText = CreateInfoLine(signedInGroup, "Signed In Station", $"{CurrentStationDisplayName} Linked", "VR", 0.62f);
        signedInActivityText = CreateInfoLine(signedInGroup, "Signed In Activities", "0 active VR activities loaded.", "VR", 0.46f);
        signedInPendingText = CreateInfoLine(signedInGroup, "Signed In Pending", "Pending sync: 0", "Cloud", 0.30f);

        portalRefreshButton = CreateLargeButton(signedInGroup, "Refresh Button", "Refresh Activities", "Check for new or updated activities", () => StartCoroutine(LoadCompetitionsCoroutine()), new Vector2(0f, 0.02f), new Vector2(0.47f, 0.22f), TechWiseUITheme.PrimaryBlue, Color.white, "Sync");
        unpairStationButton = CreateLargeButton(signedInGroup, "Unpair Button", "Unpair Station", "Release station and log out headset", UnpairStation, new Vector2(0.53f, 0.02f), new Vector2(1f, 0.22f), TechWiseUITheme.Red, Color.white, "X");
        portalLogoutButton = unpairStationButton;
    }

    void BuildNavigationPanel(Transform parent)
    {
        var panel = CreateRect("Action Panel", parent, new Vector2(0.585f, 0.075f), new Vector2(0.965f, 0.975f));
        var panelImg = panel.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(panelImg, Color.white, true);
        panelImg.raycastTarget = false;

        var modeLabel = CreateText(panel, "Mode Label", "SYSTEM MODE", 17f, FontStyles.Bold, TextAlignmentOptions.Left);
        modeLabel.color = TechWiseUITheme.MutedText;
        Stretch(modeLabel.rectTransform, new Vector2(0.075f, 0.915f), new Vector2(0.45f, 0.955f));

        var modeRow = CreateRect("Mode Toggle", panel, new Vector2(0.075f, 0.815f), new Vector2(0.925f, 0.885f));
        vrModeButton = CreateModeButton(modeRow, "VR Mode Button", "Meta Quest 2 (VR Mode)", UseVrMode, Vector2.zero, Vector2.one);
        desktopModeButton = null;

        var mainLabel = CreateText(panel, "Main Menu Label", "MAIN MENU", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        mainLabel.color = TechWiseUITheme.MutedText;
        Stretch(mainLabel.rectTransform, new Vector2(0.075f, 0.755f), new Vector2(0.45f, 0.795f));

        var stack = CreateRect("Button Stack", panel, new Vector2(0.075f, 0.255f), new Vector2(0.925f, 0.725f));
        var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 9f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        startVisual = CreateMenuAction(stack, "Start Button", "Start", "Launch the assembly tutorial.", "Play", TechWiseUITheme.PrimaryBlue, SinglePlayer, false, false);
        practiceVisual = CreateMenuAction(stack, "Practice Mode Button", "Practice Mode", "Choose assembly or disassembly practice.", "Aim", TechWiseUITheme.PrimaryBlue, PracticeMode, false, false);
        competitionVisual = CreateMenuAction(stack, "Competition Button", "Competition", "Compete and sync leaderboard attempts.", "Cup", TechWiseUITheme.PrimaryBlue, ShowCompetitionChooser, true, false);
        controlsVisual = CreateMenuAction(stack, "Controls Button", "Controls", "View keyboard, mouse, and VR controls.", "Pad", TechWiseUITheme.PrimaryBlue, ShowControls, false, false);
        settingsVisual = CreateMenuAction(stack, "Settings Button", "Settings", "View preferences and sync details.", "Gear", TechWiseUITheme.PrimaryBlue, ShowSettings, false, false);
        quitVisual = CreateMenuAction(stack, "Quit Button", "Quit", "Exit TechWise 360.", "Power", TechWiseUITheme.Red, QuitGame, false, true);
        startVisual.signedOutSubtitle = "Launch the assembly tutorial.";
        practiceVisual.signedOutSubtitle = "Choose assembly or disassembly practice.";
        competitionVisual.signedOutSubtitle = "Log in required to compete.";
        settingsVisual.signedOutSubtitle = "View preferences and sync details.";

        startButton = startVisual.button;
        practiceButton = practiceVisual.button;
        competitionButton = competitionVisual.button;
        controlsButton = controlsVisual.button;
        settingsButton = settingsVisual.button;
        quitButton = quitVisual.button;

        accountLabelText = CreateText(panel, "Account Label", "ACCOUNT", 15f, FontStyles.Normal, TextAlignmentOptions.Left);
        accountLabelText.color = TechWiseUITheme.MutedText;
        Stretch(accountLabelText.rectTransform, new Vector2(0.075f, 0.16f), new Vector2(0.4f, 0.195f));

        accountArea = CreateRect("Account Area", panel, new Vector2(0.075f, 0.045f), new Vector2(0.925f, 0.135f));
        logoutVisual = CreateMenuAction(accountArea, "Logout Button", "Log Out", "Sign out of your account.", "User", TechWiseUITheme.PrimaryBlue, LogoutPortal, true, false);
        logoutButton = logoutVisual.button;
    }

    void BuildModalBackdrop(Transform parent)
    {
        modalBackdrop = CreateRect("Modal Backdrop", parent, Vector2.zero, Vector2.one);
        var backdropImg = modalBackdrop.gameObject.AddComponent<Image>();
        backdropImg.color = new Color32(9, 28, 64, 82);
        backdropImg.raycastTarget = true;
        var backdropBtn = modalBackdrop.gameObject.AddComponent<Button>();
        backdropBtn.targetGraphic = backdropImg;
        backdropBtn.onClick.AddListener(CloseModals);
        modalBackdrop.gameObject.SetActive(false);
    }

    void BuildControlsPanel()
    {
        var controls = CreateRect("Controls Panel", modernRoot, Vector2.zero, Vector2.one);
        controlsPanel = controls.gameObject;
        CreateBackground(controls);
        BuildBranding(controls);

        var card = CreateRect("Controls Card", controls, new Vector2(0.095f, 0.075f), new Vector2(0.905f, 0.79f));
        var cardImg = card.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(cardImg, Color.white, true);
        cardImg.raycastTarget = false;

        var title = CreateText(card, "Controls Title", "Meta Quest 2 Controls", 44f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = TechWiseUITheme.Navy;
        Stretch(title.rectTransform, new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.92f));

        var subtitle = CreateText(card, "Controls Subtitle", "6DoF Spatial Interaction and Touch Controller mappings for VR mode.", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = TechWiseUITheme.MutedText;
        Stretch(subtitle.rectTransform, new Vector2(0.15f, 0.755f), new Vector2(0.85f, 0.805f));

        var vrLeft = CreateRect("VR Controls Section", card, new Vector2(0.035f, 0.12f), new Vector2(0.59f, 0.71f));
        var vrLeftTitle = CreateText(vrLeft, "VR Left Title", "Touch Controller Mappings", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
        vrLeftTitle.color = TechWiseUITheme.Navy;
        Stretch(vrLeftTitle.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 1f));
        var mappings = new[]
        {
            ("Grip (Side)", "Grab, hold, and drop PC components"),
            ("Trigger (Index)", "Pull screws, press UI buttons, slot parts"),
            ("Left Stick", "Smooth locomotion / move around workstation"),
            ("Right Stick", "Snap turn and viewpoint rotation"),
            ("Button A / X", "Select interactable item / confirm dialogs"),
            ("Button B / Y", "Toggle pause menu / reset component"),
            ("Proximity", "Automatic headset wear tracking for audit"),
        };

        for (var i = 0; i < mappings.Length; i++)
            CreateControlRow(vrLeft, mappings[i].Item1, mappings[i].Item2, 0.82f - i * 0.11f);

        var vrRight = CreateRect("VR Safety Section", card, new Vector2(0.61f, 0.12f), new Vector2(0.965f, 0.71f));
        TechWiseUITheme.StylePanel(vrRight.gameObject.AddComponent<Image>(), new Color32(244, 248, 255, 255), false);
        var vrRightTitle = CreateText(vrRight, "VR Right Title", "Safety & Verification", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
        vrRightTitle.color = TechWiseUITheme.Navy;
        Stretch(vrRightTitle.rectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f));
        CreateIconGlyph(vrRight, "VR Illustration", TechWiseUITheme.PrimaryBlue, new Vector2(0.38f, 0.52f), new Vector2(0.62f, 0.74f));
        var vrBody = CreateText(vrRight, "VR Body", "Pure VR Simulation Mode\n\n- Option B Station Zero-Typing Pairing\n- Realtime Proximity Sensor Verification\n- Headset & Station Device Fingerprints\n- Offline Queuing with Cloud Auto-Sync", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
        vrBody.color = TechWiseUITheme.Navy;
        Stretch(vrBody.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.48f));

        var back = CreateLargeButton(card, "Back Button", "Back", "", ShowMainMenu, new Vector2(0.84f, 0.035f), new Vector2(0.965f, 0.11f), TechWiseUITheme.PrimaryBlue, Color.white);
        AddLayout(back.gameObject, 0f);
        controlsPanel.SetActive(false);
    }

    void CreateSafetyNote(Transform parent)
    {
        var note = CreateRect("Progress Safe Note", parent, new Vector2(0.048f, 0.075f), new Vector2(0.47f, 0.165f));
        TechWiseUITheme.StylePanel(note.gameObject.AddComponent<Image>(), new Color32(244, 250, 255, 205), false);
        var title = CreateText(note, "Note Title", "Your progress is safe.", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = TechWiseUITheme.Navy;
        Stretch(title.rectTransform, new Vector2(0.17f, 0.48f), new Vector2(0.94f, 0.8f));
        var body = CreateText(note, "Note Body", "Competition data saves locally first, then syncs when online.", 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        body.color = TechWiseUITheme.MutedText;
        Stretch(body.rectTransform, new Vector2(0.17f, 0.18f), new Vector2(0.94f, 0.46f));
        CreateIconBadge(note, "Safe Icon", "OK", TechWiseUITheme.PrimaryBlue, new Vector2(0.045f, 0.25f), new Vector2(0.13f, 0.75f));
    }

    Button CreateModeButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max)
    {
        var buttonRect = CreateRect(name, parent, min, max);
        var image = buttonRect.gameObject.AddComponent<Image>();
        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(202, 219, 241, 255);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        CreateIconImage(buttonRect, label + " Icon", label == "VR" ? "VR" : "Desktop", TechWiseUITheme.Navy, new Vector2(0.24f, 0.3f), new Vector2(0.34f, 0.7f));
        var text = CreateText(buttonRect, "Label", label, 22f, FontStyles.Bold, TextAlignmentOptions.Left);
        text.color = TechWiseUITheme.Navy;
        Stretch(text.rectTransform, new Vector2(0.40f, 0f), new Vector2(0.92f, 1f));
        return button;
    }

    MenuActionVisual CreateMenuAction(Transform parent, string name, string title, string subtitle, string iconText, Color32 accent, UnityEngine.Events.UnityAction action, bool requiresLogin, bool dangerous)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 72f;
        layout.preferredHeight = 76f;
        if (parent.GetComponent<LayoutGroup>() == null)
            Stretch(buttonObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        var image = buttonObject.GetComponent<Image>();
        TechWiseUITheme.StylePanel(image, Color.white, true);
        var outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 225, 244, 180);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var icon = CreateRect("Icon Bubble", buttonObject.transform, new Vector2(0.035f, 0.18f), new Vector2(0.15f, 0.82f));
        var iconImage = icon.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(iconImage, dangerous ? TechWiseUITheme.LightRed : TechWiseUITheme.PaleBlue, false);
        var iconGlyph = CreateIconImage(icon, "Icon", iconText, dangerous ? TechWiseUITheme.Red : accent, new Vector2(0.23f, 0.23f), new Vector2(0.77f, 0.77f));

        var titleText = CreateText(buttonObject.transform, "Title", title, 23f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleText.color = dangerous ? TechWiseUITheme.Red : TechWiseUITheme.Navy;
        Stretch(titleText.rectTransform, new Vector2(0.185f, 0.49f), new Vector2(0.78f, 0.82f));

        var subtitleText = CreateText(buttonObject.transform, "Subtitle", subtitle, 15f, FontStyles.Normal, TextAlignmentOptions.Left);
        subtitleText.color = TechWiseUITheme.MutedText;
        Stretch(subtitleText.rectTransform, new Vector2(0.185f, 0.18f), new Vector2(0.82f, 0.48f));

        var arrow = CreateText(buttonObject.transform, "Arrow", string.Empty, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
        arrow.color = dangerous ? TechWiseUITheme.Red : accent;
        Stretch(arrow.rectTransform, new Vector2(0.9f, 0.28f), new Vector2(0.97f, 0.72f));

        return new MenuActionVisual
        {
            button = button,
            root = buttonObject.GetComponent<RectTransform>(),
            rootImage = image,
            title = titleText,
            subtitle = subtitleText,
            iconGlyph = iconGlyph,
            arrow = arrow,
            accent = accent,
            signedInSubtitle = subtitle,
            signedOutSubtitle = "Log in to use this.",
            requiresLogin = requiresLogin,
            dangerous = dangerous,
        };
    }

    Button CreateLargeButton(Transform parent, string name, string title, string subtitle, UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max, Color32 background, Color32 foreground, string iconText = "")
    {
        var rect = CreateRect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        TechWiseUITheme.StyleButton(button, background, foreground);

        if (!string.IsNullOrWhiteSpace(iconText))
            CreateIconImage(rect, "Button Icon", iconText, foreground, new Vector2(0.13f, 0.35f), new Vector2(0.23f, 0.7f));

        var titleAlignment = string.IsNullOrWhiteSpace(iconText) ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        var titleText = CreateText(rect, "Title", title, 20f, FontStyles.Bold, titleAlignment);
        titleText.color = foreground;
        Stretch(titleText.rectTransform, new Vector2(string.IsNullOrWhiteSpace(iconText) ? 0.04f : 0.29f, string.IsNullOrWhiteSpace(subtitle) ? 0.18f : 0.38f), new Vector2(0.96f, string.IsNullOrWhiteSpace(subtitle) ? 0.82f : 0.78f));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleText = CreateText(rect, "Subtitle", subtitle, 13f, FontStyles.Normal, titleAlignment);
            subtitleText.color = foreground;
            Stretch(subtitleText.rectTransform, new Vector2(string.IsNullOrWhiteSpace(iconText) ? 0.06f : 0.29f, 0.12f), new Vector2(0.94f, 0.38f));
        }

        return button;
    }

    TMP_Text CreateInfoLine(Transform parent, string name, string value, string iconText, float y)
    {
        CreateIconBadge(parent, name + " Icon", iconText, TechWiseUITheme.PrimaryBlue, new Vector2(0f, y - 0.035f), new Vector2(0.07f, y + 0.055f));
        var text = CreateText(parent, name, value, 20f, FontStyles.Normal, TextAlignmentOptions.Left);
        text.color = TechWiseUITheme.Navy;
        Stretch(text.rectTransform, new Vector2(0.09f, y - 0.02f), new Vector2(0.97f, y + 0.055f));
        return text;
    }

    TMP_Text CreateCompetitionInfoRow(Transform parent, string name, string title, string subtitle, string iconText, float y)
    {
        var row = CreateRect(name + " Row", parent, new Vector2(0.06f, y), new Vector2(0.94f, y + 0.085f));
        TechWiseUITheme.StylePanel(row.gameObject.AddComponent<Image>(), Color.white, false);
        var outline = row.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 225, 244, 180);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        CreateIconBadge(row, name + " Icon", iconText, TechWiseUITheme.PrimaryBlue, new Vector2(0.035f, 0.18f), new Vector2(0.13f, 0.82f));

        var titleText = CreateText(row, name + " Title", title, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleText.color = TechWiseUITheme.Navy;
        Stretch(titleText.rectTransform, new Vector2(0.17f, 0.48f), new Vector2(0.94f, 0.82f));

        var subtitleText = CreateText(row, name, subtitle, 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        subtitleText.color = TechWiseUITheme.MutedText;
        Stretch(subtitleText.rectTransform, new Vector2(0.17f, 0.17f), new Vector2(0.94f, 0.48f));
        return subtitleText;
    }

    TMP_Text CreateIconBadge(Transform parent, string name, string value, Color32 color, Vector2 min, Vector2 max)
    {
        var rect = CreateRect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(image, new Color(color.r / 255f, color.g / 255f, color.b / 255f, 0.12f), false);
        CreateIconImage(rect, name + " Glyph", value, color, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
        var text = CreateText(rect, name + " Text", string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = color;
        Stretch(text.rectTransform, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
        return text;
    }

    Image CreateIconGlyph(Transform parent, string name, Color32 color, Vector2 min, Vector2 max)
    {
        var glyph = CreateRect(name, parent, min, max);
        var image = glyph.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(image, color, false);
        image.raycastTarget = false;
        return image;
    }

    Image CreateIconImage(Transform parent, string name, string iconKey, Color32 color, Vector2 min, Vector2 max)
    {
        var glyph = CreateRect(name, parent, min, max);
        var image = glyph.gameObject.AddComponent<Image>();
        image.sprite = TechWiseUITheme.IconSprite(iconKey, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TMP_InputField CreateInput(Transform parent, string placeholder, Vector2 min, Vector2 max, string iconText)
    {
        var root = CreateRect(placeholder, parent, min, max);
        var image = root.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(image, Color.white, false);
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(202, 219, 241, 255);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        var input = root.gameObject.AddComponent<TMP_InputField>();

        CreateIconImage(root, "Input Icon", iconText, TechWiseUITheme.PrimaryBlue, new Vector2(0.035f, 0.28f), new Vector2(0.085f, 0.72f));

        var text = CreateText(root, "Text", "", 19f, FontStyles.Normal, TextAlignmentOptions.Left);
        text.color = TechWiseUITheme.Navy;
        Stretch(text.rectTransform, new Vector2(0.11f, 0.16f), new Vector2(0.96f, 0.84f));

        var placeholderText = CreateText(root, "Placeholder", placeholder, 19f, FontStyles.Normal, TextAlignmentOptions.Left);
        placeholderText.color = new Color32(120, 136, 160, 255);
        Stretch(placeholderText.rectTransform, new Vector2(0.11f, 0.16f), new Vector2(0.96f, 0.84f));

        input.textComponent = text;
        input.placeholder = placeholderText;
        return input;
    }

    void EnsurePracticeModal()
    {
        if (practiceModal != null)
            return;

        practiceModal = CreateModal("Practice Mode", "Choose a practice mode.\nPractice attempts stay local and do not affect the leaderboard.", "Aim");
        CreateModalAction(practiceModal, "Assembly Practice Modal Button", "Assembly Practice", "Practice assembling components.", "CPU", StartAssemblyPractice, 0.44f, TechWiseUITheme.PrimaryBlue, Color.white);
        CreateModalAction(practiceModal, "Disassembly Practice Modal Button", "Disassembly Practice", "Practice disassembling components.", "Tool", StartDisassemblyPractice, 0.31f, TechWiseUITheme.PrimaryBlue, Color.white);
        CreateModalAction(practiceModal, "Close Practice Modal Button", "Close", "Return to the main menu.", "X", CloseModals, 0.13f, Color.white, TechWiseUITheme.Navy);
        practiceModal.gameObject.SetActive(false);
    }

    void EnsureCompetitionModal()
    {
        if (competitionModal != null)
            return;

        competitionModal = CreateModal("Competition", "", "Cup");
        var statusBox = CreateRect("Competition Status", competitionModal, new Vector2(0.06f, 0.63f), new Vector2(0.94f, 0.74f));
        TechWiseUITheme.StylePanel(statusBox.gameObject.AddComponent<Image>(), new Color32(245, 251, 255, 245), false);
        competitionModalStatusText = CreateText(statusBox, "Status Text", "", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        competitionModalStatusText.color = TechWiseUITheme.Navy;
        Stretch(competitionModalStatusText.rectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.96f, 0.86f));

        competitionAssemblyText = CreateCompetitionInfoRow(competitionModal, "Assembly Competition Info", "Assembly", "Open attempt if allowed by dashboard.", "CPU", 0.51f);
        competitionDisassemblyText = CreateCompetitionInfoRow(competitionModal, "Disassembly Competition Info", "Disassembly", "Open attempt if allowed by dashboard.", "Tool", 0.40f);

        CreateModalAction(competitionModal, "Assembly Competition Modal Button", "Assembly Competition", "Compete in assembly challenges.", "Cup", StartAssemblyCompetition, 0.28f, Color.white, TechWiseUITheme.Navy);
        CreateModalAction(competitionModal, "Disassembly Competition Modal Button", "Disassembly Competition", "Compete in disassembly challenges.", "Tool", StartDisassemblyCompetition, 0.17f, Color.white, TechWiseUITheme.Navy);
        CreateModalAction(competitionModal, "Refresh Competition Modal Button", "Refresh Activities", "Check for new or updated activities.", "Sync", () => StartCoroutine(LoadCompetitionsCoroutine()), 0.06f, Color.white, TechWiseUITheme.PrimaryBlue);
        CreateModalAction(competitionModal, "Close Competition Modal Button", "Close", "Return to the main menu.", "X", CloseModals, -0.05f, Color.white, TechWiseUITheme.Navy);
        competitionModal.gameObject.SetActive(false);
    }

    void EnsureSettingsModal()
    {
        if (settingsModal != null)
            return;

        settingsModal = CreateModal("Settings", "", "Gear");
        settingsModalStatusText = CreateText(settingsModal, "Settings Status", "", 18f, FontStyles.Normal, TextAlignmentOptions.Left);
        settingsModalStatusText.color = TechWiseUITheme.Navy;
        Stretch(settingsModalStatusText.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.72f));
        CreateModalAction(settingsModal, "Sync Now Settings Button", "Sync Now", "Upload pending competition attempts.", "Sync", () =>
        {
            TechWiseOfflineAttemptQueue.SyncNow();
            RefreshSettingsModal();
            RefreshPortalStatus();
        }, 0.24f, TechWiseUITheme.PrimaryBlue, Color.white);
        CreateModalAction(settingsModal, "Close Settings Modal Button", "Close", "Return to the main menu.", "X", CloseModals, 0.1f, Color.white, TechWiseUITheme.Navy);
        settingsModal.gameObject.SetActive(false);
    }

    RectTransform CreateModal(string titleText, string subtitleText, string iconText)
    {
        var modal = CreateRect(titleText + " Modal", mainPanel.transform, new Vector2(0.32f, 0.12f), new Vector2(0.68f, 0.82f));
        TechWiseUITheme.StylePanel(modal.gameObject.AddComponent<Image>(), new Color32(255, 255, 255, 248));
        modal.SetAsLastSibling();

        CreateIconBadge(modal, titleText + " Icon", iconText, TechWiseUITheme.PrimaryBlue, new Vector2(0.42f, 0.85f), new Vector2(0.52f, 0.96f));
        var title = CreateText(modal, titleText + " Title", titleText, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = TechWiseUITheme.Navy;
        Stretch(title.rectTransform, new Vector2(0.16f, 0.78f), new Vector2(0.84f, 0.88f));

        var close = CreateLargeButton(modal, titleText + " X Button", "X", "", CloseModals, new Vector2(0.89f, 0.86f), new Vector2(0.96f, 0.95f), Color.white, TechWiseUITheme.PrimaryBlue);
        AddLayout(close.gameObject, 0f);

        if (!string.IsNullOrWhiteSpace(subtitleText))
        {
            var subtitle = CreateText(modal, titleText + " Subtitle", subtitleText, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            subtitle.color = TechWiseUITheme.Navy;
            Stretch(subtitle.rectTransform, new Vector2(0.12f, 0.66f), new Vector2(0.88f, 0.76f));
        }

        return modal;
    }

    void CreateModalAction(RectTransform modal, string name, string title, string subtitle, string iconText, UnityEngine.Events.UnityAction action, float y, Color32 background, Color32 foreground)
    {
        var minY = Mathf.Clamp01(y);
        var maxY = Mathf.Clamp01(y + 0.105f);
        var button = CreateLargeButton(modal, name, title, subtitle, action, new Vector2(0.06f, minY), new Vector2(0.94f, maxY), background, foreground);
        if (background == Color.white)
            TechWiseUITheme.StyleButton(button, Color.white, foreground, false);

        var outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 225, 244, 180);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        var iconColor = foreground == Color.white ? TechWiseUITheme.PrimaryBlue : foreground;
        CreateIconBadge(button.transform, "Icon", iconText, iconColor, new Vector2(0.035f, 0.18f), new Vector2(0.13f, 0.82f));
        var arrow = CreateText(button.transform, "Arrow", string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        arrow.color = foreground;
        Stretch(arrow.rectTransform, new Vector2(0.88f, 0.25f), new Vector2(0.96f, 0.75f));
    }

    void RefreshCompetitionModal()
    {
        if (competitionModalStatusText == null)
            return;

        var assembly = FindCompetitionFor(TechWiseSimulationModeManager.AssemblyType);
        var disassembly = FindCompetitionFor(TechWiseSimulationModeManager.DisassemblyType);
        var network = Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Online";
        competitionModalStatusText.text =
            $"{network}. {activeCompetitions.Count} active VR activities loaded.\n" +
            $"Pending sync: {TechWiseOfflineAttemptQueue.PendingCount}.";
        if (competitionAssemblyText != null)
            competitionAssemblyText.text = assembly == null ? "Open attempt if allowed by dashboard." : assembly.title;
        if (competitionDisassemblyText != null)
            competitionDisassemblyText.text = disassembly == null ? "Open attempt if allowed by dashboard." : disassembly.title;
    }

    void RefreshSettingsModal()
    {
        if (settingsModalStatusText == null)
            return;

        var profile = TechWiseSessionStore.GetProfile();
        var name = TechWiseSessionStore.HasSession
            ? string.IsNullOrWhiteSpace(profile?.full_name) ? "Student" : profile.full_name
            : "Not signed in";
        var network = Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Online";

        settingsModalStatusText.text =
            $"Account: {name}\n" +
            $"Network: {network}\n" +
            $"Pending sync: {TechWiseOfflineAttemptQueue.PendingCount}\n" +
            $"Portal: {TechWisePortalClient.PortalBaseUrl}";
    }

    void ShowModal(RectTransform modal)
    {
        if (modalBackdrop != null)
            modalBackdrop.gameObject.SetActive(true);
        if (modal != null)
        {
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
        }
    }

    static void HideModal(RectTransform modal)
    {
        if (modal != null)
            modal.gameObject.SetActive(false);
    }

    void SetHighlightedAction(MenuActionVisual active)
    {
        SetActionHighlighted(startVisual, active == startVisual);
        SetActionHighlighted(practiceVisual, active == practiceVisual);
        SetActionHighlighted(competitionVisual, active == competitionVisual);
        SetActionHighlighted(controlsVisual, active == controlsVisual);
        SetActionHighlighted(settingsVisual, active == settingsVisual);
        SetActionHighlighted(quitVisual, active == quitVisual);
        SetActionHighlighted(logoutVisual, active == logoutVisual);
    }

    static void SetActionHighlighted(MenuActionVisual visual, bool highlighted)
    {
        if (visual?.rootImage == null)
            return;

        visual.rootImage.color = highlighted ? visual.accent : Color.white;
        if (visual.title != null)
            visual.title.color = highlighted ? Color.white : visual.dangerous ? TechWiseUITheme.Red : TechWiseUITheme.Navy;
        if (visual.subtitle != null)
            visual.subtitle.color = highlighted ? new Color32(220, 236, 255, 255) : TechWiseUITheme.MutedText;
        if (visual.icon != null)
            visual.icon.color = highlighted ? Color.white : visual.dangerous ? TechWiseUITheme.Red : visual.accent;
        if (visual.iconGlyph != null)
            visual.iconGlyph.color = highlighted ? Color.white : visual.dangerous ? TechWiseUITheme.Red : visual.accent;
        if (visual.arrow != null)
            visual.arrow.color = highlighted ? Color.white : visual.dangerous ? TechWiseUITheme.Red : visual.accent;
    }

    TechWiseVrCompetition FindCompetitionFor(string simulationType)
    {
        foreach (var competition in activeCompetitions)
        {
            if (competition == null)
                continue;

            if (competition.simulation_type == "both" || competition.simulation_type == simulationType)
                return competition;
        }

        return null;
    }

    void RefreshMenuAuthState()
    {
        var signedIn = TechWiseSessionStore.HasSession;

        if (loginGroup != null)
            loginGroup.gameObject.SetActive(!signedIn);
        if (signedInGroup != null)
            signedInGroup.gameObject.SetActive(signedIn);
        if (syncPillText != null)
            syncPillText.transform.parent.gameObject.SetActive(signedIn);
        if (accountLabelText != null)
            accountLabelText.gameObject.SetActive(signedIn);
        if (accountArea != null)
            accountArea.gameObject.SetActive(signedIn);

        SetActionAvailability(startVisual, true);
        SetActionAvailability(practiceVisual, true);
        SetActionAvailability(competitionVisual, signedIn);
        SetActionAvailability(settingsVisual, true);
        SetActionAvailability(logoutVisual, signedIn);
        SetActionAvailability(controlsVisual, true);
        SetActionAvailability(quitVisual, true);
        SetHighlightedAction(null);

        if (!signedIn)
            CloseModals();

        RaiseMenuStateChanged();
    }

    static void SetActionAvailability(MenuActionVisual visual, bool available)
    {
        if (visual == null)
            return;

        var enabled = !visual.requiresLogin || available;
        if (visual.button != null)
            visual.button.interactable = true;
        if (visual.subtitle != null)
            visual.subtitle.text = enabled ? visual.signedInSubtitle : visual.signedOutSubtitle;
        if (visual.arrow != null)
            visual.arrow.text = string.Empty;
        if (visual.rootImage != null)
            visual.rootImage.color = enabled ? Color.white : new Color32(246, 248, 252, 255);
        if (visual.iconGlyph != null)
            visual.iconGlyph.color = enabled
                ? visual.dangerous ? TechWiseUITheme.Red : visual.accent
                : new Color32(126, 143, 168, 255);
        if (visual.title != null)
            visual.title.color = enabled
                ? visual.dangerous ? TechWiseUITheme.Red : TechWiseUITheme.Navy
                : TechWiseUITheme.MutedText;
        if (visual.subtitle != null && !enabled)
            visual.subtitle.color = TechWiseUITheme.MutedText;
    }

    void RefreshPortalStatus(string fallbackMessage = "")
    {
        portalStatusMessage = fallbackMessage ?? string.Empty;
        UpdateStationUi();
        var signedIn = TechWiseSessionStore.HasSession;
        if (!signedIn)
        {
            if (syncCardTitleText != null)
                syncCardTitleText.text = "VR Lab Station Pairing";
            if (syncCardSubtitleText != null)
                syncCardSubtitleText.text = "Option B zero-typing pairing: Pair from your student web dashboard.";
            if (portalStatusText != null)
                portalStatusText.text = string.IsNullOrWhiteSpace(fallbackMessage)
                    ? $"Waiting for student pairing on {CurrentStationDisplayName}...\nOpen your dashboard and click 'Pair Station & Launch VR'."
                    : fallbackMessage;
            RaiseMenuStateChanged();
            return;
        }

        var profile = TechWiseSessionStore.GetProfile();
        var name = string.IsNullOrWhiteSpace(profile?.full_name) ? "Student" : profile.full_name;
        var pending = TechWiseOfflineAttemptQueue.PendingCount;
        var activityText = activeCompetitions.Count == 0
            ? "No active VR activities loaded."
            : $"{activeCompetitions.Count} active VR activity{(activeCompetitions.Count == 1 ? string.Empty : "ies")} loaded.";

        if (syncCardTitleText != null)
            syncCardTitleText.text = $"Welcome back, {FirstName(name)}!";
        if (syncCardSubtitleText != null)
            syncCardSubtitleText.text = $"{CurrentStationDisplayName} linked. Your progress is saved automatically.";
        if (syncPillText != null)
        {
            syncPillText.text = pending == 0 ? "SYNCED" : $"PENDING {pending}";
            syncPillText.color = pending == 0 ? TechWiseUITheme.Green : TechWiseUITheme.PrimaryBlue;
        }
        if (signedInNameText != null)
            signedInNameText.text = $"Logged in as {name}.";
        if (signedInStationText != null)
            signedInStationText.text = $"{CurrentStationDisplayName} Linked";
        if (signedInActivityText != null)
            signedInActivityText.text = activityText;
        if (signedInPendingText != null)
            signedInPendingText.text = $"Pending sync: {pending}";
        if (portalStatusText != null)
            portalStatusText.text = fallbackMessage;
        RaiseMenuStateChanged();
    }

    void SetPortalStatus(string message)
    {
        portalStatusMessage = message ?? string.Empty;
        if (portalStatusText != null)
            portalStatusText.text = portalStatusMessage;
        RaiseMenuStateChanged();
    }

    void RefreshModeButtons()
    {
        if (desktopModeButton != null)
            desktopModeButton.gameObject.SetActive(false);
        SetModeButtonState(vrModeButton, true);
        RaiseMenuStateChanged();
    }

    static void SetModeButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        TechWiseUITheme.StyleButton(
            button,
            selected ? TechWiseUITheme.PrimaryBlue : Color.white,
            selected ? Color.white : TechWiseUITheme.Navy,
            selected);

        var targetGraphic = button.targetGraphic as Image;
        foreach (var image in button.GetComponentsInChildren<Image>(true))
        {
            if (image != targetGraphic)
                image.color = selected ? Color.white : TechWiseUITheme.Navy;
        }
    }

    bool EnsureLoggedInForMode()
    {
        if (TechWiseSessionStore.HasSession)
            return true;

        SetPortalStatus("Log in with a student account before opening a game mode.");
        RefreshMenuAuthState();
        return false;
    }

    void SetControlMode(string mode)
    {
        PlayerPrefs.SetString(ControlModeKey, mode);
        PlayerPrefs.SetInt(ControlModeExplicitKey, 1);
        PlayerPrefs.Save();
        RefreshModeButtons();
    }

    void RaiseMenuStateChanged()
    {
        MenuStateChanged?.Invoke();
    }

    void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("MainMenu scene name is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    void SetPanelState(bool showMain, bool showControls)
    {
        if (!showMain)
            CloseModals();

        if (mainPanel != null)
            mainPanel.SetActive(showMain);

        if (controlsPanel != null)
            controlsPanel.SetActive(showControls);
    }

    void UpdateControlsPanelText()
    {
        // The modern controls panel is built from individual rows, so there is no old text block to update.
    }

    static void EnsureEventSystem()
    {
        var eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = new GameObject("TechWise Menu EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        eventSystem.enabled = true;
        // XRUIInputModule handles both tracked XR pointers and desktop mouse/gamepad.
        // Keeping one input module avoids duplicate clicks and guarantees interactors
        // can register during scene activation instead of after a runtime conversion.
        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            if (!(module is XRUIInputModule)) module.enabled = false;

        var xrModule = eventSystem.GetComponent<XRUIInputModule>();
        if (xrModule == null)
            xrModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        xrModule.enableXRInput = true;
        xrModule.enableMouseInput = true;
        xrModule.enableTouchInput = true;
        xrModule.enableGamepadInput = true;
        xrModule.enableJoystickInput = true;
        xrModule.enableBuiltinActionsAsFallback = true;
        xrModule.enabled = true;
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect, anchorMin, anchorMax);
        return rect;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string value, float maxSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    static void AddLayout(GameObject obj, float preferredHeight)
    {
        var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        if (preferredHeight > 0f)
        {
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
        }
    }

    void CreateControlRow(Transform parent, string key, string action, float y)
    {
        var row = CreateRect("Control Row " + key, parent, new Vector2(0f, y), new Vector2(1f, y + 0.06f));
        TechWiseUITheme.StylePanel(row.gameObject.AddComponent<Image>(), new Color32(255, 255, 255, 230), false);
        var keyBox = CreateRect("Key", row, new Vector2(0.02f, 0.14f), new Vector2(0.27f, 0.86f));
        TechWiseUITheme.StylePanel(keyBox.gameObject.AddComponent<Image>(), new Color32(247, 252, 255, 255), false);
        var keyText = CreateText(keyBox, "Key Text", key, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        keyText.color = TechWiseUITheme.PrimaryBlue;
        Stretch(keyText.rectTransform, Vector2.zero, Vector2.one);
        var actionText = CreateText(row, "Action Text", action, 16f, FontStyles.Normal, TextAlignmentOptions.Left);
        actionText.color = TechWiseUITheme.Navy;
        Stretch(actionText.rectTransform, new Vector2(0.32f, 0.08f), new Vector2(0.98f, 0.92f));
    }

    static string FirstName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Student";

        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed.Substring(0, space) : trimmed;
    }
}
