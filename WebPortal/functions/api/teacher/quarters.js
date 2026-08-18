import {
  createQuarter,
  deleteQuarter,
  fail,
  getQuarters,
  json,
  optionsResponse,
  publicQuarter,
  readJson,
  requireEnv,
  requireTeacher,
  updateQuarter
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const quarters = await getQuarters(env);
    return json({ quarters: quarters.map(publicQuarter) });
  } catch (error) {
    return fail(error.message || "Unable to load terms.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const quarter = await createQuarter(env, body, teacher.id);
    return json({ quarter: publicQuarter(quarter) }, 201);
  } catch (error) {
    return fail(error.message || "Unable to create term.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const body = await readJson(request);
    const quarter = await updateQuarter(env, body);
    return json({ quarter: publicQuarter(quarter) });
  } catch (error) {
    return fail(error.message || "Unable to update term.", error.status || 500);
  }
}

export async function onRequestDelete({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const body = await readJson(request);
    await deleteQuarter(env, body);
    return json({ ok: true });
  } catch (error) {
    return fail(error.message || "Unable to delete term.", error.status || 500);
  }
}
