-- ==============================================================================
-- TechWise 360 - Quick Fix for student_enrollments schema
-- Run this in your Supabase Dashboard -> SQL Editor -> Run
-- ==============================================================================

-- 1. Add missing columns to student_enrollments table
ALTER TABLE public.student_enrollments 
  ADD COLUMN IF NOT EXISTS ended_at timestamptz,
  ADD COLUMN IF NOT EXISTS student_name text,
  ADD COLUMN IF NOT EXISTS section_name text;

-- 2. Populate section_name from class_sections if available
UPDATE public.student_enrollments e 
SET section_name = s.name 
FROM public.class_sections s 
WHERE e.section_id = s.id AND e.section_name IS NULL;

-- 3. Populate student_name from profiles if available
UPDATE public.student_enrollments e
SET student_name = p.full_name
FROM public.profiles p
WHERE e.student_id = p.id AND e.student_name IS NULL;

-- 4. Create an index on (student_id, quarter_id) for active enrollments
CREATE UNIQUE INDEX IF NOT EXISTS one_current_enrollment_per_term 
ON public.student_enrollments (student_id, quarter_id) 
WHERE ended_at IS NULL;
