import {
  ApiError,
  assignStudentToSection,
  cleanStudentPayload,
  createTeacherNotifications,
  deleteAuthUser,
  fail,
  findProfileByEmail,
  findProfileByUsername,
  getRegistrationSections,
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
    const registrationData = await getRegistrationSections(env);
    const section = registrationData.sections.find((item) => item.id === payload.section_id);
    if (!section || section.grade_level !== payload.grade_level) {
      throw new ApiError("Please select an active section for your grade level and school year.", 400);
    }

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
        grade_level: section.grade_level,
        section_id: section.id,
        section: section.name,
        adviser: section.adviser_name,
        phone_number: payload.phone_number,
        cp_number: payload.phone_number
      });

      await assignStudentToSection(env, { student_id: profile.id, section_id: section.id }, null);

      await createTeacherNotifications(env, {
        event_type: "student_registered",
        student_id: profile.id,
        title: "New student account request",
        body: `${profile.full_name || profile.username} requested access for ${profile.grade_level || "class"}${profile.section ? ` - ${profile.section}` : ""}.`,
        entity_type: "profile",
        entity_id: profile.id,
        metadata: {
          grade_level: section.grade_level,
          section: section.name,
          section_id: section.id,
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
