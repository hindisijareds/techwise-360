# Quest 2 headset tracking repair — 2026-09-08

Continued the existing project after inspecting git history (latest ac94749), the dirty working tree, XR rig prefabs, scene inspection reports, desktop-mode isolation, Input System pose-driver source, and the existing Quest build entry point.

## Change

Repaired unfinished overlapping edits in Assets/Script/TechWiseVrRigStabilizer.cs. The active XR origin camera now uses dedicated, locally owned Input System actions for XRHMD centerEyePosition, centerEyeRotation, and trackingState. TrackedPoseDriver updates position and rotation during Update and BeforeRender. Removed competing direct-polling and minimum-world-height logic. Startup/device-relative mode retains CameraYOffset; valid floor tracking uses measured headset height with zero extra offset. A temporary tracking loss retains the previous height convention. Actions are disposed/recreated across scenes. Desktop mode is respected.

## Verification and deployment

- Unity 6000.4.8f1 Android development build through TechWiseBatch1Build.Quest succeeded: 0 errors, 57 warnings, 4m48s.
- Existing Quest menu verification passed.
- APK: Builds/Batch1/Quest/TechWise360.apk (410709707 bytes).
- Installed on connected Quest 2 with adb install --no-streaming -r; result Success. Existing app data retained.
- Package com.DefaultCompany.VRMultiplayer lastUpdateTime: 2026-09-08 01:20:59 (device clock).
- Launched app. Live MainMenu diagnostics showed Floor tracking, state=15, resolved position/rotation controls=1/1, headset height=1.08m and camera height=1.08m; startup had a 1.36m offset before tracking became valid.
- Build log: Logs/Quest2/build-head-tracking.log. Device log: Logs/Quest2/head-tracking-device.log.
- User-visible movement and gameplay scene height confirmation are pending; the headset paused the application shortly after launch. No claim of a completed physical gameplay test.

Pre-existing runtime messages include inactive affordance-callout coroutine and duplicate EventSystem warnings; these are separate from the repaired tracking path.
