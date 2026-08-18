import {
  createLessonUploadUrl,
  fail,
  json,
  optionsResponse,
  readJson,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const upload = await createLessonUploadUrl(env, body, teacher.id);
    return json(upload);
  } catch (error) {
    return fail(error.message || "Unable to prepare lesson upload.", error.status || 500);
  }
}
