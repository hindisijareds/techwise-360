import {
  createModule,
  fail,
  getModules,
  json,
  optionsResponse,
  publicModule,
  readJson,
  requireEnv,
  requireTeacher,
  updateModule
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const modules = await getModules(env);
    return json({ modules: modules.map(publicModule) });
  } catch (error) {
    return fail(error.message || "Unable to load modules.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const module = await createModule(env, body, teacher.id);
    return json({ module: publicModule(module) }, 201);
  } catch (error) {
    return fail(error.message || "Unable to create module.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const body = await readJson(request);
    const module = await updateModule(env, body);
    return json({ module: publicModule(module) });
  } catch (error) {
    return fail(error.message || "Unable to update module.", error.status || 500);
  }
}
