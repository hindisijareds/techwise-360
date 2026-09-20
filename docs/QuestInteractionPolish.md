# Quest interaction polish — 14 September 2026

This update addresses the latest headset feedback without changing the assembly sequence, scoring, menus outside the requested pause controls, or component placement rules.

- The wrist Pause / Settings shortcut has a **Hide** button. The left controller Menu shortcut continues to open the pause menu. **Settings → Show wrist shortcut** restores it. Visibility is retained for the current app session.
- Pausing temporarily disables near component casting and world interaction layers on the existing hand interactors. This prevents a nearby component hover from suppressing `NearFarInteractor.UpdateUIModel`. Controller tracking, far UI casting, and the XR UI input module remain active. Original interaction settings restore on Resume; installed socket ownership remains intact.
- Component names sit 18 mm above the model's actual upper surface, centered in X/Z. Camera-dependent stacking has been removed. Names hide while held and return on release. All registered assembly parts, the two tools, and the removable side panel receive names.
- Old assembly-table signs are hidden as complete placards, including both Image and RawImage backgrounds and multiline GPU/PSU headings. They remain hidden if an old scene script tries to reactivate them, and restore on assembly teardown. Separate Knowledge Corner panels retain their existing behavior.
- Component outlines use oriented mesh bounds, exclude text/other interactables, and add only 3 mm total padding. They hide during a hand grab and return after release. Placement targets remain available to guide release.
- Fan target arrows lie horizontally inside the case and terminate at the actual socket centers. Their directions follow the rear/front mount orientation. Existing through-case target rendering remains enabled.
- While valid screwdriver contact and trigger input tighten a screw, a small magnified contact panel appears. It hides on release/pause. The player's tracked camera pose and field of view never change. A 384×384 render is updated at most 20 times per second only while driving; no new interaction or collision system is introduced.
- The screwdriver uses grey steel and a striped yellow handle. Its visible model rotates by the same progress delta as the active screw while the grip, collider, and tip pose remain stable.
- Cooler fasteners retain their seated head positions and gain individual shaft lengths extending through the cooler bracket into the motherboard. M.2 and motherboard screw geometry is unchanged.

Build entry point: `TechWiseInteractionPolishBuild.BuildQuest`. APK: `Builds/InteractionPolish/TechWise360.apk`. Prior APKs are retained. Test/build records are saved under `Logs/InteractionPolish`. Physical Quest ergonomics, stereo appearance and frame rate still need headset verification. No sideload has been performed for this update.

## Verification

The full Practice/Tutorial assembly suite passed **610 checks with zero core runtime errors**. New checks cover name anchoring, grip visibility, compact mesh outlines, removed placards, screw shaft length, driver rotation without moving the grip/tip, grey/yellow materials, magnifier rendering and hiding, and horizontal fan arrows aimed at their exact mount positions. Both complete assembly sequences passed. A regression uncovered an order-dependent GPU connector registration: it now resolves the existing `GPU_Bracket1` socket explicitly, avoiding unrelated empty-key demo sockets.

The pause/settings/reset suite passed **63 checks with zero core runtime errors**. It exercises Hide and Show, checks the existing NearFarInteractor produces UI ray points at timeScale zero, checks world targeting is disabled while paused and restored on Resume, uses tracked UI ray hits to click Settings/Resume/Reset, and retains desktop, Tutorial, disassembly reset and Competition protection checks.

Results: `assembly-results.txt` and `pause-results.txt` in `Logs/InteractionPolish`. The existing XR Toolkit registration-capacity diagnostics during Editor scene reloads remain recorded separately. Visual evidence: `component-labels.png`, `cooler-screws-tight.png`, and `screw-detail.png` in the same directory. Automated tests do not substitute for controller ergonomics and stereo checks on the headset.

## APK

Android build succeeded in 5m 32s with **0 errors and 59 warnings**. APK v2 signature verification passed with the existing Android debug certificate. Package: `com.DefaultCompany.VRMultiplayer`; version `0.0.1` / code 1; minimum SDK 30; target SDK 32; native ABI `arm64-v8a`; IL2CPP development build. Size: **407,808,093 bytes**. SHA256: `0F3C5FB9FE86A351A7E3C227B6EFC426D7548B45ACCCED12EC99798E5D94513A`. Signature, metadata, hash, and build summary are saved under `Logs/InteractionPolish`.
