import {
  fail,
  getRegistrationSections,
  json,
  optionsResponse,
  publicClassSection,
  publicQuarter,
  requireEnv
} from "./_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ env }) {
  try {
    requireEnv(env);
    const { activeQuarter, sections } = await getRegistrationSections(env);
    return json({
      active_term: activeQuarter ? publicQuarter(activeQuarter) : null,
      school_year: activeQuarter?.school_year || "",
      sections: sections.map(publicClassSection)
    });
  } catch (error) {
    return fail(error.message || "Unable to load registration sections.", error.status || 500);
  }
}
