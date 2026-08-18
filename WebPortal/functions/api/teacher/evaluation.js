import {
  ApiError,
  EVALUATION_SURVEY_CATEGORIES,
  EVALUATION_SURVEY_ITEMS,
  fail,
  getActiveQuarter,
  getAllLessonAttempts,
  getEvaluationCycleById,
  getEvaluationCycles,
  getEvaluationSurveyResponses,
  getLessons,
  getModules,
  getStudents,
  json,
  optionsResponse,
  publicEvaluationCycle,
  publicEvaluationSurveyResponse,
  publicLesson,
  publicLessonAttempt,
  publicModule,
  publicProfile,
  publicQuarter,
  readJson,
  requireEnv,
  requireTeacher,
  saveEvaluationCycle,
  saveEvaluationSurveyResponse
} from "../_utils.js";

const GRADES = ["Grade 9", "Grade 10"];

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    return json(await buildEvaluationPayload(env, teacher));
  } catch (error) {
    return fail(error.message || "Unable to load evaluation data.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const action = String(body.action || "save_cycle").trim().toLowerCase();

    if (action === "save_cycle") {
      await validateCycleSelection(env, body);
      await saveEvaluationCycle(env, teacher.id, body);
      return json(await buildEvaluationPayload(env, teacher));
    }

    if (action === "submit_survey") {
      const cycleId = String(body.cycle_id || "").trim();
      if (!cycleId) throw new ApiError("Saved test setup is required.", 400);
      const cycle = await getEvaluationCycleById(env, cycleId);
      if (!cycle) throw new ApiError("Saved test setup was not found.", 404);
      await saveEvaluationSurveyResponse(env, cycle, teacher, "teacher", body);
      return json(await buildEvaluationPayload(env, teacher));
    }

    throw new ApiError("Evaluation action is invalid.", 400);
  } catch (error) {
    return fail(error.message || "Unable to update evaluation data.", error.status || 500);
  }
}

async function buildEvaluationPayload(env, teacher) {
  const [activeQuarter, modules, lessons, students, attempts, cycles, responses] = await Promise.all([
    getActiveQuarter(env),
    getModules(env),
    getLessons(env),
    getStudents(env),
    getAllLessonAttempts(env),
    getEvaluationCycles(env),
    getEvaluationSurveyResponses(env)
  ]);

  const quarterId = activeQuarter?.id || null;
  const moduleById = new Map(modules.map((module) => [module.id, module]));
  const activeModules = modules.filter((module) =>
    module.status === "published"
    && (!quarterId || module.quarter_id === quarterId)
  );
  const activeModuleIds = new Set(activeModules.map((module) => module.id));
  const assessmentOptions = lessons.filter((lesson) => {
    const module = moduleById.get(lesson.module_id);
    return lesson.status === "published"
      && ["practice", "assessment"].includes(lesson.lesson_type)
      && activeModuleIds.has(lesson.module_id)
      && module;
  }).map((lesson) => {
    const module = moduleById.get(lesson.module_id);
    return {
      ...publicLesson(lesson),
      module: publicModule(module),
      module_title: module.title,
      module_category: module.category,
      grade_level: module.grade_level
    };
  }).sort((a, b) =>
    String(a.grade_level).localeCompare(String(b.grade_level))
    || String(a.module_title).localeCompare(String(b.module_title))
    || (Number(a.sort_order || 0) - Number(b.sort_order || 0))
  );

  const activeCycles = cycles.filter((cycle) => !quarterId || cycle.quarter_id === quarterId);
  const activeCycleIds = new Set(activeCycles.map((cycle) => cycle.id));
  const activeResponses = responses.filter((response) => activeCycleIds.has(response.cycle_id));
  const approvedStudents = students.filter((student) => student.role === "student" && student.status === "approved");

  const summaries = GRADES.map((grade) => {
    const cycle = activeCycles.find((item) => item.grade_level === grade) || null;
    return buildGradeSummary({
      grade,
      cycle,
      students: approvedStudents.filter((student) => student.grade_level === grade),
      attempts,
      lessons,
      responses: cycle ? activeResponses.filter((response) => response.cycle_id === cycle.id) : [],
      teacherId: teacher.id
    });
  });

  return {
    active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
    survey_categories: EVALUATION_SURVEY_CATEGORIES,
    assessment_options: assessmentOptions,
    cycles: activeCycles.map(publicEvaluationCycle),
    summaries,
    overall: buildOverallSummary(summaries)
  };
}

