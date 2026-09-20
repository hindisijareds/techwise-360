import {
  createClassSection,
  fail,
  getTeacherSectionsDashboard,
  json,
  optionsResponse,
  readJson,
  requireEnv,
  requireTeacher,
  updateClassSection
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const url = new URL(request.url);
    const schoolYear = String(url.searchParams.get("school_year") || "").trim();
    return json(await getTeacherSectionsDashboard(env, schoolYear));
  } catch (error) {
    return fail(error.message || "Unable to load sections.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const section = await createClassSection(env, await readJson(request), teacher.id);
    return json({ section }, 201);
  } catch (error) {
    return fail(error.message || "Unable to create section.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const section = await updateClassSection(env, await readJson(request), teacher.id);
    return json({ section });
  } catch (error) {
    return fail(error.message || "Unable to update section.", error.status || 500);
  }
}
