import {
  ApiError,
  createStudentAvatarUrl,
  fail,
  getActiveQuarter,
  getAllLessonAttempts,
  getLessons,
  getModules,
  getStudentBadges,
  getStudentCertificates,
  getStudentProgress,
  json,
  optionsResponse,
  publicBadge,
  publicCertificate,
  publicLesson,
  publicLessonAttempt,
  publicModule,
  publicProfile,
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

    const [activeQuarter, modules, lessons, progress, attempts, badges, certificates] = await Promise.all([
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getStudentProgress(env, profile.id),
      getAllLessonAttempts(env),
      getStudentBadges(env),
      getStudentCertificates(env)
    ]);

    const quarterId = activeQuarter?.id || null;
    const visibleModules = modules.filter((module) =>
      module.status === "published"
      && module.grade_level === profile.grade_level
      && (!quarterId || module.quarter_id === quarterId)
    );
    const moduleIds = new Set(visibleModules.map((module) => module.id));
    const visibleLessons = lessons.filter((lesson) =>
      lesson.status === "published"
      && moduleIds.has(lesson.module_id)
    );
    const visibleLessonIds = new Set(visibleLessons.map((lesson) => lesson.id));

    const avatarUrl = profile.avatar_path ? await createStudentAvatarUrl(env, profile.avatar_path).catch(() => "") : "";

    return json({
      profile: publicProfile(profile),
      avatar_url: avatarUrl,
      active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
      modules: visibleModules.map(publicModule),
      lessons: visibleLessons.map(publicLesson),
      progress: progress.map(publicProgress),
      attempts: attempts
        .filter((attempt) => attempt.student_id === profile.id && visibleLessonIds.has(attempt.lesson_id))
        .map(publicLessonAttempt),
      badges: badges
        .filter((badge) => badge.student_id === profile.id)
        .map(publicBadge),
      certificates: certificates
        .filter((certificate) => certificate.student_id === profile.id)
        .map(publicCertificate)
    });
  } catch (error) {
    return fail(error.message || "Unable to load dashboard.", error.status || 500);
  }
}
