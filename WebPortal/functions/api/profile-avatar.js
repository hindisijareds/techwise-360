import {
  createStudentAvatarUrl,
  fail,
  getProfileById,
  json,
  optionsResponse,
  requireEnv,
  requireProfile
} from "./_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    const url = new URL(request.url);
    const studentId = String(url.searchParams.get("id") || "").trim();
    if (!studentId) return fail("Student id is required.", 400);
    const student = await getProfileById(env, studentId);
    if (!student || student.role !== "student") return fail("Student was not found.", 404);
    const isTeacher = profile.role === "teacher" && profile.status === "approved";
    const isOwner = profile.id === student.id && profile.role === "student" && profile.status === "approved";
    if (!isTeacher && !isOwner) return fail("You cannot access this profile picture.", 403);
    const signedUrl = student.avatar_path ? await createStudentAvatarUrl(env, student.avatar_path) : "";
    return json({ signed_url: signedUrl });
  } catch (error) {
    return fail(error.message || "Unable to load profile picture.", error.status || 500);
  }
}
