import { ApiError, optionsResponse, requireEnv, requireTeacher, selectRows } from '../_utils.js';
import { limitedJson, uuid, vrFail, vrJson } from '../_vr.js';
import { academicRpc } from '../_academic.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestGet({request,env}) {
  try{requireEnv(env);await requireTeacher(request,env);return vrJson({years:await selectRows(env,'academic_years','select=*&order=name.desc')});}catch(e){return vrFail(e);}
}
export async function onRequestPost({request,env}) {
  try{
    requireEnv(env);await requireTeacher(request,env);const b=await limitedJson(request);
    if(b.id){b.id=uuid(b.id);if(!['activate','deactivate'].includes(b.action))throw new ApiError('Choose activate or deactivate.',400);}
    else if(!Number.isInteger(b.start_year)||b.start_year<2000||b.start_year>2199||b.end_year!==undefined&&b.end_year!==b.start_year+1)throw new ApiError('Use consecutive years, for example 2027-2028.',400);
    return vrJson({year:await academicRpc(env,'save_academic_year',{p_body:b})});
  }catch(e){return vrFail(e);}
}
