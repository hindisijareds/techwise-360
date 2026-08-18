# TechWise 360 System Presentation Script

## How To Use This Script

Use this as your speaking guide for the demo. The main script is written like something you can read out loud, but you can also shorten it depending on your allotted time.

Important security wording:

- Say that passwords are handled by Supabase Authentication and stored as password hashes.
- Do not say that the system "encrypts passwords" manually. For login systems, secure password hashing is the correct method because the original password should not be recoverable.
- The VR simulator does not store the student's password. It stores the login session and queues attempt results locally for offline sync.

---

## 1. Opening Script

Good day everyone. Today I will present our system, TechWise 360.

TechWise 360 is an integrated learning platform for ICT students. It combines two major parts: a web portal for teachers and students, and a VR simulation for PC assembly and disassembly practice.

The purpose of the system is to make ICT learning more organized, interactive, and trackable. Instead of students only reading lessons or watching demonstrations, they can study modules in the web portal, answer checks and assessments, practice in a VR environment, and have their progress and competition attempts recorded for the teacher.

The system is called TechWise 360 because it covers the full learning cycle: learning, practicing, competing, monitoring progress, and giving recognition through badges and certificates.

---

## 2. System Overview

TechWise 360 has two connected platforms.

First is the WebPortal. This is where students create accounts, log in, open lessons, answer assessments, track their progress, view achievements, and see their VR leaderboard records.

The teacher also uses the WebPortal to approve student accounts, manage learners, create and publish lessons, upload learning materials, monitor completion, review reports, manage achievements, and create VR competitions.

Second is the VR simulation. This is where students can practice PC assembly and disassembly in an interactive environment. The VR simulation supports desktop mode and VR mode, so students can use it with normal keyboard and mouse controls or with VR equipment when available.

The two parts are connected through online APIs. When the student completes a VR competition attempt, the result is sent to the WebPortal and appears in the teacher dashboard and student leaderboard.

---

## 3. Main System Flow

The basic flow of the system is:

1. A student registers an account in the WebPortal.
2. The teacher reviews and approves the student's account.
3. Once approved, the student can log in and access their dashboard.
4. The teacher creates terms, modules, lessons, practice checks, assessments, and VR competitions.
5. The student studies the lesson content and submits checks or assessments.
6. The student opens the VR simulation to practice or compete in PC assembly and disassembly.
7. The system records progress, scores, completion, badges, certificates, and leaderboard attempts.
8. The teacher monitors the student's progress through reports, completion monitoring, achievements, and VR leaderboard pages.

This means the system is not only for content delivery. It also supports practice, evaluation, and progress monitoring.

---

## 4. WebPortal Student Side Demo Script

On the student side, the first step is account creation.

The student creates an account by entering personal information such as their name, email, grade level, section, adviser, phone number, username, and password. The form validates important details. For example, the password must be strong, and the phone number must follow the required format.

After registration, the account is not immediately allowed to access the full system. It must first be approved by the teacher. This is important because it prevents unknown users from entering the learning platform.

Once the teacher approves the account, the student can log in.

Inside the student dashboard, there are several main pages.

The Home page gives the student a quick overview of their learning status.

The Learn page shows the modules and lessons published for the student's grade level and active term. The student can open a module, read lesson content, access uploaded files, and continue lesson activities.

The Assessments page contains practice checks and formal assessments. The intended flow is that students complete the required learning activities in order. This avoids confusion because the system can guide students from pre-assessment, to lessons, to practice checks, and then to final assessments.

The Progress page shows completion by module and lesson. This is where students can see what they have finished, what is in progress, and what is not yet started.

The VR Leaderboard page shows VR competition records, such as assembly and disassembly attempts, scores, mistakes, and time records.

The Achievements page shows badges and certificates earned by the student.

The Settings page allows the student to manage profile information and account settings.

---

## 5. WebPortal Teacher Side Demo Script

On the teacher side, the dashboard is designed for managing the whole class.

