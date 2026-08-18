import {
  fail,
  getActiveQuarter,
  getLessonProgress,
  getLessons,
  getModules,
  getStudentBadges,
  getStudentCertificates,
  getStudents,
  json,
  optionsResponse,
  publicBadge,
  publicCertificate,
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

    const [students, activeQuarter, modules, lessons, progress, badges, certificates] = await Promise.all([
      getStudents(env),
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getLessonProgress(env),
      getStudentBadges(env),
      getStudentCertificates(env)
    ]);

    return json({
      students: students.map(publicProfile),
      active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
      modules: modules.map(publicModule),
      lessons: lessons.map(publicLesson),
      progress: progress.map(publicProgress),
      badges: badges.map(publicBadge),
      certificates: certificates.map(publicCertificate)
    });
  } catch (error) {
    return fail(error.message || "Unable to load students.", error.status || 500);
  }
}
