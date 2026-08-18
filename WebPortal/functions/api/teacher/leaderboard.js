import {
  fail,
  getStudents,
  json,
  optionsResponse,
  publicProfile,
  publicVrAttempt,
  publicVrCompetition,
  requireEnv,
  requireTeacher,
  selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const url = new URL(request.url);
    const filters = getFilters(url);
    const [students, attempts, competitions] = await Promise.all([
      getStudents(env),
      selectRows(env, "vr_simulation_attempts", "select=*&order=completed_at.desc"),
      selectRows(env, "vr_competitions", "select=*&order=created_at.desc")
    ]);
    const result = buildLeaderboard(students, attempts, competitions, filters, false);
    return json(result);
  } catch (error) {
    return fail(error.message || "Unable to load leaderboard.", error.status || 500);
  }
}

function getFilters(url) {
  return {
    grade: url.searchParams.get("grade") || "",
    section: url.searchParams.get("section") || "",
    simulation: url.searchParams.get("simulation") || "",
    competitionId: url.searchParams.get("competition_id") || "",
    quarterId: url.searchParams.get("quarter_id") || "",
    search: (url.searchParams.get("search") || "").toLowerCase()
  };
}

function buildLeaderboard(students, attempts, competitions, filters) {
  const approvedStudents = students
    .filter((student) => student.role === "student" && student.status === "approved")
    .filter((student) => !filters.grade || student.grade_level === filters.grade)
    .filter((student) => !filters.section || student.section === filters.section)
    .filter((student) => !filters.search || String(student.full_name || student.username || "").toLowerCase().includes(filters.search));
  const studentById = new Map(approvedStudents.map((student) => [student.id, student]));
  const competitionById = new Map(competitions.map((competition) => [competition.id, competition]));
  const visibleAttempts = attempts
    .filter((attempt) => studentById.has(attempt.student_id))
    .filter((attempt) => ["completed", "qualified"].includes(attempt.status || "completed"))
    .filter((attempt) => !filters.simulation || attempt.simulation_type === filters.simulation)
    .filter((attempt) => !filters.competitionId || attempt.competition_id === filters.competitionId)
    .filter((attempt) => !filters.quarterId || competitionById.get(attempt.competition_id)?.quarter_id === filters.quarterId);
  const attemptCounts = new Map();
  visibleAttempts.forEach((attempt) => {
    const key = `${attempt.student_id}|${attempt.competition_id || "open"}|${attempt.simulation_type}`;
    attemptCounts.set(key, (attemptCounts.get(key) || 0) + 1);
  });
  const bestByKey = new Map();
  visibleAttempts.forEach((attempt) => {
    const key = `${attempt.student_id}|${attempt.competition_id || "open"}|${attempt.simulation_type}`;
    const existing = bestByKey.get(key);
    if (!existing || compareAttempts(attempt, existing) < 0) bestByKey.set(key, attempt);
  });
  const rows = [...bestByKey.values()]
    .sort(compareAttempts)
    .map((attempt, index) => {
      const student = studentById.get(attempt.student_id);
      const competition = competitionById.get(attempt.competition_id);
      const key = `${attempt.student_id}|${attempt.competition_id || "open"}|${attempt.simulation_type}`;
      return {
        rank: index + 1,
        student: publicProfile(student),
        attempt: publicVrAttempt(attempt),
        competition: competition ? publicVrCompetition(competition) : null,
        attempts_count: attemptCounts.get(key) || 1
      };
    });
  return {
    rows,
    competitions: competitions.map(publicVrCompetition),
    summary: {
      top_score: rows[0]?.attempt.score_percent || 0,
      fastest_time: rows.length ? Math.min(...rows.map((row) => row.attempt.duration_seconds)) : 0,
      active_competitions: competitions.filter((competition) => competition.status === "active").length,
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
