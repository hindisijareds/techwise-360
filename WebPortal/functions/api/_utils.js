import { academicRpc } from './_academic.js';
const REQUIRED_ENV = ["SUPABASE_URL", "SUPABASE_ANON_KEY", "SUPABASE_SERVICE_ROLE_KEY"];

const VALID_GRADES = ["Grade 9", "Grade 10"];
const VALID_CLASS_SECTION_STATUS = ["draft", "active", "archived"];
const VALID_QUARTERS = ["T1", "T2", "T3"];
const VALID_PROFILE_STATUS = ["pending", "approved", "rejected", "inactive"];
const VALID_CONTENT_STATUS = ["draft", "published", "archived"];
const VALID_LESSON_TYPES = ["lesson", "practice", "assessment"];
const VALID_SECTION_TYPES = [
  "paragraph",
  "key_points",
  "example",
  "quick_tip",
  "resource",
  "steps",
  "safety_note",
  "vocabulary",
  "summary",
  "reflection"
];
const VALID_QUESTION_TYPES = ["multiple_choice", "true_false", "short_answer", "ordering", "identification"];
const VALID_BADGE_COLORS = ["blue", "green", "gold", "purple", "teal", "orange", "red"];
const VALID_NOTIFICATION_EVENTS = [
  "student_registered",
  "lesson_completed",
  "practice_submitted",
  "assessment_submitted",
  "badge_awarded",
  "certificate_awarded"
];
const VALID_STUDENT_NOTIFICATION_EVENTS = [
  "account_approved",
  "lesson_published",
  "lesson_updated",
  "assessment_published",
  "assessment_updated",
  "badge_awarded",
  "certificate_awarded"
];
const VALID_TEACHER_VIEWS = ["overview", "students", "sections", "content", "assessments", "reports", "evaluation"];
const VALID_REPORT_RANGES = ["week", "month"];
const VALID_REPORT_FORMATS = ["pdf", "csv"];
export const EVALUATION_SURVEY_CATEGORIES = [
  {
    key: "functional_suitability",
    label: "Learning Tools",
    items: [
      { key: "functional_suitability_1", prompt: "TechWise 360 has the tools I need for ICT lessons." },
      { key: "functional_suitability_2", prompt: "The lessons, checks, and dashboards match what we need to learn." },
      { key: "functional_suitability_3", prompt: "The website helps me finish ICT learning tasks correctly." }
    ]
  },
  {
    key: "usability",
    label: "Ease of Use",
    items: [
      { key: "usability_1", prompt: "The website is easy to understand and move around." },
      { key: "usability_2", prompt: "The instructions and controls are clear." },
      { key: "usability_3", prompt: "The layout helps me focus on learning tasks." }
    ]
  },
  {
    key: "reliability",
    label: "Saving and Stability",
    items: [
      { key: "reliability_1", prompt: "The website works consistently during use." },
      { key: "reliability_2", prompt: "Learning progress and submissions are saved properly." },
      { key: "reliability_3", prompt: "The website remains usable without unexpected errors." }
    ]
  },
  {
    key: "performance_efficiency",
    label: "Speed",
    items: [
      { key: "performance_efficiency_1", prompt: "Pages and learning materials load in a reasonable time." },
      { key: "performance_efficiency_2", prompt: "The website responds quickly to user actions." },
      { key: "performance_efficiency_3", prompt: "The dashboard presents information without unnecessary delay." }
    ]
  },
  {
    key: "portability",
    label: "Device Use",
    items: [
      { key: "portability_1", prompt: "The website can be used on available school devices." },
      { key: "portability_2", prompt: "The interface adjusts well to different screen sizes." },
      { key: "portability_3", prompt: "The website supports practical use in the school environment." }
    ]
  }
];
export const EVALUATION_SURVEY_ITEMS = EVALUATION_SURVEY_CATEGORIES.flatMap((category) =>
  category.items.map((item) => ({ ...item, category_key: category.key, category_label: category.label }))
);
const DEFAULT_NOTIFICATION_PREFERENCES = {
  student_accounts: true,
  lesson_completions: true,
  practice_submissions: true,
  achievement_awards: true
};
const LESSON_FILE_BUCKET = "lesson-files";
const MAX_LESSON_FILE_BYTES = 104857600;
const VALID_LESSON_FILE_TYPES = [
  "application/pdf",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  "video/mp4"
];
const STUDENT_AVATAR_BUCKET = "student-avatars";
const MAX_STUDENT_AVATAR_BYTES = 2097152;
const VALID_STUDENT_AVATAR_TYPES = ["image/jpeg", "image/png", "image/webp"];

export function optionsResponse() {
  return new Response(null, {
    status: 204,
    headers: corsHeaders()
  });
}

export function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...corsHeaders()
    }
  });
}

export function fail(message, status = 400) {
  return json({ error: message }, status);
}

export async function readJson(request) {
  try {
    return await request.json();
  } catch {
    throw new ApiError("Invalid JSON body.", 400);
  }
}

export function requireEnv(env) {
  const missing = REQUIRED_ENV.filter((key) => !env[key]);
  if (missing.length > 0) {
    throw new ApiError(`Missing environment variable: ${missing.join(", ")}`, 500);
  }
}

