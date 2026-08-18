import {
  fail,
  getPendingStudents,
  json,
  optionsResponse,
  publicProfile,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const students = await getPendingStudents(env);
    return json({ students: students.map(publicProfile) });
  } catch (error) {
    return fail(error.message || "Unable to load pending students.", error.status || 500);
  }
}
