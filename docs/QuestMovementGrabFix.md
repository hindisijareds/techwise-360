# Quest movement and grab recovery — 2026-09-15

The project had `m_TimeScale: 0` saved in `ProjectSettings/TimeManager.asset`. The edit-mode menu diagnostic opened a pause session without disposing it, allowing the paused time setting to persist into subsequent builds. A zero time scale stops joystick translation and fixed physics updates even when controller tracking and menu UI still respond.

Changes:

- Restore the saved time scale to 1 and initialize new game sessions with normal simulation timing.
- Pause sessions modify time only during play and dispose only once. The editor diagnostic now closes its menu in `finally`, restoring interactor layers and providers even if a probe fails.
- All build entry points reject a paused runtime or saved TimeManager setting.
- The rig stabilizer registers only active UI rays/poke interactors. Disabled teleport rays no longer acquire stale UI registration slots.
- The first headset launch exposed a hidden teleport callout starting a coroutine from gaze callbacks. Inactive/disabled callouts now reject those callbacks safely and clear their gaze state.
- Preserve existing tutorial restrictions, competition locks, pause filtering, component rules, scoring, and assembly content.

Validation:

- 69 pause/reset checks passed, including startup scaled/fixed time, nonzero configured joystick movement, XR selection before pause, blocked selection during pause, restored selection and locomotion after Resume, settings, resets, tutorial resume, competition restrictions, main-menu return, and inactive/disabled gaze callouts.
- 610 detailed assembly checks passed across Practice and Tutorial.
- Zero core runtime errors. The remaining preexisting UI Toolkit capacity diagnostics occur during editor scene transitions: 54 messages in pause/reset and 9 in assembly verification. They are retained in the logs; this is not a claim that the entire project is error-free.
- These automated checks do not substitute for wearing the Quest and physically operating the controllers.

Evidence is under `Logs/MovementGrabFix`. The updated APK uses the existing output path `Builds/InteractionPolish/TechWise360.apk`.

Final delivery: ARM64 build succeeded (0 errors, 62 warnings). APK v2 signature verified against the existing certificate. SHA256: `409A6F43563E4A7FE0D6B48C60B2BA64ABF497FD89B347219C302D85C1387B94`.

`adb install -r` returned `Success` on Quest 2 `1WMHHA40KD2125`; package update time was `2026-09-15 16:00:39`. The replacement app launched and reached a focused OpenXR session. The captured final startup log contained no Unity errors, fatal exceptions, or registration-capacity errors. This launch smoke check does not verify physical grip/joystick operation throughout gameplay.