The teacher can review pending student accounts and approve or reject them. This makes sure only valid students can access the system.

The Students page lets the teacher view approved students, filter them by grade and section, check performance, and open student profiles.

The Lessons page is where the teacher manages modules and lessons. The teacher can create learning modules, add lessons, publish content, and upload lesson files such as PDF, DOCX, PPTX, and MP4 files.

The Terms page controls the active term or quarter, so lessons and progress can be organized by the current school period.

The Assessments page manages practice checks and assessments connected to lessons.

The Reports page gives the teacher a wider view of student progress. It includes completion data, performance insights, support indicators, charts, and export options.

The VR Leaderboard page shows student VR attempts. This includes assembly and disassembly attempts, scores, time, mistakes, and competition records.

The Evaluation page collects feedback and evaluation data from users.

The Achievements page lets the teacher view student awards, issue certificates and badges, and track which students already have achievements.

The Settings page lets the teacher update account and dashboard settings.

---

## 6. VR Simulation Demo Script

The VR simulation is the interactive part of TechWise 360.

When the simulation opens, the student sees the TechWise 360 main menu. The student can choose between Desktop mode and VR mode.

Desktop mode allows students to use keyboard, mouse, and the on-screen crosshair. VR mode is intended for use with a headset and controllers.

The main menu includes Start, Practice Mode, Competition, Controls, Settings, and Quit.

Start opens the guided simulation flow. This is useful for students who are still learning the environment.

Practice Mode lets the student practice PC assembly or disassembly without affecting leaderboard competition records. This is where students can learn the process first.

Competition Mode is for recorded attempts. In competition mode, the system tracks the timer, progress, mistakes, and final score. The result is then submitted to the WebPortal.

The Controls screen explains keyboard, mouse, and VR controls so students know how to move, look around, grab parts, rotate parts, and interact with the simulation.

Inside the simulation, students work with PC components such as the CPU, RAM, M.2 drive, graphics card, storage drive, power supply, CPU cooler, and motherboard.

For assembly, the expected order is:

1. CPU
2. RAM
3. M.2
4. CPU Cooler
5. Motherboard
6. GPU
7. Storage
8. PSU

For disassembly, the expected order is reversed:

1. CPU Cooler
2. PSU
3. Storage
4. GPU
5. M.2
6. RAM
7. CPU
8. Motherboard

This order is important because the competition scoring checks whether the student performs the task correctly.

---

## 7. VR Scoring And Mistakes Script

In VR competition mode, the system records:

- the simulation type, either assembly or disassembly;
- the competition mode;
- the expected order of components;
- the student's completed order;
- the number of wrong-order actions;
- the number of wrong-part actions;
- the duration;
- the final score;
- and whether the attempt was queued offline.

Mistakes happen when the student performs the component process in the wrong order or places the wrong part into a slot.

The score starts from 100 percent. The system then subtracts penalties. Wrong-order and wrong-part mistakes reduce the score. The system also applies a time penalty if the attempt goes beyond the target time.

The target time is different depending on the mode. Assembly has a longer target time, while disassembly has a shorter target time.

At the end of the competition, the student sees a completion summary showing the score, time, mistakes, and sync status.

---

## 8. Offline Mode And Sync Script

One important feature of the VR simulation is offline attempt saving.

If the student completes a VR competition while the connection is working, the simulator sends the attempt directly to the WebPortal.

If the internet is not available, or if the session cannot sync at that moment, the attempt is saved locally on the computer instead of being lost.

The local queue is stored by the Unity application as a JSON file named `techwise-vr-attempt-queue.json` inside Unity's persistent data folder. This queue contains the VR attempt payload, not the student's password.

The simulator checks for pending uploads and tries to sync again. It waits briefly after startup, then retries periodically. The current implementation retries every 30 seconds when the student is logged in and the internet is reachable.

When the sync succeeds, the attempt is removed from the local queue and appears on the WebPortal dashboard or leaderboard.

