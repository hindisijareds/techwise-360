# Batch 2 — Assessment / Competition System

Status: **IMPLEMENTED; CORE AND SCENE ASSERTIONS PASSED; MANUAL/XR ACCEPTANCE REMAINS**. Stop after Batch 2. Authoritative sources remain `MASTER_REQUIREMENTS.md` and the saved `BATCH_2_REQUIREMENTS.md`.

## Architecture and changes

Extended the existing `TechWiseAttemptRecorder` and Batch 1 `TechWiseSimulationRuntime`; no second practice/assessment simulation was introduced. Desktop and Quest XRI inputs retain the same socket filters and live component validation. Existing pre-START locking and practice-only guidance were preserved. No website, schema, deployment or production identifier changes belong to this batch.

- `TechWiseAssessmentClock` creates an attempt ID at official START. It measures double-precision monotonic time supplied by `Time.realtimeSinceStartupAsDouble`. Repeated Start and Submit calls cannot reset it. The displayed/payload integer seconds are floored; metadata also stores precise elapsed seconds. Additive scene notifications no longer reset the recorder or shared simulation.
- DONE freezes time and captures each component's state before cancelling active grabs. Input is then locked and the captured state validated/scored. Holding a part cannot become a valid removal because DONE itself releases it. The resulting snapshot is not recomputed after submission.
- Final validation covers the actual authored component rules: matching target, occupancy/orientation/support/optional fastening, installed state, released removal and distance from target. RAM omissions identify individual modules. Destroyed tracked components remain incomplete. Cable/thermal-paste substeps without authored validation are still outside this coverage; no invented cable errors are generated.
- Assessment mistake copies receive a human-readable category, severity, configured penalty, chronological order, precise elapsed time and component reference where available. Practice feedback and its event objects remain unchanged. Removed the short start grace period for actual student mistakes. Recovery resets retain their existing non-scoring behavior.
- Results now label the activity and completion, score, accuracy, elapsed time, deductions and existing queue status. Explicit View Mistakes/Hide Mistakes, Close/reopen and Return controls are provided. Review titles use the actual category, including missing parts, orientation, dependency and incomplete disassembly; they no longer label every non-wrong-part error as wrong order. No automatic navigation is added.
- The existing offline queue is retained. Enqueue copies the payload and ignores duplicate IDs already pending, preventing accidental local mutation/duplicate completion. Backend idempotency, account isolation, authenticated handoff, durable-save failure recovery and server score integrity remain Batch 3 work. Raw backend error text is no longer displayed on the result screen.
- Targeted regression fixes found during disassembly testing: ignore sockets carried by the released component when checking its destination (a motherboard must not be judged against its own CPU/RAM sockets); reject socket selection when the component is more than 0.18 m from the attachment target, preventing stale physics hover candidates from re-snapping removed parts. The existing matching/orientation/support checks remain.
- Keep the submitted HUD responsive to desktop/VR control-mode changes. Correct the optional Batch 1 test's omission count to reflect Batch 2's per-component validation.

## Scoring configuration

`Assets/Resources/TechWiseAssessmentScoring.json` is loaded and copied at START, then stored with the result and its breakdown. No approved research formula was found in the inspected scoring code/documentation. The documented existing formula is preserved as the default rather than silently replacing existing scores with a different weighted model.

Defaults: standard mistakes 8 points, orientation/minor mistakes 4, missing-support/critical mistakes 16. Assembly target 600 seconds, disassembly target 420 seconds. Every full 30 seconds beyond the target costs 2 points, capped at 20. Completion is the fraction of fully complete component groups; RAM requires all authored modules.

`score = min(completionPercent, max(0, 100 - sum(mistakePenalties) - timePenalty))`

`accuracy = min(completionPercent, max(0, 100 - sum(mistakePenalties)))`

More mistakes cannot improve a score. Severe mistakes cost more. Time cannot account for more than 20 deducted points; empty work cannot earn points through speed. Settings validation bounds penalties and prevents a zero time interval. Each result retains the configuration version and exact settings for reproduction. The master prompt's 80/20 model is a suggested alternative, not the selected formula.

## Files modified relative to the Batch 1 handoff

Existing scripts:

- `Assets/Script/TechWiseAttemptRecorder.cs`
- `Assets/Script/TechWiseSimulationRuntime.cs`
- `Assets/Script/TechWisePortalClient.cs` (DTO fields only)
- `Assets/Script/TechWiseOfflineAttemptQueue.cs`
- `Assets/Editor/TechWiseBatch1Verification.cs` (one omission-count assertion)

Created scripts/assets, plus Unity metadata:

- `Assets/Script/TechWiseAssessmentClock.cs`
- `Assets/Script/TechWiseAssessmentScoring.cs`
- `Assets/Resources/TechWiseAssessmentScoring.json`
- `Assets/Editor/TechWiseBatch2Verification.cs`

Documentation: this report, `BATCH_2_REQUIREMENTS.md`, `CHECKLIST.md`, `requirements-ledger.json`. Pre-existing dirty work, including the Batch 1 edits, remains intact. No commit/push is claimed.

