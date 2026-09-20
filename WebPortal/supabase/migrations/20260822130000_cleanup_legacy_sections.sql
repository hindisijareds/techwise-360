-- TechWise 360 legacy section cleanup for school year 2026-2027.
-- Run after 20260822_teacher_sections.sql.
--
-- The current application has six authoritative sections: three per grade.
-- This migration maps legacy profile values such as "1", "Section 1", and
-- "Eagle" to a valid section for the student's grade, preserves assignment
-- timestamps, synchronizes profile text fields, and removes obsolete managed
-- section rows for this school year.

begin;

-- Free an official code if a legacy row is incorrectly using it. The legacy
-- row is removed later, after its assignments have been remapped.
update public.class_sections legacy
set
  code = 'LEGACY-' || legacy.id::text,
  updated_at = now()
from (
  values
    ('Grade 9', 'Ylang Ylang', 'G9-YLANG'),
    ('Grade 9', 'Dama De Noche', 'G9-DAMA'),
    ('Grade 9', 'Sampaguita', 'G9-SAMPAGUITA'),
    ('Grade 10', 'Rosal', 'G10-ROSAL'),
    ('Grade 10', 'Lavender', 'G10-LAVENDER'),
    ('Grade 10', 'Tulip', 'G10-TULIP')
) as fixed(grade_level, section_name, section_code)
where legacy.school_year = '2026-2027'
  and lower(btrim(legacy.code)) = lower(fixed.section_code)
  and (
    legacy.grade_level <> fixed.grade_level
    or lower(btrim(legacy.name)) <> lower(fixed.section_name)
  );

insert into public.class_sections (
  school_year,
  grade_level,
  name,
  code,
  adviser_name,
  capacity,
  status,
  created_by
)
select
  '2026-2027',
  fixed.grade_level,
  fixed.section_name,
  fixed.section_code,
  fixed.adviser_name,
  35,
  'active',
  null
from (
  values
    ('Grade 9', 'Ylang Ylang', 'G9-YLANG', 'Carla Mar Locquiao'),
    ('Grade 9', 'Dama De Noche', 'G9-DAMA', 'Lara Santos'),
    ('Grade 9', 'Sampaguita', 'G9-SAMPAGUITA', 'Joel Jacob'),
    ('Grade 10', 'Rosal', 'G10-ROSAL', 'Salvador Reasonda Jr.'),
    ('Grade 10', 'Lavender', 'G10-LAVENDER', 'Noella Krista Valdez'),
    ('Grade 10', 'Tulip', 'G10-TULIP', 'Richie Unlayao')
) as fixed(grade_level, section_name, section_code, adviser_name)
where not exists (
  select 1
  from public.class_sections existing
  where existing.school_year = '2026-2027'
    and existing.grade_level = fixed.grade_level
    and lower(btrim(existing.name)) = lower(fixed.section_name)
);

create temporary table _techwise_fixed_sections (
  section_id uuid primary key,
  grade_level text not null,
  section_name text not null,
  section_code text not null,
  adviser_name text not null,
  section_position integer not null
) on commit drop;

insert into _techwise_fixed_sections (
  section_id,
  grade_level,
  section_name,
  section_code,
  adviser_name,
  section_position
)
select
  sections.id,
  fixed.grade_level,
  fixed.section_name,
  fixed.section_code,
  fixed.adviser_name,
  fixed.section_position
from (
  values
    ('Grade 9', 'Ylang Ylang', 'G9-YLANG', 'Carla Mar Locquiao', 1),
    ('Grade 9', 'Dama De Noche', 'G9-DAMA', 'Lara Santos', 2),
    ('Grade 9', 'Sampaguita', 'G9-SAMPAGUITA', 'Joel Jacob', 3),
    ('Grade 10', 'Rosal', 'G10-ROSAL', 'Salvador Reasonda Jr.', 1),
    ('Grade 10', 'Lavender', 'G10-LAVENDER', 'Noella Krista Valdez', 2),
    ('Grade 10', 'Tulip', 'G10-TULIP', 'Richie Unlayao', 3)
) as fixed(grade_level, section_name, section_code, adviser_name, section_position)
join public.class_sections sections
  on sections.school_year = '2026-2027'
 and sections.grade_level = fixed.grade_level
 and lower(btrim(sections.name)) = lower(fixed.section_name);

