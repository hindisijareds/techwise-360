-- TechWise 360 managed class sections migration
-- Safe to run more than once against an existing project.

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

create index if not exists profiles_section_id_idx on public.profiles (section_id);
create index if not exists class_sections_year_status_idx on public.class_sections (school_year, status, grade_level);
create index if not exists student_section_assignments_section_current_idx on public.student_section_assignments (section_id, ended_at);
create index if not exists student_section_assignments_student_history_idx on public.student_section_assignments (student_id, assigned_at desc);
create index if not exists section_activity_section_recent_idx on public.section_activity (section_id, created_at desc);
create index if not exists section_activity_recent_idx on public.section_activity (created_at desc);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists class_sections_set_updated_at on public.class_sections;
create trigger class_sections_set_updated_at
before update on public.class_sections
for each row execute function public.set_updated_at();

do $$
begin
  alter table public.teacher_settings drop constraint if exists teacher_settings_default_landing_view_check;
  alter table public.teacher_settings
    add constraint teacher_settings_default_landing_view_check
    check (default_landing_view in ('overview', 'students', 'sections', 'content', 'assessments', 'reports', 'evaluation'));
end $$;

insert into public.class_sections (school_year, grade_level, name, code, adviser_name, capacity, status, created_by)
select section_data.school_year, section_data.grade_level, section_data.name, section_data.code,
  section_data.adviser_name, 35, 'active', 'd128e2c1-57e2-4037-804a-7f47ba711247'
from (
  values
    ('2026-2027', 'Grade 9', 'Ylang Ylang', 'G9-YLANG', 'Carla Mar Locquiao'),
    ('2026-2027', 'Grade 9', 'Dama De Noche', 'G9-DAMA', 'Lara Santos'),
    ('2026-2027', 'Grade 9', 'Sampaguita', 'G9-SAMPAGUITA', 'Joel Jacob'),
    ('2026-2027', 'Grade 10', 'Rosal', 'G10-ROSAL', 'Salvador Reasonda Jr.'),
    ('2026-2027', 'Grade 10', 'Lavender', 'G10-LAVENDER', 'Noella Krista Valdez'),
    ('2026-2027', 'Grade 10', 'Tulip', 'G10-TULIP', 'Richie Unlayao')
) as section_data(school_year, grade_level, name, code, adviser_name)
where not exists (
  select 1 from public.class_sections existing
  where existing.school_year = section_data.school_year
    and existing.grade_level = section_data.grade_level
    and lower(existing.name) = lower(section_data.name)
)
and not exists (
  select 1 from public.class_sections existing
  where existing.school_year = section_data.school_year
    and lower(existing.code) = lower(section_data.code)
);

update public.profiles profiles
set section_id = class_sections.id,
  section = class_sections.name,
  adviser = class_sections.adviser_name,
  updated_at = now()
from public.class_sections
where profiles.role = 'student'
  and profiles.grade_level = class_sections.grade_level
  and lower(coalesce(profiles.section, '')) = lower(class_sections.name)
  and class_sections.school_year = '2026-2027'
  and profiles.section_id is distinct from class_sections.id;

insert into public.student_section_assignments (student_id, section_id, assigned_by, assigned_at)
select profiles.id, profiles.section_id, 'd128e2c1-57e2-4037-804a-7f47ba711247', coalesce(profiles.created_at, now())
from public.profiles
where profiles.role = 'student'
  and profiles.section_id is not null
  and not exists (
    select 1 from public.student_section_assignments current_assignment
    where current_assignment.student_id = profiles.id and current_assignment.ended_at is null
  );

insert into public.section_activity (section_id, actor_id, action, details, created_at)
select class_sections.id, class_sections.created_by, 'created', jsonb_build_object('source', 'initial_import'), class_sections.created_at
from public.class_sections
where class_sections.school_year = '2026-2027'
  and not exists (
    select 1 from public.section_activity activity
    where activity.section_id = class_sections.id and activity.action = 'created'
  );

alter table public.class_sections enable row level security;
alter table public.student_section_assignments enable row level security;
alter table public.section_activity enable row level security;

drop policy if exists "Teachers can manage class sections" on public.class_sections;
create policy "Teachers can manage class sections" on public.class_sections for all to authenticated
using (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'))
with check (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'));

drop policy if exists "Students can read their active class section" on public.class_sections;
create policy "Students can read their active class section" on public.class_sections for select to authenticated
using (status = 'active' and exists (
  select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'student' and profiles.section_id = class_sections.id
));

drop policy if exists "Teachers can manage section assignments" on public.student_section_assignments;
create policy "Teachers can manage section assignments" on public.student_section_assignments for all to authenticated
using (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'))
with check (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'));

drop policy if exists "Students can read their section assignment history" on public.student_section_assignments;
create policy "Students can read their section assignment history" on public.student_section_assignments for select to authenticated
using (auth.uid() = student_id);

drop policy if exists "Teachers can manage section activity" on public.section_activity;
create policy "Teachers can manage section activity" on public.section_activity for all to authenticated
using (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'))
with check (exists (select 1 from public.profiles where profiles.id = auth.uid() and profiles.role = 'teacher' and profiles.status = 'approved'));

notify pgrst, 'reload schema';
