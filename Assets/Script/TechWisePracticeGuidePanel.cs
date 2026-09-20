using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Read-only practice guidance; never instantiated during an assessment.</summary>
[DefaultExecutionOrder(-7000)]
public sealed class TechWisePracticeGuidePanel : MonoBehaviour
{
    GameObject panel;
    TMP_Text text;
    LineRenderer partMarker, targetMarker;
    Material markerMaterial, targetMaterial;
    float refreshAt;
    bool desktop;
    Transform outlinedPart;
    Renderer[] outlinedRenderers;
    readonly Vector3[] outlineCorners = new Vector3[8];
    static readonly int[] OutlinePath = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var root = new GameObject("TechWise Practice Side Guide Runtime");
        DontDestroyOnLoad(root);
        root.AddComponent<TechWisePracticeGuidePanel>();
    }
    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; Clear(); }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Clear();
    void Clear()
    {
        if (panel != null) Destroy(panel);
        if (partMarker != null) Destroy(partMarker.gameObject);
        if (targetMarker != null) Destroy(targetMarker.gameObject);
        if (markerMaterial != null) Destroy(markerMaterial);
        if (targetMaterial != null) Destroy(targetMaterial);
        panel = null; text = null;
        outlinedPart = null; outlinedRenderers = null;
    }
    void Update()
    {
        var state = TechWiseSimulationRuntime.Instance;
        if (TechWiseSimulationModeManager.IsCompetitionMode || TechWiseTutorialRuntime.InTutorial || state == null || state.Parts.Count == 0)
        { if (panel != null) Clear(); return; }
        var useDesktop = MainMenu.IsDesktopModeSelected(false);
        if (panel != null && desktop != useDesktop) Clear();
        if (panel == null) { desktop = useDesktop; CreatePanel(); }
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        var builder = new StringBuilder(TechWiseSimulationModeManager.IsTutorialMode ? "<b>Tutorial Mode</b>\nPC " : "<b>Practice Mode</b>\nPC ");
        builder.AppendLine(TechWiseSimulationModeManager.IsDisassembly ? "DISASSEMBLY" : "ASSEMBLY");
        builder.AppendLine();
        if (state.ConfigurationError != null) builder.AppendLine(state.ConfigurationError);
        else if (TechWiseDisassemblyRuntime.IsPreparing) builder.AppendLine("Preparing the assembled PC...");
        else if (TechWiseDetailedAssemblyRuntime.Active)
        {
            var phase = TechWiseDetailedAssemblyRuntime.Instance;
            builder.AppendLine("<b>" + phase.Heading + "</b>").AppendLine().AppendLine(phase.Instruction);
            builder.AppendLine().AppendLine(state.Feedback);
            if (phase.CurrentPhase == TechWiseDetailedAssemblyRuntime.Phase.Complete)
                builder.AppendLine("\nAll assembly steps finished. Open Menu / Pause and choose Reset Practice to build again.");
        }
        else
        {
            int number = 0;
            foreach (var step in TechWiseSimulationModeManager.GetExpectedOrder())
            {
                number++;
                var done = state.IsStepComplete(step);
                builder.Append(done ? "<color=#99E3BB>[done] " : step == state.CurrentStep ? "<color=#FFE19A>> " : "<color=#CDD6E0>");
                builder.Append(number).Append(". ").Append(TechWiseSimulationRuntime.Label(step)).AppendLine("</color>");
            }
            builder.AppendLine();
            builder.AppendLine(state.Feedback);
            if (state.PracticeComplete)
            {
                builder.AppendLine("\n<b>Practice complete — mistake review</b>");
                builder.Append("Mistakes: ").AppendLine(state.PracticeMistakes.Count.ToString());
                if (state.PracticeMistakes.Count == 0) builder.AppendLine("No mistakes recorded.");
                int index = 0;
                foreach (var mistake in state.PracticeMistakes)
                    builder.Append('\n').Append(++index).Append(". ").AppendLine(mistake.explanation).AppendLine(mistake.correction);
                builder.AppendLine("\nPractice is for learning; no formal grade is submitted.");
                if (desktop) builder.AppendLine("Press Esc, then scroll this panel to read the full review.");
            }
        }
        text.text = builder.ToString();
        var current = state.ConfigurationError == null && !TechWiseDisassemblyRuntime.IsPreparing ? state.CurrentStep : null;
        var part = current != null ? state.Parts.Find(p => p != null && TechWiseSimulationModeManager.ResolveStepId(p.transform) == current &&
            (TechWiseSimulationModeManager.IsDisassembly ? state.IsPartInstalled(p) : !state.IsPartInstalled(p))) : null;
        var socket = current != null ? state.Sockets.Find(s => s != null && TechWiseSimulationModeManager.ResolveStepId(s.transform) == current &&
            !s.hasSelection && part != null && TechWiseSimulationRuntime.Matches(s, part.transform)) : null;
        DrawPartOutline(part != null ? part.transform : null);
        var ready = part != null && socket != null && TechWiseSimulationRuntime.IsHeld(part) &&
            state.PlacementProblem(socket, part, out _, out _) == null;
        targetMarker.startColor = targetMarker.endColor = ready ? Color.green : new Color(1f, 0.65f, 0.15f);
        DrawMarker(targetMarker, socket != null && !TechWiseSimulationModeManager.IsDisassembly ? socket.GetAttachTransform(part) : null, 0.045f);
    }
    void CreatePanel()
    {
        panel = new GameObject("TechWise Practice Side Guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = panel.GetComponent<Canvas>();
        canvas.sortingOrder = 100;
        if (desktop)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        else
        {
            canvas.renderMode = RenderMode.WorldSpace;
            panel.AddComponent<TrackedDeviceGraphicRaycaster>();
            var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                var camPos = camera.transform.position;
                var camFwd = camera.transform.forward;
                camFwd.y = 0f;
                if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
                camFwd.Normalize();
                var camRight = Vector3.Cross(Vector3.up, camFwd);

                panel.transform.position = camPos + camFwd * 1.35f + camRight * 0.75f + Vector3.up * 0.05f;
                panel.transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
                canvas.worldCamera = camera;
            }
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 720);
            panel.transform.localScale = Vector3.one * 0.00125f;

            var draggable = panel.AddComponent<TechWiseDraggableUiPanel>();
            draggable.SetBounds(new Vector2(420, 50), new Vector2(0,335));
        }
        var viewport = new GameObject("Guide scroll area", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(panel.transform, false);
        var rect = viewport.GetComponent<RectTransform>();
        // Keep the left side available for the existing desktop controls guide.
        rect.anchorMin = desktop ? new Vector2(1, 0.13f) : Vector2.zero;
        rect.anchorMax = desktop ? new Vector2(1, 0.88f) : new Vector2(1,.92f);
        rect.offsetMin = desktop ? new Vector2(-410, 0) : Vector2.zero;
        rect.offsetMax = desktop ? new Vector2(-16, 0) : Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.07f, 0.94f);
        var content = new GameObject("Practice steps and review", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = new Vector2(18, 0); contentRect.offsetMax = new Vector2(-18, -18);
        text = content.GetComponent<TextMeshProUGUI>(); text.fontSize = 23; text.color = Color.white;
        text.richText = true; text.raycastTarget = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = rect; scroll.content = contentRect;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
        markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        markerMaterial.color = new Color(0.3f, 1, 0.65f);
        partMarker = CreateMarker("Practice component indicator"); targetMarker = CreateMarker("Practice target indicator");
        targetMaterial = new Material(Resources.Load<Shader>("TechWisePlacementMarker")) { color = new Color(.3f, 1, .65f) };
        targetMarker.sharedMaterial = targetMaterial;
    }
    LineRenderer CreateMarker(string name)
    {
        var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.sharedMaterial = markerMaterial; line.positionCount = 5; line.widthMultiplier = 0.003f;
        line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
    static void DrawMarker(LineRenderer line, Transform target, float radius)
    {
        TechWiseComponentGeometry.TargetArrow(line, target, .12f);
    }

    void DrawPartOutline(Transform part)
    {
        TechWiseComponentGeometry.Outline(partMarker, part);
    }
}
