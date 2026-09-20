import {
  createStudentNotification,
  createStudentAvatarUrl,
  fail,
  getActiveQuarter,
  getAllLessonAttempts,
  getEvaluationCycles,
  getLessons,
  getModules,
  getProfileById,
  getQuarters,
  getStudentBadges,
  getStudentCertificates,
  getStudentProgress,
  json,
  optionsResponse,
  publicBadge,
  publicCertificate,
  publicEvaluationCycle,
  publicLesson,
  publicLessonAttempt,
  publicModule,
  publicProfile,
  publicProgress,
  publicQuarter,
  readJson,
  requireEnv,
  requireTeacher,
  selectRows,
  updateStudentProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const url = new URL(request.url);
    const studentId = String(url.searchParams.get("id") || "").trim();
    const selectedQuarterId = String(url.searchParams.get("quarter_id") || "").trim();
    if (!studentId) return fail("Student id is required.", 400);

    const student = await getProfileById(env, studentId);
    if (!student || student.role !== "student") return fail("Student was not found.", 404);

    const [
      activeQuarter,
      quarters,
      modules,
      lessons,
      progress,
      attempts,
      badges,
      certificates,
      cycles
    ] = await Promise.all([
      getActiveQuarter(env),
      getQuarters(env),
      getModules(env),
      getLessons(env),
      getStudentProgress(env, student.id),
      getAllLessonAttempts(env),
      getStudentBadges(env),
      getStudentCertificates(env),
      getEvaluationCycles(env)
    ]);

    const selectedQuarter = quarters.find((quarter) => quarter.id === selectedQuarterId) || activeQuarter || quarters[0] || null;
    const enrollments = selectedQuarter ? await selectRows(env,'student_enrollments',`student_id=eq.${student.id}&quarter_id=eq.${selectedQuarter.id}&select=*&order=created_at.desc`) : [];
    const historicalGrade = enrollments[0]?.grade_level || progress.find(p => p.quarter_id === selectedQuarter?.id)?.grade_level || student.grade_level;
    const visibleModules = modules.filter((module) =>
      module.grade_level === historicalGrade
      && module.quarter_id === selectedQuarter?.id
      && ["published", "archived"].includes(module.status)
    );
    const visibleModuleIds = new Set(visibleModules.map((module) => module.id));
    const visibleLessons = lessons.filter((lesson) =>
      visibleModuleIds.has(lesson.module_id)
      && lesson.quarter_id === selectedQuarter?.id
      && ["published", "archived"].includes(lesson.status)
    );
    const visibleLessonIds = new Set(visibleLessons.map((lesson) => lesson.id));
    const studentAttempts = attempts.filter((attempt) => attempt.student_id === student.id && visibleLessonIds.has(attempt.lesson_id));
    const evaluationCycle = cycles.find((cycle) =>
      cycle.quarter_id === selectedQuarter?.id
      && cycle.grade_level === student.grade_level
    ) || null;
    const avatarUrl = student.avatar_path ? await createStudentAvatarUrl(env, student.avatar_path).catch(() => "") : "";

    return json({
      student: publicProfile(student),
      active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
      selected_quarter: selectedQuarter ? publicQuarter(selectedQuarter) : null,
      enrollments,
      quarters: quarters.map(publicQuarter),
      modules: visibleModules.map(publicModule),
      lessons: visibleLessons.map(publicLesson),
      progress: progress.filter((item) => visibleLessonIds.has(item.lesson_id)).map(publicProgress),
      attempts: studentAttempts.map(publicLessonAttempt),
      badges: badges.filter((badge) => badge.student_id === student.id).map(publicBadge),
      certificates: certificates.filter((certificate) => certificate.student_id === student.id).map(publicCertificate),
      evaluation_cycle: evaluationCycle ? publicEvaluationCycle(evaluationCycle) : null,
      avatar_url: avatarUrl
    });
  } catch (error) {
    return fail(error.message || "Unable to load student profile.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const previous = body.id || body.student_id ? await getProfileById(env, body.id || body.student_id) : null;
    const student = await updateStudentProfile(env, body, teacher.id);
    if (student.status === "approved" && previous?.status !== "approved") {
      await createStudentNotification(env, {
        student_id: student.id,
        teacher_id: teacher.id,
        event_type: "account_approved",
        title: "Account approved",
        body: "Your TechWise 360 account has been approved. You can start learning now.",
        entity_type: "profile",
        entity_id: student.id,
        metadata: {
          status: "approved"
        }
      });
    }
    return json({ student: publicProfile(student) });
  } catch (error) {
    return fail(error.message || "Unable to update student.", error.status || 500);
  }
}
