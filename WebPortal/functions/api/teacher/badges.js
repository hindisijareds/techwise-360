import {
  ApiError,
  fail,
  getStudentBadges,
  getStudents,
  insertRow,
  json,
  optionsResponse,
  patchRows,
  publicBadge,
  publicBadgeDefinition,
  publicProfile,
  readJson,
  requireEnv,
  requireTeacher,
  selectRows
} from "../_utils.js";

const BADGE_COLORS = ["blue", "green", "gold", "purple", "teal", "orange", "red"];
const CATEGORIES = ["achievement", "completion", "competition", "participation", "skill", "custom"];

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const [definitions, awards, students] = await Promise.all([
      selectRows(env, "badge_definitions", "select=*&order=created_at.desc"),
      getStudentBadges(env),
      getStudents(env)
    ]);
    return json({
      badges: definitions.map(publicBadgeDefinition),
      awards: awards.map(publicBadge),
      students: students.filter((student) => student.role === "student").map(publicProfile)
    });
  } catch (error) {
    return fail(error.message || "Unable to load badges.", error.status || 500);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const payload = cleanBadgePayload(await readJson(request), teacher.id);
    const badge = await insertRow(env, "badge_definitions", payload, "Unable to create badge.");
    return json({ badge: publicBadgeDefinition(badge) });
  } catch (error) {
    return fail(error.message || "Unable to create badge.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    await requireTeacher(request, env);
    const body = await readJson(request);
    const id = String(body.id || "").trim();
    if (!id) throw new ApiError("Badge id is required.", 400);
    const payload = cleanBadgePayload(body, "", true);
    const rows = await patchRows(env, "badge_definitions", `id=eq.${encodeURIComponent(id)}`, payload, "Unable to update badge.");
    return json({ badge: publicBadgeDefinition(rows[0]) });
  } catch (error) {
    return fail(error.message || "Unable to update badge.", error.status || 500);
  }
}

function cleanBadgePayload(body, teacherId, partial = false) {
  const title = cleanText(body.title);
  const badgeKey = normalizeKey(body.badge_key || title);
  const payload = {
    badge_key: badgeKey,
    title,
    description: cleanText(body.description),
    category: cleanText(body.category || "achievement").toLowerCase(),
    color: cleanText(body.color || "blue").toLowerCase(),
    icon: cleanText(body.icon || "award").toLowerCase() || "award",
    status: cleanText(body.status || "active").toLowerCase(),
    criteria: body.criteria && typeof body.criteria === "object" && !Array.isArray(body.criteria) ? body.criteria : {},
    updated_at: new Date().toISOString()
  };
  if (!partial) payload.created_by = teacherId;
  Object.keys(payload).forEach((key) => payload[key] === undefined && delete payload[key]);
  if (!payload.title && !partial) throw new ApiError("Badge title is required.", 400);
  if (!payload.badge_key && !partial) throw new ApiError("Badge key is required.", 400);
  if (payload.category && !CATEGORIES.includes(payload.category)) throw new ApiError("Badge category is invalid.", 400);
  if (payload.color && !BADGE_COLORS.includes(payload.color)) throw new ApiError("Badge color is invalid.", 400);
  if (payload.status && !["active", "archived"].includes(payload.status)) throw new ApiError("Badge status is invalid.", 400);
  return payload;
}

function cleanText(value) {
  return String(value || "").trim().replace(/\s+/g, " ").slice(0, 500);
}

function normalizeKey(value) {
  return cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 80);
}
