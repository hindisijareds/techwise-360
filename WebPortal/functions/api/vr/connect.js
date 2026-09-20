import { ApiError, optionsResponse, requireEnv, requireProfile } from '../_utils.js';
import { hashToken, randomToken, rpc, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestPost({request,env}) {
  try { requireEnv(env); const {profile}=await requireProfile(request,env);
    if(profile.role!=='student'||profile.status!=='approved') throw new ApiError('Approved student access is required.',403);
    const code=randomToken(); await rpc(env,'issue_vr_code',{p_student:profile.id,p_hash:await hashToken(code)});
    return vrJson({code,expires_in:120});
  } catch(e) { return vrFail(e); }
}
