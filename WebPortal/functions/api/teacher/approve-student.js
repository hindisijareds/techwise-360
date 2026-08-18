import {
  ApiError,
  createStudentNotification,
  fail,
  json,
  optionsResponse,
  publicProfile,
  readJson,
  recordApprovalEvent,
  requireEnv,
  requireTeacher,
  updateStudentStatus
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const studentId = String(body.student_id || "").trim();

    if (!studentId) {
      throw new ApiError("Student id is required.", 400);
    }

    const student = await updateStudentStatus(env, studentId, "approved");
    await recordApprovalEvent(env, {
      student_id: student.id,
      teacher_id: teacher.id,
      action: "approved",
      notes: String(body.notes || "").trim() || null
    });
    await createStudentNotification(env, {
      student_id: student.id,
      teacher_id: teacher.id,
      event_type: "account_approved",
      title: "Account approved",
      body: "Your TechWise 360 account has been approved. You can start learning now.",
      entity_type: "profile",
      entity_id: student.id,
      metadata: {
        status: "approved"
      }
    });

    return json({ student: publicProfile(student) });
  } catch (error) {
    return fail(error.message || "Unable to approve student.", error.status || 500);
  }
}
