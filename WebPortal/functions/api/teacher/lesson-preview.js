import {
  ApiError,
  fail,
  getLessonById,
  getLessonFilesByLesson,
  getLessonQuestions,
  getLessonSections,
  getLessons,
  getModules,
  json,
  optionsResponse,
  publicLesson,
  publicLessonFile,
  publicLessonQuestion,
  publicLessonSection,
  publicModule,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const url = new URL(request.url);
    const lessonId = String(url.searchParams.get("lesson_id") || "").trim();
    if (!lessonId) throw new ApiError("Lesson id is required.", 400);

    const [lesson, modules, lessons, sections, questions, files] = await Promise.all([
      getLessonById(env, lessonId),
      getModules(env),
      getLessons(env),
      getLessonSections(env, lessonId),
      getLessonQuestions(env, lessonId),
      getOptionalLessonFiles(env, lessonId)
    ]);
    if (!lesson) throw new ApiError("Lesson was not found.", 404);
    const module = modules.find((item) => item.id === lesson.module_id) || null;
    const moduleLessons = lessons
      .filter((item) => item.module_id === lesson.module_id && item.status !== "archived")
      .sort((a, b) => (a.sort_order - b.sort_order) || String(a.created_at).localeCompare(String(b.created_at)));

    return json({
      mode: "teacher_preview",
      module: module ? publicModule(module) : null,
      lesson: publicLesson(lesson),
      module_lessons: moduleLessons.map(publicLesson),
      sections: sections.map(publicLessonSection),
      questions: questions.map((question) => publicLessonQuestion(question, true)),
      files: files.map(publicLessonFile),
      progress: null,
      attempts: []
    });
  } catch (error) {
    return fail(error.message || "Unable to preview lesson.", error.status || 500);
  }
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
