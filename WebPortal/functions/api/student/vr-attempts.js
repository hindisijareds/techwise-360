import {
  ApiError,
  fail,
  insertRow,
  json,
  optionsResponse,
  publicVrAttempt,
  readJson,
  requireEnv,
  requireProfile,
  selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== "student" || profile.status !== "approved") {
      throw new ApiError("Approved student access is required.", 403);
    }
    const body = await readJson(request);
    const payload = await cleanAttemptPayload(env, body, profile);
    const attempt = await insertRow(env, "vr_simulation_attempts", payload, "Unable to save VR attempt.");
    return json({ attempt: publicVrAttempt(attempt) });
  } catch (error) {
    return fail(error.message || "Unable to save VR attempt.", error.status || 500);
  }
}

async function cleanAttemptPayload(env, body, profile) {
  const simulationType = String(body.simulation_type || "").trim().toLowerCase();
  if (!["assembly", "disassembly"].includes(simulationType)) {
    throw new ApiError("Simulation type must be assembly or disassembly.", 400);
  }
  const score = Number(body.score_percent);
  const duration = Number(body.duration_seconds);
  const mistakes = Number(body.mistakes || 0);
  if (!Number.isFinite(score) || score < 0 || score > 100) throw new ApiError("Score must be 0 to 100.", 400);
  if (!Number.isInteger(duration) || duration < 0) throw new ApiError("Duration must be a positive number of seconds.", 400);
  if (!Number.isInteger(mistakes) || mistakes < 0) throw new ApiError("Mistakes must be zero or more.", 400);

  const completedAt = body.completed_at ? new Date(body.completed_at) : new Date();
  if (Number.isNaN(completedAt.getTime())) {
    throw new ApiError("Completed date is invalid.", 400);
  }

  const competitionId = body.competition_id ? String(body.competition_id).trim() : null;
  if (competitionId) {
    const rows = await selectRows(env, "vr_competitions", `select=*&id=eq.${encodeURIComponent(competitionId)}`);
    const competition = rows[0];
    if (!competition || !isCompetitionAcceptingAttempt(competition, completedAt)) {
      throw new ApiError("Active competition was not found for this attempt.", 404);
    }
    if (competition.grade_level && competition.grade_level !== profile.grade_level) throw new ApiError("Competition is not available for your grade.", 403);
    if (competition.section && competition.section !== profile.section) throw new ApiError("Competition is not available for your section.", 403);
    if (competition.simulation_type !== "both" && competition.simulation_type !== simulationType) {
      throw new ApiError("Competition simulation type does not match this attempt.", 400);
    }
  }

  return {
    student_id: profile.id,
    competition_id: competitionId,
    simulation_type: simulationType,
    score_percent: Math.round(score * 100) / 100,
    duration_seconds: duration,
    mistakes,
    status: "completed",
    metadata: body.metadata && typeof body.metadata === "object" && !Array.isArray(body.metadata) ? body.metadata : {},
    started_at: body.started_at ? new Date(body.started_at).toISOString() : null,
    completed_at: completedAt.toISOString()
  };
}

function isCompetitionAcceptingAttempt(competition, completedAt) {
  if (competition.status === "active") return true;
  if (competition.status !== "closed") return false;

  const completedTime = completedAt.getTime();
  const startTime = competition.start_at ? new Date(competition.start_at).getTime() : null;
  const endTime = competition.end_at ? new Date(competition.end_at).getTime() : null;

  if (startTime && completedTime < startTime) return false;
  if (endTime && completedTime > endTime) return false;

  return Boolean(competition.start_at || competition.end_at);
}
