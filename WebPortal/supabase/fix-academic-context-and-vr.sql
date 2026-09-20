-- ==============================================================================
-- TechWise 360 - Comprehensive Fix for Academic Context & VR Competition Upload
-- Run this script in your Supabase Project -> SQL Editor -> Run
-- This script is completely IDEMPOTENT (safe to run multiple times).
-- ==============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. Academic Years Table & Data Alignment
-- -----------------------------------------------------------------------------
ALTER TABLE IF EXISTS public.academic_years
  ADD COLUMN IF NOT EXISTS start_year integer,
  ADD COLUMN IF NOT EXISTS end_year integer,
  ADD COLUMN IF NOT EXISTS start_date date,
  ADD COLUMN IF NOT EXISTS end_date date,
  ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'inactive',
  ADD COLUMN IF NOT EXISTS updated_at timestamptz NOT NULL DEFAULT now();

-- Populate academic_years from existing school_year values
INSERT INTO public.academic_years (name)
SELECT DISTINCT school_year FROM public.class_sections WHERE school_year IS NOT NULL
UNION
SELECT DISTINCT school_year FROM public.quarters WHERE school_year IS NOT NULL
ON CONFLICT (name) DO NOTHING;

-- Set start_year and end_year
UPDATE public.academic_years
SET start_year = split_part(name, '-', 1)::integer,
    end_year = split_part(name, '-', 2)::integer
WHERE start_year IS NULL AND name ~ '^\d{4}-\d{4}$';

-- Ensure at least one active academic year exists
UPDATE public.academic_years
SET status = 'active'
WHERE name IN (SELECT school_year FROM public.quarters WHERE is_active LIMIT 1)
  AND NOT EXISTS (SELECT 1 FROM public.academic_years WHERE status = 'active');

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM public.academic_years WHERE status = 'active') THEN
    UPDATE public.academic_years
    SET status = 'active'
    WHERE id = (SELECT id FROM public.academic_years ORDER BY name DESC LIMIT 1);
  END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2. Quarters Table & Academic Year Linking
-- -----------------------------------------------------------------------------
ALTER TABLE IF EXISTS public.quarters
  ADD COLUMN IF NOT EXISTS academic_year_id uuid REFERENCES public.academic_years(id) ON DELETE RESTRICT;

UPDATE public.quarters q
SET academic_year_id = y.id
FROM public.academic_years y
WHERE q.school_year = y.name AND q.academic_year_id IS NULL;

-- Ensure at least one quarter is active
UPDATE public.quarters
SET is_active = true, status = 'active'
WHERE id = (
  SELECT id FROM public.quarters
  WHERE status <> 'archived'
  ORDER BY school_year DESC, name ASC
  LIMIT 1
)
AND NOT EXISTS (SELECT 1 FROM public.quarters WHERE is_active = true AND status = 'active');

-- -----------------------------------------------------------------------------
-- 3. Class Sections Table & Academic Year Linking
-- -----------------------------------------------------------------------------
ALTER TABLE IF EXISTS public.class_sections
  ADD COLUMN IF NOT EXISTS academic_year_id uuid REFERENCES public.academic_years(id) ON DELETE RESTRICT;

UPDATE public.class_sections s
SET academic_year_id = y.id
FROM public.academic_years y
WHERE s.school_year = y.name AND s.academic_year_id IS NULL;

-- -----------------------------------------------------------------------------
-- 4. Student Enrollments Table (Batch 4 Schema Fixes)
-- -----------------------------------------------------------------------------
ALTER TABLE IF EXISTS public.student_enrollments
  ADD COLUMN IF NOT EXISTS ended_at timestamptz,
  ADD COLUMN IF NOT EXISTS student_name text,
  ADD COLUMN IF NOT EXISTS section_name text;

-- Backfill section_name and student_name
UPDATE public.student_enrollments e
SET section_name = s.name
FROM public.class_sections s
WHERE e.section_id = s.id AND e.section_name IS NULL;

UPDATE public.student_enrollments e
SET student_name = p.full_name
FROM public.profiles p
WHERE e.student_id = p.id AND e.student_name IS NULL;

