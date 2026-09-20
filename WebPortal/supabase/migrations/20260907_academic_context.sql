-- Batch 4. Apply after 20260906_vr_integration.sql. No destructive reset.
begin;
insert into academic_years(name) select distinct school_year from class_sections s where not exists(select 1 from academic_years y where y.name=s.school_year) on conflict do nothing;
alter table academic_years add column if not exists start_year integer;
alter table academic_years add column if not exists end_year integer;
alter table academic_years add column if not exists start_date date;
alter table academic_years add column if not exists end_date date;
alter table academic_years add column if not exists status text not null default 'inactive';
alter table academic_years add column if not exists updated_at timestamptz not null default now();
update academic_years set start_year=split_part(name,'-',1)::integer,end_year=split_part(name,'-',2)::integer
where start_year is null and name ~ '^\d{4}-\d{4}$';
-- Preserve irregular legacy labels; require valid ranges on future writes.
create unique index if not exists one_active_academic_year on academic_years((status)) where status='active';
update academic_years set status='active' where id in(select academic_year_id from quarters where is_active)
  and not exists(select 1 from academic_years where status='active');

create or replace function validate_academic_year() returns trigger language plpgsql set search_path=public as $$
begin
  if tg_op='UPDATE' and (new.name<>old.name or new.start_year is distinct from old.start_year or new.end_year is distinct from old.end_year) then
    raise exception 'Academic year identity cannot be changed. Create a new academic year instead.';
  end if;
  if new.start_year is null or new.start_year<2000 or new.start_year>2199 or new.end_year<>new.start_year+1 or new.end_year is null
    or new.name<>new.start_year::text||'-'||new.end_year::text then raise exception 'Use consecutive academic years, for example 2027-2028.'; end if;
  if new.status not in ('active','inactive') then raise exception 'Invalid academic year status.'; end if;
  if (new.start_date is not null and extract(year from new.start_date) not between new.start_year and new.end_year)
    or (new.end_date is not null and extract(year from new.end_date) not between new.start_year and new.end_year)
    or new.start_date>new.end_date then raise exception 'Academic year dates must be ordered and within the academic year.'; end if;
  new.updated_at=now(); return new;
end $$;
drop trigger if exists academic_year_validation on academic_years;
create trigger academic_year_validation before insert or update on academic_years for each row execute function validate_academic_year();

create or replace function link_quarter_academic_year() returns trigger language plpgsql set search_path=public as $$
declare y academic_years;
begin
  if tg_op='UPDATE' and (new.school_year<>old.school_year or new.name<>old.name or new.academic_year_id is distinct from old.academic_year_id) then
    raise exception 'A term cannot be moved to another academic year or term number. Create a new term.'; end if;
  select * into y from academic_years where name=new.school_year;
  if not found then raise exception 'Create the academic year before creating its terms.'; end if;
  new.academic_year_id=y.id;
  if new.is_active and (y.status<>'active' or new.status<>'active') then raise exception 'Activate this academic year before selecting its active term.'; end if;
  if new.start_date>new.end_date or (new.start_date is not null and extract(year from new.start_date) not between y.start_year and y.end_year)
    or (new.end_date is not null and extract(year from new.end_date) not between y.start_year and y.end_year)
    or new.start_date<y.start_date or new.end_date>y.end_date then raise exception 'Term dates must be ordered and within its academic year dates.'; end if;
  return new;
end $$;
drop trigger if exists quarter_academic_year on quarters;
create trigger quarter_academic_year before insert or update on quarters for each row execute function link_quarter_academic_year();

alter table class_sections add column if not exists academic_year_id uuid references academic_years(id) on delete restrict;
update class_sections s set academic_year_id=y.id from academic_years y where s.school_year=y.name and s.academic_year_id is null;
alter table student_enrollments add column if not exists ended_at timestamptz;
alter table student_enrollments add column if not exists student_name text;
alter table student_enrollments add column if not exists section_name text;
update student_enrollments e set section_name=s.name from class_sections s where e.section_id=s.id and e.section_name is null;
with ranked as (select id,row_number() over(partition by student_id,quarter_id order by created_at desc,id desc) n from student_enrollments where ended_at is null)
update student_enrollments set ended_at=now() where id in(select id from ranked where n>1);
create unique index if not exists one_current_enrollment_per_term on student_enrollments(student_id,quarter_id) where ended_at is null;