export async function registerAuthUser(env, payload) {
  const response = await supabaseFetch(env, "/auth/v1/admin/users", {
    admin: true,
    method: "POST",
    body: {
      email: payload.email,
      password: payload.password,
      email_confirm: true,
      user_metadata: {
        role: "student",
        username: payload.username,
        full_name: payload.full_name
      }
    }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to create auth user.", response.status);
  }

  return response.json();
}

export async function deleteAuthUser(env, userId) {
  await supabaseFetch(env, `/auth/v1/admin/users/${encodeURIComponent(userId)}`, {
    admin: true,
    method: "DELETE"
  });
}

export async function updateAuthUserPassword(env, userId, password) {
  const response = await supabaseFetch(env, `/auth/v1/admin/users/${encodeURIComponent(userId)}`, {
    admin: true,
    method: "PUT",
    body: { password }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to update password.", response.status);
  }

  return response.json();
}

export async function requestPasswordResetEmail(env, email, redirectTo) {
  const normalized = normalizeEmail(email);
  if (!normalized || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalized)) {
    throw new ApiError("Please enter a valid email address.", 400);
  }

  const response = await supabaseFetch(env, `/auth/v1/recover?redirect_to=${encodeURIComponent(redirectTo)}`, {
    method: "POST",
    body: {
      email: normalized
    }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to send password reset email.", response.status);
  }

  return { ok: true };
}

export async function updateAuthenticatedPassword(env, token, password) {
  if (!isStrongPassword(password)) {
    throw new ApiError("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.", 400);
  }

  const response = await supabaseFetch(env, "/auth/v1/user", {
    token,
    method: "PUT",
    body: { password }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to update password.", response.status);
  }

  return response.json();
}

export async function signInWithPassword(env, email, password) {
  const response = await supabaseFetch(env, "/auth/v1/token?grant_type=password", {
    method: "POST",
    body: { email, password }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Invalid email/username or password.", 401);
  }

  return response.json();
}

export async function refreshAuthSession(env, refreshToken) {
  const token = String(refreshToken || "").trim();
  if (!token) {
    throw new ApiError("Refresh token is required.", 400);
  }

  const response = await supabaseFetch(env, "/auth/v1/token?grant_type=refresh_token", {
    method: "POST",
    body: { refresh_token: token }
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Session expired. Please log in again.", 401);
  }

  return response.json();
}

export async function getAuthUser(env, token) {
  const response = await supabaseFetch(env, "/auth/v1/user", {
    token
  });

  if (!response.ok) {
    throw new ApiError("Session expired. Please log in again.", 401);
  }

  return response.json();
}

export async function insertProfile(env, profile) {
  return insertRow(env, "profiles", profile, "Unable to save student profile.");
}

export async function findProfileByUsername(env, username) {
  const normalized = normalizeUsername(username);
  const rows = await selectRows(env, "profiles", `username=eq.${encodeURIComponent(normalized)}&select=*`);
  return rows[0] || null;
}

export async function findProfileByEmail(env, email) {
  const normalized = normalizeEmail(email);
  const rows = await selectRows(env, "profiles", `email=eq.${encodeURIComponent(normalized)}&select=*`);
  return rows[0] || null;
}

export async function getProfileById(env, id) {
  const rows = await selectRows(env, "profiles", `id=eq.${encodeURIComponent(id)}&select=*`);
  return rows[0] || null;
}

export async function getPendingStudents(env) {
  return selectRows(env, "profiles", "role=eq.student&status=eq.pending&select=*&order=created_at.asc");
}

export async function getStudents(env) {
  return selectRows(env, "profiles", "role=eq.student&select=*&order=created_at.desc");
}

export async function getApprovedTeachers(env) {
  return selectRows(env, "profiles", "role=eq.teacher&status=eq.approved&select=id,full_name,username");
}

export async function updateTeacherProfile(env, teacherId, body) {
  const patch = cleanTeacherProfilePatch(body);
  validateTeacherProfilePatch(patch);
  const rows = await patchRows(env, "profiles", `id=eq.${encodeURIComponent(teacherId)}&role=eq.teacher`, patch, "Unable to update teacher profile.");
  if (!rows[0]) throw new ApiError("Teacher profile was not found.", 404);
  return rows[0];
}

export async function getTeacherSettings(env, teacherId) {
  try {
    const rows = await selectRows(env, "teacher_settings", `teacher_id=eq.${encodeURIComponent(teacherId)}&select=*&limit=1`);
    return normalizeTeacherSettings(rows[0], teacherId);
  } catch (error) {
    if (isMissingOptionalTableError(error, "teacher_settings")) return normalizeTeacherSettings(null, teacherId);
    throw error;
  }
}

export async function saveTeacherSettings(env, teacherId, body) {
  const payload = cleanTeacherSettingsPayload(body);
  validateTeacherSettingsPayload(payload);
  try {
    const existing = await selectRows(env, "teacher_settings", `teacher_id=eq.${encodeURIComponent(teacherId)}&select=*&limit=1`);
    if (existing[0]) {
      const rows = await patchRows(
        env,
        "teacher_settings",
        `teacher_id=eq.${encodeURIComponent(teacherId)}`,
        { ...payload, updated_at: new Date().toISOString() },
        "Unable to update teacher settings."
      );
      return normalizeTeacherSettings(rows[0], teacherId);
    }
    const row = await insertRow(env, "teacher_settings", {
      teacher_id: teacherId,
      ...payload
    }, "Unable to save teacher settings.");
    return normalizeTeacherSettings(row, teacherId);
  } catch (error) {
    if (isMissingOptionalTableError(error, "teacher_settings")) return normalizeTeacherSettings(null, teacherId);
    throw error;
  }
}

export async function changeTeacherPassword(env, teacher, body) {
  const currentPassword = String(body.current_password || "");
  const newPassword = String(body.new_password || "");
  const confirmPassword = String(body.confirm_password || "");
  if (!currentPassword || !newPassword || !confirmPassword) {
    throw new ApiError("Please complete all password fields.", 400);
  }
  if (newPassword !== confirmPassword) {
    throw new ApiError("New password and confirmation must match.", 400);
  }
  if (!isStrongPassword(newPassword)) {
    throw new ApiError("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.", 400);
  }
  await signInWithPassword(env, teacher.email, currentPassword);
  await updateAuthUserPassword(env, teacher.id, newPassword);
}

export async function updateStudentProfile(env, body, teacherId = null) {
  const studentId = String(body.id || body.student_id || "").trim();
  if (!studentId) throw new ApiError("Student id is required.", 400);
  const currentStudent = await getProfileById(env, studentId);
  if (!currentStudent || currentStudent.role !== "student") throw new ApiError("Student account was not found.", 404);

  const patch = cleanStudentManagementPatch(body);
  const requestedSectionId = Object.prototype.hasOwnProperty.call(body, "section_id")
    ? String(body.section_id || "").trim()
    : "";
  let section = null;
  if (requestedSectionId) {
    section = await getClassSectionById(env, requestedSectionId);
    if (!section || section.status !== "active") {
      throw new ApiError("Please select an active managed section.", 400);
    }
    patch.section_id = section.id;
    patch.grade_level = section.grade_level;
    patch.section = section.name;
    patch.adviser = section.adviser_name;
  } else if (["grade_level", "section", "adviser"].some((field) => Object.prototype.hasOwnProperty.call(body, field))) {
    const classChanged = (body.grade_level !== undefined && cleanText(body.grade_level) !== cleanText(currentStudent.grade_level))
      || (body.section !== undefined && cleanText(body.section) !== cleanText(currentStudent.section))
      || (body.adviser !== undefined && cleanText(body.adviser) !== cleanText(currentStudent.adviser));
    if (classChanged) throw new ApiError("Select a managed section when changing a student's class.", 400);
    delete patch.grade_level;
    delete patch.section;
    delete patch.adviser;
  }
  validateStudentManagementPatch(patch);
  if (section) await syncStudentSectionAssignment(env, currentStudent, section, teacherId);

  const rows = await patchRows(env, "profiles", `id=eq.${encodeURIComponent(studentId)}&role=eq.student`, patch, "Unable to update student.");
  if (!rows[0]) throw new ApiError("Student account was not found.", 404);
  return rows[0];
}

export async function updateStudentSelfProfile(env, studentId, body) {
  const patch = cleanStudentSelfPatch(body);
  validateStudentSelfPatch(patch);
  const rows = await patchRows(env, "profiles", `id=eq.${encodeURIComponent(studentId)}&role=eq.student`, patch, "Unable to update profile.");
  if (!rows[0]) throw new ApiError("Student profile was not found.", 404);
  return rows[0];
}

export async function setStudentAvatarPath(env, studentId, avatarPath) {
  const rows = await patchRows(env, "profiles", `id=eq.${encodeURIComponent(studentId)}&role=eq.student`, {
    avatar_path: avatarPath || null,
    updated_at: new Date().toISOString()
  }, "Unable to update profile picture.");
  if (!rows[0]) throw new ApiError("Student profile was not found.", 404);
  return rows[0];
}

export async function updateStudentStatus(env, studentId, status) {
  if (!VALID_PROFILE_STATUS.includes(status)) {
    throw new ApiError("Student status is invalid.", 400);
  }

  const rows = await patchRows(env, "profiles", `id=eq.${encodeURIComponent(studentId)}&role=eq.student`, {
    status,
    updated_at: new Date().toISOString()
  }, "Unable to update student status.");

  if (!rows[0]) {
    throw new ApiError("Student account was not found.", 404);
  }
  return rows[0];
}

export async function recordApprovalEvent(env, event) {
  await insertRow(env, "approval_events", event, "Unable to record approval event.", false);
}

export async function getQuarters(env) {
  return selectRows(env, "quarters", "select=*&order=school_year.desc,name.asc");
}

export async function getActiveQuarter(env) {
  const rows = await selectRows(env, "quarters", "is_active=eq.true&status=neq.archived&select=*&limit=1");
  return rows[0] || null;
}

export async function getClassSectionById(env, sectionId) {
  const rows = await selectRows(env, "class_sections", `id=eq.${encodeURIComponent(sectionId)}&select=*&limit=1`);
  return rows[0] || null;
}

export async function getRegistrationSections(env) {
  const activeQuarter = await getActiveQuarter(env);
  if (!activeQuarter) return { activeQuarter: null, sections: [] };
  const sections = await selectRows(
    env,
    "class_sections",
    `school_year=eq.${encodeURIComponent(activeQuarter.school_year)}&status=eq.active&select=*&order=grade_level.asc,name.asc`
  );
  return { activeQuarter, sections };
}

export function publicClassSection(section) {
  return {
    id: section.id,
    school_year: section.school_year,
    grade_level: section.grade_level,
    name: section.name,
    code: section.code,
    adviser_name: section.adviser_name,
    room: section.room || "",
    capacity: Number(section.capacity || 0),
    status: section.status,
    assigned_count: Number(section.assigned_count || 0),
    available_seats: Number(section.available_seats || 0),
    roster: section.roster || [],
    created_at: section.created_at,
    updated_at: section.updated_at
  };
}

export async function getTeacherSectionsDashboard(env, schoolYear = "") {
  const [activeQuarter, allSections, assignments, students, activities, actors, years] = await Promise.all([
    getActiveQuarter(env),
    selectRows(env, "class_sections", "select=*&order=school_year.desc,grade_level.asc,name.asc"),
    selectRows(env, "student_section_assignments", "ended_at=is.null&select=*&order=assigned_at.desc"),
    selectRows(env, "profiles", "role=eq.student&select=id,full_name,username,email,status,grade_level,section,adviser,section_id,student_number,created_at&order=full_name.asc"),
    selectRows(env, "section_activity", "select=*&order=created_at.desc&limit=50"),
    selectRows(env, "profiles", "select=id,full_name,username,role"),
    selectRows(env, "academic_years", "select=name&order=name.desc")
  ]);
  const selectedYear = schoolYear || activeQuarter?.school_year || allSections[0]?.school_year || "";
  const assignmentByStudent = new Map(assignments.map((item) => [item.student_id, item]));
  const studentById = new Map(students.map((student) => [student.id, student]));
  const actorById = new Map(actors.map((actor) => [actor.id, actor]));
  const seatStatuses = new Set(["pending", "approved"]);
  const sections = allSections.map((section) => {
    const sectionAssignments = assignments.filter((item) => item.section_id === section.id);
    const roster = sectionAssignments.map((assignment) => {
      const student = studentById.get(assignment.student_id);
      return student ? { ...publicProfile(student), assignment_id: assignment.id, assigned_at: assignment.assigned_at } : null;
    }).filter(Boolean);
    const assignedCount = roster.filter((student) => seatStatuses.has(student.status)).length;
    return publicClassSection({
      ...section,
      assigned_count: assignedCount,
      available_seats: Math.max(0, Number(section.capacity || 0) - assignedCount),
      roster
    });
  });
  const yearSections = sections.filter((section) => !selectedYear || section.school_year === selectedYear);
  const activeSections = yearSections.filter((section) => section.status === "active");
  const assignedStudents = activeSections.reduce((sum, section) => sum + section.assigned_count, 0);
  const availableSeats = activeSections.reduce((sum, section) => sum + section.available_seats, 0);
  const recentActivity = activities.map((activity) => {
    const section = allSections.find((item) => item.id === activity.section_id);
    const actor = actorById.get(activity.actor_id);
    const student = studentById.get(activity.student_id);
    return {
      ...activity,
      section_name: section?.name || "Section",
      grade_level: section?.grade_level || "",
      actor_name: actor?.full_name || actor?.username || "System",
      student_name: student?.full_name || student?.username || ""
    };
  });
  return {
    active_quarter: activeQuarter ? publicQuarter(activeQuarter) : null,
    selected_school_year: selectedYear,
    school_years: [...new Set([...years.map(y => y.name), ...allSections.map(section => section.school_year)].filter(Boolean))],
    sections,
    summary: {
      total_sections: yearSections.length,
      active_sections: activeSections.length,
      assigned_students: assignedStudents,
      available_seats: availableSeats
    },
    adviser_suggestions: [...new Set(allSections.map((section) => section.adviser_name).filter(Boolean))].sort(),
    recent_activity: recentActivity,
    unassigned_students: students
      .filter((student) => seatStatuses.has(student.status) && !assignmentByStudent.has(student.id))
      .map(publicProfile)
  };
}

export async function createClassSection(env, body, teacherId) {
  const payload = cleanClassSectionPayload(body, false);
  validateClassSectionPayload(payload, false);
  let section;
  try {
    section = await insertRow(env, "class_sections", {
      ...payload,
      status: "draft",
      created_by: teacherId
    }, "Unable to create section.");
  } catch (error) {
    if (error.status === 409) throw new ApiError("That section name or code is already used in this school year.", 409);
    throw error;
  }
  await recordSectionActivity(env, section.id, teacherId, "created", { name: section.name, code: section.code });
  return publicClassSection(section);
}

export async function updateClassSection(env, body, teacherId) {
  const sectionId = String(body.id || body.section_id || "").trim();
  if (!sectionId) throw new ApiError("Section id is required.", 400);
  const existing = await getClassSectionById(env, sectionId);
  if (!existing) throw new ApiError("Section was not found.", 404);
  const patch = cleanClassSectionPayload(body, true);
  validateClassSectionPayload({ ...existing, ...patch }, false);
  const currentAssignments = await selectRows(env, "student_section_assignments", `section_id=eq.${encodeURIComponent(sectionId)}&ended_at=is.null&select=id,student_id`);
  if (patch.grade_level && patch.grade_level !== existing.grade_level && currentAssignments.length) {
    throw new ApiError("Transfer assigned students before changing this section's grade.", 409);
  }
  if (patch.status === "archived" && currentAssignments.length) {
    throw new ApiError("Transfer all assigned students before archiving this section.", 409);
  }
  const nextStatus = patch.status || existing.status;
  if (nextStatus === "active") validateClassSectionPayload({ ...existing, ...patch }, true);
  let rows;
  try {
    rows = await patchRows(env, "class_sections", `id=eq.${encodeURIComponent(sectionId)}`, {
      ...patch,
      updated_at: new Date().toISOString()
    }, "Unable to update section.");
  } catch (error) {
    if (error.status === 409) throw new ApiError("That section name or code is already used in this school year.", 409);
    throw error;
  }
  const section = rows[0];
  if (!section) throw new ApiError("Section was not found.", 404);
  const profilePatch = {};
  if (section.name !== existing.name) profilePatch.section = section.name;
  if (section.adviser_name !== existing.adviser_name) profilePatch.adviser = section.adviser_name;
  if (Object.keys(profilePatch).length) {
    await patchRows(env, "profiles", `section_id=eq.${encodeURIComponent(section.id)}&role=eq.student`, {
      ...profilePatch,
      updated_at: new Date().toISOString()
    }, "Unable to synchronize student section details.", false);
  }
  const action = section.status !== existing.status
    ? section.status === "active" ? "activated" : section.status === "draft" ? "drafted" : "archived"
    : "updated";
  await recordSectionActivity(env, section.id, teacherId, action, sectionChangeDetails(existing, section));
  return publicClassSection(section);
}

export async function assignStudentToSection(env, body, teacherId) {
  const studentId = String(body.student_id || "").trim();
  const sectionId = String(body.section_id || "").trim();
  if (!studentId || !sectionId) throw new ApiError("Student and section are required.", 400);
  const [student, section] = await Promise.all([getProfileById(env, studentId), getClassSectionById(env, sectionId)]);
  if (!student || student.role !== "student") throw new ApiError("Student was not found.", 404);
  if (!section || section.status !== "active") throw new ApiError("Students can only be assigned to active sections.", 400);
  const assignment = await syncStudentSectionAssignment(env, student, section, teacherId);
  const refreshed = await getProfileById(env, student.id);
  const dashboard = await getTeacherSectionsDashboard(env, section.school_year);
  const updatedSection = dashboard.sections.find((item) => item.id === section.id);
  return {
    student: publicProfile(refreshed),
    assignment,
    warning: updatedSection && updatedSection.assigned_count > updatedSection.capacity
      ? `${updatedSection.name} is over capacity (${updatedSection.assigned_count}/${updatedSection.capacity}).`
      : updatedSection && updatedSection.assigned_count === updatedSection.capacity
        ? `${updatedSection.name} is now full.`
        : ""
  };
}

async function syncStudentSectionAssignment(env, student, section, teacherId) {
  const term = await getActiveQuarter(env);
  if (!term || term.school_year !== section.school_year) throw new ApiError('Select a section in the active academic year and term, or use Enrollment Management for another term.',409);
  try {
    await academicRpc(env, 'enroll_student', {
      p_student: student.id,
      p_year: term.academic_year_id || null,
      p_term: term.id,
      p_section: section.id,
      p_grade: section.grade_level,
      p_teacher: teacherId || null
    });
  } catch (err) {
    console.error("academicRpc enroll_student notice:", err.message || err);
    if (err.status === 409 && !err.message?.includes('already enrolled')) {
      throw err;
    }
  }
  const currentRows = await selectRows(env, "student_section_assignments", `student_id=eq.${encodeURIComponent(student.id)}&ended_at=is.null&select=*&limit=1`);
  const current = currentRows[0] || null;
  if (current?.section_id === section.id) {
    await patchRows(env, "profiles", `id=eq.${encodeURIComponent(student.id)}`, {
      section_id: section.id,
      grade_level: section.grade_level,
      section: section.name,
      adviser: section.adviser_name,
      updated_at: new Date().toISOString()
    }, "Unable to synchronize student section.", false);
    return current;
  }
  const changedAt = new Date().toISOString();
  if (current) {
    await patchRows(env, "student_section_assignments", `id=eq.${encodeURIComponent(current.id)}`, { ended_at: changedAt }, "Unable to close the previous assignment.", false);
  }
  let assignment;
  try {
    assignment = await insertRow(env, "student_section_assignments", {
      student_id: student.id,
      section_id: section.id,
      assigned_by: teacherId || null,
      assigned_at: changedAt
    }, "Unable to assign the student.");
  } catch (error) {
    if (current) await patchRows(env, "student_section_assignments", `id=eq.${encodeURIComponent(current.id)}`, { ended_at: null }, "Unable to restore the previous assignment.", false).catch(() => {});
    throw error;
  }
  try {
    await patchRows(env, "profiles", `id=eq.${encodeURIComponent(student.id)}`, {
      section_id: section.id,
      grade_level: section.grade_level,
      section: section.name,
      adviser: section.adviser_name,
      updated_at: changedAt
    }, "Unable to update the student's current section.", false);
  } catch (error) {
    await deleteRows(env, "student_section_assignments", `id=eq.${encodeURIComponent(assignment.id)}`, "Unable to roll back the assignment.").catch(() => {});
    if (current) await patchRows(env, "student_section_assignments", `id=eq.${encodeURIComponent(current.id)}`, { ended_at: null }, "Unable to restore the previous assignment.", false).catch(() => {});
    throw error;
  }
  await recordSectionActivity(env, section.id, teacherId, current ? "student_transferred" : "student_assigned", {
    from_section_id: current?.section_id || null,
    student_name: student.full_name || student.username
  }, student.id);
  return assignment;
}

async function recordSectionActivity(env, sectionId, actorId, action, details = {}, studentId = null) {
  return insertRow(env, "section_activity", {
    section_id: sectionId,
    student_id: studentId,
    actor_id: actorId || null,
    action,
    details
  }, "Unable to record section activity.", false);
}

function cleanClassSectionPayload(body, partial) {
  const numberValue = body.capacity !== undefined && body.capacity !== "" ? Number(body.capacity) : undefined;
  return cleanPatch({
    school_year: body.school_year !== undefined ? cleanText(body.school_year) : partial ? undefined : "",
    grade_level: body.grade_level !== undefined ? cleanText(body.grade_level) : partial ? undefined : "",
    name: body.name !== undefined ? cleanText(body.name) : partial ? undefined : "",
    code: body.code !== undefined ? cleanText(body.code).toUpperCase() : partial ? undefined : "",
    adviser_name: body.adviser_name !== undefined ? cleanText(body.adviser_name) : partial ? undefined : "",
    room: body.room !== undefined ? cleanText(body.room) || null : undefined,
    capacity: numberValue,
    status: body.status !== undefined ? cleanText(body.status).toLowerCase() : undefined
  });
}

function validateClassSectionPayload(payload, activating) {
  if (!/^\d{4}-\d{4}$/.test(payload.school_year || "")) throw new ApiError("School year must use the format 2026-2027.", 400);
  if (!VALID_GRADES.includes(payload.grade_level)) throw new ApiError("Grade level must be Grade 9 or Grade 10.", 400);
  if (!payload.name || payload.name.length > 80) throw new ApiError("Section name is required and must be 80 characters or fewer.", 400);
  if (!payload.code || payload.code.length > 40) throw new ApiError("Section code is required and must be 40 characters or fewer.", 400);
  if (!payload.adviser_name || payload.adviser_name.length > 120) throw new ApiError("Adviser name is required and must be 120 characters or fewer.", 400);
  if (!Number.isInteger(Number(payload.capacity)) || Number(payload.capacity) <= 0 || Number(payload.capacity) > 500) {
    throw new ApiError("Capacity must be a whole number between 1 and 500.", 400);
  }
  if (payload.status && !VALID_CLASS_SECTION_STATUS.includes(payload.status)) throw new ApiError("Section status is invalid.", 400);
  if (activating && [payload.name, payload.code, payload.adviser_name, payload.grade_level].some((value) => !value)) {
    throw new ApiError("Name, code, adviser, grade, and capacity are required before activation.", 400);
  }
}

function sectionChangeDetails(previous, current) {
  const fields = ["school_year", "grade_level", "name", "code", "adviser_name", "room", "capacity", "status"];
  return Object.fromEntries(fields.filter((field) => previous[field] !== current[field]).map((field) => [field, {
    from: previous[field] ?? null,
    to: current[field] ?? null
  }]));
}

export async function createQuarter(env, body, teacherId) {
  const payload = cleanQuarterPayload(body);
  validateQuarterPayload(payload);
  return academicRpc(env, 'save_academic_term', {p_body:payload,p_teacher:teacherId});
}
export async function updateQuarter(env, body) {
  if (!body.id) throw new ApiError('Term id is required.',400);
  return academicRpc(env, 'save_academic_term', {p_body:body,p_teacher:null});
}
export async function deleteQuarter() {
  throw new ApiError('Terms retain historical records. Archive the term instead of deleting it.',409);
}

export async function getModules(env) {
  return selectRows(env, "modules", "select=*&order=sort_order.asc,created_at.desc");
}

export async function createModule(env, body, teacherId) {
  const payload = cleanModulePayload(body);
  validateModulePayload(payload);
  return insertRow(env, "modules", {
    ...payload,
    created_by: teacherId
  }, "Unable to create module.");
}

export async function updateModule(env, body) {
  const id = String(body.id || "").trim();
  if (!id) throw new ApiError("Module id is required.", 400);

  const patch = cleanModulePayload(body, true);
  validateModulePayload(patch, true);

  const rows = await patchRows(env, "modules", `id=eq.${encodeURIComponent(id)}`, patch, "Unable to update module.");
  if (!rows[0]) throw new ApiError("Module was not found.", 404);
  return rows[0];
}

export async function getLessons(env) {
  return selectRows(env, "lessons", "select=*&order=sort_order.asc,created_at.desc");
}

export async function getLessonById(env, lessonId) {
  const rows = await selectRows(env, "lessons", `id=eq.${encodeURIComponent(lessonId)}&select=*`);
  return rows[0] || null;
}

export async function createLesson(env, body, teacherId) {
  const payload = cleanLessonPayload(body);
  validateLessonPayload(payload);
  return insertRow(env, "lessons", {
    ...payload,
    created_by: teacherId
  }, "Unable to create lesson.");
}

export async function updateLesson(env, body) {
  const id = String(body.id || "").trim();
  if (!id) throw new ApiError("Lesson id is required.", 400);

  const patch = cleanLessonPayload(body, true);
  validateLessonPayload(patch, true);

  const rows = await patchRows(env, "lessons", `id=eq.${encodeURIComponent(id)}`, patch, "Unable to update lesson.");
  if (!rows[0]) throw new ApiError("Lesson was not found.", 404);
  return rows[0];
}

export async function getLessonProgress(env) {
  return selectRows(env, "lesson_progress", "select=*");
}

export async function getLessonFiles(env) {
  return selectRows(env, "lesson_files", "select=*&order=created_at.desc");
}

export async function getLessonFileById(env, fileId) {
  const rows = await selectRows(env, "lesson_files", `id=eq.${encodeURIComponent(fileId)}&select=*`);
  return rows[0] || null;
}

export async function getLessonFilesByLesson(env, lessonId) {
  return selectRows(env, "lesson_files", `lesson_id=eq.${encodeURIComponent(lessonId)}&select=*&order=created_at.desc`);
}

export async function saveLessonFile(env, body, teacherId) {
  const payload = cleanLessonFilePayload(body);
  validateLessonFilePayload(payload);
  return insertRow(env, "lesson_files", {
    ...payload,
    uploaded_by: teacherId
  }, "Unable to save lesson file.");
}

export async function updateLessonFile(env, body) {
  const id = String(body.id || body.file_id || "").trim();
  if (!id) throw new ApiError("Lesson file id is required.", 400);
  const patch = cleanPatch({
    lesson_id: Object.prototype.hasOwnProperty.call(body, "lesson_id") ? String(body.lesson_id || "").trim() || null : undefined
  });
  const rows = await patchRows(env, "lesson_files", `id=eq.${encodeURIComponent(id)}`, patch, "Unable to update lesson file.");
  if (!rows[0]) throw new ApiError("Lesson file was not found.", 404);
  return rows[0];
}

export async function getLessonSections(env, lessonId) {
  return selectRows(env, "lesson_sections", `lesson_id=eq.${encodeURIComponent(lessonId)}&select=*&order=sort_order.asc,created_at.asc`);
}

export async function getLessonQuestions(env, lessonId) {
  return selectRows(env, "lesson_questions", `lesson_id=eq.${encodeURIComponent(lessonId)}&select=*&order=sort_order.asc,created_at.asc`);
}

export async function getLessonAttempts(env, lessonId, studentId) {
  return selectRows(
    env,
    "lesson_attempts",
    `lesson_id=eq.${encodeURIComponent(lessonId)}&student_id=eq.${encodeURIComponent(studentId)}&select=*&order=submitted_at.desc`
  );
}

export async function getAllLessonAttempts(env) {
  return selectRows(env, "lesson_attempts", "select=*&order=submitted_at.desc");
}

export async function saveLessonStructure(env, lessonId, body) {
  if (Array.isArray(body.sections)) {
    await deleteRows(env, "lesson_sections", `lesson_id=eq.${encodeURIComponent(lessonId)}`, "Unable to replace lesson content.");
    const sections = cleanLessonSections(body.sections);
    validateLessonSections(sections);
    for (const section of sections) {
      await insertRow(env, "lesson_sections", {
        ...section,
        lesson_id: lessonId
      }, "Unable to save lesson content.", false);
    }
  }

  if (Array.isArray(body.questions)) {
    await deleteRows(env, "lesson_questions", `lesson_id=eq.${encodeURIComponent(lessonId)}`, "Unable to replace lesson practice.");
    const questions = cleanLessonQuestions(body.questions);
    validateLessonQuestions(questions);
    for (const question of questions) {
      await insertRow(env, "lesson_questions", {
        ...question,
        lesson_id: lessonId
      }, "Unable to save lesson practice.", false);
    }
  }
}

export async function createLessonUploadUrl(env, body, teacherId) {
  const payload = cleanLessonUploadPayload(body);
  validateLessonUploadPayload(payload);
  const extension = extensionForMime(payload.mime_type, payload.filename);
  const safeName = normalizeFileName(payload.filename).replace(/\.[^.]+$/, "");
  const storagePath = `${teacherId}/${Date.now()}-${crypto.randomUUID()}-${safeName}${extension}`;
  const response = await storageFetch(env, `/object/upload/sign/${LESSON_FILE_BUCKET}/${encodeStoragePath(storagePath)}`, {
    method: "POST"
  });
  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to create upload URL.", response.status);
  }
  const data = await response.json();
  const signedUrl = absoluteSupabaseUrl(env, data.url || data.signedURL || data.signedUrl);
  return {
    bucket: LESSON_FILE_BUCKET,
    path: storagePath,
    token: data.token || "",
    upload_url: signedUrl
  };
}

export async function createLessonDownloadUrl(env, file) {
  const response = await storageFetch(env, `/object/sign/${LESSON_FILE_BUCKET}/${encodeStoragePath(file.storage_path)}`, {
    method: "POST",
    body: { expiresIn: 300 }
  });
  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to create preview URL.", response.status);
  }
  const data = await response.json();
  return absoluteSupabaseUrl(env, data.signedURL || data.signedUrl || data.url);
}

export async function createStudentAvatarUploadUrl(env, student, body) {
  const payload = cleanAvatarUploadPayload(body);
  validateAvatarUploadPayload(payload);
  const extension = extensionForAvatarMime(payload.mime_type);
  const storagePath = `${student.id}/avatar${extension}`;
  const response = await storageFetch(env, `/object/upload/sign/${STUDENT_AVATAR_BUCKET}/${encodeStoragePath(storagePath)}`, {
    method: "POST",
    body: { upsert: true }
  });
  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to create avatar upload URL.", response.status);
  }
  const data = await response.json();
  const signedUrl = absoluteSupabaseUrl(env, data.url || data.signedURL || data.signedUrl);
  return {
    bucket: STUDENT_AVATAR_BUCKET,
    path: storagePath,
    token: data.token || "",
    upload_url: signedUrl
  };
}

export async function createStudentAvatarUrl(env, avatarPath) {
  if (!avatarPath) return "";
  const response = await storageFetch(env, `/object/sign/${STUDENT_AVATAR_BUCKET}/${encodeStoragePath(avatarPath)}`, {
    method: "POST",
    body: { expiresIn: 300 }
  });
  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to create avatar URL.", response.status);
  }
  const data = await response.json();
  return absoluteSupabaseUrl(env, data.signedURL || data.signedUrl || data.url);
}

export async function removeStudentAvatarObject(env, avatarPath) {
  if (!avatarPath) return;
  const response = await storageFetch(env, `/object/${STUDENT_AVATAR_BUCKET}/${encodeStoragePath(avatarPath)}`, {
    method: "DELETE"
  });
  if (!response.ok && response.status !== 404) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || "Unable to remove profile picture.", response.status);
  }
}

