You are acting as the lead senior full-stack engineer, Unity VR developer, database architect, QA engineer, UI/UX engineer, and DevOps engineer for our capstone system called **TechWise360**.

You have access to the existing project repository and files.

Your task is NOT to create a disconnected prototype.

Your task is to:

1. Inspect and understand the entire existing TechWise360 project.
2. Identify the current architecture, frameworks, APIs, database structure, authentication implementation, deployment configuration, and Unity project structure.
3. Fix the existing bugs.
4. Implement all requirements listed below.
5. Preserve existing working functionality.
6. Properly connect the web system, database/API, and Unity VR simulation.
7. Test the complete end-to-end system.
8. Build the updated VR simulation.
9. Build the production web application.
10. Deploy/update the existing published web application using its current Cloudflare deployment configuration.
11. Commit and push the completed changes to the existing Git repository if Git remote access is already configured.

Do not blindly rewrite the project.

First inspect the current implementation and modify the existing architecture professionally.

Do not delete working features merely because replacing them is easier.

Do not use mock data for features that already have a real backend/database.

Do not create duplicate authentication, duplicate databases, or duplicate systems when the project already has appropriate implementations.

---

# PROJECT

**TechWise360**

TechWise360 is an educational learning system containing:

* Teacher web portal
* Student web portal
* Lessons/modules
* Assessments
* Academic year and term management
* Student management
* Class records
* Reports
* Analytics
* Unity-based PC Assembly/Disassembly VR Simulation
* Desktop simulation support where currently implemented
* Practice Mode
* Actual Assessment / Competition Mode

The target users include Grade 9 and Grade 10 students.

The system is intended for a school environment where some students may have limited or unreliable internet access.

---

# PHASE 1 — INSPECT THE EXISTING PROJECT

Before modifying anything, analyze the repository.

Determine:

* frontend framework
* backend/API architecture
* database
* database schema
* authentication implementation
* session/token implementation
* teacher/student roles
* academic year implementation
* term implementation
* registration implementation
* assessments
* modules
* reporting
* class records
* Unity integration
* Unity scenes
* Unity scripts
* VR SDK being used
* Meta Quest/OpenXR implementation
* desktop controls
* API endpoints
* Cloudflare configuration
* Git configuration
* build scripts
* environment variables
* deployment scripts

Search the codebase thoroughly before deciding something does not exist.

Create a clear internal implementation plan and then execute it.

Do not stop after analysis.

---

# PHASE 2 — ACADEMIC YEAR MANAGEMENT

The current system must NOT overwrite previous academic years.

The adviser specifically identified this as a major requirement.

A teacher must be able to create additional academic years such as:

* 2026–2027
* 2027–2028
* 2028–2029
* future years

Do NOT solve this by editing and overwriting the previous academic year.

Previous academic years must remain retrievable.

Historical records must remain intact.

Teachers may need to return to earlier records because of:

* incomplete grades
* grade corrections
* follow-ups
* student concerns
* previous assessments
* reports
* historical records

Implement a proper academic-year entity/model if one does not already exist.

An academic year should support appropriate fields such as:

* unique ID
* academic year label
* start year/date
* end year/date
* status
* active/inactive
* created timestamp
* updated timestamp

Only one academic year should normally be active at a time unless the existing architecture intentionally supports otherwise.

Changing the active academic year must NOT delete or overwrite historical records.

---

# PHASE 3 — TERM MANAGEMENT

Terms must be associated with an academic year.

Do not treat First, Second, and Third Term as permanent global records that are overwritten forever.

A teacher must be able to create/manage terms for an academic year.

Example:

Academic Year 2026–2027

* First Term
* Second Term
* Third Term

Academic Year 2027–2028

* First Term
* Second Term
* Third Term

If the school uses different terminology in the existing project, retain compatibility.

The important rule is:

**Academic Year and Term must be separate entities/relationships.**

A term must belong to an academic year.

Support active/inactive status where appropriate.

Historical terms and their corresponding data must remain accessible.

---

# PHASE 4 — REGISTRATION / ENROLLMENT

