import { optionsResponse, requireEnv } from '../_utils.js';
import { requireVrStudent, studentContext, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestGet({request,env}) {
  try { requireEnv(env); return vrJson(await studentContext(env,await requireVrStudent(request,env))); }
  catch(e) { return vrFail(e); }
}
