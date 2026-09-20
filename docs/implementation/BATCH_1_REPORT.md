# Batch 1 — core Unity simulation

Status: **CORE CODE IMPLEMENTED; MANUAL VERIFICATION HANDED TO USER; QUEST PACKAGING BLOCKED BY AVAILABLE MEMORY**. The latest user instruction stops automated gameplay testing. Do not start Batch 2 automatically.

## Scope and preserved work

The authoritative source is `MASTER_REQUIREMENTS.md`; `CHECKLIST.md` tracks all phases and `requirements-ledger.json` retains every source requirement/example line with its own status. The existing tutorial uses Singleplayer; practice and competition use Multiplayer. No Batch 2 website, database, identity, or deployment changes were made.

Substantial Unity, scene, portal and migration edits already existed when this work began. They remain intact. This report lists only files changed by this batch, rather than attributing the entire dirty Git working tree to this work.

## Implemented changes for user verification

- Shared XRI placement validation for desktop and VR: key compatibility, single ownership, occupancy, orientation and supporting-component checks. Optional per-socket fastening requirements provide the future screw integration point.
- Exclude Knowledge Corner display objects from assembly progress and assessment validation. Distinguish the GPU connector key from the actual GPU slot key.
- Retain the actual loose motherboard, component sockets and installed parts in practice/assessment. Disable tutorial socket callbacks that swap those objects for a prebuilt model or advance tutorial panels. Preserve the tutorial scene's original sequence behavior.
- Require all four authored RAM sticks for the RAM step; validate live socket ownership rather than treating one successful snap as permanent completion.
- Prepare disassembly through validated socket selection, align component attachment transforms, report failed setup, and remove unconditional selection/success fallbacks.
- Fix local grab conflicts with networking ownership and parent synchronization. Restore collider trigger state after release. Extend desktop ray interaction layers to cover authored component layers; prevent mouse-look from fighting right-drag rotation and movement while the cursor is unlocked.
- Add a practice side panel with current/completed/upcoming steps, immediate release feedback and a scrollable structured mistake review. Practice results remain local learning feedback. Add a pulsing component outline and target arrow; neither appears in competition.
- Label the practice blackboard “Practice Mode” with off-white italic styling using existing fonts; correct the GPU instruction's PCIe terminology and desktop hold/release/rotation-axis text.
- Make DONE submit the current assembly/disassembly, including incomplete work. Validate omissions, stop the monotonic timer, lock manipulation, and reveal the existing results UI. All-parts placement no longer automatically finishes an assessment. Suppress correctness/progress disclosure before submission.
- Preserve the existing mistake/time scoring model and cap scores by actual final completion. Reuse the existing result payload and offline queue; server idempotency and cross-account queue security are future-batch work.
- Avoid duplicate fallback/scene EventSystems and fix teardown event subscriptions that failed when networking had been disabled for local play.
- Place practice guidance on the right, keeping the existing left-side controls guide readable. Build helpers restore preloaded assets as well as release version metadata after packaging.

## Files changed by this batch

New files (plus their Unity `.meta` files):

- `Assets/Script/TechWiseSimulationRuntime.cs`
- `Assets/Script/TechWisePlacementRule.cs`
- `Assets/Script/TechWisePracticeGuidePanel.cs`
- `Assets/Editor/TechWiseBatch1Verification.cs`
- `Assets/Editor/TechWiseBatch1Build.cs`
- `docs/implementation/MASTER_REQUIREMENTS.md`
- `docs/implementation/requirements-ledger.json`
- `docs/implementation/CHECKLIST.md`
- `docs/implementation/architecture.md`
- `docs/implementation/BATCH_1_REPORT.md`

Modified existing files:

- `Assets/Script/TechWiseSimulationModeManager.cs`
- `Assets/Script/TechWisePracticeModeRuntime.cs`
- `Assets/Script/TechWiseDisassemblyRuntime.cs`
- `Assets/Script/TechWiseComponentRecovery.cs`
- `Assets/Script/TechWiseDesktopController.cs`
- `Assets/Script/TechWiseGuideAssistant.cs` (already had user changes)
- `Assets/Script/TechWiseInGameMenu.cs` (already had user changes)
- `Assets/Script/TechWiseAttemptRecorder.cs` (already had user changes)
- `Assets/VRMPAssets/Scripts/Helpers/XRLockSocketInteractor.cs`
- `Assets/VRMPAssets/Scripts/Helpers/KeyLockSystem/Keychain.cs`
- `Assets/VRMPAssets/Scripts/Helpers/KeyLockSystem/Lock.cs`
- `Assets/VRMPAssets/Scripts/Network/NetworkInteractions/NetworkBaseInteractable.cs`
- `Assets/VRMPAssets/Scripts/Network/NetworkInteractions/NetworkPhysicsInteractable.cs`
- `Assets/VRMPAssets/Scripts/Network/NetworkManagers/XRINetworkGameManager.cs`
- `Assets/VRMPAssets/Scripts/Player/ConnectionToggler.cs`
- `Assets/VRMPAssets/Scripts/UI/PlayerList/PlayerListUI.cs`
- `Assets/VRMPAssets/Scripts/UI/OfflineMenu.cs`

