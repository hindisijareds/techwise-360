-- 1. Create the teacher user in Supabase Dashboard:
--    Authentication > Users > Add user
--
-- 2. Copy the new user's UUID and email, then replace the placeholders below.
--    Keep role = 'teacher' and status = 'approved'.

insert into public.profiles (
  id,
  role,
  status,
  username,
  email,
  full_name,
  first_name,
  last_name
) values (
  'd128e2c1-57e2-4037-804a-7f47ba711247',
  'teacher',
  'approved',
  'teacher',
  'jaredestabillo04@gmail.com',
  'Teacher Account',
  'Teacher',
  'Account'
)
on conflict (id) do update set
  role = excluded.role,
  status = excluded.status,
  username = excluded.username,
  email = excluded.email,
  full_name = excluded.full_name,
  first_name = excluded.first_name,
  last_name = excluded.last_name,
  updated_at = now();

alter table public.profiles add column if not exists student_number text;
alter table public.profiles add column if not exists avatar_path text;

create unique index if not exists profiles_student_number_unique
on public.profiles (student_number)
where student_number is not null and student_number <> '';

do $$
begin
  alter table public.quarters drop constraint if exists quarters_name_check;
  alter table public.quarters add column if not exists status text not null default 'active';
  alter table public.quarters drop constraint if exists quarters_status_check;
  update public.quarters set name = 'T1', title = '1st Term' where name = 'Q1';
  update public.quarters set name = 'T2', title = '2nd Term' where name = 'Q2';
  update public.quarters set name = 'T3', title = '3rd Term' where name = 'Q3';
  delete from public.quarters where name = 'Q4';
  alter table public.quarters
    add constraint quarters_name_check
    check (name in ('T1', 'T2', 'T3'));
  alter table public.quarters
    add constraint quarters_status_check
    check (status in ('active', 'archived'));
end $$;

insert into public.quarters (name, title, school_year, is_active, created_by)
values
  ('T1', '1st Term', '2026-2027', false, 'd128e2c1-57e2-4037-804a-7f47ba711247'),
  ('T2', '2nd Term', '2026-2027', false, 'd128e2c1-57e2-4037-804a-7f47ba711247'),
  ('T3', '3rd Term', '2026-2027', false, 'd128e2c1-57e2-4037-804a-7f47ba711247')
on conflict (name, school_year) do update set
  title = excluded.title,
  created_by = coalesce(public.quarters.created_by, excluded.created_by),
  updated_at = now();

update public.quarters
set is_active = true
where name = 'T1'
  and school_year = '2026-2027'
  and not exists (
    select 1 from public.quarters active_quarter
    where active_quarter.is_active = true
  );

-- Compatibility patch for existing online projects that were seeded before
-- lesson schedules and structured lesson authoring tables were added.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
  'student-avatars',
  'student-avatars',
  false,
  2097152,
  array[
    'image/jpeg',
    'image/png',
    'image/webp'
  ]
)
on conflict (id) do update set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

alter table public.lessons add column if not exists scheduled_date date;
alter table public.lessons add column if not exists due_date date;

create table if not exists public.lesson_files (
  id uuid primary key default gen_random_uuid(),
  lesson_id uuid references public.lessons(id) on delete set null,
  storage_path text not null unique,
  original_filename text not null,
  mime_type text not null,
  size_bytes integer not null check (size_bytes between 1 and 104857600),
  uploaded_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now()
);