create or replace function validate_enrollment_context() returns trigger language plpgsql set search_path=public as $$
declare q quarters; s class_sections; p profiles;
begin
  if tg_op='UPDATE' then
    if (new.student_id,new.academic_year_id,new.quarter_id,new.section_id,new.grade_level) is distinct from
       (old.student_id,old.academic_year_id,old.quarter_id,old.section_id,old.grade_level) then
      raise exception 'Enrollment identity is historical. Add a new enrollment instead.'; end if;
    return new;
  end if;
  select * into q from quarters where id=new.quarter_id;
  select * into s from class_sections where id=new.section_id;
  select * into p from profiles where id=new.student_id;
  if p.id is null or p.role<>'student' or p.status not in ('approved','pending') then raise exception 'Select an approved or pending student account.'; end if;
  if q.id is null or q.status<>'active' or q.academic_year_id<>new.academic_year_id or s.id is null or s.status<>'active'
    or s.academic_year_id<>new.academic_year_id or s.grade_level<>new.grade_level then raise exception 'Student enrollment requires a matching year, term, grade and active section.'; end if;
  new.student_name=p.full_name; new.section_name=s.name; return new;
end $$;
drop trigger if exists enrollment_context_validation on student_enrollments;
create trigger enrollment_context_validation before insert or update on student_enrollments for each row execute function validate_enrollment_context();
-- Only backfill known current context; do not guess historical enrollments from today's profile.
insert into student_enrollments(student_id,academic_year_id,quarter_id,section_id,grade_level)
select p.id,q.academic_year_id,q.id,s.id,s.grade_level from profiles p join class_sections s on s.id=p.section_id
join quarters q on q.school_year=s.school_year and q.is_active and q.status='active'
where p.role='student' and p.status in ('pending','approved') and s.status='active' and p.grade_level=s.grade_level
and not exists(select 1 from student_enrollments e where e.student_id=p.id and e.quarter_id=q.id and e.ended_at is null)
on conflict do nothing;

create or replace function save_academic_year(p_body jsonb) returns academic_years language plpgsql security definer set search_path=public as $$
declare y academic_years; n integer;
begin
  perform pg_advisory_xact_lock(640360);
  if p_body->>'id' is null then
    n=(p_body->>'start_year')::integer;
    insert into academic_years(name,start_year,end_year,start_date,end_date)
      values(n::text||'-'||(n+1)::text,n,n+1,nullif(p_body->>'start_date','')::date,nullif(p_body->>'end_date','')::date) returning * into y;
  else
    select * into y from academic_years where id=(p_body->>'id')::uuid for update;
    if not found then raise exception 'Academic year not found.'; end if;
  end if;
  if p_body->>'action'='activate' then
    update quarters set is_active=false where is_active;
    update academic_years set status='inactive' where status='active';
    update academic_years set status='active' where id=y.id returning * into y;
  elsif p_body->>'action'='deactivate' then
    update quarters set is_active=false where academic_year_id=y.id and is_active;
    update academic_years set status='inactive' where id=y.id returning * into y;
  end if;
  return y;
end $$;

create or replace function save_academic_term(p_body jsonb,p_teacher uuid) returns quarters language plpgsql security definer set search_path=public as $$
declare q quarters; v_active boolean;
begin
  perform pg_advisory_xact_lock(640360);
  v_active=coalesce((p_body->>'is_active')::boolean,false);
  if v_active then update quarters set is_active=false where is_active; end if;
  if nullif(p_body->>'id','') is null then
    insert into quarters(name,title,school_year,start_date,end_date,is_active,status,created_by)
      values(p_body->>'name',p_body->>'title',p_body->>'school_year',nullif(p_body->>'start_date','')::date,nullif(p_body->>'end_date','')::date,v_active,'active',p_teacher) returning * into q;
  else
    select * into q from quarters where id=(p_body->>'id')::uuid for update;
    if not found then raise exception 'Term not found.'; end if;
    update quarters set name=coalesce(p_body->>'name',name),title=coalesce(p_body->>'title',title),school_year=coalesce(p_body->>'school_year',school_year),
      start_date=case when p_body?'start_date' then nullif(p_body->>'start_date','')::date else start_date end,
      end_date=case when p_body?'end_date' then nullif(p_body->>'end_date','')::date else end_date end,
      status=case when v_active then 'active' else coalesce(p_body->>'status',status) end,
      is_active=case when p_body->>'status'='archived' then false when p_body?'is_active' then v_active else is_active end
      where id=q.id returning * into q;
  end if;
  if q.is_active then
    update profiles p set section_id=s.id,section=s.name,grade_level=e.grade_level,adviser=s.adviser_name
    from student_enrollments e join class_sections s on s.id=e.section_id where e.student_id=p.id and e.quarter_id=q.id and e.ended_at is null;
  end if;
  return q;
end $$;

