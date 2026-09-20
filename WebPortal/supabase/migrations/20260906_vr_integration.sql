-- Additive Batch 3 migration. Apply after the existing section/term migrations.
begin;
create table if not exists public.academic_years (
  id uuid primary key default gen_random_uuid(), name text not null unique,
  created_at timestamptz not null default now()
);
insert into public.academic_years(name) select distinct school_year from public.quarters on conflict do nothing;
alter table public.quarters add column if not exists academic_year_id uuid references public.academic_years(id) on delete restrict;
update public.quarters q set academic_year_id = y.id from public.academic_years y where q.school_year = y.name and q.academic_year_id is null;
create or replace function public.link_quarter_academic_year() returns trigger language plpgsql set search_path = public as $$
begin
  insert into academic_years(name) values(new.school_year) on conflict do nothing;
  select id into new.academic_year_id from academic_years where name = new.school_year;
  return new;
end $$;
drop trigger if exists quarter_academic_year on public.quarters;
create trigger quarter_academic_year before insert or update of school_year on public.quarters
  for each row execute function public.link_quarter_academic_year();

create table if not exists public.student_enrollments (
  id uuid primary key default gen_random_uuid(), student_id uuid not null references public.profiles(id) on delete restrict,
  academic_year_id uuid not null references public.academic_years(id) on delete restrict,
  quarter_id uuid not null references public.quarters(id) on delete restrict,
  section_id uuid not null references public.class_sections(id) on delete restrict,
  grade_level text not null, created_at timestamptz not null default now(),
  unique(student_id, quarter_id, section_id)
);
create table if not exists public.vr_launch_codes (
  code_hash text primary key, student_id uuid not null references public.profiles(id) on delete cascade,
  created_at timestamptz not null default now(), expires_at timestamptz not null, consumed_at timestamptz
);
create table if not exists public.vr_device_sessions (
  token_hash text primary key, student_id uuid not null references public.profiles(id) on delete cascade,
  expires_at timestamptz not null, created_at timestamptz not null default now()
);
create table if not exists public.vr_assessment_sessions (
  id uuid primary key, student_id uuid not null references public.profiles(id) on delete restrict,
  competition_id uuid references public.vr_competitions(id) on delete restrict,
  simulation_type text not null check(simulation_type in ('assembly','disassembly')),
  academic_year_id uuid not null references public.academic_years(id) on delete restrict,
  quarter_id uuid not null references public.quarters(id) on delete restrict,
  enrollment_id uuid not null references public.student_enrollments(id) on delete restrict,
  section_id uuid not null references public.class_sections(id) on delete restrict,
  grade_level text not null, section_name text not null, student_name text not null,
  started_at timestamptz not null default now(), status text not null default 'in_progress' check(status in ('in_progress','completed')),
  scoring_configuration jsonb not null
);
alter table public.vr_simulation_attempts add column if not exists assessment_session_id uuid references public.vr_assessment_sessions(id) on delete restrict;
alter table public.vr_simulation_attempts add column if not exists academic_year_id uuid references public.academic_years(id) on delete restrict;
alter table public.vr_simulation_attempts add column if not exists quarter_id uuid references public.quarters(id) on delete restrict;
alter table public.vr_simulation_attempts add column if not exists enrollment_id uuid references public.student_enrollments(id) on delete restrict;
alter table public.vr_simulation_attempts add column if not exists section_id uuid references public.class_sections(id) on delete restrict;
alter table public.vr_simulation_attempts add column if not exists grade_level text;
alter table public.vr_simulation_attempts add column if not exists section_name text;
alter table public.vr_simulation_attempts add column if not exists student_name text;
alter table public.vr_simulation_attempts add column if not exists accuracy_percent numeric(5,2);
alter table public.vr_simulation_attempts add column if not exists received_at timestamptz;
create unique index if not exists vr_one_result_per_session on public.vr_simulation_attempts(assessment_session_id);
create index if not exists vr_results_term_section on public.vr_simulation_attempts(academic_year_id,quarter_id,section_id,completed_at);

