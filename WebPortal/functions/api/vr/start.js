import { ApiError, optionsResponse, requireEnv } from '../_utils.js';
import { limitedJson, requireVrStudent, rpc, SCORING, uuid, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestPost({request,env}) {
  try { requireEnv(env); const profile=await requireVrStudent(request,env); const b=await limitedJson(request);
    if(!['assembly','disassembly'].includes(b.simulation_type)) throw new ApiError('Choose a valid VR activity.',400);
    const attempt=await rpc(env,'start_vr_assessment',{p_student:profile.id,p_id:uuid(b.attempt_id),p_simulation:b.simulation_type,p_competition:b.competition_id?uuid(b.competition_id):null,p_scoring:SCORING});
    return vrJson({attempt});
  } catch(e) { return vrFail(e); }
}
