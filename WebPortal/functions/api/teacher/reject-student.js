import {
  ApiError,
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

    const student = await updateStudentStatus(env, studentId, "rejected");
    await recordApprovalEvent(env, {
      student_id: student.id,
      teacher_id: teacher.id,
      action: "rejected",
      notes: String(body.notes || "").trim() || null
    });

    return json({ student: publicProfile(student) });
  } catch (error) {
    return fail(error.message || "Unable to reject student.", error.status || 500);
  }
}