async function validateCycleSelection(env, body) {
  const grade = String(body.grade_level || "").trim();
  const quarterId = String(body.quarter_id || "").trim();
  if (!GRADES.includes(grade)) throw new ApiError("Grade must be Grade 9 or Grade 10.", 400);
  if (!quarterId) throw new ApiError("Active term is required.", 400);

  const [modules, lessons] = await Promise.all([getModules(env), getLessons(env)]);
  const moduleById = new Map(modules.map((module) => [module.id, module]));
  const lessonById = new Map(lessons.map((lesson) => [lesson.id, lesson]));
  [body.pretest_lesson_id, body.posttest_lesson_id].filter(Boolean).forEach((lessonId) => {
    const lesson = lessonById.get(String(lessonId));
    const module = lesson ? moduleById.get(lesson.module_id) : null;
    if (!lesson || !module) throw new ApiError("Selected assessment was not found.", 400);
    if (lesson.status !== "published" || !["practice", "assessment"].includes(lesson.lesson_type)) {
      throw new ApiError("Test setup can only use published practice or assessment lessons.", 400);
    }
    if (module.status !== "published" || module.grade_level !== grade || module.quarter_id !== quarterId) {
      throw new ApiError("Selected assessment must match the grade and active term.", 400);
    }
  });
}

function buildGradeSummary({ grade, cycle, students, attempts, lessons, responses, teacherId }) {
  const lessonById = new Map(lessons.map((lesson) => [lesson.id, lesson]));
  const preLesson = cycle?.pretest_lesson_id ? lessonById.get(cycle.pretest_lesson_id) : null;
  const postLesson = cycle?.posttest_lesson_id ? lessonById.get(cycle.posttest_lesson_id) : null;
  const preAttempts = latestAttemptByStudent(attempts.filter((attempt) => attempt.lesson_id === cycle?.pretest_lesson_id));
  const postAttempts = latestAttemptByStudent(attempts.filter((attempt) => attempt.lesson_id === cycle?.posttest_lesson_id));
  const preScores = [];
  const postScores = [];

  const studentRows = students.map((student) => {
    const pre = preAttempts.get(student.id) || null;
    const post = postAttempts.get(student.id) || null;
    if (pre && hasNumber(pre.score_percent)) preScores.push(Number(pre.score_percent));
    if (post && hasNumber(post.score_percent)) postScores.push(Number(post.score_percent));
    return {
      student: publicProfile(student),
      pre_attempt: pre ? publicLessonAttempt(pre) : null,
      post_attempt: post ? publicLessonAttempt(post) : null,
      change: pre && post ? round1(Number(post.score_percent || 0) - Number(pre.score_percent || 0)) : null
    };
  });

  const survey = buildSurveySummary(responses, teacherId);
  const preAverage = average(preScores);
  const postAverage = average(postScores);

  return {
    grade_level: grade,
    cycle: cycle ? publicEvaluationCycle(cycle) : null,
    pretest_lesson: preLesson ? publicLesson(preLesson) : null,
    posttest_lesson: postLesson ? publicLesson(postLesson) : null,
    student_count: students.length,
    pretest_average: preAverage,
    posttest_average: postAverage,
    growth: preAverage !== null && postAverage !== null ? round1(postAverage - preAverage) : null,
    growth_percent: preAverage && postAverage !== null ? round1(((postAverage - preAverage) / preAverage) * 100) : null,
    missing_pretest_count: students.length - preScores.length,
    missing_posttest_count: students.length - postScores.length,
    students: studentRows,
    survey,
    readiness: {
      pretest_mapped: Boolean(cycle?.pretest_lesson_id),
      posttest_mapped: Boolean(cycle?.posttest_lesson_id),
      survey_open: Boolean(cycle?.survey_is_open),
      responses_collected: responses.length > 0,
      exports_ready: Boolean(cycle?.pretest_lesson_id && cycle?.posttest_lesson_id)
    }
  };
}

