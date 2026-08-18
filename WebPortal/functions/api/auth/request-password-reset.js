import {
  fail,
  json,
  optionsResponse,
  readJson,
  requestPasswordResetEmail,
  requireEnv
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

const DEFAULT_PUBLIC_SITE_URL = "https://techwise360-web-portal.pages.dev";

function getPasswordResetOrigin(request, env) {
  const configured = env.PUBLIC_SITE_URL || env.SITE_URL;
  if (configured) return new URL(configured).origin;

  const requestUrl = new URL(request.url);
  const localHosts = new Set(["localhost", "127.0.0.1", "::1"]);
  if (localHosts.has(requestUrl.hostname)) return DEFAULT_PUBLIC_SITE_URL;

  return requestUrl.origin;
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await readJson(request);
    const origin = getPasswordResetOrigin(request, env);
    const redirectTo = new URL("/reset-password.html", origin).toString();
    await requestPasswordResetEmail(env, body.email, redirectTo);
    return json({ ok: true });
  } catch (error) {
    return fail(error.message || "Unable to send password reset email.", error.status || 500);
  }
}