export async function getStudentProgress(env, studentId) {
  return selectRows(env, "lesson_progress", `student_id=eq.${encodeURIComponent(studentId)}&select=*`);
}

export async function saveStudentProgress(env, studentId, lessonId, patch) {
  const existing = await selectRows(
    env,
    "lesson_progress",
    `student_id=eq.${encodeURIComponent(studentId)}&lesson_id=eq.${encodeURIComponent(lessonId)}&select=*`
  );
  const now = new Date().toISOString();
  const payload = cleanPatch({
    status: patch.status,
    progress_percent: patch.progress_percent,
    score_percent: patch.score_percent,
    started_at: patch.started_at,
    completed_at: patch.completed_at,
    updated_at: now
  });

  if (existing[0]) {
    const rows = await patchRows(
      env,
      "lesson_progress",
      `id=eq.${encodeURIComponent(existing[0].id)}`,
      payload,
      "Unable to update lesson progress."
    );
    return rows[0];
  }

  return insertRow(env, "lesson_progress", {
    student_id: studentId,
    lesson_id: lessonId,
    status: payload.status || "in_progress",
    progress_percent: Number.isInteger(payload.progress_percent) ? payload.progress_percent : 10,
    score_percent: payload.score_percent ?? null,
    started_at: payload.started_at || now,
    completed_at: payload.completed_at || null
  }, "Unable to start lesson progress.");
}