do $$
begin
  if (select count(*) from _techwise_fixed_sections) <> 6 then
    raise exception 'The six fixed 2026-2027 sections are missing. Run 20260822_teacher_sections.sql first.';
  end if;
end $$;

create temporary table _techwise_student_section_map (
  student_id uuid primary key,
  target_section_id uuid not null,
  previous_section_id uuid,
  previous_section_name text,
  previous_adviser text
) on commit drop;

insert into _techwise_student_section_map (
  student_id,
  target_section_id,
  previous_section_id,
  previous_section_name,
  previous_adviser
)
with student_candidates as (
  select
    profiles.id,
    profiles.grade_level,
    profiles.section_id,
    profiles.section,
    profiles.adviser,
    lower(btrim(coalesce(profiles.section, ''))) as normalized_section,
    dense_rank() over (
      partition by profiles.grade_level
      order by lower(btrim(coalesce(nullif(profiles.section, ''), profiles.id::text)))
    ) as legacy_position
  from public.profiles profiles
  where profiles.role = 'student'
    and profiles.grade_level in ('Grade 9', 'Grade 10')
), fixed_counts as (
  select grade_level, count(*)::integer as section_count
  from _techwise_fixed_sections
  group by grade_level
)
select
  student.id,
  coalesce(
    current_fixed.section_id,
    name_match.section_id,
    adviser_match.section_id,
    alias_match.section_id,
    fallback.section_id
  ),
  student.section_id,
  student.section,
  student.adviser
from student_candidates student
join fixed_counts counts on counts.grade_level = student.grade_level
left join _techwise_fixed_sections current_fixed
  on current_fixed.section_id = student.section_id
 and current_fixed.grade_level = student.grade_level
left join lateral (
  select fixed.section_id
  from _techwise_fixed_sections fixed
  where fixed.grade_level = student.grade_level
    and lower(btrim(fixed.section_name)) = student.normalized_section
  limit 1
) name_match on true
left join lateral (
  select fixed.section_id
  from _techwise_fixed_sections fixed
  where fixed.grade_level = student.grade_level
    and lower(btrim(fixed.adviser_name)) = lower(btrim(coalesce(student.adviser, '')))
  limit 1
) adviser_match on true
left join lateral (
  select fixed.section_id
  from _techwise_fixed_sections fixed
  where fixed.grade_level = student.grade_level
    and fixed.section_position = case
      when student.normalized_section in ('1', 'section 1', 'eagle') then 1
      when student.normalized_section in ('2', 'section 2') then 2
      when student.normalized_section in ('3', 'section 3') then 3
      else null
    end
  limit 1
) alias_match on true
join _techwise_fixed_sections fallback
  on fallback.grade_level = student.grade_level
 and fallback.section_position = 1 + mod((student.legacy_position - 1)::integer, counts.section_count);

-- Keep historical timestamps, but replace references to obsolete section rows
-- so those obsolete rows can be removed without violating the restrict FK.
update public.student_section_assignments assignments
set section_id = student_map.target_section_id
from _techwise_student_section_map student_map,
     public.class_sections legacy_section
where assignments.student_id = student_map.student_id
  and legacy_section.id = assignments.section_id
  and legacy_section.school_year = '2026-2027'
  and legacy_section.grade_level in ('Grade 9', 'Grade 10')
  and not exists (
    select 1
    from _techwise_fixed_sections fixed
    where fixed.section_id = legacy_section.id
  )
  and assignments.section_id is distinct from student_map.target_section_id;

