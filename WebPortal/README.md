# TechWise 360 Web Portal

Cloudflare Pages portal for TechWise 360 student registration, teacher approval, student management, term management, module/lesson management, and student dashboards.

## What It Includes

- `index.html`: student/teacher login.
- `create-account.html`: student registration form with strong password and Phone Number validation.
- `teacher-dashboard.html`: teacher dashboard for students, sections, terms, lessons, reporting, and achievements.
- `student-dashboard.html`: mockup-style student dashboard with progress, lessons, and profile details.
- `functions/api/*`: Cloudflare Pages Functions backed by Supabase.
- `supabase/schema.sql`: online Supabase migration for profiles, terms, modules, lessons, lesson files, and progress.
- `supabase/seed-teacher.sql`: first teacher account plus starter terms/modules/lessons.

## Online Supabase Setup

1. Open the existing Supabase project connected to Cloudflare.
2. Go to `SQL Editor`.
3. Run `supabase/schema.sql`.
   - The script creates/updates the private Supabase Storage bucket `lesson-files` for PDF, DOCX, PPTX, and MP4 lesson uploads up to 100MB.
4. Create the teacher user in `Authentication > Users > Add user` if it does not exist.
5. Copy the teacher user's UUID and email into `supabase/seed-teacher.sql`.
6. Run `supabase/seed-teacher.sql`.
7. In `Authentication > Providers > Email`, keep email confirmation disabled for this capstone version because teacher approval is the login gate.

The migration keeps existing student accounts and copies legacy `cp_number` values into the new `phone_number` column.

For an already deployed database, run `supabase/migrations/20260822_teacher_sections.sql` before deploying the updated portal. It is idempotent, imports the six existing Grade 9/10 sections, links matching student profiles, and creates assignment history. If the project contains legacy names such as `1`, `Section 1`, or `Eagle`, run `supabase/migrations/20260822130000_cleanup_legacy_sections.sql` immediately afterward to remap those students and remove obsolete 2026-2027 section rows.

## Cloudflare Pages Setup

The existing Pages project name is:

```powershell
techwise360-web-portal
```

Add or confirm these Cloudflare environment variables:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`
- `PUBLIC_SITE_URL` set to the deployed portal URL, for example `https://techwise360-web-portal.pages.dev`

The service role key is used only by Pages Functions and must never be placed in browser code.
For password reset emails, Supabase must also point to the online portal:

- Set Supabase `Authentication > URL Configuration > Site URL` to the deployed portal URL, not `localhost`.
- Add the same deployed URL and the deployed `/reset-password.html` URL to `Authentication > URL Configuration > Redirect URLs`.

## Local Development

1. Copy `.dev.vars.example` to `.dev.vars`.
2. Fill in the Supabase values from the existing online Supabase project.
3. Install dependencies and run:

```powershell
npm install
npm run dev
```

Open the local URL printed by Wrangler.

## Deploy

Deploy the current folder directly to the existing Cloudflare Pages project:

```powershell
npm run deploy
```

The deploy script always targets the Cloudflare Pages `production` branch, so the
main `https://techwise360-web-portal.pages.dev` domain is updated instead of creating
a branch preview. It also runs the JavaScript checks before uploading.

If you want to run Wrangler directly without the npm script:

```powershell
npx wrangler pages deploy . --project-name techwise360-web-portal --branch production --commit-dirty=true
```

## Automated System Screenshots

The screenshot tool captures the public pages, every teacher and student dashboard section, achievement sub-tabs, and a combined PDF softcopy.

1. Copy `capture.env.example` to `capture.env`.
2. Add one approved teacher account and one approved student account. This local file is ignored by Git.
3. Install the capture browser once, then run the screenshots:

```powershell
npm run screenshots:install
npm run screenshots
```

Each run creates a timestamped folder under `artifacts/system-screenshots/` containing the PNG files and `manifest.json`. The combined instructor-ready file is written to `output/pdf/TechWise360-System-Softcopy.pdf`.

To rebuild only the PDF from the latest completed screenshot run:

```powershell
npm run screenshots:pdf
```

## Test Checklist

- Register a student with a valid Phone Number like `09123456789`.
- Confirm weak passwords are rejected and strong passwords are accepted.
- Try duplicate username/email and confirm validation appears.
- Log in as teacher and approve/reject pending students.
- Use Student Management to search, filter, export, edit, deactivate/reactivate, and reset students.
- Use Sections to create a draft, activate it, edit its adviser/capacity, transfer a student, and verify the activity log.
- Confirm archived and draft sections never appear on student registration.
- Confirm inactive students are blocked from logging in.
- Confirm student badges and certificates render after running the latest schema and seed scripts.
- Create or activate a term.
- Create modules and lessons, then publish lessons.
- Upload PDF/DOCX/PPTX/MP4 lesson files from the teacher Lessons page and confirm Recent Uploads updates.
- Preview uploaded files as a teacher and confirm approved students can open files only for published lessons in their grade.
- Confirm lesson scheduled/due dates appear in the Content Calendar.
- Log in as an approved student and confirm active-term lessons and dashboard progress render.
- Check desktop and phone screen widths against the mockup layout.