export async function submitLessonAttempt(env, studentId, lessonId, questions, body) {
  const answers = normalizeAnswers(body.answers);
  const timeSpent = Number(body.time_spent_seconds || 0);
  if (!Number.isInteger(timeSpent) || timeSpent < 0) {
    throw new ApiError("Time spent is invalid.", 400);
  }
  if (!questions.length) {
    throw new ApiError("This lesson does not have practice questions yet.", 400);
  }

  const feedback = questions.map((question) => {
    const submitted = cleanText(answers[question.id] ?? "");
    const correct = isCorrectAnswer(question, submitted);
    return {
      question_id: question.id,
      prompt: question.prompt,
      submitted_answer: submitted,
      correct_answer: question.correct_answer,
      is_correct: correct,
      points: correct ? question.points : 0,
      max_points: question.points,
      explanation: question.explanation || ""
    };
  });
  const totalPoints = questions.reduce((sum, question) => sum + Number(question.points || 0), 0);
  const earnedPoints = feedback.reduce((sum, item) => sum + item.points, 0);
  const score = totalPoints ? Math.round((earnedPoints / totalPoints) * 100) : 0;
  const correctCount = feedback.filter((item) => item.is_correct).length;

  const attempt = await insertRow(env, "lesson_attempts", {
    student_id: studentId,
    lesson_id: lessonId,
    answers,
    feedback,
    score_percent: score,
    correct_count: correctCount,
    total_points: totalPoints,
    time_spent_seconds: timeSpent
  }, "Unable to submit practice.");

  const now = new Date().toISOString();
  const progress = await saveStudentProgress(env, studentId, lessonId, {
    status: "completed",
    progress_percent: 100,
    score_percent: score,
    started_at: now,
    completed_at: now
  });

  return { attempt, progress, feedback };
}

export async function getStudentBadges(env) {
  return selectRows(env, "student_badges", "select=*&order=awarded_at.desc");
}

export async function getStudentCertificates(env) {
  return selectRows(env, "student_certificates", "select=*&order=awarded_at.desc");
}

export async function saveStudentReward(env, body, teacherId) {
  const payload = cleanRewardPayload(body, teacherId);
  validateRewardPayload(payload);
  const table = payload.reward_type === "certificate" ? "student_certificates" : "student_badges";
  const keyField = payload.reward_type === "certificate" ? "certificate_key" : "badge_key";

  if (payload.action === "remove") {
    await deleteRows(env, table, `student_id=eq.${encodeURIComponent(payload.student_id)}&${keyField}=eq.${encodeURIComponent(payload.reward_key)}`, `Unable to remove ${payload.reward_type}.`);
    return { removed: true };
  }

  return insertRow(env, table, {
    student_id: payload.student_id,
    [keyField]: payload.reward_key,
    title: payload.title,
    ...(payload.reward_type === "badge" ? { color: payload.color } : {}),
    ...(payload.reward_type === "certificate" ? {
      template_id: payload.template_id,
      recipient_name: payload.recipient_name,
      achievement_name: payload.achievement_name,
      achievement_description: payload.achievement_description,
      issue_date: payload.issue_date || null,
      formatted_issue_date: payload.formatted_issue_date,
      principal_name: payload.principal_name,
      teacher_name: payload.teacher_name,
      certificate_number: payload.certificate_number,
      notes: payload.notes,
      status: payload.status,
      certificate_data: payload.certificate_data
    } : {}),
    awarded_by: teacherId
  }, `Unable to save ${payload.reward_type}.`);
}

export async function createTeacherNotifications(env, input) {
  const eventType = cleanText(input.event_type).toLowerCase();
  if (!VALID_NOTIFICATION_EVENTS.includes(eventType)) {
    throw new ApiError("Notification event type is invalid.", 400);
  }

  const title = cleanText(input.title);
  const body = cleanText(input.body);
  if (!title || !body) {
    throw new ApiError("Notification title and body are required.", 400);
  }

  try {
    const teachers = await getApprovedTeachers(env);
    const rows = [];
    for (const teacher of teachers) {
      const settings = await getTeacherSettings(env, teacher.id);
      if (!isNotificationEventEnabled(eventType, settings)) continue;
      const row = await insertRow(env, "teacher_notifications", {
        teacher_id: teacher.id,
        student_id: input.student_id || null,
        event_type: eventType,
        title,
        body,
        entity_type: input.entity_type ? cleanText(input.entity_type).toLowerCase() : null,
        entity_id: input.entity_id || null,
        metadata: input.metadata && typeof input.metadata === "object" && !Array.isArray(input.metadata) ? input.metadata : {}
      }, "Unable to create teacher notification.");
      rows.push(row);
    }
    return rows;
  } catch (error) {
    if (isMissingOptionalTableError(error, "teacher_notifications")) return [];
    return [];
  }
}

export async function getTeacherNotifications(env, teacherId) {
  try {
    const [rows, settings] = await Promise.all([
      selectRows(
        env,
        "teacher_notifications",
        `teacher_id=eq.${encodeURIComponent(teacherId)}&select=*&order=created_at.desc&limit=60`
      ),
      getTeacherSettings(env, teacherId)
    ]);
    return rows.filter((notification) => isNotificationEventEnabled(notification.event_type, settings));
  } catch (error) {
    if (isMissingOptionalTableError(error, "teacher_notifications")) return [];
    throw error;
  }
}

export async function markTeacherNotificationRead(env, teacherId, notificationId) {
  const rows = await patchRows(
    env,
    "teacher_notifications",
    `id=eq.${encodeURIComponent(notificationId)}&teacher_id=eq.${encodeURIComponent(teacherId)}`,
    { read_at: new Date().toISOString() },
    "Unable to update notification."
  );
  if (!rows[0]) throw new ApiError("Notification was not found.", 404);
  return rows[0];
}

export async function markAllTeacherNotificationsRead(env, teacherId) {
  await patchRows(
    env,
    "teacher_notifications",
    `teacher_id=eq.${encodeURIComponent(teacherId)}&read_at=is.null`,
    { read_at: new Date().toISOString() },
    "Unable to update notifications.",
    false
  );
}

export async function createStudentNotification(env, input) {
  const eventType = cleanText(input.event_type).toLowerCase();
  if (!VALID_STUDENT_NOTIFICATION_EVENTS.includes(eventType)) {
    throw new ApiError("Student notification event type is invalid.", 400);
  }

  const studentId = String(input.student_id || "").trim();
  const title = cleanText(input.title);
  const body = cleanText(input.body);
  if (!studentId || !title || !body) {
    throw new ApiError("Student notification requires student, title, and body.", 400);
  }

  const entityType = input.entity_type ? cleanText(input.entity_type).toLowerCase() : "general";
  const entityId = input.entity_id || studentId;
  const metadata = input.metadata && typeof input.metadata === "object" && !Array.isArray(input.metadata)
    ? input.metadata
    : {};

  try {
    const existing = await selectRows(
      env,
      "student_notifications",
      [
        `student_id=eq.${encodeURIComponent(studentId)}`,
        `event_type=eq.${encodeURIComponent(eventType)}`,
        `entity_type=eq.${encodeURIComponent(entityType)}`,
        `entity_id=eq.${encodeURIComponent(entityId)}`,
        "select=*",
        "limit=1"
      ].join("&")
    );
    if (existing[0]) return existing[0];

    return insertRow(env, "student_notifications", {
      student_id: studentId,
      teacher_id: input.teacher_id || null,
      event_type: eventType,
      title,
      body,
      entity_type: entityType,
      entity_id: entityId,
      metadata
    }, "Unable to create student notification.");
  } catch (error) {
    if (isMissingOptionalTableError(error, "student_notifications")) return null;
    return null;
  }
}

