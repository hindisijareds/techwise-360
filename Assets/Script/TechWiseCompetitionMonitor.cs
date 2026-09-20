using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Competition assembly presentation driven by the immutable DONE assessment.</summary>
public sealed class TechWiseCompetitionMonitor : MonoBehaviour
{
    internal enum ScreenState { Waiting, Ready, Checking, Error, Desktop }
    internal ScreenState State { get; private set; }
    static TechWiseCompetitionMonitor instance;
    GameObject station;
    GameObject desktop;
    TMP_Text title, body, buttonLabel;
    Button actionButton, previousButton, beginningButton;
    Material frameMaterial, cableMaterial;
    readonly List<string> errors = new();
    internal IReadOnlyList<string> Errors => errors;
    internal static bool Applies => TechWiseSimulationModeManager.IsCompetitionMode &&
        SceneManager.GetActiveScene().name == "Multiplayer";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var root = new GameObject("TechWise Competition PC Verification");
        DontDestroyOnLoad(root); instance = root.AddComponent<TechWiseCompetitionMonitor>();
    }
    void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
    void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; Clear(); }
    void SceneLoaded(Scene scene, LoadSceneMode mode) => Clear();
    void Clear()
    {
        StopAllCoroutines();
        if (station != null) Destroy(station);
        if (frameMaterial != null) Destroy(frameMaterial);
        if (cableMaterial != null) Destroy(cableMaterial);
        station = null; errors.Clear(); State = ScreenState.Waiting;
    }
    void Update()
    {
        if (!Applies) { if (station != null) Clear(); return; }
        if (station == null) TryCreate();
        if (station == null) return;
        var recorder = FindAnyObjectByType<TechWiseAttemptRecorder>();
        bool finished = recorder != null && recorder.IsFinished;
        bool running = recorder != null && recorder.CanSubmitMonitor;
        bool readyToStart = recorder != null && recorder.IsReadyToStart;

        previousButton.interactable = recorder != null && recorder.CanResetPrevious;
        beginningButton.interactable = recorder != null && (running || finished);
        if (State == ScreenState.Waiting)
        {
            if (finished)
            {
                TransitionToSubmitted(recorder);
                return;
            }

            actionButton.interactable = running || readyToStart;
            if (running)
            {
                buttonLabel.text = "DONE";
                var (timeStr, score, mistakes, progress) = recorder.GetLiveMetrics();
                title.text = "COMPETITION RUNNING";
                body.text = $"TIME: {timeStr}    SCORE: {score}%\nMISTAKES: {mistakes}    PROGRESS: {progress}\n\nAssemble your PC components, then press DONE below.\nDONE locks your assembly and stops the timer.";
            }
            else if (readyToStart)
            {
                buttonLabel.text = "START";
                title.text = "PC ASSEMBLY COMPETITION";
                body.text = "Competition is ready!\n\nPress START here, on the floating prompt, or pull Trigger / press A to begin.";
            }
            else
            {
                buttonLabel.text = "WAIT";
                title.text = "PC ASSEMBLY COMPETITION";
                body.text = "Preparing competition...";
            }
        }
        else if (State == ScreenState.Ready)
        {
            UpdateSubmittedDisplay(recorder);
        }
    }
    void TryCreate()
    {
        var simulation = TechWiseSimulationRuntime.Instance;
        var socket = simulation != null ? simulation.FindSocket("Motherboard") : null;
        var camera = Camera.main;
        if (socket == null || camera == null) return;
        var caseRoot = socket.transform.parent;
        var bounds = new Bounds(socket.transform.position, Vector3.one * .3f);
        foreach (var renderer in caseRoot.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        var towardPlayer = Vector3.ProjectOnPlane(camera.transform.position - bounds.center, Vector3.up).normalized;
        if (towardPlayer.sqrMagnitude < .1f) towardPlayer = Vector3.back;
        var right = Vector3.Cross(Vector3.up, -towardPlayer);
        station = new GameObject("Competition verification monitor");
        station.transform.position = new Vector3(bounds.center.x, bounds.min.y + .46f, bounds.center.z) + right * (bounds.extents.x + .44f) + towardPlayer * .1f;
        station.transform.rotation = Quaternion.LookRotation(-towardPlayer, Vector3.up);
        var position=station.transform.position;
        position.y=TechWiseWorkbenchSurface.Height(position,bounds.min.y)+.371f;
        station.transform.position=position;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        frameMaterial = new Material(shader); frameMaterial.color = new Color(.025f, .032f, .04f);
        if (frameMaterial.HasProperty("_BaseColor")) frameMaterial.SetColor("_BaseColor", frameMaterial.color);
        cableMaterial = new Material(shader); cableMaterial.color = new Color(.12f, .14f, .16f);
        if (cableMaterial.HasProperty("_BaseColor")) cableMaterial.SetColor("_BaseColor", cableMaterial.color);
        Box("Monitor bezel", new Vector3(0, 0, .026f), new Vector3(.71f, .45f, .045f));
        Box("Monitor stand", new Vector3(0, -.28f, .05f), new Vector3(.055f, .15f, .05f));
        Box("Monitor base", new Vector3(0, -.36f, .01f), new Vector3(.3f, .022f, .19f));
        var display = new GameObject("Verification display", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        display.transform.SetParent(station.transform, false);
        var canvas = display.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var rect = display.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(900, 550); rect.localScale = Vector3.one * .00075f;
        display.GetComponent<Image>().color = new Color(.015f, .035f, .07f);
        display.AddComponent<GraphicRaycaster>();
        var raycaster = display.AddComponent<TrackedDeviceGraphicRaycaster>(); raycaster.checkFor3DOcclusion = false; raycaster.checkFor2DOcclusion = false;
        title = Text(display.transform, "PC ASSEMBLY VERIFICATION", 38, new Vector2(.05f, .82f), new Vector2(.95f, .97f));
        title.color = new Color(.35f, .85f, 1);
        body = Text(display.transform, "", 28, new Vector2(.05f, .2f), new Vector2(.95f, .81f));
        TechWiseScrollableText.Wrap(body);
        var buttonObject = new GameObject("Done / Test PC", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(display.transform, false);
        var buttonRect = buttonObject.GetComponent<RectTransform>(); buttonRect.anchorMin = new Vector2(.69f, .04f); buttonRect.anchorMax = new Vector2(.95f, .17f); buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        buttonObject.GetComponent<Image>().color = new Color(.08f, .35f, .5f);
        actionButton = buttonObject.GetComponent<Button>(); actionButton.targetGraphic = buttonObject.GetComponent<Image>(); actionButton.onClick.AddListener(Press);
        buttonLabel = Text(buttonObject.transform, "DONE", 30, Vector2.zero, Vector2.one); buttonLabel.alignment = TextAlignmentOptions.Center;
        desktop = Panel(display.transform, "PC desktop", new Vector2(.025f, .19f), new Vector2(.975f, .81f), new Color(.025f, .15f, .3f)).gameObject;
        Panel(desktop.transform, "Wallpaper accent", new Vector2(.55f, .2f), new Vector2(1, 1), new Color(.035f, .23f, .42f));
        Text(desktop.transform, "TechWise OS", 38, new Vector2(.55f, .5f), new Vector2(.96f, .72f));
        Text(desktop.transform, "Your PC is ready", 23, new Vector2(.55f, .39f), new Vector2(.96f, .51f));
        string[] iconNames = { "This PC", "Files", "Settings" };
        for (int i = 0; i < iconNames.Length; i++)
        {
            float x = .06f + i * .15f;
            Panel(desktop.transform, iconNames[i] + " icon", new Vector2(x, .63f), new Vector2(x + .075f, .8f), i == 1 ? new Color(.96f, .71f, .25f) : new Color(.55f, .82f, .95f));
            Text(desktop.transform, iconNames[i], 19, new Vector2(x - .01f, .5f), new Vector2(x + .13f, .61f));
        }
        Panel(desktop.transform, "Desktop taskbar", Vector2.zero, new Vector2(1, .14f), new Color(.015f, .045f, .1f));
        Text(desktop.transform, "Start     |     System ready", 20, new Vector2(.025f, .015f), new Vector2(.8f, .13f));
        desktop.SetActive(false);
        previousButton=ResetButton(display.transform,"Reset Previous",.04f,.35f,()=>FindAnyObjectByType<TechWiseAttemptRecorder>()?.ResetToPreviousPoint());
        beginningButton=ResetButton(display.transform,"Reset Beginning",.37f,.67f,()=>FindAnyObjectByType<TechWiseAttemptRecorder>()?.ResetToBeginning());
        // Route the visible display cable from the back of the monitor to the case.
        var cable = new GameObject("Monitor cable to PC unit").AddComponent<LineRenderer>(); cable.transform.SetParent(station.transform, false);
        cable.sharedMaterial = cableMaterial; cable.widthMultiplier = .009f; cable.positionCount = 5;
        var start = station.transform.TransformPoint(new Vector3(0, -.13f, .055f));
        var end = bounds.center - towardPlayer * bounds.extents.z;
        cable.SetPositions(new[] { start, start + Vector3.down * .2f, new Vector3((start.x + end.x) * .5f, bounds.min.y + .035f, (start.z + end.z) * .5f), end + Vector3.down * .15f, end });
    }
    static Button ResetButton(Transform parent,string label,float min,float max,UnityEngine.Events.UnityAction action)
    {
        var root=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button)); root.transform.SetParent(parent,false);
        var rect=root.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(min,.04f);rect.anchorMax=new Vector2(max,.17f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        root.GetComponent<Image>().color=new Color(.08f,.25f,.36f);
        var button=root.GetComponent<Button>();button.targetGraphic=root.GetComponent<Image>();button.onClick.AddListener(action);
        Text(root.transform,label,23,new Vector2(.04f,.05f),new Vector2(.96f,.95f)).alignment=TextAlignmentOptions.Center;
        return button;
    }
    void Box(string name, Vector3 position, Vector3 scale)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(station.transform, false);
        obj.transform.localPosition = position; obj.transform.localScale = scale; obj.GetComponent<Renderer>().sharedMaterial = frameMaterial;
        // Keep physical stand/base colliders; UI raycaster ignores 3D occlusion.
    }
    static TMP_Text Text(Transform parent, string value, float size, Vector2 min, Vector2 max)
    {
        var obj = new GameObject("Monitor text", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.color = Color.white;
        text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = size; text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false; var rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return text;
    }
    static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
    }
    internal void Press()
    {
        if (!Applies) return;
        if (State == ScreenState.Waiting)
        {
            var recorder = FindAnyObjectByType<TechWiseAttemptRecorder>();
            if (recorder != null && recorder.CanSubmitMonitor)
            {
                recorder.SubmitFromMonitor();
                TransitionToSubmitted(recorder);
            }
            else if (recorder != null && recorder.IsReadyToStart)
            {
                recorder.StartCompetitionAttempt();
            }
            else if (recorder != null && recorder.IsFinished)
            {
                TransitionToSubmitted(recorder);
            }
        }
        else if (State == ScreenState.Ready || State == ScreenState.Error || State == ScreenState.Desktop)
        {
            StopAllCoroutines();
            StartCoroutine(Boot());
        }
    }

    void TransitionToSubmitted(TechWiseAttemptRecorder recorder)
    {
        var componentResults = recorder != null ? recorder.SubmittedResults : null;
        SetAssessment(componentResults, null);
        UpdateSubmittedDisplay(recorder);
    }

    void UpdateSubmittedDisplay(TechWiseAttemptRecorder recorder)
    {
        if (station == null || body == null) return;
        string summary = recorder != null && !string.IsNullOrEmpty(recorder.LastCompletionSummary)
            ? recorder.LastCompletionSummary
            : "Assembly complete.";
        string sync = recorder != null && !string.IsNullOrEmpty(recorder.CurrentSyncStatus)
            ? recorder.CurrentSyncStatus
            : "";

        string statusLine = string.IsNullOrEmpty(sync) ? "" : $"STATUS: {sync}\n";
        string checkResult = errors.Count == 0
            ? "HARDWARE CHECK: All components installed & verified."
            : $"HARDWARE CHECK: {errors.Count} issue(s) detected.";

        title.text = TechWiseSimulationModeManager.IsDisassembly ? "DISASSEMBLY SUBMITTED" : "ASSEMBLY SUBMITTED";
        body.text = $"{summary}\n{statusLine}{checkResult}\n\nAttempt locked. " + (TechWiseSimulationModeManager.IsDisassembly ? "Press RESULTS to review." : "Press TEST PC to run the hardware check.");
        buttonLabel.text = TechWiseSimulationModeManager.IsDisassembly ? "RESULTS" : "TEST PC";
        actionButton.interactable = true;
    }

    internal static void ReceiveAssessment(TechWiseVrComponentResult[] components, string configurationError)
    {
        if (!Applies || instance == null) return;
        if (instance.station == null) instance.TryCreate();
        instance.SetAssessment(components, configurationError);
        var recorder = FindAnyObjectByType<TechWiseAttemptRecorder>();
        instance.UpdateSubmittedDisplay(recorder);
    }

    internal void SetAssessment(TechWiseVrComponentResult[] components, string configurationError)
    {
        errors.Clear();
        if (!string.IsNullOrEmpty(configurationError) && !errors.Contains("Workbench configuration: " + configurationError))
            errors.Add("Workbench configuration: " + configurationError);

        foreach (var step in TechWiseSimulationModeManager.AssemblyOrder)
        {
            if (components == null || !components.Any(c => c != null && c.step == step))
            {
                var msg = TechWiseSimulationRuntime.Label(step) + ": not detected.";
                if (!errors.Contains(msg)) errors.Add(msg);
            }
        }

        if (components != null)
        {
            foreach (var component in components)
            {
                if (component != null && !component.complete)
                {
                    var msg = component.component + ": " + component.explanation + " " + component.correction;
                    if (!errors.Contains(msg)) errors.Add(msg);
                }
            }
        }

        State = ScreenState.Ready;
        if (station == null) return;
        desktop.SetActive(false);
        body.gameObject.SetActive(true);
        title.text = TechWiseSimulationModeManager.IsDisassembly ? "DISASSEMBLY SUBMITTED" : "ASSEMBLY SUBMITTED";
        buttonLabel.text = TechWiseSimulationModeManager.IsDisassembly ? "RESULTS" : "TEST PC";
        actionButton.interactable = true;
    }

    IEnumerator Boot()
    {
        if (TechWiseSimulationModeManager.IsDisassembly)
        {
            State=errors.Count==0?ScreenState.Desktop:ScreenState.Error;
            title.text=errors.Count==0?"DISASSEMBLY VERIFIED":"DISASSEMBLY INCOMPLETE";
            body.text=errors.Count==0?"Every required part and screw was removed correctly.":string.Join("\n\n",errors);
            buttonLabel.text="RESULTS"; yield break;
        }
        State = ScreenState.Checking;
        actionButton.interactable = false;
        desktop.SetActive(false);
        body.gameObject.SetActive(true);
        title.text = "TECHWISE PC";
        body.text = "Powering on...";
        yield return new WaitForSecondsRealtime(.8f);

        body.text = "POST — checking installed hardware...";
        yield return new WaitForSecondsRealtime(1.2f);

        if (errors.Count > 0)
        {
            State = ScreenState.Error;
            title.text = "STARTUP CHECK FAILED";
            body.text=string.Join("\n\n",errors)+"\n\nScroll to read all issues. Start a new attempt to correct them.";
            buttonLabel.text="RETEST"; actionButton.interactable=true;
            yield break;
        }

        body.text = "Hardware check passed.\n\nLoading operating system...";
        yield return new WaitForSecondsRealtime(1.4f);

        State = ScreenState.Desktop;
        title.text = "TECHWISE DESKTOP";
        body.gameObject.SetActive(false);
        desktop.SetActive(true);
        buttonLabel.text = "RESTART PC";
        actionButton.interactable = true;
    }
}
