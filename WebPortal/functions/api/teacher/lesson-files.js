import {
  fail,
  getLessonFiles,
  json,
  optionsResponse,
  publicLessonFile,
  readJson,
  requireEnv,
  requireTeacher,
  saveLessonFile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const files = await getLessonFiles(env);
    return json({ files: files.map(publicLessonFile) });
  } catch (error) {
    return fail(error.message || "Unable to load lesson files.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const file = await saveLessonFile(env, body, teacher.id);
    return json({ file: publicLessonFile(file) }, 201);
  } catch (error) {
    return fail(error.message || "Unable to save lesson file.", error.status || 500);
  }
}