function buildSurveySummary(responses, teacherId) {
  const studentResponses = responses.filter((response) => response.respondent_role === "student");
  const teacherResponses = responses.filter((response) => response.respondent_role === "teacher");
  const categoryAverages = EVALUATION_SURVEY_CATEGORIES.map((category) => {
    const values = responses.flatMap((response) =>
      category.items.map((item) => Number(response.answers?.[item.key])).filter(Number.isFinite)
    );
    const value = average(values);
    return {
      key: category.key,
      label: category.label,
      average: value,
      rating: feedbackLabel(value)
    };
  });
  const allValues = responses.flatMap((response) =>
    EVALUATION_SURVEY_ITEMS.map((item) => Number(response.answers?.[item.key])).filter(Number.isFinite)
  );
  const overallAverage = average(allValues);
  return {
    responses: responses.map(publicEvaluationSurveyResponse),
    student_response_count: studentResponses.length,
    teacher_response_count: teacherResponses.length,
    total_response_count: responses.length,
    category_averages: categoryAverages,
    overall_average: overallAverage,
    overall_rating: feedbackLabel(overallAverage),
    comments: responses
      .filter((response) => response.comment)
      .map((response) => ({
        respondent_role: response.respondent_role,
        comment: response.comment,
        submitted_at: response.submitted_at
      }))
  };
}

function buildOverallSummary(summaries) {
  const studentCount = summaries.reduce((sum, item) => sum + item.student_count, 0);
  const preAverage = weightedAverage(summaries.map((item) => [item.pretest_average, item.student_count]));
  const postAverage = weightedAverage(summaries.map((item) => [item.posttest_average, item.student_count]));
  const surveyValues = summaries
    .flatMap((item) => item.survey.category_averages.map((category) => category.average))
    .filter(Number.isFinite);
  return {
    student_count: studentCount,
    pretest_average: preAverage,
    posttest_average: postAverage,
    growth: preAverage !== null && postAverage !== null ? round1(postAverage - preAverage) : null,
    growth_percent: preAverage && postAverage !== null ? round1(((postAverage - preAverage) / preAverage) * 100) : null,
    missing_pretest_count: summaries.reduce((sum, item) => sum + item.missing_pretest_count, 0),
    missing_posttest_count: summaries.reduce((sum, item) => sum + item.missing_posttest_count, 0),
    student_response_count: summaries.reduce((sum, item) => sum + item.survey.student_response_count, 0),
    teacher_response_count: summaries.reduce((sum, item) => sum + item.survey.teacher_response_count, 0),
    survey_average: average(surveyValues),
    survey_rating: feedbackLabel(average(surveyValues))
  };
}

function latestAttemptByStudent(attempts) {
  const latest = new Map();
  attempts.forEach((attempt) => {
    const current = latest.get(attempt.student_id);
    if (!current || new Date(attempt.submitted_at || 0) > new Date(current.submitted_at || 0)) {
      latest.set(attempt.student_id, attempt);
    }
  });
  return latest;
}

function average(values) {
  const numeric = values.filter(Number.isFinite);
  if (!numeric.length) return null;
  return round1(numeric.reduce((sum, value) => sum + value, 0) / numeric.length);
}

function weightedAverage(pairs) {
  const valid = pairs.filter(([value, weight]) => Number.isFinite(value) && Number(weight) > 0);
  if (!valid.length) return null;
  const totalWeight = valid.reduce((sum, [, weight]) => sum + Number(weight), 0);
  return round1(valid.reduce((sum, [value, weight]) => sum + Number(value) * Number(weight), 0) / totalWeight);
}

function round1(value) {
  return Math.round(Number(value) * 10) / 10;
}

function feedbackLabel(value) {
  if (!Number.isFinite(value)) return "Not collected";
  if (value >= 4.21) return "Very positive";
  if (value >= 3.41) return "Positive";
  if (value >= 2.61) return "Neutral";
  if (value >= 1.81) return "Needs improvement";
  return "Poor";
}

function hasNumber(value) {
  return value !== null && value !== undefined && value !== "" && Number.isFinite(Number(value));
}