create table if not exists public.lesson_sections (
  id uuid primary key default gen_random_uuid(),
  lesson_id uuid not null references public.lessons(id) on delete cascade,
  section_type text not null default 'paragraph' check (section_type in ('paragraph', 'key_points', 'example', 'quick_tip', 'resource', 'steps', 'safety_note', 'vocabulary', 'summary', 'reflection')),
  title text,
  body text,
  media_url text,
  sort_order integer not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.lesson_questions (
  id uuid primary key default gen_random_uuid(),
  lesson_id uuid not null references public.lessons(id) on delete cascade,
  question_type text not null default 'multiple_choice' check (question_type in ('multiple_choice', 'true_false', 'short_answer', 'ordering', 'identification')),
  prompt text not null,
  choices jsonb not null default '[]'::jsonb,
  correct_answer text not null,
  hint text,
  explanation text,
  points integer not null default 1 check (points between 1 and 100),
  sort_order integer not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.teacher_notifications (
  id uuid primary key default gen_random_uuid(),
  teacher_id uuid not null references public.profiles(id) on delete cascade,
  student_id uuid references public.profiles(id) on delete set null,
  event_type text not null check (event_type in ('student_registered', 'lesson_completed', 'practice_submitted', 'assessment_submitted', 'badge_awarded', 'certificate_awarded')),
  title text not null,
  body text not null,
  entity_type text,
  entity_id uuid,
  metadata jsonb not null default '{}'::jsonb,
  read_at timestamptz,
  created_at timestamptz not null default now()
);

create table if not exists public.student_notifications (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  teacher_id uuid references public.profiles(id) on delete set null,
  event_type text not null check (event_type in ('account_approved', 'lesson_published', 'lesson_updated', 'assessment_published', 'assessment_updated', 'badge_awarded', 'certificate_awarded')),
  title text not null,
  body text not null,
  entity_type text,
  entity_id uuid,
  metadata jsonb not null default '{}'::jsonb,
  read_at timestamptz,
  created_at timestamptz not null default now(),
  unique (student_id, event_type, entity_type, entity_id)
);

alter table public.student_certificates add column if not exists template_id text not null default 'achievement';
alter table public.student_certificates add column if not exists recipient_name text;
alter table public.student_certificates add column if not exists achievement_name text;
alter table public.student_certificates add column if not exists achievement_description text;
alter table public.student_certificates add column if not exists issue_date date;
alter table public.student_certificates add column if not exists formatted_issue_date text;
alter table public.student_certificates add column if not exists principal_name text;
alter table public.student_certificates add column if not exists teacher_name text;
alter table public.student_certificates add column if not exists certificate_number text;
alter table public.student_certificates add column if not exists notes text;
alter table public.student_certificates add column if not exists status text not null default 'issued';
alter table public.student_certificates add column if not exists certificate_data jsonb not null default '{}'::jsonb;
alter table public.student_certificates add column if not exists updated_at timestamptz not null default now();

create table if not exists public.teacher_settings (
  teacher_id uuid primary key references public.profiles(id) on delete cascade,
  notification_preferences jsonb not null default '{"student_accounts": true, "lesson_completions": true, "practice_submissions": true, "achievement_awards": true}'::jsonb,
  default_grade text default 'Grade 10' check (default_grade is null or default_grade in ('Grade 9', 'Grade 10')),
  default_landing_view text not null default 'overview' check (default_landing_view in ('overview', 'students', 'content', 'assessments', 'reports', 'evaluation')),
  compact_lessons boolean not null default false,
  report_default_range text not null default 'week' check (report_default_range in ('week', 'month')),
  report_export_format text not null default 'pdf' check (report_export_format in ('pdf', 'csv')),
  report_default_grade text check (report_default_grade is null or report_default_grade in ('Grade 9', 'Grade 10')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

do $$
begin
  alter table public.teacher_settings drop constraint if exists teacher_settings_default_landing_view_check;
  alter table public.teacher_settings
    add constraint teacher_settings_default_landing_view_check
    check (default_landing_view in ('overview', 'students', 'content', 'assessments', 'reports', 'evaluation'));
end $$;

create table if not exists public.evaluation_cycles (
  id uuid primary key default gen_random_uuid(),
  quarter_id uuid not null references public.quarters(id) on delete cascade,
  grade_level text not null check (grade_level in ('Grade 9', 'Grade 10')),
  pretest_lesson_id uuid references public.lessons(id) on delete set null,
  posttest_lesson_id uuid references public.lessons(id) on delete set null,
  survey_is_open boolean not null default false,
  created_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (quarter_id, grade_level)
);

create table if not exists public.evaluation_survey_responses (
  id uuid primary key default gen_random_uuid(),
  cycle_id uuid not null references public.evaluation_cycles(id) on delete cascade,
  respondent_id uuid not null references public.profiles(id) on delete cascade,
  respondent_role text not null check (respondent_role in ('student', 'teacher')),
  answers jsonb not null default '{}'::jsonb,
  comment text,
  submitted_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (cycle_id, respondent_id)
);

create table if not exists public.vr_competitions (
  id uuid primary key default gen_random_uuid(),
  title text not null,
  description text,
  simulation_type text not null default 'assembly' check (simulation_type in ('assembly', 'disassembly', 'both')),
  ranking_method text not null default 'score_time_mistakes' check (ranking_method in ('score_time_mistakes')),
  grade_level text check (grade_level is null or grade_level in ('Grade 9', 'Grade 10')),
  section text,
  quarter_id uuid references public.quarters(id) on delete set null,
  start_at timestamptz,
  end_at timestamptz,
  attempts_allowed integer check (attempts_allowed is null or attempts_allowed > 0),
  status text not null default 'active' check (status in ('draft', 'active', 'closed', 'archived')),
  created_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.vr_simulation_attempts (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  competition_id uuid references public.vr_competitions(id) on delete set null,
  simulation_type text not null check (simulation_type in ('assembly', 'disassembly')),
  score_percent numeric(5,2) not null check (score_percent between 0 and 100),
  duration_seconds integer not null check (duration_seconds >= 0),
  mistakes integer not null default 0 check (mistakes >= 0),
  status text not null default 'completed' check (status in ('in_progress', 'completed', 'qualified', 'disqualified')),
  metadata jsonb not null default '{}'::jsonb,
  started_at timestamptz,
  completed_at timestamptz not null default now(),
  created_at timestamptz not null default now()
);

create table if not exists public.badge_definitions (
  id uuid primary key default gen_random_uuid(),
  badge_key text not null unique,
  title text not null,
  description text,
  category text not null default 'achievement' check (category in ('achievement', 'completion', 'competition', 'participation', 'skill', 'custom')),
  color text not null default 'blue' check (color in ('blue', 'green', 'gold', 'purple', 'teal', 'orange', 'red')),
  icon text not null default 'award',
  status text not null default 'active' check (status in ('active', 'archived')),
  criteria jsonb not null default '{}'::jsonb,
  created_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.student_badges add column if not exists badge_id uuid references public.badge_definitions(id) on delete set null;
alter table public.student_badges add column if not exists category text;
alter table public.student_badges add column if not exists description text;
alter table public.student_badges add column if not exists icon text;
alter table public.student_badges add column if not exists reason text;
alter table public.student_badges add column if not exists source text not null default 'manual';
alter table public.student_badges add column if not exists updated_at timestamptz not null default now();

do $$
begin
  alter table public.student_badges drop constraint if exists student_badges_color_check;
  alter table public.student_badges
    add constraint student_badges_color_check
    check (color in ('blue', 'green', 'gold', 'purple', 'teal', 'orange', 'red'));
end $$;

create index if not exists teacher_notifications_teacher_recent_idx on public.teacher_notifications (teacher_id, created_at desc);
create index if not exists teacher_notifications_teacher_unread_idx on public.teacher_notifications (teacher_id, read_at) where read_at is null;
create index if not exists student_notifications_student_recent_idx on public.student_notifications (student_id, created_at desc);
create index if not exists student_notifications_student_unread_idx on public.student_notifications (student_id, read_at) where read_at is null;
create index if not exists teacher_settings_defaults_idx on public.teacher_settings (default_grade, default_landing_view);
create index if not exists evaluation_cycles_quarter_grade_idx on public.evaluation_cycles (quarter_id, grade_level);
create index if not exists evaluation_survey_responses_cycle_idx on public.evaluation_survey_responses (cycle_id, submitted_at desc);
create index if not exists evaluation_survey_responses_respondent_idx on public.evaluation_survey_responses (respondent_id);
create index if not exists student_badges_badge_id_idx on public.student_badges (badge_id);
create index if not exists vr_competitions_status_idx on public.vr_competitions (status, grade_level, section);
create index if not exists vr_competitions_quarter_idx on public.vr_competitions (quarter_id);
create index if not exists vr_simulation_attempts_student_idx on public.vr_simulation_attempts (student_id, completed_at desc);
create index if not exists vr_simulation_attempts_competition_idx on public.vr_simulation_attempts (competition_id, score_percent desc, duration_seconds asc, mistakes asc);
create index if not exists badge_definitions_status_idx on public.badge_definitions (status, category);

alter table public.teacher_notifications enable row level security;
alter table public.student_notifications enable row level security;
alter table public.teacher_settings enable row level security;
alter table public.evaluation_cycles enable row level security;
alter table public.evaluation_survey_responses enable row level security;
alter table public.vr_competitions enable row level security;
alter table public.vr_simulation_attempts enable row level security;
alter table public.badge_definitions enable row level security;

drop policy if exists "Teachers can manage badge definitions" on public.badge_definitions;
create policy "Teachers can manage badge definitions"
on public.badge_definitions
for all
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read active badge definitions" on public.badge_definitions;
create policy "Students can read active badge definitions"
on public.badge_definitions
for select
to authenticated
using (
  status = 'active'
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Teachers can manage VR competitions" on public.vr_competitions;
create policy "Teachers can manage VR competitions"
on public.vr_competitions
for all
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read visible VR competitions" on public.vr_competitions;
create policy "Students can read visible VR competitions"
on public.vr_competitions
for select
to authenticated
using (
  status in ('active', 'closed')
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
      and (vr_competitions.grade_level is null or vr_competitions.grade_level = profiles.grade_level)
      and (vr_competitions.section is null or vr_competitions.section = profiles.section)
  )
);

drop policy if exists "Teachers can read VR attempts" on public.vr_simulation_attempts;
create policy "Teachers can read VR attempts"
on public.vr_simulation_attempts
for select
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read class leaderboard attempts" on public.vr_simulation_attempts;
create policy "Students can read class leaderboard attempts"
on public.vr_simulation_attempts
for select
to authenticated
using (
  exists (
    select 1
    from public.profiles self_profile
    join public.profiles attempt_profile on attempt_profile.id = vr_simulation_attempts.student_id
    where self_profile.id = auth.uid()
      and self_profile.role = 'student'
      and self_profile.status = 'approved'
      and attempt_profile.role = 'student'
      and attempt_profile.status = 'approved'
      and attempt_profile.grade_level = self_profile.grade_level
      and coalesce(attempt_profile.section, '') = coalesce(self_profile.section, '')
  )
);

drop policy if exists "Teachers can read their own notifications" on public.teacher_notifications;
create policy "Teachers can read their own notifications"
on public.teacher_notifications
for select
to authenticated
using (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Teachers can update their own notifications" on public.teacher_notifications;
create policy "Teachers can update their own notifications"
on public.teacher_notifications
for update
to authenticated
using (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read their own notifications" on public.student_notifications;
create policy "Students can read their own notifications"
on public.student_notifications
for select
to authenticated
using (
  auth.uid() = student_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can update their own notifications" on public.student_notifications;
create policy "Students can update their own notifications"
on public.student_notifications
for update
to authenticated
using (
  auth.uid() = student_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
  )
)
with check (
  auth.uid() = student_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Teachers can read their own settings" on public.teacher_settings;
create policy "Teachers can read their own settings"
on public.teacher_settings
for select
to authenticated
using (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Teachers can update their own settings" on public.teacher_settings;
create policy "Teachers can update their own settings"
on public.teacher_settings
for update
to authenticated
using (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  auth.uid() = teacher_id
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
    and profiles.status = 'approved'
  )
);

drop policy if exists "Teachers can manage evaluation cycles" on public.evaluation_cycles;
create policy "Teachers can manage evaluation cycles"
on public.evaluation_cycles
for all
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read open evaluation cycles for their grade" on public.evaluation_cycles;
create policy "Students can read open evaluation cycles for their grade"
on public.evaluation_cycles
for select
to authenticated
using (
  survey_is_open = true
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.status = 'approved'
      and profiles.grade_level = evaluation_cycles.grade_level
  )
);

drop policy if exists "Teachers can read evaluation survey responses" on public.evaluation_survey_responses;
create policy "Teachers can read evaluation survey responses"
on public.evaluation_survey_responses
for select
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read their own evaluation survey response" on public.evaluation_survey_responses;
create policy "Students can read their own evaluation survey response"
on public.evaluation_survey_responses
for select
to authenticated
using (
  auth.uid() = respondent_id
  and respondent_role = 'student'
  and exists (
    select 1 from public.evaluation_cycles
    join public.profiles on profiles.id = auth.uid()
    where evaluation_cycles.id = evaluation_survey_responses.cycle_id
      and evaluation_cycles.survey_is_open = true
      and profiles.role = 'student'
      and profiles.status = 'approved'
      and profiles.grade_level = evaluation_cycles.grade_level
  )
);

drop policy if exists "Students can insert their own evaluation survey response" on public.evaluation_survey_responses;
create policy "Students can insert their own evaluation survey response"
on public.evaluation_survey_responses
for insert
to authenticated
with check (
  auth.uid() = respondent_id
  and respondent_role = 'student'
  and exists (
    select 1 from public.evaluation_cycles
    join public.profiles on profiles.id = auth.uid()
    where evaluation_cycles.id = evaluation_survey_responses.cycle_id
      and evaluation_cycles.survey_is_open = true
      and profiles.role = 'student'
      and profiles.status = 'approved'
      and profiles.grade_level = evaluation_cycles.grade_level
  )
);

drop policy if exists "Students can update their own evaluation survey response" on public.evaluation_survey_responses;
create policy "Students can update their own evaluation survey response"
on public.evaluation_survey_responses
for update
to authenticated
using (
  auth.uid() = respondent_id
  and respondent_role = 'student'
)
with check (
  auth.uid() = respondent_id
  and respondent_role = 'student'
  and exists (
    select 1 from public.evaluation_cycles
    join public.profiles on profiles.id = auth.uid()
    where evaluation_cycles.id = evaluation_survey_responses.cycle_id
      and evaluation_cycles.survey_is_open = true
      and profiles.role = 'student'
      and profiles.status = 'approved'
      and profiles.grade_level = evaluation_cycles.grade_level
  )
);

drop policy if exists "Teachers can manage evaluation survey responses" on public.evaluation_survey_responses;
create policy "Teachers can manage evaluation survey responses"
on public.evaluation_survey_responses
for all
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
)
with check (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

do $$
begin
  alter table public.lesson_sections drop constraint if exists lesson_sections_section_type_check;
  alter table public.lesson_sections
    add constraint lesson_sections_section_type_check
    check (section_type in ('paragraph', 'key_points', 'example', 'quick_tip', 'resource', 'steps', 'safety_note', 'vocabulary', 'summary', 'reflection'));

  alter table public.lesson_questions drop constraint if exists lesson_questions_question_type_check;
  alter table public.lesson_questions
    add constraint lesson_questions_question_type_check
    check (question_type in ('multiple_choice', 'true_false', 'short_answer', 'ordering', 'identification'));
end $$;

notify pgrst, 'reload schema';

delete from public.modules
where (title, grade_level) in (
  ('Hardware Basics', 'Grade 10'),
  ('Spreadsheet Basics', 'Grade 10'),
  ('Network Basics', 'Grade 9'),
  ('Internet Safety', 'Grade 9'),
  ('Digital Productivity', 'Grade 10')
);

delete from public.student_badges
where badge_key in (
  'productivity-starter',
  'network-scout',
  'safety-ready'
);

delete from public.student_certificates
where certificate_key in (
  'hardware-basics-complete',
  'spreadsheet-basics-complete',
  'internet-safety-complete'
);

insert into public.modules (
  quarter_id,
  created_by,
  title,
  description,
  category,
  grade_level,
  status,
  sort_order
)
select
  q.id,
  'd128e2c1-57e2-4037-804a-7f47ba711247',
  'PC Assembly and Disassembly',
  'Grade 10 ICT-CSS Module 5 ready lesson for safe computer assembly, disassembly, ports, cables, and hardware checks.',
  'Computer Basics',
  'Grade 10',
  'published',
  6
from public.quarters q
where q.name = 'T1'
  and q.school_year = '2026-2027'
  and not exists (
    select 1 from public.modules existing_module
    where existing_module.title = 'PC Assembly and Disassembly'
      and existing_module.grade_level = 'Grade 10'
  );

update public.modules
set
  description = 'Grade 10 ICT-CSS Module 5 complete learning path for PC assembly, disassembly, safety, ports, cables, and procedure checks.',
  category = 'Computer Basics',
  status = 'published',
  sort_order = 6,
  updated_at = now()
where title = 'PC Assembly and Disassembly'
  and grade_level = 'Grade 10';

delete from public.lessons
using public.modules
where lessons.module_id = modules.id
  and modules.title = 'PC Assembly and Disassembly'
  and lessons.title in (
    'Module Orientation and Safety Expectations',
    'Pre-Test: Assembly and Disassembly Readiness',
    'Safety Before Disassembly',
    'Computer Hardware to Disassemble',
    'Safety and Hardware Check',
    'Topic 1: Disassembling a Personal Computer',
    'Disassembly Procedure Check',
    'Topic 2: Assembling a Personal Computer',
    'Assembly Procedure Check',
    'Topic 3: Connecting Peripherals and Cables',
    'Final Assessment: PC Assembly and Cable Connections',
    'Disassembling a Personal Computer',
    'Disassembling and Assembling a Personal Computer',
    'Connecting Ports and Cables',
    'Assembly Check Practice'
  );

insert into public.lessons (
  module_id,
  quarter_id,
  created_by,
  title,
  description,
  lesson_type,
  status,
  duration_minutes,
  scheduled_date,
  due_date,
  sort_order
)
select
  modules.id,
  modules.quarter_id,
  modules.created_by,
  lesson_data.title,
  lesson_data.description,
  lesson_data.lesson_type,
  'published',
  lesson_data.duration_minutes,
  lesson_data.scheduled_date::date,
  lesson_data.due_date::date,
  lesson_data.sort_order
from public.modules
join (
  values
    ('Module Orientation and Safety Expectations', 'Understand the goals of the ICT-CSS assembly and disassembly module, the expected skills, and the safety habits required before working on a PC.', 'lesson', 18, '2026-06-19', '2026-06-20', 1),
    ('Pre-Test: Assembly and Disassembly Readiness', 'Check your prior knowledge about anti-static safety, computer parts, front-panel wiring, drives, and power/data connectors.', 'practice', 15, '2026-06-19', '2026-06-20', 2),
    ('Topic 1: Disassembling a Personal Computer', 'Learn the safe order for unplugging, opening, and removing major internal computer components without damaging parts.', 'lesson', 35, '2026-06-20', '2026-06-22', 3),
    ('Disassembly Procedure Check', 'Practice the correct disassembly order and explain why safety and organization matter when removing PC parts.', 'practice', 15, '2026-06-21', '2026-06-23', 4),
    ('Topic 2: Assembling a Personal Computer', 'Learn how to prepare the workplace, install the motherboard, CPU, heat sink, RAM, power supply, drives, and add-in cards.', 'lesson', 45, '2026-06-22', '2026-06-24', 5),
    ('Assembly Procedure Check', 'Check the correct assembly order and review safe installation of core PC components.', 'practice', 18, '2026-06-23', '2026-06-25', 6),
    ('Topic 3: Connecting Peripherals and Cables', 'Identify common computer ports and cables, connect peripherals in the correct order, and understand why the power cable is connected last.', 'lesson', 28, '2026-06-24', '2026-06-26', 7),
    ('Final Assessment: PC Assembly and Cable Connections', 'Demonstrate your understanding of PC parts, disassembly, assembly, ports, cables, and safe servicing procedures.', 'practice', 25, '2026-06-25', '2026-06-27', 8)
) as lesson_data(title, description, lesson_type, duration_minutes, scheduled_date, due_date, sort_order)
  on modules.title = 'PC Assembly and Disassembly'
where modules.grade_level = 'Grade 10'
  and not exists (
    select 1 from public.lessons existing_lesson
    where existing_lesson.module_id = modules.id
      and existing_lesson.title = lesson_data.title
  );

-- Supplemental Module 5 content from the ICT-CSS Assembly and Disassembly PC module.
-- This adds hardware identification lessons and practices without replacing the
-- existing PC Assembly and Disassembly learning path.
insert into public.modules (
  quarter_id,
  created_by,
  title,
  description,
  category,
  grade_level,
  status,
  sort_order
)
select
  q.id,
  'd128e2c1-57e2-4037-804a-7f47ba711247',
  'Hardware Identification',
  'Grade 10 ICT-CSS Module 5 support lessons for identifying PC parts, internal drives, ports, cables, connectors, and safe hardware procedures.',
  'Computer Basics',
  'Grade 10',
  'published',
  7
from public.quarters q
where q.name = 'T1'
  and q.school_year = '2026-2027'
  and not exists (
    select 1 from public.modules existing_module
    where existing_module.title = 'Hardware Identification'
      and existing_module.grade_level = 'Grade 10'
  );

update public.modules
set
  description = 'Grade 10 ICT-CSS Module 5 support lessons for identifying PC parts, internal drives, ports, cables, connectors, and safe hardware procedures.',
  category = 'Computer Basics',
  status = 'published',
  sort_order = 7,
  updated_at = now()
where title = 'Hardware Identification'
  and grade_level = 'Grade 10';

insert into public.lessons (
  module_id,
  quarter_id,
  created_by,
  title,
  description,
  lesson_type,
  status,
  duration_minutes,
  scheduled_date,
  due_date,
  sort_order
)
select
  modules.id,
  modules.quarter_id,
  modules.created_by,
  lesson_data.title,
  lesson_data.description,
  lesson_data.lesson_type,
  'published',
  lesson_data.duration_minutes,
  lesson_data.scheduled_date::date,
  lesson_data.due_date::date,
  lesson_data.sort_order
from public.modules
join (
  values
    ('Module 5 Guide and Pre-Test', 'Follow the PDF module guide, review expectations, and answer the pre-test before starting the three main lessons.', 'practice', 25, '2026-07-04', '2026-07-06', 10),
    ('Lesson 1: Disassembling a Personal Computer', 'Learn the PDF Lesson 1 procedure for safely unplugging, opening, and removing PC components in order.', 'lesson', 40, '2026-07-05', '2026-07-07', 11),
    ('Lesson 1 Practice: Disassembly Activities', 'Complete the PDF Lesson 1 activity prompts and disassembly ordering check.', 'practice', 22, '2026-07-06', '2026-07-08', 12),
    ('Lesson 2: Assembling a Personal Computer', 'Learn the PDF Lesson 2 procedure for preparing, installing, mounting, powering, and checking PC components.', 'lesson', 50, '2026-07-07', '2026-07-09', 13),
    ('Lesson 2 Practice: Assembly Activities', 'Complete the PDF Lesson 2 activity prompts and assembly ordering check.', 'practice', 22, '2026-07-08', '2026-07-10', 14),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'Learn the PDF Lesson 3 procedure for connecting monitor, keyboard, mouse, USB, network, and power cables.', 'lesson', 35, '2026-07-09', '2026-07-11', 15),
    ('Lesson 3 Practice: Ports and Cables Activities', 'Complete the PDF Lesson 3 activity prompts and cable connection ordering check.', 'practice', 20, '2026-07-10', '2026-07-12', 16),
    ('Post-Test: Module 5 Hardware and Procedures', 'Answer post-test style questions covering hardware identification, connectors, disassembly, assembly, and peripheral connections.', 'practice', 30, '2026-07-11', '2026-07-13', 17)
) as lesson_data(title, description, lesson_type, duration_minutes, scheduled_date, due_date, sort_order)
  on modules.title = 'Hardware Identification'
where modules.grade_level = 'Grade 10'
  and not exists (
    select 1 from public.lessons existing_lesson
    where existing_lesson.module_id = modules.id
      and existing_lesson.title = lesson_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.sort_order
from public.lessons
join (
  values
    ('Module Orientation and Safety Expectations', 'paragraph', 'What This Module Is About', 'In ICT-CSS, assembling and disassembling a personal computer is not only about removing parts. A good technician works in order, protects the computer from damage, protects themselves from electrical hazards, and checks each connection before power is restored. This module prepares you to handle a system unit, identify important parts, follow procedures, and explain why each step matters.', 1),
    ('Module Orientation and Safety Expectations', 'key_points', 'Learning Targets', 'Identify the proper procedure for computer disassembly and assembly.\nObserve occupational health and safety habits while handling computer hardware.\nRecognize major internal parts, ports, cables, and connectors.\nPerform basic technician decisions such as unplugging first, grounding yourself, organizing screws, and avoiding force.', 2),
    ('Module Orientation and Safety Expectations', 'safety_note', 'Safety Comes First', 'Before touching any internal component, shut down the computer, unplug the power cable, disconnect external cables, and discharge static electricity. Static electricity can damage RAM, expansion cards, and motherboard circuits even when you do not feel a shock.', 3),
    ('Module Orientation and Safety Expectations', 'vocabulary', 'Terms You Will Use', 'System unit - the case that contains the computer internal parts.\nESD - electrostatic discharge, a sudden static-electricity release that can damage components.\nAnti-static bag - protective storage for electronic parts that are sensitive to static.\nMotherboard - the main circuit board where CPU, memory, cards, and connectors attach.\nConnector - a plug or socket that joins a cable or device to a computer part.', 4),
    ('Module Orientation and Safety Expectations', 'summary', 'Remember', 'Technicians prepare before touching hardware.\nThe power cable is removed before internal servicing.\nComponents should be handled by their edges.\nCorrect procedure prevents damage and makes reassembly easier.', 5),
    ('Module Orientation and Safety Expectations', 'quick_tip', 'Notebook Habit', 'Write important steps and connector names in your notebook. Technicians often rely on notes and labels to avoid mistakes during reassembly.', 6),

    ('Pre-Test: Assembly and Disassembly Readiness', 'paragraph', 'Practice Goal', 'This pre-test checks what you already know before the lesson. Do not worry if you miss items now. Use the feedback to notice which parts of the module you need to focus on: safety, drives, cables, front-panel wiring, power connectors, and procedure order.', 1),
    ('Pre-Test: Assembly and Disassembly Readiness', 'quick_tip', 'Answer Honestly', 'A pre-test is not the final grade. It helps you discover what you already know and what you still need to learn.', 2),

    ('Topic 1: Disassembling a Personal Computer', 'paragraph', 'Brief Introduction', 'Disassembly means opening the system unit and removing parts in a safe, organized order. The goal is not speed. The goal is to remove components without electrical risk, broken connectors, lost screws, or confusion during reassembly.', 1),
    ('Topic 1: Disassembling a Personal Computer', 'key_points', 'Objectives', 'Identify hardware that may be removed from the system unit.\nExplain why procedure matters during disassembly.\nDemonstrate the correct order for safely disassembling a personal computer.\nApply safety habits such as unplugging, grounding, labeling, and organizing screws.', 2),
    ('Topic 1: Disassembling a Personal Computer', 'safety_note', 'Before Opening the Case', 'Prepare your tools, clear the work area, and use a container for screws. Unplug every external cable first: power, monitor, USB devices, keyboard, mouse, internet or Ethernet, modem, speakers, and other peripherals. This prevents electrical hazards and cable strain while opening the case.', 3),
    ('Topic 1: Disassembling a Personal Computer', 'steps', 'Disassembly Procedure', '1. Unplug every cable and wire connected to the computer.\n2. Open the outer shell or case by removing the screws at the back panel.\n3. Unplug and remove the system fan carefully.\n4. Unplug the CPU fan from the motherboard, then remove the screws holding it to the heat sink.\n5. Disconnect power supply cables from the motherboard, drives, and other powered devices.\n6. Remove the CD/DVD or optical drive after unplugging its IDE or SATA data cable and power connector.\n7. Remove the hard drive by unplugging the data and power connectors, then sliding it from its bay.\n8. Remove memory modules by pressing both locking tabs away from the RAM.\n9. Remove the motherboard last, only after cables, cards, drives, fan, RAM, and power connections are clear.', 4),
    ('Topic 1: Disassembling a Personal Computer', 'vocabulary', 'Hardware You May Remove', 'System fan - moves air through the case to reduce heat.\nCPU fan and heat sink - cools the processor.\nPower supply unit - provides power to the motherboard and drives.\nOptical drive - reads CDs or DVDs.\nHard drive or storage drive - stores programs and files.\nRAM - temporary memory held by locking tabs.\nMotherboard - main board removed last because many parts connect to it.', 5),
    ('Topic 1: Disassembling a Personal Computer', 'example', 'Technician Example', 'If the task is to inspect RAM, do not pull it immediately. First unplug the PC, open the case, ground yourself, locate the RAM slot, press both locking tabs, and hold the module by its edges. Touching the gold contacts or forcing the module can damage it.', 6),
    ('Topic 1: Disassembling a Personal Computer', 'summary', 'Remember', 'Unplugging is the first disassembly step.\nThe motherboard is removed last.\nUse labels, photos, or notes before disconnecting cables.\nNever force parts; look for screws, clips, tabs, or locks first.', 7),
    ('Topic 1: Disassembling a Personal Computer', 'quick_tip', 'Take Photos', 'Take a clear photo before disconnecting internal cables. It helps you return each connector to the correct location during assembly.', 8),

    ('Disassembly Procedure Check', 'paragraph', 'Practice Goal', 'This checkpoint helps you confirm the safe order for removing parts from a PC. Focus on why each step belongs in its place, not only memorizing the list.', 1),
    ('Disassembly Procedure Check', 'quick_tip', 'Think Like a Technician', 'Ask yourself: Is the computer safe to touch? Are cables out of the way? Will removing this part affect another part that is still attached?', 2),

    ('Topic 2: Assembling a Personal Computer', 'paragraph', 'Brief Introduction', 'Assembly is the reverse skill of disassembly, but it also requires planning. You must check parts, prepare the case, install components in a logical order, and make sure power and data connections are secure before turning on the computer.', 1),
    ('Topic 2: Assembling a Personal Computer', 'key_points', 'Objectives', 'Identify hardware that must be assembled inside the system unit.\nExplain why correct procedure matters during assembly.\nDemonstrate the assembly order for a personal computer.\nInstall parts carefully without forcing sockets, cables, or cards.', 2),
    ('Topic 2: Assembling a Personal Computer', 'steps', 'Assembly Procedure', '1. Prepare your workplace: take inventory, make space, allow enough time, prepare grounding protection, and keep drivers/tools ready.\n2. Prepare the motherboard by checking for visible defects and reviewing the motherboard manual.\n3. Install the CPU by opening the socket lever, aligning the CPU mark, placing it gently, and locking the lever.\n4. Install the CPU heat sink and fan according to the manufacturer instructions, then connect the fan power lead to the motherboard.\n5. Install RAM modules by aligning the notch and pressing evenly until both clips lock.\n6. Place the motherboard into the case using proper brass standoffs and aligned screw holes.\n7. Connect the power supply to the motherboard, including the main ATX connector and CPU power connector.\n8. Install graphics or video cards in the correct expansion slot and secure them with screws.\n9. Install internal drives such as hard drives, SSDs, and optical drives using rails, cages, or screws.\n10. Install add-in cards in free PCI or PCIe slots and secure them properly.', 3),
    ('Topic 2: Assembling a Personal Computer', 'safety_note', 'Avoid Short Circuits', 'Motherboard standoffs are important. They raise the motherboard away from the case metal. If the board touches the case directly in the wrong place, it may cause short circuits or hardware malfunction.', 4),
    ('Topic 2: Assembling a Personal Computer', 'example', 'RAM Installation Example', 'RAM fits only when the notch is aligned with the slot. Press down firmly but evenly. If the clips do not lock, do not hit the module; remove it, check orientation, and try again.', 5),
    ('Topic 2: Assembling a Personal Computer', 'reflection', 'Technician Thinking', 'A skilled technician does not guess where parts go. They inspect, read labels, check the manual, and match the connector shape before applying pressure.', 6),
    ('Topic 2: Assembling a Personal Computer', 'summary', 'Remember', 'Prepare the workplace before assembly.\nInstall the CPU and heat sink carefully.\nRAM should lock on both sides.\nUse standoffs before securing the motherboard.\nConnect power and data cables only where they fit correctly.', 7),
    ('Topic 2: Assembling a Personal Computer', 'quick_tip', 'Manuals Matter', 'When a front-panel connector or motherboard pin layout is unclear, check the motherboard manual instead of guessing.', 8),

    ('Assembly Procedure Check', 'paragraph', 'Practice Goal', 'This practice checks whether you can arrange assembly steps and recognize why careful installation prevents damage.', 1),
    ('Assembly Procedure Check', 'quick_tip', 'Use the Big Picture', 'Assembly starts with preparation and board-level parts, then moves to case mounting, power, drives, cards, and final checks.', 2),

    ('Topic 3: Connecting Peripherals and Cables', 'paragraph', 'Brief Introduction', 'After internal parts are installed, external devices and cables must be connected to the correct ports. A technician must recognize cable shape, connector orientation, and port function. Never force a connector into a port.', 1),
    ('Topic 3: Connecting Peripherals and Cables', 'key_points', 'Objectives', 'Identify common ports and cables used in a computer system.\nExplain the function of ports and cables.\nConnect external hardware to correct ports.\nExplain why the power cable is connected after other cables.', 2),
    ('Topic 3: Connecting Peripherals and Cables', 'steps', 'Connecting External Hardware', '1. Attach the monitor cable to the video port.\n2. Secure the monitor cable by tightening the connector screws when available.\n3. Plug the keyboard cable into the PS/2 keyboard port or a USB port.\n4. Plug the mouse cable into the PS/2 mouse port or a USB port.\n5. Plug USB devices into USB ports.\n6. Plug the network cable into the network or LAN port.\n7. Plug the power cable into the power supply last.', 3),
    ('Topic 3: Connecting Peripherals and Cables', 'vocabulary', 'Common Ports and Cables', 'VGA port - video connection for a monitor.\nUSB port - connects modern peripherals such as printers, cameras, flash drives, keyboard, and mouse.\nPS/2 port - older round keyboard or mouse connection.\nLAN or Ethernet port - connects a network cable.\nAudio port - connects speakers or headset.\nSATA cable - data cable for storage drives.\n20/24-pin power connector - supplies motherboard power.\nP4 12V connector - auxiliary CPU power connector.\n4-pin Molex - powers some drives and internal components.\n4-pin Berg - older connector often used for floppy disk drives.', 4),
    ('Topic 3: Connecting Peripherals and Cables', 'safety_note', 'Power Cable Last', 'Connect the power cable after other peripherals and internal connections have been checked. This reduces the chance of powering the system while a cable is loose, misaligned, or still being handled.', 5),
    ('Topic 3: Connecting Peripherals and Cables', 'example', 'Connector Orientation Example', 'A connector should fit naturally when it is aligned correctly. If it does not fit, check the shape, pins, notch, or label. Forcing a connector can bend pins or damage the port.', 6),
    ('Topic 3: Connecting Peripherals and Cables', 'summary', 'Remember', 'Match cables to the correct port.\nUSB and PS/2 may both be used for keyboard or mouse depending on the device.\nLAN/Ethernet is for networking.\nSATA is a data cable for drives.\nPower is connected last after checking all other connections.', 7),
    ('Topic 3: Connecting Peripherals and Cables', 'quick_tip', 'Look Before You Plug', 'Check the label, shape, and orientation before inserting a connector. A correct connector should not need force.', 8),

    ('Final Assessment: PC Assembly and Cable Connections', 'paragraph', 'Assessment Goal', 'Use everything from the module to answer questions about safety, components, procedure order, ports, cables, and connectors. This final check shows whether you are ready to explain and perform basic PC assembly and disassembly with proper care.', 1),
    ('Final Assessment: PC Assembly and Cable Connections', 'summary', 'Before You Submit', 'Review disassembly order.\nReview assembly order.\nReview ports and cable functions.\nRemember that safety and correct procedure protect both the technician and the computer.', 2),
    ('Final Assessment: PC Assembly and Cable Connections', 'quick_tip', 'Answer From Procedure', 'When unsure, think about what must happen first for safety and what must be connected last before powering on.', 3)
) as section_data(lesson_title, section_type, title, body, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'PC Assembly and Disassembly'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

delete from public.lesson_questions
using public.lessons
join public.modules on modules.id = lessons.module_id
where lesson_questions.lesson_id = lessons.id
  and modules.title = 'PC Assembly and Disassembly'
  and lessons.title = 'Pre-Test: Assembly and Disassembly Readiness';

delete from public.lesson_questions
using public.lessons
join public.modules on modules.id = lessons.module_id
where lesson_questions.lesson_id = lessons.id
  and modules.title = 'Hardware Identification'
  and lessons.title = 'Module 5 Guide and Pre-Test';

insert into public.lesson_questions (lesson_id, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
select
  lessons.id,
  question_data.question_type,
  question_data.prompt,
  question_data.choices::jsonb,
  question_data.correct_answer,
  question_data.hint,
  question_data.explanation,
  question_data.points,
  question_data.sort_order
from public.lessons
join (
  values
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'How will you secure the safety of the CPU?', '["By following the steps", "By washing your hands first", "By wearing the anti-static wrist strap", "By listening carefully to your teacher"]', 'By wearing the anti-static wrist strap', 'Think about static electricity.', 'The PDF pre-test identifies the anti-static wrist strap as the safety tool for handling the CPU and other sensitive parts.', 1, 1),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'What is the use of an anti-static bag?', '["A bag used for storing electronic components for inventory", "A bag used for storing electronic components before and after the activity", "A bag used for storing electronic components to prevent the loss of the components", "A bag used for storing electronic components which are prone to damage caused by electrostatic discharge"]', 'A bag used for storing electronic components which are prone to damage caused by electrostatic discharge', 'It protects parts from static electricity.', 'Anti-static bags protect electronic components that can be damaged by electrostatic discharge.', 1, 2),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'All of the following are front-panel wiring except one. Which one is not included?', '["Power LED", "Reset switch", "Switch on", "HDD LED"]', 'Switch on', 'Look for the labels usually found on small case leads.', 'Power LED, Reset switch, and HDD LED are common front-panel wiring labels. "Switch on" is not the usual front-panel label in the PDF item.', 1, 3),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'Before you remove the CPU fan, what is the first thing to do?', '["Remove the four screws", "Remove the power cord", "Remove the CPU", "Remove the heat sink"]', 'Remove the power cord', 'Safety comes before removing parts.', 'The PDF pre-test checks that power is removed before working near the CPU fan and heat sink.', 1, 4),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'All of the following are internal drives of the computer except one. Which one is not an internal drive?', '["Flash Drive", "Floppy Drive", "Hard Drive", "Optical Drive"]', 'Flash Drive', 'Think of removable storage.', 'A flash drive is usually removable external storage, while floppy, hard, and optical drives can be installed internally.', 1, 5),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'What is the last component to be removed in disassembly of a computer?', '["Floppy Disk Drive", "Hard Disk Drive", "Power Supply", "Motherboard"]', 'Motherboard', 'Many components and connectors attach to it.', 'The motherboard is removed last after connected parts, drives, memory, and cables are cleared.', 1, 6),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'What is the first thing to do before you touch your computer for disassembling?', '["Unplug the cable and wires", "Clean your computer", "Unscrew the computer", "Remove the chassis"]', 'Unplug the cable and wires', 'Start with safety.', 'Unplugging cables and wires is the first safety step before disassembly.', 1, 7),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'Why is it important to prepare the workplace before you do the activity or work?', '["To ensure the safety of the student", "To finish early before the time", "To prepare the readiness", "To arrange the tools and materials needed"]', 'To arrange the tools and materials needed', 'Think about tools and materials before starting.', 'A prepared workplace keeps tools and materials ready and makes the activity safer and more organized.', 1, 8),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'What cable passes or saves the information being provided and processed by the computer?', '["Data Cable", "P4 12V Connector", "SATA Connector", "20/24 pin Molex"]', 'Data Cable', 'The name describes what it carries.', 'A data cable carries information between devices such as drives and the motherboard.', 1, 9),
    ('Pre-Test: Assembly and Disassembly Readiness', 'multiple_choice', 'What power supply cable is used with motherboards that have an Intel Pentium 4 or later processor?', '["SATA Port", "SATA Cable", "P4 12V Connector", "4 Pin Berg"]', 'P4 12V Connector', 'It provides auxiliary CPU or motherboard power.', 'The P4 12V connector supplies auxiliary power for motherboards using Pentium 4 or later processors.', 1, 10),

    ('Disassembly Procedure Check', 'ordering', 'Arrange the disassembly procedure in the correct order. Type the numbers separated by commas.', '["Detach the hard drive", "Detach the power supply", "Open the outer shell or case", "Pull out the motherboard", "Remove the CD/DVD drives", "Remove the CPU fan", "Remove the system fan", "Unplug all cables and wires", "Remove the memory"]', '8,3,7,6,2,5,1,9,4', 'Begin with safety and access. The motherboard is last.', 'A safe disassembly starts by unplugging cables, opening the case, removing fans, power and drives, then memory, and finally the motherboard.', 3, 1),
    ('Disassembly Procedure Check', 'short_answer', 'Why should you discharge your body before touching computer components?', '[]', 'to prevent electrostatic discharge|prevent esd|to avoid static damage|prevent static electricity damage', 'Use the term ESD if you know it.', 'Discharging your body helps prevent electrostatic discharge from damaging sensitive parts.', 1, 2),
    ('Disassembly Procedure Check', 'multiple_choice', 'Before removing the CPU fan, what should you do first?', '["Unplug the fan power connector", "Remove the motherboard", "Plug in the power cable", "Install the hard drive"]', 'Unplug the fan power connector', 'Follow the wire from the fan to the motherboard.', 'The CPU fan power connector must be unplugged before the fan is removed from the heat sink.', 1, 3),
    ('Disassembly Procedure Check', 'identification', 'Identify the component held in place by locking tabs at both ends of its slot.', '["RAM", "CPU fan", "Power supply", "VGA port"]', 'ram|memory|memory module|ram module', 'It is temporary memory.', 'RAM modules are held by locking tabs at both ends of the RAM slot.', 1, 4),
    ('Disassembly Procedure Check', 'true_false', 'You should force a component out if it does not come loose immediately.', '["True", "False"]', 'False', 'Think about screws, clips, and locking tabs.', 'Never force components. Check for screws, clips, tabs, or cable locks first.', 1, 5),

    ('Assembly Procedure Check', 'ordering', 'Arrange the assembly procedure in the correct order. Type the numbers separated by commas.', '["Connect the power supply", "Install graphics or video cards", "Install internal drives", "Install memory RAM modules", "Install add-in cards", "Install the CPU", "Install the CPU heat sink", "Place the motherboard into the case", "Prepare the motherboard", "Prepare your workplace"]', '10,9,6,7,4,8,1,2,3,5', 'Start with preparation, then motherboard/CPU/RAM, then case and expansion parts.', 'A good assembly process begins with workplace and motherboard preparation before CPU, cooling, RAM, case mounting, power, cards, and drives.', 3, 1),
    ('Assembly Procedure Check', 'multiple_choice', 'Why are motherboard standoffs important?', '["They prevent short circuits by lifting the board from the case", "They erase old files", "They make the computer run without power", "They replace the CPU fan"]', 'They prevent short circuits by lifting the board from the case', 'The motherboard should not touch the metal case directly in the wrong places.', 'Standoffs support the motherboard and help prevent electrical short circuits.', 1, 2),
    ('Assembly Procedure Check', 'multiple_choice', 'What should you check when installing the CPU?', '["The CPU and socket alignment marks", "The monitor brightness", "The desktop wallpaper", "The keyboard color"]', 'The CPU and socket alignment marks', 'Look for the triangle or missing-pin mark.', 'CPU alignment marks show the correct orientation before locking the CPU into the socket.', 1, 3),
    ('Assembly Procedure Check', 'true_false', 'RAM should be pressed evenly until the clips on both sides lock into place.', '["True", "False"]', 'True', 'Both sides should secure the module.', 'RAM is properly installed when it is aligned and both locking clips hold it in place.', 1, 4),
    ('Assembly Procedure Check', 'short_answer', 'What document should you consult when front-panel connectors or motherboard pin layouts are unclear?', '[]', 'motherboard manual|manual|user manual|motherboard user manual', 'Technicians use documentation.', 'The motherboard manual shows socket and connector locations accurately.', 1, 5),

    ('Final Assessment: PC Assembly and Cable Connections', 'identification', 'Identify the connector used to give supply to the motherboard.', '["20/24 pin Molex connector", "IDE cable", "Data cable", "VGA port"]', '20/24 pin molex connector|20/24 pin power connector|atx power connector|motherboard power connector', 'It is the large main motherboard power connector.', 'The 20/24-pin power connector supplies power from the PSU to the motherboard.', 1, 1),
    ('Final Assessment: PC Assembly and Cable Connections', 'multiple_choice', 'Which wires should be disconnected first during computer disassembly?', '["Back-panel external cables and wires", "Front-panel wires only", "Data cable only", "The CPU pins"]', 'Back-panel external cables and wires', 'Start with what connects the PC to external power and peripherals.', 'External/back-panel cables are removed first so the system unit can be handled safely.', 1, 2),
    ('Final Assessment: PC Assembly and Cable Connections', 'multiple_choice', 'Which one is NOT an internal drive of a computer?', '["Flash drive", "Hard drive", "Optical drive", "Floppy drive"]', 'Flash drive', 'Think of drives mounted inside the case.', 'A flash drive is usually an external/removable storage device, not an internal drive mounted in the system unit.', 1, 3),
    ('Final Assessment: PC Assembly and Cable Connections', 'identification', 'Identify the auxiliary power connector used by motherboards with Pentium 4 or later processors.', '["P4 12V connector", "SATA cable", "VGA port", "PS/2 port"]', 'p4 12v connector|p4 connector|cpu power connector', 'It provides extra CPU power.', 'The P4 12V connector is an auxiliary motherboard/CPU power connector.', 1, 4),
    ('Final Assessment: PC Assembly and Cable Connections', 'multiple_choice', 'Which cable is commonly used for hard drives, optical drives, and solid-state drives?', '["SATA cable", "Audio cable", "PS/2 cable", "Monitor stand"]', 'SATA cable', 'It is a modern storage data cable.', 'SATA cables connect storage devices such as hard drives, optical drives, and SSDs.', 1, 5),
    ('Final Assessment: PC Assembly and Cable Connections', 'ordering', 'Arrange the external cable connection procedure in the correct order. Type the numbers separated by commas.', '["Attach the monitor cable to the video port", "Secure the monitor cable screws", "Plug the keyboard cable into PS/2 or USB", "Plug the mouse cable into PS/2 or USB", "Plug USB devices into USB ports", "Plug the network cable into the network port", "Plug the power cable into the power supply"]', '1,2,3,4,5,6,7', 'The power cable is last.', 'Peripherals are connected first; the power cable is connected last after all other connections are checked.', 3, 6),
    ('Final Assessment: PC Assembly and Cable Connections', 'true_false', 'The power cable should be plugged in after other peripherals and cables are connected.', '["True", "False"]', 'True', 'Power is the final connection.', 'Connecting power last reduces the risk of handling live connections or powering on with a loose cable.', 1, 7),
    ('Final Assessment: PC Assembly and Cable Connections', 'short_answer', 'Why is it important to follow the correct procedures in assembly and disassembly?', '[]', 'to prevent damage|prevent damage|avoid damage|protect the computer|protect components|avoid accidents|safety', 'Think about safety and hardware protection.', 'Correct procedures protect the technician, prevent component damage, and make the work easier to check.', 1, 8)
) as question_data(lesson_title, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
  on lessons.title = question_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'PC Assembly and Disassembly'
  and not exists (
    select 1
    from public.lesson_questions existing_question
    where existing_question.lesson_id = lessons.id
      and existing_question.prompt = question_data.prompt
  );

-- Supplemental Module 5 content from the ICT-CSS Assembly and Disassembly PC module.
-- This adds hardware identification lessons and practices without replacing the
-- existing PC Assembly and Disassembly learning path.
insert into public.modules (
  quarter_id,
  created_by,
  title,
  description,
  category,
  grade_level,
  status,
  sort_order
)
select
  q.id,
  'd128e2c1-57e2-4037-804a-7f47ba711247',
  'Hardware Identification',
  'Grade 10 ICT-CSS Module 5 support lessons for identifying PC parts, internal drives, ports, cables, connectors, and safe hardware procedures.',
  'Computer Basics',
  'Grade 10',
  'published',
  7
from public.quarters q
where q.name = 'T1'
  and q.school_year = '2026-2027'
  and not exists (
    select 1 from public.modules existing_module
    where existing_module.title = 'Hardware Identification'
      and existing_module.grade_level = 'Grade 10'
  );

update public.modules
set
  description = 'Grade 10 ICT-CSS Module 5 support lessons for identifying PC parts, internal drives, ports, cables, connectors, and safe hardware procedures.',
  category = 'Computer Basics',
  status = 'published',
  sort_order = 7,
  updated_at = now()
where title = 'Hardware Identification'
  and grade_level = 'Grade 10';

insert into public.lessons (
  module_id,
  quarter_id,
  created_by,
  title,
  description,
  lesson_type,
  status,
  duration_minutes,
  scheduled_date,
  due_date,
  sort_order
)
select
  modules.id,
  modules.quarter_id,
  modules.created_by,
  lesson_data.title,
  lesson_data.description,
  lesson_data.lesson_type,
  'published',
  lesson_data.duration_minutes,
  lesson_data.scheduled_date::date,
  lesson_data.due_date::date,
  lesson_data.sort_order
from public.modules
join (
  values
    ('Hardware Identification Orientation', 'Review the ICT-CSS Module 5 expectations, the parts of the module, and the hardware groups students must recognize before assembly work.', 'lesson', 18, '2026-06-26', '2026-06-28', 1),
    ('Internal PC Parts and Functions', 'Identify the motherboard, CPU, CPU fan and heat sink, RAM, power supply, internal drives, video card, slots, and front-panel wiring.', 'lesson', 35, '2026-06-27', '2026-06-29', 2),
    ('Ports, Cables, and Connectors', 'Identify VGA, USB, PS/2, LAN, audio, SATA, Molex, Berg, ATX, and P4 12V connectors and explain where each is used.', 'lesson', 30, '2026-06-28', '2026-06-30', 3),
    ('Hardware Identification Practice', 'Practice naming PC parts and matching each part to its function, location, or safe handling reminder.', 'practice', 20, '2026-06-29', '2026-07-01', 4),
    ('Ports and Cable Matching Practice', 'Practice identifying cables, ports, and the correct order for connecting external hardware.', 'practice', 18, '2026-06-30', '2026-07-02', 5),
    ('Module 5 Hardware Review Check', 'Review Module 5 hardware, disassembly, assembly, ports, cables, and post-test style identification items.', 'practice', 25, '2026-07-01', '2026-07-03', 6)
) as lesson_data(title, description, lesson_type, duration_minutes, scheduled_date, due_date, sort_order)
  on modules.title = 'Hardware Identification'
where modules.grade_level = 'Grade 10'
  and not exists (
    select 1 from public.lessons existing_lesson
    where existing_lesson.module_id = modules.id
      and existing_lesson.title = lesson_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.sort_order
from public.lessons
join (
  values
    ('Hardware Identification Orientation', 'paragraph', 'Module 5 Focus', 'This support module strengthens the same ICT-CSS Module 5 skills: identify computer hardware, follow safe procedures, assemble and disassemble a PC, and connect peripherals to the correct ports.', 1),
    ('Hardware Identification Orientation', 'key_points', 'Learning Expectations', 'Identify the proper procedure in disassembly and assembly.\nObserve occupational health and safety procedures.\nRecognize internal parts, cables, ports, and connectors.\nPerform basic skills needed to assemble and disassemble a PC.', 2),
    ('Hardware Identification Orientation', 'steps', 'How to Use This Module', '1. Read each lesson carefully.\n2. Write important hardware names and functions in your notebook.\n3. Complete every practice activity.\n4. Check your answers and review feedback.\n5. Use the post-test style checks to measure what you learned.', 3),
    ('Hardware Identification Orientation', 'vocabulary', 'Module Parts', 'Expectations - what you should know after the lesson.\nPre-assessment - checks your prior knowledge.\nLooking Back - recalls related skills from the previous lesson.\nBrief Introduction - gives an overview of the topic.\nActivities - let you practice the skill.\nRemember - summarizes important points.\nCheck Your Understanding - verifies your learning.\nPost-assessment - checks your learning after the module.', 4),

    ('Internal PC Parts and Functions', 'paragraph', 'Why Hardware Identification Matters', 'A technician must know the part, its function, and its safe handling rule before removing or installing it. Identifying parts correctly prevents damage, wrong connections, and unsafe servicing.', 1),
    ('Internal PC Parts and Functions', 'vocabulary', 'Internal Components', 'Motherboard - the main circuit board where the CPU, RAM, cards, power connectors, and ports connect.\nCPU - the processor that carries out instructions.\nCPU fan and heat sink - cool the processor.\nRAM - temporary memory installed in slots with locking tabs.\nPower supply unit - provides electrical power to the motherboard and drives.\nHard drive or SSD - stores files and programs.\nOptical drive - reads CDs or DVDs.\nVideo card - provides display output through a graphics slot.\nFront panel - connects power switch, reset switch, power LED, HDD LED, USB, audio, and speaker leads.', 2),
    ('Internal PC Parts and Functions', 'safety_note', 'Safe Handling Rules', 'Unplug the computer before servicing. Discharge static electricity before touching parts. Hold RAM, cards, and boards by their edges. Do not force a connector, slot, or component. Check the manual when a socket or pin layout is unclear.', 3),
    ('Internal PC Parts and Functions', 'example', 'Identification Example', 'If a part has locking tabs on both ends of the slot, it is usually RAM. If a large connector from the power supply plugs into the motherboard, it is the 20/24-pin ATX motherboard power connector.', 4),
    ('Internal PC Parts and Functions', 'reflection', 'Looking Back', 'Before disassembling, recall the correct order: unplug all cables, open the case, remove fans, disconnect power, remove drives, remove memory, and remove the motherboard last.', 5),
    ('Internal PC Parts and Functions', 'summary', 'Remember', 'Correct identification comes before correct procedure.\nThe motherboard connects most internal parts.\nPower connectors and data cables are different.\nRAM uses locking tabs.\nThe motherboard is removed last during disassembly.', 6),

    ('Ports, Cables, and Connectors', 'paragraph', 'Brief Introduction', 'After assembly, external hardware must be connected to the correct ports. Ports and cables may look similar, so technicians check labels, shape, pin count, and connector orientation before plugging anything in.', 1),
    ('Ports, Cables, and Connectors', 'vocabulary', 'Ports and Cables', 'VGA port - connects a monitor.\nUSB port - connects modern peripherals such as printers, cameras, flash drives, keyboard, and mouse.\nPS/2 port - older round keyboard or mouse connector.\nLAN or Ethernet port - connects a network cable.\nAudio port - connects speakers or headset.\nSATA cable - connects storage devices such as hard drives, optical drives, and SSDs.\n20/24-pin ATX connector - supplies motherboard power.\nP4 12V connector - supplies auxiliary CPU or motherboard power.\n4-pin Molex - powers some drives and internal components.\n4-pin Berg - commonly used for older floppy drives.', 2),
    ('Ports, Cables, and Connectors', 'steps', 'External Hardware Connection Order', '1. Attach the monitor cable to the video port.\n2. Secure the monitor cable screws when available.\n3. Plug the keyboard cable into the PS/2 keyboard port or a USB port.\n4. Plug the mouse cable into the PS/2 mouse port or a USB port.\n5. Plug USB devices into USB ports.\n6. Plug the network cable into the network port.\n7. Plug the power cable into the power supply last.', 3),
    ('Ports, Cables, and Connectors', 'safety_note', 'Never Force a Cable', 'When attaching cables, never force a connection. If the cable does not fit, check the port, connector shape, orientation, and label. The power cable should be connected after all other cables have been checked.', 4),
    ('Ports, Cables, and Connectors', 'summary', 'Remember', 'Match each connector to the correct port.\nPower is connected last.\nSATA is a data cable for drives.\nP4 12V is auxiliary power.\nBerg is used for older floppy drives.\nLAN/Ethernet is used for networking.', 5),

    ('Hardware Identification Practice', 'paragraph', 'Practice Goal', 'Use this activity to identify PC parts from their function, location, or safe handling clue. This mirrors the hardware identification part of the Module 5 post-test.', 1),
    ('Hardware Identification Practice', 'quick_tip', 'How to Answer', 'Look for the clue: cooling means CPU fan and heat sink, temporary memory means RAM, main board means motherboard, and power supply connector means ATX or P4 12V depending on the description.', 2),

    ('Ports and Cable Matching Practice', 'paragraph', 'Practice Goal', 'Use this practice to match cables and ports to their correct functions and to review the external connection order.', 1),
    ('Ports and Cable Matching Practice', 'quick_tip', 'Check Shape and Purpose', 'Video goes to the monitor port, network cable goes to LAN/Ethernet, storage devices often use SATA, and the power cable is connected last.', 2),

    ('Module 5 Hardware Review Check', 'paragraph', 'Review Goal', 'This final review combines hardware identification, disassembly order, assembly order, ports, cables, and safety procedures from the PDF module.', 1),
    ('Module 5 Hardware Review Check', 'summary', 'Before You Start', 'Review the pre-assessment topics, the three main lessons, the activity prompts, and the post-test style component identification items.', 2)
) as section_data(lesson_title, section_type, title, body, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, media_url, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.media_url,
  section_data.sort_order
from public.lessons
join (
  values
    ('Module 5 Guide and Pre-Test', 'paragraph', 'How to Use This Module', 'Set aside other tasks before starting. Read each page carefully, write important concepts in your notebook, complete the activities, review feedback, and use the post-test to check what you learned.', null, 1),
    ('Module 5 Guide and Pre-Test', 'key_points', 'Expectations', 'Identify the proper procedure for disassembly and assembly.\nObserve the proper procedure for disassembly and assembly.\nApply occupational health and safety procedures.\nPerform basic skills needed to assemble and disassemble a PC.', null, 2),
    ('Module 5 Guide and Pre-Test', 'vocabulary', 'Parts of the Module', 'Expectations - skills you should know after the module.\nPre-test - checks prior knowledge.\nLooking Back - reviews previous learning.\nBrief Introduction - gives the lesson overview.\nActivities - let you practice with a task.\nRemember - summarizes key ideas.\nCheck Your Understanding - verifies learning.\nPost-test - measures learning after the module.', null, 3),
    ('Module 5 Guide and Pre-Test', 'resource', 'Computer Hardware Overview', 'The PDF opens with ICT-CSS Module 5: Assembly and Disassembly PC. This lesson prepares students for hardware identification before hands-on assembly work.', 'assets/module5/prepare-workplace.png', 4),

    ('Lesson 1: Disassembling a Personal Computer', 'paragraph', 'Brief Introduction', 'Disassembly is the safe and organized removal of PC parts. Prepare tools, keep a container for screws, unplug everything first, and remove parts in a procedure that prevents damage.', null, 1),
    ('Lesson 1: Disassembling a Personal Computer', 'key_points', 'Objectives', 'Identify the hardware to be disassembled.\nRecognize the importance of following disassembly procedure.\nDemonstrate the steps in disassembling a personal computer.', null, 2),
    ('Lesson 1: Disassembling a Personal Computer', 'steps', 'PDF Disassembly Procedure', '1. Unplug every cable connected to the computer.\n2. Open the outer shell or case by removing back screws.\n3. Unplug and remove the system fan.\n4. Unplug and remove the CPU fan from the heat sink.\n5. Disconnect power supply cables from the motherboard and drives.\n6. Remove the CD/DVD or optical drive.\n7. Remove the hard drive.\n8. Remove memory by pressing both locking tabs.\n9. Remove the motherboard last.', null, 3),
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'Unplugging First', 'The PDF shows unplugging as the first safety step before opening or touching internal components.', 'assets/module5/unplug-power-cable.jpg', 4),
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'System Fan and CPU Fan', 'Fans are removed only after their power connectors are unplugged and screws or clips are released.', 'assets/module5/system-fan.jpg', 5),
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'CPU Fan and Heat Sink', 'The CPU fan sits above the heat sink and plugs into the motherboard. Follow the wire to locate the connector before removal.', 'assets/module5/cpu-fan-and-heatsink.jpg', 6),
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'Memory and Motherboard Removal', 'RAM is removed by pressing both locking tabs. The motherboard is removed last after connected parts are cleared.', 'assets/module5/motherboard-removal.png', 7),
    ('Lesson 1: Disassembling a Personal Computer', 'summary', 'Remember', 'Unplug first.\nKeep screws organized.\nDisconnect fan and power cables before removing parts.\nNever force RAM or cards.\nMotherboard removal comes last.', null, 8),

    ('Lesson 1 Practice: Disassembly Activities', 'reflection', 'Activity 1', 'Answer in your notebook: Why do we need to discharge our body before touching computer components? What is the essence of following correct procedures when connecting PC parts?', null, 1),
    ('Lesson 1 Practice: Disassembly Activities', 'reflection', 'Activity 2', 'Write the disassembly procedure from memory without looking at the lesson. Then compare your answer with the correct order.', null, 2),
    ('Lesson 1 Practice: Disassembly Activities', 'quick_tip', 'Check Your Understanding', 'The first step is unplugging all cables and wires. The motherboard is removed last.', null, 3),

    ('Lesson 2: Assembling a Personal Computer', 'paragraph', 'Brief Introduction', 'Assembly requires planning and careful handling. Take inventory, prepare the workplace, check manuals, install board-level components, mount the motherboard, connect power, and install cards and drives.', null, 1),
    ('Lesson 2: Assembling a Personal Computer', 'key_points', 'Objectives', 'Identify the hardware to be assembled.\nRecognize the importance of following assembly procedure.\nDemonstrate the steps in assembling a personal computer.', null, 2),
    ('Lesson 2: Assembling a Personal Computer', 'steps', 'PDF Assembly Procedure', '1. Prepare your workplace.\n2. Prepare the motherboard.\n3. Install the CPU.\n4. Install the CPU heat sink and fan.\n5. Install RAM modules.\n6. Place the motherboard into the case.\n7. Connect the power supply.\n8. Install graphics or video cards.\n9. Install internal drives.\n10. Install add-in cards.', null, 3),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'CPU Installation', 'Open the CPU socket lever, align the CPU and socket marks, place the CPU gently, and lock the lever.', 'assets/module5/cpu-install.jpg', 4),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'CPU Alignment Marks', 'The CPU fits only in the correct orientation. Check the triangular mark or missing-pin corner before locking the socket.', 'assets/module5/cpu-alignment.png', 5),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'Motherboard Standoffs', 'Use the correct standoff locations before securing the motherboard to the case.', 'assets/module5/motherboard-standoffs.png', 6),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'Power and Front-Panel Connectors', 'Connect the large ATX connector, CPU power connector, and small front-panel leads according to the motherboard manual.', 'assets/module5/front-panel-connectors.png', 7),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'Graphics Card and Internal Drives', 'Install the graphics card in the correct expansion slot and mount internal drives using the case rails, cages, or screws.', 'assets/module5/graphics-card-install.jpg', 8),
    ('Lesson 2: Assembling a Personal Computer', 'summary', 'Remember', 'Prepare first.\nCheck the motherboard manual.\nAlign CPU and RAM before pressing.\nUse standoffs.\nConnect power and data cables carefully.\nInstall cards and drives after the motherboard is secured.', null, 9),

    ('Lesson 2 Practice: Assembly Activities', 'reflection', 'Activity 1', 'Answer in your notebook: Why should we never exert too much force when attaching cables or PC parts? As a computer technician, why are careful skills important?', null, 1),
    ('Lesson 2 Practice: Assembly Activities', 'reflection', 'Activity 2', 'Write the assembly procedure from memory without looking at the lesson. Then compare your answer with the correct order.', null, 2),
    ('Lesson 2 Practice: Assembly Activities', 'quick_tip', 'Check Your Understanding', 'Assembly starts with workplace and motherboard preparation, then CPU, heat sink, RAM, motherboard mounting, power, video card, internal drives, and add-in cards.', null, 3),

    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'paragraph', 'Brief Introduction', 'Peripherals and external hardware must be connected to the correct ports. Check cable shape, connector orientation, labels, and the port function before plugging anything in.', null, 1),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'key_points', 'Objectives', 'Identify ports and cables used in a computer.\nExplain the function of ports and cables.\nDemonstrate the steps in connecting cables to computer ports.', null, 2),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'safety_note', 'Keep in Mind', 'Never force a connection. Plug in the power cable only after all other cables have been connected and checked.', null, 3),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'steps', 'PDF Peripheral Connection Procedure', '1. Attach the monitor VGA cable to the video port.\n2. Secure the cable by tightening the connector screws.\n3. Plug the keyboard cable into the PS/2 keyboard port or USB port.\n4. Plug the mouse cable into the PS/2 mouse port or USB port.\n5. Plug USB devices into USB ports.\n6. Plug the network cable into the network port.\n7. Plug the power cable into the power supply.', null, 4),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'Ports and Cable Examples', 'The PDF shows common external ports such as USB, PS/2, VGA, audio, and network connections.', 'assets/module5/ports-usb-ps2.png', 5),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'VGA and Monitor Cable', 'A VGA cable connects the monitor to the video port and may be secured with connector screws.', 'assets/module5/vga-cable.png', 6),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'Keyboard, Mouse, USB, and Network Connections', 'Keyboard and mouse may use PS/2 or USB. USB devices use USB ports. Network cable uses the LAN/Ethernet port.', 'assets/module5/peripheral-procedure.png', 7),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'Back Panel After Connecting Cables', 'After the cables are connected properly, the back panel should show each device in its correct port.', 'assets/module5/back-panel-complete.png', 8),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'summary', 'Remember', 'Monitor connects to video.\nKeyboard and mouse connect to PS/2 or USB.\nUSB devices connect to USB ports.\nNetwork connects to LAN/Ethernet.\nPower cable is connected last.', null, 9),

    ('Lesson 3 Practice: Ports and Cables Activities', 'reflection', 'Activity 1', 'Answer in your notebook: Why should the power cable always be last in the connection procedure? What do we need to check when connecting a cable to its port?', null, 1),
    ('Lesson 3 Practice: Ports and Cables Activities', 'reflection', 'Activity 2', 'Write the procedure for connecting peripherals from memory without looking at the lesson. Then compare your answer with the correct order.', null, 2),
    ('Lesson 3 Practice: Ports and Cables Activities', 'quick_tip', 'Check Your Understanding', 'Arrange the cable connection pictures or steps in order, then explain what each step shows.', null, 3),

    ('Post-Test: Module 5 Hardware and Procedures', 'paragraph', 'Post-Test Review', 'This check covers the Module 5 post-test ideas: identify computer parts, recognize connectors, recall procedure order, and explain safe connection habits.', null, 1),
    ('Post-Test: Module 5 Hardware and Procedures', 'resource', 'Hardware Identification Picture', 'Use this PC interior picture to review common parts such as the CPU fan and heat sink, power supply, drives, video card, front panel, memory slots, CPU, and video card slot.', 'assets/module5/pc-inside-identification.jpg', 2),
    ('Post-Test: Module 5 Hardware and Procedures', 'vocabulary', 'Connector Review', '20/24-pin ATX connector - supplies motherboard power.\nBack-panel wires - external cables removed first during disassembly.\n4-pin Berg - used with older floppy disk drives.\nP4 12V connector - auxiliary motherboard or CPU power.\nSATA cable - data cable for hard drives, optical drives, and SSDs.\n4-pin Molex - powers some drives and internal components.\nData cable - passes information processed or saved by the computer.', null, 3),
    ('Post-Test: Module 5 Hardware and Procedures', 'summary', 'Final Reminder', 'Follow the correct procedure, observe OHS standards, and connect cables only to their correct ports. Correct procedure prevents damage to the computer and protects the technician.', null, 4)
) as section_data(lesson_title, section_type, title, body, media_url, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, media_url, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.media_url,
  section_data.sort_order
from public.lessons
join (
  values
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'RAM Removal', 'Press the locking tabs on both ends before lifting the RAM module. Hold the module by its edges and avoid touching the gold contacts.', 'assets/module5/ram-removal.png', 9),
    ('Lesson 1: Disassembling a Personal Computer', 'resource', 'Optical Drive', 'The optical drive is removed only after its data and power connectors are unplugged. Slide it carefully from the drive bay.', 'assets/module5/optical-drive.jpg', 10),

    ('Lesson 2: Assembling a Personal Computer', 'resource', 'ATX Motherboard Power Connector', 'The 20/24-pin ATX connector supplies power to the motherboard. Align the latch and connector shape before pressing it into place.', 'assets/module5/atx-power-connector.png', 10),
    ('Lesson 2: Assembling a Personal Computer', 'resource', 'Internal Drive Installation', 'Internal drives should be mounted in the correct bay or rail before connecting SATA data and power cables.', 'assets/module5/internal-drive-install.png', 11),

    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'VGA, Audio, and LAN Ports', 'Back-panel ports have different shapes and purposes. Match the connector to the correct video, audio, USB, PS/2, or network port.', 'assets/module5/ports-vga-audio-lan.png', 10),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'USB Connection', 'USB devices connect to USB ports. Check the connector orientation and do not force it if it does not fit.', 'assets/module5/usb-connection.png', 11),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'Network Port', 'The LAN or Ethernet cable connects to the network port. It is used when the computer needs a wired network connection.', 'assets/module5/network-port.jpg', 12),
    ('Lesson 3: Connecting Peripherals of a Personal Computer', 'resource', 'Power Supply Cable', 'The power cable is connected last after the monitor, keyboard, mouse, USB devices, and network cable are already checked.', 'assets/module5/power-supply-cable.png', 13),

    ('Post-Test: Module 5 Hardware and Procedures', 'resource', 'Component Identification Review', 'Review the PC interior image and practice naming each visible component before answering post-test style questions.', 'assets/module5/pc-inside-identification.jpg', 7)
) as section_data(lesson_title, section_type, title, body, media_url, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.sort_order
from public.lessons
join (
  values
    ('Module Orientation and Safety Expectations', 'reflection', 'PDF Looking Back: Hardware Functions', 'Practice naming the function of common parts before proceeding: motherboard, CPU fan and heat sink, power supply, RAM, hard drive, optical drive, video card, front panel, and power connectors.', 7),
    ('Topic 1: Disassembling a Personal Computer', 'reflection', 'PDF Activity: Disassembly Reasoning', 'Answer in your notebook: Why do we need to discharge our body before touching computer components? What is the essence of following correct procedures when connecting PC parts?', 9),
    ('Topic 2: Assembling a Personal Computer', 'reflection', 'PDF Activity: Assembly Reasoning', 'Answer in your notebook: Why should we never exert too much force when attaching PC cables? As a computer technician, why are careful skills a crucial factor?', 9),
    ('Topic 3: Connecting Peripherals and Cables', 'reflection', 'PDF Activity: Cable Orientation', 'Answer in your notebook: Why should the power cable be connected last? What should you check to confirm that a cable is connected to the proper orientation and port?', 9),
    ('Final Assessment: PC Assembly and Cable Connections', 'vocabulary', 'Post-Test Identification Review', 'Review these common identification answers: CPU fan and heat sink, power supply, power supply connector, hard drive or floppy drive, video card, front panel, optical disk drive, memory slots, CPU, and video card slot.', 4)
) as section_data(lesson_title, section_type, title, body, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'PC Assembly and Disassembly'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, media_url, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.media_url,
  section_data.sort_order
from public.lessons
join (
  values
    ('Module Orientation and Safety Expectations', 'resource', 'Computer Hardware Overview', 'Use this visual as a preview of the parts and safe work area students will study in this module.', 'assets/module5/prepare-workplace.png', 8),

    ('Topic 1: Disassembling a Personal Computer', 'resource', 'Unplug Cables First', 'Before opening the case, unplug all external cables and wires connected to the computer.', 'assets/module5/unplug-power-cable.jpg', 10),
    ('Topic 1: Disassembling a Personal Computer', 'resource', 'System Fan', 'A system fan moves air through the case and should be unplugged before removal.', 'assets/module5/system-fan.jpg', 11),
    ('Topic 1: Disassembling a Personal Computer', 'resource', 'CPU Fan and Heat Sink', 'The CPU fan and heat sink cool the processor. Follow the fan wire to locate its motherboard connector.', 'assets/module5/cpu-fan-and-heatsink.jpg', 12),
    ('Topic 1: Disassembling a Personal Computer', 'resource', 'RAM Removal', 'RAM modules are released by pressing both locking tabs before lifting the module by its edges.', 'assets/module5/ram-removal.png', 13),
    ('Topic 1: Disassembling a Personal Computer', 'resource', 'Motherboard Removal', 'The motherboard should be removed last after drives, memory, fans, cards, and cables are already cleared.', 'assets/module5/motherboard-removal.png', 14),

    ('Topic 2: Assembling a Personal Computer', 'resource', 'CPU Installation', 'Open the CPU socket lever, align the CPU mark with the socket mark, place the CPU gently, and lock it in place.', 'assets/module5/cpu-install.jpg', 10),
    ('Topic 2: Assembling a Personal Computer', 'resource', 'CPU Alignment Marks', 'Check the triangle or missing-pin corner before locking the CPU into the socket.', 'assets/module5/cpu-alignment.png', 11),
    ('Topic 2: Assembling a Personal Computer', 'resource', 'Motherboard Standoffs', 'Standoffs keep the motherboard lifted from the case metal to help prevent short circuits.', 'assets/module5/motherboard-standoffs.png', 12),
    ('Topic 2: Assembling a Personal Computer', 'resource', 'ATX Motherboard Power Connector', 'The 20/24-pin ATX connector supplies main motherboard power. Align the latch and connector shape before pressing.', 'assets/module5/atx-power-connector.png', 13),
    ('Topic 2: Assembling a Personal Computer', 'resource', 'Graphics Card Installation', 'Install the graphics card in the correct expansion slot and secure it properly.', 'assets/module5/graphics-card-install.jpg', 14),
    ('Topic 2: Assembling a Personal Computer', 'resource', 'Internal Drive Installation', 'Mount internal drives in the correct bay or rail before connecting data and power cables.', 'assets/module5/internal-drive-install.png', 15),

    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'USB and PS/2 Ports', 'Keyboard and mouse may connect through PS/2 or USB ports depending on the device.', 'assets/module5/ports-usb-ps2.png', 10),
    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'VGA and Monitor Cable', 'A VGA cable connects the monitor to the video port and may be secured with screws.', 'assets/module5/vga-cable.png', 11),
    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'Back-Panel Ports', 'Match each connector to the correct video, audio, USB, PS/2, or network port.', 'assets/module5/ports-vga-audio-lan.png', 12),
    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'USB Connection', 'USB devices connect to USB ports. Check orientation and do not force the connector.', 'assets/module5/usb-connection.png', 13),
    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'Network Port', 'The LAN or Ethernet cable connects to the network port for wired network access.', 'assets/module5/network-port.jpg', 14),
    ('Topic 3: Connecting Peripherals and Cables', 'resource', 'Power Cable Last', 'Connect the power cable only after all other cables have been connected and checked.', 'assets/module5/power-supply-cable.png', 15),

    ('Final Assessment: PC Assembly and Cable Connections', 'resource', 'Component Identification Review', 'Use this PC interior visual to review the CPU fan and heat sink, power supply, drives, video card, front panel, memory slots, CPU, and video card slot.', 'assets/module5/pc-inside-identification.jpg', 5)
) as section_data(lesson_title, section_type, title, body, media_url, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'PC Assembly and Disassembly'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.lesson_questions (lesson_id, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
select
  lessons.id,
  question_data.question_type,
  question_data.prompt,
  question_data.choices::jsonb,
  question_data.correct_answer,
  question_data.hint,
  question_data.explanation,
  question_data.points,
  question_data.sort_order
from public.lessons
join (
  values
    ('Hardware Identification Practice', 'identification', 'Identify the part that holds the CPU, memory, cards, connectors, and ports together as the main circuit board.', '["Motherboard", "Power supply", "CPU fan", "Optical drive"]', 'motherboard|mainboard|system board', 'It is the main board of the computer.', 'The motherboard is the main circuit board where core parts and connectors attach.', 1, 1),
    ('Hardware Identification Practice', 'multiple_choice', 'Which part cools the processor during operation?', '["CPU fan and heat sink", "Power supply", "SATA cable", "Front panel"]', 'CPU fan and heat sink', 'Cooling is usually mounted above the CPU.', 'The CPU fan and heat sink remove heat from the processor.', 1, 2),
    ('Hardware Identification Practice', 'identification', 'Identify the temporary memory installed in slots with locking tabs at both ends.', '["RAM", "CPU", "LAN port", "Power supply"]', 'ram|memory|memory module|ram module', 'It uses locking clips.', 'RAM is temporary memory and is held by locking tabs on the slot.', 1, 3),
    ('Hardware Identification Practice', 'multiple_choice', 'Which part provides electrical power to the motherboard and drives?', '["Power supply unit", "Video card", "Optical drive", "Memory slot"]', 'Power supply unit', 'It has many power cables.', 'The PSU converts and supplies power to internal computer components.', 1, 4),
    ('Hardware Identification Practice', 'multiple_choice', 'Which item is part of typical front-panel wiring?', '["HDD LED", "Monitor brightness", "Desktop wallpaper", "Mouse pointer"]', 'HDD LED', 'Think about the small case leads.', 'HDD LED, Power LED, Reset SW, and Power SW are common front-panel leads.', 1, 5),
    ('Hardware Identification Practice', 'multiple_choice', 'Which internal drive reads CDs or DVDs?', '["Optical drive", "Flash drive", "Video card", "CPU socket"]', 'Optical drive', 'It reads disc media.', 'An optical drive reads optical discs such as CDs and DVDs.', 1, 6),
    ('Hardware Identification Practice', 'true_false', 'A flash drive is usually an internal drive mounted inside the system unit.', '["True", "False"]', 'False', 'Think of removable USB storage.', 'A flash drive is usually removable external storage, not an internal drive.', 1, 7),
    ('Hardware Identification Practice', 'identification', 'Identify the expansion card used for graphics or monitor display output.', '["Video card", "RAM", "Power supply", "CPU fan"]', 'video card|graphics card|gpu|graphic card', 'It is installed in a graphics expansion slot.', 'A video or graphics card provides display output and graphics processing.', 1, 8),

    ('Ports and Cable Matching Practice', 'multiple_choice', 'Which port is commonly used to connect a monitor?', '["VGA port", "Audio port", "LAN port", "Berg connector"]', 'VGA port', 'It is a video connection.', 'A VGA port connects a monitor or display cable.', 1, 1),
    ('Ports and Cable Matching Practice', 'multiple_choice', 'Which cable is commonly used for hard drives, optical drives, and SSDs?', '["SATA cable", "PS/2 cable", "Audio cable", "Monitor stand"]', 'SATA cable', 'It is a modern storage data cable.', 'SATA is a common data cable for storage drives.', 1, 2),
    ('Ports and Cable Matching Practice', 'identification', 'Identify the auxiliary power connector used by motherboards with Pentium 4 or later processors.', '["P4 12V connector", "LAN port", "USB port", "VGA port"]', 'p4 12v connector|p4 connector|cpu power connector', 'It provides extra CPU or motherboard power.', 'The P4 12V connector supplies auxiliary power near the processor area.', 1, 3),
    ('Ports and Cable Matching Practice', 'multiple_choice', 'Which connector is commonly used with older floppy disk drives?', '["4-pin Berg connector", "VGA connector", "RJ-45 connector", "SATA data cable"]', '4-pin Berg connector', 'It is associated with floppy drives.', 'A 4-pin Berg connector is commonly used for older floppy disk drives.', 1, 4),
    ('Ports and Cable Matching Practice', 'multiple_choice', 'Which port connects a computer to a local area network?', '["LAN or Ethernet port", "Audio port", "PS/2 keyboard port", "VGA port"]', 'LAN or Ethernet port', 'It uses a network cable.', 'The LAN or Ethernet port connects a computer to a network.', 1, 5),
    ('Ports and Cable Matching Practice', 'ordering', 'Arrange the external cable connection procedure. Type the numbers separated by commas.', '["Attach monitor cable to video port", "Secure monitor cable screws", "Plug keyboard into PS/2 or USB", "Plug mouse into PS/2 or USB", "Plug USB devices", "Plug network cable", "Plug power cable"]', '1,2,3,4,5,6,7', 'Power is connected last.', 'The power cable is connected last after the other cables are in the correct ports.', 3, 6),

    ('Module 5 Hardware Review Check', 'multiple_choice', 'What is the first thing to do before touching a computer for disassembly?', '["Unplug the cables and wires", "Remove the motherboard", "Install the hard drive", "Connect the power cable"]', 'Unplug the cables and wires', 'Start with safety.', 'Unplugging all cables and wires is the first safety step.', 1, 1),
    ('Module 5 Hardware Review Check', 'multiple_choice', 'Why should the workplace be prepared before PC servicing?', '["To arrange tools and support safety", "To change the wallpaper", "To skip the procedure", "To make the PC heavier"]', 'To arrange tools and support safety', 'Think about tools, time, and safe work.', 'A prepared workplace helps keep tools ready, parts organized, and work safer.', 1, 2),
    ('Module 5 Hardware Review Check', 'ordering', 'Arrange the disassembly order. Type the numbers separated by commas.', '["Remove hard drive", "Detach power supply", "Open case", "Pull out motherboard", "Remove CD/DVD drives", "Remove CPU fan", "Remove system fan", "Unplug all cables and wires", "Remove memory"]', '8,3,7,6,2,5,1,9,4', 'Unplug first. Motherboard last.', 'The PDF disassembly order starts with unplugging and ends with the motherboard.', 3, 3),
    ('Module 5 Hardware Review Check', 'ordering', 'Arrange the assembly order. Type the numbers separated by commas.', '["Connect power supply", "Install graphics/video card", "Install internal drives", "Install RAM", "Install add-in cards", "Install CPU", "Install CPU heat sink", "Place motherboard into case", "Prepare motherboard", "Prepare workplace"]', '10,9,6,7,4,8,1,2,3,5', 'Prepare first, then board-level parts.', 'The PDF assembly order begins with workplace and motherboard preparation, then CPU, heat sink, RAM, case mounting, power, cards, and drives.', 3, 4),
    ('Module 5 Hardware Review Check', 'multiple_choice', 'Which connector supplies power to the motherboard?', '["20/24-pin ATX power connector", "IDE cable", "VGA port", "Audio jack"]', '20/24-pin ATX power connector', 'It is the large motherboard power connector.', 'The 20/24-pin ATX connector supplies main motherboard power.', 1, 5),
    ('Module 5 Hardware Review Check', 'short_answer', 'Why should you never force cables or PC parts into place?', '[]', 'to prevent damage|avoid damage|prevent bent pins|avoid bent pins|protect components|prevent breaking parts', 'Think about pins, ports, and connectors.', 'Forcing parts can bend pins, damage ports, break connectors, or cause unsafe assembly.', 1, 6),
    ('Module 5 Hardware Review Check', 'true_false', 'The power cable should be plugged in only after the other cables have been connected and checked.', '["True", "False"]', 'True', 'Power is last.', 'Connecting power last reduces the chance of powering the computer while cables are loose or being handled.', 1, 7),
    ('Module 5 Hardware Review Check', 'short_answer', 'Why do we discharge our body before touching computer components?', '[]', 'prevent esd|prevent electrostatic discharge|avoid static damage|prevent static electricity damage|protect components', 'Use the term ESD if you know it.', 'Discharging the body helps prevent electrostatic discharge from damaging sensitive parts.', 1, 8)
) as question_data(lesson_title, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
  on lessons.title = question_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_questions existing_question
    where existing_question.lesson_id = lessons.id
      and existing_question.prompt = question_data.prompt
  );

insert into public.lesson_questions (lesson_id, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
select
  lessons.id,
  question_data.question_type,
  question_data.prompt,
  question_data.choices::jsonb,
  question_data.correct_answer,
  question_data.hint,
  question_data.explanation,
  question_data.points,
  question_data.sort_order
from public.lessons
join (
  values
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'How will you secure the safety of the CPU?', '["By following the steps", "By washing your hands first", "By wearing the anti-static wrist strap", "By listening carefully to your teacher"]', 'By wearing the anti-static wrist strap', 'Think about static electricity.', 'The PDF pre-test identifies the anti-static wrist strap as the safety tool for handling the CPU and other sensitive parts.', 1, 1),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'What is the use of an anti-static bag?', '["A bag used for storing electronic components for inventory", "A bag used for storing electronic components before and after the activity", "A bag used for storing electronic components to prevent the loss of the components", "A bag used for storing electronic components which are prone to damage caused by electrostatic discharge"]', 'A bag used for storing electronic components which are prone to damage caused by electrostatic discharge', 'It protects parts from static electricity.', 'Anti-static bags protect electronic components that can be damaged by electrostatic discharge.', 1, 2),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'All of the following are front-panel wiring except one. Which one is not included?', '["Power LED", "Reset switch", "Switch on", "HDD LED"]', 'Switch on', 'Look for the labels usually found on small case leads.', 'Power LED, Reset switch, and HDD LED are common front-panel wiring labels. "Switch on" is not the usual front-panel label in the PDF item.', 1, 3),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'Before you remove the CPU fan, what is the first thing to do?', '["Remove the four screws", "Remove the power cord", "Remove the CPU", "Remove the heat sink"]', 'Remove the power cord', 'Safety comes before removing parts.', 'The PDF pre-test checks that power is removed before working near the CPU fan and heat sink.', 1, 4),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'All of the following are internal drives of the computer except one. Which one is not an internal drive?', '["Flash Drive", "Floppy Drive", "Hard Drive", "Optical Drive"]', 'Flash Drive', 'Think of removable storage.', 'A flash drive is usually removable external storage, while floppy, hard, and optical drives can be installed internally.', 1, 5),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'What is the last component to be removed in disassembly of a computer?', '["Floppy Disk Drive", "Hard Disk Drive", "Power Supply", "Motherboard"]', 'Motherboard', 'Many components and connectors attach to it.', 'The motherboard is removed last after connected parts, drives, memory, and cables are cleared.', 1, 6),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'What is the first thing to do before you touch your computer for disassembling?', '["Unplug the cable and wires", "Clean your computer", "Unscrew the computer", "Remove the chassis"]', 'Unplug the cable and wires', 'Start with safety.', 'Unplugging cables and wires is the first safety step before disassembly.', 1, 7),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'Why is it important to prepare the workplace before you do the activity or work?', '["To ensure the safety of the student", "To finish early before the time", "To prepare the readiness", "To arrange the tools and materials needed"]', 'To arrange the tools and materials needed', 'Think about tools and materials before starting.', 'A prepared workplace keeps tools and materials ready and makes the activity safer and more organized.', 1, 8),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'What cable passes or saves the information being provided and processed by the computer?', '["Data Cable", "P4 12V Connector", "SATA Connector", "20/24 pin Molex"]', 'Data Cable', 'The name describes what it carries.', 'A data cable carries information between devices such as drives and the motherboard.', 1, 9),
    ('Module 5 Guide and Pre-Test', 'multiple_choice', 'What power supply cable is used with motherboards that have an Intel Pentium 4 or later processor?', '["SATA Port", "SATA Cable", "P4 12V Connector", "4 Pin Berg"]', 'P4 12V Connector', 'It provides auxiliary CPU or motherboard power.', 'The P4 12V connector supplies auxiliary power for motherboards using Pentium 4 or later processors.', 1, 10),

    ('Lesson 1 Practice: Disassembly Activities', 'ordering', 'Arrange the PDF disassembly procedure. Type the numbers separated by commas.', '["Detach the hard drive", "Detach the power supply", "Open the outer shell or case", "Pull out the motherboard", "Remove the CD/DVD drives", "Remove the CPU fan", "Remove the system fan", "Unplug all cables and wires", "Remove memory"]', '8,3,7,6,2,5,1,9,4', 'Unplug first. Motherboard last.', 'The PDF order starts with unplugging, then opening the case, removing fans, power, drives, memory, and finally the motherboard.', 3, 1),
    ('Lesson 1 Practice: Disassembly Activities', 'short_answer', 'Why do we need to discharge our body before touching computer components?', '[]', 'prevent esd|prevent electrostatic discharge|avoid static damage|prevent static electricity damage|protect components', 'Use the term ESD if you know it.', 'Discharging your body helps prevent electrostatic discharge from damaging sensitive components.', 1, 2),
    ('Lesson 1 Practice: Disassembly Activities', 'multiple_choice', 'Before removing the CPU fan, what should you do first?', '["Unplug the fan connector", "Remove the motherboard", "Plug in the power cable", "Install the hard drive"]', 'Unplug the fan connector', 'Follow the fan wire.', 'The CPU fan connector should be unplugged before the fan is removed from the heat sink.', 1, 3),
    ('Lesson 1 Practice: Disassembly Activities', 'true_false', 'The motherboard should be pulled out before removing memory, fans, drives, and power cables.', '["True", "False"]', 'False', 'Motherboard removal is near the end.', 'The motherboard is removed last because many parts and connectors attach to it.', 1, 4),

    ('Lesson 2 Practice: Assembly Activities', 'ordering', 'Arrange the PDF assembly procedure. Type the numbers separated by commas.', '["Connect the power supply", "Install graphics/video card", "Install internal drives", "Install RAM modules", "Install add-in cards", "Install the CPU", "Install the CPU heat sink", "Place motherboard into case", "Prepare motherboard", "Prepare workplace"]', '10,9,6,7,4,8,1,2,3,5', 'Prepare workplace first.', 'The PDF order begins with preparation, then CPU, cooling, RAM, motherboard mounting, power, graphics/video card, drives, and add-in cards.', 3, 1),
    ('Lesson 2 Practice: Assembly Activities', 'multiple_choice', 'Why should motherboard standoffs be installed correctly?', '["They prevent short circuits", "They erase old files", "They replace the CPU fan", "They connect the keyboard"]', 'They prevent short circuits', 'The board should not touch the case metal in the wrong places.', 'Standoffs lift and support the motherboard, helping prevent short circuits.', 1, 2),
    ('Lesson 2 Practice: Assembly Activities', 'multiple_choice', 'What should be checked when installing the CPU?', '["CPU and socket alignment marks", "Monitor brightness", "Desktop wallpaper", "Keyboard color"]', 'CPU and socket alignment marks', 'Look for the triangle or missing-pin corner.', 'Alignment marks show the correct CPU orientation before locking it into the socket.', 1, 3),
    ('Lesson 2 Practice: Assembly Activities', 'short_answer', 'Why should we never exert too much force when attaching cables or PC parts?', '[]', 'prevent damage|avoid damage|prevent bent pins|avoid bent pins|protect components|avoid breaking parts', 'Think about ports and pins.', 'Too much force can bend pins, break connectors, or damage components.', 1, 4),

    ('Lesson 3 Practice: Ports and Cables Activities', 'ordering', 'Arrange the PDF external hardware connection procedure. Type the numbers separated by commas.', '["Attach monitor cable", "Secure connector screws", "Plug keyboard cable", "Plug mouse cable", "Plug USB cable", "Plug network cable", "Plug power cable"]', '1,2,3,4,5,6,7', 'Power cable comes last.', 'The PDF procedure connects monitor, keyboard, mouse, USB, and network first. Power is connected last.', 3, 1),
    ('Lesson 3 Practice: Ports and Cables Activities', 'multiple_choice', 'Why should the power cable be the last connection?', '["It confirms other cables are already connected and checked", "It makes the monitor brighter", "It replaces the network cable", "It removes static automatically"]', 'It confirms other cables are already connected and checked', 'Power should not be applied while you are still handling other cables.', 'Connecting power last reduces risk while cables and peripherals are still being arranged.', 1, 2),
    ('Lesson 3 Practice: Ports and Cables Activities', 'multiple_choice', 'Which cable connects to a network port?', '["LAN or Ethernet cable", "VGA cable", "Audio cable", "Berg connector"]', 'LAN or Ethernet cable', 'It is for networking.', 'The LAN or Ethernet cable connects the computer to a network port.', 1, 3),
    ('Lesson 3 Practice: Ports and Cables Activities', 'true_false', 'When a connector does not fit, it is safe to force it into the port.', '["True", "False"]', 'False', 'Check orientation and shape.', 'Never force a connector. Check the port, shape, pins, orientation, and label.', 1, 4),

    ('Post-Test: Module 5 Hardware and Procedures', 'identification', 'Identify the part that cools the CPU.', '["CPU fan and heat sink", "Power supply", "LAN port", "SATA cable"]', 'cpu fan and heat sink|cpu fan|heat sink|heatsink', 'It sits above the processor.', 'The CPU fan and heat sink cool the processor.', 1, 1),
    ('Post-Test: Module 5 Hardware and Procedures', 'identification', 'Identify the part that provides power to the computer components.', '["Power supply", "Video card", "Memory slot", "Audio port"]', 'power supply|psu|power supply unit', 'It has many power cables.', 'The power supply provides power to the motherboard and drives.', 1, 2),
    ('Post-Test: Module 5 Hardware and Procedures', 'multiple_choice', 'Which connector supplies power to the motherboard?', '["20/24-pin ATX connector", "IDE cable", "Data cable", "VGA port"]', '20/24-pin ATX connector', 'It is the large motherboard power connector.', 'The 20/24-pin ATX connector supplies main motherboard power.', 1, 3),
    ('Post-Test: Module 5 Hardware and Procedures', 'multiple_choice', 'Which connector is commonly used with older floppy disk drives?', '["4-pin Berg", "SATA cable", "VGA connector", "LAN port"]', '4-pin Berg', 'It is a small older drive power connector.', 'A 4-pin Berg connector is commonly used for floppy disk drives.', 1, 4),
    ('Post-Test: Module 5 Hardware and Procedures', 'multiple_choice', 'Which cable is used for hard drives, optical drives, and SSDs?', '["SATA cable", "Audio cable", "PS/2 cable", "Monitor stand"]', 'SATA cable', 'It is a modern storage data cable.', 'SATA cables connect storage devices such as hard drives, optical drives, and solid-state drives.', 1, 5),
    ('Post-Test: Module 5 Hardware and Procedures', 'multiple_choice', 'Which cable carries information being processed or saved by the computer?', '["Data cable", "P4 12V connector", "20/24-pin connector", "Power cable"]', 'Data cable', 'The name describes what it carries.', 'A data cable carries information between devices such as drives and the motherboard.', 1, 6),
    ('Post-Test: Module 5 Hardware and Procedures', 'multiple_choice', 'Which part is NOT an internal drive?', '["Flash drive", "Hard drive", "Floppy drive", "Optical drive"]', 'Flash drive', 'Think of removable storage.', 'A flash drive is usually external/removable, while hard, floppy, and optical drives can be internal.', 1, 7),
    ('Post-Test: Module 5 Hardware and Procedures', 'short_answer', 'Why is following the correct procedure important in assembly and disassembly?', '[]', 'prevent damage|avoid damage|protect components|avoid accidents|safety|protect the technician', 'Think about safety and hardware protection.', 'Correct procedures protect the technician, prevent component damage, and make the work easier to check.', 1, 8)
) as question_data(lesson_title, question_type, prompt, choices, correct_answer, hint, explanation, points, sort_order)
  on lessons.title = question_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_questions existing_question
    where existing_question.lesson_id = lessons.id
      and existing_question.prompt = question_data.prompt
  );

