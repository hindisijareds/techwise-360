# Repository architecture — Batch 1 inspection (2026-09-06)

## Unity

- Unity 6000.4.8f1, URP 17.4, XR Interaction Toolkit 3.4.1, OpenXR 1.16.1, Input System. Installed editor: `D:/UnityEditor/6000.4.8f1/Editor/Unity.exe`; Windows and Android modules present.
- Enabled build scenes: MainMenu, Singleplayer, Multiplayer. MainMenu routes **tutorial/lesson** to Singleplayer and **practice/competition** to Multiplayer. The names are historical: the local-mode runtime disables network/lobby onboarding in both scenes.
- `Assets/Script` is the default game assembly. `Assets/VRMPAssets` has its own VRMP asmdef; shared key/socket helpers cannot depend on the default game assembly. Existing Netcode, Relay, Lobby, Vivox and avatar assets remain.
- `MainMenu` stores activity/mode/control selection in PlayerPrefs. `TechWisePracticeModeRuntime` disables online onboarding and handles player bounds. `TechWiseDesktopController` drives an XRI ray from actual mouse/keyboard input. Quest input uses existing OpenXR/XRI action assets and rig.
- Existing keys on components and `XRLockSocketInteractor` targets determine compatibility. Motherboard, CPU, cooler, four RAM parts/slots, M.2, GPU, SATA storage and PSU are authored in the practice scene. Knowledge Corner duplicates are learning displays, not assessment parts.
- Existing instructional sequence: CPU, RAM, M.2, cooler, motherboard, GPU, storage, PSU; reverse servicing curriculum: cooler, PSU, storage, GPU, M.2, RAM, CPU, motherboard. Preserve this authored sequence rather than inventing a new one. Cable/thermal-paste/latch substeps require further explicit state coverage.
- Existing guide is primarily blackboard text. Batch 1 adds a separate side guide and shared live state/filter layer; existing scene assets and menu UI are retained.
- Android settings inspected: ARM64, minimum SDK 30, target SDK 32, OpenXR loader configured, Input System enabled. SDK compatibility/store-policy changes are not part of this batch. No production identifier change.

## Web, API and data

- Static HTML/CSS and browser JavaScript (`WebPortal/app.js`), without React/Next or a separate bundler. Existing `npm run check` checks JavaScript syntax. Playwright capture scripts exist; they are not a full assertion suite.
- Cloudflare Pages Functions (`WebPortal/functions/api`) provide login/refresh, registration, teacher/student dashboards, lesson files, assessments, VR competitions, attempt submission, reports/completion readers and sections.
- Supabase Auth, Postgres and private lesson/avatar storage. Server helpers use authenticated profiles and roles; privileged Supabase configuration belongs in server environment variables. Unity currently logs into the same portal API separately; no secure browser-to-Unity token handoff was found.
- Schema contains profiles, quarters (`school_year` text), sections (`school_year` text), section assignment history, lessons/modules/files/progress, assessments and attempts, VR competitions/attempts, badges and evaluation records. Separate relational academic years and year/term enrollments remain future-batch work.
- `student/vr-attempts` derives `student_id` from the authenticated profile. It still trusts numeric client score inputs and inserts on each POST; a client attempt ID alone does not provide server idempotency.
- Unity has structured mistake metadata, a portal client, session store, and a disk pending-attempt queue with retries. Retained for Batch 1; shared identity, account isolation, secure storage, server integrity and retry/idempotency acceptance remain on the master checklist.

## Deployment and existing work

- Existing Pages project: `techwise360-web-portal`; package scripts deploy the static directory to its `production` Pages branch. No second deployment/project added. Deployment is deferred by the controlled batch request.
- Git branch `main`, existing GitHub `origin`. Substantial pre-existing uncommitted scene, Unity, website and migration work was present. Batch changes must preserve it and must not indiscriminately stage the working tree.
- No applicable AGENTS.md found in the repository. Generated Library, Builds, Logs and local assistant caches are ignored.
- Read-only schema/API inspection is architectural evidence; it is not a claim of production database, auth, reporting, or end-to-end verification.

## Batch 3 integration update (2026-09-07)

The inspection above records the original architecture. Batch 3 extends the same Supabase provider and Pages Functions with website-code exchange, scoped in-memory Unity sessions, server-registered assessment/enrollment snapshots, validated/idempotent writes to the existing VR results table, and owner-bound pending sync. The existing teacher Reports page now reads those records with context filters and CSV export. Academic-year/enrollment database foundations are implemented; their broader management interfaces remain later work. See BATCH_3_REPORT.md for files, migration order, test evidence and rollout limits. No live deployment or database migration has been performed.
