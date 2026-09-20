import { ApiError, getProfileById, optionsResponse, requireEnv } from '../_utils.js';
import { hashToken, limitedJson, randomToken, rpc, studentContext, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestPost({request,env}) {
  try { requireEnv(env); const body=await limitedJson(request); const code=String(body.code||'').trim();
    if(!/^[a-f0-9]{64}$/.test(code)) throw new ApiError('Invalid connection code.',400);
    const token='twvr_'+randomToken(); const id=await rpc(env,'exchange_vr_code',{p_hash:await hashToken(code),p_token_hash:await hashToken(token)});
    const context=await studentContext(env,await getProfileById(env,id));
    return vrJson({...context,session:{access_token:token,token_type:'Bearer',expires_in:28800,expires_at:new Date(Date.now()+28800000).toISOString()}});
  } catch(e) { return vrFail(e); }
}
