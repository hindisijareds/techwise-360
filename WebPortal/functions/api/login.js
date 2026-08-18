import {
  ApiError,
  fail,
  findProfileByUsername,
  getProfileById,
  json,
  optionsResponse,
  publicProfile,
  readJson,
  requireEnv,
  sessionPayload,
  signInWithPassword
} from "./_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await readJson(request);
    const identifier = String(body.identifier || "").trim();
    const password = String(body.password || "");

    if (!identifier || !password) {
      throw new ApiError("Please enter your email/username and password.", 400);
    }

    let email = identifier.toLowerCase();
    if (!identifier.includes("@")) {
      const profile = await findProfileByUsername(env, identifier);
      if (!profile) {
        throw new ApiError("Invalid email/username or password.", 401);
      }
      email = profile.email;
    }

    const session = await signInWithPassword(env, email, password);
    const profile = await getProfileById(env, session.user.id);

    if (!profile) {
      throw new ApiError("Profile was not found.", 404);
    }

    if (profile.role === "student" && profile.status !== "approved") {
      const statusMessages = {
        pending: "Your account is still pending teacher approval.",
        rejected: "Your account request was rejected. Please contact your teacher.",
        inactive: "Your account is inactive. Please contact your teacher."
      };
      const statusText = statusMessages[profile.status] || "Your student account is not approved.";
      throw new ApiError(statusText, 403);
    }

    if (profile.role === "teacher" && profile.status !== "approved") {
      throw new ApiError("Teacher account is not approved.", 403);
    }

    return json({
      session: sessionPayload(session),
      profile: publicProfile(profile)
    });
  } catch (error) {
    return fail(error.message || "Unable to log in.", error.status || 500);
  }
}
