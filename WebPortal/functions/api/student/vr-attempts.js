import { ApiError, optionsResponse, patchRows, requireEnv, selectRows } from '../_utils.js';
import { limitedJson, requireVrStudent, rpc, SCORING, uuid, validateResult, vrFail, vrJson } from '../_vr.js';

export const onRequestOptions = () => optionsResponse();

function safeUuid(val) {
  if (!val) return null;
  try {
    let s = String(val).toLowerCase().trim();
    if (/^[a-f0-9]{32}$/.test(s)) s = s.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
    return /^[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$/.test(s) ? s : null;
  } catch (_) {
    return null;
  }
}

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const profile = await requireVrStudent(request, env);
    const rows = await selectRows(env, 'vr_simulation_attempts', `student_id=eq.${profile.id}&order=completed_at.desc&limit=25`).catch(() => []);
    return vrJson({ attempts: rows });
  } catch (e) {
    return vrFail(e);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const profile = await requireVrStudent(request, env);
    const body = await limitedJson(request);
    const rawLocalId = body.metadata?.local_attempt_id;
    const id = safeUuid(rawLocalId) || crypto.randomUUID();

    // 1. Resolve active quarter/term
    const qRows = await selectRows(env, 'quarters', 'is_active=eq.true&status=eq.active&limit=1').catch(() => []);
    const term = qRows[0] || (await selectRows(env, 'quarters', 'status=neq.archived&order=created_at.desc&limit=1').catch(() => []))[0] || null;

    // 2. Resolve student section
    let section = null;
    if (profile.section_id) {
      const sId = safeUuid(profile.section_id);
      if (sId) {
        const sRows = await selectRows(env, 'class_sections', `id=eq.${sId}&limit=1`).catch(() => []);
        section = sRows[0] || null;
      }
    }
    if (!section && profile.section) {
      const sRows = await selectRows(env, 'class_sections', `name=eq.${encodeURIComponent(profile.section)}&status=eq.active&limit=1`).catch(() => []);
      section = sRows[0] || null;
    }
    if (!section) {
      const sGrade = profile.grade_level ? `grade_level=eq.${encodeURIComponent(profile.grade_level)}&status=eq.active&limit=1` : 'status=eq.active&limit=1';
      const sRows = await selectRows(env, 'class_sections', sGrade).catch(() => []);
      section = sRows[0] || (await selectRows(env, 'class_sections', 'status=eq.active&limit=1').catch(() => []))[0] || null;
    }

    if (section && (!profile.section_id || profile.section_id !== section.id)) {
      try {
        await patchRows(env, 'profiles', `id=eq.${encodeURIComponent(profile.id)}`, {
          section_id: section.id,
          section: section.name,
          grade_level: section.grade_level,
          adviser: section.adviser_name || profile.adviser || ''
        });
      } catch (patchErr) {
        console.error('Profile section patch notice:', patchErr);
      }
      profile.section_id = section.id;
      profile.section = section.name;
      profile.grade_level = section.grade_level;
    }

    // 3. Resolve or create enrollment
    let enrollmentId = null;
    if (term && section) {
      try {
        const eRows = await selectRows(env, 'student_enrollments', `student_id=eq.${uuid(profile.id)}&quarter_id=eq.${term.id}&section_id=eq.${section.id}&limit=1`).catch(() => []);
        if (eRows[0]) {
          enrollmentId = eRows[0].id;
        } else {
          const postRes = await fetch(`${env.SUPABASE_URL}/rest/v1/student_enrollments`, {
            method: 'POST',
            headers: {
              apikey: env.SUPABASE_SERVICE_ROLE_KEY,
              Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
              'Content-Type': 'application/json',
              Prefer: 'return=representation'
            },
            body: JSON.stringify({
              student_id: profile.id,
              academic_year_id: term.academic_year_id || section.academic_year_id,
              quarter_id: term.id,
              section_id: section.id,
              grade_level: section.grade_level,
              student_name: profile.full_name,
              section_name: section.name
            })
          });
          const eData = await postRes.json().catch(() => null);
          enrollmentId = Array.isArray(eData) && eData[0] ? eData[0].id : null;
        }
      } catch (enrollErr) {
        console.error('Enrollment resolution notice:', enrollErr);
      }
    }

    // 4. Check or start assessment session
    let rows = await selectRows(env, 'vr_assessment_sessions', `select=*&id=eq.${id}&student_id=eq.${uuid(profile.id)}`).catch(() => []);
    if (!rows[0]) {
      let rpcStarted = false;
      try {
        await rpc(env, 'start_vr_assessment', {
          p_student: profile.id,
          p_id: id,
          p_simulation: body.simulation_type || 'assembly',
          p_competition: safeUuid(body.competition_id),
          p_scoring: SCORING
        });
        rpcStarted = true;
      } catch (err) {
        console.error('start_vr_assessment notice:', err.message || err);
      }

      if (!rpcStarted) {
        const yearId = term?.academic_year_id || section?.academic_year_id || null;
        const sessionPayload = {
          id,
          student_id: profile.id,
          competition_id: safeUuid(body.competition_id),
          simulation_type: body.simulation_type || 'assembly',
          academic_year_id: yearId,
          quarter_id: term?.id || null,
          section_id: section?.id || null,
          grade_level: profile.grade_level || section?.grade_level || '',
          section_name: section?.name || profile.section || '',
          student_name: profile.full_name,
          scoring_configuration: SCORING
        };
        if (enrollmentId) sessionPayload.enrollment_id = enrollmentId;

        await fetch(`${env.SUPABASE_URL}/rest/v1/vr_assessment_sessions`, {
          method: 'POST',
          headers: {
            apikey: env.SUPABASE_SERVICE_ROLE_KEY,
            Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
            'Content-Type': 'application/json',
            Prefer: 'return=representation'
          },
          body: JSON.stringify(sessionPayload)
        }).catch((e) => console.error('Direct session insert notice:', e));
      }

      rows = await selectRows(env, 'vr_assessment_sessions', `select=*&id=eq.${id}&student_id=eq.${uuid(profile.id)}`).catch(() => []);
    }

    if (!rows[0]) throw new ApiError('Assessment attempt could not be initialized for this student.', 404);

    // 5. Complete assessment
    let existing = [];
    try {
      existing = await selectRows(env, 'vr_simulation_attempts', `select=*&assessment_session_id=eq.${id}&student_id=eq.${uuid(profile.id)}`);
    } catch {
      try {
        existing = await selectRows(env, 'vr_simulation_attempts', `select=*&id=eq.${id}&student_id=eq.${uuid(profile.id)}`);
      } catch {}
    }

    let attempt = existing[0];
    if (!attempt) {
      const validated = validateResult(body, rows[0]);
      try {
        attempt = await rpc(env, 'complete_vr_assessment', { p_student: profile.id, p_id: id, p_result: validated });
      } catch (rpcErr) {
        if (rpcErr?.status === 503) {
          throw rpcErr;
        }
        console.error('complete_vr_assessment notice:', rpcErr.message || rpcErr);
        const attemptPayload = {
          assessment_session_id: id,
          student_id: profile.id,
          competition_id: rows[0].competition_id || null,
          simulation_type: rows[0].simulation_type || 'assembly',
          academic_year_id: rows[0].academic_year_id || null,
          quarter_id: rows[0].quarter_id || null,
          section_id: rows[0].section_id || null,
          grade_level: rows[0].grade_level || profile.grade_level,
          section_name: rows[0].section_name || section?.name || '',
          student_name: rows[0].student_name || profile.full_name,
          score_percent: validated.score_percent,
          accuracy_percent: validated.accuracy_percent,
          duration_seconds: validated.duration_seconds,
          mistakes: validated.mistakes,
          status: 'completed',
          metadata: validated.metadata,
          started_at: rows[0].started_at || new Date().toISOString(),
          completed_at: validated.completed_at,
          received_at: new Date().toISOString()
        };
        if (enrollmentId || rows[0].enrollment_id) attemptPayload.enrollment_id = enrollmentId || rows[0].enrollment_id;

        let insertRes = await fetch(`${env.SUPABASE_URL}/rest/v1/vr_simulation_attempts`, {
          method: 'POST',
          headers: {
            apikey: env.SUPABASE_SERVICE_ROLE_KEY,
            Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
            'Content-Type': 'application/json',
            Prefer: 'return=representation'
          },
          body: JSON.stringify(attemptPayload)
        });

        if (!insertRes.ok) {
          const errDetail = await insertRes.text().catch(() => '');
          console.warn('Full vr_simulation_attempts insert failed, attempting base schema fallback:', errDetail);
          const basePayload = {
            id,
            student_id: profile.id,
            competition_id: safeUuid(body.competition_id) || safeUuid(rows[0].competition_id) || null,
            simulation_type: body.simulation_type || rows[0].simulation_type || 'assembly',
            score_percent: Number(validated.score_percent) || 0,
            duration_seconds: Number(validated.duration_seconds) || 0,
            mistakes: Number(validated.mistakes) || 0,
            status: 'completed',
            metadata: validated.metadata || {},
            started_at: rows[0].started_at || new Date().toISOString(),
            completed_at: validated.completed_at || new Date().toISOString()
          };

          insertRes = await fetch(`${env.SUPABASE_URL}/rest/v1/vr_simulation_attempts`, {
            method: 'POST',
            headers: {
              apikey: env.SUPABASE_SERVICE_ROLE_KEY,
              Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
              'Content-Type': 'application/json',
              Prefer: 'return=representation'
            },
            body: JSON.stringify(basePayload)
          });
        }

        if (!insertRes.ok) {
          const failureText = await insertRes.text().catch(() => 'Supabase insert error');
          console.error('Base vr_simulation_attempts insert failed:', failureText);
          throw new ApiError(`Unable to persist VR attempt in database: ${failureText}`, 503);
        }

        const data = await insertRes.json().catch(() => null);
        attempt = Array.isArray(data) ? data[0] : data;
        if (!attempt || !attempt.student_id) {
          throw new ApiError('Unable to persist VR attempt in database.', 503);
        }
      }
    }

    return vrJson({ status: 'synced', attempt_id: id, attempt });
  } catch (e) {
    return vrFail(e);
  }
}