This means the student can still complete a competition attempt even if the internet connection is temporarily unstable. The result will sync later once the system is online again.

---

## 9. WebPortal And VR Data Flow Script

The data flow works like this:

The teacher creates learning content and VR competitions in the WebPortal.

The WebPortal stores the data in Supabase. Supabase is the backend database and authentication service used by the system.

The student dashboard reads only the content that applies to the student's grade level, active term, and published lessons.

When the student opens the VR simulation and logs in, the simulator receives the student's profile and session. It can then load available competitions and submit completed attempts.

When a student finishes a VR competition, Unity sends the attempt to the WebPortal API.

The API checks that the student is approved, checks that the competition is active or valid, checks that the competition matches the student's grade and section, validates the score, duration, and mistakes, and then saves the attempt.

After saving, the teacher and student dashboards can use the attempt for leaderboard and report displays.

---

## 10. Security Script

For security, TechWise 360 uses role-based access and server-side validation.

The system has student and teacher roles. Teacher-only pages and APIs require an approved teacher account. Student-only pages and APIs require an approved student account.

Student accounts are not automatically active after registration. They must be approved by the teacher first. Rejected or inactive students cannot access the protected student features.

For passwords, the system uses Supabase Authentication. Passwords are not stored in the frontend, and they are not stored by our custom application database as readable text. Supabase Authentication handles the secure password process and stores password hashes.

This is important because password hashing is safer than reversible encryption for login passwords. If a password is hashed, the original password should not be readable by the application.

The WebPortal uses session tokens after login. API requests include a bearer token, and the server checks that token before returning protected data.

Sensitive backend keys are kept as Cloudflare environment variables. The Supabase service role key is used only inside Cloudflare Pages Functions and is never placed in browser code.

Uploaded lesson files use a private Supabase Storage bucket. Students can only open lesson files for published lessons that match their grade level and current access.

The VR simulator does not save the student's password. It stores the active session and profile locally so it can sync VR attempts. When the student logs out, the local session is cleared.

For offline mode, the local queue stores attempt results such as score, time, mistakes, and component order metadata. It does not store the password.

---

## 11. Teacher Dashboard Detailed Script

Now I will explain the teacher dashboard more specifically.

The teacher dashboard is the management center of the system.

In the Students section, the teacher can view students by grade and section. This is useful because the system supports Grade 9 and Grade 10, with sections assigned under each grade level.

For Grade 9, the sections are Ylang Ylang, Dama De Noche, and Sampaguita.

For Grade 10, the sections are Rosal, Lavender, and Tulip.

When the teacher selects all grades, the section filter is disabled because the system is already showing all students. When a specific grade is selected, the section filter becomes available.

This prevents confusion and makes the dashboard easier to use.

In the Lessons section, the teacher can publish modules and lessons by grade and active term. Students only see lessons that are published and assigned to their grade.

In the Reports section, the teacher can filter by grade and section to monitor completion, student support needs, performance, and learning progress.

In the Achievements section, the teacher can view student certificates and badges, issue awards, and open student profiles.

In the VR Leaderboard section, the teacher can review VR competition results. The leaderboard is based on actual attempts submitted by students from the simulation.

---

## 12. Student Learning Flow Script

For students, the learning flow is designed to be simple.

First, the student logs in.

Second, the student opens the Learn page and starts the available module.

Third, the student completes lessons, reads uploaded materials, and answers practice checks.

Fourth, the student can use the VR simulation to practice the real PC assembly and disassembly workflow.

Fifth, after practicing, the student can join a VR competition if the teacher has opened one.

Sixth, the student's score, progress, badges, certificates, and leaderboard records become visible in the student dashboard.

This flow helps students understand what to do next instead of jumping randomly between lessons, assessments, and VR activities.

---

## 13. Suggested Live Demo Order

For the live demo, I recommend this order:

