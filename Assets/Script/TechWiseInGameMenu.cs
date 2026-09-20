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
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-7000)]
public sealed class TechWiseInGameMenu : MonoBehaviour
{
    const string MainMenuSceneName = "MainMenu";
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const string ControlModeExplicitKey = "TechWise360.ControlModeExplicit";
    const string ControlsGuideVisibleKey = "TechWise360.ControlsGuideVisible";
    const string WristShortcutHiddenKey = "TechWise360.WristShortcutHidden";

    sealed class ControlsGuideView
    {
        public GameObject root;
        public GameObject panel;
        public RectTransform panelRect;
        public GameObject tab;
        public GameObject scrollObject;
        public TMP_Text title;
        public TMP_Text body;
        public TMP_Text toggleText;
        public RectTransform scrollContent;
        public bool worldSpace;
        public TechWiseDraggableUiPanel draggable;
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
    Canvas vrPauseShortcut;
    Transform shortcutHand;
    bool shortcutHidden;
    Button shortcutVisibilityButton;
    string lastControlMode;
    bool isOpen;
    bool menuButtonHeld;
    bool? worldSpaceMenu;
    bool resetting;
    TechWisePauseSession pauseSession;
    Button resetButton;
    Button detailsBackButton;
    Button desktopModeButton;
    Button settingsDesktopButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseInGameMenu>() != null)
            return;

        var menuObject = new GameObject("TechWise In-Game Menu");
        if (Application.isPlaying) DontDestroyOnLoad(menuObject);
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
        pauseSession?.Dispose(); pauseSession = null; isOpen = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (vrControlsCanvas != null) vrControlsCanvas.gameObject.SetActive(false);
        if (vrPauseShortcut != null) vrPauseShortcut.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        ConfigureMenuCanvas();
        if (WasMenuShortcutPressed())
            ToggleMenu();

        if (isOpen)
            RefreshStatus();

