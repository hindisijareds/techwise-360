# Batch 3 — VR authentication, database and results

Implementation checkpoint: 2026-09-07. Continues Batches 1/2; no Batch 4 work, live migration, deployment, commit, or new player build.

## Authentication and identity

The existing portal uses Supabase Auth, bearer access tokens, refresh tokens and approved profile roles. Browser sessions are stored under `techwise360.session` in localStorage, not authentication cookies. Existing teacher and student login endpoints remain the identity provider.

A signed-in student opens **Connect VR Simulation** from the student dashboard. `/api/vr/connect` issues a random 256-bit, single-use code valid for two minutes; only its SHA-256 hash is stored. The student pastes the code into Unity's username/code field and selects **Connect Code**. An empty field opens the connection page. `/api/vr/exchange` atomically consumes the code and issues a scoped eight-hour VR bearer session. It is usable only by the VR context/start/result APIs. Existing Unity password login remains a fallback.

Unity holds credentials in memory, removes legacy plaintext credential preferences, requires HTTPS, binds its session to the selected portal, refreshes existing Supabase sessions when needed, and rejects stale responses after portal/account changes. Scoped VR credentials have no refresh token: reconnect after expiry or restarting the app. No credentials are placed in URLs, logs or queued payloads. Codes and scoped credentials are hashed in the database.

## Assessment and result flow

1. START prepares a GUID and registers it with the authenticated API before gameplay or the timer starts. A lost response can be retried with the same ID in the current scene.
2. PostgreSQL resolves the approved student, active term, matching active section, academic year and enrollment. Assigned competition status, time window, activity, grade/section and attempt limit are enforced. The registered context and scoring configuration are immutable snapshots.
3. The existing Batch 2 clock/scoring/DONE validation remains responsible for gameplay evidence. DONE freezes the result and copies it into the local queue with the original student ID and portal origin.
4. The API verifies attempt ownership, timing, the 11-component manifest, known mistake kinds, and final missing-component explanations. It recomputes severity penalties, completion cap, accuracy, time deductions and score using the registered rules.
5. A locked database function inserts into the existing `vr_simulation_attempts` table. A unique `assessment_session_id` constraint ensures concurrent retries return the original result. Replayed payloads cannot overwrite a completed result.
6. Unity displays **Synced** only after an explicit matching attempt/student acknowledgement. Teacher Reports reads this same result table; no manual score entry or parallel class-record database was added.

The server checks identity, context and score consistency. It cannot attest that a modified client actually performed the reported physical actions. This batch does not claim anti-cheat attestation.

## Database migration and rollout

New file: `WebPortal/supabase/migrations/20260906_vr_integration.sql`.

- Adds `academic_years`, `student_enrollments`, `vr_launch_codes`, `vr_device_sessions`, `vr_assessment_sessions`.
- Backfills academic years from existing term `school_year` values; a term trigger maintains the relationship.
- Adds attempt/year/term/enrollment/section relationships, historical name/grade/section snapshots, accuracy and received timestamp to existing VR results.
- Adds unique result-per-session and result-context indexes.
- Adds privileged RPCs `issue_vr_code`, `exchange_vr_code`, `start_vr_assessment`, `complete_vr_assessment`.
- Enables RLS on new tables, denies anonymous/authenticated direct writes and RPC execution, and permits the existing server service role.
- Preserves existing result rows. Unknown historical context remains null and is shown as legacy/unassigned, rather than fabricating enrollments. Legacy results count toward assigned competition attempt limits.

Apply to a reviewed database containing the existing schema and managed-section migration. If the pre-existing legacy-section cleanup is needed, its own instructions require running `20260822_teacher_sections.sql` before `20260822130000_cleanup_legacy_sections.sql`; do not infer that order from filename sorting. This batch does not rerun that cleanup automatically.

Rollout order: apply the new additive migration, deploy the matching Pages Functions/static files, then distribute an updated Unity player. Keep the existing server environment variables `SUPABASE_URL`, `SUPABASE_ANON_KEY`, and `SUPABASE_SERVICE_ROLE_KEY`; no new permanent client secret is required. Existing pre-Batch-3 players cannot register formal attempts with this protocol and should be updated together with the endpoint. The old insecure insert-only submission protocol is intentionally not accepted.

