# Quest pause menu and Practice reset

In a gameplay scene, press the left Quest controller's Menu button to open or close the menu. The world-space controls board also has a Menu / Pause button. Point either controller ray at a menu button and press its existing UI trigger. On desktop, Escape and the existing Menu button remain available.

The menu reuses the existing desktop menu, Settings information, Controls, Sync, navigation, and account actions. In VR it is positioned in front of the player each time it opens. Settings and Controls expand into a readable panel with a Back button. Menu UI uses the installed XR UI input module and TrackedDeviceGraphicRaycaster; it does not introduce another ray or grab system.

Pause suspends scaled simulation time, locomotion, and assembly manipulation while preserving headset tracking and UI input. Held selections are canceled without recording a placement or mistake. Existing socket ownership is preserved. Resume restores the previous time scale and only the providers that were enabled before pausing. Competition elapsed time remains based on real time, consistent with the existing desktop competition policy.

Reset Practice reloads the current practice scene using its existing initialization. Assembly starts with a fresh assembly, including CPU locks, paste, fasteners, RAM, fans, case preparation, tools, mistakes, and progress. Disassembly runs its existing PC preparation again. The activity type, control preferences, account, and previously saved competition records are retained. Reset is hidden and guarded in Tutorial and Competition.

Verification entry point: `TechWisePauseResetVerification.Run`. Results, screenshots, and Unity logs are written to `Logs/PauseReset`. This is simulated Editor verification; it does not establish physical Quest controller behavior.

Verification on 13 September 2026 passed 44 checks with zero application runtime errors. Checks covered the left Menu press edge, held-button debouncing, both tracked UI ray positions, Settings/Controls/Back/Resume, frozen simulation time and locomotion, canceled grabs without mistakes, retained socket ownership, repeated assembly resets, disassembly reconstruction, Tutorial pause/resume, desktop access, Competition reset protection, and returning to Main Menu. The installed XR Toolkit still emits its existing UI Toolkit registration-capacity errors during repeated Editor scene reloads (75 messages plus their slot diagnostics in this run). These remain in the log; no package or XR input-system rewrite was made as part of this change.

Build entry point: `TechWisePauseResetVerification.BuildQuest`. Output: `Builds/QuestPauseReset/TechWise360.apk`, Android ARM64 / IL2CPP. The build helper restores project settings after the build. No sideloading is performed for this change.

The APK build succeeded with zero errors and 55 warnings. Android SDK tools verified its v2 APK signature and `arm64-v8a` contents. Package: `com.DefaultCompany.VRMultiplayer`, version `0.0.1` (code 1), minimum SDK 30, target SDK 32. The development APK uses the existing Android debug signer. Full metadata and SHA256 are saved in `Logs/PauseReset/apk-verification.txt`.
