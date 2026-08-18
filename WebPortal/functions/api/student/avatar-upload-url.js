import {
  createStudentAvatarUploadUrl,
  fail,
  json,
  optionsResponse,
  readJson,
  requireEnv,
  requireProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    const body = await readJson(request);
    const upload = await createStudentAvatarUploadUrl(env, profile, body);
    return json({ ...upload, old_path: profile.avatar_path || "" });
  } catch (error) {
    return fail(error.message || "Unable to prepare profile picture upload.", error.status || 500);
  }
}

function requireApprovedStudent(profile) {
  if (profile.role !== "student" || profile.status !== "approved") {
    throw Object.assign(new Error("Approved student access is required."), { status: 403 });
  }
}