The adviser identified that student registration currently does not clearly specify where the student's enrollment is saved.

Fix this.

A student enrollment must clearly belong to:

* Academic Year
* Term
* Grade Level
* Section

Do not rely only on hidden assumptions.

If the system uses an active academic year and active term, the registration UI should clearly display them.

For example:

Academic Year:
2027–2028

Term:
First Term

Grade Level:
Grade 9

Section:
Section A

Ideally use IDs internally rather than storing only display strings.

The database must establish correct relationships.

A student enrolled in one academic year should not automatically overwrite or destroy previous enrollment records.

Consider implementing a proper enrollment/junction record if this matches the current architecture.

Example relationship concept:

Student
↓
Enrollment
↓
Academic Year
↓
Term
↓
Grade Level
↓
Section

Do not duplicate student accounts merely because they enroll in a new academic year.

---

# PHASE 5 — REMOVE REDUNDANT FULL NAME FIELD

The adviser questioned why registration contains:

* First Name
* Last Name
* Full Name

If Full Name is manually entered while First Name and Last Name already exist, this creates redundant data.

Inspect how the system uses Full Name.

If it has no legitimate independent purpose:

remove the manually editable Full Name field.

Generate/display the name from appropriate components instead.

For example:

firstName + middleName + lastName

or whatever structure the existing database uses.

Do not unnecessarily store duplicate information.

If a fullName field is technically needed for search/display/indexing, generate it automatically and document why.

Do not require the user to type the same information twice.

---

# PHASE 6 — STUDENT FIELDS

Review student registration fields for necessity and consistency.

Keep Grade Level limited to the actual supported levels:

* Grade 9
* Grade 10

unless the existing system has a justified configurable implementation.

Review "Hometown".

Do not automatically remove it if it legitimately supports students outside Camantiles/Manaoag boundaries.

Only retain fields that have a clear purpose.

Validate required vs optional fields properly.

Avoid redundant database fields.

---

# PHASE 7 — SECTION FLEXIBILITY

The adviser requested flexibility for future sections.

Currently there may be only three sections.

Do NOT hard-code the system so that only those sections will ever exist.

Implement configurable sections.

Preferred approach:

Teachers/admins should be able to create sections.

Example:

Sections

* Section A
* Section B
* Section C
* Others / Add New Section

If the existing UI specifically needs an "Others" option, implement it so selecting Others allows the teacher/admin to create a new valid section.

Better still, use a proper Section management entity.

A newly created section should then become selectable in future enrollment forms.

Do not store free-form section strings if a proper relational solution fits the existing architecture.

---

# PHASE 8 — MODULE DOWNLOAD AND OFFLINE SUPPORT

Students must be able to:

* open modules
* view modules
* download modules

The adviser emphasized that not every student has reliable Wi-Fi.

Improve offline behavior where technically appropriate.

At minimum:

* allow module files/content to be downloaded
* ensure download controls work
* give clear download/open indicators
* provide graceful handling when connectivity is lost

If the existing application already contains an offline module feature, inspect it and fix it rather than creating an unrelated second implementation.

If the web application is a PWA or already supports caching, extend the existing implementation carefully.

Do not pretend a module is available offline unless the required content has actually been cached/downloaded.

---

# PHASE 9 — ASSESSMENTS

Teacher-created assessments may include duration/time limits.

Maintain existing assessment functionality.

However, the important implementation is the connection to the PC Assembly/Disassembly simulation.

The VR simulation is part of the educational assessment.

Actual assessment results must be retrievable by the teacher.

---

# PHASE 10 — WEB AUTHENTICATION ↔ VR AUTHENTICATION

A major adviser requirement:

If the student is already logged into the TechWise360 website, the VR simulation should identify the same student.

Do not unnecessarily make the student log in a second time if the existing architecture can securely support shared authentication/session handoff.

Analyze the current auth system first.

Implement a secure solution appropriate to the architecture.

Possible concepts include:

* short-lived launch token
* secure API token
* authenticated deep link
* session exchange
* device authorization flow

Choose the solution that best fits the existing project.

