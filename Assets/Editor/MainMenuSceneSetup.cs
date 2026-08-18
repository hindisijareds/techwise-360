using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuSceneSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string XrOriginVariantPath = "Assets/VRMPAssets/Prefabs/PrefabVariants/XR Origin Hands (XR Rig) MP Template Variant.prefab";

    static readonly Color32 Navy = new(12, 42, 82, 255);
    static readonly Color32 PrimaryBlue = new(18, 101, 210, 255);
    static readonly Color32 HoverBlue = new(35, 132, 239, 255);
    static readonly Color32 PressedBlue = new(10, 68, 158, 255);
    static readonly Color32 PaleBlue = new(235, 246, 255, 255);
    static readonly Color32 PanelBlue = new(244, 250, 255, 245);
    static readonly Color32 AccentCyan = new(74, 198, 255, 255);
    static readonly Color32 MutedText = new(67, 89, 119, 255);

    [MenuItem("TechWise 360/Rebuild Start Menu")]
    public static void RebuildStartMenu()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveOldMenuObjects();

        var canvas = CreateCanvas();
        CreateBackground(canvas.transform);

        var menuRoot = new GameObject("TechWise 360 Start Menu", typeof(MainMenu));
        var menu = menuRoot.GetComponent<MainMenu>();

        var mainPanel = CreateMainPanel(canvas.transform, menu);
        var controlsPanel = CreateControlsPanel(canvas.transform, menu);
        controlsPanel.SetActive(false);

        var serializedMenu = new SerializedObject(menu);
        serializedMenu.FindProperty("singleplayerSceneName").stringValue = "Singleplayer";
        serializedMenu.FindProperty("practiceSceneName").stringValue = "Multiplayer";
        serializedMenu.FindProperty("mainPanel").objectReferenceValue = mainPanel;
        serializedMenu.FindProperty("controlsPanel").objectReferenceValue = controlsPanel;
        serializedMenu.FindProperty("desktopModeButton").objectReferenceValue = GameObject.Find("Desktop Mode Button")?.GetComponent<Button>();
        serializedMenu.FindProperty("vrModeButton").objectReferenceValue = GameObject.Find("VR Mode Button")?.GetComponent<Button>();
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TechWise 360 minimal start menu rebuilt.");
    }

    [MenuItem("TechWise 360/Apply Minimal Menu And Desktop Controller")]
    public static void ApplyMinimalMenuAndDesktopController()
    {
        RebuildStartMenu();
        InstallDesktopController();
    }

    [MenuItem("TechWise 360/Install Desktop Controller")]
    public static void InstallDesktopController()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(XrOriginVariantPath);
        try
        {
            if (prefabRoot.GetComponent<TechWiseDesktopController>() == null)
            {
                prefabRoot.AddComponent<TechWiseDesktopController>();
                Debug.Log("Added TechWise desktop controller to the shared XR Origin variant.");
            }
            else
            {
                Debug.Log("TechWise desktop controller already exists on the shared XR Origin variant.");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, XrOriginVariantPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("TechWise 360/Build Windows Development")]
    public static void BuildWindowsDevelopment()
    {
        BuildWindowsPlayer("Builds/TechWise360DesktopControls", BuildOptions.None, "student");
    }

    [MenuItem("TechWise 360/Build Windows Release")]
    public static void BuildWindowsRelease()
    {
        BuildWindowsPlayer("Builds/TechWise360Release", BuildOptions.None, "release");
    }

    static void BuildWindowsPlayer(string buildDirectory, BuildOptions buildOptions, string buildKind)
    {
        const string executableName = "TechWise360.exe";
        var buildPath = buildDirectory + "/" + executableName;

        if (!System.IO.Directory.Exists(buildDirectory))
            System.IO.Directory.CreateDirectory(buildDirectory);

        var scenes = new[]
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Singleplayer.unity",
            "Assets/Scenes/Multiplayer.unity",
        };

        var report = BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.StandaloneWindows64, buildOptions);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception($"Windows {buildKind} build failed: {report.summary.result}");

        Debug.Log($"Windows {buildKind} build created at {buildPath}");
    }

    static void RemoveOldMenuObjects()
    {
        var names = new[]
        {
            "MainMenu Canvas",
            "Connection Canvas",
            "TechWise 360 Menu Canvas",
            "TechWise 360 Start Menu",
            "EventSystem",
            "Multiplayer Button",
            "Network Manager VR Mutliplayer",
            "XR Device Simulator",
            "XR Device Simulator UI",
            "XR Interaction Simulator",
        };

        var targets = new List<GameObject>();
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            foreach (var name in names)
            {
                if (obj != null && obj.name == name)
                    targets.Add(obj);
            }
        }

        foreach (var target in targets)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }

    static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("TechWise 360 Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    static void CreateBackground(Transform parent)
    {
        var background = CreateRect("White Blue Background", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.one);
        background.gameObject.AddComponent<Image>().color = Color.white;

        var topBand = CreateRect("Top Blue Band", parent, new Vector2(0f, 0.82f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        topBand.gameObject.AddComponent<Image>().color = new Color32(224, 242, 255, 255);

        var leftWash = CreateRect("Left Pale Blue Wash", parent, new Vector2(0f, 0f), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);
        leftWash.gameObject.AddComponent<Image>().color = PaleBlue;

        var accentLine = CreateRect("Blue Accent Line", parent, new Vector2(0.045f, 0.095f), new Vector2(0.955f, 0.103f), Vector2.zero, Vector2.zero);
        accentLine.gameObject.AddComponent<Image>().color = new Color32(207, 231, 255, 255);

        var cyanLine = CreateRect("Cyan Accent Line", parent, new Vector2(0.045f, 0.103f), new Vector2(0.35f, 0.11f), Vector2.zero, Vector2.zero);
        cyanLine.gameObject.AddComponent<Image>().color = AccentCyan;
    }

    static GameObject CreateMainPanel(Transform parent, MainMenu menu)
    {
        var panel = CreateRect("Main Panel", parent, new Vector2(0.07f, 0.13f), new Vector2(0.93f, 0.88f), Vector2.zero, Vector2.zero).gameObject;

        var content = CreateRect("Content Area", panel.transform, new Vector2(0f, 0f), new Vector2(0.57f, 1f), Vector2.zero, Vector2.zero).gameObject;
        var title = CreateText("Title", content.transform, "TECHWISE 360", 86, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 0.46f), new Vector2(0.95f, 0.62f), Vector2.zero, Vector2.zero);
        title.color = Navy;

        var actionPanel = CreateActionPanel(panel.transform, menu);
        SetRect(actionPanel.GetComponent<RectTransform>(), new Vector2(0.65f, 0.04f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);

        return panel;
    }

    static GameObject CreateActionPanel(Transform parent, MainMenu menu)
    {
        var panel = CreateRect("Action Panel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        panel.AddComponent<Image>().color = PanelBlue;

        var modeRow = CreateRect("Mode Toggle", panel.transform, new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.88f), Vector2.zero, Vector2.zero).gameObject;
        var modeLayout = modeRow.AddComponent<HorizontalLayoutGroup>();
        modeLayout.spacing = 10f;
        modeLayout.childControlHeight = true;
        modeLayout.childControlWidth = true;
        modeLayout.childForceExpandHeight = true;
        modeLayout.childForceExpandWidth = true;

        CreateButton(modeRow.transform, "Desktop Mode Button", "Desktop", menu.UseDesktopMode);
        CreateButton(modeRow.transform, "VR Mode Button", "VR", menu.UseVrMode);

        var buttons = CreateRect("Button Stack", panel.transform, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.69f), Vector2.zero, Vector2.zero).gameObject;
        var layout = buttons.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateButton(buttons.transform, "Start Button", "Start", menu.SinglePlayer);
        CreateButton(buttons.transform, "Practice Mode Button", "Practice Mode", menu.PracticeMode);
        CreateButton(buttons.transform, "Controls Button", "Controls", menu.ShowControls);
        CreateButton(buttons.transform, "Quit Button", "Quit", menu.QuitGame);

        return panel;
    }

    static GameObject CreateControlsPanel(Transform parent, MainMenu menu)
    {
        var panel = CreateRect("Controls Panel", parent, new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.82f), Vector2.zero, Vector2.zero).gameObject;
        panel.AddComponent<Image>().color = new Color32(248, 252, 255, 250);

        var accent = CreateRect("Controls Accent", panel.transform, new Vector2(0f, 0.92f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        accent.gameObject.AddComponent<Image>().color = PrimaryBlue;

        var title = CreateText("Controls Title", panel.transform, "Controls", 44, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0.08f, 0.76f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);
        title.color = Navy;

        var body = CreateText(
            "Controls Text",
            panel.transform,
            "Desktop: WASD move, mouse look, center crosshair\nHold Left Click: grab / drop\nRight Mouse Drag: free rotate held part\nMouse Wheel: rotate held part, R cycles axis\nShift + Wheel: move held part closer / farther\nEsc: unlock cursor\n\nVR: headset/controllers if connected\nNo headset: XR Device Simulator uses keyboard/mouse to drive virtual remotes",
            25,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.9f, 0.74f), Vector2.zero, Vector2.zero);
        body.color = MutedText;

        var backButton = CreateButton(panel.transform, "Back Button", "Back", menu.ShowMainMenu);
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.34f, 0.2f), Vector2.zero, Vector2.zero);

        return panel;
    }

    static Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 68f;
        layoutElement.minHeight = 64f;

        var image = buttonObject.AddComponent<Image>();
        image.color = PrimaryBlue;

        var button = buttonObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = PrimaryBlue;
        colors.highlightedColor = HoverBlue;
        colors.pressedColor = PressedBlue;
        colors.selectedColor = HoverBlue;
        colors.disabledColor = new Color32(180, 196, 218, 255);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, action);

        var text = CreateText("Label", buttonObject.transform, label, 27, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.color = Color.white;

        return button;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string value, float maxSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var textObject = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = 14f;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        return rect;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = null;
    }
}