create or replace function public.issue_vr_code(p_student uuid, p_hash text) returns void
language plpgsql security definer set search_path = public as $$
begin
  perform 1 from profiles where id=p_student and role='student' and status='approved' for update;
  if not found then raise exception 'VR_STUDENT_REQUIRED'; end if;
  if (select count(*) from vr_launch_codes where student_id=p_student and created_at>now()-interval '5 minutes') >= 5 then
    raise exception 'VR_RATE_LIMIT';
  end if;
  delete from vr_launch_codes where expires_at < now()-interval '1 day';
  insert into vr_launch_codes(code_hash,student_id,expires_at) values(p_hash,p_student,now()+interval '2 minutes');
end $$;

create or replace function public.exchange_vr_code(p_hash text,p_token_hash text) returns uuid
language plpgsql security definer set search_path = public as $$
declare v_student uuid;
begin
  update vr_launch_codes set consumed_at=now() where code_hash=p_hash and consumed_at is null and expires_at>now()
    returning student_id into v_student;
  if v_student is null then raise exception 'VR_CODE_EXPIRED'; end if;
  perform 1 from profiles where id=v_student and role='student' and status='approved';
  if not found then raise exception 'VR_STUDENT_REQUIRED'; end if;
  delete from vr_device_sessions where expires_at<now();
  insert into vr_device_sessions(token_hash,student_id,expires_at) values(p_token_hash,v_student,now()+interval '8 hours');
  return v_student;
end $$;

create or replace function public.start_vr_assessment(p_student uuid,p_id uuid,p_simulation text,p_competition uuid,p_scoring jsonb)
returns public.vr_assessment_sessions language plpgsql security definer set search_path = public as $$
declare v_profile profiles; v_quarter quarters; v_section class_sections; v_comp vr_competitions;
  v_existing vr_assessment_sessions; v_enrollment uuid;
begin
  -- Serialize per-student start requests, including attempt-limit enforcement.
  select * into v_profile from profiles where id=p_student and role='student' and status='approved' for update;
  if not found then raise exception 'VR_STUDENT_REQUIRED'; end if;
  select * into v_existing from vr_assessment_sessions where id=p_id;
  if found then
    if v_existing.student_id<>p_student or v_existing.simulation_type<>p_simulation or v_existing.competition_id is distinct from p_competition then
      raise exception 'VR_ATTEMPT_CONFLICT';
    end if;
    return v_existing;
  end if;
  if p_simulation not in ('assembly','disassembly') then raise exception 'VR_INVALID_RESULT'; end if;
  select * into v_quarter from quarters where is_active and status='active';
  select * into v_section from class_sections where id=v_profile.section_id and status='active';
  if v_quarter.id is null or v_section.id is null or v_section.school_year<>v_quarter.school_year or v_section.grade_level<>v_profile.grade_level then
    raise exception 'VR_CONTEXT_REQUIRED';
  end if;
  if p_competition is not null then
    select * into v_comp from vr_competitions where id=p_competition;
    if v_comp.id is null or v_comp.status<>'active' or (v_comp.start_at is not null and now()<v_comp.start_at)
      or (v_comp.end_at is not null and now()>v_comp.end_at)
      or (v_comp.quarter_id is not null and v_comp.quarter_id<>v_quarter.id)
      or (nullif(v_comp.grade_level,'') is not null and v_comp.grade_level<>v_profile.grade_level)
      or (nullif(v_comp.section,'') is not null and v_comp.section<>v_section.name)
      or (v_comp.simulation_type<>'both' and v_comp.simulation_type<>p_simulation) then raise exception 'VR_COMPETITION_UNAVAILABLE'; end if;
    if v_comp.attempts_allowed is not null and (
      (select count(*) from vr_assessment_sessions where student_id=p_student and competition_id=p_competition) +
      (select count(*) from vr_simulation_attempts where student_id=p_student and competition_id=p_competition and assessment_session_id is null)
    )>=v_comp.attempts_allowed then
      raise exception 'VR_ATTEMPT_LIMIT';
    end if;
  end if;
  insert into student_enrollments(student_id,academic_year_id,quarter_id,section_id,grade_level)
    values(p_student,v_quarter.academic_year_id,v_quarter.id,v_section.id,v_profile.grade_level) on conflict do nothing;
  select id into v_enrollment from student_enrollments where student_id=p_student and quarter_id=v_quarter.id and section_id=v_section.id;
  insert into vr_assessment_sessions(id,student_id,competition_id,simulation_type,academic_year_id,quarter_id,enrollment_id,section_id,grade_level,section_name,student_name,scoring_configuration)
    values(p_id,p_student,p_competition,p_simulation,v_quarter.academic_year_id,v_quarter.id,v_enrollment,v_section.id,v_profile.grade_level,v_section.name,v_profile.full_name,p_scoring)
    returning * into v_existing;
  return v_existing;