Do NOT hard-code credentials.

Do NOT put permanent secret keys in Unity client code.

Do NOT expose backend secrets.

The VR application must be able to determine the authenticated student and relevant context, including where needed:

* student ID
* student name
* grade level
* section
* academic year
* term

---

# PHASE 11 — VR SIMULATION ARCHITECTURE

Inspect the existing Unity simulation before making changes.

The simulation supports PC:

* Assembly
* Disassembly

and should provide at least:

1. Practice Mode
2. Actual Assessment / Competition Mode

The two modes MUST behave differently.

---

# PHASE 12 — PRACTICE MODE

Practice Mode is intended for learning.

Implement a structured step-by-step PC assembly/disassembly experience.

Students should not simply receive a pile of components without guidance.

There should be a sequence.

Example conceptual assembly flow:

1. Prepare/open case
2. Install power supply if applicable
3. Install motherboard
4. Install CPU
5. Install CPU cooler
6. Install RAM
7. Install storage
8. Install GPU
9. Connect required cables
10. Perform final check

Do not blindly use this exact sequence if the existing simulation or curriculum defines another correct sequence.

Inspect the existing learning content and simulation.

Use the educationally correct sequence.

---

# PRACTICE MODE SIDE STEP PANEL

The adviser requested:

**The steps should appear at the side during Practice Mode.**

Create a proper Practice Guide panel.

For VR:

Use appropriate World Space UI positioned so it is readable without obstructing the workbench.

For Desktop:

Use a clean side UI.

Example behavior:

✓ Completed step

→ Current step

Upcoming step

Example:

PC ASSEMBLY

✓ 1. Prepare Case
✓ 2. Install PSU
→ 3. Install Motherboard
4. Install CPU
5. Install Cooler
6. Install RAM
7. Install Storage
8. Install GPU
9. Connect Cables

The user should always understand what they are currently supposed to do.

---

# PHASE 13 — PRACTICE VISUAL GUIDANCE

Practice Mode should provide visual guidance.

Implement appropriate:

* object highlight
* glow
* outline
* pulse/blink
* arrow indicator

For example:

Current instruction:

Install RAM

Then:

* correct RAM component highlights
* valid RAM slot highlights
* optional subtle arrow points toward the component/target

Do not make the visual effects excessive or distracting.

Prefer clean educational guidance.

Avoid using giant arrows if an outline/glow is enough.

---

# PHASE 14 — PRACTICE MODE MISTAKES

Practice Mode may provide immediate educational feedback.

If the student attempts an incorrect action:

* identify that the action is incorrect
* prevent invalid permanent placement where appropriate
* provide concise educational guidance

Example:

Incorrect location.

RAM must be installed in a valid DIMM slot.

Also record mistakes during Practice Mode.

At the end of Practice Mode, provide a complete review.

Example:

Practice Complete

Mistakes: 3

1. Incorrect RAM slot
   Description of the issue.

2. CPU orientation incorrect
   Description.

3. Missing storage cable
   Description.

Practice results should primarily assist learning rather than act as formal grades unless the existing system requires otherwise.

---

# PHASE 15 — PRACTICE MODE BLACKBOARD

The adviser requested:

Place the text:

**Practice Mode**

on the blackboard in the Practice Mode environment.

Use a chalk-like visual style/font appropriate to the classroom scene.

Keep it readable and visually consistent.

Do not redesign the entire environment unnecessarily.

---

# PHASE 16 — DESKTOP CONTROL GUIDE

Create a clean control guide for Desktop Practice Mode.

Inspect the existing desktop controls first.

Only display controls that actually exist.

Possible example:

WASD — Move
Mouse — Look
Left Click — Grab/Select
E — Interact
R — Rotate
Mouse Wheel — Zoom
ESC — Pause

Do NOT invent keybindings.

If current bindings differ, use the real controls.

Show the guide at an appropriate time such as:

* first launch
* beginning of Practice Mode
* Help/Controls panel

---

# PHASE 17 — META QUEST VR CONTROL GUIDE

Create a controller guide for Meta Quest/OpenXR if the existing project supports Quest.

