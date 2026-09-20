create extension if not exists pgcrypto;

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  role text not null check (role in ('student', 'teacher')),
  status text not null default 'pending' check (status in ('pending', 'approved', 'rejected', 'inactive')),
  username text not null unique,
  email text not null unique,
  full_name text not null,
  first_name text,
  last_name text,
  home_town text,
  grade_level text,
  section text,
  adviser text,
  phone_number text,
  cp_number text,
  student_number text,
  avatar_path text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.profiles add column if not exists first_name text;
alter table public.profiles add column if not exists last_name text;
alter table public.profiles add column if not exists home_town text;
alter table public.profiles add column if not exists grade_level text;
alter table public.profiles add column if not exists section text;
alter table public.profiles add column if not exists adviser text;
alter table public.profiles add column if not exists phone_number text;
alter table public.profiles add column if not exists cp_number text;
alter table public.profiles add column if not exists student_number text;
alter table public.profiles add column if not exists avatar_path text;

create unique index if not exists profiles_student_number_unique
on public.profiles (student_number)
where student_number is not null and student_number <> '';

alter table public.profiles drop constraint if exists profiles_status_check;
alter table public.profiles add constraint profiles_status_check
check (status in ('pending', 'approved', 'rejected', 'inactive'));

update public.profiles
set phone_number = cp_number
where phone_number is null and cp_number is not null;

create table if not exists public.approval_events (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  teacher_id uuid not null references public.profiles(id) on delete cascade,
  action text not null check (action in ('approved', 'rejected')),
  notes text,
  created_at timestamptz not null default now()
);

create table if not exists public.quarters (
  id uuid primary key default gen_random_uuid(),
  name text not null check (name in ('T1', 'T2', 'T3')),
  title text not null,
  school_year text not null,
  start_date date,
  end_date date,
  is_active boolean not null default false,
  status text not null default 'active' check (status in ('active', 'archived')),
  created_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (name, school_year)
);

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

create unique index if not exists quarters_one_active_idx
on public.quarters (is_active)
where is_active;

