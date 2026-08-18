import {
  ApiError,
  createLessonDownloadUrl,
  fail,
  getActiveQuarter,
  getLessonById,
  getLessonFileById,
  getLessonFilesByLesson,
  getModules,
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
    const fileId = url.searchParams.get("file_id");
    const lessonId = url.searchParams.get("lesson_id");

    let file = fileId ? await getLessonFileById(env, fileId) : null;
    if (!file && lessonId) {
      file = (await getLessonFilesByLesson(env, lessonId))[0] || null;
    }
    if (!file) throw new ApiError("Lesson file was not found.", 404);

    const lesson = file.lesson_id ? await getLessonById(env, file.lesson_id) : null;
    await ensureLessonFileAccess(env, profile, lesson);
    const signed_url = await createLessonDownloadUrl(env, file);
    return json({ signed_url });
  } catch (error) {
    return fail(error.message || "Unable to open lesson file.", error.status || 500);
  }
}

async function ensureLessonFileAccess(env, profile, lesson) {
  if (profile.role === "teacher" && profile.status === "approved") return;
  if (profile.role !== "student" || profile.status !== "approved") {
    throw new ApiError("Approved student access is required.", 403);
  }
  if (!lesson || lesson.status !== "published") {
    throw new ApiError("This lesson file is not available to students.", 403);
  }
  const [activeQuarter, modules] = await Promise.all([
    getActiveQuarter(env),
    getModules(env)
  ]);
  const module = modules.find((item) => item.id === lesson.module_id);
  if (!module || module.status !== "published" || module.grade_level !== profile.grade_level) {
    throw new ApiError("This lesson file is not assigned to your grade.", 403);
  }
  if (activeQuarter?.id && module.quarter_id !== activeQuarter.id) {
    throw new ApiError("This lesson file is not in the active term.", 403);
  }
}