This is lower priority than core functionality.

The guide should preferably use:

* a 3D controller model
* labels pointing to buttons
* actual project bindings

Example concepts only:

Trigger — Grab
Grip — Hold/Interact
Thumbstick — Move
A — Confirm
B — Back
Menu — Pause

Do not use those mappings unless they match the current Input System/OpenXR bindings.

The actual game input mapping is the source of truth.

---

# PHASE 18 — NO CONTROLLER REMAPPING

The adviser instructed us NOT to provide controller configuration/remapping.

Remove or avoid custom controller mapping UI.

Use reliable default controls.

Do not spend development time creating a user-facing button remapping system.

---

# PHASE 19 — ASSESSMENT / COMPETITION MODE

Assessment/Competition Mode must evaluate the student's actual ability.

Therefore:

DO NOT show:

* step-by-step instructions
* highlighted correct component
* highlighted correct slot
* arrow guidance
* instant explanations
* immediate mistake correction that reveals the correct answer

The student performs the assembly/disassembly independently.

The system may internally detect and record mistakes without showing them.

---

# PHASE 20 — DONE BUTTON

This is a major requirement.

The assessment must NOT automatically finish simply because all expected parts appear to have been placed.

Add a clear:

**DONE**

button.

The student decides when they believe the PC assembly/disassembly is complete.

When Done is pressed:

1. Confirm if appropriate.
2. Stop the assessment timer.
3. Lock further manipulation.
4. Validate the complete assembly/disassembly.
5. Evaluate missing/incorrect components.
6. Evaluate applicable sequence mistakes.
7. Calculate mistakes.
8. Calculate score.
9. Generate final result.
10. Save the result.
11. Attempt to sync it with the backend.
12. Display the results screen.

Do not reveal assessment mistakes before submission.

---

# PHASE 21 — ASSESSMENT RESULT SCREEN

After the student presses Done, show an appropriate results screen.

Example:

PC ASSEMBLY COMPLETE

Final Score
92 / 100

Completion Time
08:34

Mistakes
3

Accuracy
94%

[VIEW MISTAKES]

[RETURN]

When View Mistakes is opened, display meaningful educational descriptions.

Example:

1. Incorrect RAM Slot

You installed the RAM in an incorrect DIMM position.

2. Storage Connection Missing

The storage device was installed but its required connection was incomplete.

3. GPU Installation Issue

The graphics card was not installed correctly.

Use technically correct descriptions based on actual simulation rules.

---

# PHASE 22 — MISTAKE TRACKING

Do not store only:

mistakes = 3

Store structured mistake information.

Create an appropriate mistake type/model.

Possible fields:

* mistake ID/type
* component
* category
* description
* timestamp
* relevant step
* severity
* penalty
* student/session ID

Potential categories:

* Incorrect Component
* Incorrect Target
* Incorrect Orientation
* Incorrect Sequence
* Missing Component
* Missing Connection
* Invalid Placement
* Critical Assembly Error

Use categories appropriate to the simulation.

This structured data should eventually support analytics/weak-area analysis.

---

# PHASE 23 — COMPONENT VALIDATION

Each interactable PC component must have appropriate validation.

Where applicable, evaluate:

* correct component
* valid target
* correct snap point
* valid orientation
* required previous state
* dependency
* connection
* installation status

Example concept:

RAM
→ valid DIMM slot

GPU
→ correct PCIe slot

CPU
→ CPU socket

Storage
→ correct mounting area + required connections

Do not permit impossible permanent placements.

---

# PHASE 24 — PRACTICE VS ASSESSMENT BEHAVIOR

Enforce this distinction:

PRACTICE MODE

* Step guide: YES
* Highlight: YES
* Arrow assistance: YES
* Immediate feedback: YES
* Mistake explanation: YES
* End review: YES
* Formal grade: usually NO
* Teacher class-record submission: optional depending on current requirements

ASSESSMENT / COMPETITION MODE

* Step guide: NO
* Highlight help: NO
* Arrow assistance: NO
* Immediate mistake revelation: NO
* Timer: YES
* Done button: YES
* Mistake calculation: YES
* Score: YES
* End review: YES
* Save result: YES
* Teacher class record: YES

