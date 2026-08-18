import {
  ApiError,
  fail,
  getActiveQuarter,
  getLessons,
  getModules,
  getStudentProgress,
  json,
  optionsResponse,
  publicLesson,
  publicModule,
  publicProgress,
  publicQuarter,
  requireEnv,
  requireProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== "student" || profile.status !== "approved") {
      throw new ApiError("Approved student access is required.", 403);
    }
    const [activeQuarter, modules, lessons, progress] = await Promise.all([
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getStudentProgress(env, profile.id)
    ]);
    const quarterId = activeQuarter?.id || null;
    const visibleModules = modules.filter((module) =>
      module.status === "published"
      && module.grade_level === profile.grade_level
      && (!quarterId || module.quarter_id === quarterId)
    );
    const moduleIds = new Set(visibleModules.map((module) => module.id));
    const visibleLessons = lessons.filter((lesson) => lesson.status === "published" && moduleIds.has(lesson.module_id));
    return json({
      active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
      modules: visibleModules.map(publicModule),
      lessons: visibleLessons.map(publicLesson),
      progress: progress.map(publicProgress)
    });
  } catch (error) {
    return fail(error.message || "Unable to load completion summary.", error.status || 500);
  }
}