create table if not exists public.class_sections (
  id uuid primary key default gen_random_uuid(),
  school_year text not null,
  grade_level text not null check (grade_level in ('Grade 9', 'Grade 10')),
  name text not null,
  code text not null,
  adviser_name text not null,
  room text,
  capacity integer not null default 35 check (capacity > 0),
  status text not null default 'draft' check (status in ('draft', 'active', 'archived')),
  created_by uuid references public.profiles(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create unique index if not exists class_sections_year_grade_name_unique
on public.class_sections (school_year, grade_level, lower(name));

create unique index if not exists class_sections_year_code_unique
on public.class_sections (school_year, lower(code));

alter table public.profiles add column if not exists section_id uuid references public.class_sections(id) on delete set null;

create table if not exists public.student_section_assignments (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  section_id uuid not null references public.class_sections(id) on delete restrict,
  assigned_by uuid references public.profiles(id) on delete set null,
  assigned_at timestamptz not null default now(),
  ended_at timestamptz,
  check (ended_at is null or ended_at >= assigned_at)
);

create unique index if not exists student_section_assignments_current_unique
on public.student_section_assignments (student_id)
where ended_at is null;

create table if not exists public.section_activity (
  id uuid primary key default gen_random_uuid(),
  section_id uuid not null references public.class_sections(id) on delete cascade,
  student_id uuid references public.profiles(id) on delete set null,
  actor_id uuid references public.profiles(id) on delete set null,
  action text not null check (action in ('created', 'updated', 'activated', 'drafted', 'archived', 'student_assigned', 'student_transferred')),
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create table if not exists public.modules (
  id uuid primary key default gen_random_uuid(),
  quarter_id uuid references public.quarters(id) on delete set null,
  created_by uuid references public.profiles(id) on delete set null,
  title text not null,
  description text,
  category text not null default 'Computer Basics',
  grade_level text not null check (grade_level in ('Grade 9', 'Grade 10')),
  status text not null default 'draft' check (status in ('draft', 'published', 'archived')),
  sort_order integer not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.lessons (
  id uuid primary key default gen_random_uuid(),
  module_id uuid not null references public.modules(id) on delete cascade,
  quarter_id uuid references public.quarters(id) on delete set null,
  created_by uuid references public.profiles(id) on delete set null,
  title text not null,
  description text,
  lesson_type text not null default 'lesson' check (lesson_type in ('lesson', 'practice', 'assessment')),
  status text not null default 'draft' check (status in ('draft', 'published', 'archived')),
  duration_minutes integer not null default 30 check (duration_minutes between 1 and 600),
  resource_url text,
  sort_order integer not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.lessons add column if not exists scheduled_date date;
alter table public.lessons add column if not exists due_date date;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
  'lesson-files',
  'lesson-files',
  false,
  104857600,
  array[
    'application/pdf',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    'video/mp4'
  ]
)
on conflict (id) do update set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

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

create table if not exists public.lesson_files (
  id uuid primary key default gen_random_uuid(),
  lesson_id uuid references public.lessons(id) on delete set null,
  storage_path text not null unique,
  original_filename text not null,
  mime_type text not null check (mime_type in (
    'application/pdf',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    'video/mp4'
  )),
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

create table if not exists public.lesson_attempts (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  lesson_id uuid not null references public.lessons(id) on delete cascade,
  answers jsonb not null default '{}'::jsonb,
  feedback jsonb not null default '[]'::jsonb,
  score_percent integer not null default 0 check (score_percent between 0 and 100),
  correct_count integer not null default 0 check (correct_count >= 0),
  total_points integer not null default 0 check (total_points >= 0),
  time_spent_seconds integer not null default 0 check (time_spent_seconds >= 0),
  submitted_at timestamptz not null default now()
);

create table if not exists public.lesson_progress (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  lesson_id uuid not null references public.lessons(id) on delete cascade,
  status text not null default 'not_started' check (status in ('not_started', 'in_progress', 'completed')),
  progress_percent integer not null default 0 check (progress_percent between 0 and 100),
  score_percent integer check (score_percent between 0 and 100),
  started_at timestamptz,
  completed_at timestamptz,
  updated_at timestamptz not null default now(),
  unique (student_id, lesson_id)
);

create table if not exists public.student_badges (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  badge_key text not null,
  title text not null,
  color text not null default 'blue' check (color in ('blue', 'green', 'gold', 'purple', 'teal', 'orange', 'red')),
  awarded_by uuid references public.profiles(id) on delete set null,
  awarded_at timestamptz not null default now(),
  unique (student_id, badge_key)
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

create table if not exists public.student_certificates (
  id uuid primary key default gen_random_uuid(),
  student_id uuid not null references public.profiles(id) on delete cascade,
  certificate_key text not null,
  title text not null,
  awarded_by uuid references public.profiles(id) on delete set null,
  awarded_at timestamptz not null default now(),
  unique (student_id, certificate_key)
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

create table if not exists public.teacher_settings (
  teacher_id uuid primary key references public.profiles(id) on delete cascade,
  notification_preferences jsonb not null default '{"student_accounts": true, "lesson_completions": true, "practice_submissions": true, "achievement_awards": true}'::jsonb,
  default_grade text default 'Grade 10' check (default_grade is null or default_grade in ('Grade 9', 'Grade 10')),
  default_landing_view text not null default 'overview' check (default_landing_view in ('overview', 'students', 'sections', 'content', 'assessments', 'reports', 'evaluation')),
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
    check (default_landing_view in ('overview', 'students', 'sections', 'content', 'assessments', 'reports', 'evaluation'));
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

create index if not exists profiles_role_status_idx on public.profiles (role, status);
create index if not exists profiles_grade_status_idx on public.profiles (grade_level, status);
create index if not exists profiles_section_id_idx on public.profiles (section_id);
create index if not exists approval_events_student_id_idx on public.approval_events (student_id);
create index if not exists approval_events_teacher_id_idx on public.approval_events (teacher_id);
create index if not exists quarters_active_idx on public.quarters (is_active);
create index if not exists class_sections_year_status_idx on public.class_sections (school_year, status, grade_level);
create index if not exists student_section_assignments_section_current_idx on public.student_section_assignments (section_id, ended_at);
create index if not exists student_section_assignments_student_history_idx on public.student_section_assignments (student_id, assigned_at desc);
create index if not exists section_activity_section_recent_idx on public.section_activity (section_id, created_at desc);
create index if not exists section_activity_recent_idx on public.section_activity (created_at desc);
create index if not exists modules_quarter_grade_idx on public.modules (quarter_id, grade_level, status);
create index if not exists lessons_module_status_idx on public.lessons (module_id, status);
create index if not exists lessons_schedule_idx on public.lessons (scheduled_date, due_date);
create index if not exists lesson_files_lesson_idx on public.lesson_files (lesson_id);
create index if not exists lesson_files_recent_idx on public.lesson_files (created_at desc);
create index if not exists lesson_sections_lesson_idx on public.lesson_sections (lesson_id, sort_order);
create index if not exists lesson_questions_lesson_idx on public.lesson_questions (lesson_id, sort_order);
create index if not exists lesson_attempts_student_lesson_idx on public.lesson_attempts (student_id, lesson_id, submitted_at desc);
create index if not exists lesson_progress_student_idx on public.lesson_progress (student_id);
create index if not exists lesson_progress_lesson_idx on public.lesson_progress (lesson_id);
create index if not exists student_badges_student_idx on public.student_badges (student_id);
create index if not exists student_badges_badge_id_idx on public.student_badges (badge_id);
create index if not exists student_certificates_student_idx on public.student_certificates (student_id);
create index if not exists vr_competitions_status_idx on public.vr_competitions (status, grade_level, section);
create index if not exists vr_competitions_quarter_idx on public.vr_competitions (quarter_id);
create index if not exists vr_simulation_attempts_student_idx on public.vr_simulation_attempts (student_id, completed_at desc);
create index if not exists vr_simulation_attempts_competition_idx on public.vr_simulation_attempts (competition_id, score_percent desc, duration_seconds asc, mistakes asc);
create index if not exists badge_definitions_status_idx on public.badge_definitions (status, category);
create index if not exists teacher_notifications_teacher_recent_idx on public.teacher_notifications (teacher_id, created_at desc);
create index if not exists teacher_notifications_teacher_unread_idx on public.teacher_notifications (teacher_id, read_at) where read_at is null;
create index if not exists student_notifications_student_recent_idx on public.student_notifications (student_id, created_at desc);
create index if not exists student_notifications_student_unread_idx on public.student_notifications (student_id, read_at) where read_at is null;
create index if not exists teacher_settings_defaults_idx on public.teacher_settings (default_grade, default_landing_view);
create index if not exists evaluation_cycles_quarter_grade_idx on public.evaluation_cycles (quarter_id, grade_level);
create index if not exists evaluation_survey_responses_cycle_idx on public.evaluation_survey_responses (cycle_id, submitted_at desc);
create index if not exists evaluation_survey_responses_respondent_idx on public.evaluation_survey_responses (respondent_id);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists profiles_set_updated_at on public.profiles;
create trigger profiles_set_updated_at
before update on public.profiles
for each row
execute function public.set_updated_at();

drop trigger if exists quarters_set_updated_at on public.quarters;
create trigger quarters_set_updated_at
before update on public.quarters
for each row
execute function public.set_updated_at();

drop trigger if exists class_sections_set_updated_at on public.class_sections;
create trigger class_sections_set_updated_at
before update on public.class_sections
for each row
execute function public.set_updated_at();

drop trigger if exists modules_set_updated_at on public.modules;
create trigger modules_set_updated_at
before update on public.modules
for each row
execute function public.set_updated_at();

drop trigger if exists lessons_set_updated_at on public.lessons;
create trigger lessons_set_updated_at
before update on public.lessons
for each row
execute function public.set_updated_at();

drop trigger if exists lesson_sections_set_updated_at on public.lesson_sections;
create trigger lesson_sections_set_updated_at
before update on public.lesson_sections
for each row
execute function public.set_updated_at();

drop trigger if exists lesson_questions_set_updated_at on public.lesson_questions;
create trigger lesson_questions_set_updated_at
before update on public.lesson_questions
for each row
execute function public.set_updated_at();

drop trigger if exists lesson_progress_set_updated_at on public.lesson_progress;
create trigger lesson_progress_set_updated_at
before update on public.lesson_progress
for each row
execute function public.set_updated_at();

drop trigger if exists teacher_settings_set_updated_at on public.teacher_settings;
create trigger teacher_settings_set_updated_at
before update on public.teacher_settings
for each row
execute function public.set_updated_at();

drop trigger if exists evaluation_cycles_set_updated_at on public.evaluation_cycles;
create trigger evaluation_cycles_set_updated_at
before update on public.evaluation_cycles
for each row
execute function public.set_updated_at();

drop trigger if exists evaluation_survey_responses_set_updated_at on public.evaluation_survey_responses;
create trigger evaluation_survey_responses_set_updated_at
before update on public.evaluation_survey_responses
for each row
execute function public.set_updated_at();

alter table public.profiles enable row level security;
alter table public.approval_events enable row level security;
alter table public.quarters enable row level security;
alter table public.class_sections enable row level security;
alter table public.student_section_assignments enable row level security;
alter table public.section_activity enable row level security;
alter table public.modules enable row level security;
alter table public.lessons enable row level security;
alter table public.lesson_files enable row level security;
alter table public.lesson_sections enable row level security;
alter table public.lesson_questions enable row level security;
alter table public.lesson_attempts enable row level security;
alter table public.lesson_progress enable row level security;
alter table public.student_badges enable row level security;
alter table public.student_certificates enable row level security;
alter table public.vr_competitions enable row level security;
alter table public.vr_simulation_attempts enable row level security;
alter table public.badge_definitions enable row level security;
alter table public.teacher_notifications enable row level security;
alter table public.student_notifications enable row level security;
alter table public.teacher_settings enable row level security;
alter table public.evaluation_cycles enable row level security;
alter table public.evaluation_survey_responses enable row level security;

drop policy if exists "Users can read their own profile" on public.profiles;
create policy "Users can read their own profile"
on public.profiles
for select
to authenticated
using (auth.uid() = id);

drop policy if exists "Users can read their own approval events" on public.approval_events;
create policy "Users can read their own approval events"
on public.approval_events
for select
to authenticated
using (auth.uid() = student_id or auth.uid() = teacher_id);

drop policy if exists "Authenticated users can read quarters" on public.quarters;
drop policy if exists "Authenticated users can read terms" on public.quarters;
create policy "Authenticated users can read terms"
on public.quarters
for select
to authenticated
using (true);

drop policy if exists "Teachers can manage class sections" on public.class_sections;
create policy "Teachers can manage class sections"
on public.class_sections
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

drop policy if exists "Students can read their active class section" on public.class_sections;
create policy "Students can read their active class section"
on public.class_sections
for select
to authenticated
using (
  status = 'active'
  and exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'student'
      and profiles.section_id = class_sections.id
  )
);

drop policy if exists "Teachers can manage section assignments" on public.student_section_assignments;
create policy "Teachers can manage section assignments"
on public.student_section_assignments
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

drop policy if exists "Students can read their section assignment history" on public.student_section_assignments;
create policy "Students can read their section assignment history"
on public.student_section_assignments
for select
to authenticated
using (auth.uid() = student_id);

drop policy if exists "Teachers can read section activity" on public.section_activity;
drop policy if exists "Teachers can manage section activity" on public.section_activity;
create policy "Teachers can manage section activity"
on public.section_activity
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

drop policy if exists "Authenticated users can read published modules" on public.modules;
create policy "Authenticated users can read published modules"
on public.modules
for select
to authenticated
using (
  status = 'published'
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Authenticated users can read published lessons" on public.lessons;
create policy "Authenticated users can read published lessons"
on public.lessons
for select
to authenticated
using (
  status = 'published'
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Authenticated users can read lesson files" on public.lesson_files;
create policy "Authenticated users can read lesson files"
on public.lesson_files
for select
to authenticated
using (
  exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
  or exists (
    select 1
    from public.lessons
    join public.modules on modules.id = lessons.module_id
    join public.profiles on profiles.id = auth.uid()
    where lessons.id = lesson_files.lesson_id
      and lessons.status = 'published'
      and modules.status = 'published'
      and modules.grade_level = profiles.grade_level
      and profiles.role = 'student'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Authenticated users can read lesson sections" on public.lesson_sections;
create policy "Authenticated users can read lesson sections"
on public.lesson_sections
for select
to authenticated
using (
  exists (
    select 1
    from public.lessons
    where lessons.id = lesson_sections.lesson_id
      and (
        lessons.status = 'published'
        or exists (
          select 1 from public.profiles
          where profiles.id = auth.uid()
            and profiles.role = 'teacher'
            and profiles.status = 'approved'
        )
      )
  )
);

drop policy if exists "Authenticated users can read lesson questions" on public.lesson_questions;
create policy "Authenticated users can read lesson questions"
on public.lesson_questions
for select
to authenticated
using (
  exists (
    select 1
    from public.lessons
    where lessons.id = lesson_questions.lesson_id
      and (
        lessons.status = 'published'
        or exists (
          select 1 from public.profiles
          where profiles.id = auth.uid()
            and profiles.role = 'teacher'
            and profiles.status = 'approved'
        )
      )
  )
);

drop policy if exists "Students can read their own lesson attempts" on public.lesson_attempts;
create policy "Students can read their own lesson attempts"
on public.lesson_attempts
for select
to authenticated
using (
  auth.uid() = student_id
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read their own lesson progress" on public.lesson_progress;
create policy "Students can read their own lesson progress"
on public.lesson_progress
for select
to authenticated
using (
  auth.uid() = student_id
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

drop policy if exists "Students can read their own badges" on public.student_badges;
create policy "Students can read their own badges"
on public.student_badges
for select
to authenticated
using (
  auth.uid() = student_id
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
  )
);

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

drop policy if exists "Students can read their own certificates" on public.student_certificates;
create policy "Students can read their own certificates"
on public.student_certificates
for select
to authenticated
using (
  auth.uid() = student_id
  or exists (
    select 1 from public.profiles
    where profiles.id = auth.uid()
      and profiles.role = 'teacher'
      and profiles.status = 'approved'
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

insert into public.quarters (name, title, school_year, is_active)
values
  ('T1', '1st Term', '2026-2027', false),
  ('T2', '2nd Term', '2026-2027', false),
  ('T3', '3rd Term', '2026-2027', false)
on conflict (name, school_year) do nothing;

update public.quarters
set is_active = true
where name = 'T1'
  and school_year = '2026-2027'
  and not exists (
    select 1 from public.quarters active_quarter
    where active_quarter.is_active = true
  );