---

# PHASE 25 — TIMER

Assessment Mode requires timing.

Timer should:

* start at the correct assessment start event
* run reliably
* stop ONLY when the assessment is legitimately submitted/completed
* not reset unexpectedly
* be stored as part of the assessment attempt
* use a reliable monotonic/time mechanism where appropriate

Prevent trivial exploits if practical within the existing system.

---

# PHASE 26 — SCORING SYSTEM

The adviser specified:

**Scoring must be based on mistakes and time.**

Implement a transparent scoring system.

Accuracy should have more influence than raw speed.

Do not reward students for rushing while making many mistakes.

A reasonable starting model is:

Accuracy = 80%
Time = 20%

However:

Inspect whether an existing scoring formula or research methodology already exists.

If a documented scoring method exists, preserve it.

If there is no existing approved formula, implement a clean configurable solution.

Do not scatter penalty values throughout scripts.

Create centralized/configurable scoring constants or ScriptableObjects where appropriate.

Example conceptual penalties:

Minor error: small deduction

Wrong component: medium deduction

Missing component: medium deduction

Critical assembly error: larger deduction

Incorrect sequence: appropriate deduction

The score should be reproducible and explainable.

---

# PHASE 27 — SCREWS

Whether the simulation must include manually installing screws is currently pending adviser confirmation.

Therefore:

Do NOT spend a disproportionate amount of time implementing complex screw physics unless the project already contains this mechanic.

Structure the simulation so screw interactions could be added later.

If screw mechanics already exist, retain/fix them.

If not, use the existing component fastening behavior for now and document where screw interaction can be introduced.

Do not let this pending requirement block the entire build.

---

# PHASE 28 — GRAB / DRAG / SNAP BUGS

The existing simulation reportedly has some drag/grab issues.

Perform a full interaction audit.

Test components for:

* pickup
* grabbing
* releasing
* rotation
* desktop dragging
* VR grabbing
* snap behavior
* snap target correctness
* wrong-target prevention
* collision behavior
* falling through geometry
* physics instability
* duplicate interactions
* stuck components
* double installation
* multiple objects occupying one slot
* accidentally disappearing components
* re-grabbing after placement
* resetting when necessary

Fix root causes.

Do not hide bugs with arbitrary delays or fragile hacks.

---

# PHASE 29 — PC ASSEMBLY PROCESS QUALITY

Study the interaction quality of professional PC building simulators conceptually.

The goal is NOT to copy copyrighted assets, UI, code, or content.

Use professional PC assembly simulation concepts such as:

* clear object interaction
* snap points
* component inspection
* realistic installation order
* meaningful feedback
* understandable workbench layout
* readable component placement
* correct PC terminology

Use original implementation/assets already authorized for the project.

---

# PHASE 30 — VR RESULT API

Actual VR assessment results must be sent to the TechWise360 backend.

Create or fix the appropriate authenticated API.

A VR attempt should record enough information to identify:

* student
* assessment
* mode
* activity
* academic year
* term
* grade level
* section
* start time
* completion time
* elapsed time
* mistakes
* final score
* completion status
* submission timestamp

Use database relationships/IDs properly.

Do not depend on student names as primary identifiers.

---

# PHASE 31 — TEACHER CLASS RECORD

Actual VR assessment results must appear in the teacher's system.

The adviser specifically wants the result to be fetchable in report generation/class records.

After a student finishes:

VR Simulation
↓
Authenticated API
↓
Database
↓
Teacher Class Record
↓
Reports / Analytics

The teacher must be able to identify:

* student
* activity
* score
* completion time
* mistakes where appropriate
* completion date
* academic year
* term

Do not require manual copying of the VR score.

---

# PHASE 32 — REPORT GENERATION

Ensure VR assessment results can be included in existing reporting.

Do not create a completely separate reporting application.

Extend the existing reports/class-record implementation.

Where appropriate allow filtering by:

* Academic Year
* Term
* Grade
* Section
* Student
* Assessment
* VR Activity

