import {
  ApiError,
  EVALUATION_SURVEY_CATEGORIES,
  fail,
  getActiveQuarter,
  getEvaluationCycleById,
  getEvaluationCycles,
  getEvaluationSurveyResponses,
  json,
  optionsResponse,
  publicEvaluationCycle,
  publicEvaluationSurveyResponse,
  readJson,
  requireEnv,
  requireProfile,
  saveEvaluationSurveyResponse
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);

    const [activeQuarter, cycles, responses] = await Promise.all([
      getActiveQuarter(env),
      getEvaluationCycles(env),
      getEvaluationSurveyResponses(env)
    ]);
    const cycle = findAssignedCycle(activeQuarter, cycles, profile);
    const response = cycle
      ? responses.find((item) => item.cycle_id === cycle.id && item.respondent_id === profile.id) || null
      : null;

    return json({
      survey_categories: EVALUATION_SURVEY_CATEGORIES,
      cycle: cycle ? publicEvaluationCycle(cycle) : null,
      survey_open: Boolean(cycle?.survey_is_open),
      response: response ? publicEvaluationSurveyResponse(response) : null
    });
  } catch (error) {
    return fail(error.message || "Unable to load feedback survey.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    const body = await readJson(request);
    const cycleId = String(body.cycle_id || "").trim();
    if (!cycleId) throw new ApiError("Feedback survey is required.", 400);

    const cycle = await getEvaluationCycleById(env, cycleId);
    if (!cycle) throw new ApiError("Feedback survey was not found.", 404);
    const response = await saveEvaluationSurveyResponse(env, cycle, profile, "student", body);

    return json({
      survey_categories: EVALUATION_SURVEY_CATEGORIES,
      cycle: publicEvaluationCycle(cycle),
      survey_open: Boolean(cycle.survey_is_open),
      response: publicEvaluationSurveyResponse(response)
    });
  } catch (error) {
    return fail(error.message || "Unable to submit feedback survey.", error.status || 500);
  }
}

function requireApprovedStudent(profile) {
  if (profile.role !== "student" || profile.status !== "approved") {
    throw new ApiError("Approved student access is required.", 403);
  }
}

function findAssignedCycle(activeQuarter, cycles, profile) {
  const quarterId = activeQuarter?.id || null;
  return cycles.find((cycle) =>
    cycle.grade_level === profile.grade_level
    && (!quarterId || cycle.quarter_id === quarterId)
  ) || null;
}