create or replace function enroll_student(p_student uuid,p_year uuid,p_term uuid,p_section uuid,p_grade text,p_teacher uuid default null)
returns student_enrollments language plpgsql security definer set search_path=public as $$
declare e student_enrollments; q quarters; s class_sections;
begin
  perform pg_advisory_xact_lock(640360);
  perform 1 from profiles where id=p_student and role='student' for update;
  if not found then raise exception 'Student account not found.'; end if;
  select * into q from quarters where id=p_term;
  select * into s from class_sections where id=p_section;
  if q.id is null or s.id is null or q.academic_year_id<>p_year or s.academic_year_id<>p_year or s.grade_level<>p_grade or s.status<>'active' or q.status<>'active' then
    raise exception 'Choose a matching academic year, term, grade and active section.'; end if;
  select * into e from student_enrollments where student_id=p_student and quarter_id=p_term and ended_at is null;
  if found then
    if e.section_id=p_section then return e; end if;
    update student_enrollments set ended_at=now() where id=e.id;
  end if;
  insert into student_enrollments(student_id,academic_year_id,quarter_id,section_id,grade_level)
    values(p_student,p_year,p_term,p_section,p_grade) returning * into e;
  if q.is_active then
    update student_section_assignments set ended_at=now() where student_id=p_student and ended_at is null and section_id<>p_section;
    insert into student_section_assignments(student_id,section_id,assigned_by)
      select p_student,p_section,p_teacher where not exists(select 1 from student_section_assignments where student_id=p_student and ended_at is null);
    update profiles set section_id=s.id,section=s.name,grade_level=s.grade_level,adviser=s.adviser_name where id=p_student;
  end if;
  return e;
end $$;

create or replace function protect_academic_history() returns trigger language plpgsql set search_path=public as $$
begin
  raise exception 'Historical academic records cannot be deleted. Archive or deactivate them instead.';
end $$;
drop trigger if exists preserve_year on academic_years;
create trigger preserve_year before delete on academic_years for each row execute function protect_academic_history();
drop trigger if exists preserve_term on quarters;
create trigger preserve_term before delete on quarters for each row execute function protect_academic_history();
drop trigger if exists preserve_enrollment on student_enrollments;
create trigger preserve_enrollment before delete on student_enrollments for each row execute function protect_academic_history();

create or replace function link_section_year() returns trigger language plpgsql set search_path=public as $$
begin
  if tg_op='UPDATE' and (new.school_year<>old.school_year or new.grade_level<>old.grade_level) and exists(select 1 from student_enrollments where section_id=old.id) then
    raise exception 'An enrolled section cannot move to another year or grade. Add a new section.'; end if;
  select id into new.academic_year_id from academic_years where name=new.school_year;
  if new.academic_year_id is null then raise exception 'Create the academic year before adding its sections.'; end if;
  return new;
end $$;
drop trigger if exists section_year on class_sections;
create trigger section_year before insert or update on class_sections for each row execute function link_section_year();

create or replace function derive_student_name() returns trigger language plpgsql set search_path=public as $$
begin
  if new.role='student' and nullif(trim(new.first_name),'') is not null and nullif(trim(new.last_name),'') is not null then
    new.full_name=trim(new.first_name)||' '||trim(new.last_name); end if;
  return new;
end $$;
drop trigger if exists student_display_name on profiles;
create trigger student_display_name before insert or update on profiles for each row execute function derive_student_name();

revoke all on function save_academic_year(jsonb),save_academic_term(jsonb,uuid),enroll_student(uuid,uuid,uuid,uuid,text,uuid) from public,anon,authenticated;
grant execute on function save_academic_year(jsonb),save_academic_term(jsonb,uuid),enroll_student(uuid,uuid,uuid,uuid,text,uuid) to service_role;
-- Additional immutable learning-record context and the updated VR registration function follow.
alter table lesson_attempts add column if not exists academic_year_id uuid references academic_years(id) on delete restrict;
alter table lesson_attempts add column if not exists quarter_id uuid references quarters(id) on delete restrict;
alter table lesson_attempts add column if not exists enrollment_id uuid references student_enrollments(id) on delete restrict;
alter table lesson_attempts add column if not exists grade_level text;
alter table lesson_attempts add column if not exists section_id uuid references class_sections(id) on delete restrict;
alter table lesson_progress add column if not exists academic_year_id uuid references academic_years(id) on delete restrict;
alter table lesson_progress add column if not exists quarter_id uuid references quarters(id) on delete restrict;
alter table lesson_progress add column if not exists enrollment_id uuid references student_enrollments(id) on delete restrict;
alter table lesson_progress add column if not exists grade_level text;
alter table lesson_progress add column if not exists section_id uuid references class_sections(id) on delete restrict;
update lesson_attempts a set quarter_id=l.quarter_id,academic_year_id=q.academic_year_id,grade_level=m.grade_level
from lessons l join quarters q on q.id=l.quarter_id join modules m on m.id=l.module_id where a.lesson_id=l.id and a.quarter_id is null;
update lesson_progress a set quarter_id=l.quarter_id,academic_year_id=q.academic_year_id,grade_level=m.grade_level
from lessons l join quarters q on q.id=l.quarter_id join modules m on m.id=l.module_id where a.lesson_id=l.id and a.quarter_id is null;
create or replace function capture_learning_context() returns trigger language plpgsql set search_path=public as $$
declare q quarters; e student_enrollments;
begin
  if tg_op='UPDATE' then
    if (new.student_id,new.lesson_id,new.quarter_id,new.academic_year_id,new.enrollment_id,new.section_id,new.grade_level) is distinct from
       (old.student_id,old.lesson_id,old.quarter_id,old.academic_year_id,old.enrollment_id,old.section_id,old.grade_level) then
      raise exception 'Learning record context cannot be moved to another student or term.'; end if;
    return new;
  end if;
  select q1.* into q from lessons l join quarters q1 on q1.id=l.quarter_id where l.id=new.lesson_id;
  select * into e from student_enrollments where student_id=new.student_id and quarter_id=q.id and ended_at is null;
  if q.id is null or e.id is null then raise exception 'Enroll this student in the lesson academic year and term before recording progress.'; end if;
  new.quarter_id=q.id;new.academic_year_id=q.academic_year_id;new.enrollment_id=e.id;new.section_id=e.section_id;new.grade_level=e.grade_level;
  return new;