export async function createStudentNotificationsForGrade(env, gradeLevel, input) {
  const grade = cleanText(gradeLevel);
  if (!VALID_GRADES.includes(grade)) return [];
  try {
    const students = await selectRows(
      env,
      "profiles",
      `role=eq.student&status=eq.approved&grade_level=eq.${encodeURIComponent(grade)}&select=id,full_name,username,grade_level`
    );
    const rows = [];
    for (const student of students) {
      const row = await createStudentNotification(env, {
        ...input,
        student_id: student.id,
        metadata: {
          ...(input.metadata && typeof input.metadata === "object" && !Array.isArray(input.metadata) ? input.metadata : {}),
          grade_level: grade
        }
      });
      if (row) rows.push(row);
    }
    return rows;
  } catch (error) {
    if (isMissingOptionalTableError(error, "student_notifications")) return [];
    return [];
  }
}

export async function getStudentNotifications(env, studentId) {
  try {
    return selectRows(
      env,
      "student_notifications",
      `student_id=eq.${encodeURIComponent(studentId)}&select=*&order=created_at.desc&limit=60`
    );
  } catch (error) {
    if (isMissingOptionalTableError(error, "student_notifications")) return [];
    throw error;
  }
}

export async function markStudentNotificationRead(env, studentId, notificationId) {
  const rows = await patchRows(
    env,
    "student_notifications",
    `id=eq.${encodeURIComponent(notificationId)}&student_id=eq.${encodeURIComponent(studentId)}`,
    { read_at: new Date().toISOString() },
    "Unable to update notification."
  );
  if (!rows[0]) throw new ApiError("Notification was not found.", 404);
  return rows[0];
}

export async function markAllStudentNotificationsRead(env, studentId) {
  await patchRows(
    env,
    "student_notifications",
    `student_id=eq.${encodeURIComponent(studentId)}&read_at=is.null`,
    { read_at: new Date().toISOString() },
    "Unable to update notifications.",
    false
  );
}

export async function getEvaluationCycles(env) {
  try {
    return selectRows(env, "evaluation_cycles", "select=*&order=grade_level.asc,created_at.desc");
  } catch (error) {
    if (isMissingOptionalTableError(error, "evaluation_cycles")) return [];
    throw error;
  }
}

export async function getEvaluationCycleById(env, cycleId) {
  const rows = await selectRows(env, "evaluation_cycles", `id=eq.${encodeURIComponent(cycleId)}&select=*&limit=1`);
  return rows[0] || null;
}

export async function getEvaluationSurveyResponses(env) {
  try {
    return selectRows(env, "evaluation_survey_responses", "select=*&order=submitted_at.desc");
  } catch (error) {
    if (isMissingOptionalTableError(error, "evaluation_survey_responses")) return [];
    throw error;
  }
}

export async function saveEvaluationCycle(env, teacherId, body) {
  const payload = cleanEvaluationCyclePayload(body);
  validateEvaluationCyclePayload(payload);
  const existing = await selectRows(
    env,
    "evaluation_cycles",
    `quarter_id=eq.${encodeURIComponent(payload.quarter_id)}&grade_level=eq.${encodeURIComponent(payload.grade_level)}&select=*&limit=1`
  );
  const patch = {
    pretest_lesson_id: payload.pretest_lesson_id,
    posttest_lesson_id: payload.posttest_lesson_id,
    survey_is_open: payload.survey_is_open,
    created_by: teacherId,
    updated_at: new Date().toISOString()
  };
  if (existing[0]) {
    const rows = await patchRows(
      env,
      "evaluation_cycles",
      `id=eq.${encodeURIComponent(existing[0].id)}`,
      patch,
      "Unable to update evaluation setup."
    );
    return rows[0];
  }
  return insertRow(env, "evaluation_cycles", {
    quarter_id: payload.quarter_id,
    grade_level: payload.grade_level,
    ...patch
  }, "Unable to save evaluation setup.");
}

export async function saveEvaluationSurveyResponse(env, cycle, respondent, role, body) {
  if (!cycle?.id) throw new ApiError("Saved test setup was not found.", 404);
  const respondentId = respondent?.id || "";
  const respondentRole = role === "teacher" ? "teacher" : "student";
  if (!respondentId) throw new ApiError("Respondent is required.", 400);
  if (respondentRole === "student" && !cycle.survey_is_open) {
    throw new ApiError("This feedback survey is closed.", 403);
  }
  if (respondentRole === "student" && respondent.grade_level !== cycle.grade_level) {
    throw new ApiError("This feedback survey is not assigned to your grade.", 403);
  }

  const payload = cleanEvaluationSurveyPayload(body);
  validateEvaluationSurveyPayload(payload);
  const existing = await selectRows(
    env,
    "evaluation_survey_responses",
    `cycle_id=eq.${encodeURIComponent(cycle.id)}&respondent_id=eq.${encodeURIComponent(respondentId)}&select=*&limit=1`
  );
  const row = {
    cycle_id: cycle.id,
    respondent_id: respondentId,
    respondent_role: respondentRole,
    answers: payload.answers,
    comment: payload.comment,
    submitted_at: new Date().toISOString(),
    updated_at: new Date().toISOString()
  };
  if (existing[0]) {
    const rows = await patchRows(
      env,
      "evaluation_survey_responses",
      `id=eq.${encodeURIComponent(existing[0].id)}`,
      row,
    "Unable to update feedback survey."
    );
    return rows[0];
  }
  return insertRow(env, "evaluation_survey_responses", row, "Unable to submit feedback survey.");
}

export async function requireTeacher(request, env) {
  const { token, profile } = await requireProfile(request, env);
  if (profile.role !== "teacher" || profile.status !== "approved") {
    throw new ApiError("Teacher access is required.", 403);
  }
  return { token, profile };
}

export async function requireProfile(request, env) {
  const token = getBearerToken(request);
  if (!token) {
    throw new ApiError("Please log in first.", 401);
  }

  const user = await getAuthUser(env, token);
  const profile = await getProfileById(env, user.id);
  if (!profile) {
    throw new ApiError("Profile was not found.", 404);
  }

  return { token, user, profile };
}

export function cleanStudentPayload(body) {
  const phoneNumber = normalizePhoneNumber(body.phone_number || body.cp_number);
  return {
    username: normalizeUsername(body.username),
    password: String(body.password || ""),
    full_name: [cleanText(body.first_name), cleanText(body.last_name)].filter(Boolean).join(" "),
    first_name: cleanText(body.first_name),
    last_name: cleanText(body.last_name),
    email: normalizeEmail(body.email),
    home_town: cleanText(body.home_town),
    grade_level: cleanText(body.grade_level),
    section_id: String(body.section_id || "").trim(),
    phone_number: phoneNumber,
    cp_number: phoneNumber
  };
}

export function validateStudentPayload(payload) {
  const required = [
    "username",
    "password",
    "full_name",
    "first_name",
    "last_name",
    "email",
    "home_town",
    "grade_level",
    "section_id",
    "phone_number"
  ];

  const missing = required.filter((field) => !payload[field]);
  if (missing.length > 0) {
    throw new ApiError("Please complete all required fields.", 400);
  }

  if (!/^[a-z0-9._-]{3,32}$/.test(payload.username)) {
    throw new ApiError("Username must be 3-32 characters and use only letters, numbers, dots, underscores, or hyphens.", 400);
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(payload.email)) {
    throw new ApiError("Please enter a valid email address.", 400);
  }

  if (!isStrongPassword(payload.password)) {
    throw new ApiError("Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.", 400);
  }

  if (!/^09\d{9}$/.test(payload.phone_number)) {
    throw new ApiError("Phone Number must be exactly 11 digits and start with 09.", 400);
  }

  if (!VALID_GRADES.includes(payload.grade_level)) {
    throw new ApiError("Grade level must be Grade 9 or Grade 10.", 400);
  }

}

export function publicProfile(profile) {
  return {
    id: profile.id,
    role: profile.role,
    status: profile.status,
    username: profile.username,
    email: profile.email,
    full_name: profile.full_name,
    first_name: profile.first_name,
    last_name: profile.last_name,
    home_town: profile.home_town,
    grade_level: profile.grade_level,
    section_id: profile.section_id || "",
    section: profile.section,
    adviser: profile.adviser,
    phone_number: profile.phone_number || profile.cp_number || "",
    cp_number: profile.phone_number || profile.cp_number || "",
    student_number: profile.student_number || "",
    avatar_path: profile.avatar_path || "",
    created_at: profile.created_at,
    updated_at: profile.updated_at
  };
}

export function publicQuarter(quarter) {
  return {
    id: quarter.id,
    name: quarter.name,
    title: quarter.title,
    school_year: quarter.school_year,
    academic_year_id: quarter.academic_year_id,
    start_date: quarter.start_date,
    end_date: quarter.end_date,
    is_active: quarter.is_active,
    status: quarter.status || "active",
    created_at: quarter.created_at,
    updated_at: quarter.updated_at
  };
}

export function publicModule(module) {
  return {
    id: module.id,
    quarter_id: module.quarter_id,
    title: module.title,
    description: module.description,
    category: module.category,
    grade_level: module.grade_level,
    status: module.status,
    sort_order: module.sort_order,
    created_at: module.created_at,
    updated_at: module.updated_at
  };
}

export function publicLesson(lesson) {
  return {
    id: lesson.id,
    module_id: lesson.module_id,
    quarter_id: lesson.quarter_id,
    title: lesson.title,
    description: lesson.description,
    lesson_type: lesson.lesson_type,
    status: lesson.status,
    duration_minutes: lesson.duration_minutes,
    resource_url: lesson.resource_url,
    scheduled_date: lesson.scheduled_date,
    due_date: lesson.due_date,
    sort_order: lesson.sort_order,
    created_at: lesson.created_at,
    updated_at: lesson.updated_at
  };
}

export function publicLessonFile(file) {
  return {
    id: file.id,
    lesson_id: file.lesson_id,
    storage_path: file.storage_path,
    original_filename: file.original_filename,
    mime_type: file.mime_type,
    size_bytes: file.size_bytes,
    uploaded_by: file.uploaded_by,
    created_at: file.created_at
  };
}

export function publicLessonSection(section) {
  return {
    id: section.id,
    lesson_id: section.lesson_id,
    section_type: section.section_type,
    title: section.title,
    body: section.body,
    media_url: section.media_url,
    sort_order: section.sort_order
  };
}

export function publicLessonQuestion(question, includeAnswer = false) {
  return {
    id: question.id,
    lesson_id: question.lesson_id,
    question_type: question.question_type,
    prompt: question.prompt,
    choices: Array.isArray(question.choices) ? question.choices : [],
    hint: question.hint,
    explanation: includeAnswer ? question.explanation : "",
    points: question.points,
    sort_order: question.sort_order,
    ...(includeAnswer ? { correct_answer: question.correct_answer } : {})
  };
}

export function publicLessonAttempt(attempt) {
  return {
    id: attempt.id,
    student_id: attempt.student_id,
    lesson_id: attempt.lesson_id,
    answers: attempt.answers,
    feedback: attempt.feedback,
    score_percent: attempt.score_percent,
    correct_count: attempt.correct_count,
    total_points: attempt.total_points,
    time_spent_seconds: attempt.time_spent_seconds,
    submitted_at: attempt.submitted_at
  };
}

export function publicProgress(progress) {
  return {
    id: progress.id,
    student_id: progress.student_id,
    lesson_id: progress.lesson_id,
    status: progress.status,
    progress_percent: progress.progress_percent,
    score_percent: progress.score_percent,
    started_at: progress.started_at,
    completed_at: progress.completed_at,
    updated_at: progress.updated_at
  };
}

