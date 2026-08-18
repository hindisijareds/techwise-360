import {
  ApiError,
  createTeacherNotifications,
  fail,
  getActiveQuarter,
  getLessonAttempts,
  getLessonById,
  getLessonFilesByLesson,
  getLessonQuestions,
  getLessonSections,
  getLessons,
  getModules,
  getStudentProgress,
  json,
  optionsResponse,
  publicLesson,
  publicLessonAttempt,
  publicLessonFile,
  publicLessonQuestion,
  publicLessonSection,
  publicModule,
  publicProgress,
  publicQuarter,
  readJson,
  requireEnv,
  requireProfile,
  saveStudentProgress,
  submitLessonAttempt
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireApprovedStudent(request, env);
    const url = new URL(request.url);
    const lessonId = String(url.searchParams.get("lesson_id") || "").trim();

    if (!lessonId) {
      const list = await loadVisibleLessons(env, profile);
      return json({
        active_quarter: list.activeQuarter ? publicQuarter(list.activeQuarter) : null,
        modules: list.visibleModules.map(publicModule),
        lessons: list.visibleLessons.map(publicLesson),
        progress: list.progress.map(publicProgress)
      });
    }

    const data = await loadStudentLessonDetail(env, profile, lessonId);
    const existingProgress = data.progress.find((item) => item.lesson_id === lessonId);
    let progress = existingProgress || null;
    if (!progress) {
      progress = await saveStudentProgress(env, profile.id, lessonId, {
        status: "in_progress",
        progress_percent: 20,
        started_at: new Date().toISOString()
      });
    }
    const allProgress = existingProgress
      ? data.progress
      : [progress, ...data.progress.filter((item) => item.lesson_id !== lessonId)];

    return json({
      mode: "student",
      active_quarter: data.activeQuarter ? publicQuarter(data.activeQuarter) : null,
      module: publicModule(data.module),
      lesson: publicLesson(data.lesson),
      module_lessons: data.moduleLessons.map(publicLesson),
      sections: data.sections.map(publicLessonSection),
      questions: data.questions.map((question) => publicLessonQuestion(question, false)),
      files: data.files.map(publicLessonFile),
      progress: publicProgress(progress),
      all_progress: allProgress.map(publicProgress),
      attempts: data.attempts.map(publicLessonAttempt)
    });
  } catch (error) {
    return fail(error.message || "Unable to load lesson.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireApprovedStudent(request, env);
    const body = await readJson(request);
    const lessonId = String(body.lesson_id || "").trim();
    if (!lessonId) throw new ApiError("Lesson id is required.", 400);

    const data = await loadStudentLessonDetail(env, profile, lessonId);
    if (body.action === "complete_lesson") {
      if (data.lesson.lesson_type === "practice") {
        throw new ApiError("Submit the practice answers to complete this activity.", 400);
      }
      const now = new Date().toISOString();
      const progress = await saveStudentProgress(env, profile.id, lessonId, {
        status: "completed",
        progress_percent: 100,
        started_at: now,
        completed_at: now
      });
      await createTeacherNotifications(env, {
        event_type: "lesson_completed",
        student_id: profile.id,
        title: "Lesson completed",
        body: `${profile.full_name || profile.username} completed ${data.lesson.title}.`,
        entity_type: "lesson",
        entity_id: lessonId,
        metadata: {
          lesson_title: data.lesson.title,
          module_title: data.module.title,
          grade_level: data.module.grade_level
        }
      });
      return json({ progress: publicProgress(progress) });
    }

    const result = await submitLessonAttempt(env, profile.id, lessonId, data.questions, body);
    const isAssessment = data.lesson.lesson_type === "assessment";
    await createTeacherNotifications(env, {
      event_type: isAssessment ? "assessment_submitted" : "practice_submitted",
      student_id: profile.id,
      title: isAssessment ? "Assessment submitted" : "Practice submitted",
      body: `${profile.full_name || profile.username} scored ${result.attempt.score_percent}% on ${data.lesson.title}.`,
      entity_type: "lesson",
      entity_id: lessonId,
      metadata: {
        lesson_title: data.lesson.title,
        module_title: data.module.title,
        grade_level: data.module.grade_level,
        score_percent: result.attempt.score_percent,
        correct_count: result.attempt.correct_count,
        total_points: result.attempt.total_points
      }
    });

    return json({
      attempt: publicLessonAttempt(result.attempt),
      progress: publicProgress(result.progress),
      feedback: result.feedback
    });
  } catch (error) {
    return fail(error.message || "Unable to submit practice.", error.status || 500);
  }
}

async function requireApprovedStudent(request, env) {
  const { profile } = await requireProfile(request, env);
  if (profile.role !== "student" || profile.status !== "approved") {
    throw new ApiError("Approved student access is required.", 403);
  }
  return { profile };
}

async function loadVisibleLessons(env, profile) {
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
  const visibleLessons = lessons.filter((lesson) =>
    lesson.status === "published"
    && moduleIds.has(lesson.module_id)
  );
  return { activeQuarter, visibleModules, visibleLessons, progress };
}

async function loadStudentLessonDetail(env, profile, lessonId) {
  const [activeQuarter, modules, lessons, lesson, sections, questions, files, progress, attempts] = await Promise.all([
    getActiveQuarter(env),
    getModules(env),
    getLessons(env),
    getLessonById(env, lessonId),
    getLessonSections(env, lessonId),
    getLessonQuestions(env, lessonId),
    getOptionalLessonFiles(env, lessonId),
    getStudentProgress(env, profile.id),
    getLessonAttempts(env, lessonId, profile.id)
  ]);
  if (!lesson || lesson.status !== "published") {
    throw new ApiError("Lesson is not available.", 404);
  }
  const module = modules.find((item) => item.id === lesson.module_id);
  if (!module || module.status !== "published" || module.grade_level !== profile.grade_level) {
    throw new ApiError("This lesson is not assigned to your grade level.", 403);
  }
  if (activeQuarter?.id && module.quarter_id !== activeQuarter.id) {
    throw new ApiError("This lesson is not in the active term.", 403);
  }
  const moduleLessons = lessons
    .filter((item) => item.module_id === module.id && item.status === "published")
    .sort((a, b) => (a.sort_order - b.sort_order) || String(a.created_at).localeCompare(String(b.created_at)));
  return { activeQuarter, module, lesson, moduleLessons, sections, questions, files, progress, attempts };
}

async function getOptionalLessonFiles(env, lessonId) {
  try {
    return await getLessonFilesByLesson(env, lessonId);
  } catch (error) {
    const message = String(error.message || "");
    const missingOptionalTable =
      message.includes("lesson_files") &&
      (message.includes("schema cache") || message.includes("relation") || message.includes("does not exist"));
    if (missingOptionalTable) return [];
    throw error;
  }
}