Maintain existing report formatting unless there is a clear bug.

---

# PHASE 33 — WEAK AREA ANALYTICS

If the current project already contains Weak Area Analysis or analytics, connect structured VR mistakes to it.

Potential metrics:

RAM Installation errors
CPU Installation errors
GPU Installation errors
Storage errors
Cable/Connection errors
Sequence errors

Do NOT fabricate analytics.

Use actual stored assessment data.

If implementing this fully would require redesigning unrelated features, prioritize storing the correct structured mistake data so analytics can consume it properly.

---

# PHASE 34 — OFFLINE / FAILED SUBMISSION SAFETY

Students may have unreliable internet.

Do not lose a completed VR assessment simply because the network failed at submission.

Implement a safe local pending-result mechanism if feasible in the existing architecture.

Expected behavior:

Assessment completed
↓
Attempt API upload

If successful:
mark as synced

If failed:
save result securely/locally as Pending
↓
retry when connectivity returns
↓
prevent duplicate submissions
↓
mark as synced

Use a unique attempt identifier/idempotency mechanism if appropriate.

Do not silently discard completed assessments.

---

# PHASE 35 — DUPLICATE SUBMISSION PROTECTION

Prevent the same completed attempt from appearing multiple times because of retries.

Use:

* unique attempt ID
* idempotent server endpoint
* existing database constraints

or another professional solution compatible with the existing architecture.

---

# PHASE 36 — UI/UX

Do not unnecessarily redesign the entire TechWise360 visual identity.

Improve only what is necessary for:

* clarity
* responsiveness
* accessibility
* consistency
* usability

Keep teacher/student UI consistent.

Avoid:

* excessive gradients
* unnecessary animations
* oversized UI
* decorative clutter
* excessive modal dialogs

Use clear educational UI.

Make sure desktop and responsive layouts remain usable.

---

# PHASE 37 — VALIDATION AND ERROR HANDLING

Improve validation for relevant forms.

Examples:

* duplicate academic years
* invalid date ranges
* invalid enrollment
* no active academic year
* no active term
* invalid section
* duplicate enrollment
* missing required student data
* failed VR submission
* expired auth/session
* offline status

Provide understandable messages to the user.

Do not expose raw backend errors.

---

# PHASE 38 — DATABASE MIGRATIONS

If schema changes are required:

* use proper migrations
* preserve existing records
* avoid destructive resets
* do not wipe production data
* do not drop tables merely because migration is difficult

Handle data migration safely.

Historical academic-year data is especially important.

If the current development database contains inconsistent legacy values, migrate them carefully.

---

# PHASE 39 — SECURITY

Audit the changes for basic security.

Do not expose:

* API secrets
* database passwords
* service tokens
* Cloudflare secrets
* authentication secrets

Do not commit .env secrets.

Validate authorization server-side.

A student must not be able to submit a score for another student simply by modifying a client-side field.

A teacher must only access appropriate teacher functionality.

Do not trust Unity client score values blindly if server-side validation or integrity checks can reasonably be applied.

---

# PHASE 40 — UNITY CODE QUALITY

Refactor only where useful.

Prefer modular systems such as:

* Interaction Manager
* Assembly Manager
* Step Manager
* Mistake Tracker
* Assessment Manager
* Timer Manager
* Score Manager
* API/Network Manager
* Authentication/Session Manager
* UI Manager

Do not create giant scripts containing the entire simulation.

Avoid unnecessary singleton abuse.

Use ScriptableObjects/configuration assets where useful for:

* component metadata
* steps
* scoring penalties
* assembly definitions

Keep inspector references understandable.

Avoid hard-coded scene object names when better references are available.

---

# PHASE 41 — UNITY PERFORMANCE / META QUEST

The simulation should remain suitable for Meta Quest 2-class standalone hardware if that is the currently targeted device.

Prioritize:

* stable framerate
* reasonable polygon counts
* efficient materials
* optimized lighting
* baked lighting where appropriate
* texture compression
* reduced unnecessary transparency
* sensible physics
* object pooling only where useful
* avoiding expensive Update loops
* optimized UI
* avoiding unnecessary realtime shadows
* draw call reduction

