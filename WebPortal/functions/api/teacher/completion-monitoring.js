import {
  fail,
  getActiveQuarter,
  getLessonProgress,
  getLessons,
  getModules,
  getStudents,
  json,
  optionsResponse,
  publicLesson,
  publicModule,
  publicProfile,
  publicProgress,
  publicQuarter,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const [students, activeQuarter, modules, lessons, progress] = await Promise.all([
      getStudents(env),
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getLessonProgress(env)
    ]);
    return json({
      active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
      students: students.filter((student) => student.role === "student").map(publicProfile),
      modules: modules.map(publicModule),
      lessons: lessons.map(publicLesson),
      progress: progress.map(publicProgress)
    });
  } catch (error) {
    return fail(error.message || "Unable to load completion monitoring.", error.status || 500);
  }
}
