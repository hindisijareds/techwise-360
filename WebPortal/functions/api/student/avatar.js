import {
  createStudentAvatarUrl,
  fail,
  json,
  optionsResponse,
  publicProfile,
  readJson,
  removeStudentAvatarObject,
  requireEnv,
  requireProfile,
  setStudentAvatarPath
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    const body = await readJson(request);
    const nextPath = String(body.path || "").trim();
    if (!isOwnAvatarPath(profile.id, nextPath)) {
      return fail("Profile picture path is invalid.", 400);
    }
    if (profile.avatar_path && profile.avatar_path !== nextPath) {
      await removeStudentAvatarObject(env, profile.avatar_path).catch(() => {});
    }
    const updated = await setStudentAvatarPath(env, profile.id, nextPath);
    const avatarUrl = await createStudentAvatarUrl(env, nextPath).catch(() => "");
    return json({ profile: publicProfile(updated), avatar_url: avatarUrl });
  } catch (error) {
    return fail(error.message || "Unable to save profile picture.", error.status || 500);
  }
}

export async function onRequestDelete({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    requireApprovedStudent(profile);
    await removeStudentAvatarObject(env, profile.avatar_path).catch(() => {});
    const updated = await setStudentAvatarPath(env, profile.id, null);
    return json({ profile: publicProfile(updated), avatar_url: "" });
  } catch (error) {
    return fail(error.message || "Unable to remove profile picture.", error.status || 500);
  }
}

function isOwnAvatarPath(studentId, path) {
  return new RegExp(`^${studentId.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}/avatar\\.(jpg|png|webp)$`).test(path);
}

function requireApprovedStudent(profile) {
  if (profile.role !== "student" || profile.status !== "approved") {
    throw Object.assign(new Error("Approved student access is required."), { status: 403 });
  }
}