        RefreshControlsGuide();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (vrPauseShortcut != null) Destroy(vrPauseShortcut.gameObject);
        vrPauseShortcut = null; shortcutHand = null;
        SetMenuOpen(false);
        resetting = false;
        if (vrControlsCanvas != null)
        {
            Destroy(vrControlsCanvas.gameObject);
            vrControlsCanvas = null;
        }
        vrControlsView = null;
        EnsureEventSystem();
        EnsureUi();
        worldSpaceMenu = null;
        ConfigureMenuCanvas();
        RefreshVisibility();
    }

    void EnsureUi()
    {
        shortcutHidden = PlayerPrefs.GetInt(WristShortcutHiddenKey, 0) == 1;
        if (canvas != null)
            return;

        EnsureEventSystem();

        var canvasObject = new GameObject("TechWise In-Game Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (Application.isPlaying) DontDestroyOnLoad(canvasObject);
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
        var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem eventSystem = null;

        foreach (var es in eventSystems)
        {
            if (es != null && es.gameObject.scene.isLoaded)
            {
                eventSystem = es;
                break;
            }
        }

        if (eventSystem == null && eventSystems.Length > 0)
            eventSystem = eventSystems[0];

        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("TechWise In-Game EventSystem", typeof(EventSystem));
            if (Application.isPlaying) DontDestroyOnLoad(eventSystemObject);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        foreach (var es in eventSystems)
        {
            if (es != null && es != eventSystem)
            {
                if (es.name.Contains("TechWise In-Game EventSystem"))
                    Destroy(es.gameObject);
                else
                    es.enabled = false;
            }
        }

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;
        EventSystem.current = eventSystem;

        // XRUIInputModule handles both tracked XR pointers and desktop mouse/gamepad.
        // Keeping one active XR module avoids duplicate clicks and guarantees interactors register.
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

    void CreateDimmer(Transform parent)
    {
        var rect = CreateRect("Scene Dimmer", parent, Vector2.zero, Vector2.one);
        dimmer = rect.gameObject.AddComponent<Image>();
        dimmer.color = new Color32(5, 15, 32, 112);
        dimmer.raycastTarget = false;
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
        view.worldSpace = worldSpace;
        var root = CreateRect(worldSpace ? "VR Controls Guide Root" : "Controls Guide Root", parent, Vector2.zero, Vector2.one);
        view.root = root.gameObject;

        var panel = CreateRect("Controls Guide Panel", root, Vector2.zero, Vector2.zero);
        panel.anchorMin = worldSpace ? Vector2.zero : new Vector2(0f, 1f);
        panel.anchorMax = worldSpace ? Vector2.one : new Vector2(0f, 1f);
        panel.pivot = worldSpace ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        panel.anchoredPosition = worldSpace ? Vector2.zero : new Vector2(22f, -88f);
        panel.sizeDelta = worldSpace ? Vector2.zero : new Vector2(430f, 650f);
        view.panel = panel.gameObject;
        view.panelRect = panel;
        if (worldSpace)
        {
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(0f, 680f);
        }
        else
        {
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(22f, -88f);
            panel.sizeDelta = new Vector2(430f, 650f);
        }
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color32(5, 20, 30, 235);
        view.panel = panel.gameObject;

        var header = CreateRect("Controls Guide Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f));
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 75f);
        header.anchoredPosition = Vector2.zero;

        var title = CreateText(header, "Controls Guide Title", "Desktop Controls", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = Color.white;
        Stretch(title.rectTransform, new Vector2(0.04f, 0.1f), new Vector2(0.7f, 0.9f));
        view.title = title;

        var hide = CreateFlatButton(header, "Hide Controls Button", "Hide", () =>
        {
            if (worldSpace)
            {
                var cur = PlayerPrefs.GetInt(ControlsGuideVisibleKey, 1) != 0;
                SetControlsExpanded(!cur);
            }
            else
            {
                SetControlsExpanded(false);
            }
        }, new Color32(31, 57, 72, 255), Color.white);
        Stretch(hide.GetComponent<RectTransform>(), new Vector2(0.73f, 0.15f), new Vector2(0.96f, 0.85f));
        view.toggleText = hide.GetComponentInChildren<TMP_Text>();

        var scrollObject = CreateRect("Controls Scroll View", panel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 1f));
        scrollObject.offsetMax = new Vector2(-16f, -80f);
        scrollObject.offsetMin = new Vector2(16f, 16f);
        var scrollRect = scrollObject.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 34f;
        view.scrollObject = scrollObject.gameObject;

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
        desktopModeButton = CreateActionButton(parent, "Desktop Controls", "View desktop control scheme", "PC", () => SetControlMode(MainMenu.DesktopModeValue), new Vector2(0.67f, 0.39f), new Vector2(0.95f, 0.52f), Color.white, TechWiseUITheme.PrimaryBlue);

        CreateActionButton(parent, "VR Controls", "View VR control scheme", "VR", () => SetControlMode(MainMenu.VrModeValue), new Vector2(left, 0.23f), new Vector2(0.35f, 0.36f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Back to Main Menu", "Return to the main menu", "Home", BackToMainMenu, new Vector2(0.37f, 0.23f), new Vector2(0.65f, 0.36f), Color.white, TechWiseUITheme.PrimaryBlue);
        CreateActionButton(parent, "Log Out", "Sign out of your account", "Out", Logout, new Vector2(0.67f, 0.23f), new Vector2(0.95f, 0.36f), TechWiseUITheme.LightRed, TechWiseUITheme.Red);
        resetButton = CreateActionButton(parent, "Reset Practice", "Start this practice again", "Sync", ResetPractice, new Vector2(0.67f, 0.39f), new Vector2(0.95f, 0.52f), Color.white, TechWiseUITheme.PrimaryBlue);
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
        detailsBackButton = CreateFlatButton(detailsPanel, "Back to Pause Menu", "Back", CloseDetails, TechWiseUITheme.PrimaryBlue, Color.white);
        Stretch(detailsBackButton.GetComponent<RectTransform>(), new Vector2(.8f, .86f), new Vector2(.96f, .97f));
        detailsBackButton.gameObject.SetActive(false);
        settingsDesktopButton = CreateFlatButton(detailsPanel, "Settings Desktop Controls", "Use Desktop Controls", () => SetControlMode(MainMenu.DesktopModeValue), TechWiseUITheme.PrimaryBlue, Color.white);
        Stretch(settingsDesktopButton.GetComponent<RectTransform>(), new Vector2(.05f, .04f), new Vector2(.47f, .16f));
        settingsDesktopButton.gameObject.SetActive(false);
        shortcutVisibilityButton = CreateFlatButton(detailsPanel, "Shortcut Visibility", "Hide wrist shortcut", () => SetShortcutHidden(!shortcutHidden), TechWiseUITheme.PrimaryBlue, Color.white);
        Stretch(shortcutVisibilityButton.GetComponent<RectTransform>(), new Vector2(.5f, .04f), new Vector2(.95f, .16f));
        shortcutVisibilityButton.gameObject.SetActive(false);
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
        // The interactive controller lesson supplies its own instructions. Keep the
        // saved guide preference and restore the regular overlay after onboarding.
        var showRegularGuide = !isOpen && !TechWiseTutorialRuntime.ControlsPending;
        ApplyControlsViewState(screenControlsView, showRegularGuide && mode != MainMenu.VrModeValue, expanded);
        ApplyControlsViewState(vrControlsView, showRegularGuide && mode == MainMenu.VrModeValue, expanded);
        if (vrControlsCanvas != null) vrControlsCanvas.gameObject.SetActive(!isOpen && mode == MainMenu.VrModeValue);
    }

    void EnsureVrControlsGuide()
    {
        var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null)
            return;

        if (vrControlsCanvas != null)
            return;

        var camPos = camera.transform.position;
        var camFwd = camera.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 0.01f)
            camFwd = Vector3.forward;
        camFwd.Normalize();
        var camRight = Vector3.Cross(Vector3.up, camFwd);

        var canvasObject = new GameObject("TechWise VR Controls Guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(null);
        canvasObject.transform.position = camPos + camFwd * 1.25f - camRight * 0.65f + Vector3.up * 0.05f;
        canvasObject.transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);

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
        var pause = CreateFlatButton(canvasObject.transform, "VR Pause Button", "Menu / Pause", ToggleMenu, TechWiseUITheme.PrimaryBlue, Color.white);
        var pauseRect = pause.GetComponent<RectTransform>();
        pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1f, 1f);
        pauseRect.pivot = Vector2.one;
        pauseRect.anchoredPosition = new Vector2(0f, 80f);
        pauseRect.sizeDelta = new Vector2(260f, 70f);
        var draggable = canvasObject.AddComponent<TechWiseDraggableUiPanel>();
        draggable.SetBounds(new Vector2(780f, 680f), Vector2.zero);
        vrControlsView.draggable = draggable;

        UpdateControlsGuideText(MainMenu.VrModeValue);
    }

    void LateUpdate()
    {
        bool show = IsGameplayScene(SceneManager.GetActiveScene().name) && GetActiveControlMode() == MainMenu.VrModeValue && !isOpen && !shortcutHidden;
        if (vrPauseShortcut != null) vrPauseShortcut.gameObject.SetActive(show);
        if (!show) return;
        var camera = Camera.main; if (camera == null) return;
        if (vrPauseShortcut == null)
        {
            var obj = new GameObject("Quest wrist pause shortcut", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            vrPauseShortcut = obj.GetComponent<Canvas>(); vrPauseShortcut.renderMode = RenderMode.WorldSpace; vrPauseShortcut.sortingOrder = 2000;
            var trRaycaster = obj.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (trRaycaster != null)
            {
                trRaycaster.ignoreReversedGraphics = false;
                trRaycaster.checkFor3DOcclusion = false;
                trRaycaster.checkFor2DOcclusion = false;
            }
            var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(460, 150); rect.localScale = Vector3.one * .0005f;
            var button = CreateFlatButton(obj.transform, "Wrist Pause Settings", "Pause / Settings", ToggleMenu, TechWiseUITheme.PrimaryBlue, Color.white);
            Stretch(button.GetComponent<RectTransform>(), new Vector2(0, .35f), new Vector2(.78f, 1), Vector2.zero, Vector2.zero);
            var hide = CreateFlatButton(obj.transform, "Hide wrist shortcut", "Hide", () => SetShortcutHidden(true), TechWiseUITheme.Navy, Color.white);
            Stretch(hide.GetComponent<RectTransform>(), new Vector2(.8f, .35f), Vector2.one, Vector2.zero, Vector2.zero);
            var hint = CreateText(obj.transform, "Pause shortcut hint", TechWiseSimulationModeManager.IsPracticeMode ? "Left Menu button • Reset Practice inside" : "Left Menu button • Resume / Settings", 15, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(hint.rectTransform, Vector2.zero, new Vector2(1, .35f), Vector2.zero, Vector2.zero); hint.raycastTarget = false;
        }
        if (shortcutHand == null)
            foreach (var interactor in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None))
                if (TechWiseTutorialRuntime.IsLeft(interactor.transform)) { shortcutHand = interactor.transform; break; }
        vrPauseShortcut.worldCamera = camera;
        var pose = camera.transform;
        var position = shortcutHand != null && shortcutHand.gameObject.activeInHierarchy
            ? shortcutHand.position + Vector3.up * .13f - pose.right * .1f
            : pose.position + pose.forward * .85f - pose.right * .32f - pose.up * .22f;
        vrPauseShortcut.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - pose.position, pose.up));
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
        if (view.worldSpace)
        {
            if (view.panel != null)
                view.panel.SetActive(true);
            if (view.tab != null)
                view.tab.SetActive(false);
            if (view.scrollObject != null)
                view.scrollObject.SetActive(expanded);
            if (view.toggleText != null)
                view.toggleText.text = expanded ? "Hide" : "Show";

            if (view.panelRect != null)
                view.panelRect.sizeDelta = new Vector2(0f, expanded ? 680f : 80f);

            if (view.draggable != null)
            {
                if (expanded)
                    view.draggable.SetBounds(new Vector2(780f, 680f), Vector2.zero);
                else
                    view.draggable.SetBounds(new Vector2(780f, 80f), new Vector2(0f, 300f));
            }
        }
        else
        {
            if (view.panel != null)
                view.panel.SetActive(expanded);
            if (view.tab != null)
                view.tab.SetActive(!expanded);
        }
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
            "Left controller Menu button   Pause / resume\n" +
            "Menu / Pause panel   Settings, controls, and Reset Practice";
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
        if (open && (!IsGameplayScene(SceneManager.GetActiveScene().name) || resetting)) return;
        if (open && !isOpen) pauseSession = new TechWisePauseSession();
        if (!open) { pauseSession?.Dispose(); pauseSession = null; }
        isOpen = open;
        if (menuPanel != null)
            menuPanel.gameObject.SetActive(open);
        if (dimmer != null)
            dimmer.gameObject.SetActive(open);

        ConfigureMenuCanvas();
        if (canvas != null && worldSpaceMenu == true)
            canvas.gameObject.SetActive(open && IsGameplayScene(SceneManager.GetActiveScene().name));
        if (open)
        {
            PositionMenu();
            CloseDetails();
            RefreshStatus();
        }

        RefreshControlsGuide();
    }

    void RefreshVisibility()
    {
        var active = IsGameplayScene(SceneManager.GetActiveScene().name);
        if (canvas != null)
            canvas.gameObject.SetActive(active && (worldSpaceMenu != true || isOpen));
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
        return MainMenu.VrModeValue;
    }

    void ShowControlsHelp()
    {
        var mode = GetActiveControlMode();
        ShowMessage(
            mode == MainMenu.VrModeValue ? "VR Controls" : "Desktop Controls",
            mode == MainMenu.VrModeValue ? BuildVrControlsText() : BuildDesktopControlsText());
        ExpandDetails();
    }

    void SetShortcutHidden(bool hidden)
    {
        shortcutHidden = hidden;
        PlayerPrefs.SetInt(WristShortcutHiddenKey, hidden ? 1 : 0);
        PlayerPrefs.Save();
        if (vrPauseShortcut != null) vrPauseShortcut.gameObject.SetActive(!hidden && !isOpen);
        UpdateShortcutLabel();
    }
    void UpdateShortcutLabel()
    {
        if (shortcutVisibilityButton != null) shortcutVisibilityButton.GetComponentInChildren<TMP_Text>().text = shortcutHidden ? "Show wrist shortcut" : "Hide wrist shortcut";
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
        ExpandDetails();
        shortcutVisibilityButton.gameObject.SetActive(true);
        UpdateShortcutLabel();
#if !UNITY_ANDROID || UNITY_EDITOR
        settingsDesktopButton.gameObject.SetActive(true);
        Stretch(shortcutVisibilityButton.GetComponent<RectTransform>(), new Vector2(.5f, .04f), new Vector2(.95f, .16f));
#else
        Stretch(shortcutVisibilityButton.GetComponent<RectTransform>(), new Vector2(.2f, .04f), new Vector2(.8f, .16f));
#endif
    }

    void ExpandDetails()
    {
        detailsPanel.GetComponent<Image>().color = new Color32(247, 252, 255, 255);
        detailsPanel.Find("Details Icon").gameObject.SetActive(false);
        settingsDesktopButton.gameObject.SetActive(false);
        shortcutVisibilityButton.gameObject.SetActive(false);
        Stretch(detailsPanel, new Vector2(.05f, .07f), new Vector2(.95f, .85f));
        Stretch(detailsTitleText.rectTransform, new Vector2(.05f, .87f), new Vector2(.76f, .97f));
        Stretch(detailsBodyText.rectTransform, new Vector2(.05f, .22f), new Vector2(.95f, .83f));
        detailsBodyText.fontSizeMax = 26f;
        detailsBodyText.alignment = TextAlignmentOptions.TopLeft;
        detailsBackButton.gameObject.SetActive(true);
    }

    void CloseDetails()
    {
        if (detailsPanel == null) return;
        if (shortcutVisibilityButton != null) shortcutVisibilityButton.gameObject.SetActive(false);
        detailsPanel.Find("Details Icon").gameObject.SetActive(true);
        if (settingsDesktopButton != null) settingsDesktopButton.gameObject.SetActive(false);
        Stretch(detailsPanel, new Vector2(.05f, .07f), new Vector2(.95f, .18f));
        Stretch(detailsTitleText.rectTransform, new Vector2(.08f, .52f), new Vector2(.97f, .86f));
        Stretch(detailsBodyText.rectTransform, new Vector2(.08f, .12f), new Vector2(.97f, .5f));
        detailsBodyText.fontSizeMax = 14f;
        detailsBackButton.gameObject.SetActive(false);
        ShowMessage(TechWiseSimulationModeManager.IsCompetitionMode ? "Competition timer keeps running." : "Session paused",
            "Resume to continue. Settings and Controls remain available while paused.");
    }

    void ConfigureMenuCanvas()
    {
        if (canvas == null) return;
        bool vr = GetActiveControlMode() == MainMenu.VrModeValue;
        if (resetButton != null) resetButton.gameObject.SetActive(TechWiseSimulationModeManager.IsPracticeMode);
        if (desktopModeButton != null) desktopModeButton.gameObject.SetActive(false);
        if (worldSpaceMenu == vr) { if (vr) canvas.worldCamera = Camera.main; return; }
        worldSpaceMenu = vr;
        var scaler = canvas.GetComponent<CanvasScaler>();
        var tracked = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (vr && tracked == null) tracked = canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        if (tracked != null)
        {
            tracked.enabled = vr;
            tracked.ignoreReversedGraphics = false;
            // This is a modal UI: nearby bench geometry must not occlude menu buttons.
            tracked.checkFor3DOcclusion = false;
            tracked.checkFor2DOcclusion = false;
        }
        canvas.GetComponent<GraphicRaycaster>().enabled = !vr;
        canvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        scaler.uiScaleMode = vr ? CanvasScaler.ScaleMode.ConstantPixelSize : CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (vr)
        {
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1600f, 1000f);
            rect.localScale = Vector3.one * .001f;
            scaler.dynamicPixelsPerUnit = 20f;
            canvas.worldCamera = Camera.main;
            PositionMenu();
        }
        else canvas.transform.localScale = Vector3.one;
        var hint = menuPanel.Find("Esc Hint")?.GetComponent<TMP_Text>();
        if (hint != null) hint.gameObject.SetActive(!vr);
        canvas.gameObject.SetActive(IsGameplayScene(SceneManager.GetActiveScene().name) && (!vr || isOpen));
    }

    void PositionMenu()
    {
        if (worldSpaceMenu != true || Camera.main == null) return;
        var camera = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        canvas.transform.SetPositionAndRotation(camera.position + forward * 1.3f,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    void ResetPractice()
    {
        if (resetting || !TechWiseSimulationModeManager.IsPracticeMode ||
            !IsGameplayScene(SceneManager.GetActiveScene().name)) return;
        resetting = true;
        SetMenuOpen(false);
        // Re-enter the same activity through its normal initialization, including
        // disassembly preparation and all phase-one tools/fasteners. Keep preferences.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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

    bool WasMenuShortcutPressed()
    {
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool held = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out var pressed) && pressed;
#if ENABLE_INPUT_SYSTEM
        var inputController = UnityEngine.InputSystem.XR.XRController.leftHand;
        var menuControl = inputController?.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("menuButton");
        held |= menuControl != null && menuControl.isPressed;
#endif
        bool edge = held && !menuButtonHeld;
        menuButtonHeld = held;
        if (edge && GetActiveControlMode() == MainMenu.VrModeValue) return true;
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