Do not destroy visual quality unnecessarily.

Target a smooth VR experience.

Inspect the existing render pipeline and Quest/OpenXR setup before changing graphics settings.

---

# PHASE 42 — DESKTOP MODE

Do not break Desktop mode while improving VR.

Where interaction logic can be shared, separate:

Simulation Logic

from:

VR Input

and

Desktop Input

Both should operate on the same underlying assembly validation where practical.

---

# PHASE 43 — TESTING

Perform meaningful testing.

WEB TESTS

Test:

* login
* student login
* teacher login
* academic year creation
* historical academic year retrieval
* academic year activation
* term creation
* enrollment
* registration
* new section creation
* module opening
* module downloading
* assessment creation
* VR result retrieval
* class record
* reports

UNITY TESTS

Test:

* Practice assembly
* Practice disassembly
* step progression
* highlights
* mistakes
* end review
* Desktop interaction
* VR interaction
* Assessment assembly
* Assessment disassembly
* timer
* Done button
* mistake calculation
* scoring
* result screen
* API submission
* failed network submission
* retry
* duplicate prevention

INTEGRATION TEST

Perform the following complete flow:

1. Student logs into TechWise360 website.
2. Student launches/opens VR simulation.
3. VR identifies the correct authenticated student.
4. Student starts actual PC Assembly assessment.
5. Timer starts.
6. Student assembles PC.
7. Student receives NO solution hints.
8. Student presses Done.
9. Timer stops.
10. Simulation validates the build.
11. Mistakes are generated.
12. Score is calculated.
13. Results appear.
14. Result is submitted to backend.
15. Teacher logs into web portal.
16. Teacher opens class record.
17. Student VR assessment score appears.
18. Teacher opens reports.
19. Assessment result can be retrieved/filterable.
20. Historical academic year data remains intact.

This end-to-end flow is one of the most important acceptance tests.

---

# PHASE 44 — BUILD THE WEB APPLICATION

After implementation:

Run the project's existing:

* lint
* typecheck
* tests
* production build

Fix errors.

Do not deploy a knowingly broken build.

If warnings indicate actual issues, resolve them.

---

# PHASE 45 — BUILD THE UNITY VR APPLICATION

Inspect the project's existing target platform.

Build the updated simulation using the correct existing configuration.

For Meta Quest, verify where applicable:

* Android build target
* OpenXR/Oculus settings
* ARM64
* correct API level
* required XR plugins
* controller bindings
* scenes in build
* required permissions
* appropriate graphics API
* correct package identifiers

Do not arbitrarily change production identifiers.

If an APK/build artifact is normally generated by the project workflow, generate the updated build.

Also verify Desktop build if it is part of the project deliverables.

---

# PHASE 46 — CLOUDFLARE DEPLOYMENT

The web application is already intended to be published through Cloudflare.

Inspect the existing repository for:

* wrangler.toml
* wrangler.json/jsonc
* Cloudflare Pages configuration
* Cloudflare Workers configuration
* deployment scripts
* package.json scripts
* CI/CD workflows

Use the CURRENT deployment architecture.

Do not create a second Cloudflare project unnecessarily.

Build the production application.

Deploy/update the existing Cloudflare-hosted TechWise360 website.

If deployment authentication already exists in the environment, perform the deployment.

If it does not exist, complete everything that can be completed without inventing credentials and clearly identify the exact command/action needed.

Do not expose Cloudflare secrets in logs or commits.

After deployment, verify that the published application loads correctly.

Check important routes.

---

# PHASE 47 — GIT

After implementation and successful testing:

Review:

git status
git diff

Do not commit:

* secrets
* generated caches
* unnecessary Unity Library files
* Temp
* Logs
* machine-specific files
* node_modules
* environment secrets
* build junk that the repository intentionally ignores

Use the existing .gitignore appropriately.

Create a professional commit.

Suggested commit message:

feat: integrate academic year management and VR assessment workflow

