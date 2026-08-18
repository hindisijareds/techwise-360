import {
  fail,
  json,
  optionsResponse,
  publicProfile,
  requireEnv,
  requireProfile
} from "./_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    return json({ profile: publicProfile(profile) });
  } catch (error) {
    return fail(error.message || "Unable to load profile.", error.status || 500);
  }
}