end $$;
drop trigger if exists learning_attempt_context on lesson_attempts;
create trigger learning_attempt_context before insert or update on lesson_attempts for each row execute function capture_learning_context();
drop trigger if exists learning_progress_context on lesson_progress;
create trigger learning_progress_context before insert or update on lesson_progress for each row execute function capture_learning_context();
create or replace function preserve_content_term() returns trigger language plpgsql set search_path=public as $$
begin
  if tg_op='DELETE' then
    if tg_table_name='lessons' and (exists(select 1 from lesson_attempts where lesson_id=old.id) or exists(select 1 from lesson_progress where lesson_id=old.id)) then
      raise exception 'This lesson has historical results. Archive it instead.'; end if;
    return old;
  end if;
  if old.quarter_id is not null and new.quarter_id is distinct from old.quarter_id then
    raise exception 'Content and assessments cannot move between terms. Create a new item for the new term.'; end if;
  return new;
end $$;
drop trigger if exists lesson_term_history on lessons;
create trigger lesson_term_history before update or delete on lessons for each row execute function preserve_content_term();
drop trigger if exists module_term_history on modules;
create trigger module_term_history before update on modules for each row execute function preserve_content_term();
drop trigger if exists competition_term_history on vr_competitions;
create trigger competition_term_history before update on vr_competitions for each row execute function preserve_content_term();

alter table vr_competitions add column if not exists section_id uuid references class_sections(id) on delete restrict;
update vr_competitions c set section_id=s.id from class_sections s,quarters q where c.quarter_id=q.id and s.academic_year_id=q.academic_year_id
and c.grade_level=s.grade_level and c.section=s.name and c.section_id is null;
create or replace function sync_competition_section() returns trigger language plpgsql set search_path=public as $$
begin
  update vr_competitions set section=new.name where section_id=new.id;
  return new;
end $$;
drop trigger if exists competition_section_rename on class_sections;
create trigger competition_section_rename after update of name on class_sections for each row execute function sync_competition_section();

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
  select id into v_enrollment from student_enrollments where student_id=p_student and quarter_id=v_quarter.id and section_id=v_section.id and ended_at is null;
  if v_enrollment is null then
    insert into student_enrollments(student_id,academic_year_id,quarter_id,section_id,grade_level,student_name,section_name)
      values(p_student,v_quarter.academic_year_id,v_quarter.id,v_section.id,v_section.grade_level,v_profile.full_name,v_section.name)
      on conflict (student_id,quarter_id) where ended_at is null do update set section_id=v_section.id,section_name=v_section.name
      returning id into v_enrollment;
    if v_enrollment is null then
      select id into v_enrollment from student_enrollments where student_id=p_student and quarter_id=v_quarter.id and ended_at is null limit 1;
    end if;
  end if;
  if v_enrollment is null then raise exception 'VR_CONTEXT_REQUIRED'; end if;
  insert into vr_assessment_sessions(id,student_id,competition_id,simulation_type,academic_year_id,quarter_id,enrollment_id,section_id,grade_level,section_name,student_name,scoring_configuration)
    values(p_id,p_student,p_competition,p_simulation,v_quarter.academic_year_id,v_quarter.id,v_enrollment,v_section.id,v_profile.grade_level,v_section.name,v_profile.full_name,p_scoring)
    returning * into v_existing;
  return v_existing;
end $$;

commit;