insert into public.lesson_sections (lesson_id, section_type, title, body, sort_order)
select
  lessons.id,
  section_data.section_type,
  section_data.title,
  replace(section_data.body, E'\\n', chr(10)),
  section_data.sort_order
from public.lessons
join (
  values
    ('Module 5 Guide and Pre-Test', 'resource', 'PDF Source Context', 'This lesson package is based on the ICT-CSS 10 Assembly and Disassembly PC Module 5 for Quarter 1 Week 5, focused on common competencies in computer standard operating procedures and the learning objective: assemble computer hardware.', 5),
    ('Post-Test: Module 5 Hardware and Procedures', 'resource', 'Module References', 'The PDF module lists references from computer hardware servicing learning materials, PC assembly guides, online assembly videos, DepEd learning resources, and IT Essentials PC Hardware and Software references.', 5),
    ('Post-Test: Module 5 Hardware and Procedures', 'summary', 'Acknowledgement', 'The PDF acknowledges its writers, editors, reviewers, and management team from DepEd Manila. This website version adapts the module into interactive lessons and auto-graded practice for TechWise 360.', 6)
) as section_data(lesson_title, section_type, title, body, sort_order)
  on lessons.title = section_data.lesson_title
join public.modules on modules.id = lessons.module_id
where modules.title = 'Hardware Identification'
  and not exists (
    select 1
    from public.lesson_sections existing_section
    where existing_section.lesson_id = lessons.id
      and existing_section.title = section_data.title
  );

