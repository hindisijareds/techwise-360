using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Disposable world-space presentation; owns no simulation state.</summary>
public sealed class TechWiseTutorialView : MonoBehaviour
{
    static readonly Color Accent = new(0.25f, 0.85f, 1f);
    static readonly Color Amber = new(1f, 0.74f, 0.24f);
    static readonly Color Muted = new(0.5f, 0.6f, 0.68f, 0.45f);
    static readonly int[] OutlinePath = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
    Camera cameraRig;
    TechWiseTutorialRuntime lesson;
    RectTransform panel;
    TMP_Text heading, body, feedback, progress;
    Button pointButton, skipButton, resetButton;
    LineRenderer partOutline, targetOutline, targetArrow;
    Transform trainingTarget, trainingObject;
    Material lineMaterial, blockMaterial, targetMaterial;
    readonly List<Callout> callouts = new();
    readonly Vector3[] corners = new Vector3[8];
    Transform outlinedPart;
    Renderer[] partRenderers;
    float bindAt;
    internal Material BlockMaterial => blockMaterial;
    TechWiseTutorialRuntime.Stage stage;
    bool panelPlaced;
    bool userRepositioned;

    sealed class Callout
    {
        public Transform hand, anchor;
        public RectTransform panel;
        public TMP_Text label;
        public LineRenderer line;
        public bool left;
        public int control;
    }

