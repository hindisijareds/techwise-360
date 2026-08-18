import {
  ApiError,
  cleanStudentPayload,
  createTeacherNotifications,
  deleteAuthUser,
  fail,
  findProfileByEmail,
  findProfileByUsername,
  insertProfile,
  json,
  optionsResponse,
  readJson,
  registerAuthUser,
  requireEnv,
  validateStudentPayload
} from "./_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await readJson(request);
    const payload = cleanStudentPayload(body);
    validateStudentPayload(payload);

    const usernameTaken = await findProfileByUsername(env, payload.username);
    if (usernameTaken) {
      throw new ApiError("This username is already registered.", 409);
    }

    const emailTaken = await findProfileByEmail(env, payload.email);
    if (emailTaken) {
      throw new ApiError("This email address is already registered.", 409);
    }

    let authUser;
    try {
      authUser = await registerAuthUser(env, payload);
      const profile = await insertProfile(env, {
        id: authUser.id,
        role: "student",
        status: "pending",
        username: payload.username,
        email: payload.email,
        full_name: payload.full_name,
        first_name: payload.first_name,
        last_name: payload.last_name,
        home_town: payload.home_town,
        grade_level: payload.grade_level,
        section: payload.section,
        adviser: payload.adviser,
        phone_number: payload.phone_number,
        cp_number: payload.phone_number
      });

      await createTeacherNotifications(env, {
        event_type: "student_registered",
        student_id: profile.id,
        title: "New student account request",
        body: `${profile.full_name || profile.username} requested access for ${profile.grade_level || "class"}${profile.section ? ` - ${profile.section}` : ""}.`,
        entity_type: "profile",
        entity_id: profile.id,
        metadata: {
          grade_level: profile.grade_level,
          section: profile.section,
          email: profile.email
        }
      });

      return json({
        profile: {
          id: profile.id,
          status: profile.status,
          username: profile.username
        }
      }, 201);
    } catch (error) {
      if (authUser?.id) {
        await deleteAuthUser(env, authUser.id);
      }
      throw error;
    }
  } catch (error) {
    return fail(error.message || "Unable to register student.", error.status || 500);
  }
}
