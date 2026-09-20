# BATCH 2 — ASSESSMENT / COMPETITION SYSTEM

Continue using the original TechWise360 master requirements, the existing project state, all completed Batch 1 changes, and your existing master implementation checklist.

Proceed to:

# Batch 2 — Assessment / Competition System

Do NOT restart the project.

Do NOT redo Batch 1 unless testing reveals a regression that requires a targeted fix.

The objective of this batch is to complete the formal VR/Desktop assessment system for PC Assembly and PC Disassembly.

Implement and verify the following.

---

## 1. ASSESSMENT / COMPETITION MODE

Create or complete a dedicated Assessment / Competition Mode.

This mode must be clearly separated from Practice Mode.

During an actual assessment, the student should independently perform the task.

DO NOT show:

* step-by-step instructions
* arrows showing the next component
* highlighted correct component
* highlighted correct slot
* immediate mistake explanation
* automatic hints
* corrective feedback that gives away the correct answer

Internal validation may still happen silently.

---

## 2. TIMER

Implement a reliable assessment timer.

The timer should:

* begin only when the assessment officially starts
* continue accurately throughout the attempt
* not reset accidentally
* stop when the student submits the assessment using DONE
* store elapsed time for scoring/results
* support both PC Assembly and PC Disassembly assessments

Do not tie the timer incorrectly to frame rate.

Use an appropriate timing mechanism.

---

## 3. DONE BUTTON

This is a major adviser requirement.

The simulation must NOT automatically finish when the student places the final expected component.

Add a clear:

DONE

button.

The student decides when they believe the assessment is complete.

When DONE is pressed:

1. optionally request confirmation if appropriate
2. stop the timer
3. prevent further interaction
4. validate the complete build/disassembly
5. detect missing components/actions
6. identify mistakes
7. calculate score
8. create the final result
9. show the result screen
10. prepare the result for later backend submission

Do not reveal mistakes before DONE is pressed.

---

## 4. LOCK AFTER SUBMISSION

After DONE is confirmed:

* disable further grabbing/dragging
* prevent additional component placement
* prevent score manipulation
* prevent timer changes
* prevent a second completion event

The submitted state should be final for that attempt.

---

## 5. STRUCTURED MISTAKE TRACKING

Complete or improve the mistake system.

Do NOT store only:

mistakes = 3

Track structured mistakes.

Possible fields:

* mistake type
* component
* description
* step
* severity
* score penalty
* timestamp/order
* category

Possible categories:

* Incorrect Component
* Invalid Target
* Incorrect Orientation
* Incorrect Sequence
* Missing Component
* Missing Connection
* Invalid Placement
* Critical Assembly Error

Use the actual simulation rules and existing architecture.

Do not duplicate Batch 1 systems unnecessarily.

---

## 6. NO INSTANT MISTAKE FEEDBACK IN ASSESSMENT

During Assessment Mode:

If a mistake occurs, record it internally when appropriate.

Do NOT immediately display:

"Wrong RAM slot"

or similar corrective feedback.

The adviser specifically wants the student to see mistakes only after completing/submitting the assessment.

---

## 7. FINAL MISTAKE REVIEW

After DONE is pressed and the result is calculated, allow the student to review mistakes.

Each mistake should have a useful explanation.

Example:

Incorrect RAM Placement

The RAM module was installed in an incorrect DIMM position.

Do not show meaningless technical IDs to students.

---

## 8. SCORING SYSTEM

The adviser specified that scoring should be based on:

* mistakes
* completion time

Accuracy should be more important than speed.

Inspect the existing scoring system first.

If an approved/research-based scoring formula already exists, preserve it.

If no approved formula exists, implement a centralized configurable scoring system.

A reasonable default concept is:

Accuracy/Mistakes = 80%
Time = 20%

Do NOT scatter penalty values throughout unrelated scripts.

Use a central configuration, manager, constants file, ScriptableObject, or equivalent architecture.

Ensure:

* more mistakes reduce the score
* severe mistakes may have greater penalties
* faster completion may improve time score
* rushing with many mistakes should NOT produce a high score
* final score has valid minimum/maximum bounds

---

## 9. ASSESSMENT RESULT SCREEN

Create or improve the result screen.

It should show useful information such as:

* activity name
* PC Assembly / PC Disassembly
* final score
* elapsed time
* mistake count
* accuracy where applicable
* submission/completion status
* View Mistakes button
* Return button

Do not automatically leave the result screen before the student can read it.

---

## 10. UNIQUE ATTEMPT IDENTIFIER

Each formal assessment attempt should have a unique session/attempt identifier.

This will be needed later for:

* result submission
* duplicate prevention
* database storage
* offline retry

Prepare this architecture now even if backend integration happens in Batch 3.

---

## 11. ASSEMBLY AND DISASSEMBLY

Verify the assessment system supports BOTH:

* PC Assembly
* PC Disassembly

Do not complete only one mode unless the existing project scope explicitly says otherwise.

---

## 12. REGRESSION TESTING

Ensure Batch 1 Practice Mode still works after adding assessment functionality.

Test:

Practice:

* steps
* side panel
* highlights
* mistakes
* guidance

Assessment:

* no hints
* timer
* Done button
* validation
* scoring
* result screen

---

# BATCH 2 ACCEPTANCE TEST

Run this flow:

1. Start PC Assembly Assessment.
2. Timer begins.
3. Student receives no answer hints.
4. Student makes at least one mistake.
5. Student continues without being told the answer.
6. Student finishes what they believe is correct.
7. Assessment does NOT auto-submit.
8. Student presses DONE.
9. Timer stops.
10. Objects become locked.
11. Full validation runs.
12. Mistakes are calculated.
13. Score is calculated.
14. Result screen appears.
15. Student can review mistakes.
16. Attempt cannot accidentally submit twice.

Repeat enough testing for PC Disassembly.

Do not fabricate test results.

---

# BATCH 2 COMPLETION REPORT

At the end provide:

* files modified
* scripts created
* assessment architecture used
* timer implementation
* mistake system implementation
* scoring formula/configuration
* result screen changes
* assembly test status
* disassembly test status
* regressions fixed
* remaining issues
* updated master checklist

Do NOT proceed to Batch 3 automatically.

Stop when Batch 2 is as complete and verified as possible.
