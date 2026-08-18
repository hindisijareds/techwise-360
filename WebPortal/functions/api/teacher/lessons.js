import {
  createLesson,
  createStudentNotificationsForGrade,
  fail,
  getLessonById,
  getLessons,
  getModules,
  json,
  optionsResponse,
  publicLesson,
  readJson,
  requireEnv,
  requireTeacher,
  saveLessonStructure,
  updateLesson,
  updateLessonFile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const lessons = await getLessons(env);
    return json({ lessons: lessons.map(publicLesson) });
  } catch (error) {
    return fail(error.message || "Unable to load lessons.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const lesson = await createLesson(env, body, teacher.id);
    if (body.file_id) {
      await updateLessonFile(env, { id: body.file_id, lesson_id: lesson.id });
    }
    await saveLessonStructure(env, lesson.id, body);
    await notifyStudentsForPublishedLesson(env, teacher, lesson, "published");
    return json({ lesson: publicLesson(lesson) }, 201);
  } catch (error) {
    return fail(error.message || "Unable to create lesson.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const previous = body.id ? await getLessonById(env, body.id) : null;
    const lesson = await updateLesson(env, body);
    if (body.file_id) {
      await updateLessonFile(env, { id: body.file_id, lesson_id: lesson.id });
    }
    await saveLessonStructure(env, lesson.id, body);
    await notifyStudentsForPublishedLesson(env, teacher, lesson, previous?.status === "published" ? "updated" : "published");
    return json({ lesson: publicLesson(lesson) });
  } catch (error) {
    return fail(error.message || "Unable to update lesson.", error.status || 500);
  }
}

async function notifyStudentsForPublishedLesson(env, teacher, lesson, action) {
  if (lesson.status !== "published") return;
  const modules = await getModules(env);
  const module = modules.find((item) => item.id === lesson.module_id);
  if (!module?.grade_level) return;

  const isCheck = ["practice", "assessment"].includes(lesson.lesson_type);
  const eventType = isCheck
    ? action === "updated" ? "assessment_updated" : "assessment_published"
    : action === "updated" ? "lesson_updated" : "lesson_published";
  const title = isCheck
    ? action === "updated" ? "Check updated" : "New check available"
    : action === "updated" ? "Lesson updated" : "New lesson available";
  const moduleTitle = module.title || "your module";

  await createStudentNotificationsForGrade(env, module.grade_level, {
    teacher_id: teacher.id,
    event_type: eventType,
    title,
    body: `${lesson.title} is ${action === "updated" ? "updated" : "now available"} in ${moduleTitle}.`,
    entity_type: "lesson",
    entity_id: lesson.id,
    metadata: {
      lesson_title: lesson.title,
      lesson_type: lesson.lesson_type,
      module_title: moduleTitle,
      due_date: lesson.due_date || null
    }
  });
}
