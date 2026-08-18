import {
  ApiError,
  fail,
  json,
  optionsResponse,
  readJson,
  requireEnv,
  updateAuthenticatedPassword
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const authHeader = request.headers.get("Authorization") || "";
    const token = authHeader.startsWith("Bearer ") ? authHeader.slice(7).trim() : "";
    if (!token) {
      throw new ApiError("Reset token is missing or expired.", 401);
    }

    const body = await readJson(request);
    if (body.password !== body.confirm_password) {
      throw new ApiError("Passwords do not match.", 400);
    }

    await updateAuthenticatedPassword(env, token, body.password);
    return json({ ok: true });
  } catch (error) {
    return fail(error.message || "Unable to update password.", error.status || 500);
  }
}
