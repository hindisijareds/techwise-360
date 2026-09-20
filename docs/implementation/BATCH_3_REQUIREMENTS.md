==================================================
BATCH 3 — VR ↔ WEB AUTHENTICATION, API, DATABASE AND RESULTS
============================================================

Continue using the original TechWise360 master requirements, existing project state, completed Batch 1 and Batch 2 changes, and your existing implementation checklist.

Proceed to:

# Batch 3 — VR ↔ Web Authentication, API, Database, and Result Integration

Do NOT restart or reimplement completed functionality unless integration requires a targeted correction.

This is a high-priority integration batch.

---

## 1. INSPECT CURRENT AUTHENTICATION

Determine exactly how TechWise360 currently handles:

* student login
* teacher login
* sessions
* cookies
* JWT/token systems
* authentication provider
* authorization middleware
* role checking
* student IDs
* enrollment context
* Unity authentication if already partially implemented

Extend the existing system instead of creating unnecessary duplicate authentication.

---

## 2. WEBSITE → VR IDENTITY

Implement the adviser requirement that the VR simulation recognizes the authenticated TechWise360 student.

Avoid unnecessary duplicate login.

Choose an architecture appropriate to the current system, such as:

* short-lived launch token
* session exchange
* secure device authorization
* authenticated deep link
* short-lived API credential

Do NOT:

* hard-code credentials
* embed permanent secrets inside Unity
* trust arbitrary student IDs supplied by the Unity client
* expose backend secrets

The backend must remain the authority for user identity.

---

## 3. STUDENT CONTEXT

Unity should be able to retrieve appropriate authenticated student context:

* student ID
* student name
* grade level
* section
* academic year
* term
* enrollment
* assigned assessment where applicable

Use IDs internally wherever appropriate.

---

## 4. ASSESSMENT ATTEMPT

Create or connect a formal assessment-attempt entity.

Each attempt should include appropriate fields such as:

* attempt ID
* student ID
* assessment ID
* activity type
* academic year
* term
* grade
* section
* start time
* status

Use the unique attempt ID created/prepared in Batch 2.

---

## 5. VR RESULT DATA

Create or extend the backend data model to store:

* attempt ID
* student ID
* assessment ID
* PC Assembly / Disassembly
* mode
* academic year
* term
* grade
* section
* started time
* completed time
* elapsed time
* score
* accuracy
* mistake count
* structured mistakes
* submission status
* synchronization status where needed

Use proper relationships.

Do not use names as primary identifiers.

---

## 6. STRUCTURED MISTAKE STORAGE

Connect Batch 2 mistake records to backend storage.

Avoid creating a second unrelated mistake system.

Store actual mistake categories/details in a useful structure.

This data should later support weak-area analysis.

---

## 7. AUTHENTICATED RESULT API

Create or fix authenticated API endpoint(s) for VR results.

Required conceptual flow:

Unity
→ Authenticated API
→ Backend validation
→ Database
→ Teacher Class Record / Reports

The server must verify the authenticated student.

A student must not be able to change studentId in a request and submit another student's score.

---

## 8. DONE → BACKEND SUBMISSION

Connect the Batch 2 DONE flow to result submission.

Expected flow:

DONE
→ timer stops
→ assessment validated
→ mistakes calculated
→ score calculated
→ result displayed
→ authenticated result submission
→ backend verifies attempt/student
→ backend stores result
→ Unity receives confirmation

Do not require manual score entry.

---

## 9. DUPLICATE SUBMISSION PROTECTION

Protect against network retries creating duplicate results.

Use appropriate mechanisms such as:

* unique attempt IDs
* unique constraints
* idempotency keys
* completed-attempt server checks

A repeated submission of the same completed attempt should not create another class-record result.

---

## 10. FAILED NETWORK / PENDING SYNC

Do not lose a completed assessment because internet access fails.

Where appropriate implement:

DONE
→ try API submission

If successful:
SYNCED

If failed:
PENDING SYNC
→ save locally
→ retry later
→ same attempt ID
→ server prevents duplicate
→ mark SYNCED after success

Do not falsely tell the student a server submission succeeded if it did not.

---

## 11. TEACHER CLASS RECORD

Integrate formal VR results into the EXISTING teacher class record.

Do not create a disconnected second class record.

Teacher should be able to identify:

* student
* activity
* score
* elapsed time
* mistake count
* completion date
* academic year
* term

---

## 12. REPORT GENERATION

Ensure VR results are retrievable by the existing report system.

Prepare/support filtering where appropriate by:

* academic year
* term
* grade
* section
* student
* assessment
* VR activity

This directly addresses the adviser's request.

---

## 13. SECURITY

Review:

* token/session security
* teacher/student authorization
* result ownership
* attempt ownership
* secrets
* environment variables
* malformed results
* expired authentication
* API validation

Do not expose sensitive tokens in logs.

---

## 14. UNITY NETWORK CODE QUALITY

Keep networking separate from gameplay where practical.

Possible responsibilities:

* Auth/Session Manager
* Assessment Session Manager
* API Client
* Result Submission Service
* Pending Sync Manager

Do not put all networking, scoring, UI, and gameplay into one giant script.

---

# BATCH 3 END-TO-END TEST

Test as far as the environment allows:

1. Student logs into TechWise360.
2. VR receives correct student identity.
3. Student context loads.
4. Assessment starts.
5. Unique attempt exists.
6. Student completes assessment.
7. DONE is pressed.
8. Score/mistakes generated.
9. Result submitted through authenticated API.
10. Backend verifies identity.
11. Result saved once.
12. Teacher opens class record.
13. Result appears.
14. Reports can retrieve the result.

Also test:

15. submission fails due to connectivity
16. result becomes pending
17. connection restored
18. retry succeeds
19. duplicate result is NOT created

Do not fabricate test success.

---

# BATCH 3 COMPLETION REPORT

Provide:

* files modified
* database changes
* migrations
* APIs created/modified
* Unity networking scripts
* authentication approach
* result submission flow
* duplicate prevention method
* offline/pending sync behavior
* class-record integration
* report integration status
* tests performed
* blockers
* updated master checklist

Do NOT proceed to Batch 4 automatically.