1. Open the WebPortal login page.
2. Explain student registration and teacher approval.
3. Log in as teacher.
4. Show the Students page and filters.
5. Show Lessons, Reports, Achievements, and VR Leaderboard.
6. Log in as student.
7. Show Learn, Assessments, Progress, VR Leaderboard, and Achievements.
8. Open the VR simulation.
9. Show Desktop and VR mode selection.
10. Open Practice Mode and explain assembly/disassembly.
11. Open Competition Mode and explain scoring.
12. Explain offline sync using the pending upload system.
13. Return to the WebPortal and show where results appear.

---

## 14. Short 3 Minute Version

Good day everyone. This is TechWise 360, an integrated learning system for ICT students.

The system has two main parts: the WebPortal and the VR simulation.

The WebPortal is used by students and teachers. Students can register, log in, view lessons, answer checks and assessments, track progress, view achievements, and see VR leaderboard records. Teachers can approve accounts, manage students, publish modules and lessons, upload materials, view reports, issue certificates and badges, and manage VR competitions.

The VR simulation is used for PC assembly and disassembly. It supports both Desktop mode and VR mode. Students can practice first, then enter competition mode where the system records time, progress, mistakes, and score.

In competition mode, the system checks the expected order of components. For assembly, students follow the proper installation order. For disassembly, they follow the reverse removal order. If the student puts a component in the wrong order or uses the wrong part, the system records a mistake. The final score is based on mistakes and time.

The VR simulation also supports offline saving. If the internet connection is not available, the attempt is saved locally and placed in a queue. When the student is logged in again and the internet is available, the system automatically syncs the attempt to the WebPortal.

For security, TechWise 360 uses Supabase Authentication. Passwords are not stored by our frontend or custom database as readable text. Supabase handles password hashing. The system also uses role-based access, so teacher APIs require teacher accounts and student APIs require approved student accounts. Sensitive backend keys are stored only in Cloudflare environment variables and are not exposed to the browser.

Overall, TechWise 360 helps students learn, practice, compete, and track their progress, while giving teachers tools to manage learning activities and monitor performance.

---

## 15. Q And A Guide

### Question: Does the system encrypt passwords?

Suggested answer:

The system uses Supabase Authentication for passwords. Passwords are not stored in our frontend or custom database as readable text. Supabase stores password hashes, which is the secure standard for login systems. Hashing is different from encryption because it is not meant to be reversed.

### Question: What happens if the internet disconnects during VR competition?

Suggested answer:

The VR simulator saves the completed attempt locally in an offline queue. It stores the attempt result, such as score, time, mistakes, and component order metadata. When the student is logged in and the internet is available again, the simulator retries syncing the attempt to the WebPortal.

### Question: Can students access the system immediately after registering?

Suggested answer:

No. After registration, the student account must be approved by the teacher. This prevents unauthorized accounts from accessing protected lessons, assessments, and student features.

### Question: How does the teacher monitor student progress?

Suggested answer:

The teacher dashboard includes student lists, completion monitoring, reports, achievements, and the VR leaderboard. These pages show lesson completion, assessment results, awards, and VR attempts.

### Question: How are VR scores calculated?

Suggested answer:

The competition score starts from 100 percent. The system subtracts penalties for wrong order, wrong part, and time beyond the target. It also records mistakes and completed steps, then saves the result to the WebPortal.

### Question: Does the VR simulator store passwords?

Suggested answer:

No. The VR simulator sends the login to the WebPortal API and stores the active session and student profile for syncing. It does not store the student's password. Logging out clears the saved session.

### Question: What sensitive information is protected?

Suggested answer:

Protected information includes student accounts, profiles, lesson access, submitted assessments, VR attempts, certificates, and badges. These are accessed through authenticated APIs with role checks. Backend keys are kept server-side as environment variables.

### Question: Why is there offline mode?

Suggested answer:

Offline mode is important because internet connection can be unstable during demonstrations or class use. The system prevents completed VR attempts from being lost by saving them locally and syncing them later.

