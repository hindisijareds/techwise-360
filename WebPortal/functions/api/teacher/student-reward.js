import {
  createStudentNotification,
  createTeacherNotifications,
  fail,
  getProfileById,
  json,
  optionsResponse,
  readJson,
  requireEnv,
  requireTeacher,
  saveStudentReward
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const reward = await saveStudentReward(env, body, teacher.id);
    if (!reward?.removed) {
      const rewardType = String(body.reward_type || "").toLowerCase() === "certificate" ? "certificate" : "badge";
      const studentId = reward.student_id || body.student_id;
      const student = studentId ? await getProfileById(env, studentId) : null;
      await createTeacherNotifications(env, {
        event_type: rewardType === "certificate" ? "certificate_awarded" : "badge_awarded",
        student_id: studentId,
        title: rewardType === "certificate" ? "Certificate awarded" : "Badge awarded",
        body: `${student?.full_name || student?.username || "A student"} received ${reward.title || body.title}.`,
        entity_type: rewardType,
        entity_id: reward.id,
        metadata: {
          reward_title: reward.title || body.title,
          reward_key: reward.badge_key || reward.certificate_key || body.reward_key,
          reward_type: rewardType
        }
      });
      if (studentId) {
        await createStudentNotification(env, {
          student_id: studentId,
          teacher_id: teacher.id,
          event_type: rewardType === "certificate" ? "certificate_awarded" : "badge_awarded",
          title: rewardType === "certificate" ? "Certificate awarded" : "Badge awarded",
          body: `You received ${reward.title || body.title}.`,
          entity_type: rewardType,
          entity_id: reward.id,
          metadata: {
            reward_title: reward.title || body.title,
            reward_key: reward.badge_key || reward.certificate_key || body.reward_key,
            reward_type: rewardType
          }
        });
      }
    }
    return json({ reward });
  } catch (error) {
    return fail(error.message || "Unable to save award.", error.status || 500);
  }
}
