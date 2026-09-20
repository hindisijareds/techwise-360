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
      getStudents(env).catch(() => []),
      selectRows(env, "vr_simulation_attempts", "select=*&order=completed_at.desc").catch(() => []),
      selectRows(env, "vr_competitions", "select=*&order=created_at.desc").catch(() => [])
    ]);

    const result = buildStudentLeaderboard(students, attempts, competitions, profile);
    return json(result);
  } catch (error) {
    return fail(error.message || "Unable to load leaderboard.", error.status || 500);
  }
}

function buildStudentLeaderboard(students, attempts, competitions, profile) {
  // Always include the current student
  const studentById = new Map();
  studentById.set(profile.id, profile);
  for (const s of students) {
    if (s && s.id) studentById.set(s.id, s);
  }

  const targetGrade = String(profile.grade_level || "").toLowerCase().trim();
  const targetSection = String(profile.section || "").toLowerCase().trim();
  const targetSectionId = profile.section_id || null;

  const classmateIds = new Set();
  classmateIds.add(profile.id);

  for (const s of students) {
    if (!s || s.role !== "student" || s.status !== "approved") continue;
    const sGrade = String(s.grade_level || "").toLowerCase().trim();
    const sSection = String(s.section || "").toLowerCase().trim();
    const sSectionId = s.section_id || null;

    const gradeMatches = !targetGrade || !sGrade || sGrade === targetGrade;
    const sectionMatches = (targetSectionId && sSectionId && targetSectionId === sSectionId)
      || (targetSection && sSection && targetSection === sSection)
      || (!targetSection && !targetSectionId);

    if (gradeMatches && sectionMatches) {
      classmateIds.add(s.id);
    }
  }

  // If no other classmates found in exact section, include classmates in same grade level so leaderboard is active
  if (classmateIds.size <= 1 && targetGrade) {
    for (const s of students) {
      if (!s || s.role !== "student" || s.status !== "approved") continue;
      const sGrade = String(s.grade_level || "").toLowerCase().trim();
      if (sGrade === targetGrade) {
        classmateIds.add(s.id);
      }
    }
  }

  const visibleCompetitions = competitions.filter((competition) =>
    ["active", "closed"].includes(competition.status)
    && (!competition.grade_level || !targetGrade || String(competition.grade_level).toLowerCase().trim() === targetGrade)
    && (!competition.section || !targetSection || String(competition.section).toLowerCase().trim() === targetSection)
  );
  const visibleCompetitionIds = new Set(visibleCompetitions.map((competition) => competition.id));

  // Attempts: include classmates and ALWAYS include current student's own attempts
  const visibleAttempts = attempts
    .filter((attempt) => attempt.student_id === profile.id || classmateIds.has(attempt.student_id))
    .filter((attempt) => ["completed", "qualified"].includes(attempt.status || "completed"))
    .filter((attempt) => !attempt.competition_id || visibleCompetitionIds.has(attempt.competition_id) || attempt.student_id === profile.id);

  const bestByKey = new Map();
  visibleAttempts.forEach((attempt) => {
    // Each student is ranked by their single best performance on the class leaderboard
    const key = attempt.student_id;
    const existing = bestByKey.get(key);
    if (!existing || compareAttempts(attempt, existing) < 0) bestByKey.set(key, attempt);
  });

  const competitionById = new Map(competitions.map((competition) => [competition.id, competition]));
  const rows = [...bestByKey.values()].sort(compareAttempts).map((attempt, index) => {
    const matchedStudent = studentById.get(attempt.student_id) || {
      id: attempt.student_id,
      full_name: attempt.student_name || "Student",
      username: "student",
      grade_level: attempt.grade_level || profile.grade_level || "",
      section: attempt.section_name || profile.section || ""
    };
    return {
      rank: index + 1,
      student: publicProfile(matchedStudent),
      attempt: publicVrAttempt(attempt),
      competition: attempt.competition_id ? publicVrCompetition(competitionById.get(attempt.competition_id)) : null,
      is_current_student: attempt.student_id === profile.id
    };
  });

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
  // 1. Highest score always takes top priority
  const scoreDiff = Number(b.score_percent || 0) - Number(a.score_percent || 0);
  if (scoreDiff) return scoreDiff;

  // 2. Tie-breaker: realistic complete run (>= 20s) beats early aborted test clicks (< 20s)
  const aRealistic = Number(a.duration_seconds || 0) >= 20;
  const bRealistic = Number(b.duration_seconds || 0) >= 20;
  if (aRealistic && !bRealistic) return -1;
  if (!aRealistic && bRealistic) return 1;

  // 3. Tie-breaker: faster duration wins among positive times
  const aDur = Number(a.duration_seconds || 0);
  const bDur = Number(b.duration_seconds || 0);
  if (aDur > 0 && bDur > 0 && aDur !== bDur) return aDur - bDur;
  if (aDur > 0 && bDur <= 0) return -1;
  if (bDur > 0 && aDur <= 0) return 1;

  // 4. Tie-breaker: fewer mistakes
  const mistakeDiff = Number(a.mistakes || 0) - Number(b.mistakes || 0);
  if (mistakeDiff) return mistakeDiff;

  // 5. Tie-breaker: most recent completion
  return new Date(b.completed_at || 0) - new Date(a.completed_at || 0);
}