    internal void Create(Camera camera, TechWiseTutorialRuntime owner)
    {
        cameraRig = camera; lesson = owner;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        lineMaterial = new Material(shader) { color = Color.white };
        // Lines use a vertex-color shader so active/ready states are visible on Quest.
        var lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null) { Destroy(lineMaterial); lineMaterial = new Material(lineShader); }
        blockMaterial = new Material(shader) { color = Accent };
        if (blockMaterial.HasProperty("_BaseColor")) blockMaterial.SetColor("_BaseColor", Accent);
        panel = CanvasPanel("Tutorial lesson panel", transform, new Vector2(720, 640), 0.0015f, true);
        var draggable = panel.gameObject.AddComponent<TechWiseDraggableUiPanel>();
        draggable.SetBounds(new Vector2(640f, 100f), new Vector2(0f, 230f));
        draggable.Dragged += () =>
        {
            userRepositioned = true;
            panelPlaced = true;
        };
        heading = Text("Lesson title", panel, 32, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f));
        heading.fontStyle = FontStyles.Bold; heading.color = Accent;
        body = Text("Lesson instructions", panel, 26, new Vector2(0.06f, 0.31f), new Vector2(0.94f, 0.77f));
        TechWiseScrollableText.Wrap(body);
        feedback = Text("Action feedback", panel, 23, new Vector2(0.06f, 0.19f), new Vector2(0.94f, 0.30f));
        feedback.color = Amber;
        pointButton = Button("Point here and press trigger", panel, new Vector2(0.06f, 0.05f), new Vector2(0.61f, 0.18f));
        pointButton.gameObject.AddComponent<TechWiseTutorialTriggerTarget>().lesson = lesson;
        skipButton = Button("Skip Tutorial", panel, new Vector2(0.65f, 0.05f), new Vector2(0.94f, 0.18f));
        skipButton.onClick.AddListener(lesson.SkipControls);
        resetButton = Button("Reset Block", panel, new Vector2(0.06f, 0.05f), new Vector2(0.45f, 0.18f));
        resetButton.onClick.AddListener(lesson.ResetPracticeObject);
        progress = Text("Completion note", panel, 20, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.18f));
        progress.text = "Grip the title to move this panel. Aim at text + " + TechWiseControlLabels.Scroll + " to scroll. No timer or grade.";
        partOutline = Line("Current component outline", 0.003f);
        targetOutline = Line("Matching target outline", 0.003f);
        targetArrow = Line("Training target direction", 0.004f);
        targetMaterial = new Material(Resources.Load<Shader>("TechWisePlacementMarker"));
        targetOutline.sharedMaterial = targetArrow.sharedMaterial = targetMaterial;
        PositionPanel(true);
        BindControllers();
    }
    RectTransform CanvasPanel(string name, Transform parent, Vector2 size, float scale, bool interactive)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        obj.transform.SetParent(parent, false);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = cameraRig; canvas.sortingOrder = 120;
        if (interactive)
        {
            obj.AddComponent<GraphicRaycaster>();
            var raycaster = obj.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.ignoreReversedGraphics = false;
            raycaster.checkFor3DOcclusion = false;
            raycaster.checkFor2DOcclusion = false;
        }
        var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.localScale = Vector3.one * scale;
        var image = obj.GetComponent<Image>(); image.color = new Color(0.018f, 0.04f, 0.065f, 0.97f); image.raycastTarget = interactive;
        return rect;
    }
    static TMP_Text Text(string name, Transform parent, float size, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>();
        var rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size - 3; text.fontSizeMax = size;
        text.color = Color.white; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
    static Button Button(string title, Transform parent, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = obj.GetComponent<Image>(); image.color = new Color(0.065f, 0.29f, 0.38f); image.raycastTarget = true;
        var button = obj.GetComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Accent; colors.pressedColor = new Color(0.3f, 1f, 0.6f); button.colors = colors;
        var text = Text("Label", rect, 23, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        text.text = title; text.alignment = TextAlignmentOptions.Center;
        return button;
    }
    LineRenderer Line(string name, float width)
    {
        var obj = new GameObject(name); obj.transform.SetParent(transform, false);
        var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true; line.widthMultiplier = width; line.positionCount = 0;
        line.startColor = line.endColor = Accent; line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        return line;
    }
    internal Transform CreatePracticeTarget(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Training placement frame"); root.transform.SetParent(transform, false);
        root.transform.SetPositionAndRotation(position, rotation); trainingTarget = root.transform;
        return trainingTarget;
    }
    internal void SetPracticeObject(Transform target) => trainingObject = target;
    internal void EndControls()
    {
        trainingObject = null;
        if (trainingTarget != null) Destroy(trainingTarget.gameObject);
        trainingTarget = null;
        foreach (var callout in callouts)
        { if (callout.panel != null) callout.panel.gameObject.SetActive(false); if (callout.line != null) callout.line.positionCount = 0; }
        ClearMarkers();
        if (!panelPlaced && !userRepositioned) PositionPanel(true);
    }
    internal void ClearMarkers() { partOutline.positionCount = 0; targetOutline.positionCount = 0; targetArrow.positionCount = 0; }
    internal void Refresh(string title, string instructions, string hint, TechWiseTutorialRuntime.Stage current)
    {
        bool enteringAssembly=current==TechWiseTutorialRuntime.Stage.Assembly && stage!=current;
        string oldObjective=heading.text+string.Join("\n",System.Linq.Enumerable.Take((body.text??string.Empty).Split('\n'),2));
        string newObjective=title+string.Join("\n",System.Linq.Enumerable.Take(instructions.Split('\n'),2));
        stage = current; heading.text = title; body.text = instructions; feedback.text = hint;
        if(enteringAssembly && !userRepositioned) PositionPanel(true);
        if(oldObjective!=newObjective) { Canvas.ForceUpdateCanvases(); body.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1; }
        pointButton.gameObject.SetActive(current == TechWiseTutorialRuntime.Stage.Point);
        skipButton.gameObject.SetActive(current > TechWiseTutorialRuntime.Stage.Welcome && current < TechWiseTutorialRuntime.Stage.Assembly);
        resetButton.gameObject.SetActive(current >= TechWiseTutorialRuntime.Stage.Grab && current <= TechWiseTutorialRuntime.Stage.Place);
        progress.gameObject.SetActive(current >= TechWiseTutorialRuntime.Stage.Assembly || current == TechWiseTutorialRuntime.Stage.ControlsComplete);
        if (current == TechWiseTutorialRuntime.Stage.ControlsComplete) progress.gameObject.SetActive(false);
    }
    void LateUpdate()
    {
        if (cameraRig == null || panel == null) return;
        if (stage >= TechWiseTutorialRuntime.Stage.Assembly) return;
        if (Time.unscaledTime >= bindAt) { bindAt = Time.unscaledTime + 1; BindControllers(); }
        foreach (var callout in callouts) UpdateCallout(callout);
        if (trainingObject != null)
        {
            DrawPartOutline(trainingObject);
            bool ready = trainingTarget != null && Vector3.Distance(trainingTarget.position, trainingObject.position) <= 0.13f &&
                Quaternion.Angle(trainingObject.rotation, trainingTarget.rotation) <= 30f;
            targetOutline.startColor = targetOutline.endColor = ready ? Color.green : Amber;
            if (trainingTarget != null) DrawBox(targetOutline, trainingTarget.position, new Vector3(0.13f, 0.11f, 0.19f), trainingTarget.rotation);
            if (trainingTarget != null)
            {
                targetArrow.startColor = targetArrow.endColor = ready ? Color.green : Amber;
                targetArrow.positionCount = 3;
                targetArrow.SetPosition(0, trainingTarget.TransformPoint(new Vector3(-0.035f, 0.058f, 0.03f)));
                targetArrow.SetPosition(1, trainingTarget.TransformPoint(new Vector3(0, 0.058f, 0.085f)));
                targetArrow.SetPosition(2, trainingTarget.TransformPoint(new Vector3(0.035f, 0.058f, 0.03f)));
            }
        }
    }
    void PositionPanel(bool force)
    {
        if (userRepositioned) return;
        if (cameraRig == null || panel == null) return;
        if (!force && panelPlaced) return;
        var camera = cameraRig.transform;
        var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) return;
        var toPanel = panel.position - camera.position;
        if (!force && toPanel.magnitude < 2.2f && Vector3.Angle(forward, Vector3.ProjectOnPlane(toPanel, Vector3.up)) < 65f) return;
        var right = Vector3.Cross(Vector3.up, forward);
        var desired = camera.position + forward * 1.45f + Vector3.up * 0.14f;
        // During assembly keep the workbench directly ahead clear.
        if (stage >= TechWiseTutorialRuntime.Stage.Assembly) desired += right * 0.8f;
        panel.SetPositionAndRotation(desired, Quaternion.LookRotation(desired - camera.position, Vector3.up));
        panelPlaced = true;
    }
    void BindControllers()
    {
        foreach (bool left in new[] { true, false })
        {
            if (callouts.Exists(c => c.left == left && c.hand != null)) continue;
            Transform hand = null;
            foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
            {
                if (TechWiseTutorialRuntime.IsLeft(interactor.transform) != left) continue;
                for (var t = interactor.transform; t != null; t = t.parent)
                    if (t.name == (left ? "Left Controller" : "Right Controller")) { hand = t; break; }
                if (hand != null) break;
            }
            // Fallback to a tracked hand/controller transform if its model is missing.
            if (hand == null)
                foreach (var pose in FindObjectsByType<UnityEngine.InputSystem.XR.TrackedPoseDriver>(FindObjectsInactive.Include))
                    if (pose.GetComponent<Camera>() == null && TechWiseTutorialRuntime.IsLeft(pose.transform) == left) { hand = pose.transform; break; }
            if (hand == null) continue;
            for (int control = 0; control < 3; control++) AddCallout(hand, left, control);
        }
    }
    void AddCallout(Transform hand, bool left, int control)
    {
        string name = control == 0 ? "Trigger" : control == 1 ? "Grip" : "Joystick";
        Transform anchor = null;
        foreach (var t in hand.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0 && !t.name.Contains("Callout")) { anchor = t; break; }
        }
        var callout = new Callout { hand = hand, anchor = anchor, left = left, control = control };
        callout.panel = CanvasPanel((left ? "Left " : "Right ") + name + " callout", transform, new Vector2(320, 62), 0.0007f, false);
        callout.label = Text("Control label", callout.panel, 26, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
        callout.line = Line(name + " leader and highlight", 0.0018f);
        callouts.Add(callout);
    }
    void UpdateCallout(Callout c)
    {
        if (c.hand == null) return;
        bool available = c.hand.gameObject.activeInHierarchy;
        c.panel.gameObject.SetActive(available);
        if (!available) { c.line.positionCount = 0; return; }
        bool active = c.control == 0 && stage == TechWiseTutorialRuntime.Stage.Point ||
            c.control == 1 && stage >= TechWiseTutorialRuntime.Stage.Grab && stage <= TechWiseTutorialRuntime.Stage.Place ||
            c.control == 2 && (stage == TechWiseTutorialRuntime.Stage.Move && c.left || stage == TechWiseTutorialRuntime.Stage.Turn && !c.left || stage == TechWiseTutorialRuntime.Stage.Manipulate);
        string side = c.left ? "Left" : "Right";
        string key = c.control == 0 ? $"XRI {side} Interaction/UI Press" : c.control == 1 ? $"XRI {side} Interaction/Select" :
            stage == TechWiseTutorialRuntime.Stage.Manipulate ? $"XRI {side} Interaction/Manipulation" : $"XRI {side} Locomotion/" + (c.left ? "Move" : "Turn");
        string action = c.control == 0 ? "Point + click" : c.control == 1 ? "Hold to grab" : stage == TechWiseTutorialRuntime.Stage.Manipulate ? "Rotate / distance" : c.left ? "Move" : "Turn";
        c.label.text = lesson.Binding(key, side + " control") + "\n" + action;
        c.label.color = active ? Accent : Muted;
        // Model prefabs have a combined mesh; use small button rings at the tracked pose when no separate button bone exists.
        var local = c.control == 0 ? new Vector3(0, 0.02f, 0.055f) : c.control == 1 ? new Vector3(c.left ? 0.022f : -0.022f, -0.025f, 0.012f) : new Vector3(0, 0.045f, 0.01f);
        var anchor = c.anchor != null ? c.anchor.position : c.hand.TransformPoint(local);
        var offset = cameraRig.transform.right * (c.left ? -0.18f : 0.18f) + Vector3.up * (0.17f - 0.085f * c.control);
        c.panel.position = anchor + offset;
        c.panel.rotation = Quaternion.LookRotation(c.panel.position - cameraRig.transform.position, Vector3.up);
        c.line.startColor = c.line.endColor = active ? Accent : Muted;
        const int points = 18;
        c.line.positionCount = points + 2;
        c.line.SetPosition(0, c.panel.position);
        c.line.SetPosition(1, anchor + c.hand.right * 0.012f);
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2 / (points - 1);
            c.line.SetPosition(i + 2, anchor + (c.hand.right * Mathf.Cos(a) + c.hand.up * Mathf.Sin(a)) * (active ? 0.012f : 0.007f));
        }
    }
    internal void MarkAssembly(Transform part, Transform target, bool ready)
    {
        DrawPartOutline(part);
        targetOutline.positionCount = 0;
        targetArrow.startColor = targetArrow.endColor = ready ? Color.green : Amber;
        TechWiseComponentGeometry.TargetArrow(targetArrow, target, .12f);
    }
    void DrawPartOutline(Transform part)
    {
        TechWiseComponentGeometry.Outline(partOutline, part);
    }
    void DrawBox(LineRenderer line, Vector3 center, Vector3 size, Quaternion rotation)
    {
        if (line == null) return;
        var extents = size * 0.5f;
        corners[0] = new Vector3(-extents.x, -extents.y, -extents.z); corners[1] = new Vector3(extents.x, -extents.y, -extents.z);
        corners[2] = new Vector3(extents.x, -extents.y, extents.z); corners[3] = new Vector3(-extents.x, -extents.y, extents.z);
        corners[4] = new Vector3(-extents.x, extents.y, -extents.z); corners[5] = new Vector3(extents.x, extents.y, -extents.z);
        corners[6] = new Vector3(extents.x, extents.y, extents.z); corners[7] = new Vector3(-extents.x, extents.y, extents.z);
        line.positionCount = OutlinePath.Length;
        for (int i = 0; i < OutlinePath.Length; i++) line.SetPosition(i, center + rotation * corners[OutlinePath[i]]);
    }
    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        if (blockMaterial != null) Destroy(blockMaterial);
        if (targetMaterial != null) Destroy(targetMaterial);
    }
}