If changes are large and logically separable, multiple meaningful commits are acceptable.

If the existing Git remote is configured and credentials permit:

push to the correct existing branch/remote.

Do NOT create a different repository.

Do NOT force-push unless absolutely necessary.

---

# PHASE 48 — DOCUMENT WHAT YOU CHANGED

At the end, provide a concise implementation report containing:

## Completed

List each major requirement implemented.

## Website Changes

Describe:

* Academic Year
* Terms
* Enrollment
* Sections
* Modules
* Assessment
* Class record
* Reporting

## VR Changes

Describe:

* Practice Mode
* Assessment Mode
* Done button
* mistake system
* scoring
* step guidance
* controls
* interaction fixes
* API integration

## Database Changes

List:

* migrations
* tables/models
* relationships

## API Changes

List new or modified endpoints.

## Authentication

Explain how website → VR identity works.

## Cloudflare

State:

* build status
* deployment status
* deployed project/site if available

## Unity Build

State:

* target
* build status
* output path

## Tests

List tests executed and results.

## Git

Provide:

* branch
* commit hash
* push status

## Remaining / Pending Adviser Decision

Only include genuinely unresolved requirements such as:

* manual screw installation if still awaiting adviser approval

Do not list unfinished technical work as completed.

---

# PRIORITY ORDER

If the current codebase requires prioritization, use this order:

P0 — must work

1. Existing system must build.
2. Existing Unity interaction bugs.
3. PC Assembly/Disassembly functional.
4. Practice vs Assessment separation.
5. Done button.
6. Assessment validation.
7. Mistake tracking.
8. Timer.
9. Scoring.
10. VR result API.
11. Teacher class record integration.
12. Academic Year preservation.
13. Term relationships.
14. Enrollment relationships.
15. Shared web/VR identity.
16. Cloudflare production deployment.

P1 — important

17. Practice step panel.
18. Practice highlights/arrows.
19. Practice mistake review.
20. Section flexibility.
21. Remove redundant Full Name input.
22. Module download/offline improvement.
23. Failed-result synchronization.
24. Weak Area Analysis integration.

P2 — polish

25. Practice Mode chalkboard text.
26. Desktop controls guide.
27. Meta Quest 3D controller guide.
28. Additional UX polish.

Pending:

29. Manual screw mechanics — implement only if already required or supported; otherwise structure for later adviser decision.

---

# IMPORTANT RULES

Do not simply tell me what I should change.

MAKE THE CHANGES.

Do not stop after producing an audit.

Do not return only code snippets.

Edit the actual project files.

Use the existing architecture whenever reasonably possible.

Do not silently remove existing features.

Do not wipe historical data.

Do not rewrite the entire project without justification.

Do not introduce unnecessary frameworks.

Do not use placeholder implementations when the real backend already exists.

Do not fabricate successful tests.

Do not fabricate successful deployments.

Do not fabricate Git pushes.

When something fails, investigate and fix the root cause.

Continue working through errors until the project is in the most complete working state possible.

---

# DEFINITION OF DONE

The implementation should be considered complete when this demonstration works:

**Teacher creates a new Academic Year**
→ old Academic Year remains accessible
→ teacher creates/selects its Term
→ student is enrolled into the correct Academic Year + Term + Grade + Section
→ student logs into TechWise360
→ student enters the VR simulation using the same identity
→ student can use guided Practice Mode
→ student starts an actual PC Assembly/Disassembly assessment
→ simulation provides no answer hints
→ timer runs
→ student finishes
→ student presses DONE
→ system checks the build
→ mistakes are calculated
→ score is calculated from accuracy/mistakes and time
→ result appears to the student
→ result is securely submitted
→ teacher opens TechWise360 class record
→ student's VR result appears automatically
→ teacher can retrieve it in reports
→ previous academic years and historical records still exist
→ production web build succeeds
→ Unity build succeeds
→ published Cloudflare site is updated
→ tested changes are committed and pushed to the existing Git repository when credentials permit.

Begin by inspecting the entire repository and current implementation, then execute the work systematically until the above acceptance criteria are satisfied.