end $$;

create or replace function public.complete_vr_assessment(p_student uuid,p_id uuid,p_result jsonb)
returns public.vr_simulation_attempts language plpgsql security definer set search_path = public as $$
declare v_session vr_assessment_sessions; v_result vr_simulation_attempts;
begin
  select * into v_session from vr_assessment_sessions where id=p_id for update;
  if not found or v_session.student_id<>p_student then raise exception 'VR_ATTEMPT_NOT_FOUND'; end if;
  -- Lock + unique constraint make concurrent retries return the original immutable result.
  select * into v_result from vr_simulation_attempts where assessment_session_id=p_id;
  if found then return v_result; end if;
  insert into vr_simulation_attempts(assessment_session_id,student_id,competition_id,simulation_type,academic_year_id,quarter_id,enrollment_id,section_id,
    grade_level,section_name,student_name,score_percent,accuracy_percent,duration_seconds,mistakes,status,metadata,started_at,completed_at,received_at)
  values(p_id,p_student,v_session.competition_id,v_session.simulation_type,v_session.academic_year_id,v_session.quarter_id,v_session.enrollment_id,v_session.section_id,
    v_session.grade_level,v_session.section_name,v_session.student_name,(p_result->>'score_percent')::numeric,(p_result->>'accuracy_percent')::numeric,
    (p_result->>'duration_seconds')::integer,(p_result->>'mistakes')::integer,'completed',p_result->'metadata',v_session.started_at,(p_result->>'completed_at')::timestamptz,now())
    returning * into v_result;
  update vr_assessment_sessions set status='completed' where id=p_id;
  return v_result;
end $$;

-- All new state is accessed through the authenticated server API. No browser/Unity DB writes.
alter table public.academic_years enable row level security;
alter table public.student_enrollments enable row level security;
alter table public.vr_launch_codes enable row level security;
alter table public.vr_device_sessions enable row level security;
alter table public.vr_assessment_sessions enable row level security;
revoke all on public.academic_years,public.student_enrollments,public.vr_launch_codes,public.vr_device_sessions,public.vr_assessment_sessions from anon,authenticated;
grant all on public.academic_years,public.student_enrollments,public.vr_launch_codes,public.vr_device_sessions,public.vr_assessment_sessions to service_role;
revoke insert,update,delete on public.vr_simulation_attempts from anon,authenticated;
revoke all on function public.issue_vr_code(uuid,text),public.exchange_vr_code(text,text),public.start_vr_assessment(uuid,uuid,text,uuid,jsonb),public.complete_vr_assessment(uuid,uuid,jsonb) from public,anon,authenticated;
grant execute on function public.issue_vr_code(uuid,text),public.exchange_vr_code(text,text),public.start_vr_assessment(uuid,uuid,text,uuid,jsonb),public.complete_vr_assessment(uuid,uuid,jsonb) to service_role;
commit;