export function publicBadge(badge) {
  return {
    id: badge.id,
    student_id: badge.student_id,
    badge_id: badge.badge_id || null,
    badge_key: badge.badge_key,
    title: badge.title,
    category: badge.category || "",
    description: badge.description || "",
    icon: badge.icon || "",
    color: badge.color,
    reason: badge.reason || "",
    source: badge.source || "manual",
    awarded_at: badge.awarded_at
  };
}

export function publicBadgeDefinition(badge) {
  return {
    id: badge.id,
    badge_key: badge.badge_key,
    title: badge.title,
    description: badge.description || "",
    category: badge.category || "achievement",
    color: badge.color || "blue",
    icon: badge.icon || "award",
    status: badge.status || "active",
    criteria: badge.criteria || {},
    created_by: badge.created_by || null,
    created_at: badge.created_at,
    updated_at: badge.updated_at
  };
}

export function publicVrCompetition(competition) {
  return {
    id: competition.id,
    title: competition.title,
    description: competition.description || "",
    simulation_type: competition.simulation_type || "assembly",
    ranking_method: competition.ranking_method || "score_time_mistakes",
    grade_level: competition.grade_level || "",
    section: competition.section || "",
    quarter_id: competition.quarter_id || "",
    start_at: competition.start_at || "",
    end_at: competition.end_at || "",
    attempts_allowed: competition.attempts_allowed || null,
    status: competition.status || "active",
    created_by: competition.created_by || null,
    created_at: competition.created_at,
    updated_at: competition.updated_at
  };
}

export function publicVrAttempt(attempt) {
  return {
    assessment_session_id: attempt.assessment_session_id || "",
    academic_year_id: attempt.academic_year_id || "",
    quarter_id: attempt.quarter_id || "",
    enrollment_id: attempt.enrollment_id || "",
    section_id: attempt.section_id || "",
    grade_level: attempt.grade_level || "",
    section_name: attempt.section_name || "",
    student_name: attempt.student_name || "",
    accuracy_percent: attempt.accuracy_percent == null ? null : Number(attempt.accuracy_percent),
    received_at: attempt.received_at || "",
    id: attempt.id,
    student_id: attempt.student_id,
    competition_id: attempt.competition_id || "",
    simulation_type: attempt.simulation_type,
    score_percent: Number(attempt.score_percent || 0),
    duration_seconds: Number(attempt.duration_seconds || 0),
    mistakes: Number(attempt.mistakes || 0),
    status: attempt.status || "completed",
    metadata: attempt.metadata || {},
    started_at: attempt.started_at || "",
    completed_at: attempt.completed_at,
    created_at: attempt.created_at
  };
}

export function publicCertificate(certificate) {
  return {
    id: certificate.id,
    student_id: certificate.student_id,
    certificate_key: certificate.certificate_key,
    title: certificate.title,
    template_id: certificate.template_id || "achievement",
    recipient_name: certificate.recipient_name || "",
    achievement_name: certificate.achievement_name || "",
    achievement_description: certificate.achievement_description || "",
    issue_date: certificate.issue_date || "",
    formatted_issue_date: certificate.formatted_issue_date || "",
    principal_name: certificate.principal_name || "",
    teacher_name: certificate.teacher_name || "",
    certificate_number: certificate.certificate_number || "",
    notes: certificate.notes || "",
    status: certificate.status || "issued",
    certificate_data: certificate.certificate_data || {},
    awarded_at: certificate.awarded_at
  };
}

export function publicTeacherNotification(notification) {
  return {
    id: notification.id,
    teacher_id: notification.teacher_id,
    student_id: notification.student_id,
    event_type: notification.event_type,
    title: notification.title,
    body: notification.body,
    entity_type: notification.entity_type,
    entity_id: notification.entity_id,
    metadata: notification.metadata || {},
    read_at: notification.read_at,
    created_at: notification.created_at
  };
}

export function publicStudentNotification(notification) {
  return {
    id: notification.id,
    student_id: notification.student_id,
    teacher_id: notification.teacher_id,
    event_type: notification.event_type,
    title: notification.title,
    body: notification.body,
    entity_type: notification.entity_type,
    entity_id: notification.entity_id,
    metadata: notification.metadata || {},
    read_at: notification.read_at,
    created_at: notification.created_at
  };
}

export function publicEvaluationCycle(cycle) {
  return {
    id: cycle.id,
    quarter_id: cycle.quarter_id,
    grade_level: cycle.grade_level,
    pretest_lesson_id: cycle.pretest_lesson_id,
    posttest_lesson_id: cycle.posttest_lesson_id,
    survey_is_open: Boolean(cycle.survey_is_open),
    created_by: cycle.created_by,
    created_at: cycle.created_at,
    updated_at: cycle.updated_at
  };
}

export function publicEvaluationSurveyResponse(response) {
  return {
    id: response.id,
    cycle_id: response.cycle_id,
    respondent_id: response.respondent_id,
    respondent_role: response.respondent_role,
    answers: response.answers || {},
    comment: response.comment || "",
    submitted_at: response.submitted_at,
    updated_at: response.updated_at
  };
}

export function publicTeacherSettings(settings) {
  return normalizeTeacherSettings(settings, settings?.teacher_id || "");
}

export function sessionPayload(session) {
  return {
    access_token: session.access_token,
    refresh_token: session.refresh_token,
    expires_in: session.expires_in,
    token_type: session.token_type
  };
}

export class ApiError extends Error {
  constructor(message, status = 400) {
    super(message);
    this.status = status;
  }
}

export async function selectRows(env, table, query) {
  const response = await supabaseFetch(env, `/rest/v1/${table}?${query}`, {
    admin: true
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || `Unable to load ${table}.`, response.status);
  }

  return response.json();
}

export async function insertRow(env, table, body, fallbackMessage, returnRepresentation = true) {
  const response = await supabaseFetch(env, `/rest/v1/${table}`, {
    admin: true,
    method: "POST",
    headers: returnRepresentation ? { Prefer: "return=representation" } : {},
    body
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || fallbackMessage, response.status);
  }

  if (!returnRepresentation) return null;
  const rows = await response.json();
  return rows[0];
}

export async function patchRows(env, table, query, body, fallbackMessage, returnRepresentation = true) {
  const response = await supabaseFetch(env, `/rest/v1/${table}?${query}`, {
    admin: true,
    method: "PATCH",
    headers: returnRepresentation ? { Prefer: "return=representation" } : {},
    body
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || fallbackMessage, response.status);
  }

  if (!returnRepresentation) return [];
  return response.json();
}

export async function deleteRows(env, table, query, fallbackMessage) {
  const response = await supabaseFetch(env, `/rest/v1/${table}?${query}`, {
    admin: true,
    method: "DELETE"
  });

  if (!response.ok) {
    const error = await parseSupabaseError(response);
    throw new ApiError(error || fallbackMessage, response.status);
  }
}

async function supabaseFetch(env, path, options = {}) {
  const key = options.admin ? env.SUPABASE_SERVICE_ROLE_KEY : env.SUPABASE_ANON_KEY;
  const headers = {
    apikey: key,
    Authorization: `Bearer ${options.token || key}`,
    "Content-Type": "application/json",
    ...(options.headers || {})
  };

  return fetch(`${env.SUPABASE_URL}${path}`, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined
  });
}

async function storageFetch(env, path, options = {}) {
  const headers = {
    apikey: env.SUPABASE_SERVICE_ROLE_KEY,
    Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
    ...(options.body ? { "Content-Type": "application/json" } : {}),
    ...(options.headers || {})
  };

  return fetch(`${env.SUPABASE_URL}/storage/v1${path}`, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined
  });
}

async function parseSupabaseError(response) {
  const text = await response.text();
  if (!text) return "";

  try {
    const data = JSON.parse(text);
    if (data.message?.includes("duplicate key")) {
      return "This record already exists.";
    }
    return data.error_description || data.msg || data.message || data.error || "";
  } catch {
    return text;
  }
}

function cleanQuarterPayload(body) {
  return {
    name: cleanText(body.name).toUpperCase(),
    title: cleanText(body.title),
    school_year: cleanText(body.school_year),
    start_date: body.start_date || null,
    end_date: body.end_date || null,
    is_active: body.is_active === true,
    status: body.status ? cleanText(body.status).toLowerCase() : "active"
  };
}

function validateQuarterPayload(payload) {
  if (!VALID_QUARTERS.includes(payload.name)) {
    throw new ApiError("Term must be 1st Term, 2nd Term, or 3rd Term.", 400);
  }
  if (!payload.title) {
    throw new ApiError("Term title is required.", 400);
  }
  if (!/^\d{4}-\d{4}$/.test(payload.school_year)) {
    throw new ApiError("School year must use the format 2026-2027.", 400);
  }
  if (!["active", "archived"].includes(payload.status)) {
    throw new ApiError("Term status is invalid.", 400);
  }
}

function cleanModulePayload(body, partial = false) {
  const payload = {
    quarter_id: body.quarter_id ? String(body.quarter_id).trim() : undefined,
    title: body.title !== undefined ? cleanText(body.title) : undefined,
    description: body.description !== undefined ? cleanText(body.description) : undefined,
    category: body.category !== undefined ? cleanText(body.category) : undefined,
    grade_level: body.grade_level !== undefined ? cleanText(body.grade_level) : undefined,
    status: body.status !== undefined ? cleanText(body.status).toLowerCase() : undefined,
    sort_order: body.sort_order !== undefined ? Number(body.sort_order) : undefined
  };
  return partial ? cleanPatch(payload) : {
    quarter_id: payload.quarter_id,
    title: payload.title,
    description: payload.description || null,
    category: payload.category || "Computer Basics",
    grade_level: payload.grade_level,
    status: payload.status || "draft",
    sort_order: Number.isFinite(payload.sort_order) ? payload.sort_order : 0
  };
}

function validateModulePayload(payload, partial = false) {
  if (!partial || payload.title !== undefined) {
    if (!payload.title) throw new ApiError("Module title is required.", 400);
  }
  if (!partial || payload.quarter_id !== undefined) {
    if (!payload.quarter_id) throw new ApiError("Term is required.", 400);
  }
  if (!partial || payload.grade_level !== undefined) {
    if (!VALID_GRADES.includes(payload.grade_level)) throw new ApiError("Module grade level must be Grade 9 or Grade 10.", 400);
  }
  if (!partial || payload.status !== undefined) {
    if (!VALID_CONTENT_STATUS.includes(payload.status)) throw new ApiError("Module status is invalid.", 400);
  }
  if (payload.sort_order !== undefined && (!Number.isInteger(payload.sort_order) || payload.sort_order < 0)) {
    throw new ApiError("Module order must be a whole number.", 400);
  }
}

function cleanLessonPayload(body, partial = false) {
  const payload = {
    module_id: body.module_id ? String(body.module_id).trim() : undefined,
    quarter_id: body.quarter_id ? String(body.quarter_id).trim() : undefined,
    title: body.title !== undefined ? cleanText(body.title) : undefined,
    description: body.description !== undefined ? cleanText(body.description) : undefined,
    lesson_type: body.lesson_type !== undefined ? cleanText(body.lesson_type).toLowerCase() : undefined,
    status: body.status !== undefined ? cleanText(body.status).toLowerCase() : undefined,
    duration_minutes: body.duration_minutes !== undefined ? Number(body.duration_minutes) : undefined,
    resource_url: body.resource_url !== undefined ? cleanText(body.resource_url) : undefined,
    scheduled_date: body.scheduled_date !== undefined ? cleanText(body.scheduled_date) || null : undefined,
    due_date: body.due_date !== undefined ? cleanText(body.due_date) || null : undefined,
    sort_order: body.sort_order !== undefined ? Number(body.sort_order) : undefined
  };
  return partial ? cleanPatch(payload) : {
    module_id: payload.module_id,
    quarter_id: payload.quarter_id || null,
    title: payload.title,
    description: payload.description || null,
    lesson_type: payload.lesson_type || "lesson",
    status: payload.status || "draft",
    duration_minutes: Number.isFinite(payload.duration_minutes) ? payload.duration_minutes : 30,
    resource_url: payload.resource_url || null,
    scheduled_date: payload.scheduled_date || null,
    due_date: payload.due_date || null,
    sort_order: Number.isFinite(payload.sort_order) ? payload.sort_order : 0
  };
}

