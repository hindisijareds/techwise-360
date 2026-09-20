# TechWise360 master implementation checklist

Source of truth: [verbatim master](MASTER_REQUIREMENTS.md). Every nonempty requirement/example line has a stable ID and one allowed status in [requirements-ledger.json](requirements-ledger.json). Individual lines distinguish implemented code from manual/device verification. This table tracks phase scope and evidence.

Allowed statuses: NOT STARTED, IN PROGRESS, IMPLEMENTED, VERIFIED, BLOCKED, PENDING ADVISER DECISION.

Current scope: **Batch 3 — VR authentication, API, database and results**. Continue from Batches 1/2. Local implementation/checks are documented in BATCH_3_REPORT.md; deployment and physical-device acceptance remain. Do not start Batch 4 automatically.

VERIFIED means the stated evidence was inspected/tested, not that all device or end-to-end acceptance passed. IMPLEMENTED may describe existing code that has not been run.

| Phase | Requirement | Status | Evidence / remaining work |
|---|---|---|---|
| 01 | INSPECT THE EXISTING PROJECT | VERIFIED | Inspected project/package/build settings, scenes and scripts, web routes, schema, auth, deployment scripts and Git. See architecture.md. |
| 02 | ACADEMIC YEAR MANAGEMENT | IN PROGRESS | Batch 3 adds relational academic years and backfills existing terms; dedicated year-management UI remains. |
| 03 | TERM MANAGEMENT | IN PROGRESS | Existing term CRUD gains academic-year linkage; full term-management acceptance remains. |
| 04 | REGISTRATION / ENROLLMENT | IN PROGRESS | Formal VR enrollment is created from approved student/current term/section at START; broader registration/enrollment review remains. |
| 05 | REMOVE REDUNDANT FULL NAME FIELD | NOT STARTED | Existing full_name field and registration name handling require focused follow-up. |
| 06 | STUDENT FIELDS | NOT STARTED | Grade 9/10 and registration validation exist; full field review deferred. |
| 07 | SECTION FLEXIBILITY | IMPLEMENTED | Pre-existing uncommitted section CRUD, roster and migrations; no live backend verification in Batch 1. |
| 08 | MODULE DOWNLOAD AND OFFLINE SUPPORT | NOT STARTED | Existing signed lesson file endpoint; download/offline acceptance deferred. |
| 09 | ASSESSMENTS | IMPLEMENTED | Teacher assessment and VR competition endpoints exist; not end-to-end tested. |
| 10 | WEB AUTHENTICATION ↔ VR AUTHENTICATION | IMPLEMENTED | Single-use website code exchanges for scoped VR session; identity/context API tests pass. Live headset/portal handoff remains. |
| 11 | VR SIMULATION ARCHITECTURE | IMPLEMENTED | Existing assembly/disassembly modes extended through shared simulation state. |
| 12 | PRACTICE MODE | IMPLEMENTED | Preserved existing curriculum sequence; side panel uses live component state. |
| 13 | PRACTICE VISUAL GUIDANCE | IMPLEMENTED | Practice-only pulsing component outline and target arrow; user to check rendered readability. |
| 14 | PRACTICE MODE MISTAKES | IMPLEMENTED | Practice release validation, immediate feedback, structured local review. |
| 15 | PRACTICE MODE BLACKBOARD | IN PROGRESS | Existing classroom board now says Practice Mode; dedicated chalk font not bundled. |
| 16 | DESKTOP CONTROL GUIDE | IMPLEMENTED | Existing desktop guide corrected to actual hold/release and rotation axes. |
| 17 | META QUEST VR CONTROL GUIDE | NOT STARTED | Existing text guide reads XRI bindings. 3D controller guide deliberately low priority. |
| 18 | NO CONTROLLER REMAPPING | IMPLEMENTED | No new remapping UI; retain existing desktop/VR mode selector. |
| 19 | ASSESSMENT / COMPETITION MODE | IMPLEMENTED | Existing practice/assessment separation retained; focused no-hint checks in Batch 2. |
| 20 | DONE BUTTON | VERIFIED | START allocates ID; DONE freezes timer, snapshots components, locks input, validates and creates one result. No auto-submit. |
| 21 | ASSESSMENT RESULT SCREEN | IMPLEMENTED | Activity, score, accuracy, elapsed time, deductions/status, View Mistakes, Close/reopen and Return controls. |
| 22 | MISTAKE TRACKING | VERIFIED | Categorized severity, penalty, order and precise timestamps; individual final component omissions including all RAM modules. |
| 23 | COMPONENT VALIDATION | IN PROGRESS | Keys, occupancy, orientation, supporting dependencies and grouped RAM implemented. Explicit cable/thermal-paste substeps remain; user owns final interaction verification. |
| 24 | PRACTICE VS ASSESSMENT BEHAVIOR | IMPLEMENTED | Practice remains local/guided; formal assessment registers server context and feeds existing teacher Reports. Physical acceptance remains. |
| 25 | TIMER | VERIFIED | Dedicated monotonic clock with Start/Submit guards; additive scene notification cannot reset attempt. |
| 26 | SCORING SYSTEM | VERIFIED | Central JSON configuration and pure scoring evaluator; legacy mistake/time default preserved, severity tiers added, per-attempt settings/breakdown saved. |
| 27 | SCREWS | PENDING ADVISER DECISION | Optional per-socket fastening contract; no complex screw physics introduced. |
| 28 | GRAB / DRAG / SNAP BUGS | IMPLEMENTED | Batch 1 fixes retained. Batch 2 fixed self-socket release errors and stale-hover distant reselection; deterministic disassembly regression passed. Physical desktop/Quest interaction remains manual. |
| 29 | PC ASSEMBLY PROCESS QUALITY | IMPLEMENTED | Corrected GPU terminology; retained authorized assets and curriculum flow. |
| 30 | VR RESULT API | IMPLEMENTED | Authenticated registered-attempt result API validates evidence and recomputes score; local DB/API checks pass. Live migration/deployment pending. |
| 31 | TEACHER CLASS RECORD | IMPLEMENTED | VR results added to existing Reports/class-record area, with historical context and structured mistake review; focused browser tests pass. |
| 32 | REPORT GENERATION | IMPLEMENTED | Year/term/grade/section/student/assessment/activity/date filters and all-pages CSV export in existing Reports. Live acceptance and later analytics remain. |
| 33 | WEAK AREA ANALYTICS | NOT STARTED | Structured VR mistakes available; analytics integration deferred. |
| 34 | OFFLINE / FAILED SUBMISSION SAFETY | IMPLEMENTED | Owner/portal-bound durable queue and explicit receipts; 15 isolated Windows queue/API assertions pass. Android durability and live disconnect remain. |
| 35 | DUPLICATE SUBMISSION PROTECTION | VERIFIED | Unique assessment_session_id plus locked insert RPC; concurrent first submissions/retries produce one immutable result in local PostgreSQL tests. |
| 36 | UI/UX | IN PROGRESS | Only Unity guidance/control changes in this batch; website untouched. |
| 37 | VALIDATION AND ERROR HANDLING | IN PROGRESS | Unity setup failure feedback added; form/network cases deferred. |
| 38 | DATABASE MIGRATIONS | IMPLEMENTED | Additive 20260906_vr_integration.sql tested twice in local PostgreSQL. No live DB mutation; existing legacy results retained without invented context. |
| 39 | SECURITY | IN PROGRESS | Batch 3 scoped hashed credentials, HTTPS/session origin binding, role/ownership and server score validation checked; live security configuration remains. |
| 40 | UNITY CODE QUALITY | IMPLEMENTED | Separate session store, registration service, API client and pending queue; gameplay keeps shared validation/scoring. Unity compile passed. |
| 41 | UNITY PERFORMANCE / META QUEST | IN PROGRESS | URP/OpenXR inspected; few line renderers for hints, no per-mesh material clones. Headset profiling pending. |
| 42 | DESKTOP MODE | IMPLEMENTED | Both inputs share XRI select filters and socket validation. |
| 43 | TESTING | IN PROGRESS | Batch 3: 36 database/API, 15 isolated Unity and 9 focused browser checks passed. See BATCH_3_REPORT.md for regression and live/device limits. |
| 44 | BUILD THE WEB APPLICATION | NOT STARTED | Static web deployment with npm check; no separate bundler build. Deferred. |
| 45 | BUILD THE UNITY VR APPLICATION | BLOCKED | Windows development build succeeded; final layout refresh recorded in BATCH_1_REPORT.md. Quest packaging reached native compilation but was stopped after recurring Windows virtual-memory exhaustion. No APK claimed. |
| 46 | CLOUDFLARE DEPLOYMENT | NOT STARTED | Existing Cloudflare Pages project/config inspected; deployment deferred by batch instruction. |
| 47 | GIT | NOT STARTED | main/origin reviewed. Existing dirty work and local website commit preserved; commit/push remain pending user gameplay verification. |
| 48 | DOCUMENT WHAT YOU CHANGED | IMPLEMENTED | Master source/line IDs retained; Batch 1/2 reports preserved and Batch 3 request/report/checkpoints added. |

## Additional unnumbered requirements

- Practice side step panel: IMPLEMENTED — desktop side UI and fixed world-space VR UI; completed/current/upcoming steps.
- Priority order and definition of done: NOT STARTED as a complete cross-system demonstration. Batch 1 only addresses its Unity subset.
- Preserve existing work and avoid unrelated redesign/deployment: VERIFIED by scoped diff review: no website/API/database or deployment code was changed by this batch.
- Manual screws: PENDING ADVISER DECISION; hook exists, no manual screw dependency enabled.
