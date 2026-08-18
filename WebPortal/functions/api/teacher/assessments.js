import {
  fail,
  getActiveQuarter,
  getAllLessonAttempts,
  getLessonProgress,
  getLessons,
  getModules,
  getStudents,
  json,
  optionsResponse,
  publicLessonAttempt,
  publicProfile,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);

    const [students, activeQuarter, modules, lessons, progress, attempts] = await Promise.all([
      getStudents(env),
      getActiveQuarter(env),
      getModules(env),
      getLessons(env),
      getLessonProgress(env),
      getAllLessonAttempts(env)
    ]);

    const moduleById = new Map(modules.map((module) => [module.id, module]));
    const approvedStudents = students.filter((student) => student.role === "student" && student.status === "approved");
    const activeQuarterId = activeQuarter?.id || null;
    const assessmentLessons = lessons.filter((lesson) => {
      const module = moduleById.get(lesson.module_id);
      if (!module) return false;
      if (lesson.status === "archived") return false;
      if (!["practice", "assessment"].includes(lesson.lesson_type)) return false;
      if (activeQuarterId && module.quarter_id !== activeQuarterId) return false;
      return true;
    });
    const assessmentIds = new Set(assessmentLessons.map((lesson) => lesson.id));
    const attemptsByLesson = groupBy(attempts.filter((attempt) => assessmentIds.has(attempt.lesson_id)), "lesson_id");
    const progressByLesson = groupBy(progress.filter((item) => assessmentIds.has(item.lesson_id)), "lesson_id");

    const assessments = assessmentLessons
      .sort((a, b) => (a.sort_order - b.sort_order) || String(a.created_at).localeCompare(String(b.created_at)))
      .map((lesson) => {
        const module = moduleById.get(lesson.module_id);
        const assignedStudents = approvedStudents.filter((student) => student.grade_level === module.grade_level);
        const lessonAttempts = attemptsByLesson.get(lesson.id) || [];
        const latestAttempts = latestAttemptByStudent(lessonAttempts);
        const submittedStudentIds = new Set(latestAttempts.map((attempt) => attempt.student_id));
        const scores = latestAttempts.map((attempt) => Number(attempt.score_percent || 0));
        const lessonProgress = progressByLesson.get(lesson.id) || [];

        return {
          id: lesson.id,
          lesson_id: lesson.id,
          title: lesson.title,
          description: lesson.description,
          lesson_type: lesson.lesson_type,
          status: lesson.status,
          due_date: lesson.due_date,
          scheduled_date: lesson.scheduled_date,
          module_id: module.id,
          module_title: module.title,
          module_category: module.category,
          grade_level: module.grade_level,
          assigned_count: assignedStudents.length,
          submitted_count: submittedStudentIds.size,
          average_score: average(scores),
          submission_rate: percent(submittedStudentIds.size, assignedStudents.length),
          progress_count: lessonProgress.filter((item) => item.status === "completed" || item.progress_percent === 100).length,
          attempts: latestAttempts.map(publicLessonAttempt),
          all_attempts: lessonAttempts.map(publicLessonAttempt),
          students: assignedStudents.map((student) => {
            const latest = latestAttempts.find((attempt) => attempt.student_id === student.id) || null;
            return {
              ...publicProfile(student),
              submitted: Boolean(latest),
              latest_attempt: latest ? publicLessonAttempt(latest) : null
            };
          })
        };
      });

    const recentSubmissions = attempts
      .filter((attempt) => assessmentIds.has(attempt.lesson_id))
      .slice(0, 8)
      .map((attempt) => {
        const lesson = assessmentLessons.find((item) => item.id === attempt.lesson_id);
        const module = lesson ? moduleById.get(lesson.module_id) : null;
        const student = students.find((item) => item.id === attempt.student_id);
        return {
          ...publicLessonAttempt(attempt),
          assessment_title: lesson?.title || "Assessment",
          module_title: module?.title || "",
          student_name: student?.full_name || student?.username || "Student"
        };
      });

    const topicScores = [...groupBy(assessments, "module_id").entries()].map(([, rows]) => {
      const first = rows[0];
      const scores = rows.flatMap((assessment) => assessment.attempts.map((attempt) => Number(attempt.score_percent || 0)));
      return {
        module_id: first.module_id,
        module_title: first.module_title,
        module_category: first.module_category,
        average_score: average(scores),
        assessment_count: rows.length,
        submitted_count: scores.length
      };
    });

    return json({
      assessments,
      recent_submissions: recentSubmissions,
      topic_scores: topicScores,
      students: approvedStudents.map(publicProfile)
    });
  } catch (error) {
    return fail(error.message || "Unable to load assessments.", error.status || 500);
  }
}

function groupBy(rows, key) {
  const map = new Map();
  rows.forEach((row) => {
    const value = row[key];
    if (!map.has(value)) map.set(value, []);
    map.get(value).push(row);
  });
  return map;
}

function latestAttemptByStudent(attempts) {
  const latest = new Map();
  attempts.forEach((attempt) => {
    const current = latest.get(attempt.student_id);
    if (!current || new Date(attempt.submitted_at) > new Date(current.submitted_at)) {
      latest.set(attempt.student_id, attempt);
    }
  });
  return [...latest.values()].sort((a, b) => new Date(b.submitted_at) - new Date(a.submitted_at));
}

function average(values) {
  const numeric = values.filter((value) => Number.isFinite(value));
  if (!numeric.length) return null;
  return Math.round(numeric.reduce((sum, value) => sum + value, 0) / numeric.length);
}

function percent(value, total) {
  return total ? Math.round((value / total) * 100) : 0;
}
