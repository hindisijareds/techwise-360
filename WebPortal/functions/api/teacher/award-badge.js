import {
  ApiError,
  createStudentNotification,
  createTeacherNotifications,
  fail,
  getProfileById,
  insertRow,
  json,
  optionsResponse,
  patchRows,
  publicBadge,
  readJson,
  requireEnv,
  requireTeacher,
  selectRows
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const studentId = String(body.student_id || "").trim();
    const badgeId = String(body.badge_id || "").trim();
    if (!studentId || !badgeId) throw new ApiError("Student and badge are required.", 400);

    const [student, badgeRows] = await Promise.all([
      getProfileById(env, studentId),
      selectRows(env, "badge_definitions", `select=*&id=eq.${encodeURIComponent(badgeId)}`)
    ]);
    const badge = badgeRows[0];
    if (!student || student.role !== "student") throw new ApiError("Student was not found.", 404);
    if (!badge || badge.status !== "active") throw new ApiError("Active badge was not found.", 404);

    const reason = cleanText(body.reason);
    const existing = await selectRows(
      env,
      "student_badges",
      `select=*&student_id=eq.${encodeURIComponent(studentId)}&badge_key=eq.${encodeURIComponent(badge.badge_key)}`
    );
    const payload = {
      student_id: studentId,
      badge_id: badge.id,
      badge_key: badge.badge_key,
      title: badge.title,
      color: badge.color,
      category: badge.category,
      description: badge.description || "",
      icon: badge.icon || "award",
      reason,
      source: "manual",
      awarded_by: teacher.id,
      updated_at: new Date().toISOString()
    };
    const award = existing[0]
      ? (await patchRows(env, "student_badges", `id=eq.${encodeURIComponent(existing[0].id)}`, payload, "Unable to update badge award."))[0]
      : await insertRow(env, "student_badges", payload, "Unable to award badge.");

    await createTeacherNotifications(env, {
      event_type: "badge_awarded",
      student_id: studentId,
      title: "Badge awarded",
      body: `${student.full_name || student.username || "A student"} received ${badge.title}.`,
      entity_type: "badge",
      entity_id: award.id,
      metadata: { badge_key: badge.badge_key, badge_id: badge.id, reason }
    });
    await createStudentNotification(env, {
      student_id: studentId,
      teacher_id: teacher.id,
      event_type: "badge_awarded",
      title: "Badge awarded",
      body: `You received ${badge.title}.`,
      entity_type: "badge",
      entity_id: award.id,
      metadata: { badge_key: badge.badge_key, badge_id: badge.id, reason }
    });

    return json({ badge: publicBadge(award) });
  } catch (error) {
    return fail(error.message || "Unable to award badge.", error.status || 500);
  }
}

function cleanText(value) {
  return String(value || "").trim().replace(/\s+/g, " ").slice(0, 500);
}
