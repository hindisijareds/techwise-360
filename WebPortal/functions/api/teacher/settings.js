import {
  changeTeacherPassword,
  fail,
  getTeacherSettings,
  json,
  optionsResponse,
  publicProfile,
  publicTeacherSettings,
  readJson,
  requireEnv,
  requireTeacher,
  saveTeacherSettings,
  updateTeacherProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const settings = await getTeacherSettings(env, teacher.id);
    return json({
      profile: publicProfile(teacher),
      settings: publicTeacherSettings(settings)
    });
  } catch (error) {
    return fail(error.message || "Unable to load teacher settings.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const action = String(body.action || "settings").trim().toLowerCase();

    if (action === "password") {
      await changeTeacherPassword(env, teacher, body);
      return json({ ok: true });
    }

    const profile = body.profile ? await updateTeacherProfile(env, teacher.id, body.profile) : teacher;
    const settings = body.settings ? await saveTeacherSettings(env, teacher.id, body.settings) : await getTeacherSettings(env, teacher.id);
    return json({
      profile: publicProfile(profile),
      settings: publicTeacherSettings(settings)
    });
  } catch (error) {
    return fail(error.message || "Unable to update teacher settings.", error.status || 500);
  }
}