**No live database was migrated and no portal was deployed during this batch.** The older Batch 1 executable does not contain this integration.

## APIs

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/vr/connect` | Approved website student obtains a single-use code; five issuances per five minutes |
| POST | `/api/vr/exchange` | Consume code, return scoped session and student context |
| GET | `/api/vr/context` | Approved student identity, academic context and currently assigned competitions |
| POST | `/api/vr/start` | Register/retry the prepared attempt ID and return immutable context/rules |
| POST | `/api/student/vr-attempts` | Validate and idempotently store completed result; explicit sync receipt |
| GET | `/api/teacher/vr-results` | Approved teacher results, filters, options and 200-row pagination |

All new VR responses use `Cache-Control: no-store`. JSON request bodies are limited to 128 KiB. Server failures do not return database internals or a success receipt.

## Pending sync

The queue persists a copied result before transmission, uses a flushed temporary file and atomic replacement with backup, and retries every 30 seconds or through the existing Sync action. Entries retain their attempt ID, original student, portal, retry count and error. Only the matching signed-in student/portal may upload an entry. An expired session requires reconnecting that same student.

Explicit acknowledgement IDs are persisted separately from pending entries; absence from the queue alone no longer means success. A lost response retries the same server attempt. Local write errors remain visible and ask the student to keep the app open; unreadable queue files are preserved. Legacy entries without captured ownership remain queued for teacher/support review and are never attributed to whoever logs in next.

An internet connection is required to START a formal assessment. Once registered, a completed result can wait offline for later submission, including after the assessment window closes. Abrupt app termination before DONE is not an auto-submission/recovery feature. The pending JSON contains results and student identifiers in the OS application-data directory, not login credentials; it is not an encrypted credential vault.

## Class record and reports

The existing teacher Reports page now contains **Class Record — VR Assessments**, next to the existing completion monitor. It displays student, activity, score, accuracy, elapsed seconds, mistakes with expandable descriptions, completion date, academic year, term, grade, section and assessment.

The existing report grade/section/date controls also reload VR results. Additional year, term, section enrollment, student, assessment and activity controls use IDs. Historical results use the context captured at START, including when the student's current grade/name changes. CSV export walks all filtered pages and escapes spreadsheet-formula prefixes. Existing lesson report metrics/PDF layouts remain intact; VR is available through this table and its CSV export, not folded into the unrelated lesson-completion averages. Cross-activity weak-area analytics remain a later master phase.

## Files changed in this batch

Unity:

- `Assets/Script/MainMenu.cs`
- `Assets/Script/TechWiseSessionStore.cs`
- `Assets/Script/TechWisePortalClient.cs`
- `Assets/Script/TechWiseAssessmentSessionService.cs` + `.meta` (new)
- `Assets/Script/TechWiseAssessmentClock.cs`
- `Assets/Script/TechWiseAttemptRecorder.cs`
- `Assets/Script/TechWiseOfflineAttemptQueue.cs`
- `Assets/Editor/TechWiseBatch2Verification.cs` (explicit editor-only local verification mode)
- `Assets/Editor/TechWiseBatch3Verification.cs` + `.meta` (new)

Portal/backend:

- `WebPortal/functions/api/_vr.js` (new)
- `WebPortal/functions/api/_utils.js` (existing result serializer extended)
- `WebPortal/functions/api/vr/connect.js`, `exchange.js`, `context.js`, `start.js` (new)
- `WebPortal/functions/api/student/vr-attempts.js`
- `WebPortal/functions/api/teacher/vr-results.js` (new)
- `WebPortal/supabase/migrations/20260906_vr_integration.sql` (new)
- `WebPortal/app.js` (existing report-filter event)
- `WebPortal/student-dashboard.html`, `teacher-dashboard.html`
- `WebPortal/vr-connect.html`, `vr-integration.js`, `vr-integration.css` (new)
- `WebPortal/package.json`, `package-lock.json` (local PostgreSQL test dependency and test commands)
- `WebPortal/scripts/test-vr-integration.mjs`, `test-vr-ui.mjs` (new)
- `WebPortal/scripts/fixtures/vr/assembly-result.json`, `disassembly-result.json` (actual isolated Batch 2 test payloads)

Tracking: `BATCH_3_REQUIREMENTS.md`, this report, `CHECKLIST.md`, `requirements-ledger.json`, and appended architecture notes. Other pre-existing dirty files were preserved; their changes are not attributed to Batch 3.

## Validation and limits

- **36 database/API assertions passed:** actual migration applied twice in PGlite PostgreSQL with a minimal existing-schema fixture; actual route handlers called through a local Supabase Auth/PostgREST adapter. Includes launch-code replay/expiry, ownership spoofing, score tampering, failed storage/retry, concurrent first submissions/retries, immutable results, historical report filtering, roles, attempt limits, expiry, RPC privilege denial, and actual Batch 2 Unity payload compatibility.
- **15 isolated Unity queue/API assertions passed:** Unity 6000.4.8f1 compiled the scripts and ran `TechWiseBatch3Verification.Run`, exiting 0. Covers registered clock ID, persistence/reload, immutable queued copy, failed request, account isolation, malformed/wrong-ID acknowledgement, successful receipt, local deduplication, legacy ownership, corrupt-file preservation, invalid expiry and no PlayerPrefs credential storage. Transport is mocked and queue files are isolated under `Logs/Batch3`.
- **9 focused browser assertions passed:** Chrome with Playwright, actual connection/report HTML and integration module, mocked API responses. Includes code expiry, existing report filter event, safe text rendering and complete paginated CSV export. Unrelated dashboard bootstrap APIs are excluded; this is not a live teacher-login test.
- Existing `npm run check` passed. A screenshot of the focused report table was inspected.
- **17 core and 84 existing simulation assertions passed again** after the registration/queue changes. The local editor verification mode bypasses live registration while exercising the existing practice, assembly/disassembly, START/DONE, frozen result and guidance-separation behavior. The run still logs duplicate tutorial `SceneObjectGuid` exceptions, XR UI Toolkit interactor-capacity errors and unresolved simulator/OpenXR infrastructure issues. Passing assertions do not imply an error-free editor or physical-headset run. See `Logs/Batch3/simulation-regression.log` and the earlier Batch 2 report.

Commands: `npm --prefix WebPortal run test:vr`; `npm --prefix WebPortal run test:vr-ui` (set `TECHWISE_BROWSER_CHANNEL=chrome` to use installed Chrome, otherwise install Playwright Chromium). Unity execute method: `TechWiseBatch3Verification.Run`. Local outputs: `Logs/Batch3/api-results.txt`, `ui-results.txt`, `unity-results.txt`, `unity-final.log`, `teacher-vr-record.png`, `simulation-regression.log`.

Not yet verified: deployed Supabase/Cloudflare exchange, live student DONE-to-teacher record, actual network disconnect on a headset, Android filesystem durability, rendered Quest input/readability/performance, and a new Windows/APK build. Existing scene/package log issues from Batch 2 remain separately tracked. Local fixtures do not establish that the production database has valid active term/section assignments.

## Batch 3 checkpoint

| Request | Status | Evidence |
|---|---|---|
| 1. Inspect current auth | VERIFIED | Existing Supabase/provider/profile/token code inspected |
| 2–3. Website identity and student context | IMPLEMENTED | Exchange/context route tests pass; live device handoff pending |
| 4–6. Formal attempt, result model, structured mistakes | IMPLEMENTED | Migration/RPC tests and real Unity payload contract pass; live migration pending |
| 7–8. Authenticated API and DONE integration | IMPLEMENTED | API and Unity queue checks pass; live full-system acceptance pending |
| 9. Duplicate protection | VERIFIED | Unique constraint and concurrent first/retry requests return one immutable result locally |
| 10. Pending sync | IMPLEMENTED | Windows isolated queue tests pass; Android storage/network acceptance pending |
| 11–12. Existing class record and report retrieval | IMPLEMENTED | Existing Reports integration and filter/CSV browser checks pass; live portal acceptance pending |
| 13. Security review | IN PROGRESS | Scoped credentials, role/ownership checks and score consistency tested; live configuration remains |
| 14. Networking separation | IMPLEMENTED | Session store, session-registration service, API client and pending queue remain separate from scoring |

Stop at Batch 3. A later batch must continue from this report and the stable master ledger.