function validateLessonPayload(payload, partial = false) {
  if (!partial || payload.module_id !== undefined) {
    if (!payload.module_id) throw new ApiError("Module is required.", 400);
  }
  if (!partial || payload.title !== undefined) {
    if (!payload.title) throw new ApiError("Lesson title is required.", 400);
  }
  if (!partial || payload.lesson_type !== undefined) {
    if (!VALID_LESSON_TYPES.includes(payload.lesson_type)) throw new ApiError("Lesson type is invalid.", 400);
  }
  if (!partial || payload.status !== undefined) {
    if (!VALID_CONTENT_STATUS.includes(payload.status)) throw new ApiError("Lesson status is invalid.", 400);
  }
  if (!partial || payload.duration_minutes !== undefined) {
    if (!Number.isInteger(payload.duration_minutes) || payload.duration_minutes < 1 || payload.duration_minutes > 600) {
      throw new ApiError("Lesson duration must be between 1 and 600 minutes.", 400);
    }
  }
  if (payload.sort_order !== undefined && (!Number.isInteger(payload.sort_order) || payload.sort_order < 0)) {
    throw new ApiError("Lesson order must be a whole number.", 400);
  }
  if (payload.resource_url) validateUrl(payload.resource_url);
  if (payload.scheduled_date) validateIsoDate(payload.scheduled_date, "Scheduled date");
  if (payload.due_date) validateIsoDate(payload.due_date, "Due date");
}

function cleanLessonUploadPayload(body) {
  return {
    filename: cleanText(body.filename),
    mime_type: cleanText(body.mime_type),
    size_bytes: Number(body.size_bytes)
  };
}

function validateLessonUploadPayload(payload) {
  if (!payload.filename) throw new ApiError("Filename is required.", 400);
  if (!VALID_LESSON_FILE_TYPES.includes(payload.mime_type)) throw new ApiError("Only PDF, DOCX, PPTX, and MP4 files are supported.", 400);
  if (!Number.isInteger(payload.size_bytes) || payload.size_bytes < 1 || payload.size_bytes > MAX_LESSON_FILE_BYTES) {
    throw new ApiError("Lesson file must be 100MB or smaller.", 400);
  }
}

function cleanLessonFilePayload(body) {
  return {
    lesson_id: body.lesson_id ? String(body.lesson_id).trim() : null,
    storage_path: cleanText(body.storage_path),
    original_filename: cleanText(body.original_filename),
    mime_type: cleanText(body.mime_type),
    size_bytes: Number(body.size_bytes)
  };
}

function validateLessonFilePayload(payload) {
  if (!payload.storage_path) throw new ApiError("Uploaded file path is required.", 400);
  if (!payload.original_filename) throw new ApiError("Original filename is required.", 400);
  if (!VALID_LESSON_FILE_TYPES.includes(payload.mime_type)) throw new ApiError("Only PDF, DOCX, PPTX, and MP4 files are supported.", 400);
  if (!Number.isInteger(payload.size_bytes) || payload.size_bytes < 1 || payload.size_bytes > MAX_LESSON_FILE_BYTES) {
    throw new ApiError("Lesson file must be 100MB or smaller.", 400);
  }
}

function cleanLessonSections(sections) {
  return sections.map((section, index) => ({
    section_type: cleanText(section.section_type || "paragraph").toLowerCase(),
    title: section.title !== undefined ? cleanText(section.title) || null : null,
    body: section.body !== undefined ? String(section.body || "").trim() || null : null,
    media_url: section.media_url !== undefined ? cleanText(section.media_url) || null : null,
    sort_order: Number.isInteger(Number(section.sort_order)) ? Number(section.sort_order) : index
  })).filter((section) => section.title || section.body || section.media_url);
}

function validateLessonSections(sections) {
  if (sections.length > 20) {
    throw new ApiError("A lesson can have up to 20 content blocks.", 400);
  }
  sections.forEach((section) => {
    if (!VALID_SECTION_TYPES.includes(section.section_type)) {
      throw new ApiError("Lesson content block type is invalid.", 400);
    }
    if (section.media_url) validateUrl(section.media_url);
    if (section.title && section.title.length > 120) {
      throw new ApiError("Content block titles must be 120 characters or fewer.", 400);
    }
    if (section.body && section.body.length > 4000) {
      throw new ApiError("Content block text must be 4000 characters or fewer.", 400);
    }
  });
}

function cleanLessonQuestions(questions) {
  return questions.map((question, index) => ({
    question_type: cleanText(question.question_type || "multiple_choice").toLowerCase(),
    prompt: String(question.prompt || "").trim(),
    choices: normalizeChoices(question.choices),
    correct_answer: cleanText(question.correct_answer),
    hint: question.hint !== undefined ? String(question.hint || "").trim() || null : null,
    explanation: question.explanation !== undefined ? String(question.explanation || "").trim() || null : null,
    points: Number.isInteger(Number(question.points)) ? Number(question.points) : 1,
    sort_order: Number.isInteger(Number(question.sort_order)) ? Number(question.sort_order) : index
  })).filter((question) => question.prompt || question.correct_answer);
}

function validateLessonQuestions(questions) {
  if (questions.length > 30) {
    throw new ApiError("A lesson can have up to 30 practice questions.", 400);
  }
  questions.forEach((question) => {
    if (!VALID_QUESTION_TYPES.includes(question.question_type)) {
      throw new ApiError("Practice question type is invalid.", 400);
    }
    if (!question.prompt) {
      throw new ApiError("Each practice question needs a prompt.", 400);
    }
    if (!question.correct_answer) {
      throw new ApiError("Each practice question needs a correct answer.", 400);
    }
    if (["multiple_choice", "ordering", "identification"].includes(question.question_type) && question.choices.length < 2) {
      throw new ApiError("Multiple choice questions need at least two choices.", 400);
    }
    if (question.question_type === "true_false") {
      question.choices = ["True", "False"];
      if (!["true", "false"].includes(question.correct_answer.toLowerCase())) {
        throw new ApiError("True/false correct answer must be True or False.", 400);
      }
    }
    if (question.points < 1 || question.points > 100) {
      throw new ApiError("Question points must be between 1 and 100.", 400);
    }
  });
}

function validateIsoDate(value, label) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || Number.isNaN(Date.parse(`${value}T00:00:00Z`))) {
    throw new ApiError(`${label} must be a valid date.`, 400);
  }
}

function cleanTeacherProfilePatch(body) {
  const phoneNumber = Object.prototype.hasOwnProperty.call(body, "phone_number")
    ? normalizePhoneNumber(body.phone_number)
    : undefined;
  const firstName = body.first_name !== undefined ? cleanText(body.first_name) : undefined;
  const lastName = body.last_name !== undefined ? cleanText(body.last_name) : undefined;
  const fullName = body.full_name !== undefined
    ? cleanText(body.full_name)
    : [firstName, lastName].filter(Boolean).join(" ") || undefined;

  return cleanPatch({
    full_name: fullName,
    first_name: firstName,
    last_name: lastName,
    phone_number: phoneNumber,
    cp_number: phoneNumber,
    updated_at: new Date().toISOString()
  });
}

function validateTeacherProfilePatch(patch) {
  if (patch.full_name !== undefined && !patch.full_name) {
    throw new ApiError("Full name is required.", 400);
  }
  if (patch.phone_number !== undefined && patch.phone_number && !/^09\d{9}$/.test(patch.phone_number)) {
    throw new ApiError("Phone Number must be exactly 11 digits and start with 09.", 400);
  }
}

function cleanTeacherSettingsPayload(body) {
  const hasPreferenceFields = body.notification_preferences && typeof body.notification_preferences === "object"
    || ["notify_student_accounts", "notify_lesson_completions", "notify_practice_submissions", "notify_achievement_awards"]
      .some((key) => Object.prototype.hasOwnProperty.call(body, key));
  const preferences = hasPreferenceFields
    ? (body.notification_preferences && typeof body.notification_preferences === "object"
      ? body.notification_preferences
      : {
        student_accounts: body.notify_student_accounts,
        lesson_completions: body.notify_lesson_completions,
        practice_submissions: body.notify_practice_submissions,
        achievement_awards: body.notify_achievement_awards
      })
    : null;
  return cleanPatch({
    notification_preferences: preferences ? normalizeNotificationPreferences(preferences) : undefined,
    default_grade: body.default_grade !== undefined ? cleanText(body.default_grade) || null : undefined,
    default_landing_view: body.default_landing_view !== undefined ? cleanText(body.default_landing_view).toLowerCase() : undefined,
    compact_lessons: body.compact_lessons !== undefined ? body.compact_lessons === true : undefined,
    report_default_range: body.report_default_range !== undefined ? cleanText(body.report_default_range).toLowerCase() : undefined,
    report_export_format: body.report_export_format !== undefined ? cleanText(body.report_export_format).toLowerCase() : undefined,
    report_default_grade: body.report_default_grade !== undefined ? cleanText(body.report_default_grade) || null : undefined
  });
}

function validateTeacherSettingsPayload(payload) {
  if (payload.default_grade !== undefined && payload.default_grade !== null && !VALID_GRADES.includes(payload.default_grade)) {
    throw new ApiError("Default grade must be Grade 9 or Grade 10.", 400);
  }
  if (payload.report_default_grade !== undefined && payload.report_default_grade !== null && !VALID_GRADES.includes(payload.report_default_grade)) {
    throw new ApiError("Default report class must be Grade 9 or Grade 10.", 400);
  }
  if (payload.default_landing_view !== undefined && !VALID_TEACHER_VIEWS.includes(payload.default_landing_view)) {
    throw new ApiError("Default landing section is invalid.", 400);
  }
  if (payload.report_default_range !== undefined && !VALID_REPORT_RANGES.includes(payload.report_default_range)) {
    throw new ApiError("Default report range is invalid.", 400);
  }
  if (payload.report_export_format !== undefined && !VALID_REPORT_FORMATS.includes(payload.report_export_format)) {
    throw new ApiError("Default export format is invalid.", 400);
  }
}

function cleanStudentManagementPatch(body) {
  const phoneNumber = Object.prototype.hasOwnProperty.call(body, "phone_number")
    ? normalizePhoneNumber(body.phone_number)
    : undefined;
  const firstName = body.first_name !== undefined ? cleanText(body.first_name) : undefined;
  const lastName = body.last_name !== undefined ? cleanText(body.last_name) : undefined;
  const fullName = body.full_name !== undefined
    ? cleanText(body.full_name)
    : [firstName, lastName].filter(Boolean).join(" ") || undefined;

  return cleanPatch({
    status: body.status !== undefined ? cleanText(body.status).toLowerCase() : undefined,
    full_name: fullName,
    first_name: firstName,
    last_name: lastName,
    grade_level: body.grade_level !== undefined ? cleanText(body.grade_level) : undefined,
    section: body.section !== undefined ? cleanText(body.section) : undefined,
    adviser: body.adviser !== undefined ? cleanText(body.adviser) : undefined,
    home_town: body.home_town !== undefined ? cleanText(body.home_town) : undefined,
    student_number: body.student_number !== undefined ? cleanText(body.student_number) : undefined,
    phone_number: phoneNumber,
    cp_number: phoneNumber,
    updated_at: new Date().toISOString()
  });
}