-- Deduplicate any existing open enrollments so the unique index builds cleanly
WITH ranked AS (
  SELECT id, row_number() OVER (PARTITION BY student_id, quarter_id ORDER BY created_at DESC, id DESC) AS n
  FROM public.student_enrollments
  WHERE ended_at IS NULL
)
UPDATE public.student_enrollments
SET ended_at = now()
WHERE id IN (SELECT id FROM ranked WHERE n > 1);

CREATE UNIQUE INDEX IF NOT EXISTS one_current_enrollment_per_term
ON public.student_enrollments (student_id, quarter_id)
WHERE ended_at IS NULL;

-- -----------------------------------------------------------------------------
-- 5. Learning Context Table Columns
-- -----------------------------------------------------------------------------
ALTER TABLE IF EXISTS public.lesson_attempts
  ADD COLUMN IF NOT EXISTS academic_year_id uuid REFERENCES public.academic_years(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS quarter_id uuid REFERENCES public.quarters(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS enrollment_id uuid REFERENCES public.student_enrollments(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS grade_level text,
  ADD COLUMN IF NOT EXISTS section_id uuid REFERENCES public.class_sections(id) ON DELETE RESTRICT;

ALTER TABLE IF EXISTS public.lesson_progress
  ADD COLUMN IF NOT EXISTS academic_year_id uuid REFERENCES public.academic_years(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS quarter_id uuid REFERENCES public.quarters(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS enrollment_id uuid REFERENCES public.student_enrollments(id) ON DELETE RESTRICT,
  ADD COLUMN IF NOT EXISTS grade_level text,
  ADD COLUMN IF NOT EXISTS section_id uuid REFERENCES public.class_sections(id) ON DELETE RESTRICT;

ALTER TABLE IF EXISTS public.vr_competitions
  ADD COLUMN IF NOT EXISTS section_id uuid REFERENCES public.class_sections(id) ON DELETE RESTRICT;

UPDATE public.vr_competitions c
SET section_id = s.id
FROM public.class_sections s, public.quarters q
WHERE c.quarter_id = q.id
  AND s.academic_year_id = q.academic_year_id
  AND c.grade_level = s.grade_level
  AND c.section = s.name
  AND c.section_id IS NULL;

-- -----------------------------------------------------------------------------
-- 6. Enrollment Validation Trigger (Robust & Fault-Tolerant)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.validate_enrollment_context()
RETURNS trigger LANGUAGE plpgsql SET search_path = public AS $$
DECLARE
  q quarters;
  s class_sections;
  p profiles;
BEGIN
  IF tg_op = 'UPDATE' THEN
    IF (new.student_id, new.academic_year_id, new.quarter_id, new.section_id, new.grade_level) IS DISTINCT FROM
       (old.student_id, old.academic_year_id, old.quarter_id, old.section_id, old.grade_level) THEN
      RAISE EXCEPTION 'Enrollment identity is historical. Add a new enrollment instead.';
    END IF;
    RETURN new;
  END IF;

  SELECT * INTO q FROM public.quarters WHERE id = new.quarter_id;
  SELECT * INTO s FROM public.class_sections WHERE id = new.section_id;
  SELECT * INTO p FROM public.profiles WHERE id = new.student_id;

  IF p.id IS NULL OR p.role <> 'student' OR p.status NOT IN ('approved', 'pending') THEN
    RAISE EXCEPTION 'Select an approved or pending student account.';
  END IF;

  IF q.id IS NULL OR s.id IS NULL THEN
    RAISE EXCEPTION 'Student enrollment requires a matching year, term, grade and active section.';
  END IF;

  -- Auto-heal academic_year_id if unlinked
  IF new.academic_year_id IS NULL THEN
    new.academic_year_id := coalesce(q.academic_year_id, s.academic_year_id);
  END IF;

  new.student_name := p.full_name;
  new.section_name := s.name;
  RETURN new;
END $$;

DROP TRIGGER IF EXISTS enrollment_context_validation ON public.student_enrollments;
CREATE TRIGGER enrollment_context_validation
  BEFORE INSERT OR UPDATE ON public.student_enrollments
  FOR EACH ROW EXECUTE FUNCTION public.validate_enrollment_context();

-- -----------------------------------------------------------------------------
-- 7. Backfill Active Student Enrollments
-- -----------------------------------------------------------------------------
INSERT INTO public.student_enrollments (
  student_id, academic_year_id, quarter_id, section_id, grade_level, student_name, section_name
)
SELECT
  p.id,
  coalesce(s.academic_year_id, q.academic_year_id),
  q.id,
  s.id,
  coalesce(s.grade_level, p.grade_level),
  p.full_name,
  s.name
FROM public.profiles p
JOIN public.class_sections s ON s.id = p.section_id
JOIN public.quarters q ON q.is_active AND q.status = 'active'
WHERE p.role = 'student'
  AND p.status IN ('pending', 'approved')
  AND s.status = 'active'
  AND NOT EXISTS (
    SELECT 1 FROM public.student_enrollments e
    WHERE e.student_id = p.id AND e.quarter_id = q.id AND e.ended_at IS NULL
  )
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 8. Fixed enroll_student RPC (Handles Section Transfers & Validation)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.enroll_student(
  p_student uuid,
  p_year uuid,
  p_term uuid,
  p_section uuid,
  p_grade text,
  p_teacher uuid DEFAULT NULL
)
RETURNS public.student_enrollments LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  e public.student_enrollments;
  q public.quarters;
  s public.class_sections;
  v_prof public.profiles;
  v_year uuid;
BEGIN
  PERFORM pg_advisory_xact_lock(640360);

  SELECT * INTO v_prof FROM public.profiles WHERE id = p_student AND role = 'student' FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Student account not found.';
  END IF;

  SELECT * INTO q FROM public.quarters WHERE id = p_term;
  IF q.id IS NULL THEN
    RAISE EXCEPTION 'Active term not found.';
  END IF;

  SELECT * INTO s FROM public.class_sections WHERE id = p_section;
  IF s.id IS NULL THEN
    RAISE EXCEPTION 'Class section not found.';
  END IF;

  -- Resolve academic_year_id
  v_year := coalesce(p_year, q.academic_year_id, s.academic_year_id);
  IF v_year IS NULL THEN
    SELECT id INTO v_year FROM public.academic_years WHERE name = coalesce(s.school_year, q.school_year) LIMIT 1;
  END IF;

  -- Auto-link section or quarter if missing academic_year_id
  IF s.academic_year_id IS NULL AND v_year IS NOT NULL THEN
    UPDATE public.class_sections SET academic_year_id = v_year WHERE id = s.id;
    s.academic_year_id := v_year;
  END IF;
  IF q.academic_year_id IS NULL AND v_year IS NOT NULL THEN
    UPDATE public.quarters SET academic_year_id = v_year WHERE id = q.id;
    q.academic_year_id := v_year;
  END IF;

  -- Check existing active enrollment for this quarter
  SELECT * INTO e FROM public.student_enrollments
  WHERE student_id = p_student AND quarter_id = p_term AND ended_at IS NULL
  ORDER BY created_at DESC LIMIT 1;

  IF FOUND THEN
    IF e.section_id = p_section THEN
      -- Already enrolled in this section; update profile to match and return
      UPDATE public.profiles
      SET section_id = s.id, section = s.name, grade_level = s.grade_level, adviser = s.adviser_name, updated_at = now()
      WHERE id = p_student;
      RETURN e;
    ELSE
      -- Transferring section: gracefully close the previous enrollment
      UPDATE public.student_enrollments SET ended_at = now() WHERE id = e.id;
    END IF;
  END IF;

  -- Insert the new enrollment record
  INSERT INTO public.student_enrollments (
    student_id, academic_year_id, quarter_id, section_id, grade_level, student_name, section_name
  ) VALUES (
    p_student, coalesce(v_year, q.academic_year_id), p_term, p_section, s.grade_level, v_prof.full_name, s.name
  ) RETURNING * INTO e;

  -- Synchronize student_section_assignments and profile
  IF q.is_active THEN
    UPDATE public.student_section_assignments
    SET ended_at = now()
    WHERE student_id = p_student AND ended_at IS NULL AND section_id <> p_section;

    INSERT INTO public.student_section_assignments (student_id, section_id, assigned_by, assigned_at)
    SELECT p_student, p_section, p_teacher, now()
    WHERE NOT EXISTS (
      SELECT 1 FROM public.student_section_assignments
      WHERE student_id = p_student AND ended_at IS NULL
    );

    UPDATE public.profiles
    SET section_id = s.id, section = s.name, grade_level = s.grade_level, adviser = s.adviser_name, updated_at = now()
    WHERE id = p_student;
  END IF;

  RETURN e;
END $$;

-- -----------------------------------------------------------------------------
-- 9. Resilient start_vr_assessment RPC (Auto-Enroll & Anti-Lockout)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.start_vr_assessment(
  p_student uuid,
  p_id uuid,
  p_simulation text,
  p_competition uuid,
  p_scoring jsonb
)
RETURNS public.vr_assessment_sessions LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  v_profile public.profiles;
  v_quarter public.quarters;
  v_section public.class_sections;
  v_comp public.vr_competitions;
  v_existing public.vr_assessment_sessions;
  v_enrollment uuid;
  v_year_id uuid;
BEGIN
  -- 1. Validate student
  SELECT * INTO v_profile FROM public.profiles WHERE id = p_student AND role = 'student' AND status = 'approved' FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'VR_STUDENT_REQUIRED';
  END IF;

  -- 2. Idempotent retry check
  SELECT * INTO v_existing FROM public.vr_assessment_sessions WHERE id = p_id;
  IF FOUND THEN
    IF v_existing.student_id <> p_student OR v_existing.simulation_type <> p_simulation OR v_existing.competition_id IS DISTINCT FROM p_competition THEN
      RAISE EXCEPTION 'VR_ATTEMPT_CONFLICT';
    END IF;
    RETURN v_existing;
  END IF;

  IF p_simulation NOT IN ('assembly', 'disassembly') THEN
    RAISE EXCEPTION 'VR_INVALID_RESULT';
  END IF;

  -- 3. Resolve active quarter
  SELECT * INTO v_quarter FROM public.quarters WHERE is_active AND status = 'active' LIMIT 1;
  IF v_quarter.id IS NULL THEN
    SELECT * INTO v_quarter FROM public.quarters WHERE is_active LIMIT 1;
  END IF;
  IF v_quarter.id IS NULL THEN
    SELECT * INTO v_quarter FROM public.quarters WHERE status <> 'archived' ORDER BY created_at DESC LIMIT 1;
  END IF;
  IF v_quarter.id IS NULL THEN
    RAISE EXCEPTION 'VR_CONTEXT_REQUIRED';
  END IF;

  -- 4. Resolve active section
  SELECT * INTO v_section FROM public.class_sections WHERE id = v_profile.section_id AND status = 'active';
  IF v_section.id IS NULL THEN
    SELECT s.* INTO v_section FROM public.class_sections s
      JOIN public.student_section_assignments a ON a.section_id = s.id
      WHERE a.student_id = p_student AND a.ended_at IS NULL AND s.status = 'active'
      ORDER BY a.assigned_at DESC LIMIT 1;
    IF v_section.id IS NOT NULL THEN
      UPDATE public.profiles
      SET section_id = v_section.id, section = v_section.name, grade_level = v_section.grade_level, updated_at = now()
      WHERE id = p_student;
      v_profile.section_id := v_section.id;
      v_profile.section := v_section.name;
      v_profile.grade_level := v_section.grade_level;
    END IF;
  END IF;
  IF v_section.id IS NULL THEN
    RAISE EXCEPTION 'VR_CONTEXT_REQUIRED';
  END IF;

  v_year_id := coalesce(v_quarter.academic_year_id, v_section.academic_year_id);

  -- 5. Competition availability & limits
  IF p_competition IS NOT NULL THEN
    SELECT * INTO v_comp FROM public.vr_competitions WHERE id = p_competition;
    IF v_comp.id IS NULL OR v_comp.status <> 'active'
      OR (v_comp.start_at IS NOT NULL AND now() < v_comp.start_at)
      OR (v_comp.end_at IS NOT NULL AND now() > v_comp.end_at)
      OR (v_comp.quarter_id IS NOT NULL AND v_comp.quarter_id <> v_quarter.id)
      OR (nullif(v_comp.grade_level, '') IS NOT NULL AND v_comp.grade_level <> v_profile.grade_level)
      OR (nullif(v_comp.section, '') IS NOT NULL AND v_comp.section <> v_section.name)
      OR (v_comp.simulation_type <> 'both' AND v_comp.simulation_type <> p_simulation) THEN
      RAISE EXCEPTION 'VR_COMPETITION_UNAVAILABLE';
    END IF;

    IF v_comp.attempts_allowed IS NOT NULL AND (
      (SELECT count(*) FROM public.vr_assessment_sessions WHERE student_id = p_student AND competition_id = p_competition) +
      (SELECT count(*) FROM public.vr_simulation_attempts WHERE student_id = p_student AND competition_id = p_competition AND assessment_session_id IS NULL)
    ) >= v_comp.attempts_allowed THEN
      RAISE EXCEPTION 'VR_ATTEMPT_LIMIT';
    END IF;
  END IF;

  -- 6. Check enrollment, or auto-enroll approved student
  SELECT id INTO v_enrollment FROM public.student_enrollments
  WHERE student_id = p_student AND quarter_id = v_quarter.id AND section_id = v_section.id AND ended_at IS NULL
  ORDER BY created_at DESC LIMIT 1;

  IF v_enrollment IS NULL THEN
    INSERT INTO public.student_enrollments (
      student_id, academic_year_id, quarter_id, section_id, grade_level, student_name, section_name
    ) VALUES (
      p_student, coalesce(v_year_id, v_quarter.academic_year_id), v_quarter.id, v_section.id,
      v_section.grade_level, v_profile.full_name, v_section.name
    )
    ON CONFLICT (student_id, quarter_id) WHERE ended_at IS NULL
    DO UPDATE SET section_id = v_section.id, section_name = v_section.name
    RETURNING id INTO v_enrollment;

    IF v_enrollment IS NULL THEN
      SELECT id INTO v_enrollment FROM public.student_enrollments
      WHERE student_id = p_student AND quarter_id = v_quarter.id AND ended_at IS NULL LIMIT 1;
    END IF;
  END IF;

  IF v_enrollment IS NULL THEN
    RAISE EXCEPTION 'VR_CONTEXT_REQUIRED';
  END IF;

  -- 7. Create assessment session
  INSERT INTO public.vr_assessment_sessions (
    id, student_id, competition_id, simulation_type, academic_year_id, quarter_id,
    enrollment_id, section_id, grade_level, section_name, student_name, scoring_configuration
  ) VALUES (
    p_id, p_student, p_competition, p_simulation, coalesce(v_year_id, v_quarter.academic_year_id),
    v_quarter.id, v_enrollment, v_section.id, v_profile.grade_level, v_section.name,
    v_profile.full_name, p_scoring
  ) RETURNING * INTO v_existing;

  RETURN v_existing;
END $$;

-- -----------------------------------------------------------------------------
-- 10. Permissions
-- -----------------------------------------------------------------------------
REVOKE ALL ON FUNCTION public.enroll_student(uuid,uuid,uuid,uuid,text,uuid) FROM public, anon;
REVOKE ALL ON FUNCTION public.start_vr_assessment(uuid,uuid,text,uuid,jsonb) FROM public, anon;
GRANT EXECUTE ON FUNCTION public.enroll_student(uuid,uuid,uuid,uuid,text,uuid) TO service_role, authenticated;
GRANT EXECUTE ON FUNCTION public.start_vr_assessment(uuid,uuid,text,uuid,jsonb) TO service_role, authenticated;
GRANT EXECUTE ON FUNCTION public.complete_vr_assessment(uuid,uuid,jsonb) TO service_role, authenticated;

COMMIT;
