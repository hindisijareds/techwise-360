import {
  createStudentAvatarUrl,
  fail,
  json,
  optionsResponse,
  publicProfile,
  readJson,
  requireEnv,
  requireProfile,
  updateStudentSelfProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    const avatarUrl = profile.avatar_path ? await createStudentAvatarUrl(env, profile.avatar_path).catch(() => "") : "";
    return json({ profile: publicProfile(profile), avatar_url: avatarUrl });
  } catch (error) {
    return fail(error.message || "Unable to load profile.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    const body = await readJson(request);
    const updated = await updateStudentSelfProfile(env, profile.id, body);
    const avatarUrl = updated.avatar_path ? await createStudentAvatarUrl(env, updated.avatar_path).catch(() => "") : "";
    return json({ profile: publicProfile(updated), avatar_url: avatarUrl });
  } catch (error) {
    return fail(error.message || "Unable to update profile.", error.status || 500);
  }
}

function requireApprovedStudent(profile) {
  if (profile.role !== "student" || profile.status !== "approved") {
    throw Object.assign(new Error("Approved student access is required."), { status: 403 });
  }
}