function validateStudentManagementPatch(patch) {
  if (patch.status !== undefined && !VALID_PROFILE_STATUS.includes(patch.status)) {
    throw new ApiError("Student status is invalid.", 400);
  }
  if (patch.grade_level !== undefined && !VALID_GRADES.includes(patch.grade_level)) {
    throw new ApiError("Grade level must be Grade 9 or Grade 10.", 400);
  }
  if (patch.phone_number !== undefined && patch.phone_number && !/^09\d{9}$/.test(patch.phone_number)) {
    throw new ApiError("Phone Number must be exactly 11 digits and start with 09.", 400);
  }
  if (patch.full_name !== undefined && !patch.full_name) {
    throw new ApiError("Full name is required.", 400);
  }
}

function cleanStudentSelfPatch(body) {
  const phoneNumber = Object.prototype.hasOwnProperty.call(body, "phone_number")
    ? normalizePhoneNumber(body.phone_number)
    : undefined;
  return cleanPatch({
    home_town: body.home_town !== undefined ? cleanText(body.home_town) : undefined,
    phone_number: phoneNumber,
    cp_number: phoneNumber,
    updated_at: new Date().toISOString()
  });
}

function validateStudentSelfPatch(patch) {
  if (patch.phone_number !== undefined && patch.phone_number && !/^09\d{9}$/.test(patch.phone_number)) {
    throw new ApiError("Phone Number must be exactly 11 digits and start with 09.", 400);
  }
}

function cleanAvatarUploadPayload(body) {
  return {
    filename: cleanText(body.filename || "avatar"),
    mime_type: cleanText(body.mime_type).toLowerCase(),
    size_bytes: Number(body.size_bytes || 0)
  };
}

function validateAvatarUploadPayload(payload) {
  if (!VALID_STUDENT_AVATAR_TYPES.includes(payload.mime_type)) {
    throw new ApiError("Profile picture must be a JPEG, PNG, or WebP image.", 400);
  }
  if (!Number.isFinite(payload.size_bytes) || payload.size_bytes < 1 || payload.size_bytes > MAX_STUDENT_AVATAR_BYTES) {
    throw new ApiError("Profile picture must be 2 MB or smaller.", 400);
  }
}

function cleanRewardPayload(body, teacherId) {
  const certificateData = body.certificate_data && typeof body.certificate_data === "object" && !Array.isArray(body.certificate_data)
    ? body.certificate_data
    : {};
  return {
    reward_type: cleanText(body.reward_type).toLowerCase(),
    action: cleanText(body.action || "add").toLowerCase(),
    student_id: String(body.student_id || "").trim(),
    reward_key: normalizeRewardKey(body.reward_key || body.badge_key || body.certificate_key || body.title),
    title: cleanText(body.title),
    color: cleanText(body.color || "blue").toLowerCase(),
    template_id: cleanText(body.template_id || "achievement").toLowerCase() || "achievement",
    recipient_name: cleanText(body.recipient_name),
    achievement_name: cleanText(body.achievement_name || body.title),
    achievement_description: cleanText(body.achievement_description),
    issue_date: String(body.issue_date || "").slice(0, 10),
    formatted_issue_date: cleanText(body.formatted_issue_date),
    principal_name: cleanText(body.principal_name),
    teacher_name: cleanText(body.teacher_name),
    certificate_number: cleanText(body.certificate_number),
    notes: cleanText(body.notes),
    status: cleanText(body.status || "issued").toLowerCase() || "issued",
    certificate_data: certificateData,
    awarded_by: teacherId
  };
}

function validateRewardPayload(payload) {
  if (!["badge", "certificate"].includes(payload.reward_type)) {
    throw new ApiError("Reward type must be badge or certificate.", 400);
  }
  if (!["add", "remove"].includes(payload.action)) {
    throw new ApiError("Reward action must be add or remove.", 400);
  }
  if (!payload.student_id) {
    throw new ApiError("Student id is required.", 400);
  }
  if (!payload.reward_key) {
    throw new ApiError("Reward key is required.", 400);
  }
  if (payload.action === "add" && !payload.title) {
    throw new ApiError("Reward title is required.", 400);
  }
  if (payload.reward_type === "badge" && !VALID_BADGE_COLORS.includes(payload.color)) {
    throw new ApiError("Badge color is invalid.", 400);
  }
  if (payload.reward_type === "certificate") {
    if (!["issued", "revoked"].includes(payload.status)) {
      throw new ApiError("Certificate status is invalid.", 400);
    }
    if (payload.issue_date && !/^\d{4}-\d{2}-\d{2}$/.test(payload.issue_date)) {
      throw new ApiError("Certificate issue date is invalid.", 400);
    }
  }
}

function cleanEvaluationCyclePayload(body) {
  return {
    quarter_id: body.quarter_id ? String(body.quarter_id).trim() : "",
    grade_level: cleanText(body.grade_level),
    pretest_lesson_id: body.pretest_lesson_id ? String(body.pretest_lesson_id).trim() : null,
    posttest_lesson_id: body.posttest_lesson_id ? String(body.posttest_lesson_id).trim() : null,
    survey_is_open: body.survey_is_open === true
  };
}

function validateEvaluationCyclePayload(payload) {
  if (!payload.quarter_id) {
    throw new ApiError("Active term is required.", 400);
  }
  if (!VALID_GRADES.includes(payload.grade_level)) {
    throw new ApiError("Grade must be Grade 9 or Grade 10.", 400);
  }
}

function cleanEvaluationSurveyPayload(body) {
  const source = body.answers && typeof body.answers === "object" && !Array.isArray(body.answers)
    ? body.answers
    : {};
  const answers = {};
  EVALUATION_SURVEY_ITEMS.forEach((item) => {
    const value = Number(source[item.key]);
    if (Number.isInteger(value)) answers[item.key] = value;
  });
  return {
    answers,
    comment: body.comment !== undefined ? String(body.comment || "").trim().slice(0, 1000) : ""
  };
}

function validateEvaluationSurveyPayload(payload) {
  const missing = EVALUATION_SURVEY_ITEMS.filter((item) => !Number.isInteger(payload.answers[item.key]));
  if (missing.length) {
    throw new ApiError("Please answer all feedback survey items.", 400);
  }
  const invalid = Object.values(payload.answers).some((value) => value < 1 || value > 5);
  if (invalid) {
    throw new ApiError("Survey answers must use ratings from 1 to 5.", 400);
  }
}

function validateUrl(value) {
  try {
    const url = new URL(value);
    if (!["http:", "https:"].includes(url.protocol)) throw new Error("Invalid protocol");
  } catch {
    throw new ApiError("Resource URL must be a valid http or https link.", 400);
  }
}

function normalizeFileName(value) {
  return cleanText(value)
    .replace(/[/\\?%*:|"<>]/g, "-")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 80) || "lesson-file";
}

function extensionForMime(mimeType, filename) {
  const existing = String(filename || "").match(/\.[a-z0-9]+$/i)?.[0]?.toLowerCase();
  const byMime = {
    "application/pdf": ".pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation": ".pptx",
    "video/mp4": ".mp4"
  };
  return byMime[mimeType] || existing || "";
}

function extensionForAvatarMime(mimeType) {
  return {
    "image/jpeg": ".jpg",
    "image/png": ".png",
    "image/webp": ".webp"
  }[mimeType] || "";
}

function encodeStoragePath(path) {
  return String(path).split("/").map(encodeURIComponent).join("/");
}

function absoluteSupabaseUrl(env, value) {
  if (!value) return "";
  if (/^https?:\/\//i.test(value)) return value;
  if (value.startsWith("/object/")) return `${env.SUPABASE_URL}/storage/v1${value}`;
  return `${env.SUPABASE_URL}${value.startsWith("/") ? "" : "/"}${value}`;
}

function isStrongPassword(value) {
  const password = String(value || "");
  return password.length >= 8
    && /[a-z]/.test(password)
    && /[A-Z]/.test(password)
    && /\d/.test(password)
    && /[^A-Za-z0-9]/.test(password);
}

function cleanPatch(payload) {
  return Object.fromEntries(Object.entries(payload).filter(([, value]) => value !== undefined));
}

function getBearerToken(request) {
  const header = request.headers.get("Authorization") || "";
  const match = header.match(/^Bearer\s+(.+)$/i);
  return match?.[1] || "";
}

function cleanText(value) {
  return String(value || "").trim().replace(/\s+/g, " ");
}

function normalizeUsername(value) {
  return cleanText(value).toLowerCase();
}

function normalizeEmail(value) {
  return cleanText(value).toLowerCase();
}

function normalizePhoneNumber(value) {
  return String(value || "").replace(/\D/g, "");
}

function normalizeChoices(value) {
  if (Array.isArray(value)) {
    return value.map((choice) => cleanText(choice)).filter(Boolean).slice(0, 8);
  }
  return String(value || "")
    .split(/\r?\n/)
    .map((choice) => cleanText(choice))
    .filter(Boolean)
    .slice(0, 8);
}

function normalizeAnswers(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new ApiError("Practice answers are required.", 400);
  }
  return Object.fromEntries(
    Object.entries(value).map(([key, answer]) => [key, cleanText(answer)])
  );
}

function isCorrectAnswer(question, answer) {
  const submitted = cleanText(answer).toLowerCase();
  const correct = cleanText(question.correct_answer).toLowerCase();
  if (["short_answer", "identification"].includes(question.question_type)) {
    return correct.split("|").map((item) => cleanText(item).toLowerCase()).includes(submitted);
  }
  if (question.question_type === "ordering") {
    return normalizeOrderAnswer(submitted) === normalizeOrderAnswer(correct);
  }
  return submitted === correct;
}

function normalizeOrderAnswer(value) {
  return String(value || "")
    .toLowerCase()
    .replace(/\s+/g, "")
    .replace(/[>;|]/g, ",")
    .replace(/,+/g, ",")
    .replace(/^,|,$/g, "");
}

function normalizeRewardKey(value) {
  return cleanText(value)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80);
}

function normalizeNotificationPreferences(value = {}) {
  return Object.fromEntries(
    Object.entries(DEFAULT_NOTIFICATION_PREFERENCES).map(([key, defaultValue]) => [
      key,
      typeof value[key] === "boolean" ? value[key] : defaultValue
    ])
  );
}

function normalizeTeacherSettings(row, teacherId) {
  return {
    teacher_id: row?.teacher_id || teacherId,
    notification_preferences: normalizeNotificationPreferences(row?.notification_preferences),
    default_grade: row?.default_grade || "Grade 10",
    default_landing_view: row?.default_landing_view || "overview",
    compact_lessons: Boolean(row?.compact_lessons),
    report_default_range: row?.report_default_range || "week",
    report_export_format: row?.report_export_format || "pdf",
    report_default_grade: row?.report_default_grade || "",
    created_at: row?.created_at || null,
    updated_at: row?.updated_at || null
  };
}

function isNotificationEventEnabled(eventType, settings) {
  const preferences = normalizeNotificationPreferences(settings?.notification_preferences);
  const key = eventType === "student_registered"
    ? "student_accounts"
    : eventType === "lesson_completed"
      ? "lesson_completions"
      : ["practice_submitted", "assessment_submitted"].includes(eventType)
        ? "practice_submissions"
        : ["badge_awarded", "certificate_awarded"].includes(eventType)
          ? "achievement_awards"
          : "";
  return key ? preferences[key] !== false : true;
}

function isMissingOptionalTableError(error, table) {
  const message = String(error?.message || "");
  return message.includes(table)
    && (message.includes("schema cache") || message.includes("relation") || message.includes("does not exist"));
}

function corsHeaders() {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, PATCH, DELETE, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Authorization"
  };
}