-- Handle any remaining legacy assignment defensively using the first fixed
-- section of the legacy row's grade. Normal student assignments are handled by
-- the student-specific mapping above.
update public.student_section_assignments assignments
set section_id = fallback.section_id
from public.class_sections legacy_section,
     _techwise_fixed_sections fallback
where legacy_section.id = assignments.section_id
  and legacy_section.school_year = '2026-2027'
  and legacy_section.grade_level in ('Grade 9', 'Grade 10')
  and fallback.grade_level = legacy_section.grade_level
  and fallback.section_position = 1
  and not exists (
    select 1
    from _techwise_fixed_sections fixed
    where fixed.section_id = legacy_section.id
  );

-- Close a current assignment only when it points somewhere other than the
-- resolved fixed section. The following insert creates the replacement.
update public.student_section_assignments assignments
set ended_at = greatest(now(), assignments.assigned_at)
from _techwise_student_section_map student_map
where assignments.student_id = student_map.student_id
  and assignments.ended_at is null
  and assignments.section_id is distinct from student_map.target_section_id;

update public.profiles profiles
set
  section_id = fixed.section_id,
  section = fixed.section_name,
  adviser = fixed.adviser_name,
  updated_at = now()
from _techwise_student_section_map student_map
join _techwise_fixed_sections fixed
  on fixed.section_id = student_map.target_section_id
where profiles.id = student_map.student_id
  and (
    profiles.section_id is distinct from fixed.section_id
    or profiles.section is distinct from fixed.section_name
    or profiles.adviser is distinct from fixed.adviser_name
  );

insert into public.student_section_assignments (
  student_id,
  section_id,
  assigned_by,
  assigned_at
)
select
  student_map.student_id,
  student_map.target_section_id,
  null,
  now()
from _techwise_student_section_map student_map
where not exists (
  select 1
  from public.student_section_assignments current_assignment
  where current_assignment.student_id = student_map.student_id
    and current_assignment.ended_at is null
);

insert into public.section_activity (
  section_id,
  student_id,
  actor_id,
  action,
  details
)
select
  student_map.target_section_id,
  student_map.student_id,
  null,
  case
    when student_map.previous_section_id is null
      and nullif(btrim(coalesce(student_map.previous_section_name, '')), '') is null
      then 'student_assigned'
    else 'student_transferred'
  end,
  jsonb_build_object(
    'source', 'legacy_section_cleanup',
    'previous_section_id', student_map.previous_section_id,
    'previous_section_name', student_map.previous_section_name,
    'previous_adviser', student_map.previous_adviser
  )
from _techwise_student_section_map student_map
join _techwise_fixed_sections fixed
  on fixed.section_id = student_map.target_section_id
where student_map.previous_section_id is distinct from fixed.section_id
   or lower(btrim(coalesce(student_map.previous_section_name, ''))) <> lower(fixed.section_name)
   or lower(btrim(coalesce(student_map.previous_adviser, ''))) <> lower(fixed.adviser_name);

-- At this point no assignment references an obsolete 2026-2027 section.
-- Profile references use ON DELETE SET NULL, while section activity uses
-- ON DELETE CASCADE, so obsolete managed rows can now be removed safely.
delete from public.class_sections obsolete
where obsolete.school_year = '2026-2027'
  and obsolete.grade_level in ('Grade 9', 'Grade 10')
  and not exists (
    select 1
    from _techwise_fixed_sections fixed
    where fixed.section_id = obsolete.id
  );

-- Canonicalize the retained records after conflicting obsolete codes have
-- been removed. Capacity and room stay teacher-editable.
update public.class_sections sections
set
  name = fixed.section_name,
  code = fixed.section_code,
  adviser_name = fixed.adviser_name,
  status = 'active',
  updated_at = now()
from _techwise_fixed_sections fixed
where sections.id = fixed.section_id
  and (
    sections.name is distinct from fixed.section_name
    or sections.code is distinct from fixed.section_code
    or sections.adviser_name is distinct from fixed.adviser_name
    or sections.status is distinct from 'active'
  );

notify pgrst, 'reload schema';

commit;
