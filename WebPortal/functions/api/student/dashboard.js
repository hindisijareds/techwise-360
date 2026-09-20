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
  publicVrAttempt,
  requireEnv,
  requireProfile
  , selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== "student" || profile.status !== "approved") {
      throw new ApiError("Approved student access is required.", 403);
    }

    const [activeQuarter, modules, lessons, progress, attempts, badges, certificates, vrAttempts] = await Promise.all([
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getStudentProgress(env, profile.id),
      getAllLessonAttempts(env),
      getStudentBadges(env),
      getStudentCertificates(env),
      selectRows(env, "vr_simulation_attempts", `student_id=eq.${profile.id}&order=completed_at.desc&limit=50`).catch(() => [])
    ]);

    const quarterId = activeQuarter?.id || null;
    let enrollment = null;
    if (quarterId) {
      try {
        const enrollments = await selectRows(
          env,
          "student_enrollments",
          `student_id=eq.${profile.id}&quarter_id=eq.${quarterId}&select=*&order=created_at.desc&limit=5`
        );
        enrollment = enrollments.find((e) => !e.ended_at) || enrollments[0] || null;
      } catch {
        enrollment = null;
      }
    }

    const effectiveGrade = enrollment?.grade_level || profile.grade_level || null;
    const visibleModules = modules.filter((module) =>
      module.status === "published"
      && (!effectiveGrade || module.grade_level === effectiveGrade)
      && (!quarterId || !module.quarter_id || module.quarter_id === quarterId)
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
      vr_attempts: (vrAttempts || []).map(publicVrAttempt),
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
