import {
  ApiError,
  fail,
  getStudentBadges,
  json,
  optionsResponse,
  publicBadge,
  publicBadgeDefinition,
  requireEnv,
  requireProfile,
  selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== "student" || profile.status !== "approved") {
      throw new ApiError("Approved student access is required.", 403);
    }
    const [awards, definitions] = await Promise.all([
      getStudentBadges(env),
      selectRows(env, "badge_definitions", "select=*&status=eq.active&order=title.asc")
    ]);
    return json({
      badges: awards.filter((badge) => badge.student_id === profile.id).map(publicBadge),
      definitions: definitions.map(publicBadgeDefinition)
    });
  } catch (error) {
    return fail(error.message || "Unable to load badges.", error.status || 500);
  }
}
