import {
  ApiError,
  fail,
  getStudents,
  json,
  optionsResponse,
  publicProfile,
  publicVrAttempt,
  publicVrCompetition,
  requireEnv,
  requireProfile,
  selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== "student" || profile.status !== "approved") {
      throw new ApiError("Approved student access is required.", 403);
    }
    const [students, attempts, competitions] = await Promise.all([
      getStudents(env),
      selectRows(env, "vr_simulation_attempts", "select=*&order=completed_at.desc"),
      selectRows(env, "vr_competitions", "select=*&order=created_at.desc")
    ]);
    const classmates = students.filter((student) =>
      student.role === "student"
      && student.status === "approved"
      && student.grade_level === profile.grade_level
      && String(student.section || "") === String(profile.section || "")
    );
    const result = buildStudentLeaderboard(classmates, attempts, competitions, profile);
    return json(result);
  } catch (error) {
    return fail(error.message || "Unable to load leaderboard.", error.status || 500);
  }
}

function buildStudentLeaderboard(students, attempts, competitions, profile) {
  const studentById = new Map(students.map((student) => [student.id, student]));
  const visibleCompetitions = competitions.filter((competition) =>
    ["active", "closed"].includes(competition.status)
    && (!competition.grade_level || competition.grade_level === profile.grade_level)
    && (!competition.section || competition.section === profile.section)
  );
  const visibleCompetitionIds = new Set(visibleCompetitions.map((competition) => competition.id));
  const visibleAttempts = attempts
    .filter((attempt) => studentById.has(attempt.student_id))
    .filter((attempt) => ["completed", "qualified"].includes(attempt.status || "completed"))
    .filter((attempt) => !attempt.competition_id || visibleCompetitionIds.has(attempt.competition_id));
  const bestByKey = new Map();
  visibleAttempts.forEach((attempt) => {
    const key = `${attempt.student_id}|${attempt.competition_id || "open"}|${attempt.simulation_type}`;
    const existing = bestByKey.get(key);
    if (!existing || compareAttempts(attempt, existing) < 0) bestByKey.set(key, attempt);
  });
  const competitionById = new Map(visibleCompetitions.map((competition) => [competition.id, competition]));
  const rows = [...bestByKey.values()].sort(compareAttempts).map((attempt, index) => ({
    rank: index + 1,
    student: publicProfile(studentById.get(attempt.student_id)),
    attempt: publicVrAttempt(attempt),
    competition: attempt.competition_id ? publicVrCompetition(competitionById.get(attempt.competition_id)) : null,
    is_current_student: attempt.student_id === profile.id
  }));
  return {
    rows,
    competitions: visibleCompetitions.map(publicVrCompetition),
    summary: {
      top_score: rows[0]?.attempt.score_percent || 0,
      student_rank: rows.find((row) => row.is_current_student)?.rank || null,
      participants_ranked: new Set(rows.map((row) => row.student.id)).size
    }
  };
}

function compareAttempts(a, b) {
  const scoreDiff = Number(b.score_percent || 0) - Number(a.score_percent || 0);
  if (scoreDiff) return scoreDiff;
  const timeDiff = Number(a.duration_seconds || 0) - Number(b.duration_seconds || 0);
  if (timeDiff) return timeDiff;
  const mistakeDiff = Number(a.mistakes || 0) - Number(b.mistakes || 0);
  if (mistakeDiff) return mistakeDiff;
  return new Date(b.completed_at || 0) - new Date(a.completed_at || 0);
}
