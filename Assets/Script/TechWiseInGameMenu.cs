using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-7000)]
public sealed class TechWiseInGameMenu : MonoBehaviour
{
    const string MainMenuSceneName = "MainMenu";
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const string ControlModeExplicitKey = "TechWise360.ControlModeExplicit";
    const string ControlsGuideVisibleKey = "TechWise360.ControlsGuideVisible";

    sealed class ControlsGuideView
    {
        public GameObject root;
        public GameObject panel;
        public GameObject tab;
        public TMP_Text title;
        public TMP_Text body;
        public RectTransform scrollContent;
    }

    Canvas canvas;
    Image dimmer;
    RectTransform menuPanel;
    RectTransform detailsPanel;
    TMP_Text activityText;
    TMP_Text statusText;
    TMP_Text timeText;
    TMP_Text mistakesText;
    TMP_Text progressText;
    TMP_Text detailsTitleText;
    TMP_Text detailsBodyText;
    ControlsGuideView screenControlsView;
    ControlsGuideView vrControlsView;
    Canvas vrControlsCanvas;
    string lastControlMode;
    bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseInGameMenu>() != null)
            return;

        var menuObject = new GameObject("TechWise In-Game Menu");
        DontDestroyOnLoad(menuObject);
        menuObject.AddComponent<TechWiseInGameMenu>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureUi();
        RefreshVisibility();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        if (WasMenuShortcutPressed())
            ToggleMenu();

        if (isOpen)
            RefreshStatus();

        RefreshControlsGuide();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        EnsureUi();
        RefreshVisibility();
    }

    void EnsureUi()
    {
        if (canvas != null)
            return;

        EnsureEventSystem();

        var canvasObject = new GameObject("TechWise In-Game Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateDimmer(canvasObject.transform);
        CreateMenuButton(canvasObject.transform);
        CreateMenuPanel(canvasObject.transform);
        screenControlsView = CreateControlsGuideView(canvasObject.transform, false);
        SetMenuOpen(false);
    }

    static void EnsureEventSystem()
    {
        EventSystem fallback = null;
        EventSystem sceneSystem = null;
        foreach (var system in FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
        {
            if (system.name == "TechWise In-Game EventSystem") fallback = system;
            else if (system.gameObject.activeInHierarchy && system.enabled) sceneSystem = system;
        }
        if (fallback != null) fallback.gameObject.SetActive(sceneSystem == null);
        if (sceneSystem != null || fallback != null) return;

        var eventSystemObject = new GameObject("TechWise In-Game EventSystem", typeof(EventSystem));
        DontDestroyOnLoad(eventSystemObject);
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void CreateDimmer(Transform parent)
    {
        var rect = CreateRect("Scene Dimmer", parent, Vector2.zero, Vector2.one);
        dimmer = rect.gameObject.AddComponent<Image>();
        dimmer.color = new Color32(5, 15, 32, 112);
    }

    void CreateMenuButton(Transform parent)
    {
        var button = CreateFlatButton(parent, "Menu Button", "Menu", ToggleMenu, TechWiseUITheme.PrimaryBlue, Color.white);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(22f, -22f);
        rect.sizeDelta = new Vector2(146f, 50f);
    }

    void CreateMenuPanel(Transform parent)
    {
        menuPanel = CreateRect("In-Game Menu Panel", parent, new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f));
        TechWiseUITheme.StylePanel(menuPanel.gameObject.AddComponent<Image>(), new Color32(255, 255, 255, 242));

        CreateHeader(menuPanel);
        CreateStatusCard(menuPanel);
        CreateActionGrid(menuPanel);
        CreateDetailsPanel(menuPanel);
    }

    ControlsGuideView CreateControlsGuideView(Transform parent, bool worldSpace)
    {
        var view = new ControlsGuideView();
        var root = CreateRect(worldSpace ? "VR Controls Guide Root" : "Controls Guide Root", parent, Vector2.zero, Vector2.one);
        view.root = root.gameObject;

        var panel = CreateRect("Controls Guide Panel", root, Vector2.zero, Vector2.zero);
        panel.anchorMin = worldSpace ? Vector2.zero : new Vector2(0f, 1f);
        panel.anchorMax = worldSpace ? Vector2.one : new Vector2(0f, 1f);
        panel.pivot = worldSpace ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        panel.anchoredPosition = worldSpace ? Vector2.zero : new Vector2(22f, -88f);
        panel.sizeDelta = worldSpace ? Vector2.zero : new Vector2(430f, 650f);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color32(5, 20, 30, 235);
        view.panel = panel.gameObject;

        var title = CreateText(panel, "Controls Guide Title", "Desktop Controls", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = Color.white;
        Stretch(title.rectTransform, new Vector2(0.055f, 0.89f), new Vector2(0.7f, 0.97f));
        view.title = title;

        var hide = CreateFlatButton(panel, "Hide Controls Button", "Hide", () => SetControlsExpanded(false), new Color32(31, 57, 72, 255), Color.white);
        Stretch(hide.GetComponent<RectTransform>(), new Vector2(0.73f, 0.9f), new Vector2(0.95f, 0.965f));

        var scrollObject = CreateRect("Controls Scroll View", panel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.87f));
        var scrollRect = scrollObject.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 34f;

        var viewport = CreateRect("Viewport", scrollObject, Vector2.zero, Vector2.one);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.015f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewport;

        var content = CreateRect("Controls Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 760f);
        scrollRect.content = content;
        view.scrollContent = content;

        var body = CreateText(content, "Controls Guide Body", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.color = Color.white;
        body.fontSizeMin = 13f;
        body.overflowMode = TextOverflowModes.Overflow;
        Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));
        view.body = body;

        var tab = CreateFlatButton(root, "Show Controls Tab", "Controls", () => SetControlsExpanded(true), TechWiseUITheme.PrimaryBlue, Color.white);
        var tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f);
        tabRect.anchorMax = new Vector2(0f, 1f);
        tabRect.pivot = new Vector2(0f, 1f);
        tabRect.anchoredPosition = worldSpace ? new Vector2(18f, -18f) : new Vector2(22f, -88f);
        tabRect.sizeDelta = worldSpace ? new Vector2(220f, 62f) : new Vector2(146f, 46f);
        view.tab = tab.gameObject;

        return view;
    }

    void CreateHeader(Transform parent)
    {
        var brand = CreateRect("Brand", parent, new Vector2(0.05f, 0.87f), new Vector2(0.34f, 0.965f));
        var name = CreateText(brand, "Brand Name", "TECHWISE", 24f, FontStyles.Bold, TextAlignmentOptions.Left);
        name.color = TechWiseUITheme.Navy;
        Stretch(name.rectTransform, new Vector2(0f, 0.45f), new Vector2(0.62f, 0.88f));
        var suffix = CreateText(brand, "Brand Suffix", "360", 24f, FontStyles.Bold, TextAlignmentOptions.Left);
        suffix.color = TechWiseUITheme.PrimaryBlue;
        Stretch(suffix.rectTransform, new Vector2(0.58f, 0.45f), new Vector2(0.8f, 0.88f));
        var tagline = CreateText(brand, "Tagline", "Learn. Practice. Compete.", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
        tagline.color = TechWiseUITheme.PrimaryBlue;
        Stretch(tagline.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.48f));

        var title = CreateText(parent, "Menu Title", "In-Game Menu", 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = TechWiseUITheme.Navy;
        Stretch(title.rectTransform, new Vector2(0.35f, 0.895f), new Vector2(0.65f, 0.96f));

        var close = CreateFlatButton(parent, "Close Button", "X", () => SetMenuOpen(false), Color.white, TechWiseUITheme.Navy);
        TechWiseUITheme.StyleButton(close, Color.white, TechWiseUITheme.Navy, true);
        Stretch(close.GetComponent<RectTransform>(), new Vector2(0.9f, 0.89f), new Vector2(0.95f, 0.955f));

        var esc = CreateText(parent, "Esc Hint", "Esc", 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        esc.color = TechWiseUITheme.Navy;
        Stretch(esc.rectTransform, new Vector2(0.948f, 0.902f), new Vector2(0.978f, 0.942f));
    }

    void CreateStatusCard(Transform parent)
    {
        var card = CreateRect("Session Status Card", parent, new Vector2(0.05f, 0.705f), new Vector2(0.95f, 0.85f));
        TechWiseUITheme.StylePanel(card.gameObject.AddComponent<Image>(), new Color32(247, 252, 255, 245), false);

        var title = CreateText(card, "Status Label", "SESSION STATUS", 13f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = TechWiseUITheme.PrimaryBlue;
        Stretch(title.rectTransform, new Vector2(0.03f, 0.68f), new Vector2(0.2f, 0.9f));

        CreateStatusColumn(card, "Activity", "Activity", "Open activity", 0.11f, out activityText);
        CreateStatusColumn(card, "Status", "Status", "Ready", 0.34f, out statusText);
        CreateStatusColumn(card, "Time", "Time", "00:00", 0.49f, out timeText);
        CreateStatusColumn(card, "Mistakes", "Mistakes", "0", 0.63f, out mistakesText);
        CreateStatusColumn(card, "Progress", "Progress", "0 / 0", 0.78f, out progressText);
    }

    void CreateStatusColumn(Transform parent, string name, string label, string value, float x, out TMP_Text valueText)
    {
        var column = CreateRect(name + " Column", parent, new Vector2(x, 0.2f), new Vector2(Mathf.Min(x + 0.16f, 0.97f), 0.82f));
        var labelText = CreateText(column, "Label", label, 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        labelText.color = TechWiseUITheme.MutedText;
        Stretch(labelText.rectTransform, new Vector2(0f, 0.56f), new Vector2(1f, 0.9f));
        valueText = CreateText(column, "Value", value, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
        valueText.color = TechWiseUITheme.Navy;
        Stretch(valueText.rectTransform, new Vector2(0f, 0.1f), new Vector2(1f, 0.55f));
    }

    void CreateActionGrid(Transform parent)
    {
        var left = 0.05f;
        var mid = 0.515f;
        CreateActionButton(parent, "Resume", "Return to your session", "Play", () => SetMenuOpen(false), new Vector2(left, 0.55f), new Vector2(0.5f, 0.68f), TechWiseUITheme.PrimaryBlue, Color.white);
        CreateActionButton(parent, "Sync Now", "Sync local progress", "Sync", SyncNow, new Vector2(mid, 0.55f), new Vector2(0.95f, 0.68f), Color.white, TechWiseUITheme.PrimaryBlue);

        CreateActionButton(parent, "Controls", "View keyboard, mouse, and VR controls", "Pad", ShowControlsHelp, new Vector2(left, 0.39f), new Vector2(0.35f, 0.52f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Settings", "Adjust preferences and sync details", "Gear", ShowSettingsHelp, new Vector2(0.37f, 0.39f), new Vector2(0.65f, 0.52f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Desktop Controls", "View desktop control scheme", "PC", () => SetControlMode(MainMenu.DesktopModeValue), new Vector2(0.67f, 0.39f), new Vector2(0.95f, 0.52f), Color.white, TechWiseUITheme.PrimaryBlue);

        CreateActionButton(parent, "VR Controls", "View VR control scheme", "VR", () => SetControlMode(MainMenu.VrModeValue), new Vector2(left, 0.23f), new Vector2(0.35f, 0.36f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Back to Main Menu", "Return to the main menu", "Home", BackToMainMenu, new Vector2(0.37f, 0.23f), new Vector2(0.65f, 0.36f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Log Out", "Sign out of your account", "Out", Logout, new Vector2(0.67f, 0.23f), new Vector2(0.95f, 0.36f), TechWiseUITheme.LightRed, TechWiseUITheme.Red);
    }

    Button CreateActionButton(
        Transform parent,
        string title,
        string subtitle,
        string icon,
        UnityEngine.Events.UnityAction action,
        Vector2 min,
        Vector2 max,
        Color background,
        Color foreground)
    {
        var button = CreateFlatButton(parent, title + " Button", string.Empty, action, background, foreground);
        var rect = button.GetComponent<RectTransform>();
        Stretch(rect, min, max);
        var outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 225, 244, 180);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        var iconBubble = CreateRect("Icon", button.transform, new Vector2(0.05f, 0.2f), new Vector2(0.18f, 0.8f));
        TechWiseUITheme.StylePanel(iconBubble.gameObject.AddComponent<Image>(), foreground == TechWiseUITheme.Red ? TechWiseUITheme.LightRed : TechWiseUITheme.PaleBlue, false);
        var iconColor = foreground == Color.white ? (Color)TechWiseUITheme.PrimaryBlue : foreground;
        CreateIconImage(iconBubble, "Icon Glyph", icon, iconColor, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));

        var titleText = CreateText(button.transform, "Title", title, 20f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleText.color = foreground;
        Stretch(titleText.rectTransform, new Vector2(0.23f, 0.48f), new Vector2(0.88f, 0.78f));
        var subtitleText = CreateText(button.transform, "Subtitle", subtitle, 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        subtitleText.color = foreground == Color.white ? new Color32(220, 236, 255, 255) : TechWiseUITheme.MutedText;
        Stretch(subtitleText.rectTransform, new Vector2(0.23f, 0.22f), new Vector2(0.86f, 0.48f));

        return button;
    }

    void CreateDetailsPanel(Transform parent)
    {
        detailsPanel = CreateRect("Footer Details", parent, new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.18f));
        TechWiseUITheme.StylePanel(detailsPanel.gameObject.AddComponent<Image>(), new Color32(247, 252, 255, 235), false);
        detailsTitleText = CreateText(detailsPanel, "Details Title", "Competition timers keep running while this menu is open.", 17f, FontStyles.Bold, TextAlignmentOptions.Left);
        detailsTitleText.color = TechWiseUITheme.Navy;
        Stretch(detailsTitleText.rectTransform, new Vector2(0.08f, 0.52f), new Vector2(0.86f, 0.82f));
        detailsBodyText = CreateText(detailsPanel, "Details Body", "Completed attempts are saved locally first, then synced when online.", 15f, FontStyles.Normal, TextAlignmentOptions.Left);
        detailsBodyText.color = TechWiseUITheme.MutedText;
        Stretch(detailsBodyText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.86f, 0.48f));
        CreateIconGlyph(detailsPanel, "Details Icon", TechWiseUITheme.PrimaryBlue, new Vector2(0.035f, 0.38f), new Vector2(0.055f, 0.62f));
    }

    Image CreateIconGlyph(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        var glyph = CreateRect(name, parent, min, max);
        var image = glyph.gameObject.AddComponent<Image>();
        TechWiseUITheme.StylePanel(image, color, false);
        image.raycastTarget = false;
        return image;
    }

    Image CreateIconImage(Transform parent, string name, string iconKey, Color color, Vector2 min, Vector2 max)
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

    Button CreateFlatButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action, Color background, Color foreground)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.GetComponent<Image>();
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        TechWiseUITheme.StyleButton(button, background, foreground, true);

        if (!string.IsNullOrWhiteSpace(label))
        {
            var text = CreateText(buttonObject.transform, "Label", label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = foreground;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 3f), new Vector2(-8f, -3f));
        }

        return button;
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
        text.color = TechWiseUITheme.Navy;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
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

    void RefreshControlsGuide()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        var mode = GetActiveControlMode();
        if (mode == MainMenu.VrModeValue)
            EnsureVrControlsGuide();

        if (mode != lastControlMode)
        {
            lastControlMode = mode;
            UpdateControlsGuideText(mode);
        }

        var expanded = PlayerPrefs.GetInt(ControlsGuideVisibleKey, 1) != 0;
        ApplyControlsViewState(screenControlsView, !isOpen && mode != MainMenu.VrModeValue, expanded);
        ApplyControlsViewState(vrControlsView, !isOpen && mode == MainMenu.VrModeValue, expanded);
    }

    void EnsureVrControlsGuide()
    {
        var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null)
            return;

        if (vrControlsCanvas != null)
        {
            if (vrControlsCanvas.transform.parent != camera.transform)
                vrControlsCanvas.transform.SetParent(camera.transform, false);
            return;
        }

        var canvasObject = new GameObject("TechWise VR Controls Guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(camera.transform, false);
        canvasObject.transform.localPosition = new Vector3(-0.56f, -0.02f, 1.25f);
        canvasObject.transform.localRotation = Quaternion.identity;

        vrControlsCanvas = canvasObject.GetComponent<Canvas>();
        vrControlsCanvas.renderMode = RenderMode.WorldSpace;
        vrControlsCanvas.worldCamera = camera;
        vrControlsCanvas.sortingOrder = 1600;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(780f, 680f);
        rect.localScale = Vector3.one * 0.001f;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 20f;

        var trackedRaycasterType = Type.GetType(
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (trackedRaycasterType != null && canvasObject.GetComponent(trackedRaycasterType) == null)
            canvasObject.AddComponent(trackedRaycasterType);

        vrControlsView = CreateControlsGuideView(canvasObject.transform, true);
        UpdateControlsGuideText(MainMenu.VrModeValue);
    }

    void SetControlsExpanded(bool expanded)
    {
        PlayerPrefs.SetInt(ControlsGuideVisibleKey, expanded ? 1 : 0);
        PlayerPrefs.Save();
        RefreshControlsGuide();
    }

    static void ApplyControlsViewState(ControlsGuideView view, bool modeVisible, bool expanded)
    {
        if (view == null || view.root == null)
            return;

        view.root.SetActive(modeVisible);
        if (!modeVisible)
            return;

        if (view.panel != null)
            view.panel.SetActive(expanded);
        if (view.tab != null)
            view.tab.SetActive(!expanded);
    }

    void UpdateControlsGuideText(string mode)
    {
        var isVr = mode == MainMenu.VrModeValue;
        var title = isVr ? "VR Controls" : "Desktop Controls";
        var body = isVr ? BuildVrControlsText() : BuildDesktopControlsText();
        UpdateControlsViewText(screenControlsView, title, body);
        UpdateControlsViewText(vrControlsView, title, body);
    }

    static void UpdateControlsViewText(ControlsGuideView view, string title, string body)
    {
        if (view == null)
            return;

        if (view.title != null)
            view.title.text = title;
        if (view.body != null)
        {
            view.body.text = body;
            view.body.ForceMeshUpdate();
            if (view.scrollContent != null)
                view.scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(640f, view.body.preferredHeight + 36f));
        }
    }

    static string BuildDesktopControlsText()
    {
        return
            "W A S D   Move\n" +
            "Mouse   Look around\n" +
            "Hold Left Mouse   Grab; release to drop / place\n" +
            "Left Click   Lock cursor / press UI\n" +
            "Right Mouse + Drag   Rotate held component\n" +
            "Mouse Wheel   Rotate on selected axis\n" +
            "R   Cycle rotation axis (Y / X / Z)\n" +
            "Shift + Wheel   Move held component nearer or farther\n" +
            "F   Reset held component rotation\n" +
            "Backspace   Reset held or most recent component\n" +
            "Enter / Space   Start Competition\n" +
            "Esc   Unlock cursor / open or close Menu\n" +
            "Menu / Controls   Open menus and hide this guide";
    }

    static string BuildVrControlsText()
    {
        var leftMove = ResolveVrBinding("XRI Left Locomotion/Move", "Left joystick");
        var leftTeleport = ResolveVrBinding("XRI Left Locomotion/Teleport Mode", "Left joystick");
        var rightTurn = ResolveVrBinding("XRI Right Locomotion/Turn", "Right joystick");
        var leftGrip = ResolveVrBinding("XRI Left Interaction/Select", "Left grip");
        var rightGrip = ResolveVrBinding("XRI Right Interaction/Select", "Right grip");
        var leftTrigger = ResolveVrBinding("XRI Left Interaction/UI Press", "Left trigger");
        var rightTrigger = ResolveVrBinding("XRI Right Interaction/UI Press", "Right trigger");
        var scaleToggle = ResolveVrBinding("XRI Right Interaction/Scale Toggle", "Right joystick click");
        var jump = ResolveVrBinding("XRI Right Locomotion/Jump", "Right primary button");

        return
            $"{leftMove}   Move\n" +
            $"{rightTurn}   Turn / snap turn\n" +
            $"{leftTeleport}   Aim or activate teleport\n" +
            $"{leftGrip} / {rightGrip}   Grab and release components\n" +
            $"{leftTrigger} / {rightTrigger}   Activate held items\n" +
            $"Point + {rightTrigger}   Press UI buttons\n" +
            "Either joystick while holding   Translate or rotate a component\n" +
            $"{scaleToggle}   Toggle scale manipulation\n" +
            $"{jump}   Jump when locomotion allows it\n" +
            "Menu / Controls buttons   Open menus and hide this guide";
    }

    static string ResolveVrBinding(string actionPath, string fallback)
    {
#if ENABLE_INPUT_SYSTEM
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (asset == null)
                continue;

            var action = asset.FindAction(actionPath, false);
            if (action == null)
                continue;

            for (var index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                var path = binding.effectivePath;
                if (binding.isComposite || binding.isPartOfComposite ||
                    string.IsNullOrWhiteSpace(path) ||
                    path.IndexOf("XRController", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var display = action.GetBindingDisplayString(index);
                if (!string.IsNullOrWhiteSpace(display))
                    return display.Replace("Primary 2D Axis", "Joystick").Replace("Button", "button");
            }
        }
#endif
        return fallback;
    }

    void ToggleMenu()
    {
        SetMenuOpen(!isOpen);
    }

    void SetMenuOpen(bool open)
    {
        isOpen = open;
        if (menuPanel != null)
            menuPanel.gameObject.SetActive(open);
        if (dimmer != null)
            dimmer.gameObject.SetActive(open);

        if (open)
            RefreshStatus();

        RefreshControlsGuide();
    }

    void RefreshVisibility()
    {
        var active = IsGameplayScene(SceneManager.GetActiveScene().name);
        if (canvas != null)
            canvas.gameObject.SetActive(active);
        if (vrControlsCanvas != null)
            vrControlsCanvas.gameObject.SetActive(active);

        if (!active)
            SetMenuOpen(false);
        else
            RefreshControlsGuide();
    }

    void RefreshStatus()
    {
        TechWiseAttemptRecorder.AttemptSnapshot snapshot;
        var hasSnapshot = TechWiseAttemptRecorder.TryGetSnapshot(out snapshot);
        var simulation = TechWiseSimulationModeManager.IsDisassembly ? "Disassembly" : "Assembly";
        var mode = TechWiseSimulationModeManager.IsCompetitionMode
            ? "Competition"
            : TechWiseSimulationModeManager.IsTutorialMode ? "Tutorial" : "Practice";

        if (activityText != null)
            activityText.text = hasSnapshot ? snapshot.activity : $"{simulation} {mode}";
        if (statusText != null)
        {
            statusText.text = hasSnapshot ? snapshot.status : "Active";
            statusText.color = statusText.text == "Complete" ? TechWiseUITheme.Green : TechWiseUITheme.Navy;
        }
        if (timeText != null)
            timeText.text = FormatSeconds(hasSnapshot ? snapshot.durationSeconds : 0);
        if (mistakesText != null)
        {
            var hiddenCompetitionResult = hasSnapshot &&
                TechWiseSimulationModeManager.IsCompetitionMode &&
                !snapshot.resultsRevealed;
            if (mistakesText.transform.parent != null)
                mistakesText.transform.parent.gameObject.SetActive(!hiddenCompetitionResult);
            mistakesText.text = hasSnapshot ? snapshot.mistakes.ToString() : "0";
        }
        if (progressText != null)
            progressText.text = hasSnapshot ? $"{snapshot.completedSteps} / {snapshot.totalSteps}" : "0 / 0";
    }

    void SyncNow()
    {
        TechWiseOfflineAttemptQueue.SyncNow();
        ShowMessage(
            "Sync requested",
            Application.internetReachability == NetworkReachability.NotReachable
                ? "You are offline. Attempts remain queued and will upload when the portal is reachable."
                : "Queued competition attempts are being uploaded when the saved session is valid.");
        StartCoroutine(RefreshAfterDelay());
    }

    static string GetActiveControlMode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return MainMenu.VrModeValue;
#else
        return PlayerPrefs.GetString(MainMenu.ControlModeKey, MainMenu.DesktopModeValue);
#endif
    }

    void ShowControlsHelp()
    {
        var mode = GetActiveControlMode();
        ShowMessage(
            mode == MainMenu.VrModeValue ? "VR Controls" : "Desktop Controls",
            mode == MainMenu.VrModeValue ? BuildVrControlsText() : BuildDesktopControlsText());
    }

    void ShowSettingsHelp()
    {
        var network = Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Online";
        ShowMessage(
            "Settings",
            $"Control mode: {GetActiveControlMode()}\n" +
            $"Network: {network}\n" +
            $"Pending sync: {TechWiseOfflineAttemptQueue.PendingCount}\n" +
            $"Portal: {TechWisePortalClient.PortalBaseUrl}");
    }

    void ShowMessage(string title, string body)
    {
        if (detailsTitleText != null)
            detailsTitleText.text = title;
        if (detailsBodyText != null)
            detailsBodyText.text = body;
    }

    IEnumerator RefreshAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        RefreshStatus();
    }

    void SetControlMode(string mode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        mode = MainMenu.VrModeValue;
#endif
        PlayerPrefs.SetString(MainMenu.ControlModeKey, mode);
        PlayerPrefs.SetInt(ControlModeExplicitKey, 1);
        PlayerPrefs.Save();
        lastControlMode = null;
        RefreshControlsGuide();
        ShowControlsHelp();
    }

    void Logout()
    {
        TechWiseSessionStore.Clear();
        ShowMessage("Signed out", "The saved student session was cleared. Return to the main menu to sign in again.");
    }

    void BackToMainMenu()
    {
        SetMenuOpen(false);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    static string FormatSeconds(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }

    static bool WasMenuShortcutPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }
}