insert into public.student_badges (student_id, badge_key, title, color, awarded_by)
select
  profiles.id,
  badge_data.badge_key,
  badge_data.title,
  badge_data.color,
  'd128e2c1-57e2-4037-804a-7f47ba711247'
from public.profiles
join (
  values
    ('Jared Estabillo', 'assembly-ready', 'Assembly Procedure Record', 'gold'),
    ('Jared Estabillo', 'hardware-star', 'Hardware Identification Record', 'blue'),
    ('Denver Enriquez', 'safety-checker', 'Safety Procedure Record', 'green'),
    ('Clyde Edrada', 'cable-scout', 'Cable Identification Record', 'blue'),
    ('Lea Garlejo', 'esd-aware', 'ESD Safety Record', 'purple')
) as badge_data(full_name, badge_key, title, color)
  on lower(profiles.full_name) = lower(badge_data.full_name)
where profiles.role = 'student'
on conflict (student_id, badge_key) do nothing;

insert into public.student_certificates (student_id, certificate_key, title, awarded_by)
select
  profiles.id,
  certificate_data.certificate_key,
  certificate_data.title,
  'd128e2c1-57e2-4037-804a-7f47ba711247'
from public.profiles
join (
  values
    ('Jared Estabillo', 'pc-assembly-complete', 'PC Assembly Completion'),
    ('Denver Enriquez', 'pc-safety-check-complete', 'PC Safety Check Completion'),
    ('Lea Garlejo', 'ports-cables-complete', 'Ports and Cables Completion')
) as certificate_data(full_name, certificate_key, title)
  on lower(profiles.full_name) = lower(certificate_data.full_name)
where profiles.role = 'student'
on conflict (student_id, certificate_key) do nothing;
