import {
  ApiError,
  fail,
  insertRow,
  json,
  optionsResponse,
  patchRows,
  publicVrCompetition,
  readJson,
  requireEnv,
  requireTeacher,
  selectRows
} from "../_utils.js";

const SIMULATION_TYPES = ["assembly", "disassembly", "both"];
const STATUSES = ["draft", "active", "closed", "archived"];

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const competitions = await selectRows(env, "vr_competitions", "select=*&order=created_at.desc");
    return json({ competitions: competitions.map(publicVrCompetition) });
  } catch (error) {
    return fail(error.message || "Unable to load competitions.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const payload = cleanCompetitionPayload(await readJson(request), teacher.id);
    const competition = await insertRow(env, "vr_competitions", payload, "Unable to create competition.");
    return json({ competition: publicVrCompetition(competition) });
  } catch (error) {
    return fail(error.message || "Unable to create competition.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const body = await readJson(request);
    const id = String(body.id || "").trim();
    if (!id) throw new ApiError("Competition id is required.", 400);
    const payload = cleanCompetitionPayload(body, "", true);
    const rows = await patchRows(
      env,
      "vr_competitions",
      `id=eq.${encodeURIComponent(id)}`,
      payload,
      "Unable to update competition."
    );
    return json({ competition: publicVrCompetition(rows[0]) });
  } catch (error) {
    return fail(error.message || "Unable to update competition.", error.status || 500);
  }
}

function cleanCompetitionPayload(body, teacherId, partial = false) {
  const payload = {
    title: cleanText(body.title),
    description: cleanText(body.description),
    simulation_type: cleanText(body.simulation_type || "assembly").toLowerCase(),
    ranking_method: "score_time_mistakes",
    grade_level: cleanText(body.grade_level) || null,
    section: cleanText(body.section) || null,
    quarter_id: body.quarter_id ? String(body.quarter_id).trim() : null,
    start_at: cleanDateTime(body.start_at),
    end_at: cleanDateTime(body.end_at),
    attempts_allowed: body.attempts_allowed ? Number(body.attempts_allowed) : null,
    status: cleanText(body.status || "active").toLowerCase(),
    updated_at: new Date().toISOString()
  };
  if (!partial) payload.created_by = teacherId;
  Object.keys(payload).forEach((key) => payload[key] === undefined && delete payload[key]);
  if (!payload.title && !partial) throw new ApiError("Competition title is required.", 400);
  if (payload.simulation_type && !SIMULATION_TYPES.includes(payload.simulation_type)) {
    throw new ApiError("Simulation type is invalid.", 400);
  }
  if (payload.status && !STATUSES.includes(payload.status)) {
    throw new ApiError("Competition status is invalid.", 400);
  }
  if (payload.grade_level && !["Grade 9", "Grade 10"].includes(payload.grade_level)) {
    throw new ApiError("Grade level must be Grade 9 or Grade 10.", 400);
  }
  if (payload.attempts_allowed !== null && payload.attempts_allowed !== undefined
    && (!Number.isInteger(payload.attempts_allowed) || payload.attempts_allowed < 1)) {
    throw new ApiError("Attempts allowed must be a positive number.", 400);
  }
  return payload;
}

function cleanText(value) {
  return String(value || "").trim().replace(/\s+/g, " ").slice(0, 500);
}

function cleanDateTime(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
