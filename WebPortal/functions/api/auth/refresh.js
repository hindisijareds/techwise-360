import {
  ApiError,
  fail,
  getProfileById,
  json,
  optionsResponse,
  publicProfile,
  readJson,
  refreshAuthSession,
  requireEnv,
  sessionPayload
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await readJson(request);
    const session = await refreshAuthSession(env, body.refresh_token);
    const profile = await getProfileById(env, session.user.id);

    if (!profile) {
      throw new ApiError("Profile was not found.", 404);
    }

    if (profile.status !== "approved") {
      throw new ApiError("Account is not approved.", 403);
    }

    return json({
      session: sessionPayload(session),
      profile: publicProfile(profile)
    });
  } catch (error) {
    return fail(error.message || "Unable to refresh session.", error.status || 500);
  }
}
