import {
  assignStudentToSection,
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
    const result = await assignStudentToSection(env, await readJson(request), teacher.id);
    return json(result);
  } catch (error) {
    return fail(error.message || "Unable to update the section roster.", error.status || 500);
  }
}