### Question: What makes TechWise 360 different from a normal LMS?

Suggested answer:

TechWise 360 combines a learning portal with an interactive VR simulation. Students do not only read lessons; they can practice PC assembly and disassembly, compete, and have their practical performance recorded in the teacher dashboard.

---

## 16. Closing Script

To conclude, TechWise 360 is designed to support ICT learning from start to finish.

It helps students learn through modules, practice through assessments and VR activities, compete through recorded simulations, and track progress through dashboards.

It also helps teachers manage learners, publish lessons, monitor completion, review reports, and recognize student achievements.

The system includes security controls such as approved student access, teacher-only APIs, private file storage, session-based authentication, and password handling through Supabase Authentication.

With the WebPortal and VR simulation working together, TechWise 360 provides a more complete, interactive, and trackable learning experience for ICT students.

Thank you.

---

## 17. Technical Cheat Sheet

Use this section only if the panel asks about the implementation.

### Main technologies

- WebPortal frontend: HTML, CSS, and JavaScript.
- WebPortal backend: Cloudflare Pages Functions.
- Database and authentication: Supabase.
- File storage: Supabase Storage.
- VR simulation: Unity.
- VR interaction: Unity XR Interaction Toolkit.

### Important WebPortal files

- `index.html`: login page.
- `create-account.html`: student registration.
- `teacher-dashboard.html`: teacher portal.
- `student-dashboard.html`: student portal.
- `WebPortal/app.js`: main frontend logic.
- `WebPortal/functions/api`: server-side API functions.
- `WebPortal/supabase/schema.sql`: database structure, indexes, and security policies.

### Important VR files

- `TechWisePortalClient.cs`: connects Unity to the WebPortal API.
- `TechWiseSessionStore.cs`: stores and clears the Unity login session.
- `TechWiseOfflineAttemptQueue.cs`: saves failed or offline VR attempts locally and retries syncing.
- `TechWiseAttemptRecorder.cs`: records competition progress, mistakes, score, time, and sync status.
- `TechWiseSimulationModeManager.cs`: defines assembly/disassembly modes and expected component order.

### Main database records

- `profiles`: teacher and student account profiles.
- `quarters`: active terms.
- `modules`: learning modules.
- `lessons`: lesson records.
- `lesson_sections`: lesson content sections.
- `lesson_questions`: lesson or assessment questions.
- `lesson_files`: uploaded learning materials.
- `lesson_progress`: student progress per lesson.
- `lesson_attempts`: submitted lesson or assessment answers.
- `student_badges`: awarded badges.
- `student_certificates`: awarded certificates.
- `vr_competitions`: teacher-created VR competitions.
- `vr_simulation_attempts`: submitted VR competition attempts.
- `teacher_notifications` and `student_notifications`: dashboard notifications.
- `evaluation_survey_responses`: evaluation feedback.

### API security checks

The WebPortal APIs check the logged-in user before returning data.

- Teacher APIs call teacher access checks.
- Student APIs call student access checks.
- Student APIs require the student profile to be approved.
- Lesson files are checked before access.
- VR attempts are validated before saving.
- Competitions are checked by status, grade level, section, and simulation type.

### Supabase security

The Supabase schema enables row-level security on important tables, including profiles, lesson progress, lesson attempts, badges, certificates, VR competitions, VR attempts, notifications, and evaluation tables.

This adds another layer of protection so users only access records allowed by their role and status.

### Password and sensitive data answer

If someone asks, say:

"Passwords are handled by Supabase Authentication. The application does not store plain-text passwords. Supabase stores password hashes, which is the correct secure approach for passwords. Backend service keys are stored only as Cloudflare environment variables, and the service role key is never exposed in browser code."

### Offline sync answer

If someone asks, say:

"The VR simulator can finish a competition even if the internet drops. It saves the attempt locally as a queued JSON record containing the attempt result, not the password. When the student is logged in and the internet is available again, the queue automatically retries syncing to the WebPortal."