## Verification

The new Batch 2 request explicitly asks for acceptance/regression checks. The runner isolates the pending-result file, disables all queue sync coroutines, preserves mode preferences and restores them on completion. It does not send test scores. It uses the real Multiplayer simulation scene with deterministic XRI input; it does not simulate headset tracking or establish visual correctness.

Run:

```powershell
& 'D:/UnityEditor/6000.4.8f1/Editor/Unity.exe' -batchmode -nographics -job-worker-count 2 -projectPath 'D:/Jared/UnityProject/TechWise360' -executeMethod TechWiseBatch2Verification.RunSimulationTests -logFile 'Logs/Batch2-verification.log'
```

Final Unity 6000.4.8f1 run: **17 core assertions passed and 84 Play Mode assertions passed**. Evidence: `Logs/Batch2/core-results.txt`, `Logs/Batch2/playmode-results.txt`, and `Logs/Batch2-verification.log`. No C# compilation error was found in the final run. `git diff --check` passed for changed tracked source; Git line-ending notices remain.

| Area | Actual result and limits |
|---|---|
| Core clock/scoring | PASS: official Start, unique/stable ID, repeated Start/Submit, monotonic/frozen elapsed time, bounds, time cap, severity, reproducibility settings copy and incomplete-work cap. |
| Assembly assessment | PASS: all 11 components valid at DONE; one injected orientation validation event remained hidden before DONE and produced score 96 afterward. No automatic completion; missing RAM modules detected individually in a separate empty submission. |
| Disassembly assessment | PASS: deterministic XRI selection/release of all 11 components; all complete at DONE; one hidden orientation event produced score 96. Untouched disassembly scored zero. A held CPU stayed incomplete when DONE cancelled its grab. |
| Submission protection | PASS: timer continues at timeScale zero, then freezes; extra DONE/enqueue calls preserve one payload; later validation events do not mutate the result; interaction-manager selection filters reject grabbing after DONE. |
| Results | PASS for UI object/state checks: result controls exist, View Mistakes opens the review, HUD switches between desktop overlay and VR world-space. No visual/readability or physical button/ray acceptance is claimed. |
| Practice regression | PASS for full assembly progression, side panel/marker creation, immediate feedback, disassembly setup and first removal-step progression. This is focused coverage, not a complete visual/device regression suite. |

Earlier runs exposed a test-runner XRI overload issue and a check that bypassed the interface's selection filters; both fixture issues were corrected. Disassembly diagnostics then exposed the self-socket and stale-hover issues fixed above. The deterministic removal fixture also now updates the Rigidbody position, not just its transform. The final run passed after those corrections. The raw diagnostic records remain in ignored `Logs/Batch2`.

The passing assertion counts are **not a clean, error-free editor run**. The log also contains tutorial framework duplicate `SceneObjectGuid` exceptions, XR UI Toolkit interactor registration-capacity errors, and an old XR Device Simulator sample `ArgumentOutOfRangeException` when switching into VR without resolved device controls. OpenXR reports no runtime available. These occur in existing scene/package/sample infrastructure; no claim is made that they predate this exact test flow. They remain for focused editor/device follow-up and limit any claim of complete VR UI acceptance. The test runner's own assertions do not automatically fail on unrelated Unity log exceptions.

## Batch 2 requirement checkpoint

| Request section | Status | Evidence / remaining acceptance |
|---|---|---|
| 1. Assessment separation | VERIFIED | Shared mode guards inspected; panel/marker/socket-preview and hidden-result state assertions passed. Rendered headset acceptance remains. |
| 2. Timer | VERIFIED | Core and Play Mode timeScale/submit tests passed. |
| 3. DONE | VERIFIED | Both activity flows wait for DONE, including complete and incomplete submissions. |
| 4. Lock after submission | VERIFIED | XRI manager filter, frozen payload/time and duplicate checks passed. |
| 5. Structured mistakes | VERIFIED | Category, severity, penalty/order fields inspected and tested; individual final omissions verified. |
| 6. No instant explanation | VERIFIED | Shared event recorded internally; feedback/text/snapshot assertions withheld the injected explanation. |
| 7. Final review | IMPLEMENTED | Review object and category checks passed; rendered readability remains manual. |
| 8. Scoring | VERIFIED | Central configuration and deterministic evaluator tests passed; defaults/formula documented above. |
| 9. Result screen | IMPLEMENTED | UI/control state checked; physical interaction and visual acceptance remain. |
| 10. Unique ID | VERIFIED | ID exists at START, survives repeated Start and reaches one frozen queued payload. |
| 11. Both activities | VERIFIED | Automated assembly and disassembly submission flows passed with deterministic XRI input. |
| 12. Regression | IN PROGRESS | Focused practice and assessment assertions passed; full manual/device regression and log issues remain. |

No Batch 2 player build/APK is claimed. `Builds/Batch1/Desktop` remains the earlier Batch 1 artifact. Quest packaging previously exhausted virtual memory and is not retried in this assessment batch. Physical Quest controls, rendered readability and target-device performance remain manual acceptance.