## Verification checkpoint

- Unity 6000.4.8f1 compiled the initial changes and completed scene inventory (`Logs/Batch1-compile.log`, `Logs/Batch1/scene-audit.txt`). Final gameplay verification is assigned to the user under their latest instruction.
- Earlier runs reached 43 assertions before a grouped RAM removal failure. A later diagnostic run showed a released GPU still near its socket. Code review found network physics re-enabling its transform on local release; the final code prevents that. The user stopped further automated tests, so the final correction is **implemented, not runtime-verified**.
- Earlier runs exercised practice assembly, disassembly preparation, occupied-slot rejection, Done omission scoring, timer stop and duplicate Done protection; later changes mean those results are not a substitute for a final full run.
- Windows exhausted virtual memory during testing, including a graphics-enabled capture attempt. Screenshots were removed from the test runner. The user freed memory. Subsequent testing was stopped at the user’s request; any pending-attempt backup was restored.
- `git diff --check` passed for the batch's source paths. Existing line-ending/deprecation warnings are not treated as successful runtime verification.
- No Quest was attached in `adb devices`. Physical controller behavior, headset readability and framerate remain unverified.
- Final Windows development build **succeeded**, with 0 errors and 33 warnings. The logs contain obsolete Unity API warnings. Output: `Builds/Batch1/Desktop/TechWise360.exe`; Unity build duration: 1 minute 59 seconds. This includes the final right-side practice-panel correction. See `Logs/Batch1/build-StandaloneWindows64.txt` and `Logs/Batch1-desktop-build-final.log`. The earlier Windows build also succeeded (82 warnings).
- No automated gameplay test was run after this build, as requested by the user. Build success verifies compilation and packaging only; rendered UI and interaction behavior remain for the user.
- Android/Quest packaging reached player script compilation, Vulkan shader processing, IL2CPP conversion and native C++ compilation. Windows virtual memory fell to approximately 123 MB free and later failed to start PowerShell. The packaging attempt was deliberately stopped to end recurring memory pressure; no final APK or successful Android build is claimed. Existing Android target settings and identifiers were preserved. See `Logs/Batch1-quest-build.log`. This is a resource-blocked packaging attempt, not a reported Unity compiler failure.

## Reproducible commands

Run from the repository root with the installed Unity editor. Do not run two editors on this project at once.

```powershell
& 'D:/UnityEditor/6000.4.8f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/Jared/UnityProject/TechWise360' -executeMethod TechWiseBatch1Verification.RunSimulationTests -logFile 'Logs/Batch1-tests.log'
& 'D:/UnityEditor/6000.4.8f1/Editor/Unity.exe' -batchmode -nographics -buildTarget Win64 -projectPath 'D:/Jared/UnityProject/TechWise360' -executeMethod TechWiseBatch1Build.Desktop -quit -logFile 'Logs/Batch1-desktop-build.log'
& 'D:/UnityEditor/6000.4.8f1/Editor/Unity.exe' -batchmode -nographics -buildTarget Android -projectPath 'D:/Jared/UnityProject/TechWise360' -executeMethod TechWiseBatch1Build.Quest -quit -logFile 'Logs/Batch1-quest-build.log'
```

Test fixtures temporarily isolate the local pending-attempt file and disable automatic synchronization; they do not send test scores to the live backend. Build helpers use existing enabled scenes and development builds, and preserve release version metadata. The optional existing build incrementer is disabled by its compile-time switch.

## Remaining acceptance and boundaries

- User to check grouped RAM removal and all grab/release paths after the final local-network-physics correction; no final automated pass is claimed.
- Inspect actual rendered desktop/VR layout and motherboard alignment in the case; automated state checks do not prove visual correctness.
- Validate manual controller pickup, release, rotation and recovery on Quest, and profile performance on the target device.
- Complete explicit cable/connection and thermal-paste substep validation; this batch currently covers the eight authored component groups and basic supporting dependencies.
- The 3D Quest controller guide is low priority and remains NOT STARTED. Existing binding-derived text guidance is retained. No remapping UI was added.
- Manual screw installation is PENDING ADVISER DECISION. No mandatory manual screw requirement is enabled.
- Web/VR shared identity, server score integrity, offline account isolation, server idempotency, teacher class records, academic-year/enrollment work and deployment remain future batches.
- Git was reviewed: `main` has one pre-existing website commit ahead of `origin/main`, plus substantial pre-existing uncommitted work. No batch commit or push was made; the master instruction places these after successful testing, which the user now owns.
- Build-generated project-setting changes and deleted tracked performance-test resource files were restored. No Unity build process remains running. Keep the complete Desktop output folder together when launching or copying the executable.
