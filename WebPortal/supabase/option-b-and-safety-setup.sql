-- ==============================================================================
-- TechWise 360: Option B Lab Station Pairing & Anti-Sabotage Security Setup
-- Target: Supabase SQL Editor
-- Safe and Idempotent: Can be run multiple times without causing errors.
-- ==============================================================================

-- 1. Ensure student_enrollments has ended_at and denormalized fields
ALTER TABLE IF EXISTS public.student_enrollments 
  ADD COLUMN IF NOT EXISTS ended_at timestamptz,
  ADD COLUMN IF NOT EXISTS student_name text,
  ADD COLUMN IF NOT EXISTS section_name text;

-- 2. Ensure VR Launch Codes table exists for Option B Station Pairings
CREATE TABLE IF NOT EXISTS public.vr_launch_codes (
  code_hash text PRIMARY KEY,
  student_id uuid NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz
);

-- 3. Ensure VR Device Sessions table exists for Headset Bearer Tokens
CREATE TABLE IF NOT EXISTS public.vr_device_sessions (
  token_hash text PRIMARY KEY,
  student_id uuid NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  expires_at timestamptz NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);

-- 4. Ensure VR Simulation Attempts table has jsonb metadata and forensic tracking
ALTER TABLE IF EXISTS public.vr_simulation_attempts
  ADD COLUMN IF NOT EXISTS metadata jsonb DEFAULT '{}'::jsonb,
  ADD COLUMN IF NOT EXISTS accuracy_percent numeric(5,2),
  ADD COLUMN IF NOT EXISTS received_at timestamptz DEFAULT now();

-- 5. Create Performance Indexes for Option B Station Queries & Forensic Audits
CREATE INDEX IF NOT EXISTS idx_vr_launch_codes_active 
  ON public.vr_launch_codes (code_hash, expires_at) 
  WHERE consumed_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_vr_device_sessions_active 
  ON public.vr_device_sessions (token_hash, expires_at);

CREATE INDEX IF NOT EXISTS idx_vr_attempts_station 
  ON public.vr_simulation_attempts ((metadata->>'station_id'));

CREATE INDEX IF NOT EXISTS idx_vr_attempts_device 
  ON public.vr_simulation_attempts ((metadata->>'device_id'));

-- 6. Grant Permissions to Service Role for Cloudflare Edge API Handshakes
ALTER TABLE public.vr_launch_codes ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.vr_device_sessions ENABLE ROW LEVEL SECURITY;

GRANT ALL ON public.vr_launch_codes TO service_role;
GRANT ALL ON public.vr_device_sessions TO service_role;
GRANT SELECT ON public.vr_launch_codes TO authenticated;
GRANT SELECT ON public.vr_device_sessions TO authenticated;
