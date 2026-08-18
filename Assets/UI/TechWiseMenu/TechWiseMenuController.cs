using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class TechWiseMenuController : MonoBehaviour
{
    const string AssemblyType = TechWiseSimulationModeManager.AssemblyType;
    const string DisassemblyType = TechWiseSimulationModeManager.DisassemblyType;

    MainMenu menu;
    UIDocument document;
    PanelSettings panelSettings;
    VisualElement root;
    VisualElement mainScreen;
    VisualElement controlsScreen;
    VisualElement modalOverlay;
    VisualElement loginCard;
    VisualElement signedInCard;
    VisualElement accountSection;
    VisualElement whyLoginCard;

    TextField identifierField;
    TextField passwordField;
    Toggle rememberToggle;
    Label loginStatusLabel;
    Label welcomeTitleLabel;
    Label syncPillLabel;
    Label signedInNameLabel;
    Label signedInActivityLabel;
    Label signedInPendingLabel;
    Label safetySubtitleLabel;

    Button desktopButton;
    Button vrButton;
    Button startItem;
    Button practiceItem;
    Button competitionItem;
    Button controlsItem;
    Button settingsItem;
    Button quitItem;
    Button accountLogoutItem;
    Button refreshActivitiesButton;
    Button signedOutLoginButton;
    Button signedOutCreateButton;
    Button signedOutForgotButton;
    Button signedInLogoutButton;
    Button controlsBackButton;

    Label startSubtitleLabel;
    Label practiceSubtitleLabel;
    Label competitionSubtitleLabel;
    Label settingsSubtitleLabel;
    Label signedInSubtitleLabel;

    bool initialized;
    static readonly Color IconBlue = new(0f, 0.36f, 1f, 1f);
    static readonly Color IconRed = new(0.9f, 0.08f, 0.08f, 1f);
    static readonly Color IconGreen = new(0.04f, 0.58f, 0.22f, 1f);

    public bool Initialize(MainMenu source)
    {
        if (source == null)
            return false;

        menu = source;

        try
        {
            EnsureDocument();
            BuildInterface();
            BindEvents();
            menu.MenuStateChanged += Refresh;
            initialized = true;
            ShowMainMenuScreen();
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"TechWise UI Toolkit menu failed to initialize. Falling back to Canvas menu.\n{ex}");
            initialized = false;
            return false;
        }
    }

    void OnDestroy()
    {
        if (menu != null)
            menu.MenuStateChanged -= Refresh;
    }

    void EnsureDocument()
    {
        document = GetComponentInChildren<UIDocument>(true);
        if (document == null)
        {
            var documentObject = new GameObject("TechWise UI Toolkit Menu");
            documentObject.transform.SetParent(transform, false);
            document = documentObject.AddComponent<UIDocument>();
        }

        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "TechWise Runtime Menu Panel Settings";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 5000;
        }

        document.panelSettings = panelSettings;
    }

    void BuildInterface()
    {
        root = document.rootVisualElement;
        root.Clear();
        root.styleSheets.Clear();
        root.AddToClassList("tw-root");
        LoadStyleSheet(root);

        mainScreen = new VisualElement { name = "main-screen" };
        mainScreen.AddToClassList("tw-screen");
        mainScreen.AddToClassList("tw-main-screen");
        root.Add(mainScreen);

        controlsScreen = new VisualElement { name = "controls-screen" };
        controlsScreen.AddToClassList("tw-screen");
        controlsScreen.AddToClassList("tw-controls-screen");
        root.Add(controlsScreen);

        modalOverlay = new VisualElement { name = "modal-overlay" };
        modalOverlay.AddToClassList("modal-overlay");
        root.Add(modalOverlay);

        BuildMainScreen();
        BuildControlsScreen();
        CloseModal();
    }

    void LoadStyleSheet(VisualElement target)
    {
        var sheet = Resources.Load<StyleSheet>("UI/TechWiseMenu/TechWiseMainMenu");

#if UNITY_EDITOR
        if (sheet == null)
            sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/TechWiseMenu/TechWiseMainMenu.uss");
#endif

        if (sheet != null)
            target.styleSheets.Add(sheet);
        else
            ApplyMinimalFallbackStyles(target);
    }

    static void ApplyMinimalFallbackStyles(VisualElement target)
    {
        target.style.flexGrow = 1f;
        target.style.backgroundColor = new Color(0.92f, 0.97f, 1f, 1f);
        target.style.color = new Color(0.03f, 0.08f, 0.2f, 1f);
    }

    void BuildMainScreen()
    {
        var left = new VisualElement { name = "main-left" };
        left.AddToClassList("main-left");
        mainScreen.Add(left);

        BuildBrand(left, false);
        BuildLoginCard(left);
        BuildSignedInCard(left);
        BuildSafetyCard(left);

        var right = new VisualElement { name = "main-right" };
        right.AddToClassList("main-right");
        mainScreen.Add(right);

        BuildModeSelector(right);
        BuildMainMenuCard(right);
    }

    void BuildBrand(VisualElement parent, bool compact)
    {
        var brand = new VisualElement();
        brand.AddToClassList("brand");
        if (compact)
            brand.AddToClassList("compact");
        parent.Add(brand);

        var copy = new VisualElement();
        copy.AddToClassList("brand-copy");
        brand.Add(copy);

        var title = new VisualElement();
        title.AddToClassList("brand-title-row");
        copy.Add(title);

        var techwise = new Label("TECHWISE");
        techwise.AddToClassList("brand-title");
        title.Add(techwise);

        var number = new Label("360");
        number.AddToClassList("brand-title");
        number.AddToClassList("brand-title-blue");
        title.Add(number);

        var subtitle = new Label("Learn. Practice. Compete.");
        subtitle.AddToClassList("brand-subtitle");
        copy.Add(subtitle);

        var underline = new VisualElement();
        underline.AddToClassList("brand-underline");
        copy.Add(underline);
    }

    void BuildLoginCard(VisualElement parent)
    {
        loginCard = new VisualElement { name = "login-card" };
        loginCard.AddToClassList("card");
        loginCard.AddToClassList("sync-card");
        parent.Add(loginCard);

        loginCard.Add(MakeLabel("Student Sync", "card-title"));
        loginCard.Add(MakeLabel("Log in with a student account to sync competition attempts.", "card-subtitle"));
        loginCard.Add(MakeDivider());

        identifierField = new TextField { name = "identifier-field" };
        identifierField.AddToClassList("flat-field");
        identifierField.label = string.Empty;
        identifierField.value = string.Empty;
        identifierField.RegisterValueChangedCallback(_ => SetStatus(string.Empty));
        identifierField.tooltip = "Student Username or Email";
        loginCard.Add(identifierField);

        passwordField = new TextField { name = "password-field", isPasswordField = true };
        passwordField.AddToClassList("flat-field");
        passwordField.label = string.Empty;
        passwordField.tooltip = "Password";
        loginCard.Add(passwordField);

        var loginMeta = new VisualElement();
        loginMeta.AddToClassList("login-meta");
        loginCard.Add(loginMeta);

        rememberToggle = new Toggle("Remember me") { name = "remember-toggle" };
        rememberToggle.AddToClassList("remember-toggle");
        loginMeta.Add(rememberToggle);

        signedOutForgotButton = new Button(() => menu.SetPortalStatusFromMenu("Use the WebPortal forgot password page, then return here to log in.")) { text = "Forgot password?" };
        signedOutForgotButton.AddToClassList("link-button");
        loginMeta.Add(signedOutForgotButton);

        signedOutLoginButton = new Button(OnLoginClicked) { name = "login-button", text = "Log In" };
        signedOutLoginButton.AddToClassList("primary-button");
        loginCard.Add(signedOutLoginButton);

        var separator = new VisualElement();
        separator.AddToClassList("or-row");
        separator.Add(MakeDivider());
        separator.Add(MakeLabel("OR", "or-text"));
        separator.Add(MakeDivider());
        loginCard.Add(separator);

        signedOutCreateButton = new Button(() => menu.SetPortalStatusFromMenu("Create student accounts in the WebPortal, wait for approval, then log in here.")) { text = "Create Student Account" };
        signedOutCreateButton.AddToClassList("outline-button");
        loginCard.Add(signedOutCreateButton);

        loginStatusLabel = MakeLabel(string.Empty, "status-line");
        loginCard.Add(loginStatusLabel);
    }

    void BuildSignedInCard(VisualElement parent)
    {
        signedInCard = new VisualElement { name = "signed-card" };
        signedInCard.AddToClassList("card");
        signedInCard.AddToClassList("sync-card");
        parent.Add(signedInCard);

        var titleRow = new VisualElement();
        titleRow.AddToClassList("row-between");
        signedInCard.Add(titleRow);

        welcomeTitleLabel = MakeLabel("Welcome back!", "card-title");
        titleRow.Add(welcomeTitleLabel);

        syncPillLabel = MakeLabel("SYNCED", "sync-pill");
        titleRow.Add(syncPillLabel);

        signedInCard.Add(MakeDivider());

        signedInNameLabel = MakeLabel("Logged in.", "info-line");
        signedInActivityLabel = MakeLabel("No active VR activities loaded.", "info-line");
        signedInPendingLabel = MakeLabel("Pending sync: 0", "info-line");
        signedInCard.Add(MakeInfoRow("user", signedInNameLabel));
        signedInCard.Add(MakeInfoRow("vr", signedInActivityLabel));
        signedInCard.Add(MakeInfoRow("cloud", signedInPendingLabel));

        var actionRow = new VisualElement();
        actionRow.AddToClassList("sync-actions");
        signedInCard.Add(actionRow);

        refreshActivitiesButton = MakeTileButton("Refresh Activities", "Check for new or updated activities.", "refresh", () => menu.RefreshActivitiesFromMenu(), false);
        signedInLogoutButton = MakeTileButton("Log Out", "Sign out and clear local session data.", "logout", () => menu.LogoutFromPortal(), true);
        actionRow.Add(refreshActivitiesButton);
        actionRow.Add(signedInLogoutButton);
    }

    void BuildSafetyCard(VisualElement parent)
    {
        var safety = new VisualElement { name = "safety-card" };
        safety.AddToClassList("card");
        safety.AddToClassList("safety-card");
        parent.Add(safety);

        safety.Add(MakeIconBadge("shield", "safety-icon", false, IconBlue));

        var stack = new VisualElement();
        stack.AddToClassList("safety-copy");
        safety.Add(stack);
        stack.Add(MakeLabel("Your progress is safe.", "safety-title"));
        safetySubtitleLabel = MakeLabel("Competition data saves locally first, then syncs when you log in.", "safety-subtitle");
        stack.Add(safetySubtitleLabel);
    }

    void BuildModeSelector(VisualElement parent)
    {
        var selector = new VisualElement { name = "mode-selector" };
        selector.AddToClassList("selector-card");
        parent.Add(selector);

        selector.Add(MakeLabel("SELECT MODE", "section-label"));

        var row = new VisualElement();
        row.AddToClassList("mode-row");
        selector.Add(row);

        desktopButton = new Button(() => menu.UseDesktopMode()) { text = string.Empty };
        desktopButton.AddToClassList("mode-button");
        desktopButton.Add(MakeModeContent("monitor", "Desktop"));
        row.Add(desktopButton);

        vrButton = new Button(() => menu.UseVrMode()) { text = string.Empty };
        vrButton.AddToClassList("mode-button");
        vrButton.Add(MakeModeContent("vr", "VR"));
        row.Add(vrButton);
    }

    void BuildMainMenuCard(VisualElement parent)
    {
        var card = new VisualElement { name = "main-menu-card" };
        card.AddToClassList("menu-card");
        parent.Add(card);

        card.Add(MakeLabel("MAIN MENU", "section-label"));

        startItem = MakeMenuItem("start-item", "play", "Start", "Launch the selected activity.", () => menu.SinglePlayer());
        practiceItem = MakeMenuItem("practice-item", "cap", "Practice Mode", "Choose assembly or disassembly practice.", ShowPracticeModal);
        competitionItem = MakeMenuItem("competition-item", "trophy", "Competition", "Compete and sync leaderboard attempts.", ShowCompetitionModal);
        controlsItem = MakeMenuItem("controls-item", "keyboard", "Controls", "View keyboard, mouse, and VR controls.", ShowControlsScreen);
        settingsItem = MakeMenuItem("settings-item", "gear", "Settings", "Adjust preferences and sync details.", ShowSettingsModal);
        quitItem = MakeMenuItem("quit-item", "power", "Quit", "Exit TechWise 360.", () => menu.QuitGame(), true);

        startSubtitleLabel = startItem.Q<Label>("start-item-subtitle");
        practiceSubtitleLabel = practiceItem.Q<Label>("practice-item-subtitle");
        competitionSubtitleLabel = competitionItem.Q<Label>("competition-item-subtitle");
        settingsSubtitleLabel = settingsItem.Q<Label>("settings-item-subtitle");

        card.Add(startItem);
        card.Add(practiceItem);
        card.Add(competitionItem);
        card.Add(controlsItem);
        card.Add(settingsItem);
        card.Add(quitItem);

        accountSection = new VisualElement { name = "account-section" };
        accountSection.AddToClassList("account-section");
        accountSection.Add(MakeLabel("ACCOUNT", "section-label"));
        accountLogoutItem = MakeMenuItem("account-logout-item", "user", "Log Out", "Sign out of your account.", () => menu.LogoutFromPortal(), true);
        signedInSubtitleLabel = accountLogoutItem.Q<Label>("account-logout-item-subtitle");
        accountSection.Add(accountLogoutItem);
        card.Add(accountSection);

        whyLoginCard = new VisualElement { name = "why-login" };
        whyLoginCard.AddToClassList("why-card");
        whyLoginCard.Add(MakeLabel("Why log in?", "why-title"));
        whyLoginCard.Add(MakeInfoRow("user", MakeLabel("Logging in syncs your competition attempts and updates leaderboards across your devices.", "why-text")));
        card.Add(whyLoginCard);
    }

    Button MakeMenuItem(string name, string iconName, string title, string subtitle, Action action, bool danger = false)
    {
        var button = new Button(action) { name = name };
        button.AddToClassList("menu-item");
        if (danger)
            button.AddToClassList("danger-item");

        button.Add(MakeIconBadge(iconName, "menu-icon", danger, danger ? IconRed : IconBlue));

        var copy = new VisualElement();
        copy.AddToClassList("menu-copy");
        button.Add(copy);

        copy.Add(MakeLabel(title, "menu-title"));
        var subtitleLabel = MakeLabel(subtitle, "menu-subtitle");
        subtitleLabel.name = name + "-subtitle";
        copy.Add(subtitleLabel);

        var arrow = new Label(">");
        arrow.AddToClassList("menu-arrow");
        button.Add(arrow);

        return button;
    }

    Button MakeTileButton(string title, string subtitle, string iconName, Action action, bool danger)
    {
        var button = new Button(action);
        button.AddToClassList("tile-button");
        if (danger)
            button.AddToClassList("danger-tile");

        button.Add(MakeIconBadge(iconName, "tile-icon", danger, danger ? IconRed : IconBlue));

        var copy = new VisualElement();
        copy.AddToClassList("tile-copy");
        copy.Add(MakeLabel(title, "tile-title"));
        copy.Add(MakeLabel(subtitle, "tile-subtitle"));
        button.Add(copy);

        var arrow = new Label(">");
        arrow.AddToClassList("tile-arrow");
        button.Add(arrow);
        return button;
    }

    void BuildControlsScreen()
    {
        var header = new VisualElement();
        header.AddToClassList("controls-header");
        controlsScreen.Add(header);
        BuildBrand(header, true);

        var card = new VisualElement();
        card.AddToClassList("controls-card");
        controlsScreen.Add(card);

        card.Add(MakeLabel("Controls", "controls-title"));
        card.Add(MakeLabel("View all control mappings for Desktop and VR modes.", "controls-subtitle"));

        var body = new VisualElement();
        body.AddToClassList("controls-body");
        card.Add(body);

        var table = new VisualElement();
        table.AddToClassList("controls-table");
        body.Add(table);
        table.Add(MakeLabel("Desktop Controls", "controls-panel-title"));
        AddControlRow(table, "WASD", "move");
        AddControlRow(table, "Mouse", "look");
        AddControlRow(table, "Left click", "grab/drop part or press crosshair button");
        AddControlRow(table, "Right mouse drag", "rotate held part freely");
        AddControlRow(table, "Mouse wheel", "rotate held part");
        AddControlRow(table, "R", "cycle rotation axis");
        AddControlRow(table, "Shift + wheel", "move held part closer/farther");
        AddControlRow(table, "F", "reset held part rotation");
        AddControlRow(table, "Backspace", "reset held or last grabbed part");
        AddControlRow(table, "Enter / Space", "start visible competition prompt");
        AddControlRow(table, "Esc", "unlock cursor or open in-game menu");

        var vrPanel = new VisualElement();
        vrPanel.AddToClassList("vr-panel");
        body.Add(vrPanel);
        vrPanel.Add(MakeLabel("VR Controls", "controls-panel-title"));
        var vrGlyph = MakeIconBadge("vr", "vr-glyph", false, IconBlue);
        vrPanel.Add(vrGlyph);
        vrPanel.Add(MakeLabel("Use headset and controllers when connected.\nCrosshair/desktop controls remain available when Desktop mode is selected.", "vr-copy"));

        controlsBackButton = new Button(ShowMainMenuScreen) { text = "Back" };
        controlsBackButton.AddToClassList("primary-button");
        controlsBackButton.AddToClassList("back-button");
        card.Add(controlsBackButton);
    }

    static void AddControlRow(VisualElement table, string key, string action)
    {
        var row = new VisualElement();
        row.AddToClassList("control-row");
        row.Add(MakeLabel(key, "control-key"));
        row.Add(MakeLabel(action, "control-action"));
        table.Add(row);
    }

    void BindEvents()
    {
        modalOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == modalOverlay)
                CloseModal();
        });
    }

    void OnLoginClicked()
    {
        var identifier = identifierField?.value ?? string.Empty;
        var password = passwordField?.value ?? string.Empty;
        menu.LoginToPortal(identifier, password, (ok, _) =>
        {
            if (ok && passwordField != null)
                passwordField.value = string.Empty;
            Refresh();
        });
    }

    public void ShowMainMenuScreen()
    {
        if (!initialized)
            return;

        SetDisplay(mainScreen, true);
        SetDisplay(controlsScreen, false);
        CloseModal();
    }

    public void ShowControlsScreen()
    {
        if (!initialized)
            return;

        SetDisplay(mainScreen, false);
        SetDisplay(controlsScreen, true);
        CloseModal();
    }

    public void CloseModal()
    {
        if (modalOverlay == null)
            return;

        modalOverlay.Clear();
        SetDisplay(modalOverlay, false);
        SetSelected(practiceItem, false);
        SetSelected(competitionItem, false);
    }

    void ShowSettingsModal()
    {
        if (!RequireSignedIn("Log in before opening settings."))
            return;

        SetSelected(settingsItem, true);
        modalOverlay.Clear();
        BuildSimpleModal(
            "Settings",
            "Account and sync status",
            new[]
            {
                $"Signed in as {menu.StudentDisplayName}.",
                $"Network: {(Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Online")}.",
                $"Pending sync: {menu.PendingSyncCount}.",
                $"Portal: {TechWisePortalClient.PortalBaseUrl}"
            },
            ("Sync Now", () => menu.SyncPendingAttemptsFromMenu(), true),
            ("Close", CloseModal, false));
        SetDisplay(modalOverlay, true);
    }

    void ShowPracticeModal()
    {
        if (!RequireSignedIn("Log in before opening practice modes."))
            return;

        SetSelected(practiceItem, true);
        SetSelected(competitionItem, false);
        modalOverlay.Clear();

        var card = BeginModal("Practice Mode", "Choose a practice mode.\nPractice attempts stay local and do not affect the leaderboard.", "cap");
        card.Add(MakeModalButton("Assembly Practice", "Practice assembling components.", "cap", () => menu.StartAssemblyPractice(), true));
        card.Add(MakeModalButton("Disassembly Practice", "Practice disassembling components.", "keyboard", () => menu.StartDisassemblyPractice(), true));
        card.Add(MakeModalButton("Close", "Return to the main menu.", "close", CloseModal, false));
        SetDisplay(modalOverlay, true);
    }

    void ShowCompetitionModal()
    {
        if (!RequireSignedIn("Log in before opening competitions."))
            return;

        SetSelected(competitionItem, true);
        SetSelected(practiceItem, false);
        modalOverlay.Clear();

        var card = BeginModal("Competition", "Choose the competition attempt you want to run.", "trophy");
        card.AddToClassList("competition-modal-card");
        var status = new VisualElement();
        status.AddToClassList("modal-status");
        status.Add(MakeInfoLine("-", $"{(Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Online")}. {menu.ActiveCompetitionCount} active VR activit{(menu.ActiveCompetitionCount == 1 ? "y" : "ies")} loaded."));
        status.Add(MakeInfoLine("-", $"Pending sync: {menu.PendingSyncCount}."));
        card.Add(status);

        card.Add(MakeInfoLine("-", menu.HasCompetitionFor(AssemblyType)
            ? $"Assembly: {menu.CompetitionTitleFor(AssemblyType)}."
            : "Assembly: open attempt if allowed by dashboard."));
        card.Add(MakeInfoLine("-", menu.HasCompetitionFor(DisassemblyType)
            ? $"Disassembly: {menu.CompetitionTitleFor(DisassemblyType)}."
            : "Disassembly: open attempt if allowed by dashboard."));
        card.Add(MakeDivider());
        card.Add(MakeModalButton("Assembly Competition", "Compete in assembly challenges.", "trophy", () => menu.StartAssemblyCompetition(), true));
        card.Add(MakeModalButton("Disassembly Competition", "Compete in disassembly challenges.", "keyboard", () => menu.StartDisassemblyCompetition(), true));
        card.Add(MakeModalButton("Refresh Activities", "Check for new or updated activities.", "refresh", () => menu.RefreshActivitiesFromMenu(), false));
        card.Add(MakeModalButton("Close", "Return to the main menu.", "close", CloseModal, false));
        SetDisplay(modalOverlay, true);
    }

    void BuildSimpleModal(string title, string subtitle, string[] lines, params (string title, Action action, bool primary)[] buttons)
    {
        var card = BeginModal(title, subtitle, title == "Settings" ? "gear" : "info");
        foreach (var line in lines)
            card.Add(MakeInfoLine("-", line));

        card.Add(MakeDivider());
        foreach (var button in buttons)
            card.Add(MakeModalButton(button.title, string.Empty, button.primary ? "refresh" : "close", button.action, button.primary));
    }

    VisualElement BeginModal(string title, string subtitle, string iconName)
    {
        var card = new VisualElement();
        card.AddToClassList("modal-card");
        modalOverlay.Add(card);

        var head = new VisualElement();
        head.AddToClassList("modal-head");
        card.Add(head);

        head.Add(MakeIconBadge(iconName, "modal-icon", false, IconBlue));
        head.Add(MakeLabel(title, "modal-title"));
        var close = new Button(CloseModal) { text = "X" };
        close.AddToClassList("modal-close");
        head.Add(close);

        if (!string.IsNullOrWhiteSpace(subtitle))
            card.Add(MakeLabel(subtitle, "modal-subtitle"));

        return card;
    }

    Button MakeModalButton(string title, string subtitle, string iconName, Action action, bool primary)
    {
        var button = new Button(action);
        button.AddToClassList("modal-button");
        if (primary)
            button.AddToClassList("modal-button-primary");

        button.Add(MakeIconBadge(iconName, "modal-button-icon", false, primary ? Color.white : IconBlue));
        var copy = new VisualElement();
        copy.AddToClassList("modal-button-copy");
        copy.Add(MakeLabel(title, "modal-button-title"));
        if (!string.IsNullOrWhiteSpace(subtitle))
            copy.Add(MakeLabel(subtitle, "modal-button-subtitle"));
        button.Add(copy);
        button.Add(MakeLabel(">", "modal-button-arrow"));
        return button;
    }

    bool RequireSignedIn(string message)
    {
        if (menu.IsSignedIn)
            return true;

        menu.SetPortalStatusFromMenu(message);
        return false;
    }

    void Refresh()
    {
        if (!initialized || menu == null)
            return;

        var signedIn = menu.IsSignedIn;
        SetDisplay(loginCard, !signedIn);
        SetDisplay(signedInCard, signedIn);
        SetDisplay(accountSection, signedIn);
        SetDisplay(whyLoginCard, !signedIn);

        if (loginStatusLabel != null)
            loginStatusLabel.text = menu.PortalStatusMessage;

        if (welcomeTitleLabel != null)
            welcomeTitleLabel.text = $"Welcome back, {menu.StudentFirstName}!";
        if (syncPillLabel != null)
        {
            syncPillLabel.text = menu.PendingSyncCount == 0 ? "SYNCED" : $"PENDING {menu.PendingSyncCount}";
            syncPillLabel.EnableInClassList("has-pending", menu.PendingSyncCount > 0);
        }
        if (signedInNameLabel != null)
            signedInNameLabel.text = $"Logged in as {menu.StudentDisplayName}.";
        if (signedInActivityLabel != null)
            signedInActivityLabel.text = $"{menu.ActiveCompetitionCount} active VR activit{(menu.ActiveCompetitionCount == 1 ? "y" : "ies")} loaded.";
        if (signedInPendingLabel != null)
            signedInPendingLabel.text = $"Pending sync: {menu.PendingSyncCount}";
        if (safetySubtitleLabel != null)
            safetySubtitleLabel.text = signedIn
                ? "Competition data saves locally first, then syncs when online."
                : "Competition data saves locally first, then syncs when you log in.";

        desktopButton.EnableInClassList("is-active", menu.IsDesktopSelected);
        vrButton.EnableInClassList("is-active", !menu.IsDesktopSelected);

        SetActionEnabled(startItem, signedIn, signedIn ? "Launch the selected activity." : "Log in required to start.");
        SetActionEnabled(practiceItem, signedIn, signedIn ? "Choose assembly or disassembly practice." : "Log in required to practice.");
        SetActionEnabled(competitionItem, signedIn, signedIn ? "Compete and sync leaderboard attempts." : "Log in required to compete.");
        SetActionEnabled(settingsItem, signedIn, signedIn ? "Adjust preferences and sync details." : "Log in required to adjust preferences.");
        SetActionEnabled(accountLogoutItem, signedIn, "Sign out of your account.");

        controlsItem.SetEnabled(true);
        quitItem.SetEnabled(true);
    }

    void SetActionEnabled(Button item, bool enabled, string subtitle)
    {
        if (item == null)
            return;

        item.SetEnabled(enabled);
        item.EnableInClassList("is-disabled", !enabled);
        var subtitleLabel = item.Q<Label>(item.name + "-subtitle");
        if (subtitleLabel != null)
            subtitleLabel.text = subtitle;
        var arrow = item.Q<Label>(className: "menu-arrow");
        if (arrow != null)
            arrow.text = enabled ? ">" : string.Empty;
    }

    static void SetSelected(VisualElement element, bool selected)
    {
        if (element != null)
            element.EnableInClassList("is-selected", selected);
    }

    static VisualElement MakeModeContent(string iconName, string text)
    {
        var content = new VisualElement();
        content.AddToClassList("mode-content");
        content.Add(MakeIconImage(iconName, "mode-icon", IconBlue));
        content.Add(MakeLabel(text, "mode-text"));
        return content;
    }

    static VisualElement MakeInfoRow(string iconName, Label label)
    {
        var row = new VisualElement();
        row.AddToClassList("info-row");
        row.Add(MakeIconImage(iconName, "info-icon", IconBlue));
        row.Add(label);
        return row;
    }

    static VisualElement MakeIconBadge(string iconName, string className, bool danger, Color iconColor)
    {
        var badge = new VisualElement();
        badge.AddToClassList(className);
        if (danger)
            badge.AddToClassList("danger-icon");
        badge.Add(MakeIconImage(iconName, "line-icon", iconColor));
        return badge;
    }

    static VisualElement MakeIconImage(string iconName, string className, Color iconColor)
    {
        var icon = new VisualElement();
        icon.AddToClassList(className);
        icon.style.backgroundImage = new StyleBackground(TechWiseIconTexture.Get(iconName, iconColor));
        return icon;
    }

    static Label MakeLabel(string text, string className)
    {
        var label = new Label(text);
        if (!string.IsNullOrWhiteSpace(className))
            label.AddToClassList(className);
        return label;
    }

    static Label MakeInfoLine(string icon, string text)
    {
        var line = new Label($"{icon}  {text}");
        line.AddToClassList("info-line");
        return line;
    }

    static VisualElement MakeDivider()
    {
        var divider = new VisualElement();
        divider.AddToClassList("divider");
        return divider;
    }

    static void SetDisplay(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void SetStatus(string message)
    {
        if (loginStatusLabel != null)
            loginStatusLabel.text = message ?? string.Empty;
    }

    static class TechWiseIconTexture
    {
        const int Size = 64;
        static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Get(string name, Color color)
        {
            var key = $"{name}:{ColorUtility.ToHtmlStringRGBA(color)}";
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "TechWise Icon " + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color32[Size * Size];
            texture.SetPixels32(clear);
            Draw(texture, name, color);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        static void Draw(Texture2D texture, string name, Color color)
        {
            switch (name)
            {
                case "play":
                    FillTriangle(texture, new Vector2Int(25, 18), new Vector2Int(25, 46), new Vector2Int(46, 32), color);
                    break;
                case "monitor":
                    Rect(texture, 13, 15, 38, 25, color, 4);
                    Line(texture, 32, 41, 32, 50, color, 4);
                    Line(texture, 22, 50, 42, 50, color, 4);
                    break;
                case "vr":
                    Rect(texture, 13, 23, 38, 20, color, 4);
                    Circle(texture, 25, 33, 5, color, 4);
                    Circle(texture, 39, 33, 5, color, 4);
                    break;
                case "user":
                    Circle(texture, 32, 22, 8, color, 5);
                    Arc(texture, 32, 46, 18, 205, 335, color, 5);
                    break;
                case "cloud":
                    Arc(texture, 23, 37, 11, 175, 360, color, 5);
                    Arc(texture, 35, 32, 14, 185, 360, color, 5);
                    Arc(texture, 46, 38, 9, 200, 360, color, 5);
                    Line(texture, 18, 39, 50, 39, color, 5);
                    break;
                case "shield":
                    Poly(texture, color, 4, new Vector2Int(32, 12), new Vector2Int(48, 19), new Vector2Int(44, 43), new Vector2Int(32, 52), new Vector2Int(20, 43), new Vector2Int(16, 19), new Vector2Int(32, 12));
                    Line(texture, 26, 32, 31, 37, color, 4);
                    Line(texture, 31, 37, 40, 27, color, 4);
                    break;
                case "refresh":
                    Arc(texture, 32, 32, 19, 35, 310, color, 5);
                    Line(texture, 48, 17, 51, 31, color, 5);
                    Line(texture, 48, 17, 35, 20, color, 5);
                    Line(texture, 16, 47, 13, 33, color, 5);
                    Line(texture, 16, 47, 29, 44, color, 5);
                    break;
                case "logout":
                    Rect(texture, 16, 16, 21, 32, color, 4);
                    Line(texture, 31, 32, 51, 32, color, 5);
                    Line(texture, 43, 23, 52, 32, color, 5);
                    Line(texture, 43, 41, 52, 32, color, 5);
                    break;
                case "cap":
                    Poly(texture, color, 4, new Vector2Int(12, 28), new Vector2Int(32, 17), new Vector2Int(52, 28), new Vector2Int(32, 39), new Vector2Int(12, 28));
                    Line(texture, 22, 36, 22, 45, color, 4);
                    Line(texture, 42, 36, 42, 45, color, 4);
                    Line(texture, 22, 45, 42, 45, color, 4);
                    Line(texture, 51, 29, 51, 43, color, 3);
                    Circle(texture, 51, 47, 3, color, 3);
                    break;
                case "trophy":
                    Rect(texture, 23, 16, 18, 20, color, 4);
                    Arc(texture, 20, 24, 8, 95, 270, color, 4);
                    Arc(texture, 44, 24, 8, 270, 445, color, 4);
                    Line(texture, 32, 36, 32, 48, color, 4);
                    Line(texture, 22, 50, 42, 50, color, 4);
                    break;
                case "keyboard":
                    Rect(texture, 13, 20, 38, 26, color, 4);
                    for (var y = 28; y <= 38; y += 10)
                    {
                        for (var x = 21; x <= 43; x += 11)
                            Rect(texture, x, y, 3, 3, color, 2);
                    }
                    break;
                case "gear":
                    Circle(texture, 32, 32, 10, color, 5);
                    Circle(texture, 32, 32, 3, color, 3);
                    for (var i = 0; i < 8; i++)
                    {
                        var a = Mathf.Deg2Rad * i * 45f;
                        Line(texture, 32 + Mathf.RoundToInt(Mathf.Cos(a) * 15), 32 + Mathf.RoundToInt(Mathf.Sin(a) * 15), 32 + Mathf.RoundToInt(Mathf.Cos(a) * 22), 32 + Mathf.RoundToInt(Mathf.Sin(a) * 22), color, 5);
                    }
                    break;
                case "power":
                    Arc(texture, 32, 34, 18, 35, 325, color, 5);
                    Line(texture, 32, 13, 32, 32, color, 5);
                    break;
                case "close":
                    Line(texture, 20, 20, 44, 44, color, 5);
                    Line(texture, 44, 20, 20, 44, color, 5);
                    break;
                default:
                    Circle(texture, 32, 32, 18, color, 5);
                    break;
            }
        }

        static void Set(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size)
                return;
            texture.SetPixel(x, y, color);
        }

        static void Dot(Texture2D texture, int x, int y, Color color, int radius)
        {
            for (var oy = -radius; oy <= radius; oy++)
            for (var ox = -radius; ox <= radius; ox++)
            {
                if (ox * ox + oy * oy <= radius * radius)
                    Set(texture, x + ox, y + oy, color);
            }
        }

        static void Line(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int width)
        {
            var dx = Mathf.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Mathf.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;

            while (true)
            {
                Dot(texture, x0, y0, color, Mathf.Max(1, width / 2));
                if (x0 == x1 && y0 == y1)
                    break;
                var e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        static void Rect(Texture2D texture, int x, int y, int w, int h, Color color, int width)
        {
            Line(texture, x, y, x + w, y, color, width);
            Line(texture, x + w, y, x + w, y + h, color, width);
            Line(texture, x + w, y + h, x, y + h, color, width);
            Line(texture, x, y + h, x, y, color, width);
        }

        static void Circle(Texture2D texture, int cx, int cy, int r, Color color, int width)
        {
            Arc(texture, cx, cy, r, 0, 360, color, width);
        }

        static void Arc(Texture2D texture, int cx, int cy, int r, int startDeg, int endDeg, Color color, int width)
        {
            var previous = Vector2Int.zero;
            var hasPrevious = false;
            for (var d = startDeg; d <= endDeg; d += 4)
            {
                var rad = Mathf.Deg2Rad * d;
                var point = new Vector2Int(cx + Mathf.RoundToInt(Mathf.Cos(rad) * r), cy + Mathf.RoundToInt(Mathf.Sin(rad) * r));
                if (hasPrevious)
                    Line(texture, previous.x, previous.y, point.x, point.y, color, width);
                previous = point;
                hasPrevious = true;
            }
        }

        static void Poly(Texture2D texture, Color color, int width, params Vector2Int[] points)
        {
            for (var i = 1; i < points.Length; i++)
                Line(texture, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y, color, width);
        }

        static void FillTriangle(Texture2D texture, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
        {
            var minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            var maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            var minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            var maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var p = new Vector2Int(x, y);
                if (SameSide(p, a, b, c) && SameSide(p, b, a, c) && SameSide(p, c, a, b))
                    Set(texture, x, y, color);
            }
        }

        static bool SameSide(Vector2Int p1, Vector2Int p2, Vector2Int a, Vector2Int b)
        {
            var cp1 = Cross(b - a, p1 - a);
            var cp2 = Cross(b - a, p2 - a);
            return cp1 * cp2 >= 0;
        }

        static int Cross(Vector2Int a, Vector2Int b) => a.x * b.y - a.y * b.x;
    }
}
